using System;
using System.Collections.Generic;
using System.Linq;
using Interactables.Interobjects;
using KikosMayhem.Core;
using LabApi.Features.Wrappers;
using MapGeneration;
using UnityEngine;

namespace KikosMayhem.Items;

/// <summary>Natural custom item spawns, the special piles (Gate A, Exit, SCP-079's room) and helpers to find safe spots.</summary>
public static class ItemSpawner
{
    /// <summary>Set by the 5x Custom Item Loot Spawn global event.</summary>
    public static float CustomMultiplier = 1f;

    /// <summary>Set by Causality Corruption: the only custom item that spawns this round.</summary>
    public static CustomItem CausalityItem;

    public static void Reset()
    {
        CustomMultiplier = 1f;
        CausalityItem = null;
        _scp079PileSpawned = false;
    }

    // ───────── picking ─────────

    public static CustomItem RandomDef(Func<CustomItem, bool> filter = null)
    {
        if (CausalityItem != null && (filter == null || filter(CausalityItem))) return CausalityItem;
        var weights = MayhemPlugin.Cfg.Spawning.RarityWeights;
        var pool = ItemEngine.Spawnable.Where(d => filter == null || filter(d)).ToList();
        if (pool.Count == 0) return null;
        var rarity = Rng.Weighted(pool.Select(d => d.Rarity).Distinct()
            .Select(r => (r, weights.TryGetValue(r.ToString(), out var w) ? w : 0f)));
        var ofRarity = pool.Where(d => d.Rarity == rarity).ToList();
        return Rng.Pick(ofRarity.Count > 0 ? ofRarity : pool);
    }

    // ───────── safe positions ─────────

    /// <summary>A walkable spot inside a room: next to one of its doors, nudged toward the room center.</summary>
    public static Vector3? SpotIn(Room room)
    {
        if (room == null) return null;
        // Prefer where the game itself put an item.
        var existing = Pickup.List.Where(p => !p.IsDestroyed && p.Room == room && p is not Projectile).ToList();
        if (existing.Count > 0 && Rng.Chance(70))
            return Rng.Pick(existing).Position + Vector3.up * 0.15f;

        var doors = room.Doors.Where(d => d is not LabApi.Features.Wrappers.ElevatorDoor && d.Base != null).ToList();
        if (doors.Count == 0) return room.Position + Vector3.up * 1f;
        var door = Rng.Pick(doors);
        var toCenter = room.Position - door.Position;
        toCenter.y = 0;
        float dist = Mathf.Min(toCenter.magnitude * 0.6f, 3.5f);
        return door.Position + toCenter.normalized * dist + Vector3.up * 0.6f;
    }

    public static Vector3? RandomSpot(params FacilityZone[] zones)
    {
        var rooms = Room.List.Where(r => !r.IsDestroyed && (zones.Length == 0 ? r.Zone is FacilityZone.LightContainment or FacilityZone.HeavyContainment or FacilityZone.Entrance : zones.Contains(r.Zone))
                                         && !IsBlacklisted(r)).ToList();
        return rooms.Count == 0 ? null : SpotIn(Rng.Pick(rooms));
    }

    public static bool IsBlacklisted(Room r) =>
        MayhemPlugin.Cfg.Spawning.BlacklistedRooms.Contains(r.Name.ToString());

    /// <summary>The surface exit of the Gate A elevator.</summary>
    public static Vector3 GateASurface()
    {
        var elevator = Elevator.GetByGroup(ElevatorGroup.GateA01).FirstOrDefault() ?? Elevator.GetByGroup(ElevatorGroup.GateA02).FirstOrDefault();
        var door = elevator?.Doors.OrderByDescending(d => d.Position.y).FirstOrDefault();
        if (door == null) return Map.DefaultEscapeZone.center;
        var chamber = elevator.Base.transform.position;
        var fwd = door.Transform.forward;
        var a = door.Position + fwd * 4f;
        var b = door.Position - fwd * 4f;
        var outside = (a - chamber).sqrMagnitude > (b - chamber).sqrMagnitude ? a : b;
        return outside + Vector3.up * 0.8f;
    }

    public static Vector3 Exit() => Map.DefaultEscapeZone.center + Vector3.up * 0.5f;

    public static Room Scp079Room => Room.Get(RoomName.Hcz079).FirstOrDefault();

    // ───────── round start spawning ─────────

    public static void SpawnRoundItems()
    {
        var cfg = MayhemPlugin.Cfg.Spawning;
        int spawned = 0;
        foreach (var room in Room.List.ToList())
        {
            if (room.IsDestroyed || IsBlacklisted(room)) continue;
            if (room.Zone is not (FacilityZone.LightContainment or FacilityZone.HeavyContainment or FacilityZone.Entrance)) continue;
            if (room.Name == RoomName.Hcz079) continue; // its own pile
            float expected = cfg.CustomItemsPerRoom * CustomMultiplier;
            int count = (int)expected + (Rng.Chance(expected % 1f * 100f) ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                var def = RandomDef();
                var spot = SpotIn(room);
                if (def == null || spot == null) continue;
                ItemEngine.Spawn(def, spot.Value);
                spawned++;
            }
        }

        SpawnPile(GateASurface(), (int)(cfg.GateACustomItems * Mathf.Max(1f, CustomMultiplier)), cfg.GateANormalItems);
        SpawnPile(Exit(), (int)(cfg.ExitCustomItems * Mathf.Max(1f, CustomMultiplier)), 0);
        LabApi.Features.Console.Logger.Info($"[Mayhem] spawned {spawned} custom items + surface piles");
    }

    private static readonly ItemType[] NormalPileItems =
    {
        ItemType.Medkit, ItemType.Adrenaline, ItemType.Painkillers, ItemType.GrenadeHE, ItemType.GrenadeFlash,
        ItemType.GunE11SR, ItemType.GunCrossvec, ItemType.GunFSP9, ItemType.GunCOM18, ItemType.KeycardMTFOperative,
        ItemType.ArmorCombat, ItemType.SCP207, ItemType.SCP500, ItemType.Radio, ItemType.Ammo556x45, ItemType.Ammo9x19,
    };

    public static void SpawnPile(Vector3 center, int customCount, int normalCount, Func<CustomItem, bool> filter = null)
    {
        for (int i = 0; i < customCount; i++)
        {
            var def = RandomDef(filter);
            if (def != null) ItemEngine.Spawn(def, Scatter(center, 1.5f));
        }
        for (int i = 0; i < normalCount; i++)
            Pickup.Create(Rng.Pick(NormalPileItems), Scatter(center, 1.8f));
    }

    public static Vector3 Scatter(Vector3 center, float radius)
    {
        var o = UnityEngine.Random.insideUnitCircle * radius;
        return center + new Vector3(o.x, 0.3f, o.y);
    }

    private static bool _scp079PileSpawned;

    /// <summary>SCP-079's room: guaranteed custom items, only if 079 isn't playing (or once it's contained).</summary>
    public static void TrySpawn079Pile(bool force = false)
    {
        if (_scp079PileSpawned) return;
        if (!force && Player.ReadyList.Any(p => p.Role == PlayerRoles.RoleTypeId.Scp079)) return;
        var room = Scp079Room;
        if (room == null) return;
        var spot = SpotIn(room);
        if (spot == null) return;
        _scp079PileSpawned = true;
        var cfg = MayhemPlugin.Cfg.Spawning;
        int count = cfg.Scp079RoomCustomItems + (Rng.Chance(cfg.Scp079RoomExtraChance) ? 1 : 0);
        SpawnPile(spot.Value, count, 0);
    }
}

/// <summary>
/// Custom items (and O5 / Facility Manager cards) can't be removed from the round. If they fall out of
/// the map they're sent to SCP-106's dimension, and after a while recycled back into the facility.
/// </summary>
public static class Recycling
{
    private static readonly HashSet<ushort> InPocket = new();

    public static void Reset() => InPocket.Clear();

    private static bool Protected(Pickup p) =>
        ItemEngine.IsCustom(p.Serial) || p.Type is ItemType.KeycardO5 or ItemType.KeycardFacilityManager;

    public static void Tick()
    {
        if (!MayhemPlugin.Cfg.Items.RecycleFromPits) return;
        foreach (var p in Pickup.List.ToList())
        {
            if (p.IsDestroyed || p is Projectile || !Protected(p) || InPocket.Contains(p.Serial)) continue;
            var pos = p.Position;
            bool lost = pos.y < -2000f || (Room.GetRoomAtPosition(pos) == null && pos.y < 250f && pos.y > -1900f && FellFast(p));
            if (!lost) continue;
            SendToPocket(p);
        }
    }

    private static bool FellFast(Pickup p) => p.Rigidbody != null && p.Rigidbody.velocity.y < -12f;

    private static void SendToPocket(Pickup p)
    {
        var pocket = Room.Get(RoomName.Pocket).FirstOrDefault();
        if (pocket == null) return;
        InPocket.Add(p.Serial);
        p.Position = pocket.Position + new Vector3(Rng.Range(-6f, 6f), 1.2f, Rng.Range(-6f, 6f));
        if (p.Rigidbody != null) p.Rigidbody.velocity = Vector3.zero;
        ushort serial = p.Serial;
        Loops.After(MayhemPlugin.Cfg.Items.RecycleDelaySeconds, () =>
        {
            InPocket.Remove(serial);
            var still = Pickup.Get(serial);
            if (still == null || still.IsDestroyed) return; // someone grabbed it
            var spot = ItemSpawner.RandomSpot();
            if (spot == null) return;
            still.Position = spot.Value + Vector3.up * 1.5f; // "drops from his dimension"
            if (still.Rigidbody != null) still.Rigidbody.velocity = Vector3.zero;
        });
    }
}
