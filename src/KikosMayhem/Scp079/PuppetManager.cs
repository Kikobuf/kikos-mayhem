using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Interactables.Interobjects.DoorUtils;
using KikosMayhem.Core;
using KikosMayhem.Effects;
using KikosMayhem.Items;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp079Events;
using LabApi.Events.Arguments.Scp096Events;
using LabApi.Events.Arguments.Scp173Events;
using LabApi.Events.Handlers;
using LabApi.Features.Enums;
using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079;
using PlayerStatsSystem;
using MapGeneration.Distributors;
using UnityEngine;

namespace KikosMayhem.Scp079;

/// <summary>Puppet's exclusive SCP-127. Can't spawn, can't be dropped. Its tier follows SCP-079's tier.</summary>
public sealed class ToothAndNail : CustomItem
{
    public override string Id => "tooth_and_nail";
    public override string Name => "Tooth and Nail";
    public override Rarity Rarity => Rarity.Admin;
    public override ItemType BaseType => ItemType.GunSCP127;
    public override string Description => "Puppet's SCP-127. Tier follows SCP-079 (T3 → tier 2, T5 → tier 3): more penetration and hume shield.";
    public override bool Hidden => true;
    public override int RegenAmount => 3;
    public override float RegenInterval => 1f;
    public override float RegenDelayAfterShot => 4f;

    public static int Tier
    {
        get
        {
            int t = PuppetManager.Tier;
            return t >= 5 ? 3 : t >= 3 ? 2 : 1;
        }
    }

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev)
    {
        // Penetration approximated as bonus damage against armored humans / SCPs.
        float pen = MayhemPlugin.Cfg.Scp079.ToothAndNailPenetration[Mathf.Clamp(Tier - 1, 0, 2)];
        Combat.MultiplyDamage(ev, 1f + (pen - 50f) / 100f);
    }
}

/// <summary>SCP-079's ping grenades (tier 3): 10% of a grenade's damage × 079's tier.</summary>
public sealed class Scp079Grenade : CustomItem
{
    public override string Id => "scp079_grenade";
    public override string Name => "SCP-079 Grenade";
    public override Rarity Rarity => Rarity.Admin;
    public override ItemType BaseType => ItemType.GrenadeHE;
    public override string Description => "Spawned by SCP-079.";
    public override bool Hidden => true;

    public override void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev) =>
        Combat.MultiplyDamage(ev, 0.1f * PuppetManager.Tier);
}

/// <summary>
/// SCP-079 and its Puppet (latest Mayhem rework): 079 picks a Puppet, generators are lives, tiers unlock
/// bulkhead slams / grenade pings / generator overcharges / nuke, Puppet gets Tooth and Nail, one-shot
/// protection and armor-based speed. Puppet dies with 079.
/// </summary>
public sealed class PuppetManager : MayhemModule
{
    public static PuppetManager Instance { get; private set; }

    private static readonly List<ReferenceHub> Puppets = new();

    public static bool IsPuppet(Player p) => p != null && Puppets.Contains(p.ReferenceHub);

    public static IEnumerable<Player> AllPuppets => Puppets.Select(Player.Get).Where(p => p != null && p.IsAlive);

    public static Player Computer => Player.ReadyList.FirstOrDefault(p => p.Role == RoleTypeId.Scp079);

    public static Player MainPuppet => Puppets.Count > 0 ? Player.Get(Puppets[0]) : null;

    public static int Tier =>
        Computer?.RoleBase is Scp079Role r && r.SubroutineModule.TryGetSubroutine<Scp079TierManager>(out var t) ? t.AccessTierLevel : AscendedTier;

    /// <summary>After ascension 079 is a puppet itself, but Tooth and Nail stays at max tier.</summary>
    public static int AscendedTier;

    // ───── state ─────
    public bool Active;               // 079 is in the round
    public bool Picked;
    public float PickDeadline;
    public float GraceEnds;
    public float GeneratorsLockedUntil;
    /// <summary>Every generator that hasn't been engaged is a life for the Puppet.</summary>
    public int Lives => GeneratorSpawner.All.Count(g => !g.Engaged);
    public int MaxLives => GeneratorSpawner.All.Count();
    private readonly HashSet<Scp079Generator> _legitEngaged = new();
    private bool _soloTriggered;
    public float ComputerStunnedUntil;
    private readonly Dictionary<ReferenceHub, (float until, float startHp)> _itemHealCap = new();
    public bool Ascended;
    public Player LastPuppetKiller;

    private ReferenceHub _computerHub;
    private readonly Dictionary<ReferenceHub, float> _lastDamage = new();
    private readonly Dictionary<Generator, float> _overchargeCd = new();
    private float _grenadeCd;
    private readonly HashSet<string> _puppetIds = new();

    public override void Register()
    {
        Instance = this;
        Scp079Events.Pinging += OnPinging;
        Scp079Events.GainingExperience += OnGainingXp;
        Scp079Events.Recontaining += OnRecontaining;
        PlayerEvents.ChangedRole += OnChangedRole;
        PlayerEvents.Dying += OnDying;
        PlayerEvents.Death += OnDeath;
        PlayerEvents.Left += OnLeft;
        PlayerEvents.InteractingGenerator += OnInteractingGenerator;
        PlayerEvents.ActivatedGenerator += OnGeneratorActivated;
        PlayerEvents.DroppingItem += OnDropping;
        PlayerEvents.UsingItem += OnUsingItem;
        PlayerEvents.EnteringPocketDimension += OnEnteringPocket;
        PlayerEvents.PickingUpItem += OnPickingUp;
        PlayerEvents.TriggeringTesla += OnTesla;
        MayhemSettings.QuickRecyclePressed += QuickRecycle;
        Scp096Events.AddingTarget += On096Target;
        Scp173Events.AddingObserver += On173Observer;
        Combat.Pre.Add(PuppetDamageRules);
        Combat.KillHooks.Add(OnKill);
        PuppetVoice.Register();
        PuppetMapMarker.Register();
        ItemEngine.Define(new ToothAndNail());
        ItemEngine.Define(new Scp079Grenade());
    }

    public override void Unregister()
    {
        Scp079Events.Pinging -= OnPinging;
        Scp079Events.GainingExperience -= OnGainingXp;
        Scp079Events.Recontaining -= OnRecontaining;
        PlayerEvents.ChangedRole -= OnChangedRole;
        PlayerEvents.Dying -= OnDying;
        PlayerEvents.Death -= OnDeath;
        PlayerEvents.Left -= OnLeft;
        PlayerEvents.InteractingGenerator -= OnInteractingGenerator;
        PlayerEvents.ActivatedGenerator -= OnGeneratorActivated;
        PlayerEvents.DroppingItem -= OnDropping;
        PlayerEvents.UsingItem -= OnUsingItem;
        PlayerEvents.EnteringPocketDimension -= OnEnteringPocket;
        PlayerEvents.PickingUpItem -= OnPickingUp;
        PlayerEvents.TriggeringTesla -= OnTesla;
        MayhemSettings.QuickRecyclePressed -= QuickRecycle;
        Scp096Events.AddingTarget -= On096Target;
        Scp173Events.AddingObserver -= On173Observer;
        PuppetVoice.Unregister();
        PuppetMapMarker.Unregister();
        Instance = null;
    }

    public override void Reset()
    {
        Puppets.Clear();
        Active = Picked = Ascended = false;
        PickDeadline = GraceEnds = GeneratorsLockedUntil = 0;
        _legitEngaged.Clear();
        _soloTriggered = false;
        ComputerStunnedUntil = 0;
        _itemHealCap.Clear();
        AscendedTier = 0;
        LastPuppetKiller = null;
        _computerHub = null;
        _lastDamage.Clear();
        _overchargeCd.Clear();
        _grenadeCd = 0;
        _puppetIds.Clear();
    }

    public override void RoundStarted() => Loops.After(2f, CheckFor079);

    private void CheckFor079()
    {
        var pc = Computer;
        if (pc == null)
        {
            ItemSpawner.TrySpawn079Pile();
            return;
        }
        Setup079(pc);
    }

    private void Setup079(Player pc)
    {
        if (Active) return;
        Active = true;
        _computerHub = pc.ReferenceHub;
        var cfg = MayhemPlugin.Cfg.Scp079;
        int players = Player.ReadyList.Count(p => !p.IsHost && !Items.Defs.Decoys.IsDecoy(p));
        GeneratorSpawner.Adjust(GeneratorSpawner.TargetFor(players));
        PickDeadline = Time.time + cfg.PickSeconds;

        if (pc.RoleBase is Scp079Role role && role.SubroutineModule.TryGetSubroutine<Scp079AuxManager>(out var aux))
            aux._regenerationPerTier = cfg.AuxRegenPerTier.ToArray();

        pc.SendBroadcast("<size=30><color=#FF4040>You are Mayhem SCP-079</color></size>\n<size=22>Pick your <b>Puppet</b>: ping a player (E) or open the console (`) and type <b>.puppet</b></size>", 12);
        Hud.Message(pc, $"You have {cfg.PickSeconds:0}s to choose a Puppet. Lives (generators): {Lives}", 10f);
    }

    // ───────── picking ─────────

    public List<Player> Candidates() =>
        CandidatesRaw().Where(p => !MayhemSettings.OptedOutOfPuppet(p)).ToList() is { Count: > 0 } willing ? willing : CandidatesRaw();

    private List<Player> CandidatesRaw() =>
        Player.ReadyList.Where(p => !p.IsHost && !p.IsSCP && !IsPuppet(p) && p.Role != RoleTypeId.Overwatch).OrderBy(p => p.PlayerId).ToList();

    public string Pick(Player target)
    {
        if (!Active) return "SCP-079 isn't in this round.";
        if (Picked) return "You already have a Puppet.";
        if (target == null || target.IsSCP || target.IsHost) return "That player can't be your Puppet.";
        if (MayhemSettings.OptedOutOfPuppet(target) && Candidates().Any(c => c != target && !MayhemSettings.OptedOutOfPuppet(c)))
            return $"{target.Nickname} opted out of being Puppet. Pick someone else.";
        Picked = true;
        GraceEnds = Time.time + MayhemPlugin.Cfg.Scp079.GraceSeconds;
        GeneratorsLockedUntil = GraceEnds;
        Puppets.Add(target.ReferenceHub);
        _puppetIds.Add(target.UserId);
        SpawnPuppet(target, grace: true);
        CassieW.Say("pitch_0.9 .g3 SCP 0 7 9 has acquired a host", "SCP-079 has acquired a host.");
        return $"{target.Nickname} is now your Puppet.";
    }

    public void SpawnPuppet(Player p, bool grace, bool ascendedForm = false, float stun = 0f)
    {
        var cfg = MayhemPlugin.Cfg.Scp079;
        if (!Puppets.Contains(p.ReferenceHub)) Puppets.Add(p.ReferenceHub);
        p.SetRole(RoleTypeId.Tutorial, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.None);
        Loops.After(0.3f, () =>
        {
            if (!p.Valid() || !IsPuppet(p)) return;
            Vector3? spot;
            if (grace)
            {
                spot = ItemSpawner.SpotIn(Room.Get(RoomName.LczArmory).FirstOrDefault());
                var door = Door.Get(DoorName.LczArmory);
                if (door != null)
                {
                    door.IsOpened = false;
                    door.Lock(DoorLockReason.AdminCommand, true);
                    Loops.After(cfg.GraceSeconds, () => { if (!door.IsDestroyed) door.Lock(DoorLockReason.AdminCommand, false); });
                }
            }
            else spot = ItemSpawner.SpotIn(ItemSpawner.Scp079Room);
            if (spot != null) p.Position = spot.Value + Vector3.up * 0.3f;

            p.ClearInventory();
            p.AddItem(ItemType.ArmorCombat);
            p.AddItem(ItemType.KeycardO5);
            ItemEngine.Give(p, "tooth_and_nail");
            p.AddAmmo(ItemType.Ammo556x45, 200);

            float hp = ascendedForm ? MayhemPlugin.Cfg.Ascension.AscendedHealth : cfg.PuppetBaseHealth;
            if (stun > 0)
            {
                p.EnableEffect<Ensnared>(1, stun);
                p.EnableEffect<Concussed>(1, stun);
                EffectManager.Apply<Invincible>(p, stun);
            }
            p.MaxHealth = hp;
            p.Health = hp;
            p.CustomInfo = ascendedForm ? "<color=#00FFFF>ASCENDED SCP-079</color>" : "<color=#FF4040>SCP-079's Puppet</color>";
            p.MaxHumeShield = HumeMax;
            p.HumeShield = HumeMax;
            if ((grace || ascendedForm) && MayhemPlugin.Cfg.Scps.ShowPowersPopup)
                Hud.Popup(p, Scps.ScpPowers.PuppetText(ascendedForm), MayhemPlugin.Cfg.Scps.PowersPopupSeconds);
            Hud.Message(p, ascendedForm
                ? "<color=#00FFFF>You have ascended. Kill everyone, or reach the Gate B exit.</color>"
                : "<color=#FF4040>You are SCP-079's Puppet. Protect the computer — you die with it.</color>\n<size=20>SCP-079 can hear you anywhere, and you can hear it.</size>", 8f);
        });
    }

    private static float HumeMax => MayhemPlugin.Cfg.Scp079.ToothAndNailHumeShield[Mathf.Clamp(ToothAndNail.Tier - 1, 0, 2)];

    // ───────── pings ─────────

    private void OnPinging(Scp079PingingEventArgs ev)
    {
        var pc = ev.Player;
        var pos = ev.Position;
        var cfg = MayhemPlugin.Cfg.Scp079;

        if (!Active) Setup079(pc);
        if (Time.time < ComputerStunnedUntil)
        {
            ev.IsAllowed = false;
            Hud.Message(pc, $"<color=#FF7070>Systems rebooting ({ComputerStunnedUntil - Time.time:0}s)</color>", 1.5f, "stun");
            return;
        }

        // Pick a puppet by pinging a player.
        if (!Picked)
        {
            var target = PlayerExt.Alive.Where(p => !p.IsSCP).OrderBy(p => (p.Position - pos).sqrMagnitude).FirstOrDefault();
            if (target != null && (target.Position - pos).sqrMagnitude < 9f)
                Hud.Message(pc, Pick(target), 4f);
            return;
        }

        if (!TryAux(pc, out var aux)) return;
        int tier = Tier;

        // Heal the puppet: 1 AP = 1 HP.
        var puppet = AllPuppets.FirstOrDefault(p => (p.Position - pos).sqrMagnitude < 6.25f);
        if (puppet != null && !Ascended)
        {
            if (puppet.HasEffect<Decontaminating>() && puppet.GetEffect<Decontaminating>().IsEnabled)
            {
                Hud.Message(pc, "Can't heal the Puppet during decontamination", 2f);
                return;
            }
            float ratio = cfg.PingHealPerAux;
            float missing = puppet.MaxHealth - puppet.Health;
            float heal = Mathf.Min(missing, aux.CurrentAux * ratio);
            if (heal <= 0) return;
            aux.CurrentAux -= heal / ratio;
            puppet.Heal(heal);
            Hud.Message(pc, $"<color=#88FF88>Healed Puppet +{heal:0}</color>", 1.5f);
            return;
        }

        switch (ev.PingType)
        {
            case Scp079PingType.MicroHid:
            {
                var holder = PlayerExt.Alive.Where(p => p.CurrentItem?.Type == ItemType.MicroHID)
                    .OrderBy(p => (p.Position - pos).sqrMagnitude).FirstOrDefault();
                if (holder != null && (holder.Position - pos).sqrMagnitude < 9f)
                {
                    holder.Explode();
                    holder.Kill("Overloaded by SCP-079.");
                }
                return;
            }
            case Scp079PingType.Door when tier >= 2:
            {
                var bulkhead = BulkheadDoor.List.Where(d => !d.IsDestroyed).OrderBy(d => (d.Position - pos).sqrMagnitude).FirstOrDefault();
                if (bulkhead == null || (bulkhead.Position - pos).sqrMagnitude > 16f || !bulkhead.IsOpened) return;
                if (!Spend(pc, aux, cfg.BulkheadCost)) return;
                bulkhead.IsOpened = false;
                Loops.After(0.6f, () =>
                {
                    foreach (var o in PlayerExt.Near(bulkhead.Position, 1.6f).Where(o => o.IsEnemyOf(pc) || o.IsHuman).ToList())
                    {
                        if (IsPuppet(o)) continue;
                        o.Kill("Crushed by a bulkhead door.");
                        GrantXp(pc, cfg.ScpKillXp);
                    }
                });
                return;
            }
            case Scp079PingType.Generator when tier >= 4:
            {
                var gen = Map.Generators.OrderBy(g => (g.Base.transform.position - pos).sqrMagnitude).FirstOrDefault();
                if (gen == null || (gen.Base.transform.position - pos).sqrMagnitude > 16f) return;
                if (_overchargeCd.TryGetValue(gen, out var cd) && cd > Time.time)
                {
                    Hud.Message(pc, $"Generator cooling down ({cd - Time.time:0}s)", 1.5f);
                    return;
                }
                if (!Spend(pc, aux, cfg.OverchargeCost)) return;
                _overchargeCd[gen] = Time.time + cfg.OverchargeCooldown;
                Overcharge(pc, gen, tier);
                return;
            }
            case Scp079PingType.Human when tier >= 3:
            {
                var enemy = PlayerExt.Alive.Where(p => p.IsEnemyOf(pc) && !IsPuppet(p))
                    .OrderBy(p => (p.Position - pos).sqrMagnitude).FirstOrDefault();
                if (enemy == null || (enemy.Position - pos).sqrMagnitude > 9f) return;
                if (_grenadeCd > Time.time)
                {
                    Hud.Message(pc, $"Grenades cooling down ({_grenadeCd - Time.time:0}s)", 1.5f);
                    return;
                }
                if (!Spend(pc, aux, cfg.GrenadePingCost)) return;
                _grenadeCd = Time.time + cfg.GrenadePingCooldown;
                double fuse = Mathf.Max(1f, 4.5f - (tier - 3));
                ItemEngine.TryGetDef("scp079_grenade", out var gdef);
                for (int i = 0; i < cfg.GrenadePingCount; i++)
                {
                    var g = TimedGrenadeProjectile.SpawnActive(enemy.Position + Random.insideUnitSphere * 1.5f + Vector3.up, ItemType.GrenadeHE, pc, fuse);
                    if (g != null && gdef != null) ItemEngine.Register(g.Serial, gdef);
                }
                return;
            }
        }

        // Tier 5: ping the surface nuke controller to start the nuke.
        if (tier >= 5 && Warhead.BaseOutsidePanel != null && !Warhead.IsDetonated && !Warhead.IsDetonationInProgress
            && (Warhead.BaseOutsidePanel.transform.position - pos).sqrMagnitude < 16f)
        {
            Warhead.Start(false, false, pc);
            Hud.Message(pc, "<color=#FF4040>Alpha Warhead started</color>", 3f);
        }
    }

    private static bool TryAux(Player pc, out Scp079AuxManager aux)
    {
        aux = null;
        return pc.RoleBase is Scp079Role r && r.SubroutineModule.TryGetSubroutine(out aux);
    }

    private static bool Spend(Player pc, Scp079AuxManager aux, float cost)
    {
        if (aux.CurrentAux < cost)
        {
            Hud.Message(pc, $"<color=#FF7070>Not enough power ({cost:0} needed)</color>", 1.5f);
            return false;
        }
        aux.CurrentAux -= cost;
        return true;
    }

    private void Overcharge(Player pc, Generator gen, int tier)
    {
        var cfg = MayhemPlugin.Cfg.Scp079;
        var pos = gen.Base.transform.position;
        var room = Room.GetRoomAtPosition(pos);
        room?.LightController?.FlickerLights(2f);
        Loops.After(2f, () =>
        {
            float radius = cfg.OverchargeBaseRadius + cfg.OverchargeRadiusPerTier * tier;
            TimedGrenadeProjectile.PlayEffect(pos, ItemType.GrenadeHE);
            foreach (var o in PlayerExt.Near(pos, radius).Where(o => o.IsEnemyOf(pc) && !IsPuppet(o)).ToList())
            {
                if (room != null && o.Room != room) continue;
                float d = Vector3.Distance(o.Position, pos);
                if (d < radius * 0.5f)
                {
                    o.Kill("Electrocuted by an overcharged generator.");
                    GrantXp(pc, cfg.ScpKillXp);
                }
                else
                {
                    o.EnableEffect<Ensnared>(1, 2.5f);
                    o.EnableEffect<Concussed>(1, 5f);
                }
            }
        });
    }

    // ───────── XP ─────────

    private static void OnGainingXp(Scp079GainingExperienceEventArgs ev)
    {
        // Mayhem replaces kill XP with its own (no buffered XP).
        if (ev.Reason is Scp079HudTranslation.ExpGainTerminationDirect or Scp079HudTranslation.ExpGainTerminationAssist or Scp079HudTranslation.ExpGainTerminationWitness)
            ev.IsAllowed = false;
    }

    private static void GrantXp(Player pc, float amount)
    {
        if (pc?.RoleBase is Scp079Role r && r.SubroutineModule.TryGetSubroutine<Scp079TierManager>(out var t))
            t.ServerGrantExperience((int)amount, Scp079HudTranslation.ExpGainTerminationDirect);
    }

    private void OnKill(Player killer, Player victim, PlayerDeathEventArgs ev)
    {
        var pc = Computer;
        if (pc == null || IsPuppet(victim)) return;
        var cfg = MayhemPlugin.Cfg.Scp079;
        if (IsPuppet(killer)) GrantXp(pc, cfg.PuppetKillXp);
        else if (killer == pc) GrantXp(pc, cfg.ScpKillXp);
        else if (killer.IsSCP) GrantXp(pc, cfg.IndirectKillXp);
    }

    // ───────── damage rules ─────────

    private void PuppetDamageRules(PlayerHurtingEventArgs ev, Player attacker, Player target)
    {
        bool targetPuppet = IsPuppet(target);
        bool attackerPuppet = attacker != null && IsPuppet(attacker);

        // Puppet and SCPs are one side.
        if (attacker != null && attacker != target && (targetPuppet || attackerPuppet) && !attacker.IsEnemyOf(target))
        {
            ev.IsAllowed = false;
            return;
        }
        // Mayhem enemies that the game sees as teammates (M.Y.H.M Insurgents vs the Puppet are both Tutorial):
        // force full damage so the vanilla friendly-fire reduction (and teamkill detector) don't apply.
        if (attacker != null && attacker != target && attacker.IsEnemyOf(target) && ev.DamageHandler is AttackerDamageHandler adh
            && !HitboxIdentity.IsEnemy(attacker.ReferenceHub, target.ReferenceHub))
            adh.ForceFullFriendlyFire = true;
        // Grace: Puppet can't deal damage.
        if (attackerPuppet && Time.time < GraceEnds)
        {
            ev.IsAllowed = false;
            return;
        }
        if (!targetPuppet) return;

        _lastDamage[target.ReferenceHub] = Time.time;

        // One-shot protection: while hume shield is up, health can't be damaged at all.
        // We absorb ourselves (some handlers, e.g. the jailbird, skip vanilla hume shield).
        float hs = target.HumeShield;
        if (hs > 0)
        {
            float dmg = Combat.GetDamage(ev);
            Combat.SetDamage(ev, 0f);
            float left = hs - dmg;
            target.HumeShield = Mathf.Max(0f, left);
            if (left <= 0f)
            {
                target.EnableEffect<Disabled>(1, 2f);
                EffectManager.Apply<Invincible>(target, 0.3f);
                Hud.Message(target, "<color=#88CCFF>Shield broken!</color>", 1.5f, "hsbreak");
            }
        }
    }

    private void On096Target(Scp096AddingTargetEventArgs ev)
    {
        if (IsPuppet(ev.Target)) ev.IsAllowed = false;
    }

    private void On173Observer(Scp173AddingObserverEventArgs ev)
    {
        if (IsPuppet(ev.Target)) ev.IsAllowed = false;
    }

    private void OnEnteringPocket(PlayerEnteringPocketDimensionEventArgs ev)
    {
        if (IsPuppet(ev.Player)) ev.IsAllowed = false;
    }

    private void OnUsingItem(PlayerUsingItemEventArgs ev)
    {
        if (!IsPuppet(ev.Player) || ItemEngine.IsCustom(ev.UsableItem.Serial)) return;
        if (ev.UsableItem.Type is ItemType.Medkit or ItemType.SCP500 or ItemType.Painkillers or ItemType.Adrenaline)
        {
            var p = ev.Player;
            float cap = p.MaxHealth * MayhemPlugin.Cfg.Scp079.PuppetSelfHealPercent / 100f;
            if (p.HasEffect<Decontaminating>() && p.GetEffect<Decontaminating>().IsEnabled)
            {
                ev.IsAllowed = false;
                Hud.Message(p, "Can't heal while decontaminating", 2f);
                return;
            }
            if (p.Health >= cap)
            {
                ev.IsAllowed = false;
                Hud.Message(p, $"Puppet can only heal itself up to {cap:0} HP — SCP-079 can heal you further", 2f);
                return;
            }
            _itemHealCap[p.ReferenceHub] = (Time.time + 20f, p.Health);
        }
    }

    private void OnDropping(PlayerDroppingItemEventArgs ev)
    {
        if (ItemEngine.Get(ev.Item) is ToothAndNail) ev.IsAllowed = false;
    }

    // ───────── generators (lives) ─────────

    private void OnInteractingGenerator(PlayerInteractingGeneratorEventArgs ev)
    {
        if (!Active || ev.Player.IsSCP || IsPuppet(ev.Player)) return;
        if (Time.time < GeneratorsLockedUntil)
        {
            ev.IsAllowed = false;
            Hud.Message(ev.Player, $"Generators are locked ({GeneratorsLockedUntil - Time.time:0}s)", 1.5f);
        }
    }

    private void OnGeneratorActivated(PlayerActivatedGeneratorEventArgs ev)
    {
        if (!Active || Ascended) return;
        OnGeneratorActivatedLegit(ev.Generator);
        if (Lives <= 0 && _soloTriggered) RecontainComputer(ev.Player);
    }

    /// <summary>A generator a human finished is legitimately engaged.</summary>
    private void OnGeneratorActivatedLegit(Generator gen)
    {
        if (gen?.Base != null) _legitEngaged.Add(gen.Base);
    }

    /// <summary>Puppet died with lives left: engage a random generator, shut off activating ones, lock them for a while.</summary>
    private void SpendLife(Player killer)
    {
        var gen = GeneratorSpawner.All.Where(g => !g.Engaged).OrderBy(_ => Rng.Value).FirstOrDefault();
        if (gen != null)
        {
            _legitEngaged.Add(gen);
            gen.Engaged = true;
        }
        foreach (var g in GeneratorSpawner.All.Where(g => g.Activating && !g.Engaged)) g.Activating = false;
        GeneratorsLockedUntil = Time.time + MayhemPlugin.Cfg.Scp079.GeneratorLockAfterDeath;
        if (Lives <= 0 && _soloTriggered) RecontainComputer(killer);
    }

    private bool _killingComputer;

    private void OnRecontaining(Scp079RecontainingEventArgs ev)
    {
        // Vanilla's "last SCP died" auto-recontainment doesn't count while the Puppet is alive with lives left.
        if (Active && !_killingComputer && AllPuppets.Any() && _legitEngaged.Count < MaxLives)
        {
            ev.IsAllowed = false;
            _soloTriggered = true;
            RestoreGenerators();
        }
    }

    private void RestoreGenerators()
    {
        foreach (var g in GeneratorSpawner.All.Where(g => g.Engaged && !_legitEngaged.Contains(g)).ToList()) g.Engaged = false;
    }

    private void RecontainComputer(Player killer)
    {
        var pc = Computer;
        if (pc == null) return;
        _killingComputer = true;
        string by = killer != null ? killer.Nickname : "unknown";
        CassieW.Say("SCP 0 7 9 successfully terminated", $"SCP-079 successfully terminated by {by}.");
        foreach (var g in Map.Generators) g.Engaged = true;
        pc.Kill("Recontained.");
        _killingComputer = false;
    }

    // ───────── deaths ─────────

    private void OnDying(PlayerDyingEventArgs ev)
    {
        if (!IsPuppet(ev.Player)) return;
        foreach (var item in ev.Player.Items.Where(i => ItemEngine.Get(i) is ToothAndNail).ToList())
        {
            ItemEngine.Unregister(item.Serial);
            ev.Player.RemoveItem(item);
        }
    }

    private void OnDeath(PlayerDeathEventArgs ev)
    {
        var p = ev.Player;
        if (ev.OldRole == RoleTypeId.Scp079 && Active)
        {
            // Computer is gone: Puppet(s) die with it.
            foreach (var puppet in AllPuppets.ToList()) puppet.Kill("SCP-079 was terminated.");
            Puppets.Clear();
            Active = false;
            ItemSpawner.TrySpawn079Pile(force: true);
            return;
        }
        if (!IsPuppet(p)) return;
        LastPuppetKiller = ev.Attacker;
        p.CustomInfo = string.Empty;

        if (Ascended)
        {
            Puppets.Remove(p.ReferenceHub);
            if (!AllPuppets.Any()) KikosMayhem.Ascension.AscensionQuest.Instance?.OnAllPuppetsDead();
            return;
        }

        // The facility is gone after a detonation: no respawns underground.
        if (Lives > 0 && Computer != null && !Warhead.IsDetonated)
        {
            SpendLife(ev.Attacker);
            var cfg = MayhemPlugin.Cfg.Scp079;
            ComputerStunnedUntil = Time.time + cfg.RespawnStunSeconds;
            CassieW.Say("pitch_0.9 .g2 SCP 0 7 9 host terminated . generator engaged", $"SCP-079's Puppet was terminated by {ev.Attacker?.Nickname ?? "unknown"}. Generator engaged.");
            Loops.After(0.5f, () =>
            {
                if (p.Valid() && !p.IsAlive && Active && !Warhead.IsDetonated && Computer != null) SpawnPuppet(p, grace: false, stun: cfg.RespawnStunSeconds);
            });
        }
        else
        {
            Puppets.Remove(p.ReferenceHub);
            CassieW.Say("pitch_0.9 .g2 SCP 0 7 9 host terminated", $"SCP-079's Puppet was terminated by {ev.Attacker?.Nickname ?? "unknown"}.");
        }
    }

    private void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        // A puppet being turned into something else by an admin stops being a puppet.
        if (IsPuppet(ev.Player) && ev.NewRole.RoleTypeId != RoleTypeId.Tutorial && ev.NewRole.RoleTypeId != RoleTypeId.Spectator)
            Puppets.Remove(ev.Player.ReferenceHub);
        if (ev.NewRole.RoleTypeId == RoleTypeId.Scp079 && Round.IsRoundStarted && !Active)
            Loops.After(1f, () => { if (ev.Player.Valid() && ev.Player.Role == RoleTypeId.Scp079) Setup079(ev.Player); });
    }

    private void OnLeft(PlayerLeftEventArgs ev) => Puppets.Remove(ev.Player.ReferenceHub);

    // ───────── ticking ─────────

    public override void SlowTick()
    {
        var cfg = MayhemPlugin.Cfg.Scp079;
        var pc = Computer;

        if (Active && !Picked && pc != null)
        {
            float left = PickDeadline - Time.time;
            Hud.SetInfo(pc, "pick", $"<color=#FF4040>Choose a Puppet: {Mathf.Max(0, left):0}s</color> — ping a player or type <b>.puppet</b> in the console", 1f, 1);
            if (left <= 0)
            {
                var auto = Rng.Pick(Candidates().Where(c => c.IsAlive).ToList()) ?? Rng.Pick(Candidates());
                if (auto != null) Hud.Message(pc, Pick(auto) + " (auto-picked)", 4f);
            }
        }

        foreach (var puppet in AllPuppets.ToList())
        {
            var s = Stats.Of(puppet);
            // Armor: less defense, more speed.
            var armor = puppet.Items.FirstOrDefault(i => i.Category == ItemCategory.Armor);
            float speed = armor?.Type switch
            {
                ItemType.ArmorHeavy => 0f,
                ItemType.ArmorCombat => 10f,
                _ => 20f,
            };
            s.SetSpeed("puppet_armor", speed);
            if (armor == null || armor.Type == ItemType.ArmorLight) puppet.StaminaRemaining = 1f;

            // Hume shield from Tooth and Nail: 10/s after 12s without damage.
            float max = HumeMax;
            if (puppet.MaxHumeShield != max) puppet.MaxHumeShield = max;
            float last = _lastDamage.TryGetValue(puppet.ReferenceHub, out var t) ? t : 0;
            if (Time.time - last > cfg.HumeRegenDelay && puppet.HumeShield < max)
                puppet.HumeShield = Mathf.Min(max, puppet.HumeShield + cfg.HumeRegenPerSecond * 0.5f);

            // Healing items can only bring the Puppet up to 50% of max HP.
            if (_itemHealCap.TryGetValue(puppet.ReferenceHub, out var capInfo))
            {
                if (Time.time > capInfo.until) _itemHealCap.Remove(puppet.ReferenceHub);
                else
                {
                    float cap = Mathf.Max(capInfo.startHp, puppet.MaxHealth * cfg.PuppetSelfHealPercent / 100f);
                    if (puppet.Health > cap) puppet.Health = cap;
                }
            }

            // Ascended form regenerates quickly after a few seconds without damage.
            if (Ascended && puppet.ReferenceHub == _computerHub)
            {
                var acfg = MayhemPlugin.Cfg.Ascension;
                if (Time.time - last > acfg.AscendedRegenDelay && puppet.Health < puppet.MaxHealth)
                    puppet.Health = Mathf.Min(puppet.MaxHealth, puppet.Health + acfg.AscendedRegenPerSecond * 0.5f);
            }

            // HUD for the puppet.
            string grace = Time.time < GraceEnds ? $" · <color=#FFD700>Grace {GraceEnds - Time.time:0}s</color>" : "";
            Hud.SetInfo(puppet, "puppet", Ascended
                ? "<color=#00FFFF>ASCENDED</color> · Kill everyone or reach the Gate B exit"
                : $"<color=#FF4040>SCP-079</color> T{Tier} · Lives {Lives}/{MaxLives} · T&N tier {ToothAndNail.Tier}{grace}", 1f, 1);
            if (!Ascended) Hud.SetTopLeft(puppet, "gens", GeneratorPanel(), 1f);
        }

        // Vanilla engages every generator when the last other SCP dies — undo that while the Puppet lives.
        if (Active && Picked && !Ascended && AllPuppets.Any() && MaxLives > 0 && Lives == 0 && _legitEngaged.Count < MaxLives)
        {
            _soloTriggered = true;
            RestoreGenerators();
        }

        // HUD for 079.
        if (pc != null && Picked)
        {
            var main = AllPuppets.FirstOrDefault();
            string text = main == null
                ? $"<color=#FF7070>Puppet is down</color> · Lives {Lives}/{MaxLives}"
                : $"Puppet <color=#FF4040>{main.Health:0}</color>/{main.MaxHealth:0} HP · <color=#88CCFF>{main.HumeShield:0} HS</color> · {main.Room?.Name.ToString() ?? "?"} · Lives {Lives}/{MaxLives}";
            Hud.SetInfo(pc, "pc", text, 1f, 1);
        }
    }

    /// <summary>
    /// The Puppet's top-left panel: how many generators (lives) are left, and where any generator is being
    /// turned on right now, with its countdown.
    /// </summary>
    private string GeneratorPanel()
    {
        var gens = GeneratorSpawner.All.ToList();
        int left = gens.Count(g => !g.Engaged);
        var sb = new System.Text.StringBuilder();
        string boxes = string.Concat(gens.OrderBy(g => g.Engaged).Select(g => g.Engaged ? "<color=#555555>■</color>" : "<color=#FF4040>■</color>"));
        sb.Append($"<b>GENERATORS</b> {boxes} <color=#FF4040>{left}</color>/{gens.Count} left");
        foreach (var g in gens.Where(g => g.Activating && !g.Engaged))
        {
            string where = g.transform.position.TryGetRoom(out var room) ? PrettyRoom(room.Name) : "unknown room";
            sb.Append($"\n<color=#FFD700>⚠ ON: {where} ({Mathf.Max(0, g.RemainingTime)}s)</color>");
        }
        return sb.ToString();
    }

    /// <summary>"HczServers" -> "HCZ Servers".</summary>
    public static string PrettyRoom(RoomName name)
    {
        string s = name.ToString();
        foreach (var (prefix, label) in new[] { ("Hcz", "HCZ "), ("Lcz", "LCZ "), ("Ez", "EZ ") })
            if (s.StartsWith(prefix)) { s = label + s.Substring(prefix.Length); break; }
        return System.Text.RegularExpressions.Regex.Replace(s, "(?<=[a-z0-9])(?=[A-Z])", " ").Trim();
    }

    // ───────── ascension hooks ─────────

    /// <summary>SCP-079 becomes a second Puppet (Ascension).</summary>
    public void Ascend()
    {
        var pc = Computer;
        Ascended = true;
        AscendedTier = 5;
        if (pc == null) return;
        _computerHub = pc.ReferenceHub;
        SpawnPuppet(pc, grace: false, ascendedForm: true);
        // Both Puppets get 10s of spawn protection on ascension.
        Loops.After(0.5f, () =>
        {
            foreach (var puppet in AllPuppets) EffectManager.Apply<Invincible>(puppet, MayhemPlugin.Cfg.Ascension.SpawnProtectionSeconds);
        });
    }

    // ───────── pickups / recycling ─────────

    private static bool AllowedForPuppet(Pickup pickup)
    {
        if (pickup.Category is ItemCategory.Armor or ItemCategory.Medical or ItemCategory.Grenade or ItemCategory.Keycard
            or ItemCategory.Radio or ItemCategory.Ammo) return true;
        return pickup.Type is ItemType.Coin or ItemType.Flashlight or ItemType.SCP018 or ItemType.SCP244a or ItemType.SCP244b
            or ItemType.Lantern or ItemType.SCP500;
    }

    private void OnPickingUp(PlayerPickingUpItemEventArgs ev)
    {
        if (!IsPuppet(ev.Player) || ev.Pickup == null) return;
        if (ItemEngine.Get(ev.Pickup.Serial) is { } def && def is not ToothAndNail)
        {
            // Puppet can't use custom items: they're sent to the Pocket Dimension instead.
            ev.IsAllowed = false;
            Recycling.SendToPocket(ev.Pickup);
            Hud.Message(ev.Player, $"{def.Name} was sent to the Pocket Dimension", 2f);
            return;
        }
        if (!AllowedForPuppet(ev.Pickup))
        {
            ev.IsAllowed = false;
            Hud.Message(ev.Player, "Puppet can't pick that up", 1.5f, "nopickup");
        }
    }

    /// <summary>Quick Recycle keybind: sends the closest custom item (within 2.5m) to the Pocket Dimension.</summary>
    private void QuickRecycle(Player p)
    {
        if (!IsPuppet(p) || !p.IsAlive) return;
        float r = MayhemPlugin.Cfg.Scp079.QuickRecycleRange;
        var closest = Pickup.List.Where(x => !x.IsDestroyed && x is not Projectile && ItemEngine.IsCustom(x.Serial)
                                             && (x.Position - p.Position).sqrMagnitude <= r * r)
            .OrderBy(x => (x.Position - p.Position).sqrMagnitude).FirstOrDefault();
        if (closest == null) { Hud.Message(p, "No custom item in range", 1.2f, "recycle"); return; }
        Recycling.SendToPocket(closest);
        Hud.Message(p, "Recycled", 1.2f, "recycle");
    }

    private void OnTesla(PlayerTriggeringTeslaEventArgs ev)
    {
        if (IsPuppet(ev.Player)) ev.IsAllowed = false;
    }

    /// <summary>Puppets that can be teleported by items (not during the recontainment run).</summary>
    public static bool NoTeleport(Player p) => IsPuppet(p) && KikosMayhem.Ascension.AscensionQuest.Instance?.RecontainmentRun == true;
}
