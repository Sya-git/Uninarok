namespace Uninarok.Map
{
    /// <summary>The Addressables keys of the map scenes, shared by the import and the run time (PLAN-0010 3, D5).</summary>
    public static class MapSceneKeys
    {
        const string Root = "map/";

        /// <summary>The converted scene of a map.</summary>
        public static string Scene(string mapName) => Root + mapName;

        /// <summary>The hand-made scene beside it; most maps have none.</summary>
        public static string Additive(string mapName) => Root + mapName + ".additive";

        /// <summary>The map name of the server's: without the <c>.gat</c> suffix.</summary>
        public static string MapName(string serverName) =>
            serverName != null && serverName.EndsWith(".gat", System.StringComparison.OrdinalIgnoreCase) ? serverName.Substring(0, serverName.Length - 4) : serverName;
    }
}
