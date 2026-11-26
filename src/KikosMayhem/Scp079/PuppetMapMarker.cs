using System.Collections.Generic;
using KikosMayhem.Core;
using LabApi.Features.Wrappers;
using MEC;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using UnityEngine;

namespace KikosMayhem.Scp079;

/// <summary>
/// Shows the Puppet on SCP-079's map as an SCP-049 arrow, like the original server.
///
/// SCP-079's map only draws SCPs, so a hidden dummy player with the SCP-049 role follows the Puppet around:
/// tiny, god-moded, and synced as a spectator to everyone except SCP-079 (see <see cref="Disguise"/>), so nobody
/// else can see, hear, spectate or hit it. Its health mirrors the Puppet's, so SCP-079's SCP list shows the
/// Puppet's HP under "SCP-049". It is excluded from every Mayhem system like the Jester's Hat decoys.
/// </summary>
public static class PuppetMapMarker
{
    private static ReferenceHub _marker;
    private static ReferenceHub _ready;
    private static CoroutineHandle _loop;

    /// <summary>True for the marker dummy. Its viewer is SCP-079 only.</summary>
    public static bool IsMarker(ReferenceHub hub) => hub != null && hub == _marker;

    public static void Register() => _loop = Timing.RunCoroutine(Loop());

    public static void Unregister()
    {
        Timing.KillCoroutines(_loop);
        Despawn();
    }

    private static IEnumerator<float> Loop()
    {
        while (true)
        {
            yield return Timing.WaitForSeconds(0.1f);
            try { Tick(); }
            catch (System.Exception e) { LabApi.Features.Console.Logger.Error($"[Mayhem] puppet map marker: {e.Message}"); }
        }
    }

    private static void Tick()
    {
        if (_marker != null && _marker.gameObject == null) _marker = _ready = null; // destroyed by a round restart

        var computer = PuppetManager.Computer;
        Player puppet = null;
        if (MayhemPlugin.Cfg.Scp079.PuppetMapMarker && computer != null && Round.IsRoundStarted)
            foreach (var p in PuppetManager.AllPuppets) { puppet = p; break; }

        if (puppet == null)
        {
            Despawn();
            return;
        }
        if (_marker == null)
        {
            Spawn();
            return;
        }
        if (_ready != _marker) return;

        var m = Player.Get(_marker);
        if (m == null || !m.IsAlive) { Despawn(); return; }
        // Sit at the Puppet's feet so it never blocks shots aimed at the Puppet.
        m.Position = puppet.Position + Vector3.down * 0.8f;
        m.LookRotation = puppet.LookRotation;
        m.MaxHealth = Mathf.Max(1f, puppet.MaxHealth);
        m.Health = Mathf.Max(1f, puppet.Health);
    }

    private static void Spawn()
    {
        var hub = DummyUtils.SpawnDummy("Puppet");
        if (hub == null) return;
        _marker = hub;
        Items.Defs.Decoys.Track(hub);
        Timing.CallDelayed(0.2f, () =>
        {
            var m = Player.Get(hub);
            if (m == null || _marker != hub) return;
            m.SetRole(RoleTypeId.Scp049, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.None);
            Timing.CallDelayed(0.2f, () =>
            {
                if (_marker != hub || !m.IsAlive) return;
                m.IsGodModeEnabled = true;
                m.Scale = Vector3.one * 0.01f;
                m.ClearInventory();
                _ready = hub;
            });
        });
    }

    private static void Despawn()
    {
        if (_marker == null) return;
        var hub = _marker;
        _marker = _ready = null;
        Items.Defs.Decoys.Untrack(hub);
    }
}
