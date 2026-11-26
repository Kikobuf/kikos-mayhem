using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabApi.Features.Wrappers;
using UnityEngine;

namespace KikosMayhem.Core;

/// <summary>
/// One hint per player, re-sent every 0.5s, composed from several regions so Mayhem systems
/// never overwrite each other's hints:
///   - Top: global event banner, and under it a short pop-up (SCP powers at spawn)
///   - Top left: persistent panels (the Puppet's generator list)
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
    private static readonly Dictionary<ReferenceHub, Dictionary<string, Line>> TopLeft = new();
    private static readonly Dictionary<ReferenceHub, Dictionary<string, Line>> TopPopup = new();

    /// <summary>A keyed panel in the top-left corner (re-set it every tick to keep it up).</summary>
    public static void SetTopLeft(Player p, string key, string text, float duration = 1.2f, int order = 0) =>
        Put(TopLeft, p, key, text, duration, order);

    /// <summary>Self-test helpers.</summary>
    public static string PeekTopLeft(Player p) => TopLeft.TryGetValue(p.ReferenceHub, out var d) ? string.Join(" | ", d.Values.Select(l => l.Text)) : "";
    public static string PeekPopup(Player p) => TopPopup.TryGetValue(p.ReferenceHub, out var d) ? string.Join(" | ", d.Values.Select(l => l.Text)) : "";

    public static void ClearTopLeft(Player p, string key)
    {
        if (TopLeft.TryGetValue(p.ReferenceHub, out var d)) d.Remove(key);
    }

    /// <summary>A pop-up at the top middle of the screen (below the event banner).</summary>
    public static void Popup(Player p, string text, float duration = 5f) => Put(TopPopup, p, "popup", text, duration, 0);

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
        TopLeft.Clear();
        TopPopup.Clear();
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

            // Top middle pop-up.
            if (TopPopup.TryGetValue(p.ReferenceHub, out var popup))
            {
                Prune(popup, now);
                foreach (var l in popup.Values)
                    sb.Append("<voffset=29em><size=20>").Append(l.Text).Append("</size></voffset>\n");
            }

            // Top left panels.
            if (TopLeft.TryGetValue(p.ReferenceHub, out var topLeft))
            {
                Prune(topLeft, now);
                if (topLeft.Count > 0)
                {
                    sb.Append("<align=left><voffset=27em><size=19>");
                    foreach (var l in topLeft.Values.OrderBy(x => x.Order)) sb.Append(l.Text).Append('\n');
                    sb.Append("</size></voffset></align>\n");
                }
            }

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
