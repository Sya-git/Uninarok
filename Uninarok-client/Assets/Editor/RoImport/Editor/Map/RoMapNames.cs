using System;
using UnityEngine;

namespace Uninarok.RoImport.Editor.Map
{
    /// <summary>The object groups of a map scene (PLAN-0006 4.6, SPEC-0006 FR-5).</summary>
    public enum RoMapGroup
    {
        Models,
        Lights,
        Sounds,
        Effects
    }

    /// <summary>
    /// The names of a generated map scene (PLAN-0006 4.6, 4.7): a stable contract with the hand-made additive scene. Only
    /// this class builds or looks up these names. A group's name is its base name and its count, <c>Models (1303)</c>; the
    /// count can change between conversions, the base name never does, so lookups go by the base name.
    /// </summary>
    public static class RoMapNames
    {
        public const string RootPrefix = "Map_";
        public const string Ground = "Ground";
        public const string Sun = "Sun";

        /// <summary>The map's root object: <c>Map_&lt;name&gt;</c>.</summary>
        public static string Root(string mapName) => RootPrefix + mapName;

        /// <summary>The stable base name of a group: <c>Models</c>, <c>Lights</c>, <c>Sounds</c> or <c>Effects</c>.</summary>
        public static string Group(RoMapGroup group) => group.ToString();

        /// <summary>The name a group object gets, with its count: <c>Models (1303)</c>.</summary>
        public static string GroupLabel(RoMapGroup group, int count) => $"{Group(group)} ({count})";

        /// <summary>True if an object name is the group's, with or without its count.</summary>
        public static bool IsGroup(string objectName, RoMapGroup group)
        {
            string baseName = Group(group);
            return objectName == baseName
                   || objectName != null && objectName.StartsWith(baseName + " (", StringComparison.Ordinal) && objectName.EndsWith(")", StringComparison.Ordinal);
        }

        /// <summary>The group object directly under a map root, found by its base name; null if absent.</summary>
        public static Transform FindGroup(Transform root, RoMapGroup group)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (IsGroup(child.name, group)) return child;
            }
            return null;
        }
    }
}
