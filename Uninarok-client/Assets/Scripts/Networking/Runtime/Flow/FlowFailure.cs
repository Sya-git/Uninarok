namespace Uninarok.Networking.Flow
{
    /// <summary>The reason the flow failed: a code and a human-readable message.</summary>
    public readonly struct FlowFailure
    {
        public readonly FailureCode Code;
        public readonly string Message;

        public FlowFailure(FailureCode code, string message)
        {
            Code = code;
            Message = message;
        }

        public override string ToString() => Message == null ? Code.ToString() : Code + ": " + Message;
    }
}
