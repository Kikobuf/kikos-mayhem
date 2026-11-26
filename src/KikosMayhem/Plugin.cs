using System;
using System.IO;
using KikosMayhem.Core;
using KikosMayhem.Effects;
using KikosMayhem.Items;
using LabApi.Events.Handlers;
using LabApi.Features.Console;
using LabApi.Loader;
using LabApi.Loader.Features.Plugins;
using MEC;

namespace KikosMayhem;

public sealed class MayhemPlugin : Plugin<MayhemConfig>
{
    public static MayhemPlugin Instance { get; private set; }
    public static MayhemConfig Cfg => Instance?.Config ?? new MayhemConfig();

    public override string Name => "KikosMayhem";
    public override string Description => "Kiko's Mayhem — chaotic custom items, reworked SCPs, SCP-079's Puppet and global events.";
    public override string Author => "Kikobuf";
    public override Version Version => new(1, 4, 0);
    public override Version RequiredApiVersion => new(1, 1, 7);

    private CoroutineHandle _fast, _slow, _second;

    public override void Enable()
    {
        Instance = this;
        if (!Config.Enabled)
        {
            Logger.Warn("[Mayhem] disabled in config — running vanilla.");
            return;
        }

        try
        {
            Audio.Clips.Folder = Path.Combine(this.GetConfigDirectory().FullName, "audio");
            Directory.CreateDirectory(Audio.Clips.Folder);
        }
        catch (Exception e)
        {
            Logger.Warn($"[Mayhem] couldn't create audio folder: {e.Message}");
        }

        Catalog.DefineAll();
        Disguise.Register();
        Combat.Register();
        Combat.DyingHooks.Add(ItemEngine.OwnerDying);
        ItemEngine.Register();
        Recycling.Register();
        MayhemSettings.Register();
        Tazed.Register();
        Modules.Register();

        ServerEvents.WaitingForPlayers += OnWaiting;
        ServerEvents.RoundStarted += OnRoundStarted;
        ServerEvents.RoundRestarted += OnRestart;
        PlayerEvents.ChangedRole += OnChangedRole;

        _fast = Timing.RunCoroutine(Ticker(0.25f, FastTick));
        _slow = Timing.RunCoroutine(Ticker(0.5f, SlowTick));
        _second = Timing.RunCoroutine(Ticker(1f, SecondTick));

        Logger.Info($"[Mayhem] Kiko's Mayhem {Version} enabled — {System.Linq.Enumerable.Count(ItemEngine.All)} custom items loaded.");
    }

    public override void Disable()
    {
        Timing.KillCoroutines(_fast, _slow, _second);
        ServerEvents.WaitingForPlayers -= OnWaiting;
        ServerEvents.RoundStarted -= OnRoundStarted;
        ServerEvents.RoundRestarted -= OnRestart;
        PlayerEvents.ChangedRole -= OnChangedRole;
        Modules.Unregister();
        ItemEngine.Unregister();
        Recycling.Unregister();
        MayhemSettings.Unregister();
        Tazed.Unregister();
        Combat.Unregister();
        Disguise.Unregister();
        ResetRound();
        Instance = null;
    }

    private static System.Collections.Generic.IEnumerator<float> Ticker(float interval, Action tick)
    {
        while (true)
        {
            yield return Timing.WaitForSeconds(interval);
            try { tick(); }
            catch (Exception e) { Logger.Error($"[Mayhem] tick error: {e}"); }
        }
    }

    private static void FastTick()
    {
        if (!LabApi.Features.Wrappers.Round.IsRoundStarted) return;
        ItemEngine.Tick();
    }

    private static void SlowTick()
    {
        Hud.Tick();
        if (!LabApi.Features.Wrappers.Round.IsRoundStarted) return;
        Stats.Tick();
        Modules.SlowTick();
    }

    private static void SecondTick()
    {
        if (!LabApi.Features.Wrappers.Round.IsRoundStarted) return;
        EffectManager.Tick();
        Recycling.Tick();
        Modules.SecondTick();
    }

    /// <summary>A new role is a new life: Mayhem effects, disguises and attached music don't carry over.</summary>
    private static void OnChangedRole(LabApi.Events.Arguments.PlayerEvents.PlayerChangedRoleEventArgs ev)
    {
        if (EffectManager.Has<Items.ReckoningEffect>(ev.Player)) ev.Player.IsBypassEnabled = false;
        EffectManager.RemoveAll(ev.Player, silent: true);
        Disguise.Clear(ev.Player);
        Audio.Music.StopOn(ev.Player);
        Hud.ClearInfo(ev.Player, "odin");
        Hud.ClearInfo(ev.Player, "tracker");
    }

    private static void OnWaiting() => ResetRound();

    private static void OnRestart() => ResetRound();

    private static void ResetRound()
    {
        Loops.KillRoundLoops();
        Stats.Clear();
        EffectManager.Clear();
        Hud.Clear();
        Disguise.ClearAll();
        ItemEngine.Reset();
        ItemSpawner.Reset();
        Recycling.Reset();
        Items.Defs.Decoys.Reset();
        Audio.Music.Reset();
        Modules.Reset();
    }

    private static void OnRoundStarted()
    {
        Modules.RoundStarted();
        // Items after the global event is chosen so 5x/causality apply.
        Loops.After(1.5f, ItemSpawner.SpawnRoundItems);
    }
}
