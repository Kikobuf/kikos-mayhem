using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabApi.Features.Wrappers;
using UnityEngine;

namespace KikosMayhem.Core;

/// <summary>
/// One hint per player, re-sent every 0.5s, composed from several regions so Mayhem systems
/// never overwrite each other's hints:
///   - Top: global event banner
///   - Right: status effects with timers + crit stats
///   - Center: short-lived messages (crits, "Try again", item notifications)
///   - Bottom: held custom item description, trackers, Puppet / 079 info
/// </summary>
public static class Hud
{
    private sealed class Line
    {
        public string Text;
        public float Expires;
        public int Order;
    }

    private static readonly Dictionary<ReferenceHub, Dictionary<string, Line>> Bottom = new();
    private static readonly Dictionary<ReferenceHub, Dictionary<string, Line>> Center = new();

    public static string Banner = "";

    /// <summary>Region for persistent per-player info lines (keyed so callers can update them).</summary>
    public static void SetInfo(Player p, string key, string text, float duration = 1.2f, int order = 0) =>
        Put(Bottom, p, key, text, duration, order);

    public static void ClearInfo(Player p, string key)
    {
        if (Bottom.TryGetValue(p.ReferenceHub, out var d)) d.Remove(key);
    }

    /// <summary>A short message in the middle of the screen.</summary>
    public static void Message(Player p, string text, float duration = 3f, string key = null) =>
        Put(Center, p, key ?? text, text, duration, 0);

    public static void MessageAll(string text, float duration = 4f)
    {
        foreach (var p in Player.ReadyList) Message(p, text, duration);
    }

    private static void Put(Dictionary<ReferenceHub, Dictionary<string, Line>> region, Player p, string key, string text, float duration, int order)
    {
        if (!p.Valid()) return;
        if (!region.TryGetValue(p.ReferenceHub, out var d)) region[p.ReferenceHub] = d = new();
        d[key] = new Line { Text = text, Expires = Time.time + duration, Order = order };
    }

    public static void Clear()
    {
        Bottom.Clear();
        Center.Clear();
        Banner = "";
    }

    public static void Tick()
    {
        float now = Time.time;
        foreach (var p in Player.ReadyList)
        {
            if (p.IsHost || p.IsDummy) continue;
            var sb = new StringBuilder(512);

            // Top banner (global event).
            if (!string.IsNullOrEmpty(Banner))
                sb.Append("<voffset=33em><size=22>").Append(Banner).Append("</size></voffset>\n");

            // Right: effects
            var effects = Effects.EffectManager.Describe(p).ToList();
            if (p.IsAlive)
            {
                var s = Stats.Of(p);
                if (s.HasAnyCrit)
                    effects.Add($"<color=#7CFC00>Crit {s.CritChance:0.#}% · {s.CritMultiplier:0.##}x</color>");
            }
            if (effects.Count > 0)
            {
                sb.Append("<align=right><voffset=20em><size=18>");
                foreach (var e in effects) sb.Append(e).Append('\n');
                sb.Append("</size></voffset></align>\n");
            }

            // Center messages
            if (Center.TryGetValue(p.ReferenceHub, out var center))
            {
                Prune(center, now);
                if (center.Count > 0)
                {
                    sb.Append("<voffset=8em><size=26>");
                    foreach (var l in center.Values) sb.Append(l.Text).Append('\n');
                    sb.Append("</size></voffset>\n");
                }
            }

            // Bottom info
            if (Bottom.TryGetValue(p.ReferenceHub, out var bottom))
            {
                Prune(bottom, now);
                if (bottom.Count > 0)
                {
                    sb.Append("<voffset=-6em><size=20>");
                    foreach (var l in bottom.Values.OrderBy(x => x.Order)) sb.Append(l.Text).Append('\n');
                    sb.Append("</size></voffset>");
                }
            }

            if (sb.Length > 0) p.SendHint(sb.ToString(), 1.1f);
        }
    }

    private static void Prune(Dictionary<string, Line> d, float now)
    {
        List<string> dead = null;
        foreach (var kv in d)
            if (kv.Value.Expires < now) (dead ??= new()).Add(kv.Key);
        if (dead != null) foreach (var k in dead) d.Remove(k);
    }

    public static string Color(string text, string hex) => $"<color={hex}>{text}</color>";
}
