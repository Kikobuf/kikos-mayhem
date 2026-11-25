using System.Collections.Generic;
using System.Linq;
using JailbirdMessageType = InventorySystem.Items.Jailbird.JailbirdMessageType;
using KikosMayhem.Core;
using KikosMayhem.Effects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using PlayerStatsSystem;
using UnityEngine;

namespace KikosMayhem.Items.Defs;

public sealed class CrimsonEcho : CustomItem
{
    public override string Id => "crimson_echo";
    public override string Name => "Crimson Echo";
    public override Rarity Rarity => Rarity.Legendary;
    public override ItemType BaseType => ItemType.GunCOM15;
    public override string Description => "Deals 200 TRUE damage (ignores AHP, hume shield and damage reduction) to enemies, and 40 damage to you. Never crits.";

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev)
    {
        ev.IsAllowed = false;
        if (!target.IsEnemyOf(attacker)) return;
        var cfg = MayhemPlugin.Cfg.Items;
        float dmg = target.IsPuppet() ? cfg.CrimsonEchoPuppetTrueDamage : cfg.CrimsonEchoTrueDamage;
        target.TrueDamage(dmg, $"Torn apart by {attacker.Nickname}'s Crimson Echo.");
        attacker.SendHitMarker(1.5f);
        attacker.Damage(cfg.CrimsonEchoSelfDamage, "Crimson Echo backlash.");
    }
}

/// <summary>Shared logic for explosive revolvers.</summary>
public abstract class ExplosiveRevolver : CustomItem
{
    public override ItemType BaseType => ItemType.GunRevolver;
    public override bool CanReload => false;

    public override void OnShot(Player p, FirearmItem firearm, Player hit)
    {
        var point = hit != null ? hit.Position : p.AimPoint(80f);
        PlayerExt.Explode(point, p);
    }

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev)
    {
        // The bullet itself does little; the explosion does the work.
        if (ev.DamageHandler is FirearmDamageHandler) Combat.MultiplyDamage(ev, 0.5f);
    }
}

public sealed class GrenadeLauncher : ExplosiveRevolver
{
    public override string Id => "grenade_launcher";
    public override string Name => "Grenade Launcher";
    public override Rarity Rarity => Rarity.Legendary;
    public override string Description => "The area where your bullet lands explodes. Regenerates 1 shot every 30s.";
    public override string UpgradeTo => "rolling_thunder";
    public override float UpgradeChance => 25f;
    public override int AmmoCap => 2;
    public override int RegenAmount => 1;
    public override float RegenInterval => 30f;
}

public sealed class Minigun : CustomItem
{
    public override string Id => "minigun";
    public override string Name => "Minigun";
    public override Rarity Rarity => Rarity.Legendary;
    public override ItemType BaseType => ItemType.GunCom45;
    public override string Description => "COM-45 with 200 rounds that quickly regenerate. 15 damage per shot with 75% armor penetration.";
    public override int AmmoCap => 200;
    public override int RegenAmount => 12;
    public override float RegenInterval => 0.25f;
    public override bool CanReload => false;

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev) => Combat.SetDamage(ev, 15f);
}

public sealed class SuicideVest : CustomItem
{
    public override string Id => "suicide_vest";
    public override string Name => "Suicide Vest";
    public override Rarity Rarity => Rarity.Legendary;
    public override ItemType BaseType => ItemType.ArmorCombat;
    public override string Description => "You explode when you die, dealing around 1000 damage to everyone nearby.";

    public override void OnOwnerDying(Player owner, PlayerDyingEventArgs ev)
    {
        var pos = owner.Position;
        Loops.After(0.1f, () =>
        {
            PlayerExt.Explode(pos, owner);
            foreach (var o in PlayerExt.Near(pos, 6f).ToList())
                o.Damage(MayhemPlugin.Cfg.Items.SuicideVestDamage, "Caught in a suicide vest blast.");
        });
    }
}

public sealed class Scp18018 : CustomItem
{
    public override string Id => "scp_18018";
    public override string Name => "SCP-018018";
    public override Rarity Rarity => Rarity.Legendary;
    public override ItemType BaseType => ItemType.GrenadeHE;
    public override string Description => "On explosion releases 18 SCP-018 balls. Teammates are safe from them for the first 10s.";
    public override bool CausalityBlacklisted => true;

    private static readonly Dictionary<ReferenceHub, float> SafeUntil = new();

    public override void OnExploded(Player owner, Vector3 position)
    {
        if (owner != null) SafeUntil[owner.ReferenceHub] = Time.time + 10f;
        for (int i = 0; i < 18; i++)
        {
            var ball = TimedGrenadeProjectile.SpawnActive(position + Random.insideUnitSphere * 0.6f + Vector3.up * 0.5f, ItemType.SCP018, owner);
            if (ball?.Rigidbody != null) ball.Rigidbody.velocity = Random.onUnitSphere * Rng.Range(6f, 14f);
        }
    }

    /// <summary>Combat pre-hook: teammates of the thrower ignore SCP-018 damage for 10s.</summary>
    public static void TeamSafety(PlayerHurtingEventArgs ev, Player attacker, Player target)
    {
        if (ev.DamageHandler is not Scp018DamageHandler || attacker == null) return;
        if (!SafeUntil.TryGetValue(attacker.ReferenceHub, out var until) || until < Time.time) return;
        if (!attacker.IsEnemyOf(target)) ev.IsAllowed = false;
    }
}

public sealed class Real500 : CustomItem
{
    public override string Id => "real_500";
    public override string Name => "Real 500";
    public override Rarity Rarity => Rarity.Legendary;
    public override ItemType BaseType => ItemType.SCP500;
    public override string Description => "Guaranteed \"revive all\": every spectator respawns as your class. (Revives nobody if Puppet uses it.)";
    public override bool CausalityBlacklisted => true;

    public override void OnUsed(Player p, Item item)
    {
        p.Heal(p.MaxHealth);
        EffectManager.Remove<Irradiated>(p);
        if (p.IsPuppet()) return;
        RevivingPlayers.Start(p, int.MaxValue);
        Hud.MessageAll($"<color=#FFD700>{p.Nickname} used a Real 500 — the dead are coming back!</color>");
    }
}

public sealed class Hyperion : CustomItem
{
    public override string Id => "hyperion";
    public override string Name => "Hyperion";
    public override Rarity Rarity => Rarity.Legendary;
    public override ItemType BaseType => ItemType.Jailbird;
    public override string Description => "Infinite uses. When a charge ends it explodes, damaging nearby enemies and breaking doors; you get AHP that turns into HP.";

    public override void OnJailbird(Player p, JailbirdItem jailbird, JailbirdMessageType message)
    {
        if (message == JailbirdMessageType.ChargeStarted)
        {
            int life = p.LifeId;
            Loops.After(1.1f, () =>
            {
                if (!p.Valid() || p.LifeId != life || !p.IsAlive) return;
                Boom(p);
            });
        }
        if (message is JailbirdMessageType.AttackPerformed or JailbirdMessageType.ChargeStarted or JailbirdMessageType.Broken)
            Loops.After(0.2f, () => { if (!jailbird.IsDestroyed) jailbird.Reset(); });
    }

    private static void Boom(Player p)
    {
        var pos = p.Position + p.Camera.forward * 1.5f;
        EffectManager.Apply<Invincible>(p, 0.3f);
        PlayerExt.Explode(pos, p, ExplosionType.Grenade);
        foreach (var door in Map.Doors)
            if (door is BreakableDoor bd && (bd.Position - pos).sqrMagnitude < 16f) bd.TryBreak();
        float ahp = p.IsPuppet() ? 30f : 60f;
        var proc = p.CreateAhpProcess(ahp, ahp, 0f, 1f, 0f, false);
        Loops.After(2.5f, () =>
        {
            if (!p.Valid() || !p.IsAlive) return;
            float left = proc.CurrentAmount;
            p.ServerKillProcess(proc);
            p.Heal(left);
        });
    }
}
