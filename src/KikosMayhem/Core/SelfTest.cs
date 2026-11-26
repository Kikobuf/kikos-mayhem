using System;
using System.Collections.Generic;
using System.Linq;
using KikosMayhem.Events;
using KikosMayhem.Items;
using LabApi.Features.Console;
using LabApi.Features.Wrappers;
using MEC;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace KikosMayhem.Core;

/// <summary>
/// "mayhem selftest": drives dummy players through every custom item (equip + real shoot/use/throw input),
/// every event, and the SCP-079 / Puppet / Ascension chain. Errors show up as [ERROR] lines in the console.
/// Only meant for a test server with dummies.
/// </summary>
public static class SelfTest
{
    private static bool _running;

    public static bool OnlyPuppet;

    public static bool WrongCode;

    public static void Run(bool onlyPuppet = false, bool wrongCode = false)
    {
        if (_running) return;
        _running = true;
        OnlyPuppet = onlyPuppet || wrongCode;
        WrongCode = wrongCode;
        Timing.RunCoroutine(Routine());
    }

    private static void Log(string s) => Logger.Info("[SelfTest] " + s);

    /// <summary>Strips rich-text tags and odd glyphs so a HUD string survives the server console.</summary>
    private static string Plain(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]*>", "").Replace("\n", " / ").Replace("■", "#").Replace("⚠", "!");

    private static List<Player> Dummies => Player.ReadyList.Where(p => p.IsDummy && !Items.Defs.Decoys.IsDecoy(p)).ToList();

    private static bool Act(Player p, string categoryContains, params string[] actionNames)
    {
        bool inCat = false;
        foreach (var a in DummyActionCollector.ServerGetActions(p.ReferenceHub))
        {
            if (a.Action == null)
            {
                inCat = a.Name.IndexOf(categoryContains, StringComparison.OrdinalIgnoreCase) >= 0;
                continue;
            }
            if (inCat && actionNames.Any(n => a.Name.Equals(n, StringComparison.OrdinalIgnoreCase)))
            {
                a.Action();
                return true;
            }
        }
        return false;
    }

    private static string ActionsFor(Player p, string categoryContains)
    {
        var names = new List<string>();
        bool inCat = false;
        foreach (var a in DummyActionCollector.ServerGetActions(p.ReferenceHub))
        {
            if (a.Action == null) { inCat = a.Name.IndexOf(categoryContains, StringComparison.OrdinalIgnoreCase) >= 0; continue; }
            if (inCat) names.Add(a.Name);
        }
        return string.Join(", ", names.Distinct());
    }

    private static IEnumerable<float> EnsureHuman(Player p, RoleTypeId role = RoleTypeId.ClassD)
    {
        if (!p.IsAlive || !p.IsHuman || p.IsPuppet())
        {
            p.SetRole(role, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.All);
            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static IEnumerator<float> Routine()
    {
        Log("starting");
        Round.IsLocked = true; // keep the round alive while dummies die
        var ds = Dummies;
        if (ds.Count < 3)
        {
            Log("need at least 3 dummies (/dummy spawn)");
            _running = false;
            yield break;
        }

        // ── Items ──
        int i = 0;
        foreach (var def in ItemEngine.All.Where(d => !d.Hidden && !OnlyPuppet).ToList())
        {
            var user = ds[i % ds.Count];
            var target = ds[(i + 1) % ds.Count];
            i++;
            foreach (var w in EnsureHuman(user)) yield return w;
            foreach (var w in EnsureHuman(target, RoleTypeId.Scientist)) yield return w;

            user.ClearInventory();
            user.AddAmmo(ItemType.Ammo9x19, 200); user.AddAmmo(ItemType.Ammo556x45, 200); user.AddAmmo(ItemType.Ammo762x39, 200);
            user.AddAmmo(ItemType.Ammo44cal, 50); user.AddAmmo(ItemType.Ammo12gauge, 50);
            var item = ItemEngine.Give(user, def);
            if (item == null) { Log($"{def.Id}: GIVE FAILED"); continue; }
            yield return Timing.WaitForSeconds(0.3f);

            string cat = $"{def.BaseType} (#{item.Serial})";
            bool equipped = Act(user, cat, "Equip");
            yield return Timing.WaitForSeconds(0.8f);

            // Stand the target in front of the user.
            var fwd = user.Camera.forward; fwd.y = 0; fwd = fwd.sqrMagnitude < 0.01f ? Vector3.forward : fwd.normalized;
            target.Position = user.Position + fwd * 2.5f;
            yield return Timing.WaitForSeconds(0.2f);

            string acts = ActionsFor(user, cat);
            bool did = false;
            if (item is FirearmItem && def.BaseType == ItemType.GunRevolver)
            {
                // Vanilla can't fire revolvers from dummies (missing client shot data) — test the custom part directly.
                def.OnShot(user, (FirearmItem)item, target);
                did = true;
            }
            else if (item is FirearmItem)
            {
                for (int s = 0; s < 3; s++)
                {
                    did |= Act(user, cat, "Shoot->Click");
                    yield return Timing.WaitForSeconds(0.45f);
                }
            }
            else if (item is ThrowableItem || def.BaseType == ItemType.SCP018)
            {
                // Dummies can't throw: spawn the live projectile as if thrown.
                ushort serial = item.Serial;
                user.RemoveItem(item);
                var proj = TimedGrenadeProjectile.SpawnActive(user.Position + fwd * 1.2f + Vector3.up, def.BaseType, user, 1.5);
                if (proj != null)
                {
                    ItemEngine.Register(proj.Serial, def);
                    def.OnThrown(user, proj);
                    did = true;
                }
                yield return Timing.WaitForSeconds(6f);
            }
            else if (item is CoinItem coin)
            {
                did = def.OnFlipping(user, coin);
                if (did) def.OnFlipped(user, coin, Rng.Chance(50));
                yield return Timing.WaitForSeconds(2f);
            }
            else
            {
                foreach (var name in new[] { "UsableItem->Start", "UsableItem->Use", "Shoot->Click", "ThrowFullForce->Click", "Throw->Click", "Use->Click", "Flip->Click" })
                {
                    if (Act(user, cat, name)) { did = true; break; }
                }
                yield return Timing.WaitForSeconds(item.Category == ItemCategory.Grenade || def.BaseType == ItemType.SCP018 ? 6f : 5f);
            }
            Log($"{def.Id}: equipped={equipped} action={did} [{acts}]");
            yield return Timing.WaitForSeconds(0.5f);
        }

        // ── Events ──
        foreach (GlobalEvent e in Enum.GetValues(typeof(GlobalEvent)))
        {
            if (e == GlobalEvent.None || OnlyPuppet) continue;
            EventsModule.Instance?.StartGlobal(e);
            Log($"global event {e} started");
            yield return Timing.WaitForSeconds(1.5f);
        }
        foreach (SubEvent e in Enum.GetValues(typeof(SubEvent)))
        {
            if (e == SubEvent.None) continue;
            EventsModule.Instance?.StartSub(e);
            Log($"sub event {e} started");
            yield return Timing.WaitForSeconds(1.5f);
        }
        EventsModule.UltimateLootPile();
        Log("ultimate loot pile spawned");
        yield return Timing.WaitForSeconds(1f);

        // ── SCP-079 + Puppet + Ascension ──
        var pcPlayer = ds[0];
        var puppet = ds[1];
        pcPlayer.SetRole(RoleTypeId.Scp079, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.All);
        yield return Timing.WaitForSeconds(0.5f);
        Log("079 powers popup: " + Plain(Hud.PeekPopup(pcPlayer)));
        yield return Timing.WaitForSeconds(1.5f);
        foreach (var w in EnsureHuman(puppet)) yield return w;
        var pm = Scp079.PuppetManager.Instance;
        Log("pick puppet: " + pm?.Pick(puppet));
        yield return Timing.WaitForSeconds(2f);
        Log($"puppet role={puppet.Role} hp={puppet.Health}/{puppet.MaxHealth} hs={puppet.HumeShield} items={string.Join(",", puppet.Items.Select(x => x.Type))} lives={pm?.Lives}/{pm?.MaxLives}");

        if (pcPlayer.RoleBase is Scp079Role r && r.SubroutineModule.TryGetSubroutine<Scp079TierManager>(out var tm))
        {
            tm.ServerGrantExperience(5000, Scp079HudTranslation.ExpGainAdminCommand);
            yield return Timing.WaitForSeconds(1f);
            Log($"079 tier now {tm.AccessTierLevel}");
        }

        // Puppet on 079's map: hidden SCP-049 dummy that follows the Puppet and is only shown to SCP-079.
        yield return Timing.WaitForSeconds(1.5f);
        var marker = Player.ReadyList.FirstOrDefault(p => Scp079.PuppetMapMarker.IsMarker(p.ReferenceHub));
        if (marker == null) Log("map marker: MISSING");
        else
        {
            var forPc = PlayerRoles.FirstPersonControl.NetworkMessages.FpcServerPositionDistributor.InvokeRoleSyncEvent(marker.ReferenceHub, pcPlayer.ReferenceHub, marker.Role, new Mirror.NetworkWriter());
            var forOther = PlayerRoles.FirstPersonControl.NetworkMessages.FpcServerPositionDistributor.InvokeRoleSyncEvent(marker.ReferenceHub, ds[2].ReferenceHub, marker.Role, new Mirror.NetworkWriter());
            Log($"map marker: role={marker.Role} dist={Vector3.Distance(marker.Position, puppet.Position):F1}m hp={marker.Health:0}/{marker.MaxHealth:0} (puppet {puppet.Health:0}/{puppet.MaxHealth:0}) god={marker.IsGodModeEnabled} scale={marker.Scale.x} | 079 is sent {forPc}, others are sent {forOther} | counted alive={PlayerExt.Alive.Contains(marker)}");
        }

        // Voice: 079's SCP chat reaches the Puppet, and the Puppet's voice reaches 079, as RoundSummary (clear, anywhere).
        var heard = new List<string>();
        VoiceChat.Networking.VoiceTransceiver.VoiceMessageReceiving spy = (m, h) => heard.Add($"{Player.Get(h)?.Nickname}:{m.Channel}");
        VoiceChat.Networking.VoiceTransceiver.OnVoiceMessageReceiving += spy;
        try
        {
            var enc = new VoiceChat.Codec.OpusEncoder(VoiceChat.Codec.Enums.OpusApplicationType.Voip);
            var buf = new byte[512];
            int len = enc.Encode(new float[480], buf);
            VoiceChat.Networking.VoiceTransceiver.ServerReceiveMessage(pcPlayer.Connection, new VoiceChat.Networking.VoiceMessage(pcPlayer.ReferenceHub, VoiceChat.VoiceChatChannel.ScpChat, buf, len, false));
            Log($"079 talks (SCP chat) -> heard by [{string.Join(", ", heard)}] (puppet {puppet.Nickname} should get RoundSummary)");
            heard.Clear();
            VoiceChat.Networking.VoiceTransceiver.ServerReceiveMessage(puppet.Connection, new VoiceChat.Networking.VoiceMessage(puppet.ReferenceHub, VoiceChat.VoiceChatChannel.Proximity, buf, len, false));
            Log($"puppet talks (proximity) -> heard by [{string.Join(", ", heard)}] (079 {pcPlayer.Nickname} should get RoundSummary)");
        }
        finally { VoiceChat.Networking.VoiceTransceiver.OnVoiceMessageReceiving -= spy; }

        // Generator panel (top left) while a generator is being turned on.
        var gen = Scp079.GeneratorSpawner.All.FirstOrDefault(g => !g.Engaged);
        if (gen != null) gen.Activating = true;
        yield return Timing.WaitForSeconds(1.2f);
        Log("puppet generator panel: " + Plain(Hud.PeekTopLeft(puppet)));
        if (gen != null) gen.Activating = false;

        // Puppet damage rules: SCP can't hurt puppet, human can.
        var human = ds[2];
        foreach (var w in EnsureHuman(human, RoleTypeId.NtfSergeant)) yield return w;
        human.DisableEffect<CustomPlayerEffects.SpawnProtected>();
        puppet.DisableEffect<CustomPlayerEffects.SpawnProtected>();
        yield return Timing.WaitForSeconds(13f); // let hume shield refill after spawn
        float hpBefore = puppet.Health, hsBefore = puppet.HumeShield;
        puppet.Damage(new PlayerStatsSystem.JailbirdDamageHandler(human.ReferenceHub, 500f, Vector3.forward));
        yield return Timing.WaitForSeconds(0.3f);
        Log($"500 dmg on puppet: HP {hpBefore}->{puppet.Health}, HS {hsBefore}->{puppet.HumeShield} (expect HP unchanged, HS 0)");
        yield return Timing.WaitForSeconds(0.5f);
        hpBefore = puppet.Health;
        puppet.Damage(new PlayerStatsSystem.JailbirdDamageHandler(human.ReferenceHub, 100f, Vector3.forward));
        yield return Timing.WaitForSeconds(0.3f);
        Log($"100 dmg with no HS: HP {hpBefore}->{puppet.Health} (expect lower)");
        var scp = ds[3];
        scp.SetRole(RoleTypeId.Scp939, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.All);
        yield return Timing.WaitForSeconds(1.5f);
        scp.DisableEffect<CustomPlayerEffects.SpawnProtected>();
        hpBefore = puppet.Health;
        puppet.Damage(new PlayerStatsSystem.JailbirdDamageHandler(scp.ReferenceHub, 100f, Vector3.forward));
        yield return Timing.WaitForSeconds(0.3f);
        Log($"SCP hits puppet: HP {hpBefore}->{puppet.Health} (expect unchanged: same side)");

        // M.Y.H.M Insurgents (Tutorial) can hurt the Puppet (Tutorial) despite the game seeing them as teammates.
        var insurgent = ds[4];
        MyhmInsurgency.Make(insurgent);
        yield return Timing.WaitForSeconds(1.5f);
        insurgent.DisableEffect<CustomPlayerEffects.SpawnProtected>();
        float ehpBefore = puppet.Health + puppet.HumeShield;
        puppet.Damage(new PlayerStatsSystem.JailbirdDamageHandler(insurgent.ReferenceHub, 60f, Vector3.forward));
        yield return Timing.WaitForSeconds(0.3f);
        Log($"insurgent hits puppet: HP+HS {ehpBefore} -> {puppet.Health + puppet.HumeShield} (expect lower)");
        Scp079.PuppetManager.Instance.GraceEnds = 0; // skip the post-pick grace window for this check
        float insBefore = insurgent.Health;
        insurgent.Damage(new PlayerStatsSystem.JailbirdDamageHandler(puppet.ReferenceHub, 40f, Vector3.forward));
        yield return Timing.WaitForSeconds(0.3f);
        Log($"puppet hits insurgent: HP {insBefore} -> {insurgent.Health} (expect lower)");
        MyhmInsurgency.Reset();

        // Lives: generators adjusted for player count; dying spends one and respawns the Puppet stunned.
        Log($"generators on map: {Scp079.GeneratorSpawner.All.Count()} lives={pm?.Lives}/{pm?.MaxLives}");
        puppet.Kill("selftest");
        yield return Timing.WaitForSeconds(2f);
        Log($"after puppet death: alive={puppet.IsAlive} role={puppet.Role} hp={puppet.Health}/{puppet.MaxHealth} room={puppet.Room?.Name} lives={pm?.Lives}/{pm?.MaxLives} 079 stunned={(pm != null && Time.time < pm.ComputerStunnedUntil)}");
        yield return Timing.WaitForSeconds(5f);

        var quest = Ascension.AscensionQuest.Instance;
        if (quest != null) quest.ForceAllowed = true;
        quest?.TryStart();
        yield return Timing.WaitForSeconds(1f);
        for (int step = 0; step < 8 && quest != null; step++)
        {
            Log($"ascension stage: {quest.Current}");
            if (quest.Current is Ascension.AscensionQuest.Stage.Ascended or Ascension.AscensionQuest.Stage.Done) break;
            if (WrongCode && quest.Current == Ascension.AscensionQuest.Stage.Keypad)
            {
                var surfaceGuy = ds[2];
                if (!surfaceGuy.IsAlive) surfaceGuy.SetRole(PlayerRoles.RoleTypeId.ClassD);
                var outside = Room.List.First(r => r.Zone == MapGeneration.FacilityZone.Surface);
                surfaceGuy.Position = outside.Position + Vector3.up * 1.5f;
                yield return Timing.WaitForSeconds(0.5f);
                Log($"{surfaceGuy.Nickname} placed on surface: zone={surfaceGuy.Zone}");
            }
            quest.DebugAdvance(puppet, WrongCode);
            if (WrongCode && quest.Current == Ascension.AscensionQuest.Stage.Done)
            {
                yield return Timing.WaitForSeconds(3f);
                var surface = Player.ReadyList.Where(p => p.IsDummy && p.IsAlive).Select(p => $"{p.Nickname}:{p.Role}:{p.Zone}");
                Log($"after wrong code: warhead detonated={Warhead.IsDetonated} 079 alive={Scp079.PuppetManager.Computer != null} alive bots=[{string.Join(", ", surface)}]");
                Log("finished (wrong-code run)");
                Round.IsLocked = false;
                _running = false;
                yield break;
            }
            yield return Timing.WaitForSeconds(3f);
        }
        yield return Timing.WaitForSeconds(6f);
        Log($"after ascension: puppets={Scp079.PuppetManager.AllPuppets.Count()} pc role={pcPlayer.Role} pc hp={pcPlayer.Health}/{pcPlayer.MaxHealth} human role={human.Role} info='{human.CustomInfo}' items={string.Join(",", human.Items.Select(x => x.Type))}");

        // ── Protocols ──
        EventsModule.Instance?.ForceMayhem(false);
        yield return Timing.WaitForSeconds(6f);
        Log($"insurgency active={MyhmInsurgency.Active} insurgents={Player.ReadyList.Count(MyhmInsurgency.IsInsurgent)}");
        MayhemProtocol.Start();
        Log("mayhem protocol started");

        Log("finished");
        Round.IsLocked = false;
        _running = false;
    }
}
