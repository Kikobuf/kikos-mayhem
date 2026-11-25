using System.Collections.Generic;
using System.Linq;
using PrimitiveFlags = AdminToys.PrimitiveFlags;
using CustomPlayerEffects;
using JailbirdMessageType = InventorySystem.Items.Jailbird.JailbirdMessageType;
using KikosMayhem.Core;
using KikosMayhem.Effects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using PlayerRoles;
using UnityEngine;
using PrimitiveObjectToy = LabApi.Features.Wrappers.PrimitiveObjectToy;

namespace KikosMayhem.Items.Defs;

// ───────────────────────── Upgraded (Cyan) ─────────────────────────

public sealed class ScroogeGrenade : CustomItem
{
    public override string Id => "scrooge_grenade";
    public override string Name => "Scrooge Grenade";
    public override Rarity Rarity => Rarity.Upgraded;
    public override ItemType BaseType => ItemType.GrenadeHE;
    public override string Description => "After the explosion, 5 custom items spawn (any rarity except Admin).";

    public override void OnExploded(Player owner, Vector3 position)
    {
        for (int i = 0; i < 5; i++)
        {
            var def = ItemSpawner.RandomDef(d => d.Rarity != Rarity.Admin);
            if (def != null) ItemEngine.Spawn(def, ItemSpawner.Scatter(position, 1f));
        }
    }
}

public sealed class SonicSurge : CustomItem
{
    public override string Id => "sonic_surge";
    public override string Name => "Sonic Surge";
    public override Rarity Rarity => Rarity.Upgraded;
    public override ItemType BaseType => ItemType.GunAK;
    public override string Description => "Every hit on an enemy gives you +2% movement speed.";

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev)
    {
        if (target.IsEnemyOf(attacker)) Stats.Of(attacker).AddSpeed("sonic_surge", 2f);
    }
}

public sealed class HotswapCoin : CustomItem
{
    public override string Id => "hotswap_coin";
    public override string Name => "Hotswap Coin";
    public override Rarity Rarity => Rarity.Upgraded;
    public override ItemType BaseType => ItemType.Coin;
    public override string Description => "Swap positions with 1 random player. Doesn't work on SCPs (except Puppet).";

    public override bool OnFlipping(Player p, CoinItem coin) =>
        !ItemEngine.OnCooldown(p, coin.Serial, "hotswap", MayhemPlugin.Cfg.Items.WarpCoinCooldown);

    public override void OnFlipped(Player p, CoinItem coin, bool tails)
    {
        if (!p.CanTeleport()) return;
        var target = Rng.Pick(PlayerExt.Alive.Where(o => o != p && (!o.IsSCP || o.IsPuppet()) && o.CanTeleport()).ToList());
        if (target == null) { Msg(p, "Nobody to swap with"); return; }
        var a = p.Position;
        p.Position = target.Position;
        target.Position = a;
        Msg(p, $"Swapped places with {target.Nickname}");
        Msg(target, $"<color=#FFAA00>{p.Nickname} swapped places with you!</color>");
    }
}

public sealed class Realest207 : CustomItem
{
    public override string Id => "realest_207";
    public override string Name => "Realest SCP-207";
    public override Rarity Rarity => Rarity.Upgraded;
    public override ItemType BaseType => ItemType.SCP207;
    public override string Description => "4x SCP-207 and +100% movement speed.";

    public override void OnUsed(Player p, Item item)
    {
        p.EnableEffect<Scp207>(4);
        Stats.Of(p).SetSpeed("realest_207", 100f);
    }
}

public sealed class PaladinsElixir : CustomItem
{
    public override string Id => "paladins_elixir";
    public override string Name => "Paladin's Elixir";
    public override Rarity Rarity => Rarity.Upgraded;
    public override ItemType BaseType => ItemType.AntiSCP207;
    public override string Description => "2x SCP-207? and 87.5% damage reduction to ALL damage. (Weakens as Puppet takes damage.)";
    public override bool CausalityBlacklisted => true;

    public override void OnUsed(Player p, Item item)
    {
        p.EnableEffect<AntiScp207>(2);
        EffectManager.Remove<PaladinEffect>(p);
        EffectManager.Apply<PaladinEffect>(p);
    }
}

public sealed class RollingThunder : ExplosiveRevolver
{
    public override string Id => "rolling_thunder";
    public override string Name => "Rolling Thunder";
    public override Rarity Rarity => Rarity.Upgraded;
    public override string Description => "4-shot revolver; every shot explodes where it lands. +1 shot every 15s.";
    public override int AmmoCap => 4;
    public override int RegenAmount => 1;
    public override float RegenInterval => 15f;
}

// ───────────────────────── Ascended (Red) ─────────────────────────

public sealed class Hellfire : CustomItem
{
    public override string Id => "hellfire";
    public override string Name => "Hellfire";
    public override Rarity Rarity => Rarity.Ascended;
    public override ItemType BaseType => ItemType.GunLogicer;
    public override string Description => "Shooting an enemy spawns an orange bubble of fire that deals high damage and burns anyone inside. Regenerates 5 ammo per second.";
    public override int RegenAmount => 5;
    public override float RegenInterval => 1f;
    public override bool CausalityBlacklisted => true;

    private static readonly Dictionary<ReferenceHub, float> NextZone = new();

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev)
    {
        if (!target.IsEnemyOf(attacker)) return;
        if (NextZone.TryGetValue(attacker.ReferenceHub, out var t) && t > Time.time) return;
        NextZone[attacker.ReferenceHub] = Time.time + 0.75f;
        FireZone(target.Position, attacker);
    }

    public static void FireZone(Vector3 pos, Player owner)
    {
        const float radius = 2.5f;
        var bubble = PrimitiveObjectToy.Create(pos, Quaternion.identity, Vector3.one * radius * 2f, null, false);
        bubble.Type = PrimitiveType.Sphere;
        bubble.Color = new Color(1f, 0.45f, 0f, 0.35f);
        bubble.Flags = PrimitiveFlags.Visible; // no collision
        bubble.Spawn();
        var light = LightSourceToy.Create(pos, null, false);
        light.Color = new Color(1f, 0.4f, 0f);
        light.Range = radius * 2.5f;
        light.Intensity = 2f;
        light.Spawn();

        int ticks = 0;
        Loops.Run(Burn());
        IEnumerator<float> Burn()
        {
            while (ticks++ < 16)
            {
                foreach (var p in PlayerExt.Near(pos, radius).ToList())
                {
                    if (owner.Valid() && !p.IsEnemyOf(owner) && p != owner) continue;
                    float dmg = p.IsSCP ? 4.5f : 2f;
                    if (owner.Valid() && owner != p) p.Damage(dmg, owner);
                    else p.Damage(dmg, "Burned by Hellfire.");
                    var b = EffectManager.Apply<Burning>(p, 0, 1);
                    if (b != null) b.Source = owner;
                }
                yield return MEC.Timing.WaitForSeconds(0.5f);
            }
            if (!bubble.IsDestroyed) bubble.Destroy();
            if (!light.IsDestroyed) light.Destroy();
        }
    }
}

public sealed class PocketNuke : CustomItem
{
    public override string Id => "pocket_nuke";
    public override string Name => "Pocket Nuke";
    public override Rarity Rarity => Rarity.Ascended;
    public override ItemType BaseType => ItemType.GrenadeHE;
    public override string Description => "Blows up the whole area it's thrown in. Everyone nearby gets Irradiated (10% HP/second) — only SCP-500 cures it.";
    public override bool CausalityBlacklisted => true;

    public override void OnExploded(Player owner, Vector3 position)
    {
        var cfg = MayhemPlugin.Cfg.Items;
        for (int i = 0; i < 6; i++)
            PlayerExt.Explode(position + new Vector3(Rng.Range(-8f, 8f), 0.5f, Rng.Range(-8f, 8f)), owner);
        foreach (var p in PlayerExt.Near(position, cfg.PocketNukeRadius).ToList())
        {
            EffectManager.Apply<Irradiated>(p, 0, cfg.PocketNukeIrradiatedPercent);
            Hud.Message(p, "<color=#7CFF00>☢ You are irradiated! Find SCP-500!</color>", 4f);
        }
    }
}

public sealed class MayhemCoin : CustomItem
{
    public override string Id => "mayhem_coin";
    public override string Name => "Mayhem Coin";
    public override Rarity Rarity => Rarity.Ascended;
    public override ItemType BaseType => ItemType.Coin;
    public override string Description => "Shuffles EVERY living player: classes, locations, items and effects. 300s cooldown.";
    public override bool CausalityBlacklisted => true;

    public override bool OnFlipping(Player p, CoinItem coin) =>
        !ItemEngine.OnCooldown(p, coin.Serial, "mayhem", MayhemPlugin.Cfg.Items.MayhemCoinCooldown);

    public override void OnFlipped(Player p, CoinItem coin, bool tails)
    {
        var all = PlayerExt.Alive.Where(o => o.Role != RoleTypeId.Scp079 && o.CanTeleport()).ToList();
        if (all.Count < 2) return;

        // Locations: everyone.
        var positions = Rng.Shuffle(all.Select(o => o.Position));
        // Classes / items / effects: humans only (Puppet & SCPs keep their role).
        var humans = all.Where(o => o.IsHuman && !o.IsPuppet()).ToList();
        var roles = Rng.Shuffle(humans.Select(h => h.Role));
        var inventories = humans.Select(h => h.Items.ToList().Select(i => i.DropItem()).Where(x => x != null).ToList()).ToList();
        var effects = humans.Select(EffectManager.Take).ToList();
        inventories = Rng.Shuffle(inventories);
        effects = Rng.Shuffle(effects);

        for (int i = 0; i < humans.Count; i++)
        {
            var h = humans[i];
            if (roles[i] != h.Role) h.SetRole(roles[i], RoleChangeReason.RemoteAdmin, RoleSpawnFlags.None);
        }
        Loops.After(0.2f, () =>
        {
            for (int i = 0; i < humans.Count; i++)
            {
                var h = humans[i];
                if (!h.Valid() || !h.IsAlive) continue;
                foreach (var pk in inventories[i]) h.Take(pk);
                EffectManager.Give(h, effects[i]);
            }
            for (int i = 0; i < all.Count; i++)
                if (all[i].Valid() && all[i].IsAlive) all[i].Position = positions[i];
        });
        Hud.MessageAll($"<color=#FF3030>{p.Nickname} flipped the MAYHEM COIN</color>");
    }
}

public sealed class HellsReckoning : CustomItem
{
    public override string Id => "hells_reckoning";
    public override string Name => "Hell's Reckoning";
    public override Rarity Rarity => Rarity.Ascended;
    public override ItemType BaseType => ItemType.Jailbird;
    public override string Description => "Equip to unleash it: 6x crit damage, +100%→200% speed, 3x SCP-207, 85% damage reduction and bypass. You have 2 minutes to kill an SCP for an ADMIN item. Fail, or break it, and you die.";
    public override bool CausalityBlacklisted => true;

    private static readonly HashSet<ushort> Started = new();

    public static void Reset() => Started.Clear();

    public override void OnEquipped(Player p, Item item)
    {
        var existing = EffectManager.Get<ReckoningEffect>(p);
        if (existing != null || p.IsSCP) return;
        Started.Add(item.Serial);
        var e = EffectManager.Apply<ReckoningEffect>(p, MayhemPlugin.Cfg.Items.HellsReckoningSeconds);
        if (e != null) e.Serial = item.Serial;
        Hud.MessageAll($"<color=#FF2020>{p.Nickname} has taken up HELL'S RECKONING</color>");
    }

    public override void OnJailbird(Player p, JailbirdItem jailbird, JailbirdMessageType message)
    {
        if (message != JailbirdMessageType.Broken) return;
        var e = EffectManager.Get<ReckoningEffect>(p);
        ItemEngine.Unregister(jailbird.Serial);
        if (e != null) EffectManager.Remove(p, e);
        if (p.IsAlive) p.Kill("Hell's Reckoning shattered.");
    }

    public static void OnAnyKill(Player killer, Player victim, PlayerDeathEventArgs ev)
    {
        var e = EffectManager.Get<ReckoningEffect>(killer);
        if (e == null || ev.OldRole.GetTeam() != Team.SCPs) return;
        e.Succeeded = true;
        EffectManager.Remove(killer, e);
        var jb = Item.Get(e.Serial);
        if (jb != null && jb.CurrentOwner == killer)
        {
            ItemEngine.Unregister(e.Serial);
            killer.RemoveItem(jb);
        }
        var reward = Rng.Pick(ItemEngine.All.Where(d => d.Rarity == Rarity.Admin && !d.Hidden && d is not MitzeysScream && !ItemEngine.IsDisabled(d)).ToList());
        if (reward != null)
        {
            ItemEngine.Give(killer, reward);
            Hud.MessageAll($"<color=#FF66FF>{killer.Nickname} survived Hell's Reckoning and earned {reward.Name}!</color>");
        }
    }
}

// ───────────────────────── Admin (Rainbow) ─────────────────────────

public sealed class LiveDuckReaction : CustomItem
{
    public override string Id => "live_duck_reaction";
    public override string Name => "Live Duck Reaction";
    public override Rarity Rarity => Rarity.Admin;
    public override ItemType BaseType => ItemType.GrenadeFlash;
    public override string Description => "On impact, detonates over and over. Blinds enemies for 100+ seconds.";

    public override void OnExploded(Player owner, Vector3 position)
    {
        for (int i = 1; i <= 8; i++)
        {
            int n = i;
            Loops.After(n * 0.35f, () => TimedGrenadeProjectile.PlayEffect(position, ItemType.GrenadeFlash));
        }
        foreach (var p in PlayerExt.Near(position, 14f).Where(o => owner == null || o.IsEnemyOf(owner)).ToList())
        {
            p.EnableEffect<Flashed>(1, 5f);
            p.EnableEffect<Blindness>(255, 100f);
        }
    }
}

public sealed class Ultimate500 : CustomItem
{
    public override string Id => "ultimate_500";
    public override string Name => "Ultimate 500";
    public override Rarity Rarity => Rarity.Admin;
    public override ItemType BaseType => ItemType.SCP500;
    public override string Description => "Applies 100 SCP-500 effects all at once.";

    public override void OnUsed(Player p, Item item)
    {
        p.Heal(p.MaxHealth);
        for (int i = 0; i < 100 && p.IsAlive; i++) Gambling.GamblingModule.Resolve(p, ItemType.SCP500, quiet: true);
    }
}

public sealed class OverchargedAdrenaline : CustomItem
{
    public override string Id => "overcharged_adrenaline";
    public override string Name => "Overcharged Adrenaline";
    public override Rarity Rarity => Rarity.Admin;
    public override ItemType BaseType => ItemType.Adrenaline;
    public override string Description => "Applies the effects of 100 adrenalines all at once. Good luck.";

    public override void OnUsed(Player p, Item item)
    {
        for (int i = 0; i < 100 && p.IsAlive; i++) Gambling.GamblingModule.Resolve(p, ItemType.Adrenaline, quiet: true);
    }
}

public sealed class Elixir : CustomItem
{
    public override string Id => "elixir";
    public override string Name => "Elixir";
    public override Rarity Rarity => Rarity.Admin;
    public override ItemType BaseType => ItemType.AntiSCP207;
    public override string Description => "SCP-1853, Invigorated, SCP-207 x255, 65% negative effect & damage reduction, +255% speed, negative effect immunity, Ghostly, Silent Walk and no fall damage.";

    public override void OnUsed(Player p, Item item)
    {
        var s = Stats.Of(p);
        p.EnableEffect<Scp1853>(1);
        p.EnableEffect<Invigorated>(1);
        p.EnableEffect<Scp207>(255);
        s.SetNegativeReduction("elixir", 65f);
        s.SetDamageReduction("elixir", 65f);
        s.SetSpeed("elixir", 255f);
        s.NegativeImmuneUntil = float.MaxValue;
        p.EnableEffect<Ghostly>(1);
        p.EnableEffect<SilentWalk>(1);
        s.Flags.Add("no_fall_damage");
    }
}

public sealed class MitzeysScream : CustomItem
{
    public override string Id => "mitzeys_scream";
    public override string Name => "Mitzey's Scream";
    public override Rarity Rarity => Rarity.Admin;
    public override ItemType BaseType => ItemType.GunCom45;
    public override string Description => "200-round COM-45 that fires explosive shots. Removed from Hell's loot pool for crashing servers — admin give only.";
    public override bool NaturalSpawn => false;
    public override int AmmoCap => 200;
    public override int RegenAmount => 5;
    public override float RegenInterval => 0.5f;

    private readonly Dictionary<ReferenceHub, float> _next = new();

    public override void OnShot(Player p, FirearmItem firearm, Player hit)
    {
        if (_next.TryGetValue(p.ReferenceHub, out var t) && t > Time.time) return; // throttle explosions
        _next[p.ReferenceHub] = Time.time + 0.2f;
        PlayerExt.Explode(hit != null ? hit.Position : p.AimPoint(60f), p);
    }
}
