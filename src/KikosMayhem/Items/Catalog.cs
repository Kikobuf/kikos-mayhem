using KikosMayhem.Core;
using KikosMayhem.Items.Defs;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using PlayerStatsSystem;

namespace KikosMayhem.Items;

/// <summary>Registers every custom item and the cross-item hooks.</summary>
public static class Catalog
{
    private static bool _defined;

    public static void DefineAll()
    {
        if (!_defined)
        {
            _defined = true;
            CustomItem[] items =
            {
                // Common (green)
                new Addictionater(), new CritiCola(), new Beer(), new EchoBall(), new SchrodingersGrenade(),
                new GreedyGrenade(), new KeyToTheCity(), new LockToTheCity(), new RattleEm(), new RegeneratingE11(),
                new Soberizer(),
                // Rare (blue)
                new Accelerator(), new PinkExtract(), new Vampirism(), new TurboBoost(), new WarpCoin(), new Autovest(),
                new Mirage(), new ByeByeMachine(), new Pinpointer(), new Real207(), new WarpGrenade(), new Tazer(),
                new HomecookedDinner(), new PaddedArmour(), new AnomalousTracker(), new SwitchARoo(),
                // Epic (purple)
                new RealAnti207(), new ClusterGrenade(), new Peacemaker(), new Effectorizer(), new Skinwalker(),
                new GuardiansGlow(), new AllPill(), new DubstepGun(), new RecursiveDestinyCoin(), new HuginnAndMuninn(),
                // Legendary (yellow)
                new CrimsonEcho(), new GrenadeLauncher(), new Minigun(), new SuicideVest(), new Scp18018(), new Real500(),
                new Hyperion(),
                // Upgraded (cyan)
                new ScroogeGrenade(), new SonicSurge(), new HotswapCoin(), new Realest207(), new PaladinsElixir(),
                new RollingThunder(),
                // Ascended (red)
                new Hellfire(), new PocketNuke(), new MayhemCoin(), new HellsReckoning(),
                // Admin (rainbow)
                new LiveDuckReaction(), new Ultimate500(), new OverchargedAdrenaline(), new Elixir(), new MitzeysScream(),
                // Newer additions
                new Mitosis(), new JestersHat(),
            };
            foreach (var i in items) ItemEngine.Define(i);
        }

        Combat.KillHooks.Clear();
        Combat.Pre.Clear();
        Combat.Post.Clear();

        Combat.KillHooks.Add(EchoBall.OnAnyKill);
        Combat.KillHooks.Add(Vampirism.OnAnyKill);
        Combat.KillHooks.Add(HellsReckoning.OnAnyKill);

        Combat.Pre.Add(Scp18018.TeamSafety);
        Combat.Pre.Add(StatusDamageFilter);
    }

    /// <summary>
    /// Negative-effect immunity also blocks damage from negative effects (SCP-207 poisoning, bleeding...),
    /// and the Elixir removes fall damage.
    /// </summary>
    private static void StatusDamageFilter(PlayerHurtingEventArgs ev, Player attacker, Player target)
    {
        if (ev.DamageHandler is not UniversalDamageHandler u) return;
        var s = Stats.Of(target);
        byte id = u.TranslationId;
        if (s.Flagged("no_fall_damage") && id == DeathTranslations.Falldown.Id)
        {
            ev.IsAllowed = false;
            return;
        }
        if (!s.NegativeImmune) return;
        if (id == DeathTranslations.Scp207.Id || id == DeathTranslations.Bleeding.Id || id == DeathTranslations.Poisoned.Id
            || id == DeathTranslations.Asphyxiated.Id || id == DeathTranslations.SeveredHands.Id || id == DeathTranslations.CardiacArrest.Id)
            ev.IsAllowed = false;
    }
}
