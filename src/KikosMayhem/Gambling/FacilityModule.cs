using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using KikosMayhem.Core;
using KikosMayhem.Effects;
using KikosMayhem.Items;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;
using UnityEngine;

namespace KikosMayhem.Gambling;

/// <summary>
/// Facility-wide Mayhem rules: spawn bonuses, SCP health scaling, nuke room radiation, first-escape custom item,
/// homing SCP-018, and automatic cleanup of old items / ragdolls.
/// </summary>
public sealed class FacilityModule : MayhemModule
{
    /// <summary>"All items replaced by SCP-1853" global event.</summary>
    public static bool AllItems1853;

    private readonly HashSet<string> _escaped = new();
    private readonly Dictionary<ReferenceHub, float> _inNukeRoom = new();
    private readonly Dictionary<ushort, float> _pickupBorn = new();
    private float _nextCleanup;

    public override void Register()
    {
        PlayerEvents.Spawned += OnSpawned;
        PlayerEvents.Escaped += OnEscaped;
        ServerEvents.PickupCreated += OnPickupCreated;
    }

    public override void Unregister()
    {
        PlayerEvents.Spawned -= OnSpawned;
        PlayerEvents.Escaped -= OnEscaped;
        ServerEvents.PickupCreated -= OnPickupCreated;
    }

    public override void Reset()
    {
        AllItems1853 = false;
        _escaped.Clear();
        _inNukeRoom.Clear();
        _pickupBorn.Clear();
        _nextCleanup = 0;
    }

    private void OnPickupCreated(PickupCreatedEventArgs ev)
    {
        if (ev.Pickup != null) _pickupBorn[ev.Pickup.Serial] = Time.time;
    }

    // ───────── spawns ─────────

    private static readonly RoleTypeId[] NoBonusRoles = { RoleTypeId.Tutorial, RoleTypeId.Spectator, RoleTypeId.Overwatch, RoleTypeId.Filmmaker };

    private void OnSpawned(PlayerSpawnedEventArgs ev)
    {
        var p = ev.Player;
        Loops.After(0.4f, () =>
        {
            if (!p.Valid() || !p.IsAlive) return;
            if (p.IsSCP) ScaleScpHealth(p);
            else if (p.IsHuman && !NoBonusRoles.Contains(p.Role) && !p.IsPuppet() && !Events.MyhmInsurgency.IsInsurgent(p))
                GiveSpawnBonuses(p);
        });
    }

    private static void ScaleScpHealth(Player p)
    {
        if (p.Role is RoleTypeId.Scp079 or RoleTypeId.Scp0492) return;
        if (p.IsPuppet()) return;
        float per = MayhemPlugin.Cfg.Scps.HealthPerPlayer;
        int players = Player.ReadyList.Count(x => !x.IsHost);
        float bonus = per * Mathf.Max(0, players - 1);
        p.MaxHealth += bonus;
        p.Health = p.MaxHealth;
    }

    private static void GiveSpawnBonuses(Player p)
    {
        var bonuses = MayhemPlugin.Cfg.Spawning.SpawnBonuses;
        var got = new List<string>();
        foreach (var kv in bonuses)
        {
            if (!Rng.Chance(kv.Value)) continue;
            if (ApplyBonus(p, kv.Key)) got.Add(kv.Key);
        }
        if (got.Count > 0) Hud.Message(p, "<color=#88CCFF>Spawn bonus: " + string.Join(", ", got) + "</color>", 4f);
    }

    private static bool ApplyBonus(Player p, string key)
    {
        if (key == "ExtraAmmo")
        {
            foreach (var gun in p.Items.OfType<FirearmItem>()) p.AddAmmo(gun.AmmoType, 60);
            if (p.Role == RoleTypeId.Scientist) p.AddAmmo(ItemType.Ammo9x19, 60);
            return true;
        }
        if (key == "Scp207" && AllItems1853)
        {
            p.EnableEffect<Scp1853>(5);
            for (int i = 0; i < 5; i++) GamblingModule.Give1853Crit(p);
            return true;
        }
        if (p.IsInventoryFull) return false;
        if (ItemSpawner.CausalityItem != null && key != "ExtraAmmo")
        {
            ItemEngine.Give(p, ItemSpawner.CausalityItem);
            return true;
        }
        ItemType type = key switch
        {
            "Adrenaline" => ItemType.Adrenaline,
            "Scp207" => ItemType.SCP207,
            "Scp500" => ItemType.SCP500,
            "A7" => ItemType.GunA7,
            "E11" => ItemType.GunE11SR,
            "Jailbird" => ItemType.Jailbird,
            "MicroHid" => ItemType.MicroHID,
            "Com45" => ItemType.GunCom45,
            "Scp018" => ItemType.SCP018,
            "OverloadedCom45" => ItemType.GunCom45,
            _ => ItemType.None,
        };
        if (type == ItemType.None) return false;
        var item = p.AddItem(type);
        if (item is FirearmItem fa)
        {
            fa.StoredAmmo = key == "OverloadedCom45" ? 400 : fa.MaxAmmo;
            p.AddAmmo(fa.AmmoType, 60);
        }
        return item != null;
    }

    private void OnEscaped(PlayerEscapedEventArgs ev)
    {
        var p = ev.Player;
        if (!_escaped.Add(p.UserId)) return;
        if (!Rng.Chance(MayhemPlugin.Cfg.Spawning.EscapeCustomItemChance)) return;
        Loops.After(0.6f, () =>
        {
            if (!p.Valid() || !p.IsAlive) return;
            var def = ItemSpawner.RandomDef();
            if (def == null) return;
            ItemEngine.Give(p, def);
            Hud.Message(p, $"<color={def.Rarity.Hex()}>Escape bonus: {def.Name}</color>", 4f);
        });
    }

    // ───────── ticking ─────────

    public override void SecondTick()
    {
        NukeRoomRadiation();
        if (Time.time > _nextCleanup)
        {
            _nextCleanup = Time.time + 30f;
            Cleanup();
        }
    }

    public override void SlowTick() => HomingBalls();

    private void NukeRoomRadiation()
    {
        var cfg = MayhemPlugin.Cfg.Effects;
        foreach (var p in PlayerExt.Alive.ToList())
        {
            if (p.Room == null || p.Room.Name != RoomName.HczWarhead)
            {
                _inNukeRoom.Remove(p.ReferenceHub);
                continue;
            }
            if (!_inNukeRoom.TryGetValue(p.ReferenceHub, out var since))
            {
                _inNukeRoom[p.ReferenceHub] = Time.time;
                continue;
            }
            float inside = Time.time - since;
            if (inside < cfg.NukeRoomGraceSeconds) continue;
            if (inside - cfg.NukeRoomGraceSeconds < 1.1f)
                Hud.Message(p, "<color=#7CFF00>☢ Radiation is building up in here...</color>", 3f);
            float rate = (p.IsSCP ? cfg.NukeRoomScpPercent : cfg.NukeRoomHumanPercent) / 5f;
            EffectManager.Apply<Irradiated>(p, 10f, rate);
        }
    }

    private static void HomingBalls()
    {
        foreach (var ball in TimedGrenadeProjectile.List.ToList())
        {
            if (ball.IsDestroyed || ball.Type != ItemType.SCP018 || ball.Rigidbody == null) continue;
            var pos = ball.Position;
            var target = PlayerExt.Alive
                .Where(o => o.Role != RoleTypeId.Scp079 && (o.Position - pos).sqrMagnitude < 144f)
                .OrderBy(o => (o.Position - pos).sqrMagnitude).FirstOrDefault();
            if (target == null) continue;
            var rb = ball.Rigidbody;
            var dir = (target.Position - pos).normalized;
            float speed = Mathf.Max(8f, rb.velocity.magnitude);
            rb.velocity = Vector3.Lerp(rb.velocity, dir * speed, 0.35f);
        }
    }

    private void Cleanup()
    {
        float maxAge = MayhemPlugin.Cfg.Spawning.CleanupAfterSeconds;
        if (maxAge <= 0) return;
        foreach (var p in Pickup.List.ToList())
        {
            if (p.IsDestroyed || p is Projectile || p.IsInUse || ItemEngine.IsCustom(p.Serial)) continue;
            if (p.Type is ItemType.KeycardO5 or ItemType.KeycardFacilityManager or ItemType.MicroHID) continue;
            if (!_pickupBorn.TryGetValue(p.Serial, out var born)) { _pickupBorn[p.Serial] = Time.time; continue; }
            if (Time.time - born > maxAge)
            {
                _pickupBorn.Remove(p.Serial);
                p.Destroy();
            }
        }
        foreach (var r in Ragdoll.List.ToList())
        {
            if (r.IsDestroyed) continue;
            if (Mirror.NetworkTime.time - r.Base.Info.CreationTime > maxAge) r.Destroy();
        }
    }
}
