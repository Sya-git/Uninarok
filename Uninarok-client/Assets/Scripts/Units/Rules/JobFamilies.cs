namespace Uninarok.Units
{
    /// <summary>Job families the display rules depend on (PLAN-0009 4.11).</summary>
    public static class JobFamilies
    {
        /// <summary>Assassin, Assassin Cross, Baby Assassin, Guillotine Cross (both), Baby Guillotine Cross.</summary>
        public static bool IsAssassinFamily(int job) 
        {
            switch (job)
            {
                case 12:    // JOB_ASSASSIN
                case 4013:  // JOB_ASSASSIN_CROSS
                case 4035:  // JOB_BABY_ASSASSIN
                case 4059:  // JOB_GUILLOTINE_CROSS
                case 4065:  // JOB_GUILLOTINE_CROSS_T
                case 4101:  // JOB_BABY_CROSS
                    return true;
                default:
                    return false;
            }
        }
    }
}
