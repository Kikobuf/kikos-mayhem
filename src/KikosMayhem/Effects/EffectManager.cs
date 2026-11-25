using System.Collections.Generic;
using System.Linq;
using KikosMayhem.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using UnityEngine;

namespace KikosMayhem.Effects;

public enum EffectKind { Positive, Mixed, Negative, Hidden }

/// <summary>
/// A server-side Mayhem status effect. Shown on the HUD (green / purple / red) with a timer,
/// like Bright's custom status effect API. Instances hold per-player state.
/// </summary>
public abstract class CustomEffect
{
    public Player Owner { get; internal set; }
    public float Intensity { get; set; } = 1;
    public float Expires { get; set; } // 0 = permanent
    public float AppliedAt { get; internal set; }

    public abstract string Name { get; }
    public abstract EffectKind Kind { get; }

    /// <summary>Stacking effects add intensity when re-applied instead of replacing it.</summary>
    public virtual bool Stacks => false;
    public virtual float MaxIntensity => 255;
    public virtual bool ShowIntensity => Stacks;

    public float Remaining => Expires <= 0 ? -1 : Mathf.Max(0, Expires - Time.time);

    public virtual void OnApplied() { }
    public virtual void OnTick() { }              // every 1s
    public virtual void OnRemoved() { }

    /// <summary>Owner is attacking someone.</summary>
    public virtual void OnOwnerAttacking(Player target, PlayerHurtingEventArgs ev) { }

    /// <summary>Owner is being hurt.</summary>
    public virtual void OnOwnerHurting(Player attacker, PlayerHurtingEventArgs ev) { }

    /// <summary>Owner is about to die; set ev.IsAllowed = false to prevent.</summary>
    public virtual void OnOwnerDying(PlayerDyingEventArgs ev) { }

    public virtual string Display()
    {
        string color = Kind switch
        {
            EffectKind.Positive => "#5CFF5C",
            EffectKind.Mixed => "#C77DFF",
            EffectKind.Negative => "#FF5C5C",
            _ => "#FFFFFF",
        };
        string text = Name;
        if (ShowIntensity && Intensity > 1) text += $" x{Intensity:0.#}";
        if (Remaining >= 0) text += $" <size=14>({Remaining:0}s)</size>";
        return $"<color={color}>{text}</color>";
    }
}

public static class EffectManager
{
    private static readonly Dictionary<ReferenceHub, List<CustomEffect>> Effects = new();

    public static IEnumerable<CustomEffect> Of(Player p) =>
        p.Valid() && Effects.TryGetValue(p.ReferenceHub, out var l) ? l : Enumerable.Empty<CustomEffect>();

    public static T Get<T>(Player p) where T : CustomEffect => Of(p).OfType<T>().FirstOrDefault();

    public static bool Has<T>(Player p) where T : CustomEffect => Get<T>(p) != null;

    /// <summary>
    /// Applies an effect. Negative effects respect negative-effect immunity and reduction.
    /// duration &lt;= 0 means permanent (until death or removal).
    /// </summary>
    public static T Apply<T>(Player p, float duration = 0, float intensity = 1, bool addDuration = false) where T : CustomEffect, new()
    {
        if (!p.Valid() || !p.IsAlive) return null;

        if (typeof(T) != typeof(CustomEffect))
        {
            var probe = new T();
            if (probe.Kind == EffectKind.Negative)
            {
                var st = Stats.Of(p);
                if (st.NegativeImmune) return null;
                if (duration > 0 && st.NegativeReduction > 0)
                    duration *= 1f - st.NegativeReduction / 100f;
            }
        }

        if (!Effects.TryGetValue(p.ReferenceHub, out var list)) Effects[p.ReferenceHub] = list = new();

        var existing = list.OfType<T>().FirstOrDefault();
        if (existing != null)
        {
            existing.Intensity = existing.Stacks
                ? Mathf.Min(existing.MaxIntensity, existing.Intensity + intensity)
                : Mathf.Max(existing.Intensity, intensity);
            if (duration <= 0) existing.Expires = 0;
            else if (existing.Expires > 0)
                existing.Expires = addDuration ? existing.Expires + duration : Mathf.Max(existing.Expires, Time.time + duration);
            return existing;
        }

        var e = new T
        {
            Owner = p,
            Intensity = intensity,
            Expires = duration > 0 ? Time.time + duration : 0,
            AppliedAt = Time.time,
        };
        list.Add(e);
        e.OnApplied();
        return e;
    }

    public static void Remove<T>(Player p) where T : CustomEffect
    {
        if (!p.Valid() || !Effects.TryGetValue(p.ReferenceHub, out var list)) return;
        foreach (var e in list.OfType<T>().ToList())
        {
            list.Remove(e);
            e.OnRemoved();
        }
    }

    public static void Remove(Player p, CustomEffect effect)
    {
        if (!p.Valid() || !Effects.TryGetValue(p.ReferenceHub, out var list)) return;
        if (list.Remove(effect)) effect.OnRemoved();
    }

    public static void RemoveAll(Player p, bool silent = false)
    {
        if (!Effects.TryGetValue(p.ReferenceHub, out var list)) return;
        Effects.Remove(p.ReferenceHub);
        if (!silent) foreach (var e in list) e.OnRemoved();
    }

    /// <summary>Removes and returns all custom effects (used by the Soberizer steal and Mayhem Coin shuffle).</summary>
    public static List<CustomEffect> Take(Player p)
    {
        if (!Effects.TryGetValue(p.ReferenceHub, out var list)) return new();
        Effects.Remove(p.ReferenceHub);
        foreach (var e in list) e.OnRemoved();
        return list;
    }

    /// <summary>Gives already-constructed effects to a player (keeps remaining time).</summary>
    public static void Give(Player p, IEnumerable<CustomEffect> effects)
    {
        if (!Effects.TryGetValue(p.ReferenceHub, out var list)) Effects[p.ReferenceHub] = list = new();
        foreach (var e in effects)
        {
            if (list.Any(x => x.GetType() == e.GetType())) continue;
            e.Owner = p;
            list.Add(e);
            e.OnApplied();
        }
    }

    public static void Clear() => Effects.Clear();

    public static IEnumerable<string> Describe(Player p) =>
        Of(p).Where(e => e.Kind != EffectKind.Hidden).Select(e => e.Display());

    public static void Tick()
    {
        float now = Time.time;
        foreach (var kv in Effects.ToList())
        {
            var p = Player.Get(kv.Key);
            if (p == null || !p.IsAlive)
            {
                Effects.Remove(kv.Key);
                continue;
            }
            foreach (var e in kv.Value.ToList())
            {
                if (e.Expires > 0 && e.Expires <= now)
                {
                    kv.Value.Remove(e);
                    e.OnRemoved();
                    continue;
                }
                e.OnTick();
            }
        }
    }

    // Combat hooks (called from Combat).
    internal static void Attacking(Player attacker, Player target, PlayerHurtingEventArgs ev)
    {
        foreach (var e in Of(attacker).ToList()) e.OnOwnerAttacking(target, ev);
    }

    internal static void Hurting(Player target, Player attacker, PlayerHurtingEventArgs ev)
    {
        foreach (var e in Of(target).ToList()) e.OnOwnerHurting(attacker, ev);
    }

    internal static void Dying(PlayerDyingEventArgs ev)
    {
        foreach (var e in Of(ev.Player).ToList())
        {
            e.OnOwnerDying(ev);
            if (!ev.IsAllowed) return;
        }
    }
}
