using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Editor.Ui;

namespace Uninarok.RoImport.Tests.Rsm
{
    /// <summary>
    /// Access to the real source data for tests marked <c>NeedsData</c> (PLAN-0003 §6). When the data folder or a file is
    /// absent the test is ignored with a message, never failed.
    /// </summary>
    public static class RsmTestData
    {
        public const string Category = "NeedsData";

        public static string FixtureFolder => Path.Combine(Application.dataPath, "Tests/EditMode/RoImport/Rsm/Fixtures~");

        /// <summary>The bytes of <c>model/&lt;relative&gt;</c>, or an ignored test when the data is not there.</summary>
        public static byte[] ModelBytes(string relative)
        {
            string root = RoSourceLocation.Resolve();
            if (!Directory.Exists(root)) Assert.Ignore($"source data folder not found: {root}");
            string file = Path.Combine(root, "model", relative);
            if (!File.Exists(file)) Assert.Ignore($"source file not found: model/{relative}");
            return File.ReadAllBytes(file);
        }

        /// <summary>True when <c>texture/&lt;relative&gt;</c> exists in the source data.</summary>
        public static bool TextureExists(string relative) =>
            File.Exists(Path.Combine(RoSourceLocation.Resolve(), "texture", relative));

        /// <summary>One expected node of a reference model (Fixtures~/reference-nodes.csv, reference-models.md §7).</summary>
        public sealed class ExpectedNode
        {
            public string Model, Name, Parent;
            public int Vertices, TexCoords, Faces, PosKeys, RotKeys;
        }

        public static IReadOnlyList<ExpectedNode> ReferenceNodes()
        {
            return File.ReadAllLines(Path.Combine(FixtureFolder, "reference-nodes.csv"))
                .Where(l => l.Length > 0 && !l.StartsWith("#"))
                .Select(l => l.Split(','))
                .Select(c => new ExpectedNode
                {
                    Model = c[0], Name = c[1], Parent = c[2],
                    Vertices = int.Parse(c[3]), TexCoords = int.Parse(c[4]), Faces = int.Parse(c[5]),
                    PosKeys = int.Parse(c[6]), RotKeys = int.Parse(c[7])
                })
                .ToList();
        }

        /// <summary>The reference models, in table order.</summary>
        public static IEnumerable<string> ReferenceModels() => ReferenceNodes().Select(n => n.Model).Distinct();
    }
}
