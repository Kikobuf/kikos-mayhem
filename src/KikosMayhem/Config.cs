using System.Collections.Generic;
using System.ComponentModel;

namespace KikosMayhem;

/// <summary>
/// Every number in Kiko's Mayhem lives here so the host can rebalance without recompiling.
/// The file is generated at LabAPI/configs/&lt;port&gt;/KikosMayhem/config.yml on first start.
/// </summary>
public sealed class MayhemConfig
{
    [Description("Master switch. When false the server plays vanilla SCP:SL.")]
    public bool Enabled { get; set; } = true;

    [Description("Prints extra debug logs to the server console.")]
    public bool Debug { get; set; } = false;

    public ItemsConfig Items { get; set; } = new();
    public SpawnConfig Spawning { get; set; } = new();
    public EffectsConfig Effects { get; set; } = new();
    public GamblingConfig Gambling { get; set; } = new();
    public ScpConfig Scps { get; set; } = new();
    public Scp079Config Scp079 { get; set; } = new();
    public AscensionConfig Ascension { get; set; } = new();
    public EventsConfig Events { get; set; } = new();
    public AudioConfig Audio { get; set; } = new();
}

public sealed class ItemsConfig
{
    [Description("Custom item ids that are disabled (never spawn, can't be given). See docs/ITEMS.md for ids.")]
    public List<string> Disabled { get; set; } = new();

    [Description("Show the held custom item's name and description at the bottom of the screen.")]
    public bool ShowHeldDescription { get; set; } = true;

    [Description("Aura light intensity / range on dropped custom items.")]
    public float AuraIntensity { get; set; } = 1.2f;
    public float AuraRange { get; set; } = 1.6f;

    [Description("Custom items thrown into a pit go to SCP-106's dimension and are later recycled into the facility (also O5 / Facility Manager cards).")]
    public bool RecycleFromPits { get; set; } = true;
    public float RecycleDelaySeconds { get; set; } = 90f;

    public float SkinwalkerRevealSeconds { get; set; } = 3f;
    public float RadianceHealPerSecond { get; set; } = 2f;
    public float RadianceMaxAhp { get; set; } = 14f;
    public float RadianceRange { get; set; } = 8f;
    public float RadianceReviveCooldown { get; set; } = 120f;

    public float WarpCoinCooldown { get; set; } = 30f;
    public float LockToTheCityLockSeconds { get; set; } = 10f;
    public float LockToTheCityCooldown { get; set; } = 20f;
    public float MayhemCoinCooldown { get; set; } = 300f;

    public float VampirismStartSeconds { get; set; } = 35f;
    public float VampirismSecondsPerKill { get; set; } = 10f;

    [Description("Hell's Reckoning: seconds to kill an SCP before the jailbird breaks and kills you.")]
    public float HellsReckoningSeconds { get; set; } = 120f;

    public float PocketNukeIrradiatedPercent { get; set; } = 10f;
    public float PocketNukeRadius { get; set; } = 45f;
    public float SuicideVestDamage { get; set; } = 1000f;
    public float PinkExtractDamage { get; set; } = 1000f;
    public float CrimsonEchoTrueDamage { get; set; } = 200f;
    public float CrimsonEchoPuppetTrueDamage { get; set; } = 150f;
    public float CrimsonEchoSelfDamage { get; set; } = 40f;
    public float PinpointerDamage { get; set; } = 200f;
    public float PeacemakerScpHeal { get; set; } = 200f;
    public float PeacemakerSelfCostPercent { get; set; } = 40f;
}

public sealed class SpawnConfig
{
    [Description("Average custom items spawned per facility zone room at round start (rarity decides which).")]
    public float CustomItemsPerRoom { get; set; } = 0.35f;

    [Description("Relative spawn weight per rarity (Admin items never spawn naturally).")]
    public Dictionary<string, float> RarityWeights { get; set; } = new()
    {
        ["Common"] = 50f,
        ["Rare"] = 28f,
        ["Epic"] = 14f,
        ["Legendary"] = 6f,
        ["Upgraded"] = 1.5f,
        ["Ascended"] = 0.5f,
    };

    [Description("Gate A surface pile: number of custom items + normal items.")]
    public int GateACustomItems { get; set; } = 6;
    public int GateANormalItems { get; set; } = 10;

    [Description("Exit (Gate B escape) pile.")]
    public int ExitCustomItems { get; set; } = 2;

    [Description("SCP-079's room pile. Guaranteed 2 (+ extra roll). Only spawns if 079 isn't in the round, or once 079 is contained.")]
    public int Scp079RoomCustomItems { get; set; } = 2;
    public float Scp079RoomExtraChance { get; set; } = 35f;

    [Description("Chance (percent) to get a custom item when escaping for the first time.")]
    public float EscapeCustomItemChance { get; set; } = 30f;

    [Description("Rooms where no items spawn.")]
    public List<string> BlacklistedRooms { get; set; } = new() { "HczWaysideIncinerator", "Hcz096", "Pocket", "HczAcroamaticAbatement" };

    [Description("Spawn bonus chances (percent) rolled for every human spawn.")]
    public Dictionary<string, float> SpawnBonuses { get; set; } = new()
    {
        ["ExtraAmmo"] = 80f,
        ["Adrenaline"] = 20f,
        ["Scp207"] = 25f,
        ["Scp500"] = 5f,
        ["A7"] = 3f,
        ["E11"] = 3f,
        ["Jailbird"] = 2f,
        ["MicroHid"] = 1f,
        ["Com45"] = 0.5f,
        ["Scp018"] = 0.25f,
        ["OverloadedCom45"] = 0.05f,
    };

    [Description("Remove dropped items and ragdolls older than this many seconds (0 = never).")]
    public float CleanupAfterSeconds { get; set; } = 600f;
}

public sealed class EffectsConfig
{
    public float HerdRange { get; set; } = 8f;
    public float HerdHealPerFriend { get; set; } = 1f;
    public float IrradiatedScpScalar { get; set; } = 0.35f;
    public float TazedSeconds { get; set; } = 3f;

    [Description("Nuke room radiation: seconds you can stay before getting Irradiated, then it keeps stacking.")]
    public float NukeRoomGraceSeconds { get; set; } = 30f;
    public float NukeRoomHumanPercent { get; set; } = 1f;
    public float NukeRoomScpPercent { get; set; } = 0.1f;

    [Description("SCP-1853: crit chance (percent) and crit damage bonus per dose.")]
    public float Scp1853CritChance { get; set; } = 5f;
    public float Scp1853CritDamage { get; set; } = 0.1f;
    public float Scp1853PuppetCritChance { get; set; } = 2.5f;
    public float Scp1853PuppetCritDamage { get; set; } = 0.03f;
}

public sealed class GamblingConfig
{
    public float PainkillerSpeedChance { get; set; } = 90f;
    public float PainkillerSpeedAmount { get; set; } = 5f;
    public float PainkillerSpeedCap { get; set; } = 100f;
    public float PainkillerDeathChance { get; set; } = 10f;

    [Description("Weights for SCP-500 outcomes.")]
    public Dictionary<string, float> Scp500 { get; set; } = new()
    {
        ["ReviveOne"] = 25f,
        ["ReviveAll"] = 4f,
        ["Zombie"] = 6f,
        ["RandomizeInventory"] = 15f,
        ["RandomConsumables"] = 20f,
        ["TeleportGuardsToClassD"] = 6f,
        ["CureZombies"] = 8f,
        ["TeleportToScp"] = 6f,
        ["Nothing"] = 10f,
    };

    [Description("Weights for adrenaline outcomes.")]
    public Dictionary<string, float> Adrenaline { get; set; } = new()
    {
        ["Scp1853"] = 8f, ["Scp207"] = 8f, ["Speed"] = 10f, ["SevenBalls"] = 3f, ["SeveredHands"] = 2f,
        ["CardiacArrest"] = 3f, ["BadAdrenaline"] = 2f, ["DamageReductionAll"] = 7f, ["DamageReductionBody"] = 7f,
        ["NegativeReduction"] = 7f, ["Stained"] = 4f, ["Sinkhole"] = 4f, ["GasGasGas"] = 2f, ["TurboDrunk"] = 5f,
        ["Flash"] = 4f, ["Burned"] = 3f, ["Tazed"] = 3f, ["TeleportSpawn"] = 5f, ["Deafened"] = 3f, ["Confused"] = 3f,
        ["SixGrenades"] = 2f, ["Disabled"] = 3f, ["Herd"] = 5f, ["Irradiated"] = 3f, ["Explode"] = 1f,
    };

    [Description("Item Chances Inversed global event: painkiller death chance.")]
    public float InversedPainkillerDeathChance { get; set; } = 90f;

    public float AddictionaterOverdoseSeconds { get; set; } = 25f;
    public float AddictionaterSecondsPerItem { get; set; } = 5f;
}

public sealed class ScpConfig
{
    [Description("Extra SCP max HP per player in the server.")]
    public float HealthPerPlayer { get; set; } = 200f;

    public float Scp173BlinkFaster { get; set; } = 50f;
    public float Scp173BlinkPerDead { get; set; } = 5f;
    public float Scp173TantrumExplosionRadius { get; set; } = 2f;
    public float Scp173TantrumHealth { get; set; } = 150f;

    public float Scp096DocileSpeed { get; set; } = 20f;
    public float Scp096DistressedSpeed { get; set; } = 5f;
    public float Scp096EnragedSpeed { get; set; } = 10f;
    public float Scp096PostNukeMaxSpeed { get; set; } = 110f;
    public float Scp096AuraRange { get; set; } = 4f;
    public float Scp096DocileAuraDamage { get; set; } = 1f;
    public float Scp096EnragedAuraDamage { get; set; } = 5f;
    public float Scp096PostNukeAuraMultiplier { get; set; } = 3f;
    public int Scp096ChargeExplosions { get; set; } = 5;
    public float Scp096EnragingDamageReduction { get; set; } = 40f;

    public float Scp106EscapeChance { get; set; } = 80f;
    public float Scp106DrPerHealthyPlayer { get; set; } = 1.5f;
    public float Scp106SpeedPerPocketPlayer { get; set; } = 10f;
    public float Scp106SpeedPerTraumatized { get; set; } = 5f;
    public float Scp106AuraBaseRange { get; set; } = 3f;
    public float Scp106AuraRangePerTraumatized { get; set; } = 0.5f;
    public float Scp106AuraDamage { get; set; } = 2f;

    public float Scp939BaseSpeed { get; set; } = 15f;
    public float Scp939PounceSpeed { get; set; } = 90f;
    public float Scp939PounceSpeedSeconds { get; set; } = 8f;
    public float Scp939DamageMultiplier { get; set; } = 1.3f;
    public float Scp939HuntedPerStack { get; set; } = 0.5f;
    public float Scp939HuntedSeconds { get; set; } = 15f;

    public float Scp049BaseSpeed { get; set; } = 30f;
    public float Scp049CardiacDamagePerSecond { get; set; } = 16f;
    public float Scp049ZombieHealOnConvert { get; set; } = 20f;
    public float Scp049HealOnConvert { get; set; } = 50f;
    public float Scp0492SpeedPerZombie { get; set; } = 5f;
    public float Scp049SpeedPerZombie { get; set; } = 3f;

    [Description("SCP-966: black figure that appears behind players and poses, sometimes causing a facility-wide blackout.")]
    public bool Scp966Enabled { get; set; } = true;
    public float Scp966MinInterval { get; set; } = 45f;
    public float Scp966MaxInterval { get; set; } = 110f;
    public float Scp966LingerSeconds { get; set; } = 6f;
    public float Scp966BlackoutChance { get; set; } = 15f;
    public float Scp966BlackoutSeconds { get; set; } = 12f;
}

public sealed class Scp079Config
{
    [Description("Seconds SCP-079 has to choose a Puppet before one is picked automatically.")]
    public float PickSeconds { get; set; } = 90f;
    [Description("Grace period after Puppet is picked: Puppet locked in the armory, generators locked, no damage.")]
    public float GraceSeconds { get; set; } = 90f;

    public float PuppetBaseHealth { get; set; } = 1000f;
    public float PuppetHealthPerTier { get; set; } = 250f;

    [Description("Generators (lives) by player count: 8+ = 2, 14+ = 3, 20+ = 4, 26+ = 5.")]
    public List<int> GeneratorThresholds { get; set; } = new() { 8, 14, 20, 26 };
    public float GeneratorLockAfterDeath { get; set; } = 15f;
    public float DamageTakenPerActiveGenerator { get; set; } = 35f;

    public List<float> AuxRegenPerTier { get; set; } = new() { 2.5f, 4f, 6f, 8.5f, 11f };

    public float PuppetKillXp { get; set; } = 25f;
    public float ScpKillXp { get; set; } = 35f;
    public float IndirectKillXp { get; set; } = 8f;

    public float BulkheadCost { get; set; } = 50f;
    public float GrenadePingCost { get; set; } = 75f;
    public float GrenadePingCooldown { get; set; } = 20f;
    public int GrenadePingCount { get; set; } = 5;
    public float OverchargeCost { get; set; } = 100f;
    public float OverchargeCooldown { get; set; } = 30f;
    public float OverchargeBaseRadius { get; set; } = 6f;
    public float OverchargeRadiusPerTier { get; set; } = 1.5f;

    [Description("Tooth and Nail (Puppet's SCP-127) penetration per tier and hume shield per tier.")]
    public List<float> ToothAndNailPenetration { get; set; } = new() { 50f, 60f, 70f };
    public List<float> ToothAndNailHumeShield { get; set; } = new() { 50f, 100f, 150f };
    public float HumeRegenPerSecond { get; set; } = 10f;
    public float HumeRegenDelay { get; set; } = 12f;
}

public sealed class AscensionConfig
{
    public bool Enabled { get; set; } = true;
    [Description("PCs to find (1 always LCZ, 1 HCZ-or-Entrance, 1 always Entrance).")]
    public int PcCount { get; set; } = 3;
    [Description("Number of digits in the phone's NATO code.")]
    public int CodeLength { get; set; } = 4;
    [Description("Seconds the Puppet has to reach SCP-079's room during the recontainment run (0 = unlimited).")]
    public float RecontainmentTimeLimit { get; set; } = 0f;
    [Description("Keypad placement next to the Nuke Room warhead panel (meters to the side / up).")]
    public float KeypadSideOffset { get; set; } = 1.4f;
    public float KeypadHeightOffset { get; set; } = 0f;
    public float AscendedHealth { get; set; } = 1500f;
    public float AscendedRegenDelay { get; set; } = 4f;
    public float AscendedRegenPerSecond { get; set; } = 60f;
    public float AscendedKillAuraRadius { get; set; } = 15f;
    public float AscendedSpeedCap { get; set; } = 200f;
    public float AscendedCritMultiplierCap { get; set; } = 21f;
    [Description("Red Right Hand loadout.")]
    public List<string> RedRightHandItems { get; set; } = new() { "GunFRMG0", "GunE11SR", "KeycardO5", "Medkit", "Medkit", "Adrenaline", "ArmorHeavy", "Radio" };
}

public sealed class EventsConfig
{
    [Description("Weights for the global event picked each round.")]
    public Dictionary<string, float> GlobalEventWeights { get; set; } = new()
    {
        ["RareItemsCommon"] = 12f,
        ["FiveXLoot"] = 12f,
        ["ItemChancesInversed"] = 10f,
        ["CustomItemsOnDeath"] = 10f,
        ["CausalityCorruption"] = 4f,
        ["AllItems1853"] = 8f,
        ["FiveXCustomLoot"] = 10f,
    };

    [Description("Chance (percent) a sub event also happens this round.")]
    public float SubEventChance { get; set; } = 35f;
    public Dictionary<string, float> SubEventWeights { get; set; } = new()
    {
        ["BeQuiet"] = 1f,
        ["FinallyAwake"] = 1f,
        ["CandyOnDeath"] = 1f,
    };

    [Description("You May Want To Be Quiet: voice loudness (0-1) that makes you explode.")]
    public float BeQuietThreshold { get; set; } = 0.85f;

    [Description("Ultimate Loot Pile special event chance (percent) per round and when it happens.")]
    public float UltimateLootPileChance { get; set; } = 30f;
    public float UltimateLootPileMinTime { get; set; } = 300f;
    public float UltimateLootPileMaxTime { get; set; } = 900f;
    public int UltimateLootPileItems { get; set; } = 25;

    [Description("Minutes into the round when Mayhem Protocol / Mayhem Insurgency auto-starts (slightly randomized).")]
    public float MayhemAutoStartMinutes { get; set; } = 25f;
    public float MayhemProtocolChance { get; set; } = 35f;
    public float MayhemProtocolSeconds { get; set; } = 120f;

    [Description("[REDACTED] Protocol: arm the nuke during this hour (server local time, 0 = midnight).")]
    public bool RedactedProtocolEnabled { get; set; } = true;
    public int RedactedProtocolHour { get; set; } = 0;
    public float RedactedProtocolDelay { get; set; } = 120f;
    public int RedactedProtocolScpCount { get; set; } = 3;

    [Description("Replace the auto-nuke with the Dead Man's Switch (12/16/2024 change).")]
    public bool DeadMansSwitch { get; set; } = true;
}

public sealed class AudioConfig
{
    [Description("Volume for sound effects and music (0-1).")]
    public float Volume { get; set; } = 0.8f;
    public float MusicVolume { get; set; } = 0.6f;
}
