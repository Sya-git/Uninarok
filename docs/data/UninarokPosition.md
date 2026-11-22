# UninarokPosition

| | |
|---|---|
| **Kind** | `readonly struct` (shared packet data) |
| **Namespace** | `Uninarok.Networking.Data` |
| **Wire size** | 3 bytes (`UninarokPosition.WireSize`) |
| **Hercules** | `WBUFPOS` / `RBUFPOS` in `src/map/clif.cpp` |
| **Used by** | `AUTHENTICATION_ACCEPTED` (0x02EB). Future: idle/spawn unit packets (`packet_idle_unit`, `packet_spawn_unit`) |

**Description:** Tile position plus facing direction, packed into 3 bytes. Used wherever a packet carries a single position with a direction. This is **not** the 6-byte move pair (source and destination, `WBUFPOS2`). That is a separate future type (`UninarokMoveData`).

## Fields

| Field | C# type | Range | Description |
|---|---|---|---|
| X | `ushort` | 0–1023 | Tile X (10 bits). |
| Y | `ushort` | 0–1023 | Tile Y (10 bits). |
| Direction | `UninarokDirection` (`byte` enum) | 0–15 on the wire, 0–7 used | Facing (4 bits). |

`UninarokDirection`: `0` North, `1` NorthWest, `2` West, `3` SouthWest, `4` South, `5` SouthEast, `6` East, `7` NorthEast. Values 8–15 are legal on the wire; they are kept as-is, not rejected.

The **sprite direction** (the ACT's, action index = `animation × 8 + direction`) is **not** this value: the ACT counts clockwise from the front (0 front/south, 1 south-west, 2 west, 3 north-west, 4 north, 5 north-east, 6 east, 7 south-east), the network counter-clockwise from north. At the default camera (looking north, turn 0):

```
shown = (4 - network) mod 8                      offset 4, turning sign -1
shown = (4 - network - cameraSector) mod 8       with the camera turned (SPEC-0009 FR-6)
cameraSector = floor((turn + 22.5) / 45) mod 8   turn in degrees, Unity yaw (90 = looking east)
```

Evidence (SPEC-0009 T0.3, 2026-11-22):

| Direction | Network (Hercules) | Sprite (client table and images) | `(4 - network) mod 8` |
|---|---|---|---|
| South | 4 (`dirx[4] = 0, diry[4] = -1`) | 0 `FRONT` | 0 |
| South-west | 3 (`-1, -1`) | 1 `FRONTLEFT` | 1 |
| West | 2 (`-1, 0`) | 2 `LEFT` | 2 |
| North | 0 (`0, 1`) | 4 `BACK` | 4 |
| East | 6 (`1, 0`) | 6 `RIGHT` | 6 |

Sources: Hercules 2026.09 `src/map/unit.cpp` (`dirx`, `diry`: the server's own meaning of the value it puts in the packet) and the client's `offsetitempos.lub` (`OffsetItemPos.Direction`), plus the imported body images. A live capture beside the original client was not made (no client on the implementer's side); the owner sees it in SPEC-0009 T6.4/T8.1.

## Wire layout (bit-packed, 3 bytes)

```
byte 0: XXXXXXXX            X bits 9..2
byte 1: XXYYYYYY            X bits 1..0, Y bits 9..4
byte 2: YYYYDDDD            Y bits 3..0, direction bits 3..0
```

Encode / decode (identical to Hercules):

```
p[0] = x >> 2
p[1] = ((x << 6) | ((y >> 4) & 0x3F)) & 0xFF
p[2] = ((y << 4) | (dir & 0x0F)) & 0xFF

x   = (p[0] << 2) | (p[1] >> 6)
y   = ((p[1] & 0x3F) << 4) | (p[2] >> 4)
dir = p[2] & 0x0F
```

## API

```csharp
namespace Uninarok.Networking.Data
{
    public readonly struct UninarokPosition
    {
        public const int WireSize = 3;
        public readonly ushort X;
        public readonly ushort Y;
        public readonly UninarokDirection Direction;

        public UninarokPosition(ushort x, ushort y, UninarokDirection direction);
        public static UninarokPosition Read(ref PacketReader r);   // consumes exactly 3 bytes
        public void Write(ref PacketWriter w);                      // writes exactly 3 bytes
    }
}
```

- `Read` never throws for any 3 bytes (every bit pattern is valid). It throws `PacketFormatException` only if fewer than 3 bytes remain.
- `Write` masks out-of-range values (`X`/`Y` to 10 bits, `Direction` to 4 bits), like Hercules. It never throws.
- No allocation. Lives in `Runtime/Data` (assembly `Uninarok.Networking`, plan section 3).

## Test vectors

| Bytes | X | Y | Direction |
|---|---:|---:|---|
| `25 8C 84` | 150 | 200 | 4 (South) |
| `00 00 00` | 0 | 0 | 0 (North) |
| `FF FF FF` | 1023 | 1023 | 15 |
| `00 40 01` | 1 | 0 | 1 (NorthWest) |

Tests: decode each vector; encode(decode(bytes)) == bytes; `Write` of X = 1024 equals `Write` of X = 0.
