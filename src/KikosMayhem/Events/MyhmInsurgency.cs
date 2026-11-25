using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using KikosMayhem.Core;
using KikosMayhem.Gambling;
using KikosMayhem.Items;
using LabApi.Features.Wrappers;
using PlayerRoles;
using UnityEngine;

namespace KikosMayhem.Events;

/// <summary>
/// M.Y.H.M Insurgency: every spectator and everyone who dies afterwards becomes a super soldier
/// (M.Y.H.M Insurgent) tasked with killing everyone else until one team wins.
/// </summary>
public static class MyhmInsurgency
{
    private static readonly HashSet<ReferenceHub> Insurgents = new();
    public static bool Active { get; private set; }

    public static bool IsInsurgent(Player p) => p != null && Insurgents.Contains(p.ReferenceHub);

    public static void Reset()
    {
        Insurgents.Clear();
        Active = false;
    }

    public static void Start()
    {
        if (Active) return;
        Active = true;
        Audio.Music.PlayGlobal("mayhem_insurgency");
        CassieW.Say("pitch_0.8 .g5 facility scan complete . M Y H M insurgency detected . all remaining personnel are terminated",
            "Facility scan complete. M.Y.H.M Insurgency detected. All remaining personnel are to be terminated.");
        Hud.MessageAll("<color=#FF3030>M.Y.H.M INSURGENCY</color> — the dead rise as super soldiers", 8f);
        foreach (var p in PlayerExt.Spectators.ToList()) Make(p);
    }

    public static void OnDeath(Player p)
    {
        Insurgents.Remove(p.ReferenceHub);
        if (!Active) return;
        Loops.After(4f, () => { if (p.Valid() && !p.IsAlive && Active) Make(p); });
    }

    public static void Make(Player p)
    {
        if (!p.Valid()) return;
        Insurgents.Add(p.ReferenceHub);
        // Tutorial is an enemy of every vanilla team, which is what Insurgents need.
        p.SetRole(RoleTypeId.Tutorial, RoleChangeReason.Respawn, RoleSpawnFlags.None);
        Loops.After(0.3f, () =>
        {
            if (!p.Valid() || !IsInsurgent(p)) return;
            Vector3 spot = ItemSpawner.RandomSpot(MapGeneration.FacilityZone.HeavyContainment, MapGeneration.FacilityZone.Entrance)
                           ?? ItemSpawner.GateASurface();
            if (Warhead.IsDetonated) spot = ItemSpawner.GateASurface();
            p.Position = spot + Vector3.up * 0.3f;
            p.ClearInventory();
            p.MaxHealth = 150;
            p.Health = 150;

            var s = Stats.Of(p);
            s.SetSpeed("myhm", 150f);
            s.SetCrit("myhm", 50f, 1f);
            s.SetDamageReduction("myhm", 75f);
            p.EnableEffect<Invigorated>(1);
            p.EnableEffect<Scp1853>(1);

            Gun(p, ItemType.GunFRMG0);
            Gun(p, ItemType.GunLogicer);
            ItemEngine.Give(p, "rolling_thunder");
            ItemEngine.Give(p, "key_to_the_city");
            p.AddItem(ItemType.Medkit);
            p.AddItem(ItemType.Medkit);
            if (Rng.Chance(5)) ItemEngine.Give(p, "pocket_nuke");
            p.CustomInfo = "<color=#FF3030>M.Y.H.M Insurgent</color>";
            Hud.Message(p, "<color=#FF3030>You are an M.Y.H.M Insurgent. Everyone else is your enemy.</color>", 6f);
        });
    }

    private static void Gun(Player p, ItemType type)
    {
        var item = p.AddItem(type);
        if (item is FirearmItem fa)
        {
            fa.StoredAmmo = fa.MaxAmmo;
            p.AddAmmo(fa.AmmoType, 400);
        }
    }

    /// <summary>Tracker to the nearest enemy in the same zone.</summary>
    public static void Tick()
    {
        if (!Active) return;
        foreach (var p in Insurgents.Select(Player.Get).Where(p => p != null && p.IsAlive).ToList())
        {
            var target = PlayerExt.EnemiesOf(p).Where(e => e.Zone == p.Zone && e.Role != RoleTypeId.Scp079)
                .OrderBy(e => (e.Position - p.Position).sqrMagnitude).FirstOrDefault();
            Hud.SetInfo(p, "myhm", target == null
                ? "<color=#FF3030>No targets in this zone</color>"
                : $"<color=#FF3030>Nearest target: {target.Role} · {Vector3.Distance(target.Position, p.Position):0}m</color>", 1.2f, 3);
        }
    }
}

/// <summary>
/// Mayhem Protocol: two minutes until a blast. If more than one team is still alive when it hits,
/// it's a stalemate and everyone dies (SCPs included).
/// </summary>
public static class MayhemProtocol
{
    public static bool Active { get; private set; }
    private static float _blastAt;

    public static void Reset()
    {
        Active = false;
        _blastAt = 0;
    }

    public static void Start()
    {
        if (Active) return;
        Active = true;
        _blastAt = Time.time + MayhemPlugin.Cfg.Events.MayhemProtocolSeconds;
        Audio.Music.PlayGlobal("mayhem_protocol");
        CassieW.Say("pitch_0.8 .g4 danger . M Y H M protocol activated . detonation in 2 minutes",
            "DANGER. M.Y.H.M Protocol activated. Detonation in T-minus 2 minutes.");
        Map.TurnOffLights(3f);
    }

    public static void Tick()
    {
        if (!Active) return;
        float left = _blastAt - Time.time;
        Hud.Banner = $"<color=#FF2020>M.Y.H.M PROTOCOL — {Mathf.Max(0, left):0}s</color>";
        if (left > 0) return;
        Active = false;
        Hud.Banner = "";
        int sides = PlayerExt.Alive.Where(p => p.Role != RoleTypeId.Tutorial || p.IsPuppet() || MyhmInsurgency.IsInsurgent(p))
            .Select(PlayerExt.Side).Distinct().Count();
        if (sides <= 1) return;
        CassieW.Say("pitch_0.6 .g6 stalemate", "Stalemate.");
        foreach (var p in PlayerExt.Alive.ToList())
        {
            if (p.Role == RoleTypeId.Scp079) { p.Kill("M.Y.H.M Protocol stalemate."); continue; }
            TimedGrenadeProjectile.PlayEffect(p.Position, ItemType.GrenadeHE);
            p.Kill("M.Y.H.M Protocol stalemate.");
        }
    }
}
