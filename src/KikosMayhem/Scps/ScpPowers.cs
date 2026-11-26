using System.Collections.Generic;
using KikosMayhem.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using PlayerRoles;

namespace KikosMayhem.Scps;

/// <summary>When someone becomes an SCP, a pop-up at the top middle of their screen lists their Mayhem powers for 5 seconds.</summary>
public sealed class ScpPowers : MayhemModule
{
    private static string Title(string name) => $"<b><color=#FF4040>{name}</color></b>";

    private static Dictionary<RoleTypeId, string> Texts()
    {
        var c = MayhemPlugin.Cfg.Scps;
        return new()
        {
            [RoleTypeId.Scp173] = Title("SCP-173") +
                $"\nBlinks {c.Scp173BlinkFaster:0}% faster, +{c.Scp173BlinkPerDead:0}% per dead player · Tantrums are explosive traps · Breakneck breaks doors",
            [RoleTypeId.Scp096] = Title("SCP-096") +
                "\nTiny until enraged · Damage aura around you · Breaks nearby windows\nCharging explodes · Trying not to cry makes you invisible · Stronger after the nuke",
            [RoleTypeId.Scp106] = Title("SCP-106") +
                "\nOne hit sends to the pocket dimension · Escapees are traumatized and die in one hit\nAura grows with traumatized players · Emerging slows people nearby · Doubled after the nuke",
            [RoleTypeId.Scp939] = Title("SCP-939") +
                $"\nPounce explodes and gives +{c.Scp939PounceSpeed:0}% speed · Hits stack Hunted (more damage)\nAmnestic cloud irradiates · Not blind after the nuke",
            [RoleTypeId.Scp049] = Title("SCP-049") +
                $"\n+{c.Scp049BaseSpeed:0}% speed · Cardiac arrest deals {c.Scp049CardiacDamagePerSecond:0}/s\nVictims instantly become zombies and heal you · Your call makes you and nearby zombies invisible",
            [RoleTypeId.Scp0492] = Title("SCP-049-2") +
                $"\n+{c.Scp0492SpeedPerZombie:0}% speed for every zombie alive · Infected players drop their items",
            [RoleTypeId.Scp079] = Title("SCP-079") +
                "\nPick a Puppet: ping a player or type <b>.puppet</b> · Generators are your lives\nPing the Puppet to heal it · T2 slam bulkheads · T3 grenade barrage · T4 overcharge generators · T5 the nuke",
        };
    }

    public static string PuppetText(bool ascended) => ascended
        ? Title("ASCENDED SCP-079") + "\nKill everyone, or reach the Gate B exit · Can't be healed, but regenerates fast after a few seconds without damage"
        : Title("SCP-079's PUPPET") +
          "\nHume shield blocks all health damage (one-shot protection) · Tooth and Nail levels up with SCP-079\nYou die with SCP-079 · Generators are your lives · Reach tier 5 to start the Ascension";

    public override void Register() => PlayerEvents.ChangedRole += OnChangedRole;

    public override void Unregister() => PlayerEvents.ChangedRole -= OnChangedRole;

    private static void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        var p = ev.Player;
        if (p == null || Items.Defs.Decoys.IsDecoy(p) || !MayhemPlugin.Cfg.Scps.ShowPowersPopup) return;
        if (Texts().TryGetValue(ev.NewRole.RoleTypeId, out var text))
            Hud.Popup(p, text, MayhemPlugin.Cfg.Scps.PowersPopupSeconds);
    }
}
