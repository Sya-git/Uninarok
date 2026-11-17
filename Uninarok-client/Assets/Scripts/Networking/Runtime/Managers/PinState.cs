namespace Uninarok.Networking.Managers
{
    /// <summary>
    /// PIN-code login state of the character server (SPEC-0001 FR-13). <see cref="Unknown"/> until the server reports it,
    /// never a default zero. Values follow Hercules <c>pincode_login_response</c> (src/char/pincode.h); any other number
    /// the server sends is kept as it is.
    /// </summary>
    public enum PinState
    {
        Unknown = -1,
        Ok = 0,
        Ask = 1,
        NotSet = 2,
        Expired = 3,
        RestrictPassword = 5,
        Unused = 7,
        Wrong = 8,
    }
}
