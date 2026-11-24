# SPEC-0013: Audio

| | |
|---|---|
| **Status** | Draft |
| **Created** | 2026-11-24 |
| **Owner** | Jhonnatan |
| **Depends on** | [SPEC-0002](../0002-ro-import-foundation/spec.md) (importer family, working game data), [SPEC-0007](../0007-spr-act-pal-sprites/spec.md) (ACT frame events), [SPEC-0008](../0008-sprite-catalog/spec.md) (weapon hit sound table), [SPEC-0009](../0009-unit-display/spec.md) (unit animations), [SPEC-0010](../0010-world-session/spec.md) (world model, cells, camera, dev command line), [SPEC-0011](../0011-combat/spec.md) (hit moments) |
| **Used by** | [SPEC-0015](../0015-skills/spec.md) (effect part sounds, with the wav names of [SPEC-0014](../0014-skill-data/spec.md)'s effect table), SPEC-0006 (map effects and sounds, once expanded) |
| **Plan** | plan.md (not written) |
| **Tasks** | tasks.md (not written) |

## 1. Why

The client is silent. The original client's data holds every sound it plays: about 1,845 wavs named by sprite frame events (swings, monster steps, hurts, deaths), 640 effect wavs, and a table of hit sounds by weapon type. Effect parts need their sound (STR files name none; the wav of each part is data of SPEC-0014's effect table), and SPEC-0007 already keeps the sound events of each animation frame, but nothing plays them. Without one central place, each feature would load and play sounds its own way, with no limit on voices, no common volume and garbage on every play. Sound also tells the player where things are: near sounds in both ears, far or off-screen sounds from their side, quieter with distance.

## 2. Goals

1. The client's wavs, as the owner places them in the working data, imported once by rules that make playing them cheap, in one catalog keyed by the client's file name.
2. One audio centre: any feature asks for "sound X at a place, on a unit, or for the UI"; the centre handles volume, voices, priority, duplicates and pooling, with no garbage per frame.
3. Spatial rules as data: who listens, where a sound is heard in both ears, how it pans and fades with distance.
4. The first consumers wired: effect parts, animation frame events, hit sounds, server sound packets, UI.
5. Everything testable offline, without a server, a scene or audio hardware.

## 3. Non-goals

- Background music: no music file is in the working data and `mp3nametable.txt` is not read (SPEC-0006 D12). Music and the map's own sound emitters (SPEC-0006 FR-9 placeholders) belong to SPEC-0006, which the owner will expand to include them and which then uses this centre.
- Voice chat; text-to-speech.
- A sound options window and saving volumes between sessions are out of scope; here volumes are set from code and the dev tools.
- Which wav an effect part plays (SPEC-0014's effect table by id; SPEC-0012's STR assets hold no sound), when a skill sounds (SPEC-0015). Which sound each window plays is out of scope; this spec gives the UI category and the preload set, not the choice of UI sounds.
- Doppler, reverb, occlusion, height in the spatial rules, surround beyond two channels.

## 4. Requirements

Priority: **M** = must, **S** = should. Distances are in cells on the ground; default values are data and can be changed without code.

| ID | Pri | Requirement | Acceptance (observable) |
|---|---|---|---|
| FR-1 | M | Sound import, offline, in the importer family of SPEC-0002/0007 (same menus, report, skip-when-current): every wav the owner has extracted by hand under `data/wav` and its sub-folders (`effect` and the others) is imported; nothing is extracted from a GRF by us; unreadable or unsupported files are reported, not fatal; wav names referenced by ACT frame events (SPEC-0007) but absent from the working data are listed once each in the report. | The report gives counts per folder (640 in `effect` with the current working data), the total size before and after, and the list of referenced wav names missing; a re-run imports nothing; a corrupted wav is listed once and the rest completes. |
| FR-2 | M | Import rules, kept as data in one place: every sound mono (a stereo source is mixed down and reported), its source sample rate kept (never raised); short sounds (default under 1 s) kept uncompressed and fully loaded in memory for an instant start; longer ones compressed in memory and decoded when played; nothing streamed from disk. | For a sample of short and long wavs the imported settings match the rule; changing the threshold and re-importing moves files between the two groups; the report gives the count and memory of each group. |
| FR-3 | M | One catalog of all imported sounds, keyed by the client's file name relative to `data/wav`: case-insensitive, either slash, with or without `.wav`, Korean names as the client data writes them. A lookup allocates nothing. An unknown name plays silence and gives one warning per name per session. | `poring_attack.wav`, `PORING_ATTACK` and `effect\` + an effect wav name each resolve; a Korean-named wav resolves; `spore_move.wav` (missing from the data, SPEC-0007) asked 50 times gives one warning and no sound. |
| FR-4 | M | Loading and memory: a preload set (data; by default the UI sounds and the weapon hit sounds) is loaded at start; every other sound loads on its first request without stalling the frame. A request for a sound still loading plays if it is ready within a delay (default 100 ms), otherwise it is dropped and counted. Resident sound memory has a budget (default 32 MB): beyond it, sounds not playing and least recently used are unloaded. All data is local; nothing is downloaded. | A preloaded wav plays in the frame it is asked; the first play of an unloaded wav shows no main-thread stall over 1 ms in the profiler; with the budget set to 1 MB, playing 200 different wavs in turn keeps resident memory under 1 MB plus the sounds playing. |
| FR-5 | M | Audio centre, the only way features play sound: a request names a sound, a category and one place: a world position, a unit (the sound follows it while it plays and stays at its last position if it vanishes), or the UI (no place: no pan, no distance). Optional volume scale; one-shot or loop, a loop stopped by the handle it returns. Categories: effects, units, UI. Heard volume = master × category × request × spatial (FR-7). Loops stop on map change. | Offline: each kind of request gives a voice with the expected volume and pan; master at 0 silences everything; UI at 0 silences only UI; a UI sound plays at the title screen with no map; a loop stops on its handle and on a map change. |
| FR-6 | M | Voices: a total limit (default 32), a limit per category and per sound (default 4 of the same wav at once). Priority: UI first, then sounds on or aimed at the own character, then the rest; within a rank the louder wins. When full, a new sound takes the voice of the lowest-ranked, quietest one only if it ranks higher; otherwise it is dropped and counted. A sound heard at zero volume takes no voice. The same sound asked several times in one frame plays once, at the loudest of those requests. Voices are pooled. | Offline: 100 requests of different wavs in one frame give 32 voices, the 32 highest ranked; 30 requests of one wav in a frame give one voice at the nearest position; a UI sound with all voices busy replaces the farthest other sound; a 5th copy of a wav already playing 4 times is dropped. |
| FR-7 | M | Spatial rules, as data: the listener is the own character's ground position (the camera's focus point while there is no own character), facing the camera's turn; distance ignores height. Within the both-ears radius (default 5 cells) a sound is centred at full volume. Beyond it, the sound pans toward its side on screen (left or right of the listener as the camera sees it), the strength growing with distance up to one ear only at the full-pan distance (default 16 cells; the owner's range is 15 to 17); a sound straight above or below the listener on screen stays centred. Volume falls with distance, from full at the radius to zero at the silent distance (default 20 cells), along a fall-off curve. The three distances and the curve are data in one settings asset, changed without code, and in editor and development builds also live from the dev tools (FR-12); the radius must be below the full-pan distance and the full-pan distance not above the silent distance, otherwise the settings are refused with a warning and the defaults kept. Pan and volume follow the camera turn, the listener and a moving source while the sound plays. | Offline table with the defaults: 4 cells to the right on screen: centred, volume 1; 10 cells to the right: partly right, quieter than at 4; 16 cells or more to the right: right ear only; the same cell after a 180° camera turn: left ear only; 16 cells straight up on screen: centred, quieter; at 20 cells or more: no voice; setting the full-pan distance to 15 or 17, or changing the curve, in the settings asset changes the results with no code change; a radius of 18 with a full-pan of 16 is refused with one warning. |
| FR-8 | M | Effect sounds: an effect part's wav (the wav name and start delay its row in SPEC-0014's effect table gives; STR files hold none) plays through the centre at the part's start, at the effect's place or on its unit, category effects. This spec gives that request and its rules; SPEC-0015 makes it when it starts a skill effect's parts. SPEC-0012 plays silently. Until SPEC-0014's table exists, it is tested with rows built in the test. | Offline: a part row with a wav and a start delay of 300 ms plays that wav once, 300 ms after the effect starts, at the effect's place; the same effect 8 cells to the left on screen is quieter and left-panned; 30 identical parts started in one frame play their wav once; a row with no wav plays nothing. |
| FR-9 | M | Animation frame sounds: when a unit in view shows a frame whose event is a wav (SPEC-0007 keeps the events per frame), that wav plays on the unit, category units, once per frame entry; `atk` and other non-wav events play nothing. SPEC-0012 does not own it. | A novice's Attack2 plays `attack_short_sword.wav` on frame 5 of 9, once per swing; a walking poring plays `poring_move.wav` each walk cycle; the assassin body (no events) plays nothing. |
| FR-10 | S | Hit sounds: at a blow's hit moment (SPEC-0011) with damage above zero, the wav that the client's weapon hit table gives for the attacker's weapon type plays on the target, category units; a miss plays none. The choice is a table: player attackers by weapon type from the client data; monster attackers none by default, a row can give one. | A sword user's hit on a poring plays the sword-type hit wav at the number's moment (±1 frame); a miss plays nothing; editing a row changes the wav with no code change. |
| FR-11 | S | Server sound packet (`@sound`, the NPC sound script command): the named wav plays once, loops or stops as the packet says, on the unit it names or non-positionally when it names none. | `@sound` with a wav name plays it; with an unknown name, one warning and silence. |
| FR-12 | S | Dev tools, compiled only in editor and development builds and absent from release builds: `/sound <name>` on the own character, `/sound <name> <x> <y>` at a cell, `/volume <category> <0..1>` in the dev command line (SPEC-0010); the FR-7 distances and fall-off curve editable live, for the session, without changing the settings asset; a debug list of playing voices (wav, category, place, distance, volume, pan, rank, age) with the drop counters of FR-4 and FR-6. | `/sound poring_attack.wav` at a cell 10 to the right on screen plays from the right and shows in the list with its pan; `/volume units 0` silences swings only; setting the full-pan distance to 10 in the tool makes that sound right ear only at once; a release build has no such command, list or code. |
| FR-13 | M | Cost: with 300 units in view (100 fighting, with frame and hit sounds), 300 effects with sounds and 64 requests per frame, the centre allocates nothing per frame and nothing per request once a sound is loaded. Frame time of the centre is measured and reported (guide: 0.2 ms on the owner's PC), not a pass/fail target. | Profiler capture of that load shows 0 B GC allocation per frame from the audio centre; its time is recorded with the evidence. |
| FR-14 | M | Offline testing: catalog lookup, the import rule choice, the spatial rules, voice limits, priority and dedupe, and each consumer's choice of wav (frame event, hit table, effect part, packet) are tested without a server, a scene or audio hardware; the centre runs with no audio device and logs no error. | The tests run in the editor test runner and cover FR-2, FR-3, FR-5 to FR-11; a run with audio disabled passes with no error logged. |

## 5. Success criteria

- From Bootstrap, on a local Hercules, on prt_fild08: swings, hits and monster sounds play during a fight; a poring 10 cells to the right on screen is heard on the right, and after a 180° camera turn on the left; one within 5 cells of the character is heard in both ears; one 20 cells or more away is not heard. Effect-part sounds are checked by the FR-8 offline test (in game they come with SPEC-0015's skills).
- The import report and the list of missing wav names (warnings) are saved with the evidence.
- FR-13: 0 B per frame under its load.
- Evidence is our own captures, the profiler and the offline tests; in-game sound (side, distance, timing) is confirmed by the owner by ear in game, not by comparison with the original client.

## 6. Assumptions and constraints

- No GRF is provided or read: the owner extracts the wavs by hand into the working data (`data/wav`, today 640 under `effect`); FR-1 imports and reports what is present, and a missing name gives FR-3's warning; event names in ACT files are names relative to `data/wav` with no sub-folder (SPEC-0007 act reference, 1,845 of 1,849 found). File names are decoded as SPEC-0002 decodes the client's names.
- The weapon hit table is the client's (SPEC-0008 catalog notes it for this spec).
- `PACKETVER` 20220406 (Hercules): the server sound packet (`0x01D3` in the catalog) is checked in the plan against `packets_struct.h`, `clif.cpp` and a capture, its doc in the same change.
- Pan and volume are computed by our rules, so the results are the same with or without audio hardware; the engine only mixes.
- Effect part sounds and animation frame sounds belong to this spec (FR-8, FR-9). STR files hold no sound reference: the wav name, start delay and duplicates of each effect part are data of SPEC-0014's effect table by id (authored from leads, since the client keeps them in its executable); SPEC-0012 holds no wav and plays silently.
- Dev-only code is stripped from release builds (project rule).

## 7. Open questions

| # | Question | Blocks |
|---|---|---|
| — | None open. | — |

## 8. Revisions

| Date | Change | FRs affected |
|---|---|---|
| 2026-11-24 | Owner feedback on spatial distances: both ears within 5 cells (was 3), one ear by 16 cells (was 12; owner's range 15 to 17), silent from 20 cells (was 18); the three distances and the fall-off curve live in a settings asset and are editable in the dev tools; acceptance examples and success criteria updated. | FR-7, FR-12, §5 |
| 2026-11-24 | Self-contained wording: removed references to not-yet-existing UI specs from the header and non-goals; out-of-scope items stated without naming an owner. No requirement changed. | none (header, §3) |
| 2026-11-24 | Wording: SPEC-0012's effect table is now its catalog of effect assets keyed by path; effect wav names are read from those assets. No requirement changed. | FR-8 (wording), §3 |
| 2026-11-24 | Owner: STR files hold no wav; the wav of each effect part is data of SPEC-0014's effect table by id, not of SPEC-0012's assets. FR-8 now plays a part's wav from its SPEC-0014 row (wav, start delay), requested by SPEC-0015; acceptance made offline with built rows, since `@blvl` (a server effect packet) has no game handling in any spec (BACKLOG). Level-up sound removed from §5. Header "Used by" updated. | FR-8, header, §1, §3, §5, §6 |
| 2026-11-24 | Owner: no GRF is provided; the owner extracts the wavs by hand into the working data, and FR-1 imports what is present and lists referenced wav names missing (no extraction step). Evidence wording: in-game sound confirmed by the owner, no comparison with the original client. | FR-1, header, §2 goal 1, §5, §6 |
