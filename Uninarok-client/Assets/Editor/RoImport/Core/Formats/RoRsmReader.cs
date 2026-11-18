using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Rsm;
using GrfRsm = GRF.FileFormats.RsmFormat.Rsm;
using GrfMesh = GRF.FileFormats.RsmFormat.Mesh;
using GrfVector3 = GRF.Graphics.TkVector3;
using EncodingService = Utilities.Services.EncodingService;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>
    /// Adapter over GRF's RSM reader (PLAN-0003 4.1, D2): the only RSM code that touches GRF types. It copies the GRF
    /// members that correspond to file fields (the mapping verified by TASKS-0003 T0.2) into an <see cref="RoRsm"/>;
    /// GRF's derived members are never used. It never throws for bad input: every failure is <c>false</c> with a reason.
    /// </summary>
    public static class RoRsmReader
    {
        const string Magic = "GRSM";

        public static bool TryRead(RoPath path, byte[] bytes, out RoRsm rsm, out string reason)
        {
            rsm = null;
            if (bytes == null || bytes.Length == 0) return Fail("the file is empty", out reason);
            if (bytes.Length < 6 || bytes[0] != 'G' || bytes[1] != 'R' || bytes[2] != 'S' || bytes[3] != 'M')
                return Fail($"not an RSM file (no '{Magic}' header)", out reason);

            GrfRsm grf;
            // GRF decodes strings with its display encoding, UTF-8 in the editor, which loses the Korean texture names
            // (CP949 in the file). Read with the Korean encoding and restore the previous one (PLAN-0003 4.1, D9).
            var previous = EncodingService.DisplayEncoding;
            try
            {
                EncodingService.DisplayEncoding = EncodingService.Korean;
                grf = new GrfRsm((GRF.MultiType)bytes);
                rsm = Convert(grf);
            }
            catch (Exception e)
            {
                rsm = null;
                return Fail($"GRF could not read the model: {e.GetType().Name}: {e.Message}", out reason);
            }
            finally
            {
                EncodingService.DisplayEncoding = previous;
            }

            reason = null;
            return true;
        }

        static RoRsm Convert(GrfRsm grf)
        {
            int major = grf.Header.MajorVersion, minor = grf.Header.MinorVersion;
            var nodes = new List<RoRsmNode>(grf.Meshes.Count);
            foreach (var mesh in grf.Meshes) nodes.Add(Convert(mesh));

            // Every 1.x file of the data carries a shade type (T0.2); versions before 1.4 are unverified, so they are
            // treated as having none (smooth, SPEC-0003 FR-5). Unsupported versions are rejected later by RsmRules.
            bool hasShadeType = major > 1 || (major == 1 && minor >= 4);
            return new RoRsm
            {
                VersionMajor = major,
                VersionMinor = minor,
                HasShadeType = hasShadeType,
                ShadeType = hasShadeType ? ShadeTypeOf(grf.ShadeType) : RsmShadeType.Smooth,
                AnimationLength = grf.AnimationLength,
                Textures = grf.Textures != null ? new List<string>(grf.Textures) : new List<string>(),
                Nodes = nodes
            };
        }

        static RsmShadeType ShadeTypeOf(int value) =>
            value == (int)RsmShadeType.None ? RsmShadeType.None
            : value == (int)RsmShadeType.Flat ? RsmShadeType.Flat
            : RsmShadeType.Smooth;

        static RoRsmNode Convert(GrfMesh mesh)
        {
            var node = new RoRsmNode
            {
                Name = mesh.Name ?? string.Empty,
                ParentName = mesh.ParentName ?? string.Empty,
                TextureSlots = mesh.TextureIndexes != null ? mesh.TextureIndexes.ToArray() : Array.Empty<int>(),
                OffsetMatrix = OffsetMatrixOf(mesh),
                Offset = V(mesh.LocalPosition),            // the file's offset vector (T0.2)
                Position = V(mesh.GlobalPosition),         // the file's position (T0.2)
                RotationAngle = mesh.GlobalRotationAngle,
                RotationAxis = V(mesh.GlobalRotationAxis),
                Scale = V(mesh.GlobalScale)
            };

            var vertices = new Vector3[mesh.Vertices.Count];
            for (int i = 0; i < vertices.Length; i++) vertices[i] = V(mesh.Vertices[i]);
            node.Vertices = vertices;

            var tex = new RoRsmTexCoord[mesh.TextureVertices.Count];
            for (int i = 0; i < tex.Length; i++)
            {
                var t = mesh.TextureVertices[i];
                tex[i] = new RoRsmTexCoord(t.U, t.V, t.Color);
            }
            node.TexCoords = tex;

            var faces = new RoRsmFace[mesh.Faces.Count];
            for (int i = 0; i < faces.Length; i++)
            {
                var f = mesh.Faces[i];
                faces[i] = new RoRsmFace(
                    f.VertexIds[0], f.VertexIds[1], f.VertexIds[2],
                    f.TextureVertexIds[0], f.TextureVertexIds[1], f.TextureVertexIds[2],
                    f.TextureId, f.TwoSide != 0,
                    f.SmoothGroup != null && f.SmoothGroup.Length > 0 ? f.SmoothGroup[0] : 0);
            }
            node.Faces = faces;

            var pos = new RoRsmPosKey[mesh.PosKeyFrames.Count];
            for (int i = 0; i < pos.Length; i++)
                pos[i] = new RoRsmPosKey(mesh.PosKeyFrames[i].Frame, V(mesh.PosKeyFrames[i].Position));
            node.PosKeys = pos;

            var rot = new RoRsmRotKey[mesh.RotationKeyFrames.Count];
            for (int i = 0; i < rot.Length; i++)
            {
                var q = mesh.RotationKeyFrames[i].Quaternion;
                rot[i] = new RoRsmRotKey(mesh.RotationKeyFrames[i].Frame, new Quaternion(q.X, q.Y, q.Z, q.W));
            }
            node.RotKeys = rot;

            var scale = new RoRsmScaleKey[mesh.ScaleKeyFrames.Count];
            for (int i = 0; i < scale.Length; i++)
                scale[i] = new RoRsmScaleKey(mesh.ScaleKeyFrames[i].Frame, V(mesh.ScaleKeyFrames[i].Scale));
            node.ScaleKeys = scale;

            return node;
        }

        /// <summary>The nine floats in file order: GRF's <c>[row, col]</c> equals the file's row-major reading (T0.2).</summary>
        static float[] OffsetMatrixOf(GrfMesh mesh)
        {
            var m = mesh.TransformationMatrix;
            var result = new float[9];
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    result[3 * r + c] = m[r, c];
            return result;
        }

        static Vector3 V(GrfVector3 v) => new Vector3(v.X, v.Y, v.Z);

        static bool Fail(string why, out string reason)
        {
            reason = why;
            return false;
        }
    }
}
