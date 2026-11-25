using System.Collections.Generic;
using System.Linq;
using KikosMayhem.Core;
using LabApi.Features.Wrappers;
using MapGeneration;
using MapGeneration.Distributors;
using Mirror;
using PlayerRoles.PlayableScps.Scp079;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace KikosMayhem.Scp079;

/// <summary>
/// Mayhem 079 rounds have 2–5 generators depending on player count. Extra generators are placed at random
/// unused generator spawnpoints in HCZ (like the vanilla ones); surplus generators are removed.
/// </summary>
public static class GeneratorSpawner
{
    public static IEnumerable<Scp079Generator> All => Scp079Recontainer.AllGenerators.Where(g => g != null);

    public static int TargetFor(int players)
    {
        var t = MayhemPlugin.Cfg.Scp079.GeneratorThresholds; // e.g. 8,14,20,26 → 2,3,4,5
        int count = 2 + t.Skip(1).Count(x => players >= x);
        return Mathf.Clamp(count, 2, 5);
    }

    public static void Adjust(int target)
    {
        try
        {
            int current = All.Count();
            if (target > current) Spawn(target - current);
            else if (target < current)
            {
                foreach (var g in All.Where(g => !g.Engaged).OrderBy(_ => Rng.Value).Take(current - target).ToList())
                    NetworkServer.Destroy(g.gameObject);
            }
            MEC.Timing.CallDelayed(1f, () => Logger.Info($"[Mayhem] generators: {current} -> {All.Count()} (target {target})"));
        }
        catch (System.Exception e)
        {
            Logger.Error($"[Mayhem] generator adjust failed: {e}");
        }
    }

    private static void Spawn(int amount)
    {
        var distributor = Object.FindObjectOfType<StructureDistributor>();
        var prefab = distributor?.Settings?.SpawnableStructures?.FirstOrDefault(s => s != null && s.StructureType == StructureType.Scp079Generator);
        if (prefab == null)
        {
            Logger.Warn("[Mayhem] couldn't find the generator prefab; keeping the vanilla generator count.");
            return;
        }
        var used = All.Select(g => g.transform.position).ToList();
        var spawnpoints = StructureSpawnpoint.AvailableInstances
            .Where(sp => sp != null && sp.CompatibleStructures != null && sp.CompatibleStructures.Contains(StructureType.Scp079Generator))
            .Where(sp => Room.GetRoomAtPosition(sp.transform.position)?.Zone == FacilityZone.HeavyContainment)
            .Where(sp => used.All(u => (u - sp.transform.position).sqrMagnitude > 4f))
            .OrderBy(_ => Rng.Value)
            .Take(amount)
            .ToList();
        foreach (var sp in spawnpoints)
        {
            StructureSpawnpoint.AvailableInstances.Remove(sp);
            var go = Object.Instantiate(prefab, sp.transform.position, sp.transform.rotation);
            go.transform.SetParent(sp.transform);
            NetworkServer.Spawn(go.gameObject);
        }
        if (spawnpoints.Count < amount)
            Logger.Warn($"[Mayhem] only {spawnpoints.Count}/{amount} extra generator spawnpoints were free in HCZ on this seed.");
    }
}
