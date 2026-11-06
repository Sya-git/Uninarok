namespace Uninarok.Networking
{
    public readonly struct RagnarokLoginSession
    {
        public readonly uint AccountId;
        public readonly int AuthCode;
        public readonly uint UserLevel;
        public readonly byte Sex;
        public readonly UninarokServerEndpoint CharacterServer;

        public RagnarokLoginSession(uint accountId, int authCode, uint userLevel, byte sex, UninarokServerEndpoint characterServer)
        {
            AccountId = accountId;
            AuthCode = authCode;
            UserLevel = userLevel;
            Sex = sex;
            CharacterServer = characterServer;
        }
    }
}
