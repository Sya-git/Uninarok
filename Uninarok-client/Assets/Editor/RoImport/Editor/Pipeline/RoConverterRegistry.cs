using System;

namespace Uninarok.RoImport.Editor.Pipeline
{
    /// <summary>The converter of each job kind (plan 4.10). Contract only: implemented by T6.2.</summary>
    public static class RoConverterRegistry
    {
        public static IRoConverter For(RoJobKind kind) => throw new NotImplementedException("T6.2");
    }
}
