# SPEC-0012: Skills

| | |
|---|---|
| **Status** | Draft |
| **Created** | 2026-11-23 |
| **Owner** | Jhonnatan |
| **Depends on** | [SPEC-0001](../0001-networking-core/spec.md) (packets), [SPEC-0008](../0008-sprite-catalog/spec.md) (skill icons, skill ids), [SPEC-0009](../0009-unit-display/spec.md) (animations: casting, attack, play once and hold), [SPEC-0010](../0010-world-session/spec.md) (world model, server clock, cell picking, dev command line), [SPEC-0011](../0011-combat/spec.md) (blow timing, damage numbers, walking into range, HP/SP, debug HUD) |
| **Reference** | [Packets](../0001-networking-core/Packets/INDEX.md), [Packet catalog](../0001-networking-core/Packets/CATALOG.md) |
| **Plan** | plan.md (not written) |
| **Tasks** | tasks.md (not written) |

## 1. Why

After SPEC-0011 the player can only hit with the weapon. Every job past novice lives on skills: Bash, Fire Bolt, Heal, Fire Wall, Double Strafe. Monsters cast skills too, and other players' skills fill the screen in any town or field. Today the client ignores all of it: a monster's Fire Bolt does damage the player cannot see, a Fire Wall blocks a path nobody sees, a cast that takes two seconds looks like a frozen character. Skills share most of their display with combat (motion times, damage numbers, hurt), so they come right after it. SPEC-0013 (effects) needs to know *when* and *where* each skill shows; this spec gives it those moments and draws no effect itself.

## 2. Goals

1. The own character knows its skills as the server sends them and can use any of them on a unit, on itself or on a cell.
2. Every skill cast in view shows its cast bar and casting motion for the server's cast time, and is cancelled when the server says so.
3. Skill damage, heals and knock-backs show with SPEC-0011's timing and numbers.
4. Ground skill units (walls, traps, areas) are known to the world model where the server places them.
5. Every visual moment of a skill is announced for SPEC-0013, and everything above is testable without a server.

## 3. Non-goals

- Drawing any skill effect, STR, SPR effect, sound, the real look of ground skill units (FR-8 only places a marker), and status-change visuals (auras, hiding, status icons): SPEC-0013 and a later UI spec.
- The skill tree window, skill point allocation, the shortcut bar and skill descriptions (a UI spec); item-given skills (scrolls, `autorun`), skill selection menus (Abracadabra, Auto Spell), combo chains, spirit spheres, homunculus, mercenary and pet skills.
- Skill names shouted over the caster (chat spec); PvP and GvG targeting, as SPEC-0011.
- Client-side prediction of cast times, cooldowns or damage: the client shows what the server sends.

## 4. Requirements

Priority: **M** = must, **S** = should.

| ID | Pri | Requirement | Acceptance (observable) |
|---|---|---|---|
| FR-1 | M | Skill list in the world model: the own character keeps each skill the server gives (id, level, SP cost, range, target kind: unit, self, ground, passive; upgradable flag), from the list at entry and from every add, update and removal after it, with its name and icon from the game data. Emptied on map change as SPEC-0010 FR-3. | Replaying a recorded session of a swordsman into the model with no scene gives the server's skill list (`@skilllist` or a capture); learning a skill on the server adds it with the sent level and range. |
| FR-2 | M | Choosing a skill to use, dev only: a dev list of the own skills (name, icon, level, SP, range, kind), the dev command `/skill <id or name> [level]` in the dev command line (SPEC-0010 FR-13), and dev hotkeys F1 to F9 bound to skills by command. The level defaults to the learned one and may be lower, never higher. Passive skills cannot be chosen. All of it is stripped from release builds; the real skill window and shortcut bar wait for a UI spec. | `/skill bash` arms Bash at its learned level; `/skill bash 11` with level 10 learned is refused with a message; a passive skill is refused; F1 bound to Fire Bolt arms it; a release build contains none of the list, the command or the hotkeys. |
| FR-3 | M | Target casts: an armed unit skill turns the mouse cursor into the original client's skill target cursor; a click on a monster (or on any unit the skill allows: the own character or another player for support skills) asks for the skill on it; a right click or Esc disarms it. A self skill is asked at once, on the own character. Out of range, the own character walks into range first, by the server's rule and SPEC-0011 FR-3's distance, and asks when in range; a "too far" reply makes it approach again. No request is sent that the server would refuse: dead, sitting, target dead or gone, after-cast delay still running (FR-7). | Bash armed and clicked on a poring 6 cells away walks to it and lands without a refusal in 9 of 10 tries; Heal clicked on the own character heals it; Fire Bolt on a poring 9 cells away is cast from where the character stands; while sitting nothing is sent. |
| FR-4 | M | Ground casts: an armed ground skill shows the skill target cursor over walkable and non-walkable cells as the original client does (SPEC-0010 FR-11 picking); a click on a cell asks for the skill on that cell, walking into range as FR-3. | Fire Wall armed and clicked 3 cells in front of the character sends one ground request with that cell; the server's ground unit appears there (FR-9). |
| FR-5 | M | Cast bar and casting motion: for every cast in view (own, players, monsters) the caster turns to its target or cell, plays the casting motion and shows the original client's cast bar for the cast time the server sends, filling at the original client's pace; a cast time of 0 shows no bar. The bar and motion end when the skill fires or when the server cancels the cast (interruption, target gone); a cancelled own cast is reported once at info level. | A Fire Bolt level 5 cast shows a bar for the server's time (±1 frame) and the casting motion, then the bolt's damage; a cast interrupted by a hit ends the bar at once; a monster's cast in view shows its bar; screenshots side by side with the original client match the bar's size, colours and place. |
| FR-6 | M | Skill results with damage: a skill blow in view (single target, or each target of an area skill) shows as a SPEC-0011 blow: attacker motion stretched to `attackMT`, number and hurt at the hit moment, the skill's hit count splitting the damage as the original client does, miss and endure, and the knock-back of position skills ending on the server's cell. Healing shows the original client's heal number on the healed unit. A skill with no damage (buffs, Teleport) plays the caster's skill motion and nothing else here. | Bash on a poring shows its motion, number and hurt at 0011's timing; Double Strafe shows two numbers then the total, as the original; Magnum Break hits each monster around with its own number; a knocked-back monster ends on the server's cell (next server stop finds it within one cell); Heal shows the heal number; Increase AGI shows only the caster's motion. |
| FR-7 | M | Own skill state: SP cost is shown through SPEC-0011's SP bar from the server's values (never subtracted by the client); after-cast and per-skill delays the server sends are kept and end on time; a refused skill (not enough SP, delay, wrong weapon, failed condition) is reported once at info level with the server's reason and leaves nothing armed or casting. | Casting with 2 SP short gives one info line naming the reason and no bar; the dev HUD shows a delay counting down from the server's value; the SP bar equals `@stats` after 20 casts. |
| FR-8 | M | Ground skill units: every skill unit the server places (its id, creator, skill unit type, cell, radius, level, visible or hidden) is an entity of the world model, updated in place and removed when the server removes it or the map changes. Hidden units (traps not detected) are kept as the server sends them. Until SPEC-0013 replaces it with the real effect, each visible unit shows a plain placeholder marker on its cells, in every build; hidden units show none. | A Fire Wall makes as many units as the server sends, on its cells, each with a placeholder marker in a release build too, and units and markers go when the server removes them; an undetected trap shows no marker; a recorded session replays into the model with the server's units at the end. |
| FR-9 | M | Effect hooks: each visual moment is announced with what SPEC-0013 needs to draw it and draws nothing: cast start (skill, level, caster, target unit or cell, cast time), cast end and cancel, skill fired (skill, level, caster, target or cell, motion time), skill hit (per target, at the hit moment), ground skill used on a cell, skill unit appeared and removed (unit type, cell, radius). Announcements are in event order and carry server ids, not drawn objects. | A test listener receives, for one recorded Fire Bolt, cast start, cast end, fired and one hit per bolt hit, in that order and at the FR-5 and FR-6 moments; for a Fire Wall, one ground-used and one appear per unit; no effect is drawn. |
| FR-10 | S | Skill debug HUD (dev only, beside SPEC-0011's): own skill list with levels, the armed skill, cast state and delays; the last 20 skill events (caster, skill, target or cell, damage, hit count, cast time, motion times); refusals and their reasons; skill units in view. Not in release builds. | During a fight the HUD lists each skill event as it arrives and its values equal the packets; a release build contains none of it. |
| FR-11 | M | Cost: with 300 units in view, 50 casting and 100 numbers on screen, the skill update (model, casts, bars, hooks; excluding SPEC-0009's and SPEC-0011's updates) and the skill packets allocate nothing per frame. Frame time is measured and reported, not a pass/fail target. | Profiler capture of that load (real or replayed) shows 0 B GC allocation per frame from the skill update; frame time recorded with the evidence. |
| FR-12 | M | Offline testing: the skill list, the range rule for skills, the cast timing, the result sequence and the hook order are tested without a server and without a scene, including against recorded real sessions. | The tests run in the editor test runner; a recorded session replays with the FR-1, FR-8 and FR-9 results. |

## 5. Success criteria

- From Bootstrap, on a local Hercules, with characters of four jobs (swordsman, mage, acolyte, archer): on prt_fild08 kill 10 monsters each with Bash, Magnum Break, Fire Bolt, Fire Wall and Double Strafe, heal with Heal and buff with Increase AGI. No skill is refused for distance more than once per target, every cast bar lasts the server's cast time, own SP equals `@stats` at the end, and no skill unit is left in the model after its server removal.
- A monster that casts (e.g. a caster monster on a field with one) shows its bar and damage.
- The FR-5 side-by-side evidence with the original client is saved; FR-11: 0 B per frame.

## 6. Assumptions and constraints

- Hercules is the reference (`src/map/skill.cpp`, `unit.cpp`, `clif.cpp`, `battle.cpp`, `conf/map/battle/skill.conf`); the original client is the reference for what the player sees (cursor, cast bar, numbers), roBrowser a lead only. Whether the server itself walks a player into skill range or, as for attacks, refuses with "too far" is checked in the plan; FR-3 assumes the client approaches.
- Packet IDs and layouts depend on PACKETVER 20220406 and are each checked against `packets_struct.h`, `clif.cpp` and a real capture in the plan, docs in the same change (BACKLOG). Expected: in skill list `0x010F` (or its newer form), add `0x0111`, update `0x07E1`, removal, use failure `0x0110`, cast `0x0B1A`, cast cancel, damage `0x01DE`, position damage `0x0115`, no-damage use and heal `0x09CB`, ground use `0x0117`, unit entry `0x09CA`, unit removal, after-cast delay `0x043D`; out `0x0438` (to unit), `0x0366` (to cell).
- Skill names come from the client's game data; icons from SPEC-0008's skill icons. Skill ids are the server's.
- Casting motion, attack motion and play-once come from SPEC-0009; blow timing, numbers, walking into range and the SP bar from SPEC-0011, unchanged. Effect drawing waits for SPEC-0013 and the hooks are its only input from here.
- Game UI is built with UI Toolkit; dev-only views may stay as SPEC-0010 builds them; dev-only code is stripped from release builds (project rule).

## 7. Open questions

| # | Question | Blocks |
|---|---|---|
| — | None open. | — |

## 8. Revisions

| Date | Change | FRs affected |
|---|---|---|
