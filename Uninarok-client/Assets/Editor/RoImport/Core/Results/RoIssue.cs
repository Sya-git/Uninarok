namespace Uninarok.RoImport.Core.Results
{
    /// <summary>One problem with one item, and who asked for it (plan 4.9, SPEC-0002 FR-10).</summary>
    public sealed class RoIssue
    {
        public RoIssueCode Code;
        public string Item;
        public string Referrer;      // the model or map that asked for the item; null when requested directly
        public string Message;

        public RoIssue() { }

        public RoIssue(RoIssueCode code, string item, string referrer, string message)
        {
            Code = code;
            Item = item;
            Referrer = referrer;
            Message = message;
        }

        public override string ToString() =>
            string.IsNullOrEmpty(Referrer)
                ? $"{Code} {Item}: {Message}"
                : $"{Code} {Item} (referenced by {Referrer}): {Message}";
    }
}
