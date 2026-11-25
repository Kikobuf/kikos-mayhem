using System;
using System.Collections.Generic;
using KikosMayhem.Effects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using PlayerStatsSystem;

namespace KikosMayhem.Core;

/// <summary>
/// Single damage pipeline so every Mayhem modifier runs in a predictable order:
///   1. Pre hooks (SCP passives, Puppet one-shot protection, true-damage items)
///   2. Custom item hooks for the weapon used
///   3. Custom effect hooks (attacker, then target)
///   4. Crits
///   5. Post hooks (damage multipliers that should include crits, e.g. Hunted)
/// </summary>
public static class Combat
{
    public delegate void HurtingHook(PlayerHurtingEventArgs ev, Player attacker, Player target);
    public delegate void KillHook(Player killer, Player victim, PlayerDeathEventArgs ev);

    public static readonly List<HurtingHook> Pre = new();
    public static readonly List<HurtingHook> Post = new();
    public static readonly List<Action<PlayerDyingEventArgs>> DyingHooks = new();
    public static readonly List<KillHook> KillHooks = new();

    /// <summary>Set by items whose damage must not crit (Crimson Echo, Schrödinger's 3x).</summary>
    [ThreadStatic] public static bool SuppressCrit;

    public static void Register()
    {
        PlayerEvents.Hurting += OnHurting;
        PlayerEvents.Dying += OnDying;
        PlayerEvents.Death += OnDeath;
    }

    public static void Unregister()
    {
        PlayerEvents.Hurting -= OnHurting;
        PlayerEvents.Dying -= OnDying;
        PlayerEvents.Death -= OnDeath;
    }

    public static float GetDamage(PlayerHurtingEventArgs ev) =>
        ev.DamageHandler is StandardDamageHandler s ? s.Damage : 0f;

    public static void SetDamage(PlayerHurtingEventArgs ev, float value)
    {
        if (ev.DamageHandler is StandardDamageHandler s) s.Damage = value;
    }

    public static void MultiplyDamage(PlayerHurtingEventArgs ev, float mult)
    {
        if (ev.DamageHandler is StandardDamageHandler s) s.Damage *= mult;
    }

    private static void Trace(PlayerHurtingEventArgs ev, string stage)
    {
        if (MayhemPlugin.Cfg.Debug)
            LabApi.Features.Console.Logger.Info($"[Mayhem:dmg] {ev.Attacker?.Nickname ?? "world"} -> {ev.Player.Nickname} {ev.DamageHandler.GetType().Name} dmg={GetDamage(ev):0.#} allowed={ev.IsAllowed} @ {stage}");
    }

    private static void OnHurting(PlayerHurtingEventArgs ev)
    {
        try
        {
            Trace(ev, "start");
            SuppressCrit = false;
            var target = ev.Player;
            var attacker = ev.Attacker;

            foreach (var h in Pre)
            {
                h(ev, attacker, target);
                if (!ev.IsAllowed) { Trace(ev, "pre:" + h.Method.DeclaringType?.Name + "." + h.Method.Name); return; }
            }

            if (attacker != null && attacker != target)
            {
                Items.ItemEngine.AttackerHurting(ev, attacker, target);
                if (!ev.IsAllowed) return;
                EffectManager.Attacking(attacker, target, ev);
                if (!ev.IsAllowed) return;
            }

            EffectManager.Hurting(target, attacker, ev);
            if (!ev.IsAllowed) return;
            Items.ItemEngine.OwnerHurting(ev, attacker, target);
            if (!ev.IsAllowed) return;

            if (attacker != null && attacker != target && !SuppressCrit && attacker.IsEnemyOf(target))
                ApplyCrit(ev, attacker);

            foreach (var h in Post)
            {
                h(ev, attacker, target);
                if (!ev.IsAllowed) return;
            }
            Trace(ev, "end");
        }
        catch (Exception e)
        {
            LabApi.Features.Console.Logger.Error($"[Mayhem] hurting pipeline error: {e}");
        }
    }

    private static void ApplyCrit(PlayerHurtingEventArgs ev, Player attacker)
    {
        var s = Stats.Of(attacker);
        if (!s.HasAnyCrit) return;
        if (!Rng.Chance(s.CritChance)) return;
        MultiplyDamage(ev, s.CritMultiplier);
        attacker.SendHitMarker(2.5f);
        Hud.Message(attacker, $"<color=#FFD700>CRIT x{s.CritMultiplier:0.##}</color>", 0.8f, "crit");
    }

    private static void OnDying(PlayerDyingEventArgs ev)
    {
        try
        {
            EffectManager.Dying(ev);
            if (!ev.IsAllowed) return;
            foreach (var h in DyingHooks)
            {
                h(ev);
                if (!ev.IsAllowed) return;
            }
        }
        catch (Exception e)
        {
            LabApi.Features.Console.Logger.Error($"[Mayhem] dying pipeline error: {e}");
        }
    }

    private static void OnDeath(PlayerDeathEventArgs ev)
    {
        try
        {
            EffectManager.RemoveAll(ev.Player);
            var killer = ev.Attacker;
            if (killer != null && killer != ev.Player)
            {
                Items.ItemEngine.Killed(killer, ev.Player, ev);
                foreach (var h in KillHooks) h(killer, ev.Player, ev);
            }
        }
        catch (Exception e)
        {
            LabApi.Features.Console.Logger.Error($"[Mayhem] death pipeline error: {e}");
        }
    }
}
