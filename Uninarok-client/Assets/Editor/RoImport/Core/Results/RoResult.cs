namespace Uninarok.RoImport.Core.Results
{
    /// <summary>Result of a conversion: a status, the asset (when there is one) and, for a failure, why (plan 4.9).</summary>
    public readonly struct RoResult<T>
    {
        public readonly RoStatus Status;
        public readonly T Value;
        public readonly RoIssue Failure;

        public RoResult(RoStatus status, T value, RoIssue failure = null)
        {
            Status = status;
            Value = value;
            Failure = failure;
        }

        public bool Succeeded => Status != RoStatus.Failed;

        public static RoResult<T> Created(T value) => new RoResult<T>(RoStatus.Created, value);
        public static RoResult<T> Updated(T value) => new RoResult<T>(RoStatus.Updated, value);
        public static RoResult<T> Skipped(T value) => new RoResult<T>(RoStatus.Skipped, value);
        public static RoResult<T> Failed(RoIssue failure, T value = default) => new RoResult<T>(RoStatus.Failed, value, failure);
    }
}
