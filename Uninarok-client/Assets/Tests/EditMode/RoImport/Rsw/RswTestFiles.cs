using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Uninarok.RoImport.Core.Rsw;
using EncodingService = Utilities.Services.EncodingService;

namespace Uninarok.RoImport.Tests.Rsw
{
    /// <summary>
    /// Writes synthetic <c>.rsw</c> files (version 2.1) for the converter tests (TASKS-0006 T2.1), following the layout of
    /// the community format document: header, water, light, ground box, objects, then the quad tree (absent before 2.1).
    /// Test code only: production code never writes or parses these bytes (PLAN-0002 §1).
    /// </summary>
    public static class RswTestFiles
    {
        const int QuadTreeNodes = 1365;   // a full tree of depth 5: 1 + 4 + 16 + 64 + 256 + 1024

        public static byte[] Rsw(RoRsw rsw)
        {
            var b = new List<byte>();
            void I(int v) => b.AddRange(BitConverter.GetBytes(v));
            void F(float v) => b.AddRange(BitConverter.GetBytes(v));
            void V(Vector3 v) { F(v.x); F(v.y); F(v.z); }
            void S(string s, int length)
            {
                var bytes = EncodingService.Korean.GetBytes(s ?? string.Empty);
                var field = new byte[length];
                Array.Copy(bytes, field, Math.Min(bytes.Length, length - 1));
                b.AddRange(field);
            }

            b.AddRange(Encoding.ASCII.GetBytes("GRSW"));
            b.Add(2);
            b.Add(1);
            S("", 40);
            S(rsw.GndFile, 40);
            S(rsw.GatFile, 40);
            S("", 40);

            F(rsw.Water.Level);
            I(rsw.Water.Type);
            F(rsw.Water.WaveHeight);
            F(rsw.Water.WaveSpeed);
            F(rsw.Water.WavePitch);
            I(3);   // texture cycling

            I(rsw.Light.Longitude);
            I(rsw.Light.Latitude);
            F(rsw.Light.Diffuse.r); F(rsw.Light.Diffuse.g); F(rsw.Light.Diffuse.b);
            F(rsw.Light.Ambient.r); F(rsw.Light.Ambient.g); F(rsw.Light.Ambient.b);
            F(rsw.Light.ShadowOpacity);

            I(0); I(0); I(0); I(0);   // ground box (unused)

            I(rsw.Models.Count + rsw.Lights.Count + rsw.Sounds.Count + rsw.Effects.Count);
            foreach (var m in rsw.Models)
            {
                I(1);
                S(m.Name, 40);
                I(m.AnimType);
                F(m.AnimSpeed);
                I(m.Collision);
                S(m.File, 80);
                S(m.Node, 80);
                V(m.Position);
                V(m.Rotation);
                V(m.Scale);
            }
            foreach (var l in rsw.Lights)
            {
                I(2);
                S(l.Name, 80);
                V(l.Position);
                F(l.Color.r); F(l.Color.g); F(l.Color.b);
                F(l.Range);
            }
            foreach (var s in rsw.Sounds)
            {
                I(3);
                S(s.Name, 80);
                S(s.File, 80);
                V(s.Position);
                F(s.Volume);
                I(s.Width);
                I(s.Height);
                F(s.Range);
                F(s.Interval);
            }
            foreach (var e in rsw.Effects)
            {
                I(4);
                S(e.Name, 80);
                V(e.Position);
                I(e.Preset);
                F(e.Delay);
                F(e.A); F(e.B); F(e.C); F(e.D);
            }

            for (int i = 0; i < QuadTreeNodes * 12; i++) F(0f);
            return b.ToArray();
        }

        /// <summary>Writes <c>&lt;name&gt;.rsw</c> into the data folder.</summary>
        public static void Write(string dataFolder, string name, RoRsw rsw)
        {
            Directory.CreateDirectory(dataFolder);
            File.WriteAllBytes(Path.Combine(dataFolder, name + ".rsw"), Rsw(rsw));
        }
    }
}
