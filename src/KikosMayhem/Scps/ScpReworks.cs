using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using KikosMayhem.Core;
using KikosMayhem.Effects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp049Events;
using LabApi.Events.Arguments.Scp096Events;
using LabApi.Events.Arguments.Scp106Events;
using LabApi.Events.Arguments.Scp173Events;
using LabApi.Events.Arguments.Scp939Events;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp096;
using PlayerRoles.PlayableScps.Scp173;
using PlayerRoles.PlayableScps.Scp939;
using PlayerStatsSystem;
using UnityEngine;

namespace KikosMayhem.Scps;

// ═════════════════════════ SCP-173 ═════════════════════════
public sealed class Scp173Module : MayhemModule
{
    private readonly Dictionary<TantrumHazard, float> _tantrumHealth = new();
    private readonly HashSet<ReferenceHub> _breakneck = new();

    public override void Register()
    {
        Scp173Events.AddedObserver += OnObserver;
        Scp173Events.CreatingTantrum += OnCreatingTantrum;
        Scp173Events.CreatedTantrum += OnCreatedTantrum;
        Scp173Events.BreakneckSpeedChanged += OnBreakneck;
        PlayerEvents.EnteringHazard += OnEnteringHazard;
        PlayerEvents.InteractingDoor += OnDoor;
        PlayerEvents.ShotWeapon += OnShot;
    }

    public override void Unregister()
    {
        Scp173Events.AddedObserver -= OnObserver;
        Scp173Events.CreatingTantrum -= OnCreatingTantrum;
        Scp173Events.CreatedTantrum -= OnCreatedTantrum;
        Scp173Events.BreakneckSpeedChanged -= OnBreakneck;
        PlayerEvents.EnteringHazard -= OnEnteringHazard;
        PlayerEvents.InteractingDoor -= OnDoor;
        PlayerEvents.ShotWeapon -= OnShot;
    }

    public override void Reset()
    {
        _tantrumHealth.Clear();
        _breakneck.Clear();
    }

    /// <summary>Blink cooldown: 50% faster, plus 5% faster per dead player.</summary>
    private static void OnObserver(Scp173AddedObserverEventArgs ev) => MEC.Timing.CallDelayed(0f, () => ApplyBlink(ev.Player));

    private static void ApplyBlink(Player p)
    {
        if (!p.Valid() || p.RoleBase is not Scp173Role role) return;
        if (!role.SubroutineModule.TryGetSubroutine<Scp173BlinkTimer>(out var timer)) return;
        var cfg = MayhemPlugin.Cfg.Scps;
        int dead = PlayerExt.Spectators.Count();
        float faster = Mathf.Clamp01((cfg.Scp173BlinkFaster + cfg.Scp173BlinkPerDead * dead) / 100f);
        float target = 3f * Mathf.Max(0.15f, 1f - faster);
        if (Mathf.Abs(timer._totalCooldown - target) < 0.01f) return;
        timer._totalCooldown = target;
        timer.ServerSendRpc(true);
    }

    private void OnCreatingTantrum(Scp173CreatingTantrumEventArgs ev)
    {
        var pos = ev.Player.Position;
        if (TantrumHazard.List.Any(t => !t.IsDestroyed && (t.SourcePosition - pos).sqrMagnitude < 2.25f))
        {
            ev.IsAllowed = false;
            Hud.Message(ev.Player, "Can't stack tantrums", 1.5f);
        }
    }

    private void OnCreatedTantrum(Scp173CreatedTantrumEventArgs ev)
    {
        if (ev.Tantrum != null) _tantrumHealth[ev.Tantrum] = MayhemPlugin.Cfg.Scps.Scp173TantrumHealth;
    }

    /// <summary>Tantrums are explosive traps.</summary>
    private void OnEnteringHazard(PlayerEnteringHazardEventArgs ev)
    {
        if (ev.Hazard is not TantrumHazard tantrum || ev.Player.IsSCP) return;
        var pos = tantrum.SourcePosition;
        _tantrumHealth.Remove(tantrum);
        tantrum.Destroy();
        var scp173 = PlayerExt.Alive.FirstOrDefault(p => p.Role == RoleTypeId.Scp173);
        PlayerExt.Explode(pos, scp173);
        foreach (var p in PlayerExt.Near(pos, MayhemPlugin.Cfg.Scps.Scp173TantrumExplosionRadius * 2f).Where(p => !p.IsSCP))
        {
            p.EnableEffect<Concussed>(1, 6f);
            p.EnableEffect<Stained>(1, 6f);
            p.EnableEffect<Deafened>(1, 6f);
        }
        ev.Player.Kill("Stepped on a tantrum.");
    }

    /// <summary>Tantrums can be shot away.</summary>
    private void OnShot(PlayerShotWeaponEventArgs ev)
    {
        if (_tantrumHealth.Count == 0) return;
        var aim = ev.Player.AimPoint(60f);
        foreach (var t in _tantrumHealth.Keys.ToList())
        {
            if (t.IsDestroyed) { _tantrumHealth.Remove(t); continue; }
            if ((t.SourcePosition - aim).sqrMagnitude > 1.6f) continue;
            _tantrumHealth[t] -= 25f;
            if (_tantrumHealth[t] <= 0)
            {
                _tantrumHealth.Remove(t);
                t.Destroy();
                Hud.Message(ev.Player, "Tantrum destroyed", 1.2f);
            }
            break;
        }
    }

    private void OnBreakneck(Scp173BreakneckSpeedChangedEventArgs ev)
    {
        if (ev.Active) _breakneck.Add(ev.Player.ReferenceHub);
        else _breakneck.Remove(ev.Player.ReferenceHub);
    }

    /// <summary>During Breakneck Speed, 173 breaks doors it interacts with.</summary>
    private void OnDoor(PlayerInteractingDoorEventArgs ev)
    {
        if (ev.Player.Role != RoleTypeId.Scp173 || !_breakneck.Contains(ev.Player.ReferenceHub)) return;
        if (ev.Door is BreakableDoor bd && !bd.IsBroken)
        {
            ev.IsAllowed = false;
            bd.TryBreak();
        }
    }

    public override void SecondTick()
    {
        foreach (var p in PlayerExt.Alive.Where(p => p.Role == RoleTypeId.Scp173).ToList()) ApplyBlink(p);
    }
}

// ═════════════════════════ SCP-096 ═════════════════════════
public sealed class Scp096Module : MayhemModule
{
    private readonly Dictionary<ReferenceHub, Scp096RageState> _state = new();
    private readonly Dictionary<ReferenceHub, float> _nukeRamp = new();

    public override void Register()
    {
        Scp096Events.ChangedState += OnState;
        Scp096Events.Charging += OnCharging;
        Scp096Events.TriedNotToCry += OnCry;
        Combat.Pre.Add(EnragingResistance);
    }

    public override void Unregister()
    {
        Scp096Events.ChangedState -= OnState;
        Scp096Events.Charging -= OnCharging;
        Scp096Events.TriedNotToCry -= OnCry;
    }

    public override void Reset()
    {
        _state.Clear();
        _nukeRamp.Clear();
    }

    private Scp096RageState StateOf(Player p) => _state.TryGetValue(p.ReferenceHub, out var s) ? s : Scp096RageState.Docile;

    private void OnState(Scp096ChangedStateEventArgs ev)
    {
        var p = ev.Player;
        var state = ev.State;
        _state[p.ReferenceHub] = state;
        // Tiny while docile: you see his face even looking at the floor. Normal size when enraged.
        p.Scale = state == Scp096RageState.Enraged ? Vector3.one : new Vector3(1f, 0.55f, 1f);
        if (state != Scp096RageState.Docile) p.DisableEffect<Invisible>();
    }

    private static void OnCharging(Scp096ChargingEventArgs ev)
    {
        var p = ev.Player;
        int n = MayhemPlugin.Cfg.Scps.Scp096ChargeExplosions;
        for (int i = 0; i < n; i++)
        {
            float delay = 0.15f + i * (1f / n);
            Loops.After(delay, () =>
            {
                if (!p.Valid() || p.Role != RoleTypeId.Scp096) return;
                EffectManager.Apply<Invincible>(p, 0.2f);
                PlayerExt.Explode(p.Position + p.Camera.forward * 1.2f, p);
            });
        }
    }

    private static void OnCry(Scp096TriedNotToCryEventArgs ev) => ev.Player.EnableEffect<Invisible>(1, 30f);

    private void EnragingResistance(PlayerHurtingEventArgs ev, Player attacker, Player target)
    {
        if (target.Role != RoleTypeId.Scp096) return;
        var s = StateOf(target);
        if (s is Scp096RageState.Distressed or Scp096RageState.Enraged)
            Combat.MultiplyDamage(ev, 1f - MayhemPlugin.Cfg.Scps.Scp096EnragingDamageReduction / 100f);
    }

    public override void SecondTick()
    {
        var cfg = MayhemPlugin.Cfg.Scps;
        foreach (var p in PlayerExt.Alive.Where(p => p.Role == RoleTypeId.Scp096).ToList())
        {
            var state = StateOf(p);
            var stats = Stats.Of(p);
            float speed = state switch
            {
                Scp096RageState.Docile => cfg.Scp096DocileSpeed,
                Scp096RageState.Distressed => cfg.Scp096DistressedSpeed,
                Scp096RageState.Enraged => cfg.Scp096EnragedSpeed,
                _ => 0f,
            };

            if (Warhead.IsDetonated)
            {
                if (!_nukeRamp.TryGetValue(p.ReferenceHub, out var ramp)) ramp = 0;
                ramp = Mathf.Min(cfg.Scp096PostNukeMaxSpeed, ramp + 5f);
                _nukeRamp[p.ReferenceHub] = ramp;
                speed = Mathf.Max(speed, ramp);
                if (ramp >= cfg.Scp096PostNukeMaxSpeed && (p.GetEffect<Scp207>()?.Intensity ?? 0) < 4) p.EnableEffect<Scp207>(4);
            }
            stats.SetSpeed("scp096", speed);

            // Damage aura
            float dmg = state == Scp096RageState.Enraged ? cfg.Scp096EnragedAuraDamage : cfg.Scp096DocileAuraDamage;
            if (Warhead.IsDetonated) dmg *= cfg.Scp096PostNukeAuraMultiplier;
            foreach (var o in PlayerExt.Near(p.Position, cfg.Scp096AuraRange).Where(o => o.IsEnemyOf(p)).ToList())
            {
                o.Damage(dmg, p);
                if (Warhead.IsDetonated) EffectManager.Apply<Irradiated>(o, 5f, 1f);
            }

            // Passively breaks nearby windows so he never gets stuck.
            foreach (var w in Window.List)
                if (!w.IsDestroyed && !w.IsBroken && (w.Position - p.Position).sqrMagnitude < 9f) w.Health = 0;
        }
    }
}

// ═════════════════════════ SCP-106 ═════════════════════════
public sealed class Scp106Module : MayhemModule
{
    private readonly HashSet<ReferenceHub> _inPocket = new();

    public override void Register()
    {
        PlayerEvents.EnteredPocketDimension += OnEntered;
        PlayerEvents.LeavingPocketDimension += OnLeaving;
        PlayerEvents.LeftPocketDimension += OnLeft;
        Scp106Events.ChangedSubmersionStatus += OnSubmersion;
        Combat.Pre.Add(TraumatizedOneShot);
    }

    public override void Unregister()
    {
        PlayerEvents.EnteredPocketDimension -= OnEntered;
        PlayerEvents.LeavingPocketDimension -= OnLeaving;
        PlayerEvents.LeftPocketDimension -= OnLeft;
        Scp106Events.ChangedSubmersionStatus -= OnSubmersion;
    }

    public override void Reset() => _inPocket.Clear();

    private void OnEntered(PlayerEnteredPocketDimensionEventArgs ev) => _inPocket.Add(ev.Player.ReferenceHub);

    private static void OnLeaving(PlayerLeavingPocketDimensionEventArgs ev) =>
        ev.IsSuccessful = Rng.Chance(MayhemPlugin.Cfg.Scps.Scp106EscapeChance);

    private void OnLeft(PlayerLeftPocketDimensionEventArgs ev)
    {
        _inPocket.Remove(ev.Player.ReferenceHub);
        if (ev.IsSuccessful && ev.Player.IsAlive)
        {
            ev.Player.EnableEffect<Traumatized>(1);
            Hud.Message(ev.Player, "<color=#AA4444>You escaped... but you're traumatized. SCP-106 can one-shot you.</color>", 5f);
        }
    }

    private static bool IsTraumatized(Player p) => p.HasEffect<Traumatized>() && p.GetEffect<Traumatized>().IsEnabled;

    private static void TraumatizedOneShot(PlayerHurtingEventArgs ev, Player attacker, Player target)
    {
        if (attacker?.Role != RoleTypeId.Scp106 || !IsTraumatized(target)) return;
        Combat.SetDamage(ev, target.Health + target.ArtificialHealth + target.HumeShield + 50f);
    }

    private static void OnSubmersion(Scp106ChangedSubmersionStatusEventArgs ev)
    {
        if (ev.IsSubmerging) return;
        var p = ev.Player;
        Stats.Of(p).SetSpeed("scp106_emerge", 30f, 4f);
        foreach (var o in PlayerExt.Near(p.Position, 6f).Where(o => o.IsEnemyOf(p)).ToList())
            o.EnableEffect<Slowness>(30, 3f);
    }

    public override void SecondTick()
    {
        var cfg = MayhemPlugin.Cfg.Scps;
        _inPocket.RemoveWhere(h => Player.Get(h) is not { IsAlive: true });
        bool nuked = Warhead.IsDetonated;
        if (nuked)
            foreach (var h in PlayerExt.AliveHumans.Where(h => !h.IsPuppet() && !IsTraumatized(h)).ToList())
                h.EnableEffect<Traumatized>(1);

        foreach (var p in PlayerExt.Alive.Where(p => p.Role == RoleTypeId.Scp106).ToList())
        {
            var s = Stats.Of(p);
            var humans = PlayerExt.AliveHumans.Where(h => !h.IsPuppet()).ToList();
            int traumatized = humans.Count(IsTraumatized);
            int healthy = humans.Count - traumatized;
            s.SetDamageReduction("scp106", Mathf.Min(75f, healthy * cfg.Scp106DrPerHealthyPlayer));
            float speed = _inPocket.Count * cfg.Scp106SpeedPerPocketPlayer + traumatized * cfg.Scp106SpeedPerTraumatized;
            if (nuked) speed += 100f;
            s.SetSpeed("scp106", speed);

            float range = cfg.Scp106AuraBaseRange + traumatized * cfg.Scp106AuraRangePerTraumatized;
            float dmg = cfg.Scp106AuraDamage;
            if (nuked) { range *= 2f; dmg *= 2f; }
            foreach (var o in PlayerExt.Near(p.Position, range).Where(o => o.IsEnemyOf(p)).ToList())
                o.Damage(dmg, p);
        }
    }
}

// ═════════════════════════ SCP-939 ═════════════════════════
public sealed class Scp939Module : MayhemModule
{
    public override void Register()
    {
        Scp939Events.Lunged += OnLunged;
        PlayerEvents.ValidatedVisibility += OnVisibility;
        Combat.Post.Add(HuntedDamage);
    }

    public override void Unregister()
    {
        Scp939Events.Lunged -= OnLunged;
        PlayerEvents.ValidatedVisibility -= OnVisibility;
    }

    private static void OnLunged(Scp939LungedEventArgs ev)
    {
        if (ev.LungeState is not (Scp939LungeState.LandHit or Scp939LungeState.LandRegular or Scp939LungeState.LandHarsh)) return;
        var p = ev.Player;
        var cfg = MayhemPlugin.Cfg.Scps;
        var pos = p.Position;
        TimedGrenadeProjectile.PlayEffect(pos, ItemType.GrenadeHE);
        foreach (var o in PlayerExt.Near(pos, 5f).Where(o => o.IsEnemyOf(p)).ToList())
        {
            o.Damage(60f, p);
            o.EnableEffect<Sinkhole>(1, 3f);
            o.EnableEffect<Concussed>(1, 5f);
        }
        Stats.Of(p).SetSpeed("scp939_pounce", cfg.Scp939PounceSpeed, cfg.Scp939PounceSpeedSeconds);
    }

    private static void HuntedDamage(PlayerHurtingEventArgs ev, Player attacker, Player target)
    {
        if (attacker?.Role != RoleTypeId.Scp939 || target == attacker) return;
        var cfg = MayhemPlugin.Cfg.Scps;
        var hunted = EffectManager.Get<Hunted>(target);
        float mult = cfg.Scp939DamageMultiplier * (1f + (hunted?.Intensity ?? 0) * cfg.Scp939HuntedPerStack);
        Combat.MultiplyDamage(ev, mult);
        EffectManager.Apply<Hunted>(target, cfg.Scp939HuntedSeconds, 1);
    }

    /// <summary>After the nuke, 939 is no longer blind.</summary>
    private static void OnVisibility(PlayerValidatedVisibilityEventArgs ev)
    {
        if (ev.IsVisible || !Warhead.IsDetonated || ev.Player?.Role != RoleTypeId.Scp939 || ev.Target == null) return;
        if ((ev.Target.Position - ev.Player.Position).sqrMagnitude < 40f * 40f) ev.IsVisible = true;
    }

    public override void SecondTick()
    {
        var cfg = MayhemPlugin.Cfg.Scps;
        foreach (var p in PlayerExt.Alive.Where(p => p.Role == RoleTypeId.Scp939).ToList())
            Stats.Of(p).SetSpeed("scp939", cfg.Scp939BaseSpeed);

        // Amnestic clouds irradiate and slow everyone inside (+5s after leaving).
        foreach (var cloud in AmnesticCloudHazard.List.ToList())
        {
            if (cloud.IsDestroyed || !cloud.IsActive) continue;
            foreach (var p in cloud.AffectedPlayers.Where(p => p.IsAlive && !p.IsSCP).ToList())
            {
                EffectManager.Apply<Irradiated>(p, 5f, 1f);
                p.EnableEffect<Slowness>(25, 5f);
            }
        }
    }
}

// ═════════════════════════ SCP-049 / 049-2 ═════════════════════════
public sealed class Scp049Module : MayhemModule
{
    private readonly HashSet<ReferenceHub> _called = new();

    public override void Register()
    {
        Scp049Events.UsedDoctorsCall += OnCall;
        Scp049Events.Attacked += OnAttacked;
        PlayerEvents.Dying += OnDying;
        PlayerEvents.Death += OnDeath;
        Combat.Pre.Add(CardiacDouble);
    }

    public override void Unregister()
    {
        Scp049Events.UsedDoctorsCall -= OnCall;
        Scp049Events.Attacked -= OnAttacked;
        PlayerEvents.Dying -= OnDying;
        PlayerEvents.Death -= OnDeath;
    }

    public override void Reset() => _called.Clear();

    private static void CardiacDouble(PlayerHurtingEventArgs ev, Player attacker, Player target)
    {
        if (ev.DamageHandler is Scp049DamageHandler { DamageSubType: Scp049DamageHandler.AttackType.CardiacArrest })
            Combat.MultiplyDamage(ev, MayhemPlugin.Cfg.Scps.Scp049CardiacDamagePerSecond / 8f);
    }

    private static bool IsZombieKill(DamageHandlerBase dh, Player attacker) =>
        dh is Scp049DamageHandler || attacker?.Role == RoleTypeId.Scp0492;

    /// <summary>049 kills delete the inventory; zombie kills drop it (so it isn't lost).</summary>
    private static void OnDying(PlayerDyingEventArgs ev)
    {
        if (!ev.IsAllowed || !ev.Player.IsHuman) return;
        if (ev.DamageHandler is Scp049DamageHandler && ev.Attacker?.Role == RoleTypeId.Scp049) ev.Player.ClearInventory();
    }

    private static void OnDeath(PlayerDeathEventArgs ev)
    {
        if (!IsZombieKill(ev.DamageHandler, ev.Attacker) || ev.OldRole.GetTeam() == Team.SCPs) return;
        var p = ev.Player;
        var pos = ev.OldPosition;
        var cfg = MayhemPlugin.Cfg.Scps;
        Loops.After(0.5f, () =>
        {
            if (!p.Valid() || p.IsAlive) return;
            p.SetRole(RoleTypeId.Scp0492, RoleChangeReason.Revived, RoleSpawnFlags.None);
            Loops.After(0.1f, () => { if (p.Valid()) p.Position = pos + Vector3.up * 0.2f; });
            foreach (var z in PlayerExt.Alive.Where(z => z.Role == RoleTypeId.Scp0492 && z != p))
                z.Health = Mathf.Min(z.MaxHealth, z.Health + cfg.Scp049ZombieHealOnConvert);
            foreach (var doc in PlayerExt.Alive.Where(d => d.Role == RoleTypeId.Scp049))
                doc.Health += cfg.Scp049HealOnConvert; // can go over max
        });
    }

    private void OnCall(Scp049UsedDoctorsCallEventArgs ev)
    {
        var doc = ev.Player;
        _called.Add(doc.ReferenceHub);
        doc.EnableEffect<Invisible>(1, 20f);
        foreach (var z in PlayerExt.Near(doc.Position, 15f).Where(z => z.Role == RoleTypeId.Scp0492))
            z.EnableEffect<Invisible>(1, 20f);
        Loops.After(20f, () => _called.Remove(doc.ReferenceHub));
    }

    private void OnAttacked(Scp049AttackedEventArgs ev)
    {
        if (!_called.Remove(ev.Player.ReferenceHub)) return;
        ev.Player.DisableEffect<Invisible>();
        foreach (var z in PlayerExt.Alive.Where(z => z.Role == RoleTypeId.Scp0492)) z.DisableEffect<Invisible>();
    }

    public override void SecondTick()
    {
        var cfg = MayhemPlugin.Cfg.Scps;
        int zombies = PlayerExt.Alive.Count(z => z.Role == RoleTypeId.Scp0492);
        foreach (var p in PlayerExt.Alive.ToList())
        {
            if (p.Role == RoleTypeId.Scp049)
                Stats.Of(p).SetSpeed("scp049", cfg.Scp049BaseSpeed + zombies * cfg.Scp049SpeedPerZombie);
            else if (p.Role == RoleTypeId.Scp0492)
                Stats.Of(p).SetSpeed("scp0492", zombies * cfg.Scp0492SpeedPerZombie);
        }
    }
}

// ═════════════════════════ SCP-966 ═════════════════════════
/// <summary>
/// A black figure that appears behind a random player and just poses. Sometimes it brings a
/// facility-wide blackout with it.
/// </summary>
public sealed class Scp966Module : MayhemModule
{
    private float _next;
    private readonly List<AdminToy> _figure = new();

    public override void Reset()
    {
        _next = 0;
        Despawn();
    }

    public override void RoundStarted() => Schedule();

    private void Schedule()
    {
        var cfg = MayhemPlugin.Cfg.Scps;
        _next = Time.time + Rng.Range(cfg.Scp966MinInterval, cfg.Scp966MaxInterval);
    }

    public override void SecondTick()
    {
        var cfg = MayhemPlugin.Cfg.Scps;
        if (!cfg.Scp966Enabled || _next <= 0 || Time.time < _next || _figure.Count > 0) return;
        Schedule();
        var victim = Rng.Pick(PlayerExt.AliveHumans.Where(p => !p.IsPuppet() && p.Zone != MapGeneration.FacilityZone.Surface).ToList());
        if (victim == null) return;
        Appear(victim);
        if (Rng.Chance(cfg.Scp966BlackoutChance))
        {
            Map.TurnOffLights(cfg.Scp966BlackoutSeconds);
            CassieW.Say("pitch_0.8 .g4 facility power failure", "Facility power failure.");
        }
    }

    private void Appear(Player victim)
    {
        var fwd = victim.Camera.forward;
        fwd.y = 0;
        fwd = fwd.sqrMagnitude < 0.01f ? Vector3.forward : fwd.normalized;
        var basePos = victim.Position - fwd * 2.2f;
        var look = Quaternion.LookRotation(fwd);
        var black = new Color(0.02f, 0.02f, 0.02f, 1f);

        Part(PrimitiveType.Capsule, basePos + Vector3.up * 0.1f, look, new Vector3(0.45f, 0.95f, 0.3f), black);        // torso+legs
        Part(PrimitiveType.Sphere, basePos + Vector3.up * 1.15f, look, new Vector3(0.32f, 0.38f, 0.32f), black);      // head
        // Arms raised in a pose
        Part(PrimitiveType.Capsule, basePos + Vector3.up * 0.95f + look * Vector3.right * 0.4f, look * Quaternion.Euler(0, 0, -140), new Vector3(0.12f, 0.45f, 0.12f), black);
        Part(PrimitiveType.Capsule, basePos + Vector3.up * 0.95f - look * Vector3.right * 0.4f, look * Quaternion.Euler(0, 0, 140), new Vector3(0.12f, 0.45f, 0.12f), black);

        Loops.After(MayhemPlugin.Cfg.Scps.Scp966LingerSeconds, Despawn);
    }

    private void Part(PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale, Color color)
    {
        var t = PrimitiveObjectToy.Create(pos, rot, scale, null, false);
        t.Type = type;
        t.Color = color;
        t.Flags = AdminToys.PrimitiveFlags.Visible;
        t.Spawn();
        _figure.Add(t);
    }

    private void Despawn()
    {
        foreach (var t in _figure) if (!t.IsDestroyed) t.Destroy();
        _figure.Clear();
    }
}
