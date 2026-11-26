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

    public static string DumpRoom(string name, string filter = null)
    {
        if (!Enum.TryParse<RoomName>(name, true, out var rn)) return "Unknown room name.";
        var room = Room.Get(rn).FirstOrDefault();
        if (room == null) return "Room not on this seed.";
        var sb = new StringBuilder($"{rn} @ {room.Position} rot {room.Rotation.eulerAngles}\n");
        if (filter != null && filter.StartsWith("mesh:"))
        {
            string mf = filter.Substring(5);
            foreach (var g in room.Transform.GetComponentsInChildren<MeshFilter>(true)
                         .Where(m => m.sharedMesh != null && (mf == "*" || m.sharedMesh.name.IndexOf(mf, StringComparison.OrdinalIgnoreCase) >= 0))
                         .GroupBy(m => m.sharedMesh.name).Take(300))
            {
                var m = g.First();
                sb.AppendLine($"  mesh {g.Key} x{g.Count()} on '{m.name}' (parent '{m.transform.parent?.name}') @ {m.transform.position} size {m.GetComponent<Renderer>()?.bounds.size}");
            }
            return sb.ToString();
        }
        if (filter != null && filter.StartsWith("obj:"))
        {
            // Debug: export the meshes under a transform to an .obj (local to that transform) to inspect map geometry offline.
            string pn = filter.Substring(4).Replace('_', ' ');
            var root = room.Transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == pn);
            if (root == null) return "no such transform";
            var obj = new StringBuilder();
            int vOff = 1;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                if (!mesh.isReadable) { sb.AppendLine($"  {mf.name}: mesh not readable"); continue; }
                obj.AppendLine($"o {mf.name}");
                foreach (var v in mesh.vertices)
                {
                    var w = root.InverseTransformPoint(mf.transform.TransformPoint(v));
                    obj.AppendLine($"v {w.x:F4} {w.y:F4} {w.z:F4}");
                }
                var tris = mesh.triangles;
                for (int i = 0; i < tris.Length; i += 3) obj.AppendLine($"f {tris[i] + vOff} {tris[i + 1] + vOff} {tris[i + 2] + vOff}");
                vOff += mesh.vertexCount;
                sb.AppendLine($"  {mf.name}: {mesh.vertexCount} verts");
            }
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), pn.Replace(' ', '_') + ".obj");
            System.IO.File.WriteAllText(path, obj.ToString());
            sb.AppendLine($"  root @ {root.position} rot {root.eulerAngles} -> {path}");
            return sb.ToString();
        }
        if (filter != null && filter.StartsWith("under:"))
        {
            string pn = filter.Substring(6).Replace('_', ' ');
            foreach (var root in room.Transform.GetComponentsInChildren<Transform>(true).Where(t => t.name == pn).Take(3))
                foreach (var t in root.GetComponentsInChildren<Transform>(true).Take(120))
                {
                    var mf = t.GetComponent<MeshFilter>();
                    string mb = mf != null && mf.sharedMesh != null ? $" meshBounds c{mf.sharedMesh.bounds.center:F3} s{mf.sharedMesh.bounds.size:F3} lossy{t.lossyScale:F3}" : "";
                    var bc = t.GetComponent<BoxCollider>();
                    if (bc != null) mb += $" box c{bc.center:F3} s{bc.size:F3} lossy{t.lossyScale:F3}";
                    var local = root.InverseTransformPoint(t.position);
                    sb.AppendLine($"  {t.name} @ {t.position:F3} local {local:F3} rot {t.eulerAngles:F1} fwd {t.forward:F2} up {t.up:F2} right {t.right:F2}{mb} comps [{string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name))}]");
                }
            return sb.ToString();
        }
        foreach (var g in room.Transform.GetComponentsInChildren<Transform>(true)
                     .Where(t => filter == null || t.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || filter == "*")
                     .GroupBy(t => t.name).OrderBy(g => g.Min(t => t.position.y)).Take(filter == null ? 80 : 400))
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
