using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using LabApi.Features.Wrappers;
using UnityEngine;

namespace KikosMayhem.Core;

/// <summary>
/// Per-life player data. Every Mayhem system that "stacks" a stat (movement boost, damage
/// reduction, crits, negative-effect reduction) registers a named source here, and the
/// stat ticker turns the sum into base-game effects. That way items never fight over the
/// same MovementBoost / DamageReduction effect.
/// </summary>
public sealed class PlayerState
{
    public readonly Player Player;
    public readonly int LifeId;

    public PlayerState(Player p)
    {
        Player = p;
        LifeId = p.LifeId;
    }

    private readonly Dictionary<string, Source> _speed = new();
    private readonly Dictionary<string, Source> _dr = new();
    private readonly Dictionary<string, Source> _critChance = new();
    private readonly Dictionary<string, Source> _critMult = new();
    private readonly Dictionary<string, Source> _negReduction = new();

    /// <summary>Flat negative-effect immunity until this time.</summary>
    public float NegativeImmuneUntil;

    /// <summary>Disables crits from items like Crimson Echo.</summary>
    public bool Flagged(string flag) => Flags.Contains(flag);
    public readonly HashSet<string> Flags = new();

    /// <summary>Arbitrary data bag for items/effects.</summary>
    public readonly Dictionary<string, object> Data = new();

    public T Get<T>(string key, T fallback = default) => Data.TryGetValue(key, out var v) && v is T t ? t : fallback;
    public void Set(string key, object value) => Data[key] = value;

    private struct Source
    {
        public float Value;
        public float Expires; // 0 = permanent for this life
    }

    private static void Put(Dictionary<string, Source> d, string key, float value, float duration)
    {
        d[key] = new Source { Value = value, Expires = duration > 0 ? Time.time + duration : 0 };
    }

    private static void Add(Dictionary<string, Source> d, string key, float delta, float cap)
    {
        d.TryGetValue(key, out var s);
        s.Value = Mathf.Min(cap, s.Value + delta);
        d[key] = s;
    }

    private static float Sum(Dictionary<string, Source> d)
    {
        float now = Time.time;
        float total = 0;
        List<string> dead = null;
        foreach (var kv in d)
        {
            if (kv.Value.Expires > 0 && kv.Value.Expires < now) (dead ??= new()).Add(kv.Key);
            else total += kv.Value.Value;
        }
        if (dead != null) foreach (var k in dead) d.Remove(k);
        return total;
    }

    // Movement boost, in percent (MovementBoost intensity).
    public void SetSpeed(string key, float pct, float duration = 0) => Put(_speed, key, pct, duration);
    public void AddSpeed(string key, float pct, float cap = 255) => Add(_speed, key, pct, cap);
    public void RemoveSpeed(string key) => _speed.Remove(key);
    public float Speed => Sum(_speed);

    // Damage reduction for ALL damage, in percent.
    public void SetDamageReduction(string key, float pct, float duration = 0) => Put(_dr, key, pct, duration);
    public void AddDamageReduction(string key, float pct, float cap) => Add(_dr, key, pct, cap);
    public void RemoveDamageReduction(string key) => _dr.Remove(key);
    public float DamageReduction => Mathf.Min(99f, Sum(_dr));

    // Crit chance (percent) and crit multiplier BONUS (1.8x == +0.8).
    public void SetCrit(string key, float chancePct, float multBonus, float duration = 0)
    {
        Put(_critChance, key, chancePct, duration);
        Put(_critMult, key, multBonus, duration);
    }
    public void AddCrit(string key, float chancePct, float multBonus, float chanceCap = 100, float multCap = 50)
    {
        Add(_critChance, key, chancePct, chanceCap);
        Add(_critMult, key, multBonus, multCap);
    }
    public void RemoveCrit(string key)
    {
        _critChance.Remove(key);
        _critMult.Remove(key);
    }
    public float CritChance => Mathf.Min(100f, Sum(_critChance));
    public float CritMultiplier => 1f + Sum(_critMult);

    // Negative effect reduction, percent.
    public void SetNegativeReduction(string key, float pct, float duration = 0) => Put(_negReduction, key, pct, duration);
    public float NegativeReduction => Mathf.Min(100f, Sum(_negReduction));
    public bool NegativeImmune => NegativeImmuneUntil > Time.time;

    public bool HasAnyCrit => CritChance > 0 && CritMultiplier > 1f;
}

public static class Stats
{
    private static readonly Dictionary<ReferenceHub, PlayerState> States = new();

    /// <summary>Gets the state for the player's current life (resets automatically on a new life).</summary>
    public static PlayerState Of(Player p)
    {
        if (!States.TryGetValue(p.ReferenceHub, out var s) || s.LifeId != p.LifeId)
        {
            s = new PlayerState(p);
            States[p.ReferenceHub] = s;
        }
        return s;
    }

    public static void Clear() => States.Clear();

    public static void Forget(Player p) => States.Remove(p.ReferenceHub);

    /// <summary>Base-game effects that count as "negative" for reduction / immunity.</summary>
    public static readonly HashSet<string> NegativeEffects = new()
    {
        nameof(Asphyxiated), nameof(Bleeding), nameof(Blindness), nameof(Burned), nameof(Concussed),
        nameof(Corroding), nameof(Deafened), nameof(Disabled), nameof(Ensnared), nameof(Exhausted),
        nameof(Flashed), nameof(Hemorrhage), nameof(Poisoned), nameof(SeveredHands), nameof(Sinkhole),
        nameof(Stained), nameof(Slowness), nameof(Traumatized), nameof(AmnesiaItems), nameof(AmnesiaVision),
        nameof(CardiacArrest), nameof(Blurred), nameof(HeavyFooted),
    };

    /// <summary>Applies summed stat sources to base-game effects. Runs every 0.5s.</summary>
    public static void Tick()
    {
        foreach (var p in Player.ReadyList)
        {
            if (!p.IsAlive) continue;
            var s = Of(p);

            int speed = Mathf.Clamp(Mathf.RoundToInt(s.Speed), 0, 255);
            ApplyIntensity<MovementBoost>(p, (byte)speed);

            // DamageReduction is 0.5% per intensity point.
            int dr = Mathf.Clamp(Mathf.RoundToInt(s.DamageReduction * 2f), 0, 200);
            ApplyIntensity<DamageReduction>(p, (byte)dr);
        }
    }

    private static void ApplyIntensity<T>(Player p, byte intensity) where T : StatusEffectBase
    {
        if (!p.TryGetEffect<T>(out var eff)) return;
        if (eff.Intensity == intensity) return;
        if (intensity == 0) eff.DisableEffect();
        else eff.ServerSetState(intensity, 0f, false);
    }
}
