# CharInfo

- **Kind:** Shared character data structure
- **Current source type:** `CharacterServer.CHARACTER_DATA.CharInfo`
- **Serialized size:** 155 bytes
- **Description:** Character identity, progression, status, appearance, slot, and character-state data. The type is currently nested in the incoming packet implementation but is also consumed by gameplay code.

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
| Hp | `int` | 4 bytes | Current HP. |
| MaxHp | `int` | 4 bytes | Maximum HP. |
| Sp | `short` | 2 bytes | Current SP. |
| MaxSp | `short` | 2 bytes | Maximum SP. |
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

**Size check:** The field widths total 155 bytes. This type has no packet ID of its own; it is embedded in packets such as `CHARACTER_DATA` and `CHARACTER_CREATION_SUCCESS`.