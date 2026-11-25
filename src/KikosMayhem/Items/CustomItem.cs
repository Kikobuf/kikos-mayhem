using KikosMayhem.Core;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Features.Wrappers;
using UnityEngine;

namespace KikosMayhem.Items;

public enum Rarity { Common, Rare, Epic, Legendary, Upgraded, Ascended, Admin }

public static class RarityExt
{
    public static Color Color(this Rarity r) => r switch
    {
        Rarity.Common => new Color(0.2f, 1f, 0.2f),
        Rarity.Rare => new Color(0.2f, 0.45f, 1f),
        Rarity.Epic => new Color(0.7f, 0.2f, 1f),
        Rarity.Legendary => new Color(1f, 0.85f, 0.1f),
        Rarity.Upgraded => new Color(0.1f, 1f, 1f),
        Rarity.Ascended => new Color(1f, 0.1f, 0.1f),
        _ => UnityEngine.Color.white,
    };

    public static string Hex(this Rarity r) => r == Rarity.Admin ? "#FF66FF" : "#" + ColorUtility.ToHtmlStringRGB(r.Color());

    public static string Label(this Rarity r) => r == Rarity.Admin
        ? "<color=#FF0000>A</color><color=#FF8800>D</color><color=#FFFF00>M</color><color=#00FF00>I</color><color=#00AAFF>N</color>"
        : $"<color={r.Hex()}>{r.ToString().ToUpperInvariant()}</color>";
}

/// <summary>
/// Base class for every Mayhem custom item. A custom item is a normal game item whose serial
/// is registered with the engine; the definition receives callbacks for whatever the player does with it.
/// </summary>
public abstract class CustomItem
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract Rarity Rarity { get; }
    public abstract ItemType BaseType { get; }
    public abstract string Description { get; }

    /// <summary>Id of the item this upgrades into on 914 Very Fine, and the chance (percent).</summary>
    public virtual string UpgradeTo => null;
    public virtual float UpgradeChance => 0f;

    /// <summary>Id this item swaps into on 914 1:1 (Key ↔ Lock to the City).</summary>
    public virtual string OneToOne => null;

    /// <summary>Internal items (Tooth and Nail, 079's grenades) never spawn and aren't rewards.</summary>
    public virtual bool Hidden => false;

    /// <summary>Spawns naturally around the facility (Admin items never do).</summary>
    public virtual bool NaturalSpawn => Rarity != Rarity.Admin && !Hidden;

    /// <summary>Puppet can't benefit from this item.</summary>
    public virtual bool PuppetBlocked => false;

    /// <summary>Blocked from the Causality Corruption global event pool.</summary>
    public virtual bool CausalityBlacklisted => false;

    public string UpgradeLine =>
        UpgradeTo != null && ItemEngine.TryGetDef(UpgradeTo, out var up)
            ? $"Can be upgraded to {up.Name} on Very Fine ({UpgradeChance:0.#}%)"
            : OneToOne != null && ItemEngine.TryGetDef(OneToOne, out var swap)
                ? $"Can be switched to {swap.Name} on 1:1"
                : "Can not be upgraded.";

    // ───── lifecycle ─────
    /// <summary>Called once when the item is first created (given or spawned).</summary>
    public virtual void OnCreated(ushort serial) { }
    /// <summary>Called when a player gets the item in their inventory.</summary>
    public virtual void OnAcquired(Player p, Item item) { }
    public virtual void OnEquipped(Player p, Item item) { }
    public virtual void OnUnequipped(Player p, Item item) { }
    /// <summary>Every 0.25s while the item is held (in hand).</summary>
    public virtual void OnHeldTick(Player p, Item item) { }
    /// <summary>Every 0.25s while the item is anywhere in the inventory (armor "worn" effects).</summary>
    public virtual void OnInventoryTick(Player p, Item item) { }
    /// <summary>The item left the inventory (dropped, removed, owner died).</summary>
    public virtual void OnLeftInventory(Player p, ushort serial) { }

    // ───── usage ─────
    public virtual bool OnUsing(Player p, Item item) => true;
    public virtual void OnUsed(Player p, Item item) { }
    public virtual bool OnFlipping(Player p, CoinItem coin) => true;
    public virtual void OnFlipped(Player p, CoinItem coin, bool tails) { }
    public virtual void OnThrown(Player p, Projectile projectile) { }
    /// <summary>Return false to cancel the base explosion.</summary>
    public virtual bool OnExploding(Player owner, TimedGrenadeProjectile grenade, Vector3 position) => true;
    public virtual void OnExploded(Player owner, Vector3 position) { }
    public virtual bool OnShooting(Player p, FirearmItem firearm) => true;
    /// <summary>After a shot. <paramref name="hit"/> is true if it damaged someone.</summary>
    public virtual void OnShot(Player p, FirearmItem firearm, Player hit) { }
    public virtual bool CanReload => true;
    /// <summary>The holder is interacting with a door while holding this item.</summary>
    public virtual void OnDoorInteract(Player p, Door door, PlayerInteractingDoorEventArgs ev) { }
    /// <summary>Jailbird messages (charge started, attack performed...).</summary>
    public virtual void OnJailbird(Player p, JailbirdItem jailbird, InventorySystem.Items.Jailbird.JailbirdMessageType message) { }

    // ───── combat ─────
    /// <summary>The holder damaged someone with this item.</summary>
    public virtual void OnHit(Player attacker, Player target, PlayerHurtingEventArgs ev) { }
    /// <summary>The holder killed someone with this item.</summary>
    public virtual void OnKill(Player attacker, Player victim) { }
    /// <summary>The owner (with the item anywhere in inventory) is being hurt.</summary>
    public virtual void OnOwnerHurting(Player owner, Player attacker, PlayerHurtingEventArgs ev) { }
    public virtual void OnOwnerDying(Player owner, PlayerDyingEventArgs ev) { }

    // ───── firearms ─────
    /// <summary>Magazine capacity override (0 = vanilla).</summary>
    public virtual int AmmoCap => 0;
    /// <summary>Ammo regenerated per tick of <see cref="RegenInterval"/> (0 = no regen).</summary>
    public virtual int RegenAmount => 0;
    public virtual float RegenInterval => 1f;
    /// <summary>Regenerate even when not held.</summary>
    public virtual bool RegenWhenHolstered => true;
    /// <summary>Regen only starts after this many seconds without firing.</summary>
    public virtual float RegenDelayAfterShot => 0f;

    public override string ToString() => $"{Name} [{Id}]";

    protected static void Msg(Player p, string text, float t = 3f) => Hud.Message(p, text, t);
}
