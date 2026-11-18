using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Rsm;

namespace Uninarok.RoImport.Tests.Rsm
{
    /// <summary>
    /// Small RSM models built in code (TASKS-0003 T1.1, PLAN-0003 §6): every pure rule is tested on these, without GRF
    /// and without data files. All values are in Ragnarok space (Y down), as the reader would produce them.
    /// </summary>
    public static class RsmTestModels
    {
        public const string Texture = "test\\a.bmp";

        /// <summary>A model of the given nodes, version 1.4, smooth shading, one texture.</summary>
        public static RoRsm Model(params RoRsmNode[] nodes) => new RoRsm
        {
            VersionMajor = 1,
            VersionMinor = 4,
            ShadeType = RsmShadeType.Smooth,
            HasShadeType = true,
            AnimationLength = 1000,
            Textures = new[] { Texture },
            Nodes = nodes
        };

        /// <summary>
        /// A unit cube from (0,0,0) to (size,size,size): 8 vertices, 12 faces. With <paramref name="perFaceUv"/> every
        /// side has its own 4 texture coordinates (24 in all); otherwise each corner has one coordinate (8 in all).
        /// </summary>
        public static RoRsmNode Cube(string name = "cube", float size = 1f, bool perFaceUv = true)
        {
            var v = new[]
            {
                new Vector3(0, 0, 0), new Vector3(size, 0, 0), new Vector3(size, size, 0), new Vector3(0, size, 0),
                new Vector3(0, 0, size), new Vector3(size, 0, size), new Vector3(size, size, size), new Vector3(0, size, size)
            };
            // Sides as quads (a, b, c, d) of vertex indices.
            int[][] sides =
            {
                new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 },
                new[] { 3, 7, 6, 2 }, new[] { 0, 4, 7, 3 }, new[] { 1, 2, 6, 5 }
            };
            var tex = new List<RoRsmTexCoord>();
            var faces = new List<RoRsmFace>();
            if (!perFaceUv)
                for (int i = 0; i < 8; i++) tex.Add(new RoRsmTexCoord(i / 8f, (i % 2) * 0.5f));

            for (int side = 0; side < sides.Length; side++)
            {
                var s = sides[side];
                float u0 = side / 6f, u1 = (side + 1) / 6f;   // each side its own part of the texture
                int t0, t1, t2, t3;
                if (perFaceUv)
                {
                    t0 = tex.Count;
                    tex.Add(new RoRsmTexCoord(u0, 0));
                    tex.Add(new RoRsmTexCoord(u0, 1));
                    tex.Add(new RoRsmTexCoord(u1, 1));
                    tex.Add(new RoRsmTexCoord(u1, 0));
                    t1 = t0 + 1; t2 = t0 + 2; t3 = t0 + 3;
                }
                else
                {
                    t0 = s[0]; t1 = s[1]; t2 = s[2]; t3 = s[3];
                }
                faces.Add(new RoRsmFace(s[0], s[1], s[2], t0, t1, t2));
                faces.Add(new RoRsmFace(s[0], s[2], s[3], t0, t2, t3));
            }

            return new RoRsmNode
            {
                Name = name,
                TextureSlots = new[] { 0 },
                Vertices = v,
                TexCoords = tex.ToArray(),
                Faces = faces.ToArray()
            };
        }

        /// <summary>
        /// An open cylinder strip of <paramref name="segments"/> quads around the Y axis. The texture wraps once, so the
        /// seam has two coordinates (u = 0 and u = 1) at the same positions: segments + 1 coordinates per ring.
        /// Positions are shared: 2 * segments vertices, 2 * segments faces, wound outward.
        /// </summary>
        public static RoRsmNode CylinderStrip(int segments = 8, string name = "cylinder")
        {
            var v = new List<Vector3>();
            var tex = new List<RoRsmTexCoord>();
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                v.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
                v.Add(new Vector3(Mathf.Cos(a), -1f, Mathf.Sin(a)));
            }
            for (int i = 0; i <= segments; i++)
            {
                tex.Add(new RoRsmTexCoord(i / (float)segments, 0f));
                tex.Add(new RoRsmTexCoord(i / (float)segments, 1f));
            }

            var faces = new List<RoRsmFace>();
            for (int i = 0; i < segments; i++)
            {
                int b0 = 2 * i, t0 = b0 + 1, b1 = 2 * ((i + 1) % segments), t1 = b1 + 1;
                int ub0 = 2 * i, ut0 = ub0 + 1, ub1 = 2 * (i + 1), ut1 = ub1 + 1;
                // Outward facing in Ragnarok space (counter-clockwise seen from outside, right-handed).
                faces.Add(new RoRsmFace(b0, t1, t0, ub0, ut1, ut0));
                faces.Add(new RoRsmFace(b0, b1, t1, ub0, ub1, ut1));
            }

            return new RoRsmNode
            {
                Name = name,
                TextureSlots = new[] { 0 },
                Vertices = v.ToArray(),
                TexCoords = tex.ToArray(),
                Faces = faces.ToArray()
            };
        }

        /// <summary>
        /// A single node with a rotation and a position, like the reported models (values of 나무잡초꽃/버섯02:
        /// offset matrix with Y and Z swapped, position (-3.4286, 1.6567, 22.7747), angle -3.927 about +Y, scale 0.0901).
        /// </summary>
        public static RoRsmNode RotatedPositioned(string name = "Cylinder01")
        {
            var node = Cube(name, 10f);
            node.OffsetMatrix = new[] { 1f, 0f, 0f, 0f, 0f, 1f, 0f, -1f, 0f };
            node.Position = new Vector3(-3.4286f, 1.6567f, 22.7747f);
            node.RotationAngle = -3.927f;
            node.RotationAxis = new Vector3(0f, 1f, 0f);
            node.Scale = new Vector3(0.0901f, 0.0901f, 0.0901f);
            return node;
        }

        /// <summary>A parent cube and a child cube placed 5 units along +X under it.</summary>
        public static RoRsm ParentAndChild()
        {
            var parent = Cube("parent");
            var child = Cube("child");
            child.ParentName = "parent";
            child.Position = new Vector3(5f, 0f, 0f);
            return Model(parent, child);
        }

        /// <summary>
        /// A static base and an animated child (a hand): 5 rotation keys 250 ms apart, a quarter turn about +Z each,
        /// like the hour hand of 프론테라/시계탑.
        /// </summary>
        public static RoRsm TwoNodeAnimated()
        {
            var baseNode = Cube("base");
            var hand = Cube("hand");
            hand.ParentName = "base";
            hand.Position = new Vector3(0f, -2f, 0f);
            var keys = new RoRsmRotKey[5];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new RoRsmRotKey(i * 250, Quaternion.AngleAxis(90f * i, Vector3.forward));
            hand.RotKeys = keys;
            return Model(baseNode, hand);
        }
    }
}
