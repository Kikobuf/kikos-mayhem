# Custom items (61)

Every custom item glows with its rarity aura when dropped. While you hold one, its name, description, live cooldown and next ammo regen show at the bottom of your screen.

Give one with `mayhem give <player|me|all> <id>` (Remote Admin or server console). To disable an item, add its id to `items.disabled` in the config.

**SCP-914 upgrades:** put an upgradable item through **Very Fine** to roll its upgrade chance. A failed roll shows "Try again" and you keep the item. Key ↔ Lock to the City swap on **1:1**. 914 never destroys a custom item or turns it into a vanilla item.

**Admin (rainbow)** items never spawn naturally. The only way to get one is to kill an SCP with **Hell's Reckoning**.

**Puppet** can't pick up custom items. Touching one sends it to the Pocket Dimension instead (see the README).

## Common (🟢 Green aura)

| Id | Name | Base item | What it does | Upgrade |
|---|---|---|---|---|
| `addictionater` | Addictionater | Painkillers | Permanent +20% speed & Invigorated, 25s of Manic. Starts an overdose: painkillers, adrenaline and SCP-500 you eat are consumed instantly and ALL hit at once when it ends. |  |
| `criti_cola` | Criti Cola | AntiSCP207 | +20% movement speed. 40% crit chance, 1.8x crit damage (stacks). Puppet: 25% / 1.33x. |  |
| `beer` | Beer | AntiSCP207 | 3x Turbo Drunk (every hit gives you random effects) and 65% negative effect reduction. |  |
| `echo_ball` | Echo Ball | SCP018 | If anyone is killed by this ball, it spawns another SCP-018. | → `scp_18018` on Very Fine (18%) |
| `schrodingers_grenade` | Schrödinger's Grenade | GrenadeHE | Either explodes instantly once thrown, or does 3x damage (doesn't stack with crits). |  |
| `greedy_grenade` | Greedy High Explosive Grenade | GrenadeHE | On explosion spawns weapons, keycards and consumables — and rarely a custom item. | → `scrooge_grenade` on Very Fine (25%) |
| `key_to_the_city` | Key to the City | KeycardO5 | O5 keycard that BREAKS every door you use it on. | ↔ `lock_to_the_city` on 1:1 |
| `lock_to_the_city` | Lock to the City | KeycardJanitor | Locks any door in the facility for 10 seconds. 20s cooldown. | ↔ `key_to_the_city` on 1:1 |
| `rattle_em` | Rattle Em' | GunA7 | A7 that regenerates 30 ammo every 0.75s, up to 100. | → `minigun` on Very Fine (10%) |
| `regen_e11` | Regenerating Epsilon-11 | GunE11SR | Epsilon-11 rifle that regenerates 5 ammo per second, up to 50. |  |
| `soberizer` | Soberizer | GunCrossvec | Shooting a human steals their effects. Doesn't work on SCPs. |  |

## Rare (🔵 Blue aura)

| Id | Name | Base item | What it does | Upgrade |
|---|---|---|---|---|
| `jesters_hat` | Jester's Hat | SCP268 | Spawns a killable decoy of you, holding the last item you held. You get 10 seconds of invisibility and +10% speed. |  |
| `accelerator` | Accelerator | GunAK | 40% chance to gain +1% movement speed on hit. | → `sonic_surge` on Very Fine (25%) |
| `pink_extract` | Pink Extract | Adrenaline | Gives you the Sinkhole effect... then you explode, dealing 1000+ damage to everyone in range. |  |
| `vampirism` | Vampirism | Adrenaline | Awakens the demon inside you 😈. 35s timer: every kill gives +4% speed, +5% crit chance, 20 AHP and more time. When it runs out you drop to 1 HP. |  |
| `turbo_boost` | Turbo Boost | Adrenaline | 15s of SCP-207 x255, +255% movement speed and negative effect immunity (stacks). |  |
| `warp_coin` | Warp Coin | Coin | 50%: you teleport to someone random. 50%: someone random teleports to you (never SCPs or Puppet). 30s cooldown. | → `hotswap_coin` on Very Fine (25%) |
| `autovest` | Autovest | ArmorHeavy | While worn, your guns reload themselves (even the one you're holding). |  |
| `mirage` | Mirage | GunRevolver | Killing someone disguises you as their role and gives 50 hume shield. Disguise lasts until the shield breaks. Attackers don't get hitmarkers. |  |
| `bye_bye_machine` | Bye Bye Machine | ParticleDisruptor | Burst: sends the target to a nearby room. Disruptor: sends them to another zone. Charged shot on an SCP: 50% another zone, else a random room. 3 shots, +1 every 20s. |  |
| `pinpointer` | Pinpointer | GunA7 | Every hit deals 200 damage. Every miss kills YOU. |  |
| `real_207` | Real SCP-207 | SCP207 | 3x SCP-207 and +100% movement speed. | → `realest_207` on Very Fine (25%) |
| `warp_grenade` | Warp High Explosive Grenade | GrenadeHE | Teleports to a random enemy (Puppet and SCPs included) and explodes 1.5s faster. |  |
| `tazer` | Tazer | GunCOM18 | Shooting an enemy Tazes them. Holds 1 round, regenerates every 10s. |  |
| `homecooked_dinner` | Homecooked Dinner | Medkit | Heals you, then builds up to 280 AHP over time. Also Invigorated and Manic. |  |
| `padded_armour` | Padded Armour | ArmorLight | Silent Walk: your footsteps make no sound. |  |
| `anomalous_tracker` | Anomalous Tracker | Radio | While held, tells you if an SCP is in your zone and how far away it is. | → `huginn_muninn` on Very Fine (10%) |
| `switch_a_roo` | Switch-A-Roo | Coin | Flip to swap your whole inventory with someone else's (Puppet included). One use. |  |

## Epic (🟣 Purple aura)

| Id | Name | Base item | What it does | Upgrade |
|---|---|---|---|---|
| `real_anti_207` | Real Anti SCP-207 | AntiSCP207 | 3x SCP-207? and 25% damage reduction to ALL damage. | → `paladins_elixir` on Very Fine (25%) |
| `cluster_grenade` | Cluster Grenade | GrenadeHE | Explodes after 1.5s into a spread of grenades that keep their momentum — carpet bomb an area. The main blast does 65% less damage. | → `pocket_nuke` on Very Fine (0.5%) |
| `peacemaker` | Peacemaker | GunCOM18 | Shooting an enemy instantly converts them to YOUR class (zombies too). Shooting an SCP heals it by 200+ HP per bullet at the cost of 40% of your HP. 4 shots, +1 every 20s. |  |
| `effectorizer` | Effectorizer | GunFSP9 | Gives a random effect to the enemy you shoot (god mode, pit death and other insta-kill effects are blacklisted). |  |
| `skinwalker` | Skinwalker | ArmorLight | Everyone sees you as their OWN role. Revealed for 3s after you damage someone, and after the nuke. Attackers get no hitmarkers while you're camouflaged. |  |
| `guardians_glow` | Guardian's Glow | Lantern | You and close teammates get Radiance: healing, a little AHP, and one prevented death (3s invincible, 120s cooldown). |  |
| `all_pill` | The All Pill | SCP500 | Every living player gets one random effect. |  |
| `dubstep_gun` | Dubstep Gun | MicroHID | Infinite charge. Using it drains your health and builds Overvolt (drains faster). Kills remove stacks and give AHP. 60 stacks = you can't handle the dubstep and explode. |  |
| `recursive_destiny_coin` | Recursive Destiny Coin | Coin | 75%: revives a dead player as your class next to you. 25%: kills you. One use. |  |
| `huginn_muninn` | Huginn and Muninn | SCP1344 | Gives Eyes Of Odin: tracks ALL enemies in the facility and shows the nearest one. |  |
| `mitosis` | Mitosis | Adrenaline | Spawns an imperfect molecular copy of you (a dead player) with some minor defects. The copy must stay close to you or it destabilizes and falls apart. Its damage scales with distance from you (0.5x close → 1.25x far). |  |

## Legendary (🟡 Yellow aura)

| Id | Name | Base item | What it does | Upgrade |
|---|---|---|---|---|
| `crimson_echo` | Crimson Echo | GunCOM15 | Deals 200 TRUE damage (ignores AHP, hume shield and damage reduction) to enemies, and 40 damage to you. Never crits. |  |
| `grenade_launcher` | Grenade Launcher | GunRevolver | The area where your bullet lands explodes. Regenerates 1 shot every 30s. | → `rolling_thunder` on Very Fine (25%) |
| `minigun` | Minigun | GunCom45 | COM-45 with 200 rounds that quickly regenerate. 15 damage per shot with 75% armor penetration. |  |
| `suicide_vest` | Suicide Vest | ArmorCombat | You explode when you die, dealing around 1000 damage to everyone nearby. |  |
| `scp_18018` | SCP-018018 | GrenadeHE | On explosion releases 18 SCP-018 balls. Teammates are safe from them for the first 10s. |  |
| `real_500` | Real 500 | SCP500 | Guaranteed \"revive all\": every spectator respawns as your class. (Revives nobody if Puppet uses it.) |  |
| `hyperion` | Hyperion | Jailbird | Infinite uses. When a charge ends it explodes, damaging nearby enemies and breaking doors |  |

## Upgraded (🩵 Cyan aura)

| Id | Name | Base item | What it does | Upgrade |
|---|---|---|---|---|
| `scrooge_grenade` | Scrooge Grenade | GrenadeHE | After the explosion, 5 custom items spawn (any rarity except Admin). |  |
| `sonic_surge` | Sonic Surge | GunAK | Every hit on an enemy gives you +2% movement speed. |  |
| `hotswap_coin` | Hotswap Coin | Coin | Swap positions with 1 random player. Doesn't work on SCPs or Puppet. |  |
| `realest_207` | Realest SCP-207 | SCP207 | 4x SCP-207 and +100% movement speed. |  |
| `paladins_elixir` | Paladin's Elixir | AntiSCP207 | 2x SCP-207? and 87.5% damage reduction to ALL damage. (Weakens as Puppet takes damage.) |  |
| `rolling_thunder` | Rolling Thunder | ? | 4-shot revolver |  |

## Ascended (🔴 Red aura)

| Id | Name | Base item | What it does | Upgrade |
|---|---|---|---|---|
| `hellfire` | Hellfire | GunLogicer | Shooting an enemy spawns an orange bubble of fire that deals high damage and burns anyone inside. Regenerates 5 ammo per second. |  |
| `pocket_nuke` | Pocket Nuke | GrenadeHE | Blows up the whole area it's thrown in. Everyone nearby gets Irradiated (10% HP/second) — only SCP-500 cures it. |  |
| `mayhem_coin` | Mayhem Coin | Coin | Shuffles EVERY living player: classes, locations, items and effects. 300s cooldown. |  |
| `hells_reckoning` | Hell's Reckoning | Jailbird | Equip to unleash it: 6x crit damage, +100%→200% speed, 3x SCP-207, 85% damage reduction and bypass. You have 2 minutes to kill an SCP for an ADMIN item. Fail, or break it, and you die. |  |

## Admin (🌈 Rainbow aura)

| Id | Name | Base item | What it does | Upgrade |
|---|---|---|---|---|
| `live_duck_reaction` | Live Duck Reaction | GrenadeFlash | On impact, detonates over and over. Blinds enemies for 100+ seconds. |  |
| `ultimate_500` | Ultimate 500 | SCP500 | Applies 100 SCP-500 effects all at once. |  |
| `overcharged_adrenaline` | Overcharged Adrenaline | Adrenaline | Applies the effects of 100 adrenalines all at once. Good luck. |  |
| `elixir` | Elixir | AntiSCP207 | SCP-1853, Invigorated, SCP-207 x255, 65% negative effect & damage reduction, +255% speed, negative effect immunity, Ghostly, Silent Walk and no fall damage. |  |
| `mitzeys_scream` | Mitzey's Scream | GunCom45 | 200-round COM-45 that fires explosive shots. Removed from Hell's loot pool for crashing servers — admin give only. *(admin give only)* |  |

## Puppet-only

| Id | Name | Base item | What it does |
|---|---|---|---|
| `tooth_and_nail` | Tooth and Nail | GunSCP127 | Puppet's exclusive SCP-127. Can't spawn or be dropped. Its tier follows SCP-079's (079 T3 → tier 2, T5 → tier 3): 50/60/70% penetration and 50/100/150 hume shield. Regenerates 3 rounds/s after 4s without firing. |
