using System.Linq;
using CustomPlayerEffects;
using Interactables.Interobjects.DoorUtils;
using KikosMayhem.Core;
using KikosMayhem.Effects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using PlayerStatsSystem;
using UnityEngine;

namespace KikosMayhem.Items.Defs;

public sealed class Addictionater : CustomItem
{
    public override string Id => "addictionater";
    public override string Name => "Addictionater";
    public override Rarity Rarity => Rarity.Common;
    public override ItemType BaseType => ItemType.Painkillers;
    public override string Description => "Permanent +20% speed & Invigorated, 25s of Manic. Starts an overdose: painkillers, adrenaline and SCP-500 you eat are consumed instantly and ALL hit at once when it ends.";

    public override void OnUsed(Player p, Item item)
    {
        var s = Stats.Of(p);
        s.SetSpeed("addictionater", 20);
        p.EnableEffect<Invigorated>(1);
        EffectManager.Apply<Manic>(p, 25f);
        EffectManager.Apply<Overdose>(p, MayhemPlugin.Cfg.Gambling.AddictionaterOverdoseSeconds);
    }
}

public sealed class CritiCola : CustomItem
{
    public override string Id => "criti_cola";
    public override string Name => "Criti Cola";
    public override Rarity Rarity => Rarity.Common;
    public override ItemType BaseType => ItemType.AntiSCP207;
    public override string Description => "+20% movement speed. 40% crit chance, 1.8x crit damage (stacks). Puppet: 25% / 1.33x.";

    public override void OnUsed(Player p, Item item)
    {
        var s = Stats.Of(p);
        s.AddSpeed("criti_cola", 20f);
        if (p.IsPuppet()) s.AddCrit("criti_cola", 25f, 0.33f);
        else s.AddCrit("criti_cola", 40f, 0.8f);
    }
}

public sealed class Beer : CustomItem
{
    public override string Id => "beer";
    public override string Name => "Beer";
    public override Rarity Rarity => Rarity.Common;
    public override ItemType BaseType => ItemType.AntiSCP207;
    public override string Description => "3x Turbo Drunk (every hit gives you random effects) and 65% negative effect reduction.";

    public override void OnUsed(Player p, Item item)
    {
        EffectManager.Apply<TurboDrunk>(p, 0, 3);
        Stats.Of(p).SetNegativeReduction("beer", 65f);
    }
}

public sealed class EchoBall : CustomItem
{
    public override string Id => "echo_ball";
    public override string Name => "Echo Ball";
    public override Rarity Rarity => Rarity.Common;
    public override ItemType BaseType => ItemType.SCP018;
    public override string Description => "If anyone is killed by this ball, it spawns another SCP-018.";
    public override string UpgradeTo => "scp_18018";
    public override float UpgradeChance => 18f;

    /// <summary>Kill hook: a death to SCP-018 damage from an Echo Ball owner spawns a new Echo Ball.</summary>
    public static void OnAnyKill(Player killer, Player victim, PlayerDeathEventArgs ev)
    {
        if (ev.DamageHandler is not Scp018DamageHandler) return;
        bool echoAlive = TimedGrenadeProjectile.List.Any(g => !g.IsDestroyed && ItemEngine.Get(g.Serial) is EchoBall);
        if (!echoAlive) return;
        var ball = TimedGrenadeProjectile.SpawnActive(ev.OldPosition + Vector3.up, ItemType.SCP018, killer);
        if (ball != null && ItemEngine.TryGetDef("echo_ball", out var def)) ItemEngine.Register(ball.Serial, def);
    }
}

public sealed class SchrodingersGrenade : CustomItem
{
    public override string Id => "schrodingers_grenade";
    public override string Name => "Schrödinger's Grenade";
    public override Rarity Rarity => Rarity.Common;
    public override ItemType BaseType => ItemType.GrenadeHE;
    public override string Description => "Either explodes instantly once thrown, or does 3x damage (doesn't stack with crits).";

    private static readonly System.Collections.Generic.HashSet<ushort> Instant = new();

    public override void OnThrown(Player p, Projectile projectile)
    {
        if (!Rng.Chance(50)) return;
        Instant.Add(projectile.Serial);
        if (projectile is TimedGrenadeProjectile g)
            g.RemainingTime = Mirror.NetworkTime.time + 0.05;
    }

    private static bool _instantNow;

    public override bool OnExploding(Player owner, TimedGrenadeProjectile grenade, Vector3 position)
    {
        _instantNow = Instant.Remove(grenade.Serial);
        return true;
    }

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev)
    {
        Combat.SuppressCrit = true;
        if (!_instantNow && ev.DamageHandler is ExplosionDamageHandler) Combat.MultiplyDamage(ev, 3f);
    }
}

public sealed class GreedyGrenade : CustomItem
{
    public override string Id => "greedy_grenade";
    public override string Name => "Greedy High Explosive Grenade";
    public override Rarity Rarity => Rarity.Common;
    public override ItemType BaseType => ItemType.GrenadeHE;
    public override string Description => "On explosion spawns weapons, keycards and consumables — and rarely a custom item.";
    public override string UpgradeTo => "scrooge_grenade";
    public override float UpgradeChance => 25f;
    public override bool CausalityBlacklisted => true;

    private static readonly ItemType[] Loot =
    {
        ItemType.GunCOM15, ItemType.GunCOM18, ItemType.GunFSP9, ItemType.GunCrossvec, ItemType.GunE11SR, ItemType.GunAK,
        ItemType.GunShotgun, ItemType.GunRevolver, ItemType.KeycardScientist, ItemType.KeycardMTFPrivate,
        ItemType.KeycardMTFOperative, ItemType.KeycardContainmentEngineer, ItemType.KeycardResearchCoordinator,
        ItemType.Medkit, ItemType.Painkillers, ItemType.Adrenaline, ItemType.SCP500, ItemType.SCP207, ItemType.GrenadeFlash,
    };

    public override void OnExploded(Player owner, Vector3 position)
    {
        int n = Rng.Range(4, 8);
        for (int i = 0; i < n; i++) Pickup.Create(Rng.Pick(Loot), ItemSpawner.Scatter(position, 1.2f));
        if (Rng.Chance(12))
        {
            var def = ItemSpawner.RandomDef(d => d.Rarity != Rarity.Admin);
            if (def != null) ItemEngine.Spawn(def, position + Vector3.up * 0.5f);
        }
    }
}

public sealed class KeyToTheCity : CustomItem
{
    public override string Id => "key_to_the_city";
    public override string Name => "Key to the City";
    public override Rarity Rarity => Rarity.Common;
    public override ItemType BaseType => ItemType.KeycardO5;
    public override string Description => "O5 keycard that BREAKS every door you use it on.";
    public override string OneToOne => "lock_to_the_city";

    public override void OnDoorInteract(Player p, Door door, PlayerInteractingDoorEventArgs ev)
    {
        if (door is BreakableDoor bd && !bd.IsBroken)
        {
            ev.IsAllowed = false;
            bd.TryBreak();
        }
        else
        {
            ev.CanOpen = true;
        }
    }
}

public sealed class LockToTheCity : CustomItem
{
    public override string Id => "lock_to_the_city";
    public override string Name => "Lock to the City";
    public override Rarity Rarity => Rarity.Common;
    public override ItemType BaseType => ItemType.KeycardJanitor;
    public override string Description => "Locks any door in the facility for 10 seconds. 20s cooldown.";
    public override string OneToOne => "key_to_the_city";

    public override void OnDoorInteract(Player p, Door door, PlayerInteractingDoorEventArgs ev)
    {
        ev.IsAllowed = false;
        var cfg = MayhemPlugin.Cfg.Items;
        if (door.IsLocked) return;
        if (ItemEngine.OnCooldown(p, p.CurrentItem.Serial, "lock", cfg.LockToTheCityCooldown)) return;
        door.Lock(DoorLockReason.AdminCommand, true);
        Hud.Message(p, "<color=#88CCFF>Door locked</color>", 1.5f);
        Loops.After(cfg.LockToTheCityLockSeconds, () => { if (!door.IsDestroyed) door.Lock(DoorLockReason.AdminCommand, false); });
    }
}

public sealed class RattleEm : CustomItem
{
    public override string Id => "rattle_em";
    public override string Name => "Rattle Em'";
    public override Rarity Rarity => Rarity.Common;
    public override ItemType BaseType => ItemType.GunA7;
    public override string Description => "A7 that regenerates 30 ammo every 0.75s, up to 100.";
    public override string UpgradeTo => "minigun";
    public override float UpgradeChance => 10f;
    public override int AmmoCap => 100;
    public override int RegenAmount => 30;
    public override float RegenInterval => 0.75f;
    public override bool CanReload => false;
}

public sealed class RegeneratingE11 : CustomItem
{
    public override string Id => "regen_e11";
    public override string Name => "Regenerating Epsilon-11";
    public override Rarity Rarity => Rarity.Common;
    public override ItemType BaseType => ItemType.GunE11SR;
    public override string Description => "Epsilon-11 rifle that regenerates 5 ammo per second, up to 50.";
    public override int AmmoCap => 50;
    public override int RegenAmount => 5;
    public override float RegenInterval => 1f;
}

public sealed class Soberizer : CustomItem
{
    public override string Id => "soberizer";
    public override string Name => "Soberizer";
    public override Rarity Rarity => Rarity.Common;
    public override ItemType BaseType => ItemType.GunCrossvec;
    public override string Description => "Shooting a human steals their effects. Doesn't work on SCPs.";

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev)
    {
        if (target.IsSCP || !target.IsEnemyOf(attacker)) return;
        RandomEffects.StealOne(target, attacker);
    }
}
