using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using KikosMayhem.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using PlayerRoles;
using UnityEngine;

namespace KikosMayhem.Effects;

// ───────────── Positive ─────────────

/// <summary>Pick up and use items faster (handled in Items.ItemEngine via search/use hooks).</summary>
public sealed class Manic : CustomEffect
{
    public override string Name => "Manic";
    public override EffectKind Kind => EffectKind.Positive;
}

/// <summary>Tracks every enemy in the facility and shows the nearest one (Huginn and Muninn).</summary>
public sealed class EyesOfOdin : CustomEffect
{
    public override string Name => "Eyes Of Odin";
    public override EffectKind Kind => EffectKind.Positive;

    public override void OnTick()
    {
        var enemies = PlayerExt.EnemiesOf(Owner).ToList();
        if (enemies.Count == 0)
        {
            Hud.SetInfo(Owner, "odin", "<color=#00FFFF>👁 No enemies detected</color>", 1.5f, 5);
            return;
        }
        var nearest = enemies.OrderBy(e => (e.Position - Owner.Position).sqrMagnitude).First();
        float d = Vector3.Distance(nearest.Position, Owner.Position);
        string zone = nearest.Zone.ToString();
        Hud.SetInfo(Owner, "odin", $"<color=#00FFFF>👁 {enemies.Count} enemies · nearest: {nearest.Role} in {zone} ({d:0}m)</color>", 1.5f, 5);
    }

    public override void OnRemoved() => Hud.ClearInfo(Owner, "odin");
}

/// <summary>Appear as each viewer's own role. Red when revealed (after dealing damage).</summary>
public sealed class SkinwalkerEffect : CustomEffect
{
    public float RevealedUntil;
    public override string Name => Time.time < RevealedUntil ? "<color=#FF3030>Skinwalker</color>" : "Skinwalker";
    public override EffectKind Kind => EffectKind.Positive;

    public bool Active => Time.time >= RevealedUntil && !Warhead.IsDetonated;

    public override void OnApplied() =>
        Disguise.Set(Owner, (t, viewer) => Active ? viewer.Role : null);

    public override void OnRemoved() => Disguise.Clear(Owner);

    public override void OnOwnerAttacking(Player target, PlayerHurtingEventArgs ev) =>
        RevealedUntil = Time.time + MayhemPlugin.Cfg.Items.SkinwalkerRevealSeconds;
}

/// <summary>Guardian's Glow: regen + small AHP, and one prevented death (3s invincibility, then 120s cooldown).</summary>
public sealed class Radiance : CustomEffect
{
    public override string Name => "Radiance";
    public override EffectKind Kind => EffectKind.Positive;

    public override void OnTick()
    {
        if (Owner.Health < Owner.MaxHealth) Owner.Heal(MayhemPlugin.Cfg.Items.RadianceHealPerSecond);
        if (Owner.ArtificialHealth < MayhemPlugin.Cfg.Items.RadianceMaxAhp)
            Owner.ArtificialHealth = Mathf.Min(MayhemPlugin.Cfg.Items.RadianceMaxAhp, Owner.ArtificialHealth + 2f);
    }

    public override void OnOwnerDying(PlayerDyingEventArgs ev)
    {
        if (EffectManager.Has<ReviveCooldown>(Owner) || GasGasGas.IsActive(Owner)) return;
        ev.IsAllowed = false;
        Owner.Health = 1;
        EffectManager.Apply<Invincible>(Owner, 3f);
        EffectManager.Apply<ReviveCooldown>(Owner, MayhemPlugin.Cfg.Items.RadianceReviveCooldown);
        Audio.Sfx.Play("radiance_revive", Owner.Position);
        Hud.Message(Owner, "<color=#FFE066>✦ Radiance saved you!</color>");
    }
}

/// <summary>Takes no damage at all (Radiance save, Puppet HS break, etc).</summary>
public sealed class Invincible : CustomEffect
{
    public override string Name => "Invincible";
    public override EffectKind Kind => EffectKind.Positive;
    public override void OnOwnerHurting(Player attacker, PlayerHurtingEventArgs ev) => ev.IsAllowed = false;
    public override void OnOwnerDying(PlayerDyingEventArgs ev) => ev.IsAllowed = false;
}

/// <summary>Heals based on the number of friendlies nearby.</summary>
public sealed class Herd : CustomEffect
{
    public override string Name => "Herd";
    public override EffectKind Kind => EffectKind.Positive;

    public override void OnTick()
    {
        float r = MayhemPlugin.Cfg.Effects.HerdRange;
        int friends = PlayerExt.Near(Owner.Position, r).Count(o => o != Owner && !o.IsEnemyOf(Owner));
        if (friends > 0) Owner.Heal(friends * MayhemPlugin.Cfg.Effects.HerdHealPerFriend);
    }
}

// ───────────── Mixed ─────────────

/// <summary>Every hit on an enemy gives you one random effect per stack.</summary>
public sealed class TurboDrunk : CustomEffect
{
    public override string Name => "Turbo Drunk";
    public override EffectKind Kind => EffectKind.Mixed;
    public override bool Stacks => true;

    public override void OnOwnerAttacking(Player target, PlayerHurtingEventArgs ev)
    {
        if (!target.IsEnemyOf(Owner)) return; // drunk ignores teammates (7/19/2024)
        for (int i = 0; i < Mathf.Max(1, (int)Intensity); i++)
            RandomEffects.GiveRandom(Owner, 10f, allowExplode: false);
    }
}

// ───────────── Negative ─────────────

/// <summary>Camera shake, can't use or pull out items, barely able to move.</summary>
public sealed class Tazed : CustomEffect
{
    public override string Name => "Tazed";
    public override EffectKind Kind => EffectKind.Negative;

    public override void OnApplied()
    {
        float d = Remaining > 0 ? Remaining : 3f;
        Owner.EnableEffect<Slowness>(80, d);
        Owner.CurrentItem = null;
    }

    public override void OnRemoved()
    {
        if (Owner.IsAlive) Owner.DisableEffect<Slowness>();
    }

    /// <summary>Blocks equipping, using, shooting and throwing while Tazed.</summary>
    public static void Register()
    {
        LabApi.Events.Handlers.PlayerEvents.ChangingItem += Block;
        LabApi.Events.Handlers.PlayerEvents.UsingItem += Block;
        LabApi.Events.Handlers.PlayerEvents.ShootingWeapon += Block;
        LabApi.Events.Handlers.PlayerEvents.ThrowingProjectile += Block;
    }

    public static void Unregister()
    {
        LabApi.Events.Handlers.PlayerEvents.ChangingItem -= Block;
        LabApi.Events.Handlers.PlayerEvents.UsingItem -= Block;
        LabApi.Events.Handlers.PlayerEvents.ShootingWeapon -= Block;
        LabApi.Events.Handlers.PlayerEvents.ThrowingProjectile -= Block;
    }

    private static void Block<T>(T ev) where T : LabApi.Events.Arguments.Interfaces.IPlayerEvent, LabApi.Events.Arguments.Interfaces.ICancellableEvent
    {
        if (EffectManager.Has<Tazed>(ev.Player)) ev.IsAllowed = false;
    }
}

/// <summary>
/// Percent-of-current-HP damage every second. Intensity = percent per second.
/// Ignores damage reduction. Only SCP-500 cures it.
/// </summary>
public sealed class Irradiated : CustomEffect
{
    public override string Name => "Irradiated";
    public override EffectKind Kind => EffectKind.Negative;
    public override bool Stacks => true;
    public override bool ShowIntensity => true;
    public override float MaxIntensity => 100;

    /// <summary>Flat damage per second added on top (adrenaline gives -3hp/s).</summary>
    public float Flat;

    public override void OnTick()
    {
        float scalar = Owner.IsSCP ? MayhemPlugin.Cfg.Effects.IrradiatedScpScalar : 1f;
        float dmg = Owner.Health * (Intensity / 100f) * scalar + Flat;
        if (dmg < 1f) dmg = 1f;
        Owner.TrueDamage(dmg, "Died of radiation poisoning.");
    }
}

/// <summary>Stacking: each stack increases SCP-939's damage against you.</summary>
public sealed class Hunted : CustomEffect
{
    public override string Name => "Hunted";
    public override EffectKind Kind => EffectKind.Negative;
    public override bool Stacks => true;
    public override float MaxIntensity => 10;
}

/// <summary>Cooldown before Radiance can save you again.</summary>
public sealed class ReviveCooldown : CustomEffect
{
    public override string Name => "Revive Cooldown";
    public override EffectKind Kind => EffectKind.Negative;
}

/// <summary>Hellfire flames: deals intensity damage per second (x1.5 to SCPs), then intensity drops by 1.</summary>
public sealed class Burning : CustomEffect
{
    public override string Name => "Burning";
    public override EffectKind Kind => EffectKind.Negative;
    public override bool Stacks => true;
    public override bool ShowIntensity => true;
    public override float MaxIntensity => 30;
    public Player Source;

    public override void OnTick()
    {
        float dmg = Intensity * (Owner.IsSCP ? 1.5f : 1f);
        if (Source != null && Source.Valid() && Source.IsAlive)
            Owner.Damage(dmg, Source);
        else
            Owner.Damage(dmg, "Burned alive.");
        Intensity -= 1;
        if (Intensity <= 0) EffectManager.Remove(Owner, this);
    }
}

/// <summary>Gas Gas Gas from adrenaline: 255% speed and 255x SCP-207, breaks doors, then dies.</summary>
public sealed class GasGasGas : CustomEffect
{
    public override string Name => "GAS GAS GAS";
    public override EffectKind Kind => EffectKind.Mixed;

    public static bool IsActive(Player p) => EffectManager.Has<GasGasGas>(p);

    public override void OnApplied()
    {
        var s = Stats.Of(Owner);
        s.SetSpeed("gasgasgas", 255);
        Owner.EnableEffect<Scp207>(255);
        Audio.Music.PlayOn(Owner, "gas_gas_gas");
    }

    public override void OnTick()
    {
        foreach (var door in Map.Doors)
            if (door is BreakableDoor bd && !bd.IsBroken && (bd.Position - Owner.Position).sqrMagnitude < 9f)
                bd.TryBreak();
    }

    public override void OnRemoved()
    {
        Audio.Music.StopOn(Owner);
        if (Owner.IsAlive && Owner.LifeId == AppliedLife) Owner.Kill("Ran out of gas.");
    }

    public int AppliedLife;
    public static void Start(Player p)
    {
        var e = EffectManager.Apply<GasGasGas>(p, 10f);
        if (e != null) e.AppliedLife = p.LifeId;
    }
}

// ───────────── Random effect pool ─────────────

public static class RandomEffects
{
    /// <summary>Base-game effects that can be rolled (insta-kill / god-mode style effects are excluded).</summary>
    private static readonly (string name, byte intensity, bool negative)[] BasePool =
    {
        (nameof(Bleeding), 1, true), (nameof(Blindness), 1, true), (nameof(Burned), 1, true),
        (nameof(Concussed), 1, true), (nameof(Deafened), 1, true), (nameof(Disabled), 1, true),
        (nameof(Exhausted), 1, true), (nameof(Hemorrhage), 1, true), (nameof(Poisoned), 1, true),
        (nameof(Sinkhole), 1, true), (nameof(Stained), 1, true), (nameof(Slowness), 20, true),
        (nameof(Traumatized), 1, true), (nameof(AmnesiaVision), 1, true), (nameof(Blurred), 1, true),
        (nameof(HeavyFooted), 1, true), (nameof(Flashed), 1, true),
        (nameof(Invigorated), 1, false), (nameof(Scp207), 1, false), (nameof(AntiScp207), 1, false),
        (nameof(Scp1853), 1, false), (nameof(SilentWalk), 1, false), (nameof(Ghostly), 1, false),
        (nameof(BodyshotReduction), 2, false), (nameof(Vitality), 1, false), (nameof(RainbowTaste), 1, false),
        (nameof(Lightweight), 1, false), (nameof(NightVision), 1, false), (nameof(SugarRush), 1, false),
        (nameof(Prismatic), 1, false),
    };

    /// <summary>Gives one random effect (base game or Mayhem custom) for the duration.</summary>
    public static string GiveRandom(Player p, float duration, bool allowExplode = false)
    {
        if (!p.Valid() || !p.IsAlive) return null;
        int roll = Rng.Range(0, BasePool.Length + 9);
        if (roll < BasePool.Length)
        {
            var (name, intensity, negative) = BasePool[roll];
            if (negative && Stats.Of(p).NegativeImmune) return null;
            if (p.TryGetEffect(name, out var eff))
            {
                float d = name == nameof(Flashed) ? Mathf.Min(duration, 2f) : duration;
                p.EnableEffect(eff, intensity, d, true);
                return name;
            }
            return null;
        }
        switch (roll - BasePool.Length)
        {
            case 0: Stats.Of(p).SetSpeed("rnd_speed_" + Rng.Range(0, 99999), 10, duration); return "Movement Boost";
            case 1: Stats.Of(p).SetDamageReduction("rnd_dr_" + Rng.Range(0, 99999), 10, duration); return "Damage Reduction";
            case 2: EffectManager.Apply<Manic>(p, duration); return "Manic";
            case 3: EffectManager.Apply<Herd>(p, duration); return "Herd";
            case 4: EffectManager.Apply<Irradiated>(p, duration, 1); return "Irradiated";
            case 5: EffectManager.Apply<Burning>(p, 0, 3); return "Burning";
            case 6: EffectManager.Apply<Radiance>(p, duration); return "Radiance";
            case 7: Stats.Of(p).SetCrit("rnd_crit_" + Rng.Range(0, 99999), 5, 0.1f, duration); return "Crit";
            default: EffectManager.Apply<Hunted>(p, duration); return "Hunted";
        }
    }

    /// <summary>Picks a random present effect on the player (base or custom) for the Soberizer. Returns false if none.</summary>
    public static bool StealOne(Player from, Player to)
    {
        var custom = EffectManager.Of(from).Where(e => e.Kind != EffectKind.Hidden && e is not GasGasGas).ToList();
        var baseEffects = from.ActiveEffects
            .Where(e => BasePool.Any(b => b.name == e.GetType().Name))
            .ToList();
        int total = custom.Count + baseEffects.Count;
        if (total == 0) return false;
        int pick = Rng.Range(0, total);
        if (pick < custom.Count)
        {
            var e = custom[pick];
            float remaining = e.Remaining;
            EffectManager.Remove(from, e);
            e.Expires = remaining < 0 ? 0 : Time.time + remaining;
            EffectManager.Give(to, new[] { e });
            Hud.Message(to, $"<color=#7CFF7C>Stole {e.Name}</color>", 2f);
        }
        else
        {
            var b = baseEffects[pick - custom.Count];
            byte intensity = b.Intensity;
            float duration = b.TimeLeft;
            b.DisableEffect();
            if (to.TryGetEffect(b.GetType().Name, out var mine)) to.EnableEffect(mine, intensity, duration);
            Hud.Message(to, $"<color=#7CFF7C>Stole {b.GetType().Name}</color>", 2f);
        }
        return true;
    }
}
