namespace Uninarok.RoImport.Core.Output
{
    /// <summary>
    /// The three output areas (plan 4.5, SPEC-0002 FR-4): visual assets, data assets, and map scenes (where the hand-made
    /// additive scenes also live). Project-relative, forward slashes. Tests point them at a throwaway root.
    /// </summary>
    public sealed class RoOutputRoots
    {
        public string Art = "Assets/Art/Imported";
        public string Data = "Assets/Data";
        public string Scenes = "Assets/Scenes/Maps";        // generated map scenes and the hand-made additive scenes (plan 4.13)

        /// <summary>All three roots under one folder, e.g. <c>Assets/__RoImportTests</c>.</summary>
        public static RoOutputRoots Under(string folder)
        {
            string f = folder.TrimEnd('/');
            return new RoOutputRoots { Art = f + "/Art", Data = f + "/Data", Scenes = f + "/Scenes" };
        }
    }
}
