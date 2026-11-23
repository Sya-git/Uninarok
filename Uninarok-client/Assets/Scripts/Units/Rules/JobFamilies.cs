using System;

namespace Uninarok.Units
{
    /// <summary>Job families the display rules depend on (PLAN-0009 4.11).</summary>
    public static class JobFamilies
    {
        /// <summary>Assassin, Assassin Cross, Baby Assassin, Guillotine Cross (both), Baby Guillotine Cross.</summary>
        public static bool IsAssassinFamily(int job) => throw new NotImplementedException("T10.2");
    }
}
