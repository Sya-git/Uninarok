using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Rsw;

namespace Uninarok.RoImport.Tests.Rsw
{
    /// <summary>A synthetic RSW with the size of the ground it belongs to (the RSW itself has no size).</summary>
    public sealed class RswTestMap
    {
        public RoRsw Rsw;
        public int Width, Height;
        public float Zoom = 10f;
    }

    /// <summary>
    /// Synthetic RSW maps for the pure tests (TASKS-0006 T1.1, SPEC-0006 FR-1, FR-3). Values are raw: Ragnarok units,
    /// Y down, positions relative to the map centre, angles in degrees.
    /// </summary>
    public static class RswTestMaps
    {
        public const string MissingModelFile = "없는모델\\missing.rsm";
        public const string BoxModelFile = "외부소품\\box.rsm";

        public static RoRswModel Model(string name, Vector3 position, Vector3 rotation = default, Vector3? scale = null,
            string file = BoxModelFile, int animType = 0, float animSpeed = 1f) =>
            new RoRswModel(name, animType, animSpeed, 0, file, "Box01", position, rotation, scale ?? Vector3.one);

        static RoRsw Empty(string name) => new RoRsw
        {
            VersionMajor = 2,
            VersionMinor = 1,
            GndFile = name + ".gnd",
            GatFile = name + ".gat",
            Water = new RoRswWater(47f, 1, 0.3f, 3f, 100f),
            Light = new RoRswLight(45, 45, Color.white, new Color(0.3f, 0.3f, 0.3f), 0.5f),
        };

        /// <summary>
        /// A 10 x 10 map (zoom 10, so ±50 Ragnarok units from the centre) with one model at the centre, one at each corner,
        /// models rotated about each axis, models mirrored on one and on three axes, a light, a sound, an effect and a
        /// reference to a model that does not exist.
        /// </summary>
        public static RswTestMap Full()
        {
            var rsw = Empty("test_full");
            rsw.Models = new List<RoRswModel>
            {
                Model("centre", Vector3.zero),
                Model("corner_sw", new Vector3(-50, 0, -50)),
                Model("corner_se", new Vector3(50, 0, -50)),
                Model("corner_nw", new Vector3(-50, 0, 50)),
                Model("corner_ne", new Vector3(50, 0, 50)),
                Model("rot_x", new Vector3(10, 0, 0), new Vector3(30, 0, 0)),
                Model("rot_y", new Vector3(20, 0, 0), new Vector3(0, 30, 0)),
                Model("rot_z", new Vector3(30, 0, 0), new Vector3(0, 0, 30)),
                Model("mirror_x", new Vector3(0, 0, 10), scale: new Vector3(-1, 1, 1)),
                Model("mirror_xyz", new Vector3(0, 0, 20), scale: new Vector3(-1, -1, -1)),
                Model("missing", new Vector3(0, 0, -10), file: MissingModelFile),
            };
            rsw.Lights = new[] { new RoRswPointLight("torch", new Vector3(5, -10, 5), new Color(0.2f, 0.05f, 0f), 15f) };
            rsw.Sounds = new[] { new RoRswSound("bird", "se_prtbird_01.wav", new Vector3(-5, -20, 5), 0.8f, 10, 10, 100f, 4f) };
            rsw.Effects = new[] { new RoRswEffect("fire", new Vector3(5, -5, -5), 44, 40f, 35f, 35f, 0f, 0f) };
            return new RswTestMap { Rsw = rsw, Width = 10, Height = 10 };
        }

        /// <summary>
        /// A non-square 6 x 4 map (±30 by ±20 Ragnarok units) with one model at each corner, so a swapped width and
        /// height is visible.
        /// </summary>
        public static RswTestMap NonSquare()
        {
            var rsw = Empty("test_wide");
            rsw.Models = new List<RoRswModel>
            {
                Model("corner_sw", new Vector3(-30, 0, -20)),
                Model("corner_ne", new Vector3(30, 0, 20)),
                Model("corner_se", new Vector3(30, 0, -20)),
                Model("corner_nw", new Vector3(-30, 0, 20)),
            };
            return new RswTestMap { Rsw = rsw, Width = 6, Height = 4 };
        }
    }
}
