# CharInfo

- **Kind:** Shared character data structure
- **Serialized size:** 175 bytes (PACKETVER 20220406)
- **Description:** Character identity, progression, status, appearance, slot, and character-state data. Used by several character-server packets and by gameplay code.

Fields are serialized and parsed in the following order. All integer fields use the packet reader's little-endian encoding. Fixed strings occupy their full wire width; zero bytes are omitted when converted to `FixedString32Bytes`.

| Field | Wire type | Size | Description |
|---|---:|---:|---|
| CharId | `int` | 4 bytes | Character identifier. |
| BaseExp | `ulong` | 8 bytes | Base experience points; read as two 32-bit words. |
| Zeny | `int` | 4 bytes | Character currency value. |
| JobExp | `ulong` | 8 bytes | Job experience points; read as two 32-bit words. |
| JobLevel | `int` | 4 bytes | Job level. |
| Opt1 | `int` | 4 bytes | Status/option value; meaning is not specified in this type. |
| Opt2 | `int` | 4 bytes | Status/option value; meaning is not specified in this type. |
| Option | `int` | 4 bytes | Character option flags/value. |
| Karma | `int` | 4 bytes | Karma value. |
| Manner | `int` | 4 bytes | Manner value. |
| StatusPoint | `short` | 2 bytes | Unspent status points. |
| Hp | `long` | 8 bytes | Current HP. |
| MaxHp | `long` | 8 bytes | Maximum HP. |
| Sp | `long` | 8 bytes | Current SP. |
| MaxSp | `long` | 8 bytes | Maximum SP. |
| DefaultWalkSpeed | `short` | 2 bytes | Default walking speed. |
| Class | `short` | 2 bytes | Character class value. |
| Hair | `short` | 2 bytes | Hair style value. |
| Body | `short` | 2 bytes | Body value. |
| Weapon | `short` | 2 bytes | Weapon appearance/value; source marks this field for verification. |
| BaseLevel | `short` | 2 bytes | Base level. |
| SkillPoint | `short` | 2 bytes | Unspent skill points. |
| LookHeadBottom | `short` | 2 bytes | Bottom headgear appearance value. |
| LookShield | `short` | 2 bytes | Shield appearance value. |
| LookHeadTop | `short` | 2 bytes | Top headgear appearance value. |
| LookHeadMid | `short` | 2 bytes | Mid headgear appearance value. |
| HairColor | `short` | 2 bytes | Hair color value. |
| ClothesColor | `short` | 2 bytes | Clothing color value. |
| Name | fixed byte string | 24 bytes | Character name. |
| Str | `byte` | 1 byte | Strength. |
| Agi | `byte` | 1 byte | Agility. |
| Vit | `byte` | 1 byte | Vitality. |
| Int | `byte` | 1 byte | Intelligence. |
| Dex | `byte` | 1 byte | Dexterity. |
| Luk | `byte` | 1 byte | Luck. |
| Slot | `short` | 2 bytes | Character slot value. |
| Rename | `ushort` | 2 bytes | Rename flag/count value. |
| MapName | fixed byte string | 16 bytes | Character's map name. |
| DeleteName | `int` | 4 bytes | Deletion-related value; exact semantics are not specified in this type. |
| LookRobe | `int` | 4 bytes | Robe appearance value. |
| SlotChangeEnabled | `int` | 4 bytes | Slot-change availability value. |
| RenameEnabled | `int` | 4 bytes | Rename availability value. |
| Sex | `byte` | 1 byte | Character sex value; see [`AccountSex.md`](AccountSex.md) for the corresponding shared enum values. |

**Size check:** The field widths total 175 bytes. This type has no packet ID of its own; it is embedded in packets such as `CHARACTER_DATA` and `CHARACTER_CREATION_SUCCESS`.

**Version dependence and correction (2026-11-17):** the size of this record depends on `PACKETVER` (`char_mmo_char_tobuf`, `src/char/char.cpp`). The 155-byte layout this document first described has 4-byte `Hp`/`MaxHp` and 2-byte `Sp`/`MaxSp`; it is the layout of a server built for an older version. From `PACKETVER_MAIN_NUM >= 20201007`, which includes 20220406, Hercules writes all four as 8-byte values, giving 175 bytes. This was found against a real server built for 20220406 (the character list came as records of 175 bytes) and confirmed in the Hercules source: `MAX_CHAR_BUF` is 175.
