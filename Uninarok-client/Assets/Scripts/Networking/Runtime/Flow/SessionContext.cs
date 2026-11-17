using System.Collections.Generic;
using Uninarok.Networking.Data;

namespace Uninarok.Networking.Flow
{
    /// <summary>
    /// The single, correctly typed record handed from one server to the next (SPEC-0001 FR-12; plan 4.9): account,
    /// authorization codes, selected character, map address. It is the only place the flow and the managers share data.
    /// Ports are <c>ushort</c> end to end.
    /// </summary>
    public sealed class SessionContext
    {
        // login server
        public uint AccountId;
        public int AuthCode;
        public uint UserLevel;
        public AccountSex Sex;
        public readonly List<ServerEndpoint> Servers = new List<ServerEndpoint>();

        // character server
        public readonly List<CharInfo> Characters = new List<CharInfo>();
        public CharacterSlots Slots;
        public int CharacterId;
        public byte SelectedSlot;

        // map server
        public MapEndpoint Map;
        public MapEntry Entry;

        /// <summary>Forgets everything: a new login starts from an empty session.</summary>
        public void Reset()
        {
            AccountId = 0;
            AuthCode = 0;
            UserLevel = 0;
            Sex = default;
            Servers.Clear();
            Characters.Clear();
            Slots = default;
            CharacterId = 0;
            SelectedSlot = 0;
            Map = default;
            Entry = default;
        }
    }
}
