using Unity.Collections;

namespace Uninarok.Networking.Managers
{
    /// <summary>Which of the three packets refused the login (all three are handled as a refusal).</summary>
    public enum LoginRefusalSource
    {
        /// <summary>LOGIN_REFUSAL, 0x006A.</summary>
        Refusal,
        /// <summary>REFUSE_LOGIN_3, 0x0B02.</summary>
        Refuse3,
        /// <summary>LOGIN_SERVER_NOTICE, 0x0081: a connection error or status.</summary>
        ServerNotice,
    }

    /// <summary>A refused login, whichever of the three refusal packets carried it.</summary>
    public readonly struct LoginRefusal
    {
        public readonly LoginRefusalSource Source;
        public readonly int ErrorCode;

        /// <summary>Ban expiration date as text; empty when the refusal is not a ban or the packet has no date.</summary>
        public readonly FixedString64Bytes BlockDate;

        public LoginRefusal(LoginRefusalSource source, int errorCode, in FixedString64Bytes blockDate)
        {
            Source = source;
            ErrorCode = errorCode;
            BlockDate = blockDate;
        }

        public override string ToString() => Source + " code " + ErrorCode + (BlockDate.Length > 0 ? " until " + BlockDate : "");
    }
}
