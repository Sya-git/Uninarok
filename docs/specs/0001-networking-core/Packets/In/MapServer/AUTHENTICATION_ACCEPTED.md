# AUTHENTICATION_ACCEPTED

| | |
|---|---|
| **Direction** | Map server to client |
| **Hercules packet name** | `ZC_ACCEPT_ENTER2` (`ZC_ACCEPT_ENTER` in older packet versions) |
| **Packet ID** | `0x02EB` |
| **Declared size** | 13 bytes total, including the 2-byte packet ID (payload: 11 bytes) |
| **Status** | implemented |
| **Frequency** | cold (once per map entry) |
| **Verified** | Size and layout: Hercules `packet_authok` and length table 20220406 (13). Real capture: no |
| **Allocates** | no |

**Hercules usage:** Sent after successful map-server authentication. PACKETVER 20220406 selects `ZC_ACCEPT_ENTER2`.

**Description:** Provides the initial server tick, player position and direction, map view size, and font setting. In SPEC-0001 this packet moves the flow from `ConnectingToMap` into `LoadingMap` and fills `SessionContext.Entry` (`MapEntry`).

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| PacketId | `ushort` | 2 bytes | Header, `0x02EB`. Not part of the payload. |
| StartTimestamp | `uint` | 4 bytes | Initial server tick value. |
| Position | `UninarokPosition` | 3 bytes | Packed X, Y and facing direction. See [`data/UninarokPosition.md`](../../../../../data/UninarokPosition.md). |
| XSize | `byte` | 1 byte | Client/map view size value; Hercules currently sets it to 5. |
| YSize | `byte` | 1 byte | Client/map view size value; Hercules currently sets it to 5. |
| Font | `short` | 2 bytes | Character font value. |

**Parsing:**

```csharp
StartTimestamp = r.ReadUInt32();
Position       = UninarokPosition.Read(ref r);   // 3 bytes
XSize          = r.ReadByte();
YSize          = r.ReadByte();
Font           = r.ReadInt16();                  // Remaining == 0
```

**Sample (full packet):** `EB 02 04 03 02 01 25 8C 84 05 05 00 00` decodes to StartTimestamp `0x01020304`, Position (X 150, Y 200, South), XSize 5, YSize 5, Font 0.
