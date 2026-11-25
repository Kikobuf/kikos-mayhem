using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using InventorySystem.Items.MicroHID.Modules;
using KikosMayhem.Core;
using KikosMayhem.Effects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using PlayerRoles;
using UnityEngine;

namespace KikosMayhem.Items.Defs;

public sealed class RealAnti207 : CustomItem
{
    public override string Id => "real_anti_207";
    public override string Name => "Real Anti SCP-207";
    public override Rarity Rarity => Rarity.Epic;
    public override ItemType BaseType => ItemType.AntiSCP207;
    public override string Description => "3x SCP-207? and 25% damage reduction to ALL damage.";
    public override string UpgradeTo => "paladins_elixir";
    public override float UpgradeChance => 25f;

    public override void OnUsed(Player p, Item item)
    {
        p.EnableEffect<AntiScp207>(3);
        Stats.Of(p).SetDamageReduction("real_anti_207", 25f);
    }
}

public sealed class ClusterGrenade : CustomItem
{
    public override string Id => "cluster_grenade";
    public override string Name => "Cluster Grenade";
    public override Rarity Rarity => Rarity.Epic;
    public override ItemType BaseType => ItemType.GrenadeHE;
    public override string Description => "Explodes after 1.5s into a spread of grenades that keep their momentum — carpet bomb an area. The main blast does 65% less damage.";
    public override string UpgradeTo => "pocket_nuke";
    public override float UpgradeChance => 0.5f;

    public override void OnThrown(Player p, Projectile projectile)
    {
        if (projectile is TimedGrenadeProjectile g) g.RemainingTime = Mirror.NetworkTime.time + 1.5;
    }

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev) =>
        Combat.MultiplyDamage(ev, 0.35f);

    public override bool OnExploding(Player owner, TimedGrenadeProjectile grenade, Vector3 position)
    {
        var vel = grenade.Rigidbody != null ? grenade.Rigidbody.velocity : Vector3.zero;
        int count = Rng.Range(6, 10);
        for (int i = 0; i < count; i++)
        {
            var child = TimedGrenadeProjectile.SpawnActive(position + Vector3.up * 0.4f, ItemType.GrenadeHE, owner, Rng.Range(1.2f, 2.4f));
            if (child?.Rigidbody == null) continue;
            var spread = new Vector3(Rng.Range(-1f, 1f), Rng.Range(0.3f, 1f), Rng.Range(-1f, 1f)).normalized * Rng.Range(4f, 9f);
            child.Rigidbody.velocity = vel * 0.9f + spread;
        }
        return true;
    }
}

public sealed class Peacemaker : CustomItem
{
    public override string Id => "peacemaker";
    public override string Name => "Peacemaker";
    public override Rarity Rarity => Rarity.Epic;
    public override ItemType BaseType => ItemType.GunCOM18;
    public override string Description => "Shooting an enemy instantly converts them to YOUR class (zombies too). Shooting an SCP heals it by 200+ HP per bullet at the cost of 40% of your HP. 4 shots, +1 every 20s.";
    public override int AmmoCap => 4;
    public override int RegenAmount => 1;
    public override float RegenInterval => 20f;
    public override bool CanReload => false;

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev)
    {
        ev.IsAllowed = false;
        var cfg = MayhemPlugin.Cfg.Items;
        if ((target.IsSCP && target.Role != RoleTypeId.Scp0492) || target.IsPuppet())
        {
            target.Health = Mathf.Min(target.MaxHealth, target.Health + cfg.PeacemakerScpHeal);
            attacker.Damage(attacker.Health * cfg.PeacemakerSelfCostPercent / 100f, "Gave their life force to an SCP.");
            return;
        }
        if (!target.IsEnemyOf(attacker)) return;

        RoleTypeId role;
        if (attacker.IsPuppet()) role = RoleTypeId.Scp0492;              // Puppet's "class" is the SCPs
        else if (Events.MyhmInsurgency.IsInsurgent(attacker)) return;
        else role = attacker.Role;

        var pos = target.Position;
        target.SetRole(role, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.None);
        Loops.After(0.1f, () => { if (target.Valid()) target.Position = pos; });
        Msg(target, $"<color=#88FFAA>{attacker.Nickname} made peace with you. You're on their side now.</color>");
    }
}

public sealed class Effectorizer : CustomItem
{
    public override string Id => "effectorizer";
    public override string Name => "Effectorizer";
    public override Rarity Rarity => Rarity.Epic;
    public override ItemType BaseType => ItemType.GunFSP9;
    public override string Description => "Gives a random effect to the enemy you shoot (god mode, pit death and other insta-kill effects are blacklisted).";
    public override int RegenAmount => 1;
    public override float RegenInterval => 2f;

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev)
    {
        if (!target.IsEnemyOf(attacker)) { ev.IsAllowed = false; return; }
        RandomEffects.GiveRandom(target, 15f);
    }
}

public sealed class Skinwalker : CustomItem
{
    public override string Id => "skinwalker";
    public override string Name => "Skinwalker";
    public override Rarity Rarity => Rarity.Epic;
    public override ItemType BaseType => ItemType.ArmorLight;
    public override string Description => "Everyone sees you as their OWN role. Revealed for 3s after you damage someone, and after the nuke. Attackers get no hitmarkers while you're camouflaged.";
    public override bool PuppetBlocked => true;

    public override void OnInventoryTick(Player p, Item item)
    {
        if (p.IsPuppet()) return;
        if (!EffectManager.Has<SkinwalkerEffect>(p)) EffectManager.Apply<SkinwalkerEffect>(p);
    }

    public override void OnLeftInventory(Player p, ushort serial) => EffectManager.Remove<SkinwalkerEffect>(p);

    public override void OnOwnerHurting(Player owner, Player attacker, PlayerHurtingEventArgs ev)
    {
        if (EffectManager.Get<SkinwalkerEffect>(owner) is { Active: true }) ItemEngine.HideNextHitmarker(attacker);
    }
}

public sealed class GuardiansGlow : CustomItem
{
    public override string Id => "guardians_glow";
    public override string Name => "Guardian's Glow";
    public override Rarity Rarity => Rarity.Epic;
    public override ItemType BaseType => ItemType.Lantern;
    public override string Description => "You and close teammates get Radiance: healing, a little AHP, and one prevented death (3s invincible, 120s cooldown).";

    public override void OnInventoryTick(Player p, Item item)
    {
        float r = MayhemPlugin.Cfg.Items.RadianceRange;
        foreach (var o in PlayerExt.Near(p.Position, r))
            if (o == p || !o.IsEnemyOf(p))
                EffectManager.Apply<Radiance>(o, 2f);
    }
}

public sealed class AllPill : CustomItem
{
    public override string Id => "all_pill";
    public override string Name => "The All Pill";
    public override Rarity Rarity => Rarity.Epic;
    public override ItemType BaseType => ItemType.SCP500;
    public override string Description => "Every living player gets one random effect.";

    public override void OnUsed(Player p, Item item)
    {
        foreach (var o in PlayerExt.Alive.ToList())
        {
            var name = RandomEffects.GiveRandom(o, 30f);
            if (name != null) Msg(o, $"<color=#C77DFF>{p.Nickname} took The All Pill — you got {name}</color>");
        }
    }
}

public sealed class DubstepGun : CustomItem
{
    public override string Id => "dubstep_gun";
    public override string Name => "Dubstep Gun";
    public override Rarity Rarity => Rarity.Epic;
    public override ItemType BaseType => ItemType.MicroHID;
    public override string Description => "Infinite charge. Using it drains your health and builds Overvolt (drains faster). Kills remove stacks and give AHP. 60 stacks = you can't handle the dubstep and explode.";

    private readonly Dictionary<ushort, float> _lastActive = new();

    public override void OnHeldTick(Player p, Item item)
    {
        if (item is not MicroHIDItem micro) return;
        micro.Energy = 1f;
        bool active = micro.Phase is MicroHidPhase.WindingUp or MicroHidPhase.WoundUpSustain or MicroHidPhase.Firing;
        if (!active)
        {
            StopUsing(p, item.Serial);
            return;
        }
        if (!_lastActive.ContainsKey(item.Serial)) Audio.Music.PlayOn(p, "dubstep", true);
        _lastActive[item.Serial] = Time.time;

        var ov = EffectManager.Get<Overvolt>(p) ?? EffectManager.Apply<Overvolt>(p, 0, 0);
        float stacks = ov?.Intensity ?? 0;
        float drain = 1.5f * Mathf.Pow(1.01f, stacks);
        if (p.IsPuppet()) drain *= 0.5f;
        p.TrueDamage(drain, "Couldn't handle the dubstep.");
        if (ov != null)
        {
            ov.Intensity = Mathf.Min(60, ov.Intensity + 0.25f);
            if (ov.Intensity >= 60) Overload(p);
        }
    }

    private void StopUsing(Player p, ushort serial)
    {
        if (!_lastActive.Remove(serial)) return;
        Audio.Music.StopOn(p);
        var ov = EffectManager.Get<Overvolt>(p);
        if (ov != null && ov.Intensity > 0) p.TrueDamage(0.5f * ov.Intensity, "Couldn't handle the dubstep.");
    }

    public override void OnUnequipped(Player p, Item item) => StopUsing(p, item.Serial);
    public override void OnLeftInventory(Player p, ushort serial) => StopUsing(p, serial);

    public override void OnKill(Player attacker, Player victim)
    {
        var ov = EffectManager.Get<Overvolt>(attacker);
        float stacks = ov?.Intensity ?? 0;
        if (ov != null) ov.Intensity = Mathf.Max(0, ov.Intensity - 2);
        float ahp = attacker.IsPuppet() ? 10f : 20f + stacks;
        attacker.CreateAhpProcess(ahp, 150f, 1f, 1f, 3f, false);
        attacker.Heal(10f + stacks * 0.5f);
    }

    private void Overload(Player p)
    {
        var pos = p.Position;
        Audio.Music.StopOn(p);
        for (int i = 0; i < 4; i++)
            PlayerExt.Explode(pos + Random.insideUnitSphere * 2f, p);
        if (p.IsAlive) p.Kill("Couldn't handle the dubstep.");
    }
}

public sealed class RecursiveDestinyCoin : CustomItem
{
    public override string Id => "recursive_destiny_coin";
    public override string Name => "Recursive Destiny Coin";
    public override Rarity Rarity => Rarity.Epic;
    public override ItemType BaseType => ItemType.Coin;
    public override string Description => "75%: revives a dead player as your class next to you. 25%: kills you. One use.";

    public override void OnFlipped(Player p, CoinItem coin, bool tails)
    {
        ItemEngine.Unregister(coin.Serial);
        p.RemoveItem(coin);
        if (!Rng.Chance(75))
        {
            p.Kill("Destiny said no.");
            return;
        }
        var dead = Rng.Pick(PlayerExt.Spectators.ToList());
        if (dead == null) { Msg(p, "Nobody to revive"); return; }
        Gambling.GamblingModule.ReviveAs(dead, p);
        Msg(p, $"<color=#88FFAA>Destiny revived {dead.Nickname}</color>");
    }
}

public sealed class HuginnAndMuninn : CustomItem
{
    public override string Id => "huginn_muninn";
    public override string Name => "Huginn and Muninn";
    public override Rarity Rarity => Rarity.Epic;
    public override ItemType BaseType => ItemType.SCP1344;
    public override string Description => "Gives Eyes Of Odin: tracks ALL enemies in the facility and shows the nearest one.";

    public override bool OnUsing(Player p, Item item) => false;

    public override void OnInventoryTick(Player p, Item item)
    {
        if (!EffectManager.Has<EyesOfOdin>(p)) EffectManager.Apply<EyesOfOdin>(p);
    }

    public override void OnLeftInventory(Player p, ushort serial) => EffectManager.Remove<EyesOfOdin>(p);
}
