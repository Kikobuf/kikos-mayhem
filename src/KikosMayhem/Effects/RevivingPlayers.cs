using System.Linq;
using CustomPlayerEffects;
using KikosMayhem.Core;
using LabApi.Features.Wrappers;
using MapGeneration;
using UnityEngine;

namespace KikosMayhem.Effects;

/// <summary>
/// SCP-500 revives don't happen instantly: you have 30 seconds to be somewhere safe (on the ground, not in an
/// elevator, the Pocket Dimension, decontaminating LCZ or a facility about to be nuked). Then the dead come back to you.
/// </summary>
public sealed class RevivingPlayers : CustomEffect
{
    /// <summary>How many players to revive (int.MaxValue = everyone).</summary>
    public int Count = 1;
    public override string Name => Count == int.MaxValue ? "Reviving Players (all)" : $"Reviving Players ({Count})";
    public override EffectKind Kind => EffectKind.Positive;

    public static void Start(Player p, int count)
    {
        var e = EffectManager.Get<RevivingPlayers>(p) ?? EffectManager.Apply<RevivingPlayers>(p, MayhemPlugin.Cfg.Effects.RevivingPlayersSeconds);
        if (e == null) return;
        e.Count = count == int.MaxValue || e.Count == int.MaxValue ? int.MaxValue : e.Count + (e.AppliedAt == Time.time ? count - 1 : count);
        Hud.Message(p, "<color=#88FFAA>Find a safe spot to bring the dead back...</color>", 3f, "revive");
    }

    public static string UnsafeReason(Player p)
    {
        var room = p.Room;
        if (room == null) return "not in a room";
        if (room.Name == RoomName.Pocket) return "in the Pocket Dimension";
        if (room.Zone == FacilityZone.LightContainment && Decontamination.IsDecontaminating) return "LCZ is decontaminating";
        if (Warhead.IsDetonationInProgress && room.Zone != FacilityZone.Surface && Warhead.DetonationTime < 30f) return "the nuke is about to go off";
        if (Elevator.List.Any(e => e.WorldSpaceBounds.Contains(p.Position))) return "in an elevator";
        if (p.Velocity.y < -1.5f) return "falling";
        if (GasGasGas.IsActive(p)) return "too fast";
        return null;
    }

    public override void OnTick()
    {
        string reason = UnsafeReason(Owner);
        if (reason != null)
        {
            Hud.SetInfo(Owner, "revive", $"<color=#FFAA66>Can't revive here: {reason}</color>", 1.2f, 6);
            return;
        }
        var dead = PlayerExt.Spectators.ToList();
        int n = Count == int.MaxValue ? dead.Count : Mathf.Min(Count, dead.Count);
        foreach (var d in dead.Take(n)) Gambling.GamblingModule.ReviveAs(d, Owner);
        if (n > 0) Hud.MessageAll($"<color=#FFD700>{Owner.Nickname} brought {n} player(s) back!</color>", 4f);
        else Hud.Message(Owner, "Nobody was waiting to come back.", 2f);
        EffectManager.Remove(Owner, this);
    }

    public override void OnRemoved()
    {
        Hud.ClearInfo(Owner, "revive");
        if (Remaining <= 0.01f && Expires > 0 && Owner.IsAlive) Hud.Message(Owner, "<color=#FF7070>Your revive fizzled.</color>", 3f);
    }
}
