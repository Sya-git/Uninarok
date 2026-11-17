# AccountSex

- **Kind:** Shared enum
- **Underlying type:** `byte`
- **Current source type:** `Uninarok.Networking.Packets.In.LoginServer.AccountSex`
- **Description:** Sex/account-kind values used by networking and gameplay. Packet fields are currently represented as bytes, so conversion between the wire byte and this enum is performed by consumers.

| Name | Value | Description |
|---|---:|---|
| `SEX_FEMALE` | `0` | Female account/character value. |
| `SEX_MALE` | `1` | Male account/character value. |
| `SEX_SERVER` | `2` | Server value. |

This document describes the existing shared value type; the enum has not been moved out of the networking source code.