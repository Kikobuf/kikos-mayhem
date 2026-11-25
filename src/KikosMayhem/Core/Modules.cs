using System;
using System.Collections.Generic;
using LabApi.Features.Console;

namespace KikosMayhem.Core;

/// <summary>A gameplay system (gambling, an SCP rework, 079/Puppet, events...).</summary>
public abstract class MayhemModule
{
    public virtual void Register() { }
    public virtual void Unregister() { }
    /// <summary>Clear all per-round state.</summary>
    public virtual void Reset() { }
    public virtual void RoundStarted() { }
    public virtual void SlowTick() { }   // 0.5s
    public virtual void SecondTick() { } // 1s
}

public static class Modules
{
    public static readonly List<MayhemModule> All = new();

    public static T Get<T>() where T : MayhemModule => All.Find(m => m is T) as T;

    private static void Each(string what, Action<MayhemModule> a)
    {
        foreach (var m in All)
        {
            try { a(m); }
            catch (Exception e) { Logger.Error($"[Mayhem] {m.GetType().Name}.{what} failed: {e}"); }
        }
    }

    public static void Register()
    {
        All.Clear();
        All.AddRange(ModuleList.Create());
        Each("Register", m => m.Register());
    }

    public static void Unregister() => Each("Unregister", m => m.Unregister());
    public static void Reset() => Each("Reset", m => m.Reset());
    public static void RoundStarted() => Each("RoundStarted", m => m.RoundStarted());
    public static void SlowTick() => Each("SlowTick", m => m.SlowTick());
    public static void SecondTick() => Each("SecondTick", m => m.SecondTick());
}
