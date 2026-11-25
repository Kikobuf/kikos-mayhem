using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using KikosMayhem.Core;
using KikosMayhem.Effects;
using KikosMayhem.Items;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;
using UnityEngine;

namespace KikosMayhem.Gambling;

/// <summary>
/// Mayhem's gambling consumables: every vanilla painkiller, adrenaline and SCP-500 rolls a random outcome
/// on top of its normal effect. Also handles SCP-1853 crits and the Addictionater overdose queue.
/// </summary>
public sealed class GamblingModule : MayhemModule
{
    /// <summary>Set by the "Item Chances Inversed" global event.</summary>
    public static bool Inversed;

    private static int _depth;

    public override void Register()
    {
        PlayerEvents.UsingItem += OnUsing;
        PlayerEvents.UsedItem += OnUsed;
    }

    public override void Unregister()
    {
        PlayerEvents.UsingItem -= OnUsing;
        PlayerEvents.UsedItem -= OnUsed;
    }

    public override void Reset()
    {
        Inversed = false;
        _depth = 0;
    }

    private static bool IsGambling(ItemType t) => t is ItemType.Painkillers or ItemType.Adrenaline or ItemType.SCP500;

    /// <summary>During an Addictionater overdose, gambling items are eaten instantly and queued.</summary>
    private static void OnUsing(PlayerUsingItemEventArgs ev)
    {
        if (ItemEngine.IsCustom(ev.UsableItem.Serial) || !IsGambling(ev.UsableItem.Type)) return;
        var od = EffectManager.Get<Overdose>(ev.Player);
        if (od == null) return;
        ev.IsAllowed = false;
        od.Queue.Add(ev.UsableItem.Type);
        od.Expires += MayhemPlugin.Cfg.Gambling.AddictionaterSecondsPerItem;
        ev.Player.RemoveItem(ev.UsableItem);
        Hud.Message(ev.Player, $"<color=#FF66CC>+1 {ev.UsableItem.Type} queued</color>", 1.2f, "od");
    }

    private static void OnUsed(PlayerUsedItemEventArgs ev)
    {
        if (ItemEngine.IsCustom(ev.UsableItem.Serial)) return;
        var p = ev.Player;
        var type = ev.UsableItem.Type;
        if (type == ItemType.SCP1853)
        {
            Give1853Crit(p);
            return;
        }
        if (type == ItemType.SCP500) EffectManager.Remove<Irradiated>(p);
        if (!IsGambling(type)) return;
        if (p.IsPuppet()) return; // Puppet gets nothing from gambling items
        Resolve(p, type);
    }

    public static void Give1853Crit(Player p)
    {
        var cfg = MayhemPlugin.Cfg.Effects;
        if (p.IsPuppet()) Stats.Of(p).AddCrit("scp1853", cfg.Scp1853PuppetCritChance, cfg.Scp1853PuppetCritDamage);
        else Stats.Of(p).AddCrit("scp1853", cfg.Scp1853CritChance, cfg.Scp1853CritDamage);
    }

    /// <summary>Rolls one gambling outcome for the given consumable type.</summary>
    public static void Resolve(Player p, ItemType type, bool quiet = false)
    {
        if (!p.Valid() || !p.IsAlive || _depth > 3) return;
        _depth++;
        try
        {
            string result = type switch
            {
                ItemType.Painkillers => Painkillers(p),
                ItemType.Adrenaline => Adrenaline(p),
                ItemType.SCP500 => Scp500(p),
                _ => null,
            };
            if (!quiet && result != null && p.IsAlive) Hud.Message(p, $"<color=#FFD966>🎲 {result}</color>", 3f, "gamble");
        }
        finally { _depth--; }
    }

    // ───────── Painkillers ─────────

    private static string Painkillers(Player p)
    {
        var cfg = MayhemPlugin.Cfg.Gambling;
        float death = Inversed ? cfg.InversedPainkillerDeathChance : cfg.PainkillerDeathChance;
        if (Rng.Chance(death))
        {
            p.DropEverything();
            p.Kill("Took unsafe pills.");
            return null;
        }
        if (Rng.Chance(cfg.PainkillerSpeedChance))
        {
            Stats.Of(p).AddSpeed("painkillers", cfg.PainkillerSpeedAmount, cfg.PainkillerSpeedCap);
            return $"+{cfg.PainkillerSpeedAmount}% speed";
        }
        return "Nothing happened";
    }

    // ───────── SCP-500 ─────────

    private static string Scp500(Player p)
    {
        var weights = MayhemPlugin.Cfg.Gambling.Scp500.Select(kv =>
            (kv.Key, Inversed && kv.Key == "ReviveAll" ? kv.Value * 10f : kv.Value));
        string roll = Rng.Weighted(weights);
        switch (roll)
        {
            case "ReviveOne":
            {
                var dead = Rng.Pick(PlayerExt.Spectators.ToList());
                if (dead == null) return "Nobody to revive";
                ReviveAs(dead, p);
                return $"Revived {dead.Nickname}";
            }
            case "ReviveAll":
            {
                var dead = PlayerExt.Spectators.ToList();
                foreach (var d in dead) ReviveAs(d, p);
                Hud.MessageAll($"<color=#FFD700>{p.Nickname}'s SCP-500 revived everyone!</color>");
                return $"REVIVE ALL ({dead.Count})";
            }
            case "Zombie":
            {
                var pos = p.Position;
                p.SetRole(RoleTypeId.Scp0492, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.None);
                Loops.After(0.15f, () =>
                {
                    if (!p.Valid()) return;
                    p.Position = pos;
                    p.MaxHealth = 1500;
                    p.Health = 1500;
                });
                return "You turned into a zombie!";
            }
            case "RandomizeInventory":
            {
                var items = p.Items.Where(i => !ItemEngine.IsCustom(i.Serial)).ToList();
                foreach (var i in items)
                {
                    p.RemoveItem(i);
                    var all = System.Enum.GetValues(typeof(ItemType)).Cast<ItemType>()
                        .Where(t => t != ItemType.None && t != ItemType.ParticleDisruptor && !t.ToString().StartsWith("Snake") && !t.ToString().Contains("Marshmallow")).ToList();
                    var t = Rng.Pick(all);
                    if (t.ToString().StartsWith("Ammo")) p.AddAmmo(t, 40);
                    else p.AddItem(t);
                }
                return "Your inventory was randomized";
            }
            case "RandomConsumables":
            {
                int n = Rng.Range(1, 5);
                var pool = new[] { ItemType.SCP500, ItemType.Adrenaline, ItemType.Painkillers };
                for (int i = 0; i < n && p.IsAlive; i++) Resolve(p, Rng.Pick(pool), quiet: true);
                return $"{n} random doses!";
            }
            case "TeleportGuardsToClassD":
            {
                if (p.Role != RoleTypeId.ClassD) return "Nothing happened";
                foreach (var o in PlayerExt.Alive.Where(o => o.Team == Team.FoundationForces && o.CanTeleport()).ToList())
                {
                    o.Position = p.Position;
                    Hud.Message(o, $"<color=#FF7070>{p.Nickname}'s SCP-500 summoned you!</color>");
                }
                return "Summoned every guard and MTF to you!";
            }
            case "CureZombies":
            {
                var zombies = PlayerExt.Alive.Where(o => o.Role == RoleTypeId.Scp0492).Take(Rng.Range(1, 4)).ToList();
                if (zombies.Count == 0 || !p.IsHuman) return "Nothing happened";
                foreach (var z in zombies) z.SetRole(p.Role, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.All);
                return $"Cured {zombies.Count} zombies";
            }
            case "TeleportToScp":
            {
                var scp = PlayerExt.Alive.Where(o => o.IsSCP && o.Role is not (RoleTypeId.Scp079 or RoleTypeId.Scp0492))
                    .OrderByDescending(o => o.MaxHealth).FirstOrDefault();
                if (scp == null) return "Nothing happened";
                p.Position = scp.Position + Vector3.up * 0.3f;
                return $"Teleported to {scp.Role}!";
            }
            default:
                return "Nothing happened";
        }
    }

    /// <summary>Respawns a spectator as the reviver's class at the reviver's position.</summary>
    public static void ReviveAs(Player dead, Player reviver)
    {
        if (!dead.Valid() || dead.IsAlive) return;
        var role = reviver.IsPuppet() || reviver.IsSCP ? RoleTypeId.Scp0492 : reviver.Role;
        var pos = reviver.Position;
        dead.SetRole(role, RoleChangeReason.Respawn, RoleSpawnFlags.AssignInventory);
        Loops.After(0.15f, () => { if (dead.Valid() && dead.IsAlive) dead.Position = pos + Vector3.up * 0.3f; });
        Hud.Message(dead, $"<color=#88FFAA>Revived by {reviver.Nickname}</color>");
    }

    // ───────── Adrenaline ─────────

    private static string Adrenaline(Player p)
    {
        var weights = MayhemPlugin.Cfg.Gambling.Adrenaline.Select(kv =>
            (kv.Key, Inversed && kv.Key == "GasGasGas" ? kv.Value * 8f : kv.Value));
        string roll = Rng.Weighted(weights);
        var s = Stats.Of(p);
        switch (roll)
        {
            case "Scp1853":
                if (p.TryGetEffect<Scp1853>(out var e1853) && e1853.Intensity >= 5) return "SCP-1853 (maxed)";
                p.EnableEffect<Scp1853>((byte)Mathf.Min(5, (p.GetEffect<Scp1853>()?.Intensity ?? 0) + 1));
                Give1853Crit(p);
                return "SCP-1853";
            case "Scp207":
                p.EnableEffect<Scp207>((byte)Mathf.Min(4, (p.GetEffect<Scp207>()?.Intensity ?? 0) + 1));
                return "SCP-207";
            case "Speed":
                s.AddSpeed("adrenaline", 15f);
                return "+15% speed";
            case "SevenBalls":
                for (int i = 0; i < 7; i++) TimedGrenadeProjectile.SpawnActive(p.Position + Vector3.up, ItemType.SCP018, p);
                return "7 balls!";
            case "SeveredHands":
                p.EnableEffect<SeveredHands>(1);
                return "Your hands fell off";
            case "CardiacArrest":
                p.EnableEffect<CardiacArrest>(1, 8f);
                return "Cardiac arrest!";
            case "BadAdrenaline":
                p.Kill("Bad adrenaline.");
                return null;
            case "DamageReductionAll":
                s.AddDamageReduction("adrenaline_dr", 5f, 50f);
                return "+5% damage reduction";
            case "DamageReductionBody":
                p.EnableEffect<BodyshotReduction>((byte)Mathf.Min(4, (p.GetEffect<BodyshotReduction>()?.Intensity ?? 0) + 1));
                return "+5% body damage reduction";
            case "NegativeReduction":
            {
                int doses = s.Get("adr_neg", 0) + 1;
                s.Set("adr_neg", doses);
                float pct = doses == 1 ? 40f : doses == 2 ? 60f : 65f;
                s.SetNegativeReduction("adrenaline", pct);
                return $"{pct}% negative effect reduction";
            }
            case "Stained":
                p.EnableEffect<Stained>(1, 15f);
                return "Stained (-20% speed)";
            case "Sinkhole":
                p.EnableEffect<Sinkhole>(1, 10f);
                return "Sinkhole (-20% speed)";
            case "GasGasGas":
                GasGasGas.Start(p);
                return "GAS GAS GAS";
            case "TurboDrunk":
                EffectManager.Apply<TurboDrunk>(p, 0, 1);
                return "TURBO drunk";
            case "Flash":
                p.EnableEffect<Flashed>(1, 3f);
                p.Damage(50f, "Flashbanged by adrenaline.");
                return "Flashed!";
            case "Burned":
                p.EnableEffect<Burned>(1, 15f);
                return "Burned";
            case "Tazed":
                EffectManager.Apply<Tazed>(p, 3f);
                return "Tazed!";
            case "TeleportSpawn":
            {
                var rooms = new[] { RoomName.LczClassDSpawn, RoomName.Hcz096, RoomName.Lcz173, RoomName.Hcz049, RoomName.Hcz939 };
                if (Rng.Chance(30) && !Warhead.IsDetonated)
                {
                    p.Position = Rng.Chance(50) ? ItemSpawner.Exit() : ItemSpawner.GateASurface();
                    return "Teleported to a surface spawn";
                }
                var room = Room.Get(Rng.Pick(rooms)).FirstOrDefault();
                if (room == null || (room.Zone == FacilityZone.LightContainment && Decontamination.IsDecontaminating)) return "Nothing happened";
                Items.Defs.Teleports.ToRoom(p, room);
                return $"Teleported to {room.Name}";
            }
            case "Deafened":
                p.EnableEffect<Deafened>(1, 20f);
                return "Deafened";
            case "Confused":
                p.EnableEffect<Blurred>(1, 15f);
                return "Confused";
            case "SixGrenades":
                for (int i = 0; i < 6; i++)
                    TimedGrenadeProjectile.SpawnActive(p.Position + Random.insideUnitSphere * 2f + Vector3.up, ItemType.GrenadeHE, p, 2.5);
                return "6 grenades!";
            case "Disabled":
                p.EnableEffect<Disabled>(1, 15f);
                return "Disabled (-20% speed)";
            case "Herd":
                EffectManager.Apply<Herd>(p, 60f);
                return "Herd";
            case "Irradiated":
            {
                var irr = EffectManager.Apply<Irradiated>(p, 15f, 0.0001f);
                if (irr != null) irr.Flat = 3f;
                return "Irradiated (-3 HP/s)";
            }
            case "Explode":
                p.Explode();
                return null;
            default:
                return "Nothing happened";
        }
    }
}
