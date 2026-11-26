using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using KikosMayhem.Core;
using KikosMayhem.Effects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using Mirror;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using UnityEngine;

namespace KikosMayhem.Items.Defs;

// ───────────────────────── Mitosis ─────────────────────────

/// <summary>On the copy: tracks the host, scales damage with distance, destabilizes if too far.</summary>
public sealed class MitosisCopy : CustomEffect
{
    public Player Host;
    private float _unstable;
    public override string Name => "Molecular Copy";
    public override EffectKind Kind => EffectKind.Mixed;

    public static float MaxRange => MayhemPlugin.Cfg.Items.MitosisMaxRange;

    public float Distance => Host != null && Host.Valid() && Host.IsAlive ? Vector3.Distance(Host.Position, Owner.Position) : float.MaxValue;

    public float DamageMultiplier => Mathf.Lerp(0.5f, 1.25f, Mathf.Clamp01(Distance / MaxRange));

    public override void OnTick()
    {
        float d = Distance;
        bool hostGone = Host == null || !Host.Valid() || !Host.IsAlive;
        if (hostGone || d > MaxRange)
        {
            _unstable += 1f;
            float dmg = 3f * _unstable;
            Owner.TrueDamage(dmg, "Fell apart (molecular instability).");
            Hud.SetInfo(Owner, "mitosis", hostGone
                ? "<color=#FF4040>Your host is gone. You are falling apart...</color>"
                : $"<color=#FF4040>DESTABILIZING — get back to {Host.Nickname}! ({d:0}m / {MaxRange:0}m)</color>", 1.5f, 3);
            return;
        }
        _unstable = Mathf.Max(0, _unstable - 2f);
        Hud.SetInfo(Owner, "mitosis", $"<color=#C77DFF>Host: {Host.Nickname} · {d:0}m / {MaxRange:0}m · damage x{DamageMultiplier:0.00}</color>", 1.5f, 3);
    }

    public override void OnOwnerAttacking(Player target, PlayerHurtingEventArgs ev) => Combat.MultiplyDamage(ev, DamageMultiplier);

    public override void OnRemoved() => Hud.ClearInfo(Owner, "mitosis");
}

public sealed class Mitosis : CustomItem
{
    public override string Id => "mitosis";
    public override string Name => "Mitosis";
    public override Rarity Rarity => Rarity.Epic;
    public override ItemType BaseType => ItemType.Adrenaline;
    public override string Description => "Spawns an imperfect molecular copy of you (a dead player) with some minor defects. The copy must stay close to you or it destabilizes and falls apart. Its damage scales with distance from you (0.5x close → 1.25x far).";

    private static readonly System.Type[] Defects = { typeof(Stained), typeof(Deafened), typeof(HeavyFooted), typeof(Disabled) };

    public override void OnUsed(Player p, Item item)
    {
        var soul = Rng.Pick(PlayerExt.Spectators.ToList());
        if (soul == null || p.IsPuppet() || p.IsSCP)
        {
            Msg(p, "<color=#C77DFF>The cells split... but nothing took shape.</color>");
            return;
        }
        var pos = p.Position;
        var role = p.Role;
        var items = p.Items.Where(i => !ItemEngine.IsCustom(i.Serial)).Select(i => i.Type).ToList();
        soul.SetRole(role, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.None);
        Loops.After(0.3f, () =>
        {
            if (!soul.Valid() || !soul.IsAlive) return;
            soul.Position = pos + Vector3.up * 0.2f;
            soul.ClearInventory();
            foreach (var t in items) soul.AddItem(t);
            soul.MaxHealth = p.MaxHealth * 0.75f;
            soul.Health = soul.MaxHealth;
            // Minor defect.
            var defect = Rng.Pick(Defects);
            if (soul.TryGetEffect(defect.Name, out var eff)) soul.EnableEffect(eff, 1);
            var copy = EffectManager.Apply<MitosisCopy>(soul);
            if (copy != null) copy.Host = p;
            soul.CustomInfo = $"<color=#C77DFF>Copy of {p.Nickname}</color>";
            Msg(soul, $"<color=#C77DFF>You are a molecular copy of {p.Nickname}. Stay close or fall apart.</color>", 6f);
            Msg(p, $"<color=#C77DFF>Mitosis! {soul.Nickname} is your copy.</color>");
        });
    }
}

// ───────────────────────── Jester's Hat ─────────────────────────

/// <summary>Decoy dummies (Jester's Hat). They're excluded from round-end and targeting logic.</summary>
public static class Decoys
{
    private static readonly HashSet<ReferenceHub> All = new();

    public static bool IsDecoy(Player p) => p != null && All.Contains(p.ReferenceHub);

    /// <summary>Marks another helper dummy (e.g. the Puppet's map marker) so every Mayhem system ignores it.</summary>
    public static void Track(ReferenceHub hub) => All.Add(hub);

    /// <summary>Removes and destroys a tracked helper dummy.</summary>
    public static void Untrack(ReferenceHub hub) => Destroy(hub);

    public static void Reset()
    {
        foreach (var hub in All.ToList()) Destroy(hub);
        All.Clear();
    }

    public static Player Spawn(Player of, ItemType held)
    {
        var hub = DummyUtils.SpawnDummy(of.Nickname);
        if (hub == null) return null;
        All.Add(hub);
        var decoy = Player.Get(hub);
        var pos = of.Position;
        var look = of.LookRotation;
        var role = of.Role;
        MEC.Timing.CallDelayed(0.2f, () =>
        {
            if (decoy == null || !decoy.Valid()) return;
            decoy.SetRole(role, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.None);
            MEC.Timing.CallDelayed(0.2f, () =>
            {
                if (!decoy.Valid()) return;
                decoy.Position = pos;
                decoy.LookRotation = look;
                decoy.MaxHealth = of.MaxHealth;
                decoy.Health = of.Health;
                decoy.ClearInventory();
                if (held != ItemType.None)
                {
                    var item = decoy.AddItem(held);
                    if (item != null) decoy.CurrentItem = item;
                }
            });
        });
        Loops.After(MayhemPlugin.Cfg.Items.JestersHatDecoySeconds, () => Destroy(hub));
        return decoy;
    }

    public static void OnDeath(Player p)
    {
        if (!IsDecoy(p)) return;
        var hub = p.ReferenceHub;
        MEC.Timing.CallDelayed(0.5f, () => Destroy(hub));
    }

    private static void Destroy(ReferenceHub hub)
    {
        All.Remove(hub);
        if (hub != null && hub.gameObject != null) NetworkServer.Destroy(hub.gameObject);
    }
}

public sealed class JestersHat : CustomItem
{
    public override string Id => "jesters_hat";
    public override string Name => "Jester's Hat";
    public override Rarity Rarity => Rarity.Rare;
    public override ItemType BaseType => ItemType.SCP268;
    public override string Description => "Spawns a killable decoy of you, holding the last item you held. You get 10 seconds of invisibility and +10% speed.";

    private static readonly Dictionary<ReferenceHub, ItemType> LastHeld = new();

    /// <summary>Remembers what the player held before switching to the hat.</summary>
    public static void TrackHeld(PlayerChangedItemEventArgs ev)
    {
        if (ev.OldItem != null && ItemEngine.Get(ev.OldItem) is not JestersHat) LastHeld[ev.Player.ReferenceHub] = ev.OldItem.Type;
    }

    public override void OnUsed(Player p, Item item)
    {
        LastHeld.TryGetValue(p.ReferenceHub, out var held);
        Decoys.Spawn(p, held);
        p.EnableEffect<Invisible>(1, 10f);
        Stats.Of(p).SetSpeed("jesters_hat", 10f, 10f);
        Msg(p, "<color=#FFAA00>Now you see me...</color>", 2f);
    }
}
