using System;
using System.Collections.Generic;
using System.Linq;
using LabApi.Features.Wrappers;
using UnityEngine;
using UserSettings.ServerSpecific;

namespace KikosMayhem.Core;

/// <summary>
/// Server-specific settings (Settings → Server-specific tab in the game):
///  - "Puppet": opt out of being picked as SCP-079's Puppet
///  - "Quick Recycle": keybind for Puppet to recycle the closest custom item
/// </summary>
public static class MayhemSettings
{
    private const int HeaderId = 7900;
    private const int OptOutId = 7901;
    private const int QuickRecycleId = 7902;

    /// <summary>Raised when a player presses the Quick Recycle key.</summary>
    public static event Action<Player> QuickRecyclePressed;

    public static void Register()
    {
        var mine = new ServerSpecificSettingBase[]
        {
            new SSGroupHeader(HeaderId, "Kiko's Mayhem"),
            new SSTwoButtonsSetting(OptOutId, "Can be picked as SCP-079's Puppet", "Yes", "No (opt out)", false,
                "If you opt out, SCP-079 can only pick you when nobody else is available."),
            new SSKeybindSetting(QuickRecycleId, "Puppet: Quick Recycle", KeyCode.R, true, false,
                "As Puppet, sends the closest custom item to the Pocket Dimension."),
        };
        var existing = ServerSpecificSettingsSync.DefinedSettings ?? Array.Empty<ServerSpecificSettingBase>();
        ServerSpecificSettingsSync.DefinedSettings = existing.Where(s => s.SettingId < HeaderId || s.SettingId > QuickRecycleId).Concat(mine).ToArray();
        ServerSpecificSettingsSync.SendToAll();
        ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnValue;
    }

    public static void Unregister()
    {
        ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnValue;
        if (ServerSpecificSettingsSync.DefinedSettings != null)
            ServerSpecificSettingsSync.DefinedSettings = ServerSpecificSettingsSync.DefinedSettings
                .Where(s => s.SettingId < HeaderId || s.SettingId > QuickRecycleId).ToArray();
    }

    public static bool OptedOutOfPuppet(Player p)
    {
        try
        {
            return ServerSpecificSettingsSync.TryGetSettingOfUser<SSTwoButtonsSetting>(p.ReferenceHub, OptOutId, out var s) && s.SyncIsB;
        }
        catch { return false; }
    }

    private static void OnValue(ReferenceHub hub, ServerSpecificSettingBase setting)
    {
        if (setting is SSKeybindSetting { SettingId: QuickRecycleId } kb && kb.SyncIsPressed)
        {
            var p = Player.Get(hub);
            if (p != null) QuickRecyclePressed?.Invoke(p);
        }
    }
}
