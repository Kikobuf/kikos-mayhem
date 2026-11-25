using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using KikosMayhem.Core;
using KikosMayhem.Effects;
using KikosMayhem.Gambling;
using KikosMayhem.Items;
using KikosMayhem.Items.Defs;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Arguments.WarheadEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Enums;
using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;
using UnityEngine;
using VoiceChat.Codec;

namespace KikosMayhem.Events;

public enum GlobalEvent { None, RareItemsCommon, FiveXLoot, ItemChancesInversed, CustomItemsOnDeath, CausalityCorruption, AllItems1853, FiveXCustomLoot }
public enum SubEvent { None, BeQuiet, FinallyAwake, CandyOnDeath }

/// <summary>
/// Global events (one per round), sub events, the Ultimate Loot Pile special event, Mayhem Protocol /
/// Mayhem Insurgency (auto-start ~25 min in), the [REDACTED] Protocol, and "last team alive wins".
/// </summary>
public sealed class EventsModule : MayhemModule
{
    public static EventsModule Instance { get; private set; }

    public GlobalEvent Global { get; private set; }
    public SubEvent Sub { get; private set; }

    private float _roundStart;
    private float _mayhemAt;
    private bool _mayhemStarted;
    private float _lootPileAt;
    private bool _redactedDone;
    private readonly Dictionary<ReferenceHub, OpusDecoder> _decoders = new();
    private readonly float[] _voiceBuffer = new float[480 * 4];
    private readonly Dictionary<ReferenceHub, float> _quietCooldown = new();

    public static readonly Dictionary<GlobalEvent, string> GlobalNames = new()
    {
        [GlobalEvent.RareItemsCommon] = "Rare Items Spawn Like Common Items",
        [GlobalEvent.FiveXLoot] = "5x Loot Spawn",
        [GlobalEvent.ItemChancesInversed] = "Item Chances Inversed",
        [GlobalEvent.CustomItemsOnDeath] = "Custom Items Spawn On Death",
        [GlobalEvent.CausalityCorruption] = "Causality Corruption",
        [GlobalEvent.AllItems1853] = "All Items Replaced By SCP-1853",
        [GlobalEvent.FiveXCustomLoot] = "5x Custom Item Loot Spawn",
    };

    public static readonly Dictionary<SubEvent, string> SubNames = new()
    {
        [SubEvent.BeQuiet] = "You May Want To Be Quiet",
        [SubEvent.FinallyAwake] = "Oh, You're Finally Awake!",
        [SubEvent.CandyOnDeath] = "People Drop Candy On Death",
    };

    public override void Register()
    {
        Instance = this;
        PlayerEvents.Death += OnDeath;
        PlayerEvents.Joined += OnJoined;
        PlayerEvents.SendingVoiceMessage += OnVoice;
        PlayerEvents.Left += OnLeft;
        ServerEvents.WaveRespawned += OnWaveRespawned;
        ServerEvents.RoundEndingConditionsCheck += OnRoundEndCheck;
        WarheadEvents.Started += OnWarheadStarted;
    }

    public override void Unregister()
    {
        PlayerEvents.Death -= OnDeath;
        PlayerEvents.Joined -= OnJoined;
        PlayerEvents.SendingVoiceMessage -= OnVoice;
        PlayerEvents.Left -= OnLeft;
        ServerEvents.WaveRespawned -= OnWaveRespawned;
        ServerEvents.RoundEndingConditionsCheck -= OnRoundEndCheck;
        WarheadEvents.Started -= OnWarheadStarted;
        foreach (var d in _decoders.Values) d.Dispose();
        _decoders.Clear();
        Instance = null;
    }

    public override void Reset()
    {
        Global = GlobalEvent.None;
        Sub = SubEvent.None;
        _mayhemStarted = false;
        _redactedDone = false;
        _lootPileAt = 0;
        _quietCooldown.Clear();
        MyhmInsurgency.Reset();
        MayhemProtocol.Reset();
    }

    // ───────── round start ─────────

    public override void RoundStarted()
    {
        var cfg = MayhemPlugin.Cfg.Events;
        _roundStart = Time.time;
        _mayhemAt = _roundStart + (cfg.MayhemAutoStartMinutes + Rng.Range(-1f, 1f)) * 60f;
        _lootPileAt = Rng.Chance(cfg.UltimateLootPileChance) ? _roundStart + Rng.Range(cfg.UltimateLootPileMinTime, cfg.UltimateLootPileMaxTime) : 0;

        var pick = Rng.Weighted(cfg.GlobalEventWeights
            .Select(kv => (Enum.TryParse<GlobalEvent>(kv.Key, out var e) ? e : GlobalEvent.None, kv.Value))
            .Where(x => x.Item1 != GlobalEvent.None));
        StartGlobal(pick);

        if (Rng.Chance(cfg.SubEventChance))
        {
            var sub = Rng.Weighted(cfg.SubEventWeights
                .Select(kv => (Enum.TryParse<SubEvent>(kv.Key, out var e) ? e : SubEvent.None, kv.Value))
                .Where(x => x.Item1 != SubEvent.None));
            StartSub(sub);
        }
    }

    public void StartGlobal(GlobalEvent e)
    {
        Global = e;
        switch (e)
        {
            case GlobalEvent.ItemChancesInversed:
                GamblingModule.Inversed = true;
                break;
            case GlobalEvent.CausalityCorruption:
                ItemSpawner.CausalityItem = Rng.Pick(ItemEngine.Spawnable.Where(d => !d.CausalityBlacklisted).ToList());
                break;
            case GlobalEvent.FiveXCustomLoot:
                ItemSpawner.CustomMultiplier = 5f;
                break;
            case GlobalEvent.FiveXLoot:
                Loops.After(0.5f, () => CloneLoot(4));
                break;
            case GlobalEvent.RareItemsCommon:
                Loops.After(0.5f, ReplaceWithRare);
                break;
            case GlobalEvent.AllItems1853:
                FacilityModule.AllItems1853 = true;
                Loops.After(0.5f, ReplaceAllWith1853);
                break;
        }
        RefreshBanner();
        if (e != GlobalEvent.None)
        {
            string extra = e == GlobalEvent.CausalityCorruption && ItemSpawner.CausalityItem != null ? $" — only <color={ItemSpawner.CausalityItem.Rarity.Hex()}>{ItemSpawner.CausalityItem.Name}</color> spawns" : "";
            foreach (var p in Player.ReadyList) p.SendBroadcast($"<size=26>Global event: <color=#FFD700>{GlobalNames[e]}</color>{extra}</size>", 8);
        }
    }

    public void StartSub(SubEvent e)
    {
        Sub = e;
        if (e == SubEvent.FinallyAwake) Loops.After(1f, ScatterEveryone);
        RefreshBanner();
        if (e != SubEvent.None)
            foreach (var p in Player.ReadyList) Hud.Message(p, $"Sub event: <color=#C77DFF>{SubNames[e]}</color>", 8f);
    }

    private void RefreshBanner()
    {
        var parts = new List<string>();
        if (Global != GlobalEvent.None) parts.Add($"<color=#FFD700>{GlobalNames[Global]}</color>");
        if (Sub != SubEvent.None) parts.Add($"<color=#C77DFF>{SubNames[Sub]}</color>");
        Hud.Banner = string.Join(" · ", parts);
    }

    private void OnJoined(PlayerJoinedEventArgs ev)
    {
        if (!Round.IsRoundStarted || Global == GlobalEvent.None) return;
        Loops.After(3f, () => { if (ev.Player.Valid()) ev.Player.SendBroadcast($"Global event: <color=#FFD700>{GlobalNames[Global]}</color>", 6); });
    }

    // ───────── loot events ─────────

    private static readonly ItemType[] RarePool =
    {
        ItemType.MicroHID, ItemType.ParticleDisruptor, ItemType.Jailbird, ItemType.KeycardO5, ItemType.GunLogicer,
        ItemType.GunFRMG0, ItemType.GunCom45, ItemType.SCP018, ItemType.SCP268, ItemType.SCP1853, ItemType.SCP244a,
        ItemType.KeycardFacilityManager, ItemType.ArmorHeavy, ItemType.GunE11SR, ItemType.SCP500,
    };

    private static IEnumerable<Pickup> MapPickups() =>
        Pickup.List.Where(p => !p.IsDestroyed && p is not Projectile && !ItemEngine.IsCustom(p.Serial) && p.Room != null
                               && p.Room.Zone != FacilityZone.Surface).ToList();

    private static void ReplaceWithRare()
    {
        foreach (var p in MapPickups())
        {
            if (p.Category == ItemCategory.Ammo || !Rng.Chance(40)) continue;
            var pos = p.Position;
            p.Destroy();
            Pickup.Create(Rng.Pick(RarePool), pos);
        }
    }

    private static void ReplaceAllWith1853()
    {
        foreach (var p in MapPickups())
        {
            if (p.Category == ItemCategory.Ammo || p.Type == ItemType.SCP1853) continue;
            var pos = p.Position;
            p.Destroy();
            Pickup.Create(ItemType.SCP1853, pos);
        }
    }

    private static void CloneLoot(int copies)
    {
        int made = 0;
        foreach (var p in MapPickups())
        {
            for (int i = 0; i < copies && made < 900; i++, made++)
                Pickup.Create(p.Type, ItemSpawner.Scatter(p.Position, 0.4f));
        }
    }

    // ───────── deaths ─────────

    private void OnDeath(PlayerDeathEventArgs ev)
    {
        var pos = ev.OldPosition + Vector3.up * 0.4f;
        if (Global == GlobalEvent.CustomItemsOnDeath && ev.OldRole != RoleTypeId.Scp0492 && ev.OldRole != RoleTypeId.Scp079)
        {
            if (ev.OldRole.GetTeam() == Team.SCPs)
            {
                for (int i = 0; i < 3; i++) ItemEngine.Spawn("scrooge_grenade", ItemSpawner.Scatter(pos, 0.6f));
            }
            else
            {
                var def = ItemSpawner.RandomDef();
                if (def != null) ItemEngine.Spawn(def, pos);
            }
        }
        if (Sub == SubEvent.CandyOnDeath && ev.OldRole != RoleTypeId.Scp079)
            for (int i = 0; i < 2; i++) Pickup.Create(ItemType.SCP330, ItemSpawner.Scatter(pos, 0.4f));

        MyhmInsurgency.OnDeath(ev.Player);
    }

    // ───────── sub events ─────────

    private static void ScatterEveryone()
    {
        var scpRooms = PlayerExt.Alive.Where(p => p.IsSCP).Select(p => p.Room).Where(r => r != null).ToHashSet();
        foreach (var p in PlayerExt.AliveHumans.Where(p => !p.IsPuppet()).ToList())
            SendToRandomRoom(p, scpRooms);
    }

    private static void SendToRandomRoom(Player p, HashSet<Room> avoid)
    {
        var zones = new List<FacilityZone> { FacilityZone.HeavyContainment, FacilityZone.Entrance };
        if (!Decontamination.IsDecontaminating) zones.Add(FacilityZone.LightContainment);
        var room = Rng.Pick(Room.List.Where(r => !r.IsDestroyed && zones.Contains(r.Zone) && !avoid.Contains(r)
                                                 && !ItemSpawner.IsBlacklisted(r) && r.Name is not (RoomName.Pocket or RoomName.Hcz079)).ToList());
        Teleports.ToRoom(p, room);
    }

    private void OnWaveRespawned(WaveRespawnedEventArgs ev)
    {
        if (Sub != SubEvent.FinallyAwake) return;
        Loops.After(1f, () =>
        {
            var scpRooms = PlayerExt.Alive.Where(p => p.IsSCP).Select(p => p.Room).Where(r => r != null).ToHashSet();
            foreach (var p in ev.Players.Where(p => p.Valid() && p.IsAlive)) SendToRandomRoom(p, scpRooms);
        });
    }

    /// <summary>You May Want To Be Quiet: peaking your mic makes you explode.</summary>
    private void OnVoice(PlayerSendingVoiceMessageEventArgs ev)
    {
        if (Sub != SubEvent.BeQuiet || !ev.Player.IsAlive || ev.Player.IsSCP) return;
        var hub = ev.Player.ReferenceHub;
        if (_quietCooldown.TryGetValue(hub, out var cd) && cd > Time.time) return;
        try
        {
            if (!_decoders.TryGetValue(hub, out var dec)) _decoders[hub] = dec = new OpusDecoder();
            var msg = ev.Message;
            int n = dec.Decode(msg.Data, msg.DataLength, _voiceBuffer);
            float peak = 0;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(_voiceBuffer[i]));
            if (peak < MayhemPlugin.Cfg.Events.BeQuietThreshold) return;
        }
        catch { return; }
        _quietCooldown[hub] = Time.time + 3f;
        var p = ev.Player;
        var pos = p.Position;
        Loops.After(0f, () =>
        {
            PlayerExt.Explode(pos, p);
            foreach (var door in Map.Doors)
                if (door is BreakableDoor bd && (bd.Position - pos).sqrMagnitude < 9f) bd.TryBreak();
        });
    }

    private void OnLeft(PlayerLeftEventArgs ev)
    {
        if (_decoders.TryGetValue(ev.Player.ReferenceHub, out var d))
        {
            d.Dispose();
            _decoders.Remove(ev.Player.ReferenceHub);
        }
    }

    // ───────── ticking: special events & protocols ─────────

    public override void SecondTick()
    {
        if (_lootPileAt > 0 && Time.time > _lootPileAt)
        {
            _lootPileAt = 0;
            UltimateLootPile();
        }
        if (!_mayhemStarted && Time.time > _mayhemAt && !Warhead.IsDetonated)
        {
            _mayhemStarted = true;
            if (Rng.Chance(MayhemPlugin.Cfg.Events.MayhemProtocolChance)) MayhemProtocol.Start();
            else MyhmInsurgency.Start();
        }
        MayhemProtocol.Tick();
        MyhmInsurgency.Tick();
    }

    public void ForceMayhem(bool protocol)
    {
        _mayhemStarted = true;
        if (protocol) MayhemProtocol.Start();
        else MyhmInsurgency.Start();
    }

    private static readonly ItemType[] LootPileTypes = { ItemType.SCP1853, ItemType.SCP207, ItemType.AntiSCP207, ItemType.Adrenaline, ItemType.SCP500, ItemType.Painkillers };

    public static void UltimateLootPile()
    {
        var spots = new List<(string name, Vector3 pos)>();
        void Add(string n, Door d) { if (d != null) spots.Add((n, d.Position + Vector3.up * 0.6f)); }
        Add("Surface Nuke Room", Door.Get(DoorName.SurfaceNuke));
        Add("Gate B Walkway", Door.Get(DoorName.SurfaceEscapePrimary));
        Add("MTF Spawn", Door.Get(DoorName.SurfaceGate));
        spots.Add(("Gate A Elevator", ItemSpawner.GateASurface()));
        spots.Add(("Gate B Exit", ItemSpawner.Exit()));
        var (name, pos) = Rng.Pick(spots);
        var type = Rng.Pick(LootPileTypes);
        for (int i = 0; i < MayhemPlugin.Cfg.Events.UltimateLootPileItems; i++)
            Pickup.Create(type, ItemSpawner.Scatter(pos, 2f));
        CassieW.Say("pitch_1.1 .g1 attention . supply drop on surface", $"An Ultimate Loot Pile of {type} has spawned on Surface ({name})!");
        Hud.MessageAll($"<color=#FFD700>An Ultimate Loot Pile has spawned on Surface!</color> ({type} · {name})", 8f);
    }

    // ───────── [REDACTED] Protocol ─────────

    private void OnWarheadStarted(WarheadStartedEventArgs ev)
    {
        var cfg = MayhemPlugin.Cfg.Events;
        if (!cfg.RedactedProtocolEnabled || _redactedDone) return;
        if (DateTime.Now.Hour != cfg.RedactedProtocolHour) return;
        _redactedDone = true;
        Warhead.IsLocked = true;
        Audio.Music.PlayGlobal("redacted_protocol");
        CassieW.Say("pitch_0.5 .g7 . . . protocol . . . . . initiated", "[REDACTED] PROTOCOL INITIATED");
        ItemSpawner.SpawnPile(ItemSpawner.GateASurface(), 12, 6, d => d is not MayhemCoin);
        Hud.MessageAll("<color=#FF0000>[REDACTED] PROTOCOL — grab the gear at Gate A. Something is coming.</color>", 10f);
        Loops.After(cfg.RedactedProtocolDelay, () =>
        {
            var scps = new[] { RoleTypeId.Scp173, RoleTypeId.Scp939, RoleTypeId.Scp096, RoleTypeId.Scp049, RoleTypeId.Scp106 };
            var victims = Rng.Shuffle(PlayerExt.AliveHumans.Where(p => !p.IsPuppet())).Take(cfg.RedactedProtocolScpCount).ToList();
            foreach (var v in victims)
            {
                var pos = v.Position;
                v.SetRole(Rng.Pick(scps), RoleChangeReason.RemoteAdmin, RoleSpawnFlags.None);
                Loops.After(0.2f, () => { if (v.Valid()) v.Position = pos; });
                Hud.Message(v, "<color=#FF0000>You have been [REDACTED]. Kill everyone.</color>", 8f);
            }
        });
    }

    // ───────── round end: last team alive wins ─────────

    private static void OnRoundEndCheck(RoundEndingConditionsCheckEventArgs ev)
    {
        var sides = PlayerExt.Alive.Where(p => p.Role != RoleTypeId.Tutorial || p.IsPuppet() || MyhmInsurgency.IsInsurgent(p))
            .Select(PlayerExt.Side).Distinct().Count();
        ev.CanEnd = sides <= 1;
    }
}
