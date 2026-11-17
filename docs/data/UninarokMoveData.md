# UninarokMoveData

| | |
|---|---|
| **Kind** | `readonly struct` (shared packet data) |
| **Namespace** | `Uninarok.Networking.Data` |
| **Wire size** | 6 bytes (`UninarokMoveData.WireSize`) |
| **Hercules** | `WBUFPOS2` / `RBUFPOS2` in `src/map/clif.cpp` |
| **Used by** | `PLAYER_MOVE` (0x0087), `UNIT_MOVE_ENTRY` (0x09FD) |

**Description:** A walk segment: source tile, destination tile and the sub-tile offset of the source, bit-packed into 6 bytes. A single position with direction is [`UninarokPosition`](UninarokPosition.md).

## Fields

| Field | C# type | Range | Description |
|---|---|---|---|
| SourceX | `ushort` | 0–1023 | Start tile X (10 bits). |
| SourceY | `ushort` | 0–1023 | Start tile Y (10 bits). |
| DestX | `ushort` | 0–1023 | Destination tile X (10 bits). |
| DestY | `ushort` | 0–1023 | Destination tile Y (10 bits). |
| SubX | `byte` | 0–15 | Sub-tile offset of the start, X (4 bits). Client adds `SubX * 0.0625 - 0.5`. |
| SubY | `byte` | 0–15 | Sub-tile offset of the start, Y (4 bits). Client adds `SubY * 0.0625 - 0.5`. |

## Wire layout (bit-packed, 6 bytes, 48 bits)

```
byte 0: SourceX bits 9..2
byte 1: SourceX bits 1..0 | SourceY bits 9..4
byte 2: SourceY bits 3..0 | DestX bits 9..6
byte 3: DestX bits 5..0   | DestY bits 9..8
byte 4: DestY bits 7..0
byte 5: SubX (4 bits)     | SubY (4 bits)
```

Decode (identical to Hercules `RBUFPOS2`):

```
sx = (p[0] << 2) | (p[1] >> 6)          sy = ((p[1] & 0x3F) << 4) | (p[2] >> 4)
dx = ((p[2] & 0x0F) << 6) | (p[3] >> 2) dy = ((p[3] & 0x03) << 8) | p[4]
subX = p[5] >> 4                         subY = p[5] & 0x0F
```

## API

```csharp
public readonly struct UninarokMoveData
{
    public const int WireSize = 6;
    public readonly ushort SourceX, SourceY, DestX, DestY;
    public readonly byte SubX, SubY;
    public static UninarokMoveData Read(ref PacketReader r);   // exactly 6 bytes
    public void Write(ref PacketWriter w);                      // masks out-of-range values
}
```

Same rules as `UninarokPosition`: no allocation, lives in `Runtime/Data` (assembly `Uninarok.Networking`, plan section 3), out-of-range values are masked on write.

## Test vectors

| Bytes | Source | Dest | Sub |
|---|---|---|---|
| `25 8C 82 60 CB 88` | (150, 200) | (152, 203) | (8, 8) |
| `00 00 00 00 00 00` | (0, 0) | (0, 0) | (0, 0) |
| `FF FF FF FF FF FF` | (1023, 1023) | (1023, 1023) | (15, 15) |
| `00 40 20 0C 04 56` | (1, 2) | (3, 4) | (5, 6) |
