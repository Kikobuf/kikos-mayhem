using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace KikosMayhem.Audio;

/// <summary>
/// Audio slots. Drop .wav files (any sample rate, mono or stereo, 16-bit PCM) into
/// LabAPI/configs/&lt;port&gt;/KikosMayhem/audio/ named after the slot (e.g. gas_gas_gas.wav).
/// Missing files are skipped silently, so the mode works with no audio at all.
///
/// Slots used: gas_gas_gas, dubstep, mayhem_protocol, mayhem_insurgency, redacted_protocol,
/// hells_reckoning, ascension, recontainment, phone_ring, server_rack_hum, pc_beep,
/// keypad_beep, keypad_accept, keypad_deny, radiance_revive, red_right_hand.
/// </summary>
public static class Clips
{
    private static readonly Dictionary<string, float[]> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Missing = new(StringComparer.OrdinalIgnoreCase);
    public static string Folder;

    public static float[] Get(string slot)
    {
        if (string.IsNullOrEmpty(slot) || Folder == null) return null;
        if (Cache.TryGetValue(slot, out var c)) return c;
        if (Missing.Contains(slot)) return null;
        string path = Path.Combine(Folder, slot + ".wav");
        if (!File.Exists(path))
        {
            // Built-in sounds (phone voice lines, ascension SFX) ship inside the DLL; a file in the folder overrides them.
            var builtIn = LoadEmbedded(slot);
            if (builtIn != null) { Cache[slot] = builtIn; return builtIn; }
            Missing.Add(slot);
            return null;
        }
        try
        {
            var samples = Wav.Load(path);
            Cache[slot] = samples;
            return samples;
        }
        catch (Exception e)
        {
            Logger.Warn($"[Mayhem] couldn't load audio '{path}': {e.Message}");
            Missing.Add(slot);
            return null;
        }
    }

    public static void Reset()
    {
        Cache.Clear();
        Missing.Clear();
    }

    private static float[] LoadEmbedded(string slot)
    {
        try
        {
            using var s = typeof(Clips).Assembly.GetManifestResourceStream("KikosMayhem.Audio." + slot + ".wav");
            return s == null ? null : Wav.Load(s);
        }
        catch (Exception e)
        {
            Logger.Warn($"[Mayhem] couldn't load built-in audio '{slot}': {e.Message}");
            return null;
        }
    }

    /// <summary>Joins clips back to back with a gap between them. Null if any clip is missing.</summary>
    public static float[] Concat(IEnumerable<string> slots, float gapSeconds)
    {
        var parts = new List<float[]>();
        foreach (var slot in slots)
        {
            var c = Get(slot);
            if (c == null) return null;
            parts.Add(c);
        }
        int gap = (int)(gapSeconds * 48000);
        var res = new float[parts.Sum(c => c.Length) + gap * Math.Max(parts.Count - 1, 0)];
        int pos = 0;
        foreach (var c in parts)
        {
            Array.Copy(c, 0, res, pos, c.Length);
            pos += c.Length + gap;
        }
        return res;
    }
}

internal static class Wav
{
    private const int TargetRate = 48000;

    public static float[] Load(string path)
    {
        using var f = File.OpenRead(path);
        return Load(f);
    }

    public static float[] Load(Stream stream)
    {
        using var br = new BinaryReader(stream);
        if (new string(br.ReadChars(4)) != "RIFF") throw new InvalidDataException("not a RIFF file");
        br.ReadInt32();
        if (new string(br.ReadChars(4)) != "WAVE") throw new InvalidDataException("not a WAVE file");

        int channels = 1, rate = TargetRate, bits = 16;
        byte[] data = null;
        while (br.BaseStream.Position < br.BaseStream.Length - 8)
        {
            string id = new string(br.ReadChars(4));
            int size = br.ReadInt32();
            if (id == "fmt ")
            {
                short format = br.ReadInt16();
                channels = br.ReadInt16();
                rate = br.ReadInt32();
                br.ReadInt32();
                br.ReadInt16();
                bits = br.ReadInt16();
                if (size > 16) br.ReadBytes(size - 16);
                if (format != 1) throw new InvalidDataException("only PCM wav is supported (export as 16-bit PCM)");
            }
            else if (id == "data")
            {
                data = br.ReadBytes(size);
            }
            else br.ReadBytes(size);
            if ((size & 1) == 1 && br.BaseStream.Position < br.BaseStream.Length) br.ReadByte();
        }
        if (data == null) throw new InvalidDataException("no data chunk");
        if (bits != 16) throw new InvalidDataException("only 16-bit wav is supported");

        int frames = data.Length / 2 / channels;
        var mono = new float[frames];
        for (int i = 0; i < frames; i++)
        {
            float sum = 0;
            for (int c = 0; c < channels; c++)
                sum += BitConverter.ToInt16(data, (i * channels + c) * 2) / 32768f;
            mono[i] = sum / channels;
        }
        if (rate == TargetRate) return mono;

        // Linear resample to 48 kHz.
        int outLen = (int)((long)frames * TargetRate / rate);
        var res = new float[outLen];
        double step = (double)rate / TargetRate;
        for (int i = 0; i < outLen; i++)
        {
            double src = i * step;
            int a = (int)src;
            int b = Math.Min(a + 1, frames - 1);
            float t = (float)(src - a);
            res[i] = mono[a] * (1 - t) + mono[b] * t;
        }
        return res;
    }
}

internal static class Speakers
{
    private static byte _next = 40;

    public static byte NextId()
    {
        _next++;
        if (_next >= 250) _next = 41;
        return _next;
    }
}

/// <summary>Short positional sounds.</summary>
public static class Sfx
{
    public static SpeakerToy Play(string slot, Vector3 position, float maxDistance = 25f, bool loop = false)
        => PlayClip(Clips.Get(slot), position, maxDistance, loop);

    public static SpeakerToy PlayClip(float[] clip, Vector3 position, float maxDistance = 25f, bool loop = false)
    {
        if (clip == null) return null;
        var sp = SpeakerToy.Create(position);
        sp.ControllerId = Speakers.NextId();
        sp.IsSpatial = true;
        sp.MinDistance = 1f;
        sp.MaxDistance = maxDistance;
        sp.Volume = MayhemPlugin.Cfg.Audio.Volume;
        sp.Play(clip, false, loop);
        if (!loop)
            Core.Loops.After(clip.Length / 48000f + 0.5f, () => { if (!sp.IsDestroyed) sp.Destroy(); });
        return sp;
    }

    public static SpeakerToy Attach(string slot, Transform parent, float maxDistance = 25f, bool loop = true)
    {
        var clip = Clips.Get(slot);
        if (clip == null) return null;
        var sp = SpeakerToy.Create(parent);
        sp.ControllerId = Speakers.NextId();
        sp.IsSpatial = true;
        sp.MinDistance = 1f;
        sp.MaxDistance = maxDistance;
        sp.Volume = MayhemPlugin.Cfg.Audio.Volume;
        sp.Play(clip, false, loop);
        return sp;
    }
}

/// <summary>Music: global tracks (protocols) and tracks attached to a player (Gas Gas Gas, Dubstep, Reckoning).</summary>
public static class Music
{
    private static SpeakerToy _global;
    private static readonly Dictionary<ReferenceHub, SpeakerToy> OnPlayer = new();

    public static void PlayGlobal(string slot, bool loop = false)
    {
        StopGlobal();
        var clip = Clips.Get(slot);
        if (clip == null) return;
        _global = SpeakerToy.Create(Vector3.zero);
        _global.ControllerId = 30;
        _global.IsSpatial = false;
        _global.Volume = MayhemPlugin.Cfg.Audio.MusicVolume;
        _global.Play(clip, false, loop);
    }

    public static void StopGlobal()
    {
        if (_global != null && !_global.IsDestroyed)
        {
            _global.Stop();
            _global.Destroy();
        }
        _global = null;
    }

    public static void PlayOn(Player p, string slot, bool loop = false, float range = 30f)
    {
        StopOn(p);
        var sp = Sfx.Attach(slot, p.GameObject.transform, range, loop);
        if (sp != null) OnPlayer[p.ReferenceHub] = sp;
    }

    public static void StopOn(Player p)
    {
        if (p == null || !OnPlayer.TryGetValue(p.ReferenceHub, out var sp)) return;
        OnPlayer.Remove(p.ReferenceHub);
        if (!sp.IsDestroyed)
        {
            sp.Stop();
            sp.Destroy();
        }
    }

    public static void Reset()
    {
        StopGlobal();
        OnPlayer.Clear();
    }
}
