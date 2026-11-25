using System;
using System.Collections.Generic;
using System.Linq;
using LabApi.Features.Wrappers;
using MEC;
using PlayerRoles;
using PlayerStatsSystem;
using UnityEngine;
using Utils;
using Footprinting;
using Random = System.Random;

namespace KikosMayhem.Core;

/// <summary>Random helpers used everywhere.</summary>
public static class Rng
{
    private static readonly Random R = new();

    public static float Value => (float)R.NextDouble();

    /// <summary>True with the given percent chance (0-100).</summary>
    public static bool Chance(float percent) => R.NextDouble() * 100.0 < percent;

    public static int Range(int minInclusive, int maxExclusive) => R.Next(minInclusive, maxExclusive);

    public static float Range(float min, float max) => min + (float)R.NextDouble() * (max - min);

    public static T Pick<T>(IList<T> list) => list.Count == 0 ? default : list[R.Next(list.Count)];

    public static T Pick<T>(IEnumerable<T> items)
    {
        var list = items as IList<T> ?? items.ToList();
        return Pick(list);
    }

    /// <summary>Weighted random pick.</summary>
    public static T Weighted<T>(IEnumerable<(T item, float weight)> entries)
    {
        var list = entries.Where(e => e.weight > 0).ToList();
        float total = list.Sum(e => e.weight);
        if (list.Count == 0) return default;
        float roll = Value * total;
        foreach (var (item, weight) in list)
        {
            roll -= weight;
            if (roll <= 0) return item;
        }
        return list[list.Count - 1].item;
    }

    public static List<T> Shuffle<T>(IEnumerable<T> items)
    {
        var list = items.ToList();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = R.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}

public static class PlayerExt
{
    public static bool Valid(this Player p) => p != null && p.IsOnline && p.ReferenceHub != null;

    public static bool IsAliveHuman(this Player p) => p.Valid() && p.IsAlive && p.IsHuman;

    /// <summary>Puppet counts as a human-ish SCP-side player.</summary>
    public static bool IsPuppet(this Player p) => Scp079.PuppetManager.IsPuppet(p);

    /// <summary>True if the two players are on opposing sides (Mayhem rules: Puppet + SCPs are one side, MYHM insurgents are enemies of everyone else).</summary>
    public static bool IsEnemyOf(this Player a, Player b)
    {
        if (!a.Valid() || !b.Valid() || a == b) return false;
        if (!a.IsAlive || !b.IsAlive) return false;
        return Side(a) != Side(b);
    }

    public static int Side(Player p)
    {
        if (Events.MyhmInsurgency.IsInsurgent(p)) return 100;
        if (p.IsPuppet() || p.IsSCP) return 1;
        return p.Faction switch
        {
            Faction.FoundationStaff => 2,
            Faction.FoundationEnemy => 3,
            Faction.Flamingos => 4,
            _ => 5,
        };
    }

    /// <summary>Moves a pickup into the player's inventory (keeps the serial, so custom items stay custom).</summary>
    public static Item Take(this Player p, Pickup pickup)
    {
        if (pickup == null || pickup.IsDestroyed || p.IsInventoryFull) return null;
        var item = p.AddItem(pickup);
        if (item != null) pickup.Destroy();
        return item;
    }

    /// <summary>False for the Puppet during the recontainment run.</summary>
    public static bool CanTeleport(this Player p) => p.Valid() && !Scp079.PuppetManager.NoTeleport(p);

    public static bool IsScpOrPuppet(this Player p) => p.Valid() && (p.IsSCP || p.IsPuppet());

    public static IEnumerable<Player> Alive => Player.ReadyList.Where(p => p.IsAlive);

    public static IEnumerable<Player> AliveHumans => Player.ReadyList.Where(p => p.IsAliveHuman());

    public static IEnumerable<Player> Spectators => Player.ReadyList.Where(p => !p.IsAlive && p.Role != RoleTypeId.Overwatch && !p.IsHost);

    public static IEnumerable<Player> EnemiesOf(Player p) => Alive.Where(o => o.IsEnemyOf(p));

    public static IEnumerable<Player> Near(Vector3 pos, float radius) =>
        Alive.Where(o => (o.Position - pos).sqrMagnitude <= radius * radius);

    /// <summary>Deals damage with a custom death reason. Attacker optional.</summary>
    public static void Hurt(this Player target, float amount, string reason, Player attacker = null)
    {
        if (!target.Valid() || !target.IsAlive) return;
        if (attacker != null && attacker.Valid())
            target.Damage(amount, attacker);
        else
            target.Damage(amount, reason);
    }

    /// <summary>"True damage": removes health directly, bypassing AHP, hume shield and damage reduction.</summary>
    public static void TrueDamage(this Player target, float amount, string reason)
    {
        if (!target.Valid() || !target.IsAlive) return;
        if (target.Health - amount <= 0f)
            target.Kill(reason);
        else
            target.Health -= amount;
    }

    public static void Explode(Vector3 position, Player owner, ExplosionType type = ExplosionType.Grenade)
    {
        var footprint = owner != null && owner.Valid() ? new Footprint(owner.ReferenceHub) : new Footprint(Server.Host?.ReferenceHub);
        ExplosionUtils.ServerExplode(position, footprint, type);
    }

    public static void Explode(this Player p, ExplosionType type = ExplosionType.Grenade) =>
        Explode(p.Position, p, type);

    /// <summary>Raycast along the player's view; returns the hit point (or the max range point).</summary>
    public static Vector3 AimPoint(this Player p, float maxDistance = 100f)
    {
        var cam = p.Camera;
        const int mask = ~((1 << 13) | (1 << 16) | (1 << 17) | (1 << 2)); // ignore players/hitboxes/triggers
        return Physics.Raycast(cam.position + cam.forward * 0.5f, cam.forward, out var hit, maxDistance, mask)
            ? hit.point
            : cam.position + cam.forward * maxDistance;
    }

    public static string Hex(this Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}

/// <summary>Small wrapper around MEC so modules can cleanly cancel loops on round restart.</summary>
public static class Loops
{
    private static readonly List<CoroutineHandle> RoundHandles = new();

    public static CoroutineHandle Every(float interval, Action action, string tag = null)
    {
        var h = Timing.RunCoroutine(EveryRoutine(interval, action, tag));
        RoundHandles.Add(h);
        return h;
    }

    public static CoroutineHandle After(float delay, Action action)
    {
        var h = Timing.CallDelayed(delay, () =>
        {
            try { action(); }
            catch (Exception e) { LabApi.Features.Console.Logger.Error($"[Mayhem] delayed action failed: {e}"); }
        });
        RoundHandles.Add(h);
        return h;
    }

    public static CoroutineHandle Run(IEnumerator<float> routine)
    {
        var h = Timing.RunCoroutine(Safe(routine));
        RoundHandles.Add(h);
        return h;
    }

    private static IEnumerator<float> Safe(IEnumerator<float> inner)
    {
        while (true)
        {
            bool more;
            try { more = inner.MoveNext(); }
            catch (Exception e)
            {
                LabApi.Features.Console.Logger.Error($"[Mayhem] coroutine crashed: {e}");
                yield break;
            }
            if (!more) yield break;
            yield return inner.Current;
        }
    }

    private static IEnumerator<float> EveryRoutine(float interval, Action action, string tag)
    {
        while (true)
        {
            yield return Timing.WaitForSeconds(interval);
            try { action(); }
            catch (Exception e) { LabApi.Features.Console.Logger.Error($"[Mayhem] loop '{tag}' error: {e}"); }
        }
    }

    public static void KillRoundLoops()
    {
        foreach (var h in RoundHandles) Timing.KillCoroutines(h);
        RoundHandles.Clear();
    }
}
