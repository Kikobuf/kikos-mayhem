using System;
using System.Collections.Generic;
using System.Linq;
using KikosMayhem.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp914Events;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using PlayerStatsSystem;
using Scp914;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace KikosMayhem.Items;

public static class ItemEngine
{
    private static readonly Dictionary<string, CustomItem> Defs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<ushort, CustomItem> Serials = new();
    private static readonly Dictionary<ushort, LightSourceToy> Auras = new();
    private static readonly Dictionary<ReferenceHub, HashSet<ushort>> LastInventory = new();
    private static readonly Dictionary<(ushort, string), float> Cooldowns = new();
    private static readonly Dictionary<ushort, float> RegenClock = new();
    private static readonly HashSet<ushort> AmmoInitialized = new();

    /// <summary>Custom item that owns the explosion currently being processed (set between Exploding/Exploded).</summary>
    public static CustomItem CurrentExplosion { get; private set; }
    public static Player CurrentExplosionOwner { get; private set; }

    /// <summary>Firearm shot bookkeeping (to know if a shot hit someone).</summary>
    private static readonly Dictionary<ushort, Player> ShotHits = new();

    public static IEnumerable<CustomItem> All => Defs.Values;

    public static bool TryGetDef(string id, out CustomItem def) => Defs.TryGetValue(id, out def);

    public static void Define(CustomItem item) => Defs[item.Id] = item;

    public static bool IsDisabled(CustomItem def) =>
        MayhemPlugin.Cfg.Items.Disabled.Any(d => string.Equals(d, def.Id, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<CustomItem> Spawnable =>
        Defs.Values.Where(d => d.NaturalSpawn && !IsDisabled(d));

    public static CustomItem Get(ushort serial) => Serials.TryGetValue(serial, out var d) ? d : null;

    public static CustomItem Get(Item item) => item == null ? null : Get(item.Serial);

    public static bool Is<T>(Item item) where T : CustomItem => Get(item) is T;

    public static bool IsCustom(ushort serial) => Serials.ContainsKey(serial);

    public static IEnumerable<(Item item, CustomItem def)> InventoryOf(Player p) =>
        p.Items.Select(i => (i, Get(i))).Where(x => x.Item2 != null);

    public static bool Has<T>(Player p) where T : CustomItem => p.Items.Any(i => Get(i) is T);

    // ───────── creation ─────────

    public static Item Give(Player p, string id)
    {
        if (!Defs.TryGetValue(id, out var def)) return null;
        return Give(p, def);
    }

    public static Item Give(Player p, CustomItem def)
    {
        if (p.IsInventoryFull)
        {
            var pickup = Spawn(def, p.Position + Vector3.up * 0.5f);
            Hud.Message(p, $"Inventory full — {def.Name} dropped at your feet", 2f);
            return null;
        }
        var item = p.AddItem(def.BaseType, InventorySystem.Items.ItemAddReason.AdminCommand);
        if (item == null) return null;
        Register(item.Serial, def);
        InitAmmo(item, def);
        def.OnAcquired(p, item);
        return item;
    }

    public static Pickup Spawn(string id, Vector3 position) =>
        Defs.TryGetValue(id, out var def) ? Spawn(def, position) : null;

    public static Pickup Spawn(CustomItem def, Vector3 position)
    {
        var pickup = Pickup.Create(def.BaseType, position, Quaternion.Euler(0, Rng.Range(0f, 360f), 0));
        if (pickup == null) return null;
        Register(pickup.Serial, def);
        AddAura(pickup, def);
        return pickup;
    }

    /// <summary>Marks an existing serial as a custom item.</summary>
    public static void Register(ushort serial, CustomItem def)
    {
        Serials[serial] = def;
        def.OnCreated(serial);
    }

    public static void Unregister(ushort serial)
    {
        Serials.Remove(serial);
        RemoveAura(serial);
        RegenClock.Remove(serial);
        AmmoInitialized.Remove(serial);
    }

    /// <summary>Converts an item in someone's inventory into a different custom item (upgrades, swaps).</summary>
    public static Item Replace(Player p, Item old, CustomItem to)
    {
        var oldDef = Get(old);
        ushort serial = old.Serial;
        oldDef?.OnLeftInventory(p, serial);
        Unregister(serial);
        p.RemoveItem(old);
        return Give(p, to);
    }

    private static void InitAmmo(Item item, CustomItem def)
    {
        if (item is not FirearmItem fa || AmmoInitialized.Contains(item.Serial)) return;
        AmmoInitialized.Add(item.Serial);
        try
        {
            int cap = def.AmmoCap > 0 ? def.AmmoCap : fa.MaxAmmo;
            fa.StoredAmmo = cap;
        }
        catch (Exception e)
        {
            if (MayhemPlugin.Cfg.Debug) Logger.Debug($"ammo init failed for {def.Id}: {e.Message}");
        }
    }

    // ───────── auras ─────────

    private static void AddAura(Pickup pickup, CustomItem def)
    {
        RemoveAura(pickup.Serial);
        if (pickup.GameObject == null) return;
        try
        {
            var light = LightSourceToy.Create(Vector3.zero, pickup.Transform, false);
            light.Transform.localPosition = Vector3.up * 0.25f;
            light.Color = def.Rarity.Color();
            light.Intensity = MayhemPlugin.Cfg.Items.AuraIntensity;
            light.Range = MayhemPlugin.Cfg.Items.AuraRange;
            light.ShadowType = LightShadows.None;
            light.Spawn();
            Auras[pickup.Serial] = light;
        }
        catch (Exception e)
        {
            if (MayhemPlugin.Cfg.Debug) Logger.Debug($"aura failed: {e.Message}");
        }
    }

    private static void RemoveAura(ushort serial)
    {
        if (!Auras.TryGetValue(serial, out var light)) return;
        Auras.Remove(serial);
        if (!light.IsDestroyed) light.Destroy();
    }

    // ───────── cooldowns ─────────

    /// <summary>Returns true (and tells the player) if still cooling down; otherwise starts the cooldown.</summary>
    public static bool OnCooldown(Player p, ushort serial, string key, float seconds)
    {
        if (Cooldowns.TryGetValue((serial, key), out var until) && until > Time.time)
        {
            Hud.Message(p, $"<color=#FF7070>On cooldown ({until - Time.time:0}s)</color>", 1.5f, "cd");
            return true;
        }
        Cooldowns[(serial, key)] = Time.time + seconds;
        var def = Get(serial);
        Loops.After(seconds, () =>
        {
            var owner = Item.Get(serial)?.CurrentOwner;
            if (owner != null && def != null) Hud.Message(owner, $"<color={def.Rarity.Hex()}>{def.Name}</color> is ready", 2f);
        });
        return false;
    }

    // ───────── events ─────────

    public static void Register()
    {
        PlayerEvents.ChangedItem += OnChangedItem;
        PlayerEvents.UsingItem += OnUsingItem;
        PlayerEvents.UsedItem += OnUsedItem;
        PlayerEvents.ItemUsageEffectsApplying += OnUsageEffects;
        PlayerEvents.FlippingCoin += OnFlippingCoin;
        PlayerEvents.FlippedCoin += OnFlippedCoin;
        PlayerEvents.ThrewProjectile += OnThrewProjectile;
        PlayerEvents.ShootingWeapon += OnShooting;
        PlayerEvents.ShotWeapon += OnShot;
        PlayerEvents.ReloadingWeapon += OnReloading;
        PlayerEvents.InteractingDoor += OnDoor;
        PlayerEvents.SendingHitmarker += OnHitmarker;
        PlayerEvents.ProcessedJailbirdMessage += OnJailbird;
        PlayerEvents.SearchingPickup += OnSearchingPickup;
        PlayerEvents.PickedUpItem += OnPickedUp;
        ServerEvents.PickupCreated += OnPickupCreated;
        ServerEvents.PickupDestroyed += OnPickupDestroyed;
        ServerEvents.ProjectileExploding += OnExploding;
        ServerEvents.ProjectileExploded += OnExploded;
        Scp914Events.ProcessingPickup += On914Pickup;
        Scp914Events.ProcessingInventoryItem += On914Inventory;
    }

    public static void Unregister()
    {
        PlayerEvents.ChangedItem -= OnChangedItem;
        PlayerEvents.UsingItem -= OnUsingItem;
        PlayerEvents.UsedItem -= OnUsedItem;
        PlayerEvents.ItemUsageEffectsApplying -= OnUsageEffects;
        PlayerEvents.FlippingCoin -= OnFlippingCoin;
        PlayerEvents.FlippedCoin -= OnFlippedCoin;
        PlayerEvents.ThrewProjectile -= OnThrewProjectile;
        PlayerEvents.ShootingWeapon -= OnShooting;
        PlayerEvents.ShotWeapon -= OnShot;
        PlayerEvents.ReloadingWeapon -= OnReloading;
        PlayerEvents.InteractingDoor -= OnDoor;
        PlayerEvents.SendingHitmarker -= OnHitmarker;
        PlayerEvents.ProcessedJailbirdMessage -= OnJailbird;
        PlayerEvents.SearchingPickup -= OnSearchingPickup;
        PlayerEvents.PickedUpItem -= OnPickedUp;
        ServerEvents.PickupCreated -= OnPickupCreated;
        ServerEvents.PickupDestroyed -= OnPickupDestroyed;
        ServerEvents.ProjectileExploding -= OnExploding;
        ServerEvents.ProjectileExploded -= OnExploded;
        Scp914Events.ProcessingPickup -= On914Pickup;
        Scp914Events.ProcessingInventoryItem -= On914Inventory;
    }

    public static void Reset()
    {
        Serials.Clear();
        Auras.Clear();
        LastInventory.Clear();
        Cooldowns.Clear();
        RegenClock.Clear();
        AmmoInitialized.Clear();
        ShotHits.Clear();
        CurrentExplosion = null;
    }

    private static void Safe(string what, Action a)
    {
        try { a(); }
        catch (Exception e) { Logger.Error($"[Mayhem] item {what} error: {e}"); }
    }

    private static void OnChangedItem(PlayerChangedItemEventArgs ev) => Safe("equip", () =>
    {
        if (ev.OldItem != null && Get(ev.OldItem) is { } oldDef) oldDef.OnUnequipped(ev.Player, ev.OldItem);
        if (ev.NewItem != null && Get(ev.NewItem) is { } def)
        {
            if (def.PuppetBlocked && ev.Player.IsPuppet())
                Hud.Message(ev.Player, $"<color=#FF7070>Puppet can't use {def.Name}</color>", 2f);
            def.OnEquipped(ev.Player, ev.NewItem);
        }
    });

    private static void OnUsingItem(PlayerUsingItemEventArgs ev) => Safe("using", () =>
    {
        if (Get(ev.UsableItem) is not { } def) return;
        if (def.PuppetBlocked && ev.Player.IsPuppet()) { ev.IsAllowed = false; return; }
        if (!def.OnUsing(ev.Player, ev.UsableItem)) ev.IsAllowed = false;
    });

    /// <summary>
    /// Custom consumables never apply their vanilla effects: we stop the vanilla completion,
    /// consume the item ourselves and run the custom effect instead.
    /// </summary>
    private static void OnUsageEffects(PlayerItemUsageEffectsApplyingEventArgs ev) => Safe("usage", () =>
    {
        if (Get(ev.UsableItem) is not { } def) return;
        ev.IsAllowed = false;
        ev.ContinueProcess = false;
        var p = ev.Player;
        var item = ev.UsableItem;
        ushort serial = item.Serial;
        Unregister(serial);
        p.RemoveItem(item);
        def.OnUsed(p, item);
        Consumed?.Invoke(p, def);
    });

    /// <summary>Raised when a custom consumable is used (Addictionater listens to this).</summary>
    public static event Action<Player, CustomItem> Consumed;

    private static void OnUsedItem(PlayerUsedItemEventArgs ev) => Safe("used", () =>
    {
        if (Get(ev.UsableItem) is not { } def) return;
        ushort serial = ev.UsableItem.Serial;
        def.OnUsed(ev.Player, ev.UsableItem);
        Loops.After(0.2f, () => { if (Item.Get(serial) == null) Unregister(serial); });
    });

    private static void OnFlippingCoin(PlayerFlippingCoinEventArgs ev) => Safe("flipping", () =>
    {
        if (Get(ev.CoinItem) is not { } def) return;
        if (!def.OnFlipping(ev.Player, ev.CoinItem)) ev.IsAllowed = false;
    });

    private static void OnFlippedCoin(PlayerFlippedCoinEventArgs ev) => Safe("flipped", () =>
    {
        if (Get(ev.CoinItem) is { } def) def.OnFlipped(ev.Player, ev.CoinItem, ev.IsTails);
    });

    private static void OnThrewProjectile(PlayerThrewProjectileEventArgs ev) => Safe("thrown", () =>
    {
        if (ev.Projectile == null) return;
        var def = Get(ev.ThrowableItem.Serial) ?? Get(ev.Projectile.Serial);
        if (def == null) return;
        Serials[ev.Projectile.Serial] = def;
        AddAura(ev.Projectile, def);
        def.OnThrown(ev.Player, ev.Projectile);
    });

    private static void OnExploding(ProjectileExplodingEventArgs ev) => Safe("exploding", () =>
    {
        CurrentExplosion = null;
        CurrentExplosionOwner = null;
        if (ev.TimedGrenade == null || Get(ev.TimedGrenade.Serial) is not { } def) return;
        CurrentExplosion = def;
        CurrentExplosionOwner = ev.Player;
        NoteExplosion(def);
        if (!def.OnExploding(ev.Player, ev.TimedGrenade, ev.Position))
        {
            ev.IsAllowed = false;
            CurrentExplosion = null;
            ushort s = ev.TimedGrenade.Serial;
            Unregister(s);
            ev.TimedGrenade.Destroy();
        }
    });

    private static void OnExploded(ProjectileExplodedEventArgs ev) => Safe("exploded", () =>
    {
        var def = CurrentExplosion;
        CurrentExplosion = null;
        CurrentExplosionOwner = null;
        if (ev.TimedGrenade == null) return;
        def ??= Get(ev.TimedGrenade.Serial);
        if (def == null) return;
        Unregister(ev.TimedGrenade.Serial);
        def.OnExploded(ev.Player, ev.Position);
    });

    private static void OnShooting(PlayerShootingWeaponEventArgs ev) => Safe("shooting", () =>
    {
        if (Get(ev.FirearmItem) is not { } def) return;
        if (def.PuppetBlocked && ev.Player.IsPuppet()) return;
        ShotHits.Remove(ev.FirearmItem.Serial);
        if (!def.OnShooting(ev.Player, ev.FirearmItem)) ev.IsAllowed = false;
    });

    private static void OnShot(PlayerShotWeaponEventArgs ev) => Safe("shot", () =>
    {
        if (Get(ev.FirearmItem) is not { } def) return;
        var fa = ev.FirearmItem;
        var p = ev.Player;
        // Damage is applied during the shot; evaluate on the next frame to be safe.
        MEC.Timing.CallDelayed(0.05f, () => Safe("shot-eval", () =>
        {
            ShotHits.TryGetValue(fa.Serial, out var hit);
            ShotHits.Remove(fa.Serial);
            def.OnShot(p, fa, hit);
        }));
    });

    private static void OnJailbird(PlayerProcessedJailbirdMessageEventArgs ev) => Safe("jailbird", () =>
    {
        if (Get(ev.JailbirdItem) is { } def) def.OnJailbird(ev.Player, ev.JailbirdItem, ev.Message);
    });

    private static void OnDoor(PlayerInteractingDoorEventArgs ev) => Safe("door", () =>
    {
        if (Get(ev.Player.CurrentItem) is { } def) def.OnDoorInteract(ev.Player, ev.Door, ev);
    });

    /// <summary>Attackers of disguised players (Mirage / Skinwalker) get no hitmarkers, except SCPs.</summary>
    private static readonly Dictionary<ReferenceHub, float> HideHitmarker = new();

    public static void HideNextHitmarker(Player attacker)
    {
        if (attacker != null && !attacker.IsSCP) HideHitmarker[attacker.ReferenceHub] = Time.time + 0.2f;
    }

    private static void OnHitmarker(PlayerSendingHitmarkerEventArgs ev)
    {
        if (ev.Player != null && HideHitmarker.TryGetValue(ev.Player.ReferenceHub, out var until) && until > Time.time)
            ev.IsAllowed = false;
    }

    private static void OnReloading(PlayerReloadingWeaponEventArgs ev)
    {
        if (Get(ev.FirearmItem) is { } def && !def.CanReload) ev.IsAllowed = false;
    }

    private static void OnSearchingPickup(PlayerSearchingPickupEventArgs ev) => Safe("search", () =>
    {
        // Manic: grab instantly.
        if (!Effects.EffectManager.Has<Effects.Manic>(ev.Player) || ev.Player.IsInventoryFull) return;
        if (ev.Pickup is AmmoPickup || ev.Pickup.Category == ItemCategory.Armor) return;
        ev.IsAllowed = false;
        var pickup = ev.Pickup;
        var def = Get(pickup.Serial);
        var item = ev.Player.Take(pickup);
        if (item == null) return;
        if (def != null)
        {
            Serials[item.Serial] = def;
            def.OnAcquired(ev.Player, item);
        }
    });

    private static void OnPickedUp(PlayerPickedUpItemEventArgs ev) => Safe("pickedup", () =>
    {
        if (Get(ev.Item) is not { } def) return;
        RemoveAura(ev.Item.Serial);
        InitAmmo(ev.Item, def);
        def.OnAcquired(ev.Player, ev.Item);
        Hud.Message(ev.Player, $"Picked up {def.Rarity.Label()} <color={def.Rarity.Hex()}>{def.Name}</color>", 2.5f);
    });

    private static void OnPickupCreated(PickupCreatedEventArgs ev) => Safe("pickup", () =>
    {
        var pickup = ev.Pickup;
        if (pickup == null) return;
        // Defer: serial isn't always assigned at creation.
        MEC.Timing.CallDelayed(0.1f, () => Safe("pickup-late", () =>
        {
            if (pickup.IsDestroyed || pickup is Projectile) return;
            if (Get(pickup.Serial) is { } def && !Auras.ContainsKey(pickup.Serial)) AddAura(pickup, def);
        }));
    });

    private static void OnPickupDestroyed(PickupDestroyedEventArgs ev)
    {
        if (ev.Pickup == null) return;
        ushort serial = ev.Pickup.Serial;
        RemoveAura(serial);
        if (!IsCustom(serial)) return;
        // If it isn't in anyone's inventory a moment later, it's gone (used, fell in a pit...).
        var def = Get(serial);
        var pos = ev.Pickup.Position;
        MEC.Timing.CallDelayed(0.5f, () => Safe("pickup-gone", () =>
        {
            if (Item.Get(serial) != null || Pickup.Get(serial) != null) return;
            Unregister(serial);
        }));
    }

    // ───────── 914 upgrades ─────────

    private static bool TryUpgrade(CustomItem def, Scp914KnobSetting knob, out CustomItem result, out string message)
    {
        result = null;
        message = null;
        if (knob == Scp914KnobSetting.VeryFine && def.UpgradeTo != null && Defs.TryGetValue(def.UpgradeTo, out var up))
        {
            if (Rng.Chance(def.UpgradeChance))
            {
                result = up;
                message = $"<color={up.Rarity.Hex()}>Upgraded to {up.Name}!</color>";
            }
            else message = "<color=#FF7070>Try again</color>";
            return true;
        }
        if (knob == Scp914KnobSetting.OneToOne && def.OneToOne != null && Defs.TryGetValue(def.OneToOne, out var swap))
        {
            result = swap;
            message = $"<color={swap.Rarity.Hex()}>Switched to {swap.Name}</color>";
            return true;
        }
        return false;
    }

    private static void On914Pickup(Scp914ProcessingPickupEventArgs ev) => Safe("914 pickup", () =>
    {
        if (Get(ev.Pickup.Serial) is not { } def) return;
        // Custom items are never destroyed or turned into vanilla items by 914.
        ev.IsAllowed = false;
        var pos = ev.NewPosition;
        if (TryUpgrade(def, ev.KnobSetting, out var result, out var msg))
        {
            foreach (var p in PlayerExt.Near(pos, 6f)) Hud.Message(p, msg, 2.5f);
            if (result != null)
            {
                Unregister(ev.Pickup.Serial);
                ev.Pickup.Destroy();
                Spawn(result, pos);
                return;
            }
        }
        ev.Pickup.Position = pos;
    });

    private static void On914Inventory(Scp914ProcessingInventoryItemEventArgs ev) => Safe("914 inv", () =>
    {
        if (Get(ev.Item) is not { } def) return;
        ev.IsAllowed = false;
        if (!TryUpgrade(def, ev.KnobSetting, out var result, out var msg)) return;
        Hud.Message(ev.Player, msg, 2.5f);
        if (result != null) Replace(ev.Player, ev.Item, result);
    });

    // ───────── combat dispatch (called by Combat) ─────────

    private static CustomItem WeaponOf(PlayerHurtingEventArgs ev, Player attacker, out Item item)
    {
        item = null;
        if (ev.DamageHandler is FirearmDamageHandler fdh && fdh.Firearm != null)
        {
            item = Item.Get(fdh.Firearm.ItemSerial);
            return Get(fdh.Firearm.ItemSerial);
        }
        if (ev.DamageHandler is ExplosionDamageHandler && CurrentExplosion != null)
            return CurrentExplosion;
        if (ev.DamageHandler is JailbirdDamageHandler or MicroHidDamageHandler or DisruptorDamageHandler)
        {
            item = attacker.CurrentItem;
            return Get(item);
        }
        return null;
    }

    internal static void AttackerHurting(PlayerHurtingEventArgs ev, Player attacker, Player target)
    {
        var def = WeaponOf(ev, attacker, out var item);
        if (def == null) return;
        if (item != null) ShotHits[item.Serial] = target;
        if (def.PuppetBlocked && attacker.IsPuppet()) return;
        def.OnHit(attacker, target, ev);
    }

    internal static void OwnerHurting(PlayerHurtingEventArgs ev, Player attacker, Player target)
    {
        foreach (var (_, def) in InventoryOf(target).ToList())
        {
            def.OnOwnerHurting(target, attacker, ev);
            if (!ev.IsAllowed) return;
        }
    }

    internal static void OwnerDying(PlayerDyingEventArgs ev)
    {
        foreach (var (_, def) in InventoryOf(ev.Player).ToList())
        {
            def.OnOwnerDying(ev.Player, ev);
            if (!ev.IsAllowed) return;
        }
    }

    /// <summary>The victim's role right before the kill currently being processed (Mirage).</summary>
    public static PlayerRoles.RoleTypeId LastKillOldRole { get; private set; }

    internal static void Killed(Player killer, Player victim, PlayerDeathEventArgs ev)
    {
        LastKillOldRole = ev.OldRole;
        CustomItem def = null;
        if (ev.DamageHandler is FirearmDamageHandler fdh && fdh.Firearm != null) def = Get(fdh.Firearm.ItemSerial);
        else if (ev.DamageHandler is ExplosionDamageHandler) def = LastExplosionDef;
        else if (ev.DamageHandler is JailbirdDamageHandler or MicroHidDamageHandler or DisruptorDamageHandler) def = Get(killer.CurrentItem);
        def?.OnKill(killer, victim);
    }

    // Explosion deaths are processed after Exploded, so remember the last one briefly.
    private static CustomItem _lastExplosionDef;
    private static float _lastExplosionTime;
    private static CustomItem LastExplosionDef => Time.time - _lastExplosionTime < 0.5f ? _lastExplosionDef : null;
    internal static void NoteExplosion(CustomItem def) { _lastExplosionDef = def; _lastExplosionTime = Time.time; }

    // ───────── ticking ─────────

    public static void Tick()
    {
        foreach (var p in Player.ReadyList)
        {
            if (!p.IsAlive)
            {
                FlushInventory(p, new HashSet<ushort>());
                continue;
            }
            var now = new HashSet<ushort>();
            var held = p.CurrentItem;
            foreach (var item in p.Items.ToList())
            {
                if (Get(item) is not { } def) continue;
                now.Add(item.Serial);
                try
                {
                    def.OnInventoryTick(p, item);
                    if (held != null && held.Serial == item.Serial) def.OnHeldTick(p, item);
                    RegenAmmo(p, item, def, held);
                }
                catch (Exception e) { Logger.Error($"[Mayhem] {def.Id} tick error: {e}"); }
            }
            FlushInventory(p, now);

            if (MayhemPlugin.Cfg.Items.ShowHeldDescription && held != null && Get(held) is { } hd)
                Hud.SetInfo(p, "held", $"{hd.Rarity.Label()} <color={hd.Rarity.Hex()}><b>{hd.Name}</b></color>\n<size=16>{hd.Description}\n<i>{hd.UpgradeLine}</i></size>", 0.6f, 10);
        }

        // Rainbow aura for Admin items.
        float hue = Time.time * 0.25f % 1f;
        foreach (var kv in Auras)
            if (Get(kv.Key)?.Rarity == Rarity.Admin && !kv.Value.IsDestroyed)
                kv.Value.Color = Color.HSVToRGB(hue, 1f, 1f);
    }

    private static void FlushInventory(Player p, HashSet<ushort> now)
    {
        if (LastInventory.TryGetValue(p.ReferenceHub, out var before))
        {
            foreach (var serial in before)
                if (!now.Contains(serial) && Get(serial) is { } def)
                    Safe("left", () => def.OnLeftInventory(p, serial));
        }
        LastInventory[p.ReferenceHub] = now;
    }

    private static void RegenAmmo(Player p, Item item, CustomItem def, Item held)
    {
        if (def.RegenAmount <= 0 || item is not FirearmItem fa) return;
        if (!def.RegenWhenHolstered && (held == null || held.Serial != item.Serial)) return;
        if (RegenClock.TryGetValue(item.Serial, out var next) && next > Time.time) return;
        RegenClock[item.Serial] = Time.time + def.RegenInterval;
        int cap = def.AmmoCap > 0 ? def.AmmoCap : fa.MaxAmmo;
        if (fa.StoredAmmo >= cap) return;
        fa.StoredAmmo = Mathf.Min(cap, fa.StoredAmmo + def.RegenAmount);
    }
}
