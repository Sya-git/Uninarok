namespace Uninarok.Networking.Data
{
    /// <summary>Sex/account-kind value; one byte on the wire (docs/data/AccountSex.md).</summary>
    public enum AccountSex : byte
    {
        SEX_FEMALE = 0,
        SEX_MALE = 1,
        SEX_SERVER = 2,
    }
}
