# Changelog

## 1.2.0: Ascension polish
- The three ascension PCs glow blue.
- The phone code is 9 digits by default and random every round. Existing servers keep their old `code_length` in config.yml; set it to 9 to match.
- New code panel at the bottom middle of the Puppet's and SCP-079's screens: the NATO words, plus a slot per digit that fills in as the keypad is pressed.
- A wrong code at the Nuke Room keypad detonates the Alpha Warhead instantly. Everyone on Surface survives.
- M.Y.H.M Insurgents and the Puppet can now shoot each other for full damage. Grenades and melee still follow team rules.
- The Puppet no longer respawns after the warhead has detonated.
- Self-test: `mayhem selftest nuke` runs the wrong-code path.

## 1.1.0: synced with the latest Mayhem changelogs
**Puppet rework**
- 500 HP that doesn't scale with tier. Medical items can heal it up to 50% of max HP; SCP-079's ping heal is halved (1 AP = 0.5 HP).
- Generators are lives. The map gets 2–5 generators by player count, with extras spawned at unused HCZ spots.
- Losing a life: the Puppet respawns in 079's room, the Puppet and 079 are stunned for 5s, and activating generators shut off and are blocked for 20s.
- Hume shield 50/100/150 that regenerates 10/s after 12s. Breaking it gives 0.3s of immunity plus 2s of Disabled.
- Tooth and Nail regenerates 3 rounds/s after 4s without firing.
- Pickup whitelist. Custom items are recycled to the Pocket Dimension, and a **Quick Recycle** keybind and **Puppet opt-out** were added to the server-specific settings.
- The Puppet can't trigger teslas, and warp/hot-swap coins can't move it. Peacemaker heals the Puppet up to its max HP.
- Ascension needs 12+ players; both Puppets get 10s of spawn protection when it happens.

**Items**
- New: **Mitosis** (adrenaline) and **Jester's Hat** (SCP-268).
- Minigun: 15 damage, 75% penetration. Tazer: 1 damage per shot.
- Revolver custom weapons load the first chambers.
- The HUD shows live cooldowns and the next ammo regen for the held item.

**Gambling / events / world**
- SCP-500: new "scientist summons all MTF" and "spawn an enemy on you" outcomes. Revives now use the 30s **Reviving Players** effect, which waits for a safe spot.
- The SCP-1853 event mixes in other consumables, including custom ones.
- Halloween: 30% chance to spawn with candy. Spawn bonuses drop at your feet when your inventory is full.
- Recycled items return after 90–240s. Items lost to decontamination or the warhead go to the Pocket Dimension, and items that fall off Surface are put back on Surface.
- Nuke room radiation ramps up exponentially for humans and resets slowly.
- *Oh, You're Finally Awake!* respects the nuke timer. Muted players can't trigger *You May Want To Be Quiet*.
- Tazed is less disorienting but blocks items. M.Y.H.M Insurgents don't drop items.

**Fixes found by testing on a real 14.2.9 server**
- Effects no longer carry over after a role change.
- One-shot protection now absorbs damage from every weapon, including the jailbird.
- Short effects (like 0.3s immunity) expire exactly on time.

## 1.0.0
- First release: 59 custom items, gambling, SCP reworks, SCP-079 + Puppet, the ascension questline and every event from the community Mayhem guide.
