# Kiko's Mayhem

A chaotic **SCP: Secret Laboratory** gamemode for **LabAPI**, built for SCP:SL **14.2.x**:

- **61 custom items** across 7 rarities, each with a glowing aura
- Gambling consumables
- Reworked SCPs
- SCP-079 with a player-controlled **Puppet** and a full **ascension questline**
- Global events, sub events and late-round protocols

> Inspired by the *Mayhem* mode from Dr. Bright's servers. Recreated from the community-written Mayhem guide and updated to match the mode's latest public changelogs.
> Not affiliated with Dr. Bright's or Northwood. All code here is original.

---

## Quick start: hosting on a Windows PC

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download) (only needed if you build from source).
2. Download this repo (green **Code** button → **Download ZIP**) and unzip it.
3. Get `KikosMayhem.dll` from the **Releases** page and put it in the repo folder. You can also build it yourself in step 4.
4. Open PowerShell in the repo folder and run:
   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts\install-server.ps1
   # or, to build the DLL from source instead of downloading it:
   powershell -ExecutionPolicy Bypass -File scripts\install-server.ps1 -Build
   ```
   This installs SteamCMD and the SCP:SL Dedicated Server to `C:\SCPSL-Server`, then installs the plugin.
5. Start the server:
   ```bat
   scripts\start-server.bat
   ```
   The first time, LocalAdmin asks you to accept the EULA and to pick some options. The defaults are fine.
6. Join from the game. **Local test:** in the server browser, use *Direct connect* to `127.0.0.1:7777`.
   **Hosted for friends:** forward UDP port **7777** on your router, keep `online_mode: true` in `config_gameplay.txt`,
   and set your server's name and info. Getting listed publicly also requires Northwood's server verification;
   read Northwood's official server hosting documentation before going public.

Linux works the same way: `./scripts/install-server.sh [--build]`, then `cd ~/scpsl && ./LocalAdmin 7777`.

### Where things live

| What | Windows | Linux |
|---|---|---|
| Plugin DLL | `%AppData%\SCP Secret Laboratory\LabAPI\plugins\global\` | `~/.config/SCP Secret Laboratory/LabAPI/plugins/global/` |
| Mayhem config | `%AppData%\SCP Secret Laboratory\LabAPI\configs\7777\KikosMayhem\config.yml` | `~/.config/SCP Secret Laboratory/LabAPI/configs/7777/KikosMayhem/config.yml` |
| Music / SFX | the `audio` folder next to that config | same |

The config is created on first start. **Every number** in the mode is in there: spawn rates, damage, cooldowns, SCP buffs, 079 tiers and event weights.
See [`docs/config.default.yml`](docs/config.default.yml) for the full default file with comments.

---

## What's in the mode

### Custom items: see [docs/ITEMS.md](docs/ITEMS.md)
- Rarities: Common 🟢 · Rare 🔵 · Epic 🟣 · Legendary 🟡 · Upgraded 🩵 · Ascended 🔴 · Admin 🌈.
- Items spawn all over the facility. Special piles spawn at **Gate A (surface)**, at **Exit (Gate B)**, and in **SCP-079's room**. The 079 room pile only spawns if 079 isn't playing, or once 079 is contained.
- **SCP-914 Very Fine** upgrades items (e.g. Echo Ball → SCP-018018, Cluster Grenade → Pocket Nuke at 0.5%). Key ↔ Lock to the City swap on 1:1.
- Custom items can't leave the round. If one falls into a pit, it goes to SCP-106's dimension and is later dropped back into the facility.
- **Hell's Reckoning** is the only way to get Admin items: equip it, then kill an SCP within 2 minutes.
- Custom items lost to decontamination or the warhead are also sent to the Pocket Dimension (except SCP-018). Any item that falls off the map on Surface is put back on Surface.
- While you hold an item, the bottom of the screen shows its live cooldown and when its next ammo regenerates.
- Newer items: **Mitosis** (an adrenaline that turns a dead player into a copy of you, which must stay close) and **Jester's Hat** (an SCP-268 that leaves a killable decoy and makes you invisible for 10s).

### Status effects
Custom effects show up on the right of the HUD with timers, colored by type: green = positive, purple = mixed, red = negative.
Manic, Eyes of Odin, Skinwalker, Radiance, Herd, Turbo Drunk, Tazed, Irradiated, Hunted, Revive Cooldown, Burning, Overdose, Vampirism, Overvolt and Gas Gas Gas.
Crit chance and crit damage stack from SCP-1853, Criti Cola, Vampirism and more.

### Gambling
- **Painkillers:** 90% chance of +5% speed (capped at 100%), 10% chance of death.
- **SCP-500:** revive one player, revive everyone, turn into a 1500 HP zombie, scramble your inventory, summon guards (or all MTF if you're a scientist), spawn an enemy on top of you, cure zombies, teleport to the SCP...
  Revives give you a **Reviving Players** effect: within 30 seconds, get somewhere safe and the dead come back to you. Safe means on the ground, not in an elevator, the Pocket Dimension, decontaminating LCZ, or a facility about to be nuked.
- **Adrenaline:** 25 outcomes, from +15% speed to *Gas Gas Gas* to exploding.
- **Addictionater:** while you're overdosing, everything you eat is queued and hits all at once when the overdose ends.

### Reworked SCPs
- **173:** 50% faster blinks, plus 5% faster per dead player. Its tantrums are explosive traps you can shoot. Breakneck Speed breaks doors.
- **096:** tiny while docile, so you see his face even looking at the floor. Damage aura, breaks windows, explodes 5 times during a charge, invisible while trying not to cry. After the nuke he ramps up to +110% speed with 4x SCP-207.
- **106:** 80% chance to escape his dimension. Escaping leaves you traumatized, and he one-shots traumatized players. Damage reduction per healthy player, speed per player in his dimension, a growing aura, and a slow when he emerges. After the nuke, everyone is traumatized and his aura, damage and speed double.
- **939:** her pounce explodes, sinkholes and concusses, then gives her +90% speed. Stacking **Hunted** effect on hit. Amnestic clouds irradiate and slow. She stops being blind after the nuke.
- **049:** +30% speed and double cardiac damage. Kills turn into zombies instantly and heal the horde. Doctor's Call makes 049 and nearby zombies invisible. Each zombie alive speeds up the others.
- **966:** a black figure that appears behind someone and just poses. Sometimes it brings a facility-wide blackout.

### SCP-079 and the Puppet
- 079 picks a Puppet by pinging a player (E) or typing **`.puppet`** in the console. `.puppet` lists players, and `.puppet 5` picks #5.
  Players can opt out in **Settings → Server-specific → Kiko's Mayhem**. Opted-out players are only picked when nobody else is available.
- After it's picked, the Puppet gets a 90-second grace period locked in the LCZ armory, with generators locked.
- **Generators are lives.** The map gets 2 to 5 generators depending on player count. Extra ones spawn at unused HCZ generator spots, and every generator that isn't engaged is one life.
  - When the Puppet dies with lives left, a random generator engages and the Puppet respawns in 079's room with its health and effects reset.
  - Both the Puppet and SCP-079 are stunned for 5 seconds.
  - Generators that were activating shut off and are blocked for 20 seconds.
  - With no generators left, the Puppet dies for good. When SCP-079 dies, the Puppet dies with it.
- The Puppet has **500 HP**, which doesn't scale with tier.
  - It can heal itself with medical items, but only up to 50% of max HP. SCP-079 heals it further by pinging it (1 AP = 0.5 HP).
  - It can't heal while decontaminating.
- It starts with combat armor, an O5 card, and **Tooth and Nail**, its own SCP-127. Tooth and Nail's tier follows 079's: 50/100/150 hume shield and 50/60/70% penetration.
  - The shield regenerates 10 HS/s after 12s without taking damage.
  - **One-shot protection:** while the shield is up, the Puppet can't take health damage at all. When the shield breaks, it gets 0.3s of immunity and 2s of Disabled.
- Armor is a trade-off: none or light armor gives +20% speed and infinite stamina, combat armor gives +10%, heavy armor gives nothing.
- **Item restrictions:** the Puppet can only pick up armor, medical items, grenades, keycards, radios, ammo, coins, flashlights, SCP-018, SCP-244 and lanterns.
  - Touching a **custom item** sends it to the Pocket Dimension. It comes back to the facility 90–240s later.
  - The **Quick Recycle** keybind (server-specific settings) does the same for the closest custom item within 2.5m.
- The Puppet can't trigger teslas, and warp/hot-swap coins can't move it.
- **079 abilities by tier:**
  - All tiers: ping the Puppet to heal it.
  - Tier 2: slam bulkhead doors (50 AP).
  - Tier 3: grenade barrage on a pinged enemy (75 AP, 20s cooldown).
  - Tier 4: overcharge a generator so it explodes (100 AP, 30s cooldown per generator).
  - Tier 5: ping the surface nuke controller to start the nuke.

### The Ascension (079 tier 5, 12+ players)
1. A **server rack** on the bottom floor of the Server Room starts humming. The Puppet interacts with it.
2. **Three PCs** come online, glowing blue so they're easy to spot: one in Light Containment, one in Heavy or Entrance, and one in Entrance. The Puppet uses all three.
3. A **phone rings** in Entrance. Answering it plays a call where a voice reads a new random 9-digit code in NATO words (different every round). The call keeps looping at the phone, so you can always go back and listen again. The code is only given by voice:
   `0 ALPHA · 1 BRAVO · 2 CHARLIE · 3 DELTA · 4 ECHO · 5 FOXTROT · 6 GOLF · 7 HOTEL · 8 IMMINENT · 9 JUILLIET`
4. The Puppet decodes it and types it into the **keypad in the Nuke Room**. A panel at the bottom middle of the Puppet's and SCP-079's screens shows one slot per digit, and each key pressed fills in the next slot, so you can check it before pressing OK. Typing `.code 123456789` in the console while standing at the keypad also works.
   **A wrong code detonates the Alpha Warhead instantly.** Everyone in the facility dies; anyone on Surface survives.
5. The **Recontainment Protocol** starts: the Puppet has **45 seconds** to run to SCP-079's room and can't be teleported on the way.
6. **Ascension:** SCP-079 becomes a second Puppet, and both Puppets get 10s of spawn protection. The new Puppet can't be healed, but regenerates fast after a few seconds without taking damage.
   **Alpha-1 "Red Right Hand"** then respawns *everyone* (alive or dead) on Surface. The SCPs win by killing everyone or reaching the **Gate B exit**.

### SCP-079 and the Puppet
- **Voice chat:** SCP-079 and the Puppet hear each other anywhere. SCP-079 talks on its normal SCP chat key, and the Puppet just talks normally. After the ascension, both Puppets hear each other the same way.
- **Map:** SCP-079's map shows the Puppet as an **SCP-049 arrow**. This uses a tiny hidden dummy player named "Puppet" that follows the Puppet. Only SCP-079 can see it, and every Mayhem system ignores it. SCP-079's SCP list shows the Puppet's HP under SCP-049.
- **Generator panel:** the top-left corner of the Puppet's screen shows how many generators (lives) are left, and when someone is turning one on, which room it's in and its countdown.
- Every SCP (and the Puppet) gets a 5-second pop-up at the top middle of the screen listing its Mayhem powers when it spawns.

### Events
- **Global (one per round):** Rare Items Spawn Like Common, 5x Loot, Item Chances Inversed, Custom Items On Death, Causality Corruption, All Items → SCP-1853, 5x Custom Item Loot.
- **Sub events (sometimes):**
  - *You May Want To Be Quiet:* peak your mic and you explode. Muted players are safe.
  - *Oh, You're Finally Awake!:* everyone spawns somewhere random, and never in a facility that's about to be nuked.
  - *People Drop Candy On Death.*
- **All Items → SCP-1853** also turns some items into other consumables, including custom ones.
- **Ultimate Loot Pile:** a pile of 1853, 207, Anti-207, adrenaline, SCP-500 or painkillers appears somewhere on Surface.
- **About 25 minutes in:** either **Mayhem Protocol** (35%) or **M.Y.H.M Insurgency** (65%).
  - Mayhem Protocol: 2 minutes to finish the round, or it's a stalemate and everyone dies.
  - M.Y.H.M Insurgency: spectators become super soldiers who hunt everyone.
- **[REDACTED] Protocol:** start the nuke at midnight (server time) and find out.
- The round ends when only one side is left. The Puppet counts as the SCP side.

### Spawn bonuses
On spawn, every human rolls for bonuses: extra ammo, adrenaline, SCP-207, SCP-500, an A7, an E-11, a Jailbird, a Micro-HID, a COM-45 or SCP-018, plus a 0.05% *Overloaded COM-45*.
If your inventory is full, the bonus drops at your feet. During Halloween (October, or the game's Halloween event), you also have a 30% chance to spawn with a random candy.

### Nuke room radiation
Stay in the nuke room too long and you get **Irradiated**. For humans it ramps up exponentially the longer you stay, and the timer only resets slowly after you leave.

---

## Commands

| Command | Who | What |
|---|---|---|
| `.puppet` / `.puppet <number or name>` | SCP-079 (client console, `` ` `` key) | List players / pick your Puppet |
| `.code <digits>` | Puppet | Enter the ascension code at the Nuke Room keypad |
| `mayhem items` | Admin (RA or server console) | List custom item ids |
| `mayhem give <player\|me\|all> <id>` | Admin | Give a custom item |
| `mayhem spawn <id>` | Admin | Spawn a custom item at your feet |
| `mayhem event <name>` / `mayhem sub <name>` | Admin | Start a global / sub event |
| `mayhem protocol` / `mayhem insurgency` / `mayhem lootpile` | Admin | Trigger those right now |
| `mayhem puppet <player>` | Admin | Force someone to be SCP-079's Puppet |
| `mayhem ascension start` | Admin | Start the ascension questline (079 must be tier 5) |
| `mayhem effect <player\|me> <effect> [seconds]` | Admin | Apply a Mayhem effect |
| `mayhem selftest` | Admin, **test servers only** | Drives dummy players through every item and system |
| `mayhem selftest nuke` | Admin, **test servers only** | Runs the ascension with a wrong keypad code (detonates the warhead) |

Admin commands need the `GivingItems` RA permission. You can also alias them as `km ...`.

---

## Music and sound effects (optional)

The phone call's voice lines and the ascension sound effects (`phone_ring`, `phone_pickup`, `phone_intro`, `nato_0`–`nato_9`, `phone_repeat`, `phone_outro`, `phone_hangup`, `keypad_beep`, `keypad_accept`, `keypad_deny`, `pc_beep`, `server_rack_hum`) are built into the DLL. A file with the same name in the `audio` folder replaces the built-in one.

Everything else runs fine with no audio. To add sound, drop **16-bit PCM `.wav`** files (any sample rate, mono or stereo) into the `audio` folder next to the config, named after the slot:

`gas_gas_gas`, `dubstep`, `hells_reckoning`, `mayhem_protocol`, `mayhem_insurgency`, `redacted_protocol`, `recontainment`, `ascension`, `red_right_hand`, `server_rack_hum`, `pc_beep`, `phone_ring`, `keypad_beep`, `keypad_accept`, `keypad_deny`, `radiance_revive`

Use audio you have the rights to. No music ships with this repo.

---

## Building from source

You need the .NET 8 SDK and the SCP:SL Dedicated Server installed. The game's DLLs are referenced but never committed.

```bash
dotnet build src/KikosMayhem/KikosMayhem.csproj -c Release -p:SL_REFERENCES="<server>/SCPSL_Data/Managed"
```

The DLL ends up in `src/KikosMayhem/bin/Release/net48/KikosMayhem.dll`.
GitHub Actions builds it on every push (see the **Actions** tab). Pushing a tag like `v1.0.1` publishes a Release with the DLL attached.

### Project layout
```
src/KikosMayhem/
  Core/         HUD, stat stacking, crits/combat pipeline, disguises, audio, utilities, self test
  Effects/      custom status effects
  Items/        item engine (serials, auras, 914 upgrades, spawning, recycling) + Defs/ per rarity
  Gambling/     painkillers / adrenaline / SCP-500, spawn bonuses, SCP HP scaling, nuke-room radiation
  Scps/         173, 096, 106, 939, 049/049-2, 966
  Scp079/       SCP-079 + Puppet, and the Ascension questline
  Events/       global/sub/special events, Mayhem Protocol, M.Y.H.M Insurgency, round end rules
  Commands/     .puppet, .code, mayhem admin command
```

## Known differences / limitations
- The original mode's exact numbers aren't public. Wherever the community guide says "?" or gives no number, a sensible default was picked. All of them are in the config.
- M.Y.H.M Insurgents and the Puppet both use the Tutorial role. Gunfire between them deals full damage, but grenades and melee follow the game's team rules and won't hurt each other.
- Not included: features from Dr. Bright's other servers rather than the Mayhem mode itself, such as SCP tickets, Patreon perks, purgatory/overflow servers and SCP-575 blackouts.
- *You May Want To Be Quiet* decodes voice on the server to measure loudness, so tune `be_quiet_threshold` to your community's mics.
- The nuke room keypad is placed next to the warhead panel. If it looks off on a future map update, adjust `keypad_side_offset` / `keypad_height_offset`.

## License
MIT. See [LICENSE](LICENSE).
