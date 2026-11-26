using System;
using System.Linq;
using System.Text;
using CommandSystem;
using KikosMayhem.Core;
using KikosMayhem.Events;
using KikosMayhem.Items;
using LabApi.Features.Wrappers;
using PlayerRoles;
using UnityEngine;

namespace KikosMayhem.Commands;

/// <summary>.puppet — SCP-079 picks its Puppet. ".puppet" lists players, ".puppet 5" or ".puppet Some Name" picks.</summary>
[CommandHandler(typeof(ClientCommandHandler))]
public sealed class PuppetCommand : ICommand
{
    public string Command => "puppet";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "SCP-079: choose your Puppet. Use without arguments to list players.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        var pc = Player.Get(sender);
        var pm = Scp079.PuppetManager.Instance;
        if (pc == null || pc.Role != RoleTypeId.Scp079 || pm == null)
        {
            response = "Only SCP-079 can use this.";
            return false;
        }
        var list = pm.Candidates();
        if (arguments.Count == 0)
        {
            var sb = new StringBuilder("Choose a Puppet with .puppet <number> or .puppet <name>:\n");
            for (int i = 0; i < list.Count; i++)
                sb.AppendLine($"  {i + 1}. {list[i].Nickname} ({(list[i].IsAlive ? list[i].Role.ToString() : "dead")})");
            response = sb.ToString();
            return true;
        }
        string arg = string.Join(" ", arguments).Trim().Trim('"');
        Player target = null;
        if (int.TryParse(arg, out int n) && n >= 1 && n <= list.Count) target = list[n - 1];
        target ??= list.FirstOrDefault(p => p.UserId == arg)
                   ?? list.FirstOrDefault(p => p.Nickname.Equals(arg, StringComparison.OrdinalIgnoreCase))
                   ?? list.FirstOrDefault(p => p.Nickname.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0);
        if (target == null)
        {
            response = $"No player matching '{arg}'. Type .puppet to see the list.";
            return false;
        }
        response = pm.Pick(target);
        return true;
    }
}

/// <summary>.code 1234 — Puppet enters the ascension code while standing at the Nuke Room keypad.</summary>
[CommandHandler(typeof(ClientCommandHandler))]
public sealed class CodeCommand : ICommand
{
    public string Command => "code";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Puppet: enter the ascension code at the Nuke Room keypad.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        var p = Player.Get(sender);
        var quest = Ascension.AscensionQuest.Instance;
        if (p == null || quest == null || arguments.Count == 0)
        {
            response = "Usage: .code <digits>";
            return false;
        }
        response = quest.SubmitFromCommand(p, arguments.At(0));
        return true;
    }
}

/// <summary>Admin command: mayhem give/items/event/ascension/spawnitems/dump.</summary>
[CommandHandler(typeof(RemoteAdminCommandHandler))]
[CommandHandler(typeof(GameConsoleCommandHandler))]
public sealed class MayhemCommand : ICommand
{
    public string Command => "mayhem";
    public string[] Aliases => new[] { "km" };
    public string Description => "Kiko's Mayhem admin tools. Run 'mayhem help'.";

    private const string Help =
        "mayhem items                      — list custom item ids\n" +
        "mayhem give <player|all|me> <id>  — give a custom item\n" +
        "mayhem spawn <id>                 — spawn a custom item at your feet\n" +
        "mayhem event <GlobalEvent>        — start a global event (RareItemsCommon, FiveXLoot, ...)\n" +
        "mayhem sub <SubEvent>             — start a sub event (BeQuiet, FinallyAwake, CandyOnDeath)\n" +
        "mayhem protocol | insurgency      — start Mayhem Protocol / M.Y.H.M Insurgency now\n" +
        "mayhem lootpile                   — spawn an Ultimate Loot Pile\n" +
        "mayhem puppet <player>            — force someone to be SCP-079's Puppet\n" +
        "mayhem ascension start            — start the ascension questline now\n" +
        "mayhem effect <player|me> <Name> [seconds] — apply a Mayhem effect\n" +
        "mayhem keypad [seconds]           — highlight the warhead panel's keypad buttons (calibration)\n" +
        "mayhem dump                       — print positions of Mayhem landmarks (debug)";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (sender is CommandSender cs && !cs.CheckPermission(PlayerPermissions.GivingItems))
        {
            response = "You need the GivingItems permission.";
            return false;
        }
        if (arguments.Count == 0 || arguments.At(0) == "help")
        {
            response = Help;
            return true;
        }
        var me = Player.Get(sender);
        string sub = arguments.At(0).ToLowerInvariant();
        string Arg(int i) => arguments.Count > i ? arguments.At(i) : null;

        switch (sub)
        {
            case "items":
            {
                var sb = new StringBuilder();
                foreach (var g in ItemEngine.All.Where(d => !d.Hidden).GroupBy(d => d.Rarity).OrderBy(g => g.Key))
                    sb.AppendLine($"{g.Key}: " + string.Join(", ", g.Select(d => d.Id)));
                response = sb.ToString();
                return true;
            }
            case "give":
            {
                if (!ItemEngine.TryGetDef(Arg(2) ?? "", out var def))
                {
                    response = "Unknown item id. Use 'mayhem items'.";
                    return false;
                }
                var targets = Arg(1) switch
                {
                    "all" => PlayerExt.Alive.ToList(),
                    "me" or null => me != null ? new() { me } : new(),
                    var name => Player.ReadyList.Where(p => p.Nickname.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 || p.PlayerId.ToString() == name).ToList(),
                };
                foreach (var t in targets) ItemEngine.Give(t, def);
                response = $"Gave {def.Name} to {targets.Count} player(s).";
                return true;
            }
            case "spawn":
            {
                if (me == null || !ItemEngine.TryGetDef(Arg(1) ?? "", out var def))
                {
                    response = "Usage: mayhem spawn <id> (in game)";
                    return false;
                }
                ItemEngine.Spawn(def, me.Position + Vector3.up * 0.3f);
                response = $"Spawned {def.Name}.";
                return true;
            }
            case "event":
            {
                if (!Enum.TryParse<GlobalEvent>(Arg(1) ?? "", true, out var e))
                {
                    response = "Events: " + string.Join(", ", Enum.GetNames(typeof(GlobalEvent)));
                    return false;
                }
                EventsModule.Instance?.StartGlobal(e);
                response = $"Started {e}.";
                return true;
            }
            case "sub":
            {
                if (!Enum.TryParse<SubEvent>(Arg(1) ?? "", true, out var e))
                {
                    response = "Sub events: " + string.Join(", ", Enum.GetNames(typeof(SubEvent)));
                    return false;
                }
                EventsModule.Instance?.StartSub(e);
                response = $"Started {e}.";
                return true;
            }
            case "protocol":
                EventsModule.Instance?.ForceMayhem(true);
                response = "Mayhem Protocol started.";
                return true;
            case "insurgency":
                EventsModule.Instance?.ForceMayhem(false);
                response = "M.Y.H.M Insurgency started.";
                return true;
            case "lootpile":
                EventsModule.UltimateLootPile();
                response = "Ultimate Loot Pile spawned.";
                return true;
            case "puppet":
            {
                var pm = Scp079.PuppetManager.Instance;
                var target = Player.ReadyList.FirstOrDefault(p => p.Nickname.IndexOf(Arg(1) ?? "\0", StringComparison.OrdinalIgnoreCase) >= 0 || p.PlayerId.ToString() == Arg(1));
                if (pm == null || target == null)
                {
                    response = "Usage: mayhem puppet <player> (needs SCP-079 in the round)";
                    return false;
                }
                response = pm.Pick(target);
                return true;
            }
            case "ascension":
            {
                var q = Ascension.AscensionQuest.Instance;
                if (q == null)
                {
                    response = "Ascension module not loaded.";
                    return false;
                }
                q.ForceAllowed = true;
                q.TryStart();
                response = $"Ascension stage: {q.Current}";
                return true;
            }
            case "effect":
            {
                var target = Arg(1) == "me" ? me : Player.ReadyList.FirstOrDefault(p => p.Nickname.IndexOf(Arg(1) ?? "\0", StringComparison.OrdinalIgnoreCase) >= 0);
                float secs = float.TryParse(Arg(3), out var s) ? s : 30f;
                if (target == null || !Effects.EffectApplier.TryApply(target, Arg(2), secs))
                {
                    response = "Usage: mayhem effect <player|me> <Manic|Radiance|Herd|TurboDrunk|Tazed|Irradiated|Hunted|Burning|EyesOfOdin|SkinwalkerEffect> [seconds]";
                    return false;
                }
                response = "Applied.";
                return true;
            }
            case "dump":
                response = Debugging.Dump();
                return true;
            case "keypad":
                response = Ascension.AscensionQuest.Instance?.DebugShowKeypad(float.TryParse(Arg(1), out var ks) ? ks : 60f) ?? "Ascension module not loaded.";
                return true;
            case "dumproom":
                response = Debugging.DumpRoom(Arg(1) ?? "HczServers", Arg(2));
                return true;
            case "actions":
            {
                var target = Player.ReadyList.FirstOrDefault(p => p.IsDummy && (p.Nickname.IndexOf(Arg(1) ?? "", StringComparison.OrdinalIgnoreCase) >= 0 || p.PlayerId.ToString() == Arg(1)));
                response = target == null ? "No such dummy." : Debugging.DummyActions(target);
                return true;
            }
            case "selftest":
                SelfTest.Run(Arg(1) == "puppet", Arg(1) == "nuke");
                response = "Self test started — watch the console.";
                return true;
        }
        response = Help;
        return false;
    }
}
