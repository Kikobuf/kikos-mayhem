using System;
using System.Collections.Generic;
using LabApi.Features.Wrappers;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl.NetworkMessages;

namespace KikosMayhem.Core;

/// <summary>
/// Per-viewer role spoofing using the game's own FpcServerPositionDistributor.RoleSyncEvent.
/// Used by Mirage (look like your last victim) and Skinwalker (look like each viewer's own role).
/// Only human -> human disguises are allowed, because the role packet layout differs for SCPs.
/// </summary>
public static class Disguise
{
    /// <summary>A function returning the role the target should appear as for a given viewer (or null for no disguise).</summary>
    public delegate RoleTypeId? Resolver(Player target, Player viewer);

    private static readonly Dictionary<ReferenceHub, Resolver> Active = new();

    public static void Register() => FpcServerPositionDistributor.RoleSyncEvent += OnRoleSync;

    public static void Unregister() => FpcServerPositionDistributor.RoleSyncEvent -= OnRoleSync;

    public static void Set(Player target, Resolver resolver) => Active[target.ReferenceHub] = resolver;

    public static void SetFixed(Player target, RoleTypeId role) => Set(target, (_, _) => role);

    public static void Clear(Player target) => Active.Remove(target.ReferenceHub);

    public static bool IsDisguised(Player target) => Active.ContainsKey(target.ReferenceHub);

    public static void ClearAll() => Active.Clear();

    private static bool UsesUnits(RoleTypeId role) => role.GetTeam() == Team.FoundationForces;

    private static bool IsHumanRole(RoleTypeId role)
    {
        var team = role.GetTeam();
        return team is Team.ClassD or Team.Scientists or Team.FoundationForces or Team.ChaosInsurgency || role == RoleTypeId.Tutorial;
    }

    private static RoleTypeId OnRoleSync(ReferenceHub hub, ReferenceHub receiver, RoleTypeId supposed, NetworkWriter writer)
    {
        try
        {
            if (hub == receiver) return supposed;
            // The Puppet's map marker exists only for SCP-079; everyone else sees nothing there.
            if (KikosMayhem.Scp079.PuppetMapMarker.IsMarker(hub))
                return receiver.roleManager.CurrentRole.RoleTypeId == RoleTypeId.Scp079 ? supposed : RoleTypeId.Spectator;
            if (!Active.TryGetValue(hub, out var resolver)) return supposed;
            var target = Player.Get(hub);
            var viewer = Player.Get(receiver);
            if (target == null || viewer == null || !target.IsAlive) return supposed;

            // SCP viewers always see the truth (doc: "Doesn't work on non-human SCPs").
            if (viewer.IsSCP) return supposed;

            var fake = resolver(target, viewer);
            if (fake == null) return supposed;
            var real = hub.roleManager.CurrentRole.RoleTypeId;
            var role = fake.Value;
            if (role == supposed || !IsHumanRole(role) || !IsHumanRole(real)) return supposed;

            bool realUnits = UsesUnits(real);
            bool fakeUnits = UsesUnits(role);
            if (realUnits && !fakeUnits) return supposed; // can't strip the real unit byte safely
            if (fakeUnits && !realUnits) writer.WriteByte(0); // fake unit id
            return role;
        }
        catch (Exception e)
        {
            LabApi.Features.Console.Logger.Error($"[Mayhem] disguise sync failed: {e}");
            return supposed;
        }
    }
}
