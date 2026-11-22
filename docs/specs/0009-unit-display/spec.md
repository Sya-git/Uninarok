# SPEC-0009: Unit Display and Camera

| | |
|---|---|
| **Status** | Approved (owner, 2026-11-21) |
| **Created** | 2026-11-21 |
| **Owner** | Jhonnatan |
| **Depends on** | [SPEC-0006](../0006-rsw-map-scene/spec.md) (map scene), [SPEC-0007](../0007-spr-act-pal-sprites/spec.md) (one sprite), [SPEC-0008](../0008-sprite-catalog/spec.md) (catalog by id) |
| **Reference** | [sprite-catalog.md](../0008-sprite-catalog/sprite-catalog.md) §1 (composition tables), [SpriteAction](../../data/SpriteAction.md), [UninarokPosition](../../data/UninarokPosition.md) (direction) |
| **Plan** | [plan.md](plan.md) (Approved, 2026-11-21) |
| **Tasks** | [tasks.md](tasks.md) (Approved, 2026-11-21) |

## 1. Why

The server describes a unit by ids: a job id, and for a player a class, sex, hair style and colour, cloth colour and up to six equipment view ids. SPEC-0008 finds each sprite; nothing yet puts them together. A player is up to nine sprites (body, head, three headgears, garment, weapon, weapon trail, shield) that must stay in step, attach to each other, and swap draw order by direction and frame as the client does. Seen through a rotating camera, a unit must also show the side of it that faces the camera, not the side its world facing would show at the default camera. Both need a real camera to check, and the world spec (movement, own character) needs both. Doing them offline first means gameplay starts on correct visuals.

## 2. Goals

1. Show any unit the server can describe (player, monster, NPC, homunculus, mercenary) as the original client does, from its ids alone.
2. A unit's look depends on its world facing and the camera, never on the camera alone.
3. An RO-style camera that orbits, tilts and zooms over a real map.
4. Check all of it without a server: one unit in an editor tool, many on a real map in a dev scene.

## 3. Non-goals

- Network packets, entity registry, movement, paths, walk and attack speed from the server, own-character input, cell picking (world spec, next).
- Combat timing, damage numbers, HP bars (combat spec).
- STR and hat effects, monster attached effects, auras, carts, falcons, pets (effects spec and later).
- Name labels, chat bubbles, emotes over units, any UI.
- Sound events of animations.

## 4. Requirements

Priority: **M** = must, **S** = should.

| ID | Pri | Requirement | Acceptance (observable) |
|---|---|---|---|
| FR-1 | M | Player appearance: a player is shown from class, sex, hair style, hair colour, cloth colour, top/mid/low headgear, garment, weapon (with its trail) and shield, all looked up in the catalog with its fallbacks. A missing or unknown part is left out with one warning; the rest shows. | A swordsman male with a hair colour, a top and a low headgear, a garment, a sword and a shield shows every part as the reference viewer does; an unknown headgear id shows the player without it. |
| FR-2 | M | Other units: monsters, NPCs, homunculi and mercenaries are shown from their job id at their catalog display scale. Job ids the client draws as nothing (hidden NPCs, warp markers) show nothing. Player-class job ids used by NPCs show as players. | Poring, a town NPC and an invisible-NPC job id show as in the client (the last as nothing). |
| FR-3 | M | Draw order: parts are layered as the client does, per direction and per frame: headgear priorities, garments drawn above the body, and the per-class, per-action swaps that put weapon or garment behind or in front. | Facing front, the sword is in front of the head (BACKLOG); a garment is behind the body facing front and in front facing back; a top headgear stays above the hair in every direction. |
| FR-4 | M | Attachment: head and headgears follow the body's anchor in every frame, with the client's offset corrections (per headgear and direction, riding head corrections). | Through a full walk cycle in 8 directions, the head of a novice and of a knight on peco never leaves the neck; a corrected headgear sits where the reference viewer puts it. |
| FR-5 | M | One clock, the body's: the body's frame time drives the unit; every other part shows the frame the body is on, whatever its own frame time. All parts start and restart together and keep the same frame position when the direction changes. Parts never drift apart. | After 10 minutes of walking, head bob and weapon still match the body frame for frame; a sword trail whose own frame time is 150 ms changes frame with a 100 ms body; turning keeps the current frame in every part. |
| FR-6 | M | Camera-relative facing: the direction shown is the unit's world facing relative to the camera's turn, in 8 sectors of 45° whose borders lie 22.5° either side of each direction (the nearest of the 8), applied to every part at once. At the default camera a unit facing South shows its front. | A unit facing South shows its front at the default camera, its back after turning the camera 180°, and a side after 90°; it still shows its front at a 22° turn and the next direction at 23°; during a slow camera turn all parts switch sector on the same frame, never one before the others. |
| FR-7 | M | Animations: a unit plays any animation of its family by name (idle, walk, sit, attack, hurt, dead, cast, …) either looping, or once then holding the last frame, or once then reporting it ended. A missing animation falls back as SPEC-0007/0008 define (units to idle, equipment draws nothing). | Dead holds its last frame; an attack played once reports its end; a one-animation NPC asked to walk stays idle. |
| FR-8 | S | Head direction: a player's head can look straight, left or right of the body while idle or sitting, as the client does. | Setting head left on an idle player turns only the head and its headgears. |
| FR-9 | M | Live changes: any part, colour or the job can change while the unit is shown, without restarting its animation or moving it. | Swapping the weapon and the hair colour mid-walk keeps the walk frame; changing the job id of a unit replaces its look in place. |
| FR-10 | M | Ground placement: a unit stands with its feet on its ground point at any camera tilt and zoom, faces the camera fully (no squash from the tilt), is not cut by the ground, and does not float (approved variant E, BACKLOG). | A poring and a swordsman on a slope of prt_fild08 keep their feet on the ground, uncut and unsquashed, at the lowest and highest camera tilt. |
| FR-11 | S | Shadow: every unit gets a ground shadow at the scale the client's shadow table gives its job id; a job id not in the table gets the default scale (1); a scale of 0 means no shadow. The table (987 job ids) gives 0 to warp and hidden NPCs, hidden monsters, plants, flowers and mushrooms, and a few monsters drawn without one (snakes, Hode, Dracula, Doppelganger, Joker, …: 67 ids); baby classes get 0.75. | A poring and a novice (not in the table) have a normal shadow; a fabre's is half size; a warp NPC and a red plant have none. |
| FR-12 | M | Camera: a perspective camera (not orthographic) with the client's narrow vertical view angle of 15°, orbiting a target point on the map at a distance. It turns freely all the way round on every map, tilts and zooms within limits, and eases towards each new turn, tilt and zoom instead of jumping. Default tilt, default distance and the tilt and distance limits are the original client's, measured side by side (§6). Controls: right-drag turns; shift + right-drag tilts; wheel and ctrl + right-drag zoom; double right-click resets the turn, with shift the tilt, with ctrl the distance. Moving the target moves the camera with it. | On prontera, side by side with the original client at the same window size, the default view and both ends of the tilt and zoom ranges show the same area of the map (screenshots saved as evidence); each control does what it says; the camera turns 360° around a point, stays within the limits, and follows a target moved across the map. |
| FR-13 | M | Entity Sandbox: a new dev-only scene, named Entity Sandbox, on a real map, without a server. It places units of chosen appearance and changes their facing, animation, parts and the camera. A stress mode fills the map with the FR-14 unit count, mixed looks, each playing idle, walk, attack, hurt or dead in place and turning now and then, so FR-14 is measured on realistic load. Not in release builds. | From the Entity Sandbox, FR-6, FR-10, FR-11, FR-12 and FR-14 can be checked on prontera; the stress mode starts with one action; a release build contains none of it. |
| FR-14 | M | Cost: 100 fully equipped players and 200 monsters on prontera, all animating, cost at most 2 ms per frame for the unit update on the owner's PC, with no per-frame garbage. | The Entity Sandbox stress mode at that count shows a unit update of 2 ms or less and 0 B GC allocation per frame in the profiler, on the owner's PC. |
| FR-15 | M | Unit composer (editor tool, like the sprite preview of SPEC-0007): build one unit by choosing either a job id (monster, NPC, …) or a player (class, sex, race, variant, hair style and colour, cloth colour, three headgears, garment, weapon, shield, by view id or item id); pick the animation, the play mode (FR-7) and the world facing; turn the preview camera to check FR-6. It plays, pauses, steps one frame and scrubs, and shows the current frame number and its time for the body and for every part; each part can be hidden and its draw position in the current frame is listed. It also shows all 8 directions side by side, and saves the view as an image for evidence. Every input resolves through the catalog exactly as the game does, and each part says how it was found or why it is missing. | A swordsman with a full set plays Attack3 in all 8 directions side by side; stepping shows the body and weapon frame numbers in lock-step (FR-5); hiding the garment removes only the garment; turning the preview camera 180° shows the back of a unit facing South; an unknown headgear id is listed as missing with the reason. |

## 5. Success criteria

- A reference set (novice, swordsman with full equipment, knight on peco, a doram (summoner), poring, a 64-action monster, a town NPC) matches the reference viewer in parts, order, offsets and timing, in all 8 directions and under 4 camera turns, checked in the unit composer (FR-15) with saved images as evidence.
- The FR-14 budget is met in the Entity Sandbox stress mode, made of the reference set repeated on prontera.
- The camera's default view and limits match the original client (FR-12 evidence).

## 6. Assumptions and constraints

- The world facing is `UninarokDirection` (network direction, SPEC-0007); the camera's turn is applied by this spec only, never stored in the unit.
- Composition tables come from the lub files listed in the catalog reference §1, read through the GRF library; where a table is missing, the reference viewer's result is the reference.
- Sprites stay flat, camera-facing images played by the single runtime system of SPEC-0007 (no Animators).
- Only bodies, heads and equipment that SPEC-0008 imported are in scope; 4th classes are not (no Hercules support).
- Player and NPC lookup goes only through the catalog; this spec adds no new sprite import (the client's composition and shadow tables are read once at import time, plan D1).
- Dev-only code follows the project rule: stripped from release builds.
- The camera's limits are the client's defaults for a map without its own row in `viewpointtable.txt` (camera notes below): distance 230 to 400 world units, tilt 50° to 65° below the horizon, free turn. The default distance and tilt on entering a map are not in that file; they are measured from the original client at the same window size, side by side on prontera, and recorded with screenshots, which also confirm the limits. Per-map camera limits and locked indoor cameras (client tables not in our data) are out of scope: the camera turns freely on every map.

### Camera notes (research, 2026-11-21)

Sources: [Ragnarok Research Lab, Camera Controls](https://ragnarokresearchlab.github.io/rendering/camera-controls), [Coordinate Systems](https://ragnarokresearchlab.github.io/rendering/coordinate-systems/); roBrowser [`Camera.js`](https://github.com/vthibault/roBrowser/blob/master/src/Renderer/Camera.js), [`Renderer.js`](https://github.com/vthibault/roBrowser/blob/master/src/Renderer/Renderer.js).

Scale: one Ragnarok world unit is 0.1 Unity unit (a ground cube is 10 world units and 1 Unity unit, SPEC-0002 FR-7); a cell is 5 world units (0.5 Unity unit).

| Topic | Ragnarok Research Lab (camera controls page) | roBrowser source (`Camera.js`, `Renderer.js`) |
|---|---|---|
| Projection | Perspective, vertical angle "claimed to be 15 degrees"; near 10, far 1500 world units | Perspective, 15; near 1, far 1000 (own units) |
| Distance | About 25 world units per zoom step; nearest about 200 to 250 world units (22.5 Unity units suggested), classic farthest 400 (40 Unity units), Renewal maybe twice that | Zoom 2 to 150 (own units); wheel step 15; default from user preferences (not known) |
| Tilt | Per-map limits in degrees, example −40 to −65 starting at −65 | Default 60°, drag limits 10° to 90° |
| Turn | Per-map limits (`viewpointtable`), indoor maps locked (`indoorRswTable`) | −360° to +360°, default 0; 720° per screen width of drag |
| Easing | Not described | Eases towards the target turn, tilt and zoom each frame (rate 0.012 per ms) |
| Facing | Not described | `floor((turn + 22.5) / 45) mod 8` |

The client's own table settles the limits: `data/viewpointtable.txt` (community copies: [ROenglishRE](https://github.com/zackdreaver/ROenglishRE/blob/master/Ragnarok/data/viewpointtable.txt), [Ragnarok-OldTimes](https://github.com/JulioCF/Ragnarok-OldTimes/blob/master/emulador_oldtimes/data_oldtimes/viewpointtable.txt)) has one row per special map (`map# range# scope# range_IN# rotationFrom# rotationTo# rotation_IN# altitudeFrom# altitudeTo# altitude_IN#`) and a header with the defaults for every other map: range 230, scope 170 (so 230 to 400 world units), rotation −360 to 360, altitude −50 to −65. Tilt is therefore a small vertical orbit of 15°. The values on entering a map are not given for the default; hence the side-by-side measurement. Rough ranges found: distance about 22.5 to 40 Unity units in the classic client (maybe 80 in Renewal); tilt about 40° to 65° below the horizon depending on the map.

## 7. Open questions

All resolved on 2026-11-21 (owner accepted the proposed defaults; OQ-4 answered).

| # | Question | Decision |
|---|---|---|
| OQ-1 | Camera controls, default view and limits. | roBrowser's controls; default view and limits measured from the original client (FR-12, §6). |
| OQ-2 | Where the 8 facing sectors start. | Borders 22.5° either side of each direction (FR-6). |
| OQ-3 | Unit budget. | 100 fully equipped players + 200 monsters, unit update ≤ 2 ms, owner's PC (FR-14). |
| OQ-4 | Which clock drives parts with different frame times. | The body's (FR-5). |
| OQ-5 | Doram and mounts in the reference set. | In (success criteria). |
| OQ-6 | Perspective or orthographic. | Perspective, 15° vertical (FR-12): keeps the client's look and the approved ground placement. |
