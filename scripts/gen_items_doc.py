#!/usr/bin/env python3
"""Regenerates docs/ITEMS.md from the item definitions in src/KikosMayhem/Items/Defs/*.cs."""
import re, glob, os
root = os.path.join(os.path.dirname(__file__), '..')
items = []
for f in sorted(glob.glob(os.path.join(root, 'src/KikosMayhem/Items/Defs/*.cs'))):
    src = open(f, encoding='utf-8').read()
    for m in re.finditer(r'public sealed class (\w+) : (\w+)\s*\{(.*?)\n\}', src, re.S):
        body = m.group(3)
        def g(name):
            mm = re.search(r'public override \w+ ' + name + r' => (.*?);', body)
            return mm.group(1).strip() if mm else None
        idv = g('Id')
        if not idv:
            continue
        base = g('BaseType')
        if not base:
            pm = re.search(r'class ' + m.group(2) + r'.*?BaseType => (.*?);', src, re.S)
            base = pm.group(1) if pm else '?'
        items.append(dict(id=idv.strip('"'), name=g('Name').strip('"'), rarity=g('Rarity').replace('Rarity.', ''),
                          base=base.replace('ItemType.', ''), desc=(g('Description') or '').strip('"'),
                          up=(g('UpgradeTo') or '').strip('"'), chance=g('UpgradeChance'), swap=(g('OneToOne') or '').strip('"'),
                          ns=g('NaturalSpawn')))
order = ['Common', 'Rare', 'Epic', 'Legendary', 'Upgraded', 'Ascended', 'Admin']
aura = {'Common': '🟢 Green', 'Rare': '🔵 Blue', 'Epic': '🟣 Purple', 'Legendary': '🟡 Yellow', 'Upgraded': '🩵 Cyan', 'Ascended': '🔴 Red', 'Admin': '🌈 Rainbow'}
out = [f'# Custom items ({len(items)})', '',
       'Every custom item glows with its rarity aura when dropped. While you hold one, its name, description, live cooldown and next ammo regen show at the bottom of your screen.', '',
       'Give one with `mayhem give <player|me|all> <id>` (Remote Admin or server console). To disable an item, add its id to `items.disabled` in the config.', '',
       '**SCP-914 upgrades:** put an upgradable item through **Very Fine** to roll its upgrade chance. A failed roll shows "Try again" and you keep the item. Key ↔ Lock to the City swap on **1:1**. 914 never destroys a custom item or turns it into a vanilla item.', '',
       "**Admin (rainbow)** items never spawn naturally. The only way to get one is to kill an SCP with **Hell's Reckoning**.", '',
       '**Puppet** can\'t pick up custom items. Touching one sends it to the Pocket Dimension instead (see the README).', '']
for r in order:
    rs = [i for i in items if i['rarity'] == r]
    if not rs:
        continue
    out += [f'## {r} ({aura[r]} aura)', '', '| Id | Name | Base item | What it does | Upgrade |', '|---|---|---|---|---|']
    for i in rs:
        up = ''
        if i['up']:
            up = f"→ `{i['up']}` on Very Fine ({i['chance'].rstrip('f')}%)"
        elif i['swap']:
            up = f"↔ `{i['swap']}` on 1:1"
        note = ' *(admin give only)*' if i['ns'] == 'false' else ''
        out.append(f"| `{i['id']}` | {i['name']} | {i['base']} | {i['desc'].replace('|', '/')}{note} | {up} |")
    out.append('')
out += ['## Puppet-only', '', '| Id | Name | Base item | What it does |', '|---|---|---|---|',
        "| `tooth_and_nail` | Tooth and Nail | GunSCP127 | Puppet's exclusive SCP-127. Can't spawn or be dropped. Its tier follows SCP-079's (079 T3 → tier 2, T5 → tier 3): 50/60/70% penetration and 50/100/150 hume shield. Regenerates 3 rounds/s after 4s without firing. |"]
open(os.path.join(root, 'docs/ITEMS.md'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print(len(items), 'items documented')
