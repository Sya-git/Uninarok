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

The same enum is the **sprite direction**: an ACT action index is `animation × 8 + direction` with these values (owner decision, SPEC-0007, 2026-11-19; there is no separate sprite direction enum). Camera rotation, if any, is applied to the value before the lookup by the display spec.

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
