using System;
using System.Collections.Generic;
using System.Linq;
using KikosMayhem.Core;
using KikosMayhem.Items;
using LabApi.Events.Arguments.Scp079Events;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;
using UnityEngine;

using PrimitiveFlags = AdminToys.PrimitiveFlags;
using InvisibleInteractableToy = AdminToys.InvisibleInteractableToy;

namespace KikosMayhem.Ascension;

/// <summary>
/// The new Puppet ascension:
///  1. SCP-079 reaches tier 5 → a server rack in the bottom corner of the Server Room starts humming. Puppet interacts.
///  2. Three PCs appear: one in Light Containment, one in Heavy or Entrance, one in Entrance. Puppet uses all three.
///  3. A phone rings in Entrance. Answering gives a code read out in NATO words (0=ALPHA … 8=IMMINENT, 9=JUILLIET).
///  4. Puppet decodes it and types it into the keypad in the Nuke Room → Recontainment Protocol.
///  5. Puppet runs to SCP-079's room (can't be teleported) and recontains itself.
///  6. SCP-079 becomes a second Puppet (can't heal, regenerates fast), and an Alpha-1 "Red Right Hand" wave respawns
///     the whole server on surface. SCPs win by killing everyone or reaching the Gate B exit.
/// </summary>
public sealed class AscensionQuest : MayhemModule
{
    public static AscensionQuest Instance { get; private set; }

    public enum Stage { Inactive, ServerRack, Pcs, Phone, Keypad, Recontainment, Ascended, Done }

    public Stage Current { get; private set; } = Stage.Inactive;
    public bool RecontainmentRun => Current == Stage.Recontainment;

    public static readonly string[] Nato = { "ALPHA", "BRAVO", "CHARLIE", "DELTA", "ECHO", "FOXTROT", "GOLF", "HOTEL", "IMMINENT", "JUILLIET" };

    private string _code = "";
    private string _typed = "";
    private readonly List<AdminToy> _props = new();
    private readonly List<SpeakerToy> _sounds = new();
    private readonly List<(Room room, bool done, List<AdminToy> parts)> _pcs = new();
    private TextToy _keypadDisplay;
    private Vector3 _keypadPos;
    private float _runStarted;

    public override void Register()
    {
        Instance = this;
        Scp079Events.LeveledUp += OnLeveledUp;
    }

    public override void Unregister()
    {
        Scp079Events.LeveledUp -= OnLeveledUp;
        Instance = null;
    }

    public override void Reset()
    {
        Current = Stage.Inactive;
        _code = _typed = "";
        _props.Clear();
        _sounds.Clear();
        _pcs.Clear();
        _keypadDisplay = null;
    }

    private static KikosMayhem.Scp079.PuppetManager Pm => KikosMayhem.Scp079.PuppetManager.Instance;

    private void OnLeveledUp(Scp079LeveledUpEventArgs ev)
    {
        if (ev.Tier >= 5) Loops.After(3f, TryStart);
    }

    public override void SecondTick()
    {
        if (Current == Stage.Inactive && KikosMayhem.Scp079.PuppetManager.Tier >= 5 && Pm?.Active == true) TryStart();
        if (Current == Stage.Recontainment) CheckRun();
        if (Current == Stage.Ascended) CheckGateB();
        UpdateObjectiveHud();
    }

    public void TryStart()
    {
        if (!MayhemPlugin.Cfg.Ascension.Enabled || Current != Stage.Inactive) return;
        if (Pm == null || !Pm.Picked || !KikosMayhem.Scp079.PuppetManager.AllPuppets.Any() || KikosMayhem.Scp079.PuppetManager.Computer == null) return;
        Current = Stage.ServerRack;
        SpawnServerRack();
        Tell("<color=#00FFFF>A server rack in the Server Room is humming. Find it.</color>");
    }

    // ───────── helpers ─────────

    private static void Tell(string text, float t = 8f)
    {
        foreach (var p in KikosMayhem.Scp079.PuppetManager.AllPuppets) Hud.Message(p, text, t);
        var pc = KikosMayhem.Scp079.PuppetManager.Computer;
        if (pc != null) Hud.Message(pc, text, t);
    }

    private PrimitiveObjectToy Prim(PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale, Color color, bool collide = true)
    {
        var t = PrimitiveObjectToy.Create(pos, rot, scale, null, false);
        t.Type = type;
        t.Color = color;
        t.Flags = collide ? PrimitiveFlags.Visible | PrimitiveFlags.Collidable : PrimitiveFlags.Visible;
        t.IsStatic = true;
        t.Spawn();
        _props.Add(t);
        return t;
    }

    private TextToy Text(Vector3 pos, Quaternion rot, string text, float scale = 0.1f)
    {
        var t = TextToy.Create(pos, rot, Vector3.one * scale, null, false);
        t.TextFormat = text;
        t.DisplaySize = new Vector2(400, 120);
        t.Spawn();
        _props.Add(t);
        return t;
    }

    private InteractableToy Button(Vector3 pos, Quaternion rot, Vector3 size, float holdSeconds, Action<Player> onUse)
    {
        var t = InteractableToy.Create(pos, rot, size, null, false);
        t.Shape = InvisibleInteractableToy.ColliderShape.Box;
        t.InteractionDuration = holdSeconds;
        t.OnInteracted += p =>
        {
            try { onUse(p); }
            catch (Exception e) { LabApi.Features.Console.Logger.Error($"[Mayhem] ascension interact: {e}"); }
        };
        t.Spawn();
        _props.Add(t);
        return t;
    }

    private void Light(Vector3 pos, Color c, float range = 2.5f, float intensity = 1.5f)
    {
        var l = LightSourceToy.Create(pos, null, false);
        l.Color = c;
        l.Range = range;
        l.Intensity = intensity;
        l.Spawn();
        _props.Add(l);
    }

    private void Sound(string slot, Vector3 pos, float range = 18f)
    {
        var sp = Audio.Sfx.Play(slot, pos, range, loop: true);
        if (sp != null) _sounds.Add(sp);
    }

    private void StopSounds()
    {
        foreach (var s in _sounds) if (!s.IsDestroyed) { s.Stop(); s.Destroy(); }
        _sounds.Clear();
    }

    private static bool PuppetOnly(Player p)
    {
        if (KikosMayhem.Scp079.PuppetManager.IsPuppet(p)) return true;
        Hud.Message(p, "Nothing happens.", 1.5f);
        return false;
    }

    private static Quaternion FaceRoomCenter(Room room, Vector3 from)
    {
        var dir = room.Position - from;
        dir.y = 0;
        return dir.sqrMagnitude < 0.01f ? Quaternion.identity : Quaternion.LookRotation(dir.normalized);
    }

    // ───────── 1. server rack ─────────

    private void SpawnServerRack()
    {
        var room = Room.Get(RoomName.HczServers).FirstOrDefault();
        if (room == null)
        {
            // No server room on this map seed: skip straight to the PCs.
            StartPcs();
            return;
        }
        var pos = FindRackPosition(room);
        var rot = FaceRoomCenter(room, pos);
        Light(pos + Vector3.up * 1.2f, new Color(0f, 1f, 1f), 3f, 2f);
        Prim(PrimitiveType.Cube, pos + Vector3.up * 1.1f + rot * Vector3.forward * 0.35f, rot, new Vector3(0.6f, 0.08f, 0.02f), new Color(0f, 1f, 1f), false);
        Sound("server_rack_hum", pos);
        Button(pos + Vector3.up * 1f, rot, new Vector3(1.2f, 2f, 1.2f), 2f, p =>
        {
            if (Current != Stage.ServerRack || !PuppetOnly(p)) return;
            Audio.Sfx.Play("pc_beep", pos);
            ClearProps();
            StartPcs();
        });
    }

    /// <summary>The rack closest to the bottom corner of the Server Room (lowest floor, farthest from center).</summary>
    private static Vector3 FindRackPosition(Room room)
    {
        var center = room.Position;
        var racks = room.Transform.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("SR_Computer", StringComparison.OrdinalIgnoreCase) || t.name.StartsWith("SR_Storage", StringComparison.OrdinalIgnoreCase)
                        || t.name.IndexOf("rack", StringComparison.OrdinalIgnoreCase) >= 0)
            .Where(t => t != room.Transform)
            .ToList();
        if (racks.Count > 0)
        {
            float minY = racks.Min(r => r.position.y);
            var bottom = racks.Where(r => r.position.y < minY + 1.5f).ToList();
            var corner = bottom.OrderByDescending(r => (r.position - center).sqrMagnitude).First();
            return corner.position;
        }
        // Fallback: a spot in the room, lowest door level.
        var low = room.Doors.OrderBy(d => d.Position.y).FirstOrDefault();
        return (low?.Position ?? center) + (center - (low?.Position ?? center)).normalized * 2f;
    }

    // ───────── 2. PCs ─────────

    private void StartPcs()
    {
        Current = Stage.Pcs;
        _pcs.Clear();
        var zones = new List<FacilityZone>
        {
            Decontamination.IsDecontaminating ? FacilityZone.HeavyContainment : FacilityZone.LightContainment,
            Rng.Chance(50) ? FacilityZone.HeavyContainment : FacilityZone.Entrance,
            FacilityZone.Entrance,
        };
        var used = new HashSet<Room>();
        foreach (var zone in zones.Take(MayhemPlugin.Cfg.Ascension.PcCount))
        {
            var room = Rng.Pick(Room.List.Where(r => !r.IsDestroyed && r.Zone == zone && !used.Contains(r) && !ItemSpawner.IsBlacklisted(r)
                                                     && r.Name is not (RoomName.Hcz079 or RoomName.Pocket or RoomName.HczWarhead)).ToList());
            if (room == null) continue;
            used.Add(room);
            SpawnPc(room);
        }
        Tell("<color=#00FFFF>Three terminals have come online: one in Light Containment, one in Heavy or Entrance, one in Entrance.</color>");
        var pc = KikosMayhem.Scp079.PuppetManager.Computer;
        if (pc != null) Hud.Message(pc, "Terminals: " + string.Join(", ", _pcs.Select(x => x.room.Name.ToString())), 15f);
    }

    private void SpawnPc(Room room)
    {
        var spot = ItemSpawner.SpotIn(room) ?? room.Position + Vector3.up;
        var floor = spot - Vector3.up * 0.55f;
        var rot = FaceRoomCenter(room, floor);
        var parts = new List<AdminToy>();
        int before = _props.Count;

        var gray = new Color(0.25f, 0.25f, 0.27f);
        Prim(PrimitiveType.Cube, floor + Vector3.up * 0.38f, rot, new Vector3(0.9f, 0.76f, 0.55f), gray);                          // desk
        Prim(PrimitiveType.Cube, floor + Vector3.up * 1.0f + rot * new Vector3(0, 0, 0.1f), rot, new Vector3(0.6f, 0.4f, 0.06f), Color.black); // monitor
        Prim(PrimitiveType.Cube, floor + Vector3.up * 1.0f + rot * new Vector3(0, 0, 0.065f), rot, new Vector3(0.54f, 0.34f, 0.01f), new Color(0f, 0.9f, 0.3f), false); // screen
        Prim(PrimitiveType.Cube, floor + Vector3.up * 0.78f + rot * new Vector3(0, 0, -0.12f), rot, new Vector3(0.45f, 0.03f, 0.16f), new Color(0.1f, 0.1f, 0.1f)); // keyboard
        Light(floor + Vector3.up * 1.1f + rot * new Vector3(0, 0, -0.3f), new Color(0f, 1f, 0.4f), 2f, 1.2f);
        Sound("pc_beep", floor + Vector3.up, 10f);

        int index = _pcs.Count;
        Button(floor + Vector3.up * 0.9f, rot, new Vector3(1f, 1.2f, 0.8f), 3f, p =>
        {
            if (Current != Stage.Pcs || !PuppetOnly(p)) return;
            var entry = _pcs[index];
            if (entry.done) return;
            _pcs[index] = (entry.room, true, entry.parts);
            foreach (var part in entry.parts) if (!part.IsDestroyed) part.Destroy();
            int done = _pcs.Count(x => x.done);
            Tell($"<color=#00FFFF>Terminal accessed ({done}/{_pcs.Count})</color>", 4f);
            if (done >= _pcs.Count) StartPhone();
        });

        for (int i = before; i < _props.Count; i++) parts.Add(_props[i]);
        _pcs.Add((room, false, parts));
    }

    // ───────── 3. phone ─────────

    private void StartPhone()
    {
        StopSounds();
        ClearProps();
        Current = Stage.Phone;
        var room = Rng.Pick(Room.List.Where(r => !r.IsDestroyed && r.Zone == FacilityZone.Entrance && !ItemSpawner.IsBlacklisted(r)).ToList());
        if (room == null) { StartKeypad(); return; }
        var spot = ItemSpawner.SpotIn(room) ?? room.Position + Vector3.up;
        var floor = spot - Vector3.up * 0.55f;
        var rot = FaceRoomCenter(room, floor);
        Prim(PrimitiveType.Cube, floor + Vector3.up * 0.45f, rot, new Vector3(0.5f, 0.9f, 0.5f), new Color(0.3f, 0.2f, 0.12f));   // stand
        Prim(PrimitiveType.Cube, floor + Vector3.up * 0.95f, rot, new Vector3(0.28f, 0.1f, 0.2f), new Color(0.6f, 0.05f, 0.05f)); // phone base
        Prim(PrimitiveType.Capsule, floor + Vector3.up * 1.03f, rot * Quaternion.Euler(0, 0, 90), new Vector3(0.06f, 0.14f, 0.06f), Color.black); // handset
        Light(floor + Vector3.up * 1.2f, Color.red, 2f, 1.5f);
        Sound("phone_ring", floor + Vector3.up, 25f);

        _code = string.Concat(Enumerable.Range(0, MayhemPlugin.Cfg.Ascension.CodeLength).Select(_ => Rng.Range(0, 10).ToString()));
        Tell("<color=#00FFFF>A phone is ringing somewhere in Entrance Zone...</color>");
        var pc = KikosMayhem.Scp079.PuppetManager.Computer;
        if (pc != null) Hud.Message(pc, $"The phone is in {room.Name}", 12f);

        Button(floor + Vector3.up * 1f, rot, new Vector3(0.8f, 0.8f, 0.8f), 1.5f, p =>
        {
            if (Current != Stage.Phone || !PuppetOnly(p)) return;
            string words = string.Join(" ", _code.Select(c => Nato[c - '0']));
            StopSounds();
            ClearProps();
            Hud.Message(p, $"<color=#FFD700>☎ \"{words}\"</color>", 30f);
            p.SendBroadcast($"<color=#FFD700>☎ {words}</color>", 20);
            var comp = KikosMayhem.Scp079.PuppetManager.Computer;
            if (comp != null) comp.SendBroadcast($"<color=#FFD700>☎ {words}</color>", 20);
            StartKeypad();
        });
    }

    // ───────── 4. keypad ─────────

    private void StartKeypad()
    {
        Current = Stage.Keypad;
        _typed = "";
        var panel = Warhead.BaseNukesitePanel;
        var room = Room.Get(RoomName.HczWarhead).FirstOrDefault();
        Vector3 basePos;
        Quaternion rot;
        if (panel != null && room != null)
        {
            // Face the keypad into the room (its buttons are on the -forward side), next to the warhead panel.
            var panelPos = panel.transform.position;
            var intoWall = panelPos - room.Position;
            intoWall.y = 0;
            if (intoWall.sqrMagnitude < 0.01f) intoWall = Vector3.forward;
            rot = Quaternion.LookRotation(intoWall.normalized);
            basePos = panelPos + rot * Vector3.right * MayhemPlugin.Cfg.Ascension.KeypadSideOffset - rot * Vector3.forward * 0.1f
                      + Vector3.up * MayhemPlugin.Cfg.Ascension.KeypadHeightOffset;
        }
        else if (room != null)
        {
            basePos = (ItemSpawner.SpotIn(room) ?? room.Position) + Vector3.up * 0.6f;
            rot = FaceRoomCenter(room, basePos);
        }
        else return;

        _keypadPos = basePos;
        var fwd = rot * Vector3.forward;
        var right = rot * Vector3.right;
        Prim(PrimitiveType.Cube, basePos, rot, new Vector3(0.5f, 0.7f, 0.05f), new Color(0.15f, 0.15f, 0.18f));
        _keypadDisplay = Text(basePos + Vector3.up * 0.27f - fwd * 0.04f, rot * Quaternion.Euler(0, 180, 0), "<color=#00FF66>____</color>", 0.06f);
        Light(basePos - fwd * 0.4f, new Color(0f, 1f, 0.4f), 1.5f, 1f);

        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" };
        for (int i = 0; i < keys.Length; i++)
        {
            int row = i / 3, col = i % 3;
            var pos = basePos + right * ((col - 1) * 0.13f) + Vector3.up * (0.12f - row * 0.11f) - fwd * 0.035f;
            var key = keys[i];
            var color = key == "OK" ? new Color(0.1f, 0.6f, 0.1f) : key == "C" ? new Color(0.6f, 0.1f, 0.1f) : new Color(0.35f, 0.35f, 0.4f);
            Prim(PrimitiveType.Cube, pos, rot, new Vector3(0.1f, 0.08f, 0.02f), color, false);
            Text(pos - fwd * 0.015f, rot * Quaternion.Euler(0, 180, 0), key, 0.035f);
            Button(pos, rot, new Vector3(0.11f, 0.09f, 0.1f), 0f, p => Press(p, key));
        }
        Tell("<color=#00FFFF>Decode the phone's code and enter it on the keypad in the Nuke Room.</color> (or stand next to it and type <b>.code 1234</b> in the console)", 10f);
    }

    private void Press(Player p, string key)
    {
        if (Current != Stage.Keypad || !PuppetOnly(p)) return;
        Audio.Sfx.Play("keypad_beep", _keypadPos, 8f);
        if (key == "C") _typed = "";
        else if (key == "OK") { Submit(p, _typed); return; }
        else if (_typed.Length < 8) _typed += key;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (_keypadDisplay == null || _keypadDisplay.IsDestroyed) return;
        _keypadDisplay.TextFormat = $"<color=#00FF66>{(_typed.Length == 0 ? "____" : _typed)}</color>";
    }

    /// <summary>Console fallback: .code 1234 while standing at the keypad.</summary>
    public string SubmitFromCommand(Player p, string code)
    {
        if (Current != Stage.Keypad) return "There's no keypad waiting for a code.";
        if (!KikosMayhem.Scp079.PuppetManager.IsPuppet(p)) return "Only the Puppet can use the keypad.";
        if ((p.Position - _keypadPos).sqrMagnitude > 16f) return "Stand next to the keypad in the Nuke Room.";
        return Submit(p, code) ? "Code accepted." : "Wrong code.";
    }

    private bool Submit(Player p, string code)
    {
        if (code == _code)
        {
            Audio.Sfx.Play("keypad_accept", _keypadPos, 10f);
            ClearProps();
            StartRecontainment();
            return true;
        }
        Audio.Sfx.Play("keypad_deny", _keypadPos, 10f);
        Hud.Message(p, "<color=#FF4040>ACCESS DENIED</color>", 2f);
        _typed = "";
        RefreshDisplay();
        return false;
    }

    // ───────── 5. recontainment run ─────────

    private void StartRecontainment()
    {
        Current = Stage.Recontainment;
        _runStarted = Time.time;
        Audio.Music.PlayGlobal("recontainment");
        CassieW.Say("pitch_0.85 .g4 attention all personnel . SCP 0 7 9 recontainment protocol initiated", "Attention all personnel. SCP-079 recontainment protocol initiated.");
        Tell("<color=#FF4040>RECONTAINMENT PROTOCOL: run to SCP-079's room! You can't be teleported.</color>", 10f);
    }

    private void CheckRun()
    {
        var room = ItemSpawner.Scp079Room;
        var limit = MayhemPlugin.Cfg.Ascension.RecontainmentTimeLimit;
        foreach (var p in KikosMayhem.Scp079.PuppetManager.AllPuppets)
        {
            if (room != null && p.Room == room)
            {
                Ascend();
                return;
            }
        }
        if (limit > 0 && Time.time - _runStarted > limit)
        {
            Tell("<color=#FF4040>Recontainment failed.</color>");
            Audio.Music.StopGlobal();
            Current = Stage.Done;
        }
        if (!KikosMayhem.Scp079.PuppetManager.AllPuppets.Any() && KikosMayhem.Scp079.PuppetManager.Computer == null) Current = Stage.Done;
    }

    // ───────── 6. ascension ─────────

    private void Ascend()
    {
        Current = Stage.Ascended;
        Audio.Music.PlayGlobal("ascension");
        CassieW.Say("pitch_0.7 .g6 SCP 0 7 9 full containment breach detected . all systems shutting down", "SCP-079 full containment breach detected. All systems shutting down...");
        Map.TurnOffLights(6f);
        KikosMayhem.Scp079.PuppetManager.Instance.Ascend();
        Loops.After(4f, RedRightHand);
    }

    private void RedRightHand()
    {
        var acfg = MayhemPlugin.Cfg.Ascension;
        var loadout = acfg.RedRightHandItems
            .Select(s => Enum.TryParse<ItemType>(s, true, out var t) ? t : ItemType.None)
            .Where(t => t != ItemType.None).ToList();
        var everyone = Player.ReadyList.Where(p => !p.IsHost && p.Role != RoleTypeId.Overwatch && !KikosMayhem.Scp079.PuppetManager.IsPuppet(p) && !p.IsSCP).ToList();
        foreach (var p in everyone)
        {
            p.SetRole(RoleTypeId.NtfCaptain, RoleChangeReason.Respawn, RoleSpawnFlags.UseSpawnpoint);
            Loops.After(0.3f, () =>
            {
                if (!p.Valid() || !p.IsAlive) return;
                p.ClearInventory();
                foreach (var t in loadout)
                {
                    var item = p.AddItem(t);
                    if (item is FirearmItem fa)
                    {
                        fa.StoredAmmo = fa.MaxAmmo;
                        p.AddAmmo(fa.AmmoType, 300);
                    }
                }
                p.CustomInfo = "<color=#B00000>Alpha-1 \"Red Right Hand\"</color>";
            });
        }
        Audio.Music.PlayGlobal("red_right_hand");
        CassieW.Say("pitch_0.9 MTFUnit alpha 1 designated red right hand hasentered . allremaining . neutralize SCP 0 7 9 . noexceptions",
            "Mobile Task Force Unit Alpha-1, designated 'Red Right Hand', has entered the facility. Neutralize SCP-079. No exceptions.");
        Hud.MessageAll("<color=#B00000>ALPHA-1 \"RED RIGHT HAND\" HAS ARRIVED</color>", 8f);
    }

    /// <summary>SCPs reaching the Gate B exit wipes everyone.</summary>
    private void CheckGateB()
    {
        var exit = Map.DefaultEscapeZone;
        var scp = PlayerExt.Alive.FirstOrDefault(p => (p.IsSCP || KikosMayhem.Scp079.PuppetManager.IsPuppet(p)) && exit.Contains(p.Position));
        if (scp == null) return;
        Current = Stage.Done;
        CassieW.Say("pitch_0.6 .g6 containment breach . facility lost", "Containment lost. The facility has fallen.");
        foreach (var p in PlayerExt.Alive.Where(p => !p.IsSCP && !KikosMayhem.Scp079.PuppetManager.IsPuppet(p)).ToList())
            p.Kill("SCP-079 escaped the facility.");
    }

    public void OnAllPuppetsDead()
    {
        if (Current != Stage.Ascended) return;
        Current = Stage.Done;
        Audio.Music.StopGlobal();
        CassieW.Say("SCP 0 7 9 successfully terminated . Alpha 1 mission complete", "SCP-079 successfully terminated. Alpha-1 mission complete.");
    }

    private void UpdateObjectiveHud()
    {
        string text = Current switch
        {
            Stage.ServerRack => "Objective: find the humming server rack (Server Room, bottom corner)",
            Stage.Pcs => $"Objective: access the terminals ({_pcs.Count(x => x.done)}/{_pcs.Count})",
            Stage.Phone => "Objective: answer the ringing phone in Entrance Zone",
            Stage.Keypad => "Objective: enter the decoded code on the Nuke Room keypad",
            Stage.Recontainment => "Objective: <color=#FF4040>RUN to SCP-079's room</color>",
            _ => null,
        };
        if (text == null) return;
        foreach (var p in KikosMayhem.Scp079.PuppetManager.AllPuppets) Hud.SetInfo(p, "ascension", $"<color=#00FFFF>{text}</color>", 1.2f, 2);
        var pc = KikosMayhem.Scp079.PuppetManager.Computer;
        if (pc != null) Hud.SetInfo(pc, "ascension", $"<color=#00FFFF>{text}</color>", 1.2f, 2);
    }

    /// <summary>Test helper: completes the current stage as if the Puppet did it.</summary>
    public void DebugAdvance(Player puppet)
    {
        switch (Current)
        {
            case Stage.ServerRack:
                StopSounds(); ClearProps(); StartPcs();
                break;
            case Stage.Pcs:
                for (int i = 0; i < _pcs.Count; i++) _pcs[i] = (_pcs[i].room, true, _pcs[i].parts);
                StartPhone();
                break;
            case Stage.Phone:
                StopSounds(); ClearProps();
                LabApi.Features.Console.Logger.Info("[SelfTest] phone code: " + string.Join(" ", _code.Select(c => Nato[c - '0'])) + $" = {_code}");
                StartKeypad();
                break;
            case Stage.Keypad:
                puppet.Position = _keypadPos;
                LabApi.Features.Console.Logger.Info("[SelfTest] wrong code -> " + SubmitFromCommand(puppet, "0000000"));
                LabApi.Features.Console.Logger.Info("[SelfTest] right code -> " + SubmitFromCommand(puppet, _code));
                break;
            case Stage.Recontainment:
                Items.Defs.Teleports.ToRoom(puppet, ItemSpawner.Scp079Room);
                if (puppet.Room != ItemSpawner.Scp079Room) puppet.Position = ItemSpawner.Scp079Room.Position + Vector3.up;
                break;
        }
    }

    private void ClearProps()
    {
        foreach (var t in _props) if (t != null && !t.IsDestroyed) t.Destroy();
        _props.Clear();
    }
}
