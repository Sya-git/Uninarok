# Packet catalog (Hercules 2026.09, PACKETVER 20220406)

**Machine-generated reference, not normative.** It lists every packet Hercules defines for the map server that is not yet documented one-per-file. To implement one: create its doc from the template in `Packet-Implementation.md`, then follow the normal task flow. Sizes come from `packets2022_len_main.h` and the C structs in `packets_struct.h` (evaluated for PACKETVER 20220406); `size verified` means the struct size equals the table size.

## Server to client (map server): 168 packets defined by struct

| ID | Hercules name | Size | Check | Fields | Doc |
|---|---|---:|---|---|---|
| `0x00B0` | ZC_PAR_CHANGE | 8 | size verified | varID, count |  |
| `0x00B4` | ZC_SAY_DIALOG | -1 | variable (layout not auto-sized) | PacketLength, NpcID, ... |  |
| `0x00B4` | ZC_SAY_DIALOG2 | -1 | variable (layout not auto-sized) | PacketLength, NpcID, ... |  |
| `0x00B5` | ZC_WAIT_DIALOG | 6 | size verified | NpcID |  |
| `0x00B5` | ZC_WAIT_DIALOG2 | 6 | size verified | NpcID |  |
| `0x00BC` | ZC_STATUS_CHANGE_ACK | 6 | size verified | packetType, sp, ok, value |  |
| `0x00BE` | ZC_STATUS_CHANGE | 5 | size verified | statusID, value |  |
| `0x00E1` | ZC_ROLE_CHANGE | 30 | size verified | packetType, flag, name[NAME_LENGTH] |  |
| `0x00FA` | ZC_ACK_MAKE_GROUP | 3 | size verified | result |  |
| `0x0105` | ZC_DELETE_MEMBER_FROM_GROUP | 31 | size verified | AID, characterName[NAME_LENGTH], result |  |
| `0x0107` | ZC_NOTIFY_POSITION_TO_GROUPM | 10 | size verified | AID, xPos, yPos |  |
| `0x0109` | ZC_NOTIFY_CHAT_PARTY | -1 | variable (layout not auto-sized) | PacketLength, AID, ... |  |
| `0x010F` | ZC_SKILLINFO_LIST | -1 | variable (layout not auto-sized) | packetType, packetLength, ... |  |
| `0x0110` | ZC_ACK_TOUSESKILL | 14 | size verified | packetType, skillId, btype, itemId, flag, cause |  |
| `0x0111` | ZC_ADD_SKILL | 39 | size verified | packetType, skill |  |
| `0x0115` | ZC_NOTIFY_SKILL_POSITION | 35 | size verified | SKID, AID, targetID, startTime, attackMT, attackedMT, xPos, yPos, damage, level, count, action |  |
| `0x0117` | ZC_NOTIFY_GROUNDSKILL | 18 | size verified | SKID, AID, level, xPos, yPos, startTime |  |
| `0x0121` | ZC_NOTIFY_CARTITEM_COUNTINFO | 14 | size verified | curCount, maxCount, curWeight, maxWeight |  |
| `0x012C` | cart_additem_ack | 3 | size verified | result |  |
| `0x0131` | ZC_STORE_ENTRY | 86 | MISMATCH struct 1506 / table 86 | packetType, makerAID, storeName[MESSAGE_SIZE] |  |
| `0x0139` | ZC_ATTACK_FAILURE_FOR_DISTANCE | 16 | size verified | targetAID, targetXPos, targetYPos, xPos, yPos, currentAttRange |  |
| `0x013A` | ZC_ATTACK_RANGE | 4 | size verified | currentAttRange |  |
| `0x0147` | ZC_AUTORUN_SKILL | 39 | size verified | packetType, skill_id, skill_type, skill_lv, skill_sp, skill_range, skill_name[NAME_LENGTH], up_flag |  |
| `0x014C` | ZC_MYGUILD_BASIC_INFO | -1 | variable (layout not auto-sized) | PacketLength, ... |  |
| `0x0160` | ZC_POSITION_INFO | -1 | variable (layout not auto-sized) | PacketLength, ..., positionID, right, ranking, payRate |  |
| `0x0162` | ZC_GUILD_SKILLINFO | -1 | variable (layout not auto-sized) | PacketLength, skillPoint, ... |  |
| `0x0166` | ZC_POSITION_ID_NAME_INFO | -1 | variable (layout not auto-sized) | PacketLength, ..., positionID, posName[NAME_LENGTH] |  |
| `0x0191` | ZC_TALKBOX_CHATCONTENTS | 27 | size not auto-computed | aid, message[TALKBOX_MESSAGE_SIZE] |  |
| `0x0196` | sc_notick | 9 | size verified | index, AID, state |  |
| `0x0196` | status_change_end | 9 | size verified | index, AID, state |  |
| `0x019E` | ZC_START_CAPTURE | 2 | size verified |  |  |
| `0x01A0` | ZC_TRYCAPTURE_MONSTER | 3 | size verified | result |  |
| `0x01A2` | ZC_PROPERTY_PET | 37 | size verified | szName[NAME_LENGTH], bModified, nLevel, nFullness, nRelationship, ITID, job |  |
| `0x01A4` | ZC_CHANGESTATE_PET | 11 | size verified | type, GID, data |  |
| `0x01AD` | ZC_MAKINGARROW_LIST | -1 | variable (layout not auto-sized) | packetType, packetLength, ... |  |
| `0x01B3` | ZC_SHOW_IMAGE | 67 | size verified | packetType, image[64], type |  |
| `0x01C9` | graffiti_entry | 97 | size verified | AID, creatorAID, xPos, yPos, job, isVisible, isContens, msg[80] |  |
| `0x01D0` | ZC_SPIRITS | 8 | size verified | AID, num |  |
| `0x01D3` | ZC_SOUND | 35 | size verified | name[NAME_LENGTH], act, term, AID |  |
| `0x01DE` | ZC_NOTIFY_SKILL | 33 | size verified | SKID, AID, targetID, startTime, attackMT, attackedMT, damage, level, count, action |  |
| `0x01E1` | ZC_SPIRITS2 | 8 | size verified | AID, num |  |
| `0x0229` | ZC_STATE_CHANGE | 15 | size verified | packetType, AID, bodyState, healthState, effectState, isPKModeON |  |
| `0x025A` | ZC_MAKINGITEM_LIST | -1 | variable (layout not auto-sized) | packetType, packetLength, makeItem, ... |  |
| `0x02C5` | ZC_PARTY_JOIN_REQ_ACK | 30 | size verified | characterName[NAME_LENGTH], result |  |
| `0x02C6` | ZC_PARTY_JOIN_REQ | 30 | size verified | GRID, groupName[NAME_LENGTH] |  |
| `0x02C9` | ZC_PARTY_CONFIG | 3 | size verified | packetType, denyPartyInvites |  |
| `0x02D3` | notify_bounditem | 4 | size verified | index |  |
| `0x02EB` | authok | 13 | size verified | startTime, PosDir[3], xSize, ySize, font | [AUTHENTICATION_ACCEPTED](In/MapServer/AUTHENTICATION_ACCEPTED.md) |
| `0x02F7` | ZC_UPDATE_GDID | 47 | size verified | guildId, emblemVersion, mode, isMaster, interSid, guildName[NAME_LENGTH], masterGID |  |
| `0x043D` | ZC_SKILL_POSTDELAY | 8 | size verified | SKID, DelayTM |  |
| `0x043F` | status_change2 | 25 | size verified | index, AID, state, Left, val1, val2, val3 |  |
| `0x0442` | ZC_SKILL_SELECT_REQUEST | -1 | variable (layout not auto-sized) | packetType, packetLength, flag, ... |  |
| `0x07E1` | ZC_SKILLINFO_UPDATE2 | 15 | size verified | packetType, id, inf, level, sp, range2, upFlag |  |
| `0x07FD` | ZC_BROADCASTING_SPECIAL_ITEM_OBTAIN_item | -1 | variable (Hercules sends 39) | PacketLength, type, ItemID, len, Name[NAME_LENGTH], boxItemID_len, BoxItemID |  |
| `0x07FD` | item_drop_announce | -1 | variable (Hercules sends 59) | PacketLength, type, ItemID, len, Name[NAME_LENGTH], monsterNameLen, monsterName[NAME_LENGTH] |  |
| `0x07FE` | ZC_PLAY_NPC_BGM | 26 | size verified | bgm[NAME_LENGTH] |  |
| `0x080E` | ZC_NOTIFY_HP_TO_GROUPM | 14 | size verified | AID, hp, maxhp |  |
| `0x0814` | ZC_BUYING_STORE_ENTRY | 86 | MISMATCH struct 1506 / table 86 | packetType, makerAID, storeName[MESSAGE_SIZE] |  |
| `0x0816` | ZC_DISAPPEAR_BUYING_STORE_ENTRY | 6 | size verified | packetType, makerAID |  |
| `0x0849` | ZC_SE_PC_BUY_CASHITEM_RESULT | 16 | size verified | packetType, itemId, result, cashPoints, kafraPoints |  |
| `0x08C8` | damage | 34 | size verified | GID, targetGID, startTime, attackMT, attackedMT, damage, is_sp_damaged, count, action, leftDamage | [DAMAGE](In/MapServer/DAMAGE.md) |
| `0x08D6` | script_clear | 6 | size verified | NpcID |  |
| `0x08D7` | bgqueue_register | 28 | size verified | type, bg_name[NAME_LENGTH] |  |
| `0x08D8` | bgqueue_ack | 27 | size verified | type, bg_name[NAME_LENGTH] |  |
| `0x08D9` | bgqueue_notify_entry | 30 | size verified | name[NAME_LENGTH], position |  |
| `0x08DB` | bgqueue_notice_delete | 27 | size verified | type, bg_name[NAME_LENGTH] |  |
| `0x08DF` | bgqueue_battlebegins | 50 | size verified | bg_name[NAME_LENGTH], game_name[NAME_LENGTH] |  |
| `0x090A` | bgqueue_checkstate | 26 | size verified | bg_name[NAME_LENGTH] |  |
| `0x096D` | ZC_MERGE_ITEM_OPEN | -1 | variable (layout not auto-sized) | packetType, packetLen, ... |  |
| `0x096F` | ZC_ACK_MERGE_ITEM | 7 | size verified | packetType, index, amount, reason |  |
| `0x097B` | ZC_PERSONAL_INFOMATION | -1 | variable (layout not auto-sized) | packetType, length, total_exp, total_death, total_drop, ... |  |
| `0x0983` | status_change | 29 | size verified | index, AID, state, Total, Left, val1, val2, val3 | [STATUS_CHANGE](In/MapServer/STATUS_CHANGE.md) |
| `0x098A` | ZC_CLANINFO | -1 | variable (Hercules sends 74) | PacketLength, ClanID, ClanName[NAME_LENGTH], MasterName[NAME_LENGTH], Map[MAP_NAME_LENGTH_EXT], AllyCount, ... |  |
| `0x0999` | ZC_REQ_WEAR_EQUIP_ACK | 11 | size verified | index, wearLocation, wItemSpriteNumber, result |  |
| `0x099B` | maptypeproperty2 | 8 | size not auto-computed | type, ..., ..., ..., ..., ..., ..., ..., ..., ..., ..., ..., ..., ... |  |
| `0x09A6` | banking_check | 12 | size verified | Money, Reason |  |
| `0x09A8` | banking_deposit_ack | 16 | size verified | Reason, Money, Balance |  |
| `0x09AA` | banking_withdraw_ack | 16 | size verified | Reason, Money, Balance |  |
| `0x09C1` | ZC_C_MARKERINFO | 10 | size verified | AID, xPos, yPos |  |
| `0x09CA` | skill_entry | -1 | variable (Hercules sends 23) | PacketLength, AID, creatorAID, xPos, yPos, job, RadiusRange, isVisible, level | [SKILL_UNIT_ENTRY](In/MapServer/SKILL_UNIT_ENTRY.md) |
| `0x09CB` | ZC_USE_SKILL | 17 | size verified | SKID, level, targetAID, srcAID, result |  |
| `0x09DE` | ZC_WHISPER | -1 | variable (layout not auto-sized) | PacketLength, senderGID, sender[NAME_LENGTH], isAdmin, ... |  |
| `0x09FD` | unit_walking | -1 | variable (Hercules sends 114) | PacketLength, objecttype, AID, GID, speed, bodyState, healthState, effectState, job, head, weapon, shield, ... | [UNIT_MOVE_ENTRY](In/MapServer/UNIT_MOVE_ENTRY.md) |
| `0x09FE` | spawn_unit | -1 | variable (Hercules sends 107) | PacketLength, objecttype, AID, GID, speed, bodyState, healthState, effectState, job, head, weapon, shield, ... | [UNIT_NEW_ENTRY](In/MapServer/UNIT_NEW_ENTRY.md) |
| `0x09FF` | idle_unit | -1 | variable (Hercules sends 108) | PacketLength, objecttype, AID, GID, speed, bodyState, healthState, effectState, job, head, weapon, shield, ... | [UNIT_STAND_ENTRY](In/MapServer/UNIT_STAND_ENTRY.md) |
| `0x0A0E` | ZC_BATTLEFIELD_NOTIFY_HP | 14 | size verified | AID, hp, maxhp |  |
| `0x0A15` | ZC_GOLDPCCAFE_POINT | 12 | size verified | isActive, mode, point, playedTime |  |
| `0x0A17` | ZC_DYNAMICNPC_CREATE_RESULT | 6 | size verified | result |  |
| `0x0A1E` | ZC_ACK_CLOSE_ROULETTE | 3 | size verified | packetType, result |  |
| `0x0A30` | ZC_ACK_REQNAMEALL | 106 | size verified | gid, name[NAME_LENGTH], party_name[NAME_LENGTH], guild_name[NAME_LENGTH], position_name[NAME_LENGTH], title... | [UNIT_NAME_ACK](In/MapServer/UNIT_NAME_ACK.md) |
| `0x0A3B` | ZC_EQUIPMENT_EFFECT | -1 | variable (layout not auto-sized) | packetType, packetLength, aid, status, ... |  |
| `0x0A3F` | ZC_UPDATE_CARDSLOT | 11 | size verified | packetType, wearState, cardSlot, itemId, equipFlag |  |
| `0x0A4E` | ZC_RANDOM_COMBINE_ITEM_UI_OPEN | 6 | size verified | packetType, itemId |  |
| `0x0A50` | ZC_ACK_RANDOM_COMBINE_ITEM | 4 | size verified | packetType, result |  |
| `0x0A51` | ZC_CHECKNAME | 34 | size verified | CharId, Class, BaseLevel, Name[24] |  |
| `0x0A53` | ZC_ACK_UPLOAD_MACRO_DETECTOR | 10 | size verified | captchaKey[4], captchaFlag |  |
| `0x0A55` | ZC_COMPLETE_UPLOAD_MACRO_DETECTOR_CAPTCHA | 2 | size verified |  |  |
| `0x0A57` | ZC_ACK_APPLY_MACRO_DETECTOR | 6 | size verified | status |  |
| `0x0A58` | ZC_APPLY_MACRO_DETECTOR | 8 | size verified | imageSize, captchaKey[4] |  |
| `0x0A59` | ZC_APPLY_MACRO_DETECTOR_CAPTCHA | -1 | variable (layout not auto-sized) | PacketLength, captchaKey[4], ... |  |
| `0x0A5B` | ZC_REQ_ANSWER_MACRO_DETECTOR | 7 | size verified | retryCount, timeout |  |
| `0x0A5D` | ZC_CLOSE_MACRO_DETECTOR | 6 | size verified | status |  |
| `0x0A6A` | ZC_ACK_PREVIEW_MACRO_DETECTOR | 12 | size verified | captchaFlag, imageSize, captchaKey[4] |  |
| `0x0A6B` | ZC_PREVIEW_MACRO_DETECTOR_CAPTCHA | -1 | variable (layout not auto-sized) | PacketLength, captchaKey[4], ... |  |
| `0x0A6D` | ZC_ACK_PLAYER_AID_IN_RANGE | -1 | variable (layout not auto-sized) | PacketLength, ... |  |
| `0x0A78` | ZC_VIEW_CAMERAINFO | 15 | size verified | packetType, action, range, rotation, latitude |  |
| `0x0AA0` | ZC_OPEN_REFINING_UI | 2 | size verified | packetType |  |
| `0x0AA2` | ZC_REFINING_MATERIAL_LIST | -1 | variable (layout not auto-sized) | packetType, packetLength, itemIndex, blacksmithBlessing, ... |  |
| `0x0AA7` | ZC_MOVE_ITEM_FAILED | 6 | size verified | packetType, itemIndex, itemCount |  |
| `0x0AB4` | ZC_RANDOM_UPGRADE_ITEM_UI_OPEN | 6 | size verified | packetType, itemId |  |
| `0x0AB7` | ZC_ACK_RANDOM_UPGRADE_ITEM | 4 | size verified | packetType, result |  |
| `0x0ABD` | ZC_NOTIFY_MEMBERINFO_TO_GROUPM | 10 | size verified | AID, job, level |  |
| `0x0ADA` | ZC_BROADCAST_ITEMREFINING_RESULT | 32 | size verified | packetType, name[NAME_LENGTH], itemId, refine_level, status |  |
| `0x0ADB` | ZC_DEBUGMSG | -1 | variable (layout not auto-sized) | packetType, packetLength, color, ... |  |
| `0x0ADD` | dropflooritem | 24 | size verified | ITAID, ITID, type, IsIdentified, xPos, yPos, subX, subY, count, showdropeffect, dropeffectmode | [ITEM_FALL_ENTRY](In/MapServer/ITEM_FALL_ENTRY.md) |
| `0x0ADF` | ZC_ACK_REQNAMEALL_NPC | 58 | size verified | gid, groupId, name[NAME_LENGTH], title[NAME_LENGTH] |  |
| `0x0AE2` | ZC_UI_OPEN | 7 | size verified | UIType, data |  |
| `0x0AE7` | ZC_ADVENTURER_AGENCY_JOIN_REQ | 38 | size verified | packetType, GRID, AID, groupName[NAME_LENGTH], level, job |  |
| `0x0AF6` | ZC_ACK_RANKING | 88 | size verified | packetType, rankType, chars[10], points[10], myPoints |  |
| `0x0AFA` | ZC_ADVENTURER_AGENCY_JOIN_RESULT | 58 | size verified | packetType, player_name[NAME_LENGTH], party_name[NAME_LENGTH], AID, result |  |
| `0x0AFB` | ZC_AUTOSPELLLIST | -1 | variable (layout not auto-sized) | packetType, packetLength, ... |  |
| `0x0B08` | ZC_INVENTORY_START | -1 | variable (layout not auto-sized) | packetType, packetLength, invType, ... |  |
| `0x0B0B` | ZC_INVENTORY_END | 4 | size verified | packetType, invType, flag |  |
| `0x0B15` | ZC_ACK_OPEN_MSGBOX_EXTEND_BODYITEM_SIZE | 7 | size verified | packetType, result, itemId |  |
| `0x0B17` | ZC_ACK_EXTEND_BODYITEM_SIZE | 3 | size verified | packetType, result |  |
| `0x0B18` | ZC_EXTEND_BODYITEM_SIZE | 4 | size verified | packetType, expansionSize | [INVENTORY_EXPANSION_INFO](In/MapServer/INVENTORY_EXPANSION_INFO.md) |
| `0x0B1A` | ZC_USESKILL_ACK | 29 | size verified | packetType, srcId, dstId, x, y, skillId, element, delayTime, disposable, attackMT |  |
| `0x0B1B` | ZC_NOTIFY_ACTORINIT | 2 | size verified | packetType |  |
| `0x0B1D` | ZC_PING_LIVE | 2 | size verified | packetType |  |
| `0x0B27` | ZC_GUILD_AGIT_INFO | -1 | variable (layout not auto-sized) | packetType, packetLength, ... |  |
| `0x0B2D` | ZC_REQ_ACK_AGIT_INVESTMENT | 11 | size verified | packetType, castle_id, economy, defense |  |
| `0x0B2E` | ZC_REQ_ACK_MOVE_GUILD_AGIT | 4 | size verified | packetType, result |  |
| `0x0B36` | ZC_GUILD_EMBLEM_IMG | -1 | variable (layout not auto-sized) | packetType, packetLength, result, guild_id, emblem_id, ... |  |
| `0x0B37` | ZC_EQUIPWIN_MICROSCOPE | -1 | variable (layout not auto-sized) | PacketLength, characterName[NAME_LENGTH], job, head, accessory, accessory2, accessory3, robe, headpalette, ... |  |
| `0x0B3D` | ZC_PC_PURCHASE_ITEMLIST_FROMMC | -1 | variable (layout not auto-sized) | packetType, packetLength, AID, venderId, ... |  |
| `0x0B3F` | ZC_ACK_ADD_ITEM_RODEX | 64 | size verified | result, index, count, itemId, type, IsIdentified, IsDamaged, slot, optionData[MAX_ITEM_OPTIONS], weight, fa... |  |
| `0x0B40` | ZC_PC_PURCHASE_MYITEMLIST | -1 | variable (layout not auto-sized) | packetType, packetLength, AID, ... |  |
| `0x0B41` | ZC_ITEM_PICKUP_ACK | 70 | size verified | Index, count, nameid, IsIdentified, IsDamaged, slot, location, type, result, HireExpireDate, bindOnEquipTyp... | [ITEM_PICKUP_ACK](In/MapServer/ITEM_PICKUP_ACK.md) |
| `0x0B42` | ZC_ADD_EXCHANGE_ITEM | 62 | size verified | packetType, itemId, itemType, amount, identified, damaged, slot, option_data[MAX_ITEM_OPTIONS], location, l... |  |
| `0x0B43` | ZC_CHANGE_ITEM_OPTION | 48 | size verified | packetType, index, isDamaged, slot, option_data[MAX_ITEM_OPTIONS], refiningLevel, grade |  |
| `0x0B44` | ZC_ADD_ITEM_TO_STORE | 58 | size verified | packetType, index, amount, itemId, itemType, identified, damaged, slot, option_data[MAX_ITEM_OPTIONS], refi... |  |
| `0x0B45` | ZC_ADD_ITEM_TO_CART | 58 | size verified | packetType, index, amount, itemId, itemType, identified, damaged, slot, option_data[MAX_ITEM_OPTIONS], refi... |  |
| `0x0B47` | ZC_CHANGE_GUILD | 14 | size verified | packetType, guild_id, emblem_id, AID |  |
| `0x0B4E` | ZC_NPC_MARKET_PURCHASE_RESULT | -1 | variable (layout not auto-sized) | PacketLength, result, ... |  |
| `0x0B5A` | ZC_GRADE_ENCHANT_MATERIAL_LIST | -1 | variable (layout not auto-sized) | PacketLength, index, success_chance, blessing_info, protect_itemid, protect_amount, ... |  |
| `0x0B5D` | ZC_GRADE_ENCHANT_ACK | 10 | size verified | index, grade, result |  |
| `0x0B5E` | ZC_GRADE_ENCHANT_BROADCAST_RESULT | 33 | size verified | packetType, name[NAME_LENGTH], itemId, grade, status |  |
| `0x0B63` | ZC_ACK_READ_RODEX | -1 | variable (layout not auto-sized) | PacketLength, opentype, MailID, TextcontentsLength, zeny, ItemCnt, ... |  |
| `0x0B64` | ZC_SEARCH_STORE_INFO_ACK | -1 | variable (layout not auto-sized) | packetType, packetLength, firstPage, nextPage, usesCount, ... |  |
| `0x0B65` | ZC_REPAIRITEMLIST | -1 | variable (layout not auto-sized) | packetType, packetLength, ... |  |
| `0x0B67` | ZC_ITEM_PICKUP_PARTY | 33 | size verified | packetType, AID, itemId, identified, damaged, slot, location, itemType, refine, grade |  |
| `0x0B69` | ZC_NOTIFY_EFFECT3 | 18 | size verified | packetType, aid, effectId, num |  |
| `0x0B6E` | ZC_SE_CASHSHOP_OPEN | 14 | size verified | packetType, cashPoints, kafraPoints, tab |  |
| `0x0B73` | ZC_SOULENERGY | 8 | size verified | AID, num |  |
| `0x0B77` | ZC_PC_PURCHASE_ITEMLIST | -1 | variable (layout not auto-sized) | packetType, packetLength, ... |  |
| `0x0B78` | ZC_NPC_BARTER_MARKET_ITEMINFO | -1 | variable (layout not auto-sized) | packetType, packetLength, ... |  |
| `0x0B79` | ZC_NPC_EXPANDED_BARTER_MARKET_ITEMINFO | -1 | variable (layout not auto-sized) | packetType, packetLength, items_count, ... |  |
| `0x0B7A` | ZC_NPC_MARKET_OPEN | -1 | variable (layout not auto-sized) | packetType, packetLength, ... |  |
| `0x0B7B` | ZC_GUILD_INFO | 118 | size verified | GDID, level, userNum, maxUserNum, userAverageLevel, exp, maxExp, point, honor, virtue, emblemVersion, guild... |  |
| `0x0B7C` | ZC_BAN_LIST | -1 | variable (layout not auto-sized) | packetType, packetLen, ... |  |
| `0x0B7D` | ZC_MEMBERMGR_INFO | -1 | variable (layout not auto-sized) | packetLength, ... |  |
| `0x0B8F` | ZC_OPEN_REFORM_UI | 6 | size verified | ITID |  |
| `0x0B92` | ZC_ITEM_REFORM_ACK | 5 | size verified | index, result |  |
| `0x0B9A` | ZC_UI_OPEN2 | 11 | size verified | UIType, data |  |
| `0x0B9F` | ZC_RESPONSE_ENCHANT | 10 | size verified | msgId, ITID |  |
| `0x0BA1` | ZC_DIALOG_TEXT_ALIGN | 3 | size verified | align |  |
| `0x0BA4` | ZC_PROPERTY_HOMUN | 85 | size verified | packetType, name[NAME_LENGTH], flags, level, hunger, intimacy, atk2, matk, hit, crit, def, mdef, flee, amot... |  |
| `0x0BAE` | ZC_ACK_TAKEOFF_EQUIP_ALL | 3 | size verified | result |  |

Packets that Hercules builds by hand in `clif.cpp` (without a struct) are not listed here, for example `PLAYER_MOVE` (0x0087), `UNIT_VANISH` (0x0080), `UNIT_STOP_MOVE` (0x0088), `ITEM_DISAPPEAR` (0x00A1); the ones the client needs first are documented individually. Find others with `WFIFOW(fd, 0) = 0x....` or `// ZC_...` comments in `clif.cpp`.

## Client to server (map server): 303 handlers

One row per Hercules handler: the ID registered for PACKETVER 20220406 (the shuffle table wins over older registrations). `Offsets` are the field positions Hercules reads (from `packet()`); the first field is at 2.

| ID | Hercules name | Handler | Offsets | Size | Doc |
|---|---|---|---|---:|---|
| `0x007D` |  | pLoadEndAck | 0 | 2 | [MAP_LOADED](Out/MapServer/MAP_LOADED.md) |
| `0x0090` |  | pNpcClicked | 2 | 7 | [NPC_CLICK](Out/MapServer/NPC_CLICK.md) |
| `0x0096` |  | pWisMessage | 2,4,28 | -1 |  |
| `0x0099` |  | pBroadcast | 2,4 | -1 |  |
| `0x00AB` |  | pUnequipItem | 2 | 4 |  |
| `0x00B2` |  | pRestart | 2 | 3 | [RESTART](Out/MapServer/RESTART.md) |
| `0x00B8` |  | pNpcSelectMenu | 2,6 | 7 |  |
| `0x00B9` |  | pNpcNextClicked | 2 | 6 |  |
| `0x00BB` |  | pStatusUp | 2,4 | 5 |  |
| `0x00BF` |  | pEmotion | 2 | 3 |  |
| `0x00C1` |  | pHowManyConnections | 0 | 2 |  |
| `0x00C5` |  | pNpcBuySellSelected | 2,6 | 7 |  |
| `0x00C8` |  | pNpcBuyListSend | 2,4 | -1 |  |
| `0x00C9` |  | pNpcSellListSend | 2,4 | -1 |  |
| `0x00CC` |  | pGMKick | 2 | 6 |  |
| `0x00CE` |  | pGMKickAll | 0 | 2 |  |
| `0x00CF` |  | pPMIgnore | 2,26 | 27 |  |
| `0x00D0` |  | pPMIgnoreAll | 2 | 3 |  |
| `0x00D3` |  | pPMIgnoreList | 0 | 2 |  |
| `0x00D5` |  | pCreateChatRoom | 2,4,6,7,15 | -1 |  |
| `0x00D9` |  | pChatAddMember | 2,6 | 14 |  |
| `0x00DE` |  | pChatRoomStatusChange | 2,4,6,7,15 | -1 |  |
| `0x00E0` |  | pChangeChatOwner | 2,6 | 30 |  |
| `0x00E2` |  | pKickFromChat | 2 | 26 |  |
| `0x00E3` |  | pChatLeave | 0 | 2 |  |
| `0x00E4` |  | pTradeRequest | 2 | 6 |  |
| `0x00E6` |  | pTradeAck | 2 | 3 |  |
| `0x00E8` |  | pTradeAddItem | 2,4 | 8 |  |
| `0x00EB` |  | pTradeOk | 0 | 2 |  |
| `0x00ED` |  | pTradeCancel | 0 | 2 |  |
| `0x00EF` |  | pTradeCommit | 0 | 2 |  |
| `0x00F3` |  | pGlobalMessage | 2,4 | -1 | [GLOBAL_CHAT](Out/MapServer/GLOBAL_CHAT.md) |
| `0x00F9` |  | pCreateParty | 2 | 26 |  |
| `0x00FC` |  | pPartyInvite | 2 | 6 |  |
| `0x00FF` |  | pReplyPartyInvite | 2,6 | 10 |  |
| `0x0100` |  | pLeaveParty | 0 | 2 |  |
| `0x0103` |  | pRemovePartyMember | 2,6 | 30 |  |
| `0x0108` |  | pPartyMessage | 2,4 | -1 |  |
| `0x0112` |  | pSkillUp | 2 | 4 |  |
| `0x0118` |  | pStopAttack | 0 | 2 |  |
| `0x011B` |  | pUseSkillMap | 2,4 | 20 |  |
| `0x011D` |  | pRequestMemo | 0 | 2 |  |
| `0x0126` |  | pPutItemToCart | 2,4 | 8 |  |
| `0x0127` |  | pGetItemFromCart | 2,4 | 8 |  |
| `0x0128` |  | pMoveFromKafraToCart | 2,4 | 8 |  |
| `0x0129` |  | pMoveToKafraFromCart | 2,4 | 8 |  |
| `0x012A` |  | pRemoveOption | 0 | 2 |  |
| `0x012E` |  | pCloseVending | 0 | 2 |  |
| `0x0130` |  | pVendingListReq | 2 | 6 |  |
| `0x0134` |  | pPurchaseReq | 2,4,8 | -1 |  |
| `0x0140` |  | pMapMove | 2,18,20 | 22 |  |
| `0x0143` |  | pNpcAmountInput | 2,6 | 10 |  |
| `0x0146` |  | pNpcCloseClicked | 2 | 6 |  |
| `0x0149` |  | pGMReqNoChat | 2,6,7 | 9 |  |
| `0x014D` |  | pGuildCheckMaster | 0 | 2 |  |
| `0x014F` |  | pGuildRequestInfo | 2 | 6 |  |
| `0x0151` |  | pGuildRequestEmblem1 |  | 6 |  |
| `0x0153` |  | pGuildChangeEmblem | 2,4 | -1 |  |
| `0x0155` |  | pGuildChangeMemberPosition | 2 | -1 |  |
| `0x0159` |  | pGuildLeave | 2,6,10,14 | 54 |  |
| `0x015B` |  | pGuildExpulsion | 2,6,10,14 | 54 |  |
| `0x015D` |  | pGuildBreak | 2 | 42 |  |
| `0x0161` |  | pGuildChangePositionInfo | 2 | -1 |  |
| `0x0165` |  | pCreateGuild | 6 | 30 |  |
| `0x0168` |  | pGuildInvite | 2 | 14 |  |
| `0x016B` |  | pGuildReplyInvite | 2,6 | 10 |  |
| `0x016E` |  | pGuildChangeNotice | 2,6,66 | 186 |  |
| `0x0170` |  | pGuildRequestAlliance | 2 | 14 |  |
| `0x0172` |  | pGuildReplyAlliance | 2,6 | 10 |  |
| `0x0178` |  | pItemIdentify | 2 | 4 |  |
| `0x017A` |  | pUseCard | 2 | 4 |  |
| `0x017C` |  | pInsertCard | 2,4 | 6 |  |
| `0x017E` |  | pGuildMessage | 2,4 | -1 |  |
| `0x0180` |  | pGuildOpposition | 2 | 6 |  |
| `0x0183` |  | pGuildDelAlliance | 2,6 | 10 |  |
| `0x018A` |  | pQuitGame | 0 | 4 | [QUIT_GAME](Out/MapServer/QUIT_GAME.md) |
| `0x018E` |  | pProduceMix | 2,4,6,8 | 18 |  |
| `0x0193` |  | pCloseKafra | 0 | 2 |  |
| `0x0197` |  | pResetChar | 2 | 4 |  |
| `0x0198` |  | pGMChangeMapType | 2,4,6 | 8 |  |
| `0x019C` |  | pLocalBroadcast | 2,4 | -1 |  |
| `0x019D` |  | pGMHide | 0 | 6 |  |
| `0x019F` |  | pCatchPet | 2 | 6 |  |
| `0x01A1` |  | pPetMenu | 2 | 3 |  |
| `0x01A5` |  | pChangePetName | 2 | 26 |  |
| `0x01A7` |  | pSelectEgg | 2 | 4 |  |
| `0x01A9` |  | pSendEmotion | 2 | 6 |  |
| `0x01AE` |  | pSelectArrow | 2 | 6 |  |
| `0x01AF` |  | pChangeCart | 2 | 4 |  |
| `0x01B2` |  | pOpenVending | 2,4,84,85 | -1 |  |
| `0x01BB` |  | pGMShift | 2 | 26 |  |
| `0x01BD` |  | pGMRecall | 2 | 26 |  |
| `0x01C0` |  | pReqRemainTime |  | 2 |  |
| `0x01CE` |  | pAutoSpell | 2 | 6 |  |
| `0x01D5` |  | pNpcStringInput | 2,4,8 | -1 |  |
| `0x01DF` |  | pGMReqAccountName | 2 | 6 |  |
| `0x01E7` |  | pNoviceDoriDori | 0 | 2 |  |
| `0x01E8` |  | pCreateParty2 | 2 | 28 |  |
| `0x01ED` |  | pNoviceExplosionSpirits | 0 | 2 |  |
| `0x01F7` |  | pAdopt_reply | 0 | 14 |  |
| `0x01F9` |  | pAdopt_request | 0 | 6 |  |
| `0x01FD` |  | pRepairItem1 | 2 | 25 |  |
| `0x0202` |  | pFriendsListAdd | 2 | 26 |  |
| `0x0203` |  | pFriendsListRemove | 2,6 | 10 |  |
| `0x0208` |  | pFriendsListReply | 2,6,10 | 14 |  |
| `0x020F` |  | pPVPInfo | 2,6 | None |  |
| `0x0212` |  | pGMRc | 2 | 26 |  |
| `0x0213` |  | pCheck | 2 | 26 |  |
| `0x0217` |  | pBlacksmith | 0 | 2 |  |
| `0x0218` |  | pAlchemist | 0 | 2 |  |
| `0x021D` |  | pLessEffect | 2 | 6 |  |
| `0x0222` |  | pWeaponRefine | 2 | 6 |  |
| `0x0225` |  | pTaekwon | 0 | 2 |  |
| `0x022D` |  | pHomMenu | 2,4 | 5 |  |
| `0x0231` |  | pChangeHomunculusName | 0 | 26 |  |
| `0x0232` |  | pHomMoveTo | 2,6 | 9 |  |
| `0x0233` |  | pHomAttack | 2,6,10 | 11 |  |
| `0x0234` |  | pHomMoveToMaster | 2 | 6 |  |
| `0x0237` |  | pRankingPk | 0 | 2 |  |
| `0x023B` |  | pStoragePassword | 0 | 36 |  |
| `0x023F` |  | pMail_refreshinbox | 0 | 2 |  |
| `0x0241` |  | pMail_read | 2 | 6 |  |
| `0x0243` |  | pMail_delete | 2 | 6 |  |
| `0x0244` |  | pMail_getattach | 2 | 6 |  |
| `0x0246` |  | pMail_winopen | 2 | 4 |  |
| `0x0247` |  | pMail_setattach | 2,4 | 8 |  |
| `0x0248` |  | pMail_send | 2,4,28,68 | -1 |  |
| `0x024B` |  | pAuction_cancelreg | 0 | 4 |  |
| `0x024C` |  | pAuction_setitem | 0 | 8 |  |
| `0x024D` |  | pAuction_register | 0 | 12 |  |
| `0x024E` |  | pAuction_cancel | 0 | 6 |  |
| `0x024F` |  | pAuction_bid | 0 | 10 |  |
| `0x0251` |  | pAuction_search | 0 | 34 |  |
| `0x0254` |  | pFeelSaveOk | 0 | 3 |  |
| `0x025B` |  | pCooking | 0 | 8 |  |
| `0x025C` |  | pAuction_buysell | 0 | 4 |  |
| `0x025D` |  | pAuction_close | 0 | 6 |  |
| `0x0273` |  | pMail_return | 2,6 | 30 |  |
| `0x0288` |  | pcashshop_buy | 4,8 | -1 |  |
| `0x0292` |  | pAutoRevive | 0 | 2 |  |
| `0x029F` |  | pmercenary_action | 0 | 3 |  |
| `0x02B6` |  | pquestStateAck | 2,6 | 7 |  |
| `0x02BA` |  | pHotkey1 |  | 11 |  |
| `0x02C4` |  | pPartyInvite2 | 2 | 26 |  |
| `0x02C7` |  | pReplyPartyInvite2 | 2,6 | 7 |  |
| `0x02C8` |  | pPartyTick | 2 | 3 |  |
| `0x02CF` |  | pMemorialDungeonCommand |  | 6 |  |
| `0x02D6` |  | pViewPlayerEquip | 2 | 6 |  |
| `0x02D8` |  | p_cz_config | 6 | 10 |  |
| `0x02DB` |  | pBattleChat | 2,4 | -1 |  |
| `0x02F1` |  | pProgressbar | 0 | 2 |  |
| `0x035F` |  | pWalkToXY | 2 | 5 | [REQUEST_MOVE](Out/MapServer/REQUEST_MOVE.md) |
| `0x0360` |  | pTickSend | 2 | 6 | [HEARTBEAT_REQUEST](Out/MapServer/HEARTBEAT_REQUEST.md) |
| `0x0361` |  | pChangeDir | 2,4 | 5 | [CHANGE_DIRECTION](Out/MapServer/CHANGE_DIRECTION.md) |
| `0x0362` |  | pTakeItem | 2 | 6 | [PICKUP_ITEM](Out/MapServer/PICKUP_ITEM.md) |
| `0x0363` |  | pDropItem | 2,4 | 6 | [DROP_ITEM](Out/MapServer/DROP_ITEM.md) |
| `0x0364` |  | pMoveToKafra | 2,4 | 8 |  |
| `0x0365` |  | pMoveFromKafra | 2,4 | 8 |  |
| `0x0366` |  | pUseSkillToPos | 2,4,6,8 | 10 | [USE_SKILL_TO_POS](Out/MapServer/USE_SKILL_TO_POS.md) |
| `0x0367` |  | pUseSkillToPosMoreInfo | 2,4,6,8,10 | 31 |  |
| `0x0368` |  | pGetCharNameRequest | 2 | 6 | [REQUEST_UNIT_NAME](Out/MapServer/REQUEST_UNIT_NAME.md) |
| `0x0369` |  | pSolveCharName | 2 | 6 | [REQUEST_UNIT_NAME_BY_ID](Out/MapServer/REQUEST_UNIT_NAME_BY_ID.md) |
| `0x0436` |  | pWantToConnection | 2,6,10,18,22 | 23 | [REQUEST_ENTER_MAP](Out/MapServer/REQUEST_ENTER_MAP.md) |
| `0x0437` |  | pActionRequest | 2,6 | 7 | [REQUEST_ACT](Out/MapServer/REQUEST_ACT.md) |
| `0x0438` |  | pUseSkillToId | 2,4,6 | 10 | [USE_SKILL_TO_ID](Out/MapServer/USE_SKILL_TO_ID.md) |
| `0x0439` |  | pUseItem | 2,4 | 8 | [USE_ITEM](Out/MapServer/USE_ITEM.md) |
| `0x0443` |  | pSkillSelectMenu | 2,6 | 8 |  |
| `0x0447` | PACKET_CZ_BLOCKING_PLAY_CANCEL | p_cz_blocking_play_cancel |  | 2 |  |
| `0x044A` |  | pClientVersion |  | 6 |  |
| `0x07D7` |  | pPartyChangeOption | 2,6,7 | 8 |  |
| `0x07DA` |  | pPartyChangeLeader | 2 | 6 |  |
| `0x07E4` |  | pItemListWindowSelected | 2,4,8 | -1 |  |
| `0x07F5` |  | pGMFullStrip | 2 | 6 |  |
| `0x0801` |  | pPurchaseReq2 | 2,4,8,12 | -1 |  |
| `0x0802` |  | pPartyBookingRegisterReq | 2,4 | 18 |  |
| `0x0804` | Booking System | pPartyBookingSearchReq | 2,4,6,8,12 | 14 |  |
| `0x0806` | Booking System | pPartyBookingDeleteReq | 0 | 2 |  |
| `0x0808` | Booking System | pPartyBookingUpdateReq | 2 | 14 |  |
| `0x0811` |  | pReqOpenBuyingStore | 2,4,8,9,89 | -1 |  |
| `0x0815` |  | pReqCloseBuyingStore | 0 | 2 |  |
| `0x0817` |  | pReqClickBuyingStore | 2 | 6 |  |
| `0x0819` |  | pReqTradeBuyingStore | 2,4,8,12 | -1 |  |
| `0x0835` |  | pSearchStoreInfo | 2,4,5,9,13,14,15 | -1 |  |
| `0x0838` |  | pSearchStoreInfoNextPage | 0 | 2 |  |
| `0x083B` |  | pCloseSearchStoreInfo | 0 | 2 |  |
| `0x083C` |  | pSearchStoreInfoListItemClick | 2,6,10 | 14 |  |
| `0x0842` |  | pGMRecall2 | 2 | 6 |  |
| `0x0843` |  | pGMRemove2 | 2 | 6 |  |
| `0x0844` |  | pCashShopOpen1 |  | 2 |  |
| `0x0846` |  | pCashShopReqTab | 2 | 4 |  |
| `0x0848` |  | pCashShopBuy | 2 | -1 |  |
| `0x084A` |  | pCashShopClose | 2 | 2 |  |
| `0x08C9` |  | pCashShopSchedule | 0 | 2 |  |
| `0x08D7` |  | pBGQueueRegister | 2 | 28 |  |
| `0x08DA` |  | pBGQueueRevokeReq | 2 | 26 |  |
| `0x08E0` |  | pBGQueueBattleBeginAck | 2 | 51 |  |
| `0x08E5` |  | pPartyRecruitRegisterReq | 2,4 | None |  |
| `0x08E7` |  | pPartyRecruitSearchReq | 2 | None |  |
| `0x08E9` |  | pPartyRecruitDeleteReq | 2 | None |  |
| `0x08EB` |  | pPartyRecruitUpdateReq | 2 | None |  |
| `0x0907` |  | pMoveItem | 2,4 | 5 |  |
| `0x090A` |  | pBGQueueCheckState | 2 | 26 |  |
| `0x0916` |  | pGuildInvite2 | 2 | 26 |  |
| `0x096E` |  | ackmergeitems |  | -1 |  |
| `0x0974` |  | cancelmergeitem |  | 2 |  |
| `0x097C` |  | pRanklist |  | 4 |  |
| `0x0980` | CZ_SELECTCART | pSelectCart |  | 7 |  |
| `0x098D` | CZ_CLAN_CHAT | pClanMessage | 2,4 | -1 |  |
| `0x0998` |  | pEquipItem | 2,4 | 8 |  |
| `0x09A7` | CZ_REQ_BANKING_DEPOSIT | pBankDeposit | 2,4,6 | 10 |  |
| `0x09A9` | CZ_REQ_BANKING_WITHDRAW | pBankWithdraw | 2,4,6 | 10 |  |
| `0x09AB` | CZ_REQ_BANKING_CHECK | pBankCheck | 2,4 | 6 |  |
| `0x09B6` | CZ_REQ_OPEN_BANKING | pBankOpen | 2,4 | 6 |  |
| `0x09B8` | CZ_REQ_CLOSE_BANKING | pBankClose | 2,4 | 6 |  |
| `0x09CE` | CZ_ITEM_CREATE_EX | pGM_Monster_Item | 2 | 102 |  |
| `0x09D4` | CZ_NPC_TRADE_QUIT | pNPCShopClosed |  | 2 |  |
| `0x09D6` | CZ_NPC_MARKET_PURCHASE | pNPCMarketPurchase |  | -1 |  |
| `0x09D8` | CZ_NPC_MARKET_CLOSE | pNPCMarketClosed |  | 2 |  |
| `0x09E9` | CZ_CLOSE_RODEXBOX | pRodexCloseMailbox |  | 2 |  |
| `0x09EA` | CZ_REQ_READ_RODEX | pRodexReadMail |  | 11 |  |
| `0x09EE` | CZ_REQ_NEXT_RODEX | pRodexNextMaillist |  | 11 |  |
| `0x09F1` | CZ_REQ_ZENY_FROM_RODEX | pRodexRequestZeny |  | 11 |  |
| `0x09F3` | CZ_REQ_ITEM_FROM_RODEX | pRodexRequestItems |  | 11 |  |
| `0x09F5` | CZ_REQ_DELETE_RODEX | pRodexDeleteMail |  | 11 |  |
| `0x09FB` | CZ_PET_EVOLUTION | pPetEvolution |  | -1 |  |
| `0x0A01` | CZ_SHORTCUTKEYBAR_ROTATE | pHotkeyRowShift1 |  | 3 |  |
| `0x0A03` | CZ_REQ_CANCEL_WRITE_RODEX | pRodexCancelWriteMail |  | 2 |  |
| `0x0A04` | CZ_REQ_ADD_ITEM_RODEX | pRodexAddItem |  | 6 |  |
| `0x0A06` | CZ_REQ_REMOVE_RODEX_ITEM | pRodexRemoveItem |  | 6 |  |
| `0x0A08` | CZ_REQ_OPEN_WRITE_RODEX | pRodexOpenWriteMail |  | 26 |  |
| `0x0A13` | CZ_CHECK_RECEIVE_CHARACTER_NAME | pRodexCheckName1 |  | 26 |  |
| `0x0A16` | CZ_DYNAMICNPC_CREATE_REQUEST | pDynamicnpcCreateRequest |  | 26 |  |
| `0x0A19` | CZ_REQ_OPEN_ROULETTE | pRouletteOpen | 0 | 2 |  |
| `0x0A1B` | HEADER_CZ_REQ_ROULETTE_INFO | pRouletteInfo | 0 | 2 |  |
| `0x0A1D` | CZ_REQ_CLOSE_ROULETTE | pRouletteClose | 0 | 2 |  |
| `0x0A1F` | CZ_REQ_GENERATE_ROULETTE | pRouletteGenerate | 0 | 2 |  |
| `0x0A21` | CZ_RECV_ROULETTE_ITEM | pRouletteRecvItem | 2 | 3 |  |
| `0x0A25` | CZ_REQ_ACH_REWARD | pAchievementGetReward | 2 | 6 |  |
| `0x0A2E` | CZ_REQ_CHANGE_TITLE | pChangeTitle |  | 6 |  |
| `0x0A35` |  | pOneClick_ItemIdentify | 2 | 4 |  |
| `0x0A46` |  | pReqStyleChange |  | 14 |  |
| `0x0A48` |  | pStyleClose |  | 2 |  |
| `0x0A49` |  | pPrivateAirshipRequest |  | 22 |  |
| `0x0A4F` |  | plapineDdukDdak_ack |  | -1 |  |
| `0x0A52` |  | pCaptchaRegister |  | 20 |  |
| `0x0A54` |  | pCaptchaUpload |  | -1 |  |
| `0x0A56` |  | pMacroReporterAck |  | 6 |  |
| `0x0A5A` |  | pMacroDetectorDownloadAck |  | 2 |  |
| `0x0A5C` |  | pMacroDetectorAnswer |  | 18 |  |
| `0x0A68` |  | pOpenUIRequest |  | 3 |  |
| `0x0A69` |  | pCaptchaPreviewRequest |  | 6 |  |
| `0x0A6C` |  | pMacroReporterSelect |  | 7 |  |
| `0x0A6E` | CZ_RODEX_SEND_MAIL | pRodexSendMail |  | -1 |  |
| `0x0A70` |  | plapineDdukDdak_close |  | 2 |  |
| `0x0A77` | CZ_CAMERA_INFO | pCameraInfo |  | 15 |  |
| `0x0A88` |  | pResetCooldown |  | 2 |  |
| `0x0AA1` |  | pAddItemRefineryUI |  | 4 |  |
| `0x0AA3` |  | pRefineryUIRefine |  | 9 |  |
| `0x0AA4` |  | pRefineryUIClose |  | 2 |  |
| `0x0AB5` |  | pLapineUpgrade_close |  | 2 |  |
| `0x0AB6` |  | pLapineUpgrade_makeItem |  | 8 |  |
| `0x0AC0` | CZ_OPEN_RODEXBOX | pRodexOpenMailbox |  | 26 |  |
| `0x0AC1` | CZ_REQ_REFRESH_RODEX | pRodexRefreshMaillist |  | 26 |  |
| `0x0AE6` |  | pAdventuterAgencyJoinReq |  | 10 |  |
| `0x0AE8` |  | pChangeDress |  | 2 |  |
| `0x0AEF` |  | pAttendanceRewardRequest |  | 2 |  |
| `0x0AF8` |  | pAdventuterAgencyJoinResult |  | 11 |  |
| `0x0AFC` |  | pReqStyleChange2 |  | 16 |  |
| `0x0B0F` |  | pNPCBarterPurchase |  | -1 |  |
| `0x0B10` |  | pStartUseSkillToId |  | 10 |  |
| `0x0B11` |  | pStopUseSkillToId |  | 4 |  |
| `0x0B12` |  | pNPCBarterClosed |  | 2 |  |
| `0x0B14` |  | pInventoryExpansion |  | 2 |  |
| `0x0B16` |  | pInventoryExpansionConfirmed |  | 2 |  |
| `0x0B19` |  | pInventoryExpansionRejected |  | 2 |  |
| `0x0B1C` |  | pPing |  | 2 |  |
| `0x0B1E` |  | pGuildRequestEmblem2 |  | 14 |  |
| `0x0B21` |  | pHotkey2 |  | 13 |  |
| `0x0B22` | CZ_SHORTCUTKEYBAR_ROTATE | pHotkeyRowShift2 |  | 5 |  |
| `0x0B28` |  | pGuildCastleTeleportRequest |  | 3 |  |
| `0x0B2C` |  | pGuildCastleInfoRequest |  | 3 |  |
| `0x0B35` |  | pReqGearOff |  | 3 |  |
| `0x0B46` |  | pGuildRequestEmblem3 |  | 10 |  |
| `0x0B4C` |  | pCashShopLimitedReq |  | 2 |  |
| `0x0B57` |  | pNPCExpandedBarterPurchase |  | -1 |  |
| `0x0B58` |  | pNPCExpandedBarterClosed |  | 2 |  |
| `0x0B59` |  | pGradeEnchantAddItem |  | 4 |  |
| `0x0B5B` |  | pGradeEnchantStart |  | 14 |  |
| `0x0B5C` |  | pGradeEnchantClose |  | 2 |  |
| `0x0B66` |  | pRepairItem2 |  | 26 |  |
| `0x0B6D` |  | pCashShopOpen2 |  | 6 |  |
| `0x0B90` |  | pItemReformClose |  | 2 |  |
| `0x0B91` |  | pItemReformAck |  | 8 |  |
| `0x0B97` |  | pRodexCheckName2 |  | 27 |  |
| `0x0B9B` |  | pEnchantUINormalRequest |  | 12 |  |
| `0x0B9C` |  | pEnchantUIPerfectRequest |  | 16 |  |
| `0x0B9D` |  | pEnchantUIUpgradeRequest |  | 14 |  |
| `0x0B9E` |  | pEnchantUIResetRequest |  | 12 |  |
| `0x0BA0` |  | pEnchantUIClose |  | 2 |  |
| `0x0BA8` |  | pNpcSelectMenuZero |  | 7 |  |
| `0x0BAD` |  | pUnequipAllItems |  | 2 |  |
| `0x0BAF` |  | pUsePackageItem |  | 16 |  |
| `0x0BB0` |  | pGuildMembersNear |  | 9 |  |
