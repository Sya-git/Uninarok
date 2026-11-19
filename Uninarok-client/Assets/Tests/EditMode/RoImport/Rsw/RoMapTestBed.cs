using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsw;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Map;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Tests.Gat;
using ImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Rsw
{
    /// <summary>
    /// A temporary data folder with a synthetic map (TASKS-0006 T5.1): the fixture models and texture of
    /// <c>Rsw/Fixtures~</c>, a flat 4 x 3 ground, its 8 x 6 walkability and an RSW built in code. Outputs go under
    /// <see cref="RoImportTestRoot"/>; the open scene and the build settings are restored on dispose.
    /// </summary>
    public sealed class RoMapTestBed : IDisposable
    {
        public const int Width = 4, Height = 3;
        public const string BoxFile = "외부소품\\box.rsm";
        public const string SpinFile = "외부소품\\spin.rsm";
        public const string MissingFile = "없는모델\\missing.rsm";

        static string FixtureFolder => Path.Combine(Application.dataPath, "Tests/EditMode/RoImport/Rsw/Fixtures~/data");

        readonly EditorBuildSettingsScene[] _buildScenes;

        public RoMapTestBed(string mapName = "sample")
        {
            MapName = mapName;
            _buildScenes = EditorBuildSettings.scenes;
            string tmp = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            Data = Path.Combine(tmp, "data");
            LedgerFolder = Path.Combine(tmp, "ledger");
            Roots = RoImportTestRoot.Create();
            Copy(FixtureFolder, Data);
            WriteSources(Sample());
        }

        public string MapName { get; }
        public string Data { get; }
        public string LedgerFolder { get; }
        public RoOutputRoots Roots { get; }
        public RoMapOutputPaths Paths => RoMapOutputPaths.For(Roots, RoPath.Parse(MapName + ".rsw"));
        public RoReport LastReport { get; private set; }

        /// <summary>
        /// The sample map, in file order: a box at the centre, a rotated and scaled box, an animated box with half speed, an
        /// animated model placed as static (type 0), two instances of a missing model, a mirrored box; a light, a sound and
        /// an effect.
        /// </summary>
        public static RoRsw Sample()
        {
            var rsw = RswTestMaps.Full().Rsw;
            rsw.GndFile = "sample.gnd";
            rsw.GatFile = "sample.gat";
            rsw.Models = new List<RoRswModel>
            {
                RswTestMaps.Model("box_centre", new Vector3(0, 0, 0), file: BoxFile),
                RswTestMaps.Model("box_turned", new Vector3(10, -2, 5), new Vector3(10, 45, 20), new Vector3(1.5f, 1.5f, 1.5f), file: BoxFile),
                RswTestMaps.Model("spin_moving", new Vector3(-10, 0, -5), file: SpinFile, animType: 2, animSpeed: 0.5f),
                RswTestMaps.Model("spin_still", new Vector3(-10, 0, 5), file: SpinFile, animType: 0),
                RswTestMaps.Model("gone_a", new Vector3(5, -1, -5), new Vector3(0, 90, 0), file: MissingFile),
                RswTestMaps.Model("gone_b", new Vector3(15, 0, -10), file: MissingFile),
                RswTestMaps.Model("box_mirror", new Vector3(0, 0, 10), scale: new Vector3(-1, 1, 1), file: BoxFile),
            };
            return rsw;
        }

        public void WriteSources(RoRsw rsw) => WriteMap(MapName, rsw);

        /// <summary>Writes the ground, walkability and RSW of a map called <paramref name="name"/> into the data folder.</summary>
        public void WriteMap(string name, RoRsw rsw)
        {
            Directory.CreateDirectory(Data);
            File.WriteAllBytes(Path.Combine(Data, name + ".gnd"), RswTestFiles.Gnd(Width, Height));
            File.WriteAllBytes(Path.Combine(Data, name + ".gat"), GatTestFiles.Gat(GatTestMaps.Asymmetric(2 * Width, 2 * Height)));
            rsw.GndFile = name + ".gnd";
            rsw.GatFile = name + ".gat";
            RswTestFiles.Write(Data, name, rsw);
        }

        public RoImportOptions Options(bool force = false) => new RoImportOptions
        {
            Force = force,
            ShowProgress = false,
            Roots = Roots,
            Source = new RoFolderSource(Data),
            LedgerFolder = LedgerFolder
        };

        /// <summary>Converts the map through the public entry point and keeps the report of the run.</summary>
        public RoResult<SceneAsset> Convert(bool force = false)
        {
            var report = new RoReport();
            var result = ImportApi.Map(MapName, Options(force), report);
            LastReport = report;
            return result;
        }

        /// <summary>Opens the generated scene as the single scene and returns the map root.</summary>
        public GameObject OpenScene()
        {
            var scene = EditorSceneManager.OpenScene(Paths.Scene, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            Assert.AreEqual(1, roots.Length, "one root object");
            return roots[0];
        }

        /// <summary>The hierarchy of the generated scene as text: names, transforms, components, static flags, prefab sources.</summary>
        public string DumpScene()
        {
            var root = OpenScene();
            var sb = new StringBuilder();
            sb.AppendLine($"ambient {RenderSettings.ambientMode} {RenderSettings.ambientLight}");
            Dump(root.transform, 0, sb);
            return sb.ToString();
        }

        static void Dump(Transform t, int depth, StringBuilder sb)
        {
            var go = t.gameObject;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            string prefab = source != null && PrefabUtility.IsOutermostPrefabInstanceRoot(go)
                ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source))
                : "-";
            string components = string.Join(",", go.GetComponents<Component>().Select(c => c == null ? "missing" : c.GetType().Name));
            sb.Append(' ', depth * 2)
              .Append($"{go.name} p{t.localPosition.ToString("F4")} r{t.localRotation.eulerAngles.ToString("F3")} s{t.localScale.ToString("F4")} ")
              .Append($"[{components}] static {GameObjectUtility.GetStaticEditorFlags(go)} prefab {prefab} active {go.activeSelf}")
              .AppendLine();
            for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), depth + 1, sb);
        }

        public void Dispose()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path.StartsWith(RoImportTestRoot.Folder))
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    break;
                }
            EditorBuildSettings.scenes = _buildScenes;
            RoImportTestRoot.Delete();
            string tmp = Path.GetDirectoryName(Data);
            if (tmp != null && Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true);
        }

        static void Copy(string from, string to)
        {
            foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(to, file.Substring(from.Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target);
            }
        }
    }
}
