using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using InventorySystem.Items.Firearms.Modules;
using KikosMayhem.Core;
using KikosMayhem.Effects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;
using UnityEngine;

namespace KikosMayhem.Items.Defs;

public sealed class Accelerator : CustomItem
{
    public override string Id => "accelerator";
    public override string Name => "Accelerator";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.GunAK;
    public override string Description => "40% chance to gain +1% movement speed on hit.";
    public override string UpgradeTo => "sonic_surge";
    public override float UpgradeChance => 25f;

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev)
    {
        if (target.IsEnemyOf(attacker) && Rng.Chance(40)) Stats.Of(attacker).AddSpeed("accelerator", 1f);
    }
}

public sealed class PinkExtract : CustomItem
{
    public override string Id => "pink_extract";
    public override string Name => "Pink Extract";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.Adrenaline;
    public override string Description => "Gives you the Sinkhole effect... then you explode, dealing 1000+ damage to everyone in range.";

    public override void OnUsed(Player p, Item item)
    {
        p.EnableEffect<Sinkhole>(1, 4f);
        int life = p.LifeId;
        Hud.Message(p, "<color=#FF66CC>Something is very wrong...</color>", 3f);
        Loops.After(4f, () =>
        {
            if (!p.Valid() || !p.IsAlive || p.LifeId != life) return;
            var pos = p.Position;
            PlayerExt.Explode(pos, p);
            foreach (var o in PlayerExt.Near(pos, 7f).Where(o => o != p).ToList())
                o.Damage(MayhemPlugin.Cfg.Items.PinkExtractDamage, p);
            p.Kill("Exploded from the inside.");
        });
    }
}

public sealed class Vampirism : CustomItem
{
    public override string Id => "vampirism";
    public override string Name => "Vampirism";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.Adrenaline;
    public override string Description => "Awakens the demon inside you 😈. 35s timer: every kill gives +4% speed, +5% crit chance, 20 AHP and more time. When it runs out you drop to 1 HP.";

    public override void OnUsed(Player p, Item item) =>
        EffectManager.Apply<VampirismEffect>(p, MayhemPlugin.Cfg.Items.VampirismStartSeconds);

    public static void OnAnyKill(Player killer, Player victim, PlayerDeathEventArgs ev) =>
        EffectManager.Get<VampirismEffect>(killer)?.OnKill();
}

public sealed class TurboBoost : CustomItem
{
    public override string Id => "turbo_boost";
    public override string Name => "Turbo Boost";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.Adrenaline;
    public override string Description => "15s of SCP-207 x255, +255% movement speed and negative effect immunity (stacks).";

    public override void OnUsed(Player p, Item item)
    {
        var s = Stats.Of(p);
        s.SetSpeed("turbo_boost_" + Time.frameCount, 255f, 15f);
        s.NegativeImmuneUntil = Mathf.Max(s.NegativeImmuneUntil, Time.time) + 15f;
        p.EnableEffect<Scp207>(255, 15f);
        Hud.Message(p, "<color=#FFAA00>TURBO BOOST</color>", 2f);
    }
}

public sealed class WarpCoin : CustomItem
{
    public override string Id => "warp_coin";
    public override string Name => "Warp Coin";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.Coin;
    public override string Description => "50%: you teleport to someone random. 50%: someone random teleports to you (never SCPs or Puppet). 30s cooldown.";
    public override string UpgradeTo => "hotswap_coin";
    public override float UpgradeChance => 25f;

    public override bool PuppetBlocked => true;

    public override bool OnFlipping(Player p, CoinItem coin) =>
        !ItemEngine.OnCooldown(p, coin.Serial, "warp", MayhemPlugin.Cfg.Items.WarpCoinCooldown);

    public override void OnFlipped(Player p, CoinItem coin, bool tails)
    {
        if (!p.CanTeleport()) return;
        var target = Rng.Pick(PlayerExt.Alive.Where(o => o != p && !o.IsSCP && !o.IsPuppet() && o.Role != RoleTypeId.Scp079 && o.CanTeleport()).ToList());
        if (target == null) { Msg(p, "Nobody to warp to"); return; }
        if (Rng.Chance(50))
        {
            p.Position = target.Position;
            Msg(p, $"Warped to {target.Nickname}");
        }
        else
        {
            target.Position = p.Position;
            Msg(target, $"You were warped to {p.Nickname}!");
            Msg(p, $"Pulled {target.Nickname} to you");
        }
    }
}

public sealed class Autovest : CustomItem
{
    public override string Id => "autovest";
    public override string Name => "Autovest";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.ArmorHeavy;
    public override string Description => "While worn, your guns reload themselves (even the one you're holding).";

    private readonly Dictionary<ReferenceHub, float> _next = new();

    public override void OnInventoryTick(Player p, Item item)
    {
        if (_next.TryGetValue(p.ReferenceHub, out var t) && t > Time.time) return;
        _next[p.ReferenceHub] = Time.time + 1f;
        foreach (var fa in p.Items.OfType<FirearmItem>())
        {
            var def = ItemEngine.Get(fa);
            if (def != null && (def.RegenAmount > 0 || !def.CanReload)) continue;
            if (fa.IsReloadingOrUnloading) continue;
            int missing = fa.MaxAmmo - fa.StoredAmmo;
            if (missing <= 0) continue;
            var ammoType = fa.AmmoType;
            int reserve = p.GetAmmo(ammoType);
            int take = Mathf.Min(missing, reserve);
            if (take <= 0) continue;
            p.SetAmmo(ammoType, (ushort)(reserve - take));
            fa.StoredAmmo += take;
        }
    }
}

/// <summary>Mirage disguise lasts while its hume shield holds.</summary>
public sealed class MirageDisguise : CustomEffect
{
    public RoleTypeId Role;
    public float NukeEnds = -1;
    public override string Name => $"Mirage ({Role})";
    public override EffectKind Kind => EffectKind.Positive;

    public override void OnApplied() => Disguise.SetFixed(Owner, Role);
    public override void OnRemoved() => Disguise.Clear(Owner);

    public override void OnTick()
    {
        if (Warhead.IsDetonated)
        {
            if (NukeEnds < 0) NukeEnds = Time.time + 15f;
            else if (Time.time > NukeEnds) { EffectManager.Remove(Owner, this); return; }
        }
        if (Owner.HumeShield <= 0.5f) EffectManager.Remove(Owner, this);
    }

    public override void OnOwnerHurting(Player attacker, PlayerHurtingEventArgs ev) => ItemEngine.HideNextHitmarker(attacker);
}

public sealed class Mirage : CustomItem
{
    public override string Id => "mirage";
    public override string Name => "Mirage";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.GunRevolver;
    public override string Description => "Killing someone disguises you as their role and gives 50 hume shield. Disguise lasts until the shield breaks. Attackers don't get hitmarkers.";
    public override bool PuppetBlocked => true;
    public override bool CausalityBlacklisted => true;

    public override void OnKill(Player attacker, Player victim)
    {
        if (attacker.IsPuppet()) return;
        attacker.MaxHumeShield = Mathf.Max(attacker.MaxHumeShield, 50f);
        attacker.HumeShield += 50f;
        var role = ItemEngine.LastKillOldRole;
        if (role.GetTeam() == Team.SCPs || role == RoleTypeId.Spectator) return;
        EffectManager.Remove<MirageDisguise>(attacker);
        var e = EffectManager.Apply<MirageDisguise>(attacker);
        if (e != null)
        {
            e.Role = role;
            Disguise.SetFixed(attacker, role);
        }
        Msg(attacker, $"<color=#C77DFF>You now look like a {role}</color>", 2f);
    }
}

public static class Teleports
{
    public static IEnumerable<FacilityZone> SafeZones()
    {
        if (!Decontamination.IsDecontaminating) yield return FacilityZone.LightContainment;
        if (!Warhead.IsDetonated)
        {
            yield return FacilityZone.HeavyContainment;
            yield return FacilityZone.Entrance;
        }
        else yield return FacilityZone.Surface;
    }

    public static bool ToRoom(Player p, Room room)
    {
        if (room == null || !p.CanTeleport()) return false;
        var spot = ItemSpawner.SpotIn(room);
        if (spot == null) return false;
        p.Position = spot.Value + Vector3.up * 0.4f;
        return true;
    }

    public static Room RandomRoom(FacilityZone zone, Room exclude = null) =>
        Rng.Pick(Room.List.Where(r => !r.IsDestroyed && r.Zone == zone && r != exclude && !ItemSpawner.IsBlacklisted(r)
                                      && r.Name != RoomName.Pocket && r.Name != RoomName.Hcz079).ToList());

    public static Room NearbyRoom(Player p)
    {
        var current = p.Room;
        if (current == null) return RandomRoom(p.Zone);
        var near = current.AdjacentRooms.Where(r => r.Zone == current.Zone).ToList();
        return near.Count > 0 ? Rng.Pick(near) : RandomRoom(current.Zone, current);
    }

    public static Room OtherZoneRoom(Player p)
    {
        var zones = SafeZones().Where(z => z != p.Zone).ToList();
        return zones.Count == 0 ? NearbyRoom(p) : RandomRoom(Rng.Pick(zones));
    }
}

public sealed class ByeByeMachine : CustomItem
{
    public override string Id => "bye_bye_machine";
    public override string Name => "Bye Bye Machine";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.ParticleDisruptor;
    public override string Description => "Burst: sends the target to a nearby room. Disruptor: sends them to another zone. Charged shot on an SCP: 50% another zone, else a random room. 3 shots, +1 every 20s.";
    public override int AmmoCap => 3;
    public override int RegenAmount => 1;
    public override float RegenInterval => 20f;
    public override bool CanReload => false;

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev)
    {
        ev.IsAllowed = false;
        bool burst = ev.DamageHandler is PlayerStatsSystem.DisruptorDamageHandler d && d.FiringState == DisruptorActionModule.FiringState.FiringRapid;
        Room dest;
        if (target.IsSCP) dest = Rng.Chance(50) ? Teleports.OtherZoneRoom(target) : Teleports.RandomRoom(target.Zone);
        else dest = burst ? Teleports.NearbyRoom(target) : Teleports.OtherZoneRoom(target);
        if (Teleports.ToRoom(target, dest))
            Msg(target, "<color=#C77DFF>Bye bye!</color>", 2f);
    }
}

public sealed class Pinpointer : CustomItem
{
    public override string Id => "pinpointer";
    public override string Name => "Pinpointer";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.GunA7;
    public override string Description => "Every hit deals 200 damage. Every miss kills YOU.";

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev) =>
        Combat.SetDamage(ev, MayhemPlugin.Cfg.Items.PinpointerDamage);

    public override void OnShot(Player p, FirearmItem firearm, Player hit)
    {
        if (hit == null && p.IsAlive) p.Kill("Missed with the Pinpointer.");
    }
}

public sealed class Real207 : CustomItem
{
    public override string Id => "real_207";
    public override string Name => "Real SCP-207";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.SCP207;
    public override string Description => "3x SCP-207 and +100% movement speed.";
    public override string UpgradeTo => "realest_207";
    public override float UpgradeChance => 25f;

    public override void OnUsed(Player p, Item item)
    {
        p.EnableEffect<Scp207>(3);
        Stats.Of(p).SetSpeed("real_207", 100f);
    }
}

public sealed class WarpGrenade : CustomItem
{
    public override string Id => "warp_grenade";
    public override string Name => "Warp High Explosive Grenade";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.GrenadeHE;
    public override string Description => "Teleports to a random enemy (Puppet and SCPs included) and explodes 1.5s faster.";

    public override void OnThrown(Player p, Projectile projectile)
    {
        if (projectile is not TimedGrenadeProjectile g) return;
        g.RemainingTime -= 1.5;
        Loops.After(0.4f, () =>
        {
            if (g.IsDestroyed) return;
            var enemy = Rng.Pick(PlayerExt.EnemiesOf(p).Where(e => e.Role != RoleTypeId.Scp079).ToList());
            if (enemy == null) return;
            g.Position = enemy.Position + Vector3.up * 0.3f;
            if (g.Rigidbody != null) g.Rigidbody.velocity = Vector3.zero;
        });
    }
}

public sealed class Tazer : CustomItem
{
    public override string Id => "tazer";
    public override string Name => "Tazer";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.GunCOM18;
    public override string Description => "Shooting an enemy Tazes them. Holds 1 round, regenerates every 10s.";
    public override int AmmoCap => 1;
    public override int RegenAmount => 1;
    public override float RegenInterval => 10f;
    public override bool CanReload => false;

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev)
    {
        if (!target.IsEnemyOf(attacker)) { ev.IsAllowed = false; return; }
        Combat.SetDamage(ev, 1f);
        EffectManager.Apply<Tazed>(target, MayhemPlugin.Cfg.Effects.TazedSeconds);
    }
}

public sealed class HomecookedDinner : CustomItem
{
    public override string Id => "homecooked_dinner";
    public override string Name => "Homecooked Dinner";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.Medkit;
    public override string Description => "Heals you, then builds up to 280 AHP over time. Also Invigorated and Manic.";

    public override void OnUsed(Player p, Item item)
    {
        p.Heal(65f);
        p.CreateAhpProcess(280f, 280f, 1.2f, 1f, 6f, false);
        p.EnableEffect<Invigorated>(1, 60f);
        EffectManager.Apply<Manic>(p, 60f);
    }
}

public sealed class PaddedArmour : CustomItem
{
    public override string Id => "padded_armour";
    public override string Name => "Padded Armour";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.ArmorLight;
    public override string Description => "Silent Walk: your footsteps make no sound.";

    public override void OnInventoryTick(Player p, Item item)
    {
        if (!p.HasEffect<SilentWalk>()) p.EnableEffect<SilentWalk>(1);
    }

    public override void OnLeftInventory(Player p, ushort serial)
    {
        if (p.IsAlive) p.DisableEffect<SilentWalk>();
    }
}

public sealed class AnomalousTracker : CustomItem
{
    public override string Id => "anomalous_tracker";
    public override string Name => "Anomalous Tracker";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.Radio;
    public override string Description => "While held, tells you if an SCP is in your zone and how far away it is.";
    public override string UpgradeTo => "huginn_muninn";
    public override float UpgradeChance => 10f;

    public override void OnHeldTick(Player p, Item item)
    {
        var scps = PlayerExt.Alive.Where(o => o.IsScpOrPuppet() && o.Role != RoleTypeId.Scp079 && o.Zone == p.Zone && o != p).ToList();
        string text = scps.Count == 0
            ? "<color=#88FF88>📡 No SCPs in your zone</color>"
            : "<color=#FF8888>📡 " + string.Join(" · ", scps.Select(s => $"{(s.IsPuppet() ? "Puppet" : s.Role.ToString())} {Vector3.Distance(s.Position, p.Position):0}m")) + "</color>";
        Hud.SetInfo(p, "tracker", text, 0.6f, 4);
    }
}

public sealed class SwitchARoo : CustomItem
{
    public override string Id => "switch_a_roo";
    public override string Name => "Switch-A-Roo";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.Coin;
    public override string Description => "Flip to swap your whole inventory with someone else's (Puppet included). One use.";

    public override void OnFlipped(Player p, CoinItem coin, bool tails)
    {
        var target = Rng.Pick(PlayerExt.Alive.Where(o => o != p && (o.IsHuman || o.IsPuppet())).ToList());
        ItemEngine.Unregister(coin.Serial);
        p.RemoveItem(coin);
        if (target == null) { Msg(p, "Nobody to swap with"); return; }
        SwapInventories(p, target);
        Msg(p, $"Swapped inventories with {target.Nickname}");
        Msg(target, $"<color=#FFAA00>{p.Nickname} swapped inventories with you!</color>");
    }

    public static void SwapInventories(Player a, Player b)
    {
        var aPickups = a.Items.ToList().Select(i => i.DropItem()).Where(x => x != null).ToList();
        var bPickups = b.Items.ToList().Select(i => i.DropItem()).Where(x => x != null).ToList();
        foreach (var pk in aPickups) b.Take(pk);
        foreach (var pk in bPickups) a.Take(pk);
    }
}
