using UnityEngine;

namespace Uninarok.Units.Tests
{
    /// <summary>Synthetic data shared by the unit tests (PLAN-0009 6).</summary>
    static class UnitTestData
    {
        public static CompositionTables Tables() => ScriptableObject.CreateInstance<CompositionTables>();
    }
}
