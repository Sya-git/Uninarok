namespace Uninarok.Networking
{
    public readonly struct RagnarokLoginSession
    {
        public readonly uint AccountId;
        public readonly int AuthCode;
        public readonly uint UserLevel;
        public readonly byte Sex;
        public readonly RagnarokServerEndpoint CharacterServer;

        public RagnarokLoginSession(uint accountId, int authCode, uint userLevel, byte sex, RagnarokServerEndpoint characterServer)
        {
            AccountId = accountId;
            AuthCode = authCode;
            UserLevel = userLevel;
            Sex = sex;
            CharacterServer = characterServer;
        }
    }
}
