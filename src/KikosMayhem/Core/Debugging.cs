using System;
using System.Linq;
using System.Text;
using KikosMayhem.Items;
using LabApi.Features.Enums;
using LabApi.Features.Wrappers;
using MapGeneration;
using UnityEngine;

namespace KikosMayhem.Core;

/// <summary>Prints Mayhem landmark positions so a host can sanity-check a map seed.</summary>
public static class Debugging
{
    public static string Dump()
    {
        var sb = new StringBuilder();
        void Line(string k, object v) => sb.AppendLine($"{k}: {v}");
        Line("Rooms", Room.List.Count);
        Line("Gate A surface pile", ItemSpawner.GateASurface());
        Line("Exit pile", ItemSpawner.Exit());
        Line("079 room", ItemSpawner.Scp079Room?.Position.ToString() ?? "none");
        Line("LCZ armory door", Door.Get(DoorName.LczArmory)?.Position.ToString() ?? "none");
        Line("Nukesite panel", Warhead.BaseNukesitePanel != null ? Warhead.BaseNukesitePanel.transform.position + " fwd " + Warhead.BaseNukesitePanel.transform.forward : "none");
        Line("Outside panel", Warhead.BaseOutsidePanel != null ? Warhead.BaseOutsidePanel.transform.position.ToString() : "none");
        Line("Generators", Map.Generators.Count);
        Line("Custom items defined", ItemEngine.All.Count());
        var servers = Room.Get(RoomName.HczServers).FirstOrDefault();
        if (servers != null)
        {
            Line("Server room", servers.Position);
            var racks = servers.Transform.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.IndexOf("rack", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("server", StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(40).ToList();
            Line("Server room rack-like children", racks.Count);
            foreach (var r in racks) sb.AppendLine($"   {r.name} @ {r.position}");
        }
        else Line("Server room", "not on this seed");
        var nuke = Room.Get(RoomName.HczWarhead).FirstOrDefault();
        Line("Nuke room", nuke?.Position.ToString() ?? "none");
        if (Warhead.BaseNukesitePanel != null)
        {
            var t = Warhead.BaseNukesitePanel.transform;
            Line("Nukesite panel axes", $"right {t.right} up {t.up}");
        }
        return sb.ToString();
    }

    public static string DumpRoom(string name)
    {
        if (!Enum.TryParse<RoomName>(name, true, out var rn)) return "Unknown room name.";
        var room = Room.Get(rn).FirstOrDefault();
        if (room == null) return "Room not on this seed.";
        var sb = new StringBuilder($"{rn} @ {room.Position} rot {room.Rotation.eulerAngles}\n");
        foreach (var g in room.Transform.GetComponentsInChildren<Transform>(true)
                     .GroupBy(t => t.name).OrderBy(g => g.Min(t => t.position.y)).Take(80))
        {
            var t = g.OrderBy(x => x.position.y).First();
            sb.AppendLine($"  {g.Key} x{g.Count()} lowest @ {t.position} local {room.Transform.InverseTransformPoint(t.position)}");
        }
        return sb.ToString();
    }

    public static string DummyActions(Player p)
    {
        var sb = new StringBuilder($"Actions for {p.Nickname}:\n");
        foreach (var a in NetworkManagerUtils.Dummies.DummyActionCollector.ServerGetActions(p.ReferenceHub))
            sb.AppendLine(a.Action == null ? $"[{a.Name}]" : "   " + a.Name);
        return sb.ToString();
    }
}
