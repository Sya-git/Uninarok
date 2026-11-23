# Revisions to open (2026-11-23)

Three prompts for `/sdd-spec NNNN --revise <change>`. Paste the text under each heading as the change. Files stay uncommitted until the human approves each stage. The evidence below comes from a session that compared the client with roBrowserLegacy (`src/DB/Jobs/WeaponAction.js`, `src/DB/Items/EquipmentRules.js`), the Hercules server source and the live editor; it is a lead, not the official client. Re-verify before approving.

---

## 1. `/sdd-spec 0011 --revise` — attack animation: job families and dual wield

**Change request**

The player attack animation (FR-4, plan §4.7, `AttackMotions`) is wrong for two groups of players. Both play Attack1 or Attack2 where Attack2 or Attack3 is expected.

**A. Jobs derived from a first class play Attack1 for every weapon.**
- `AttackMotions` has rows only for first-class job ids (0 to 25, Soul Linker 4049, Summoner 4218). `CatalogWeaponTypes.BaseJobOf` returns the job whose *sprites* the unit uses (the catalog's layer table), which for a transcendent, third, expanded or fourth job is its own id (4008, 4054, 4066, 4252 ...). No row exists for it, so the number falls to 1.
- `combat-reference.md` says "the caller maps them, T5.4". T5.4 did not do it.
- roBrowserLegacy copies the first-class row to each derived job (`duplicateEntry` in `WeaponAction.js`).
- Measured in the editor with the real `NumberOf`: 129 job ids in the roBrowser table differ from ours. Groups, with the row each should use:
  - Knight, Crusader: transcendent (4008, 4014, 4015, 4022), Rune Knight, Royal Guard, Dragon Knight, Imperial Guard, their baby, second and mounted ids.
  - Priest: transcendent, Archbishop, Cardinal.
  - Wizard, Sage: transcendent, Warlock, Sorcerer, Arch Mage, Elemental Master, Soul Reaper.
  - Blacksmith, Alchemist: transcendent, Mechanic, Genetic, Meister, Biolo.
  - Hunter, Rogue: transcendent, Ranger, Shadow Chaser, Windhawk, Abyss Chaser.
  - Assassin: transcendent, Guillotine Cross, Shadow Cross.
  - Monk: transcendent, Sura, Inquisitor.
  - Bard, Dancer: transcendent, Minstrel, Wanderer, Troubadour, Trouvère.
  - Ninja: Kagerou, Oboro and their baby. Gunslinger: Rebellion and its baby.
  - Not run in the editor (ids above 4300, inferred from the table): second jobs, Hyper Novice, Spirit Handler, Soul Ascetic, Night Watch, Druid family. Some have their own rows in roBrowser (Hyper Novice like Super Novice, Druid, Doram).
- First classes, pre-transcendent (4001 to 4007), babies (4023 to 4045), Soul Linker and Summoner already match.

**B. Dual wield is never detected.**
- `CombatTimeline.AttackNumber` passes `secondHand: false` always; its comment says the second hand "waits for the catalog's item kinds (T5.4)". T5.4 did not do it.
- The server sends the right hand in `Look.Weapon` and the left hand in `Look.Shield` (Hercules `clif_get_weapon_view`: the item's view id, else its item id). An assassin with two knives therefore reads as one short sword and plays Attack2; the table row for two weapons (types 25 to 30 → Attack3) is never used.
- roBrowser combines both hands into one type (`mountWeapon`, `getDualWieldWeaponType` in `EquipmentRules.js`) and uses it for the animation and the sprite.
- **Every hand case must be handled** (the assassin family can wear a weapon in either hand; a shield only goes in the left):

  | Right (`Look.Weapon`) | Left (`Look.Shield`) | Type to use | Today |
  |---|---|---|---|
  | weapon | none | the right weapon's type | correct |
  | none | weapon | the left weapon's type, as if it were the only one (roBrowser: `first = second`); sprite and animation | **wrong: bare hand, Attack1, weapon not drawn** (recorded in `combat-dual`, first session: left knife only) |
  | weapon | weapon | the combined type 25 to 30 (assassin family only; another job keeps the right weapon's type) | **wrong: right weapon only** |
  | weapon | shield | the right weapon's type; the shield is only drawn | correct |
  | two-handed weapon | none | its type | correct |
  | katar | none | type 16: Attack3 for the assassin family (roBrowser: `KATAR` → 2); the sprite is the katar sprite (item ids 1250 to 1260 have view 16 in the item table) | correct for an Assassin (checked in the editor: 3); wrong for the derived jobs (revision A) |
  | none | shield | bare hand: Attack1 (Gunslinger family: Attack2); only the shield is drawn | correct (weapon 0 → bare hand); shield drawn if its kind is Shield |
- **Weapon or shield in the left hand:** decide by the item's kind from the item table (`TryGetItemView` → kind Weapon or Shield), as roBrowser does by item id range. A small left value (a view id) is ambiguous: treat it as a shield, as the `CombatTimeline` comment already warns. Pairs and the combination table: short sword + short sword 25, sword + sword 26, axe + axe 27, short sword + sword 28 (either order), short sword + axe 29, sword + axe 30.

**Expected after the change**
- Every job id that roBrowser lists plays the animation of its first-class row for each weapon type (use roBrowser as the lead, verify against the owner's observations).
- An assassin family player with two weapons plays Attack3; with one weapon in the right hand, as today; with one weapon only in the left hand, the attack of that weapon alone (Attack2 for a knife), not Attack1; a shield in the left hand never changes the attack.
- Tests for each row of the hand table, including a katar (Attack3, assassin and a derived assassin job), a shield with an empty right hand (Attack1) and a left value that is a shield view id.
- Tests: a table-driven test over the job families (derived id → expected number for each weapon type), a dual-wield test fed from the `combat-dual` capture, and `combat-reference.md` updated in the same change.
- Out of scope: the second-hand sprite (revision 2).

**Open question to put to the owner:** Taekwon, Star Gladiator, Gangsi and Death Knight have no row in roBrowser either (Attack1). Keep as is?

---

## 2. `/sdd-spec 0009 --revise` — dual-wield sprite and transcendent colours

**Change request**

**A. Dual-wield sprite shows one weapon.**
- `UnitLoader` loads `Look.Shield` as an `EquipmentKind.Shield`. A weapon item in the left hand is reported "is a Weapon" and not drawn. The right hand loads the single-weapon sprite.
- **Hand cases** (same table as revision 1): right weapon only, **left weapon only (the left weapon is drawn as the only weapon, with the single-weapon sprite of its type)**, two weapons (combined sprite), weapon and shield (both drawn). A shield only goes in the left hand. Which left value is a weapon and which a shield comes from the item's kind, not from the number.
- The catalog already has the combined sprites (suffix like `_단검_검`, weapon types 25 to 30; sprite-catalog.md §3.8). The composer needs the combined type from both hands (short sword + short sword = 25, sword + sword = 26, axe + axe = 27, short sword + sword = 28, short sword + axe = 29, sword + axe = 30) and must pick the sprite by it, for an assassin family job.
- The Unity composer window likely shows two blades because the combined type is typed directly as the weapon view id; that does not reflect what the server sends.
- Share the "two hands → combined type" rule with revision 1 (one place, used by combat and by the loader).

**B. Transcendent classes show the colours of the normal classes (owner report, cause not verified).**
- Hypothesis to confirm or reject: a high or transcendent job that has no body of its own is drawn with its base job's body and palettes (`UnitLoader.SpriteJob`, `Palette`), so cloth and hair colours come from the base job's palette set.
- Check how roBrowser and the official client choose the palette for transcendent and high jobs (palette file naming by job, an offset, or a separate set), and which job ids are affected (high first classes 4001 to 4007, transcendent 4008 to 4022, third jobs, babies).
- Report the cause before proposing a requirement; if the cause is data (the catalog lacks the palettes), say so.

**Expected after the change**
- An assassin with two knives shows both weapons in every direction and attack, and its attack plays Attack3 (with revision 1).
- An assassin with a knife only in the left hand shows that knife; with a knife and a shield, shows both; with a katar shows the katar sprite alone; with only a shield (right hand empty) shows only the shield.
- The composer presets (revision 3) store the left hand as a weapon or a shield.
- A transcendent class shows its own colours for each cloth and hair colour index; evidence by screenshot from the composer.

---

## 3. `/sdd-spec 0009 --revise` — presets in the Unity composer (`RoUnitComposerWindow`)

**Change request**

Add named presets to the composer window, so a look can be saved and recalled instead of rebuilt by hand. Examples to support: swordsman with a dagger, swordsman with a spear, novice with a rod; also assassin with two hands for dual wield (needs the left-hand weapon field of revision 2).

**What a preset holds:** everything the window sets for the unit: job, sex, hair style and colour, cloth colour, body style, the three headgears, garment, weapon (view id or item id), shield or left-hand weapon (view id or item id), plus the animation, direction and frame the window shows (decide which of these belong).

**Behaviour**
- Pick a preset from a list: the window shows that look at once.
- Save the current look as a new preset (name), overwrite the selected one, rename, delete.
- Presets survive an editor restart and a domain reload.
- A preset that refers to something the catalog no longer has loads what it can and warns once; it does not fail.
- No change to runtime code: editor only. No new third-party library.

**Questions for the owner (put in the spec's open questions):**
1. Shared with the team (files in the repository) or per user (not committed)?
2. Does a preset include the animation, direction and playback state, or only the look?
3. A fixed starter set (the three examples above) shipped with the repository?

---

## 4. BACKLOG note (not a revision) — server items the LATAM client does not have

Not a client bug. The imported item table is the LATAM client's; the server loads `db/re/item_db.conf` plus `db/item_db2.conf` (renewal build, `ENABLE_RENEWAL` ON), which hold items the LATAM client does not list. Measured on 2026-11-23: 128 of the 1,064 weapons of `item_db.conf` are absent from the imported table (examples: katars 1272, 1283, 1286, 1288, 1298, 28002; Claymore 1190; Muramasa 1177; spears 1423, 1424, 1427), and `item_db2.conf` items such as 1998 (Jeramiah's Jur) and 1999 (Zed's Staff) are absent too. The owner confirms these items do not exist on LATAM.

What happens if one of them is ever equipped (a GM command, a test account): the weapon reads as bare hand and is not drawn. This matches what the real LATAM client could not show either.

Note for the backlog, one line: `Hercules-2026.09/db/re/item_db.conf` and `db/item_db2.conf`: items the LATAM client table does not list (about 12% of the weapons); do not hand them to players, or accept bare hand. A possible check on import: list server items with no entry in the client table.

Katars in the client table all use view 16 (85 of 85, including the 28000 and 610000 series), so the katar animation and sprite need no change.
