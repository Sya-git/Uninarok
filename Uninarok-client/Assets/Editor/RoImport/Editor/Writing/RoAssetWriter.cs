using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Results;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Editor.Writing
{
    /// <summary>
    /// The only code that writes assets (plan 4.7; SPEC-0002 FR-4, FR-5, FR-16). Outputs are updated in place, never deleted
    /// and re-created, so GUIDs and every reference survive; identical content is not rewritten, so a rerun touches no file.
    /// Unity assigns GUIDs; this class never writes a <c>.meta</c> (D4). Main thread only.
    /// </summary>
    public sealed class RoAssetWriter
    {
        readonly string[] _roots;
        readonly string _scenesRoot;
        readonly string _projectRoot;
        readonly List<(string path, Action<AssetImporter> configure)> _deferredConfigure = new List<(string, Action<AssetImporter>)>();
        int _batchDepth;

        public RoAssetWriter(RoOutputRoots roots)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            _roots = new[] { Root(roots.Art), Root(roots.Data), Root(roots.Scenes) };
            _scenesRoot = Root(roots.Scenes);
            _projectRoot = Path.GetDirectoryName(Application.dataPath) ?? ".";
        }

        public bool InBatch => _batchDepth > 0;

        /// <summary>
        /// Writes a file asset (PNG). Identical bytes are not rewritten. <paramref name="configure"/> sets the importer and is
        /// applied, with a reimport, only when it changes the importer's settings.
        /// </summary>
        public RoStatus WriteBytes(string projectPath, byte[] bytes, Action<AssetImporter> configure = null)
        {
            string path = Checked(projectPath);
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            string disk = Disk(path);

            bool existed = File.Exists(disk);
            bool sameBytes = existed && SameBytes(disk, bytes);
            if (!sameBytes)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(disk) ?? ".");
                File.WriteAllBytes(disk, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            bool settingsChanged = configure != null && Configure(path, configure);
            if (!existed) return RoStatus.Created;
            return sameBytes && !settingsChanged ? RoStatus.Skipped : RoStatus.Updated;
        }

        /// <summary>
        /// Creates <paramref name="fresh"/> as a new asset, or copies its content into the existing asset at the path. The
        /// existing object keeps its identity and GUID. <paramref name="fresh"/> belongs to the writer afterwards: it becomes
        /// the asset when created and is destroyed otherwise.
        /// </summary>
        public RoStatus UpsertObject<T>(string projectPath, T fresh) where T : Object
        {
            string path = Checked(projectPath);
            if (fresh == null) throw new ArgumentNullException(nameof(fresh));

            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                if (File.Exists(Disk(path)))
                    throw new InvalidOperationException($"'{path}' exists but is not a {typeof(T).Name}; the writer will not replace it.");
                EnsureFolder(Parent(path));
                AssetDatabase.CreateAsset(fresh, path);
                return RoStatus.Created;
            }

            fresh.name = existing.name;
            if (EditorJsonUtility.ToJson(existing) == EditorJsonUtility.ToJson(fresh) && SameMesh(existing, fresh))
            {
                Object.DestroyImmediate(fresh);
                return RoStatus.Skipped;
            }

            EditorUtility.CopySerialized(fresh, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(fresh);
            if (!InBatch) AssetDatabase.SaveAssetIfDirty(existing);
            return RoStatus.Updated;
        }

        public bool Exists(string projectPath) => File.Exists(Disk(Normalize(projectPath)));

        public T Load<T>(string projectPath) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Normalize(projectPath));

        /// <summary>Starts (or nests) a block of writes: <c>AssetDatabase.StartAssetEditing</c> on the outermost call.</summary>
        public void BeginBatch()
        {
            if (_batchDepth++ == 0) AssetDatabase.StartAssetEditing();
        }

        /// <summary>
        /// Ends a block. The outermost call stops asset editing (running the queued imports), applies the importer settings of
        /// files created inside the block, and saves dirty assets.
        /// </summary>
        public void EndBatch()
        {
            if (_batchDepth == 0) throw new InvalidOperationException("EndBatch without BeginBatch.");
            if (--_batchDepth > 0) return;

            AssetDatabase.StopAssetEditing();
            var deferred = _deferredConfigure.ToArray();
            _deferredConfigure.Clear();
            foreach (var (path, configure) in deferred) Configure(path, configure);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Applies <paramref name="configure"/>; true when it changed the importer (which is then reimported).</summary>
        bool Configure(string path, Action<AssetImporter> configure)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                // A file created inside a batch has no importer until the batch's imports run.
                if (InBatch) _deferredConfigure.Add((path, configure));
                return false;
            }

            string before = EditorJsonUtility.ToJson(importer);
            configure(importer);
            if (EditorJsonUtility.ToJson(importer) == before) return false;
            importer.SaveAndReimport();
            return true;
        }

        // A mesh's vertex data may not appear in its JSON form; compare it explicitly so a changed mesh is never skipped.
        static bool SameMesh(Object a, Object b)
        {
            if (!(a is Mesh ma) || !(b is Mesh mb)) return true;
            if (ma.vertexCount != mb.vertexCount || ma.subMeshCount != mb.subMeshCount) return false;
            if (!SameArray(ma.vertices, mb.vertices) || !SameArray(ma.normals, mb.normals) || !SameArray(ma.uv, mb.uv)
                || !SameArray(ma.colors32, mb.colors32) || !SameArray(ma.tangents, mb.tangents)) return false;
            for (int s = 0; s < ma.subMeshCount; s++)
                if (!SameArray(ma.GetTriangles(s), mb.GetTriangles(s))) return false;
            return true;
        }

        static bool SameArray<TItem>(TItem[] a, TItem[] b)
        {
            if (a.Length != b.Length) return false;
            var cmp = EqualityComparer<TItem>.Default;
            for (int i = 0; i < a.Length; i++)
                if (!cmp.Equals(a[i], b[i])) return false;
            return true;
        }

        static bool SameBytes(string disk, byte[] bytes)
        {
            var info = new FileInfo(disk);
            if (info.Length != bytes.Length) return false;
            byte[] current = File.ReadAllBytes(disk);
            for (int i = 0; i < current.Length; i++)
                if (current[i] != bytes[i]) return false;
            return true;
        }

        void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Parent(folder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(parent.Length + 1));
        }

        /// <summary>Normalizes the path and refuses anything outside the output roots, and the hand-made additive scenes.</summary>
        string Checked(string projectPath)
        {
            string path = Normalize(projectPath);
            foreach (string segment in path.Split('/'))
                if (segment.Length == 0 || segment == "." || segment == "..")
                    throw new ArgumentException($"'{projectPath}' is not a plain project path.", nameof(projectPath));

            bool inRoot = false;
            foreach (string root in _roots)
                if (path.StartsWith(root + "/", StringComparison.Ordinal)) { inRoot = true; break; }
            if (!inRoot)
                throw new ArgumentException($"'{projectPath}' is outside the output roots ({string.Join(", ", _roots)}).", nameof(projectPath));

            if (path.StartsWith(_scenesRoot + "/", StringComparison.Ordinal)
                && path.EndsWith(RoOutputPaths.AdditiveSuffix + ".unity", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"'{projectPath}' is a hand-made additive scene; the importer never writes it (plan 4.13).", nameof(projectPath));
            return path;
        }

        string Disk(string projectPath) => Path.Combine(_projectRoot, projectPath);

        static string Normalize(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath)) throw new ArgumentException("A project path is required.", nameof(projectPath));
            return projectPath.Replace('\\', '/');
        }

        static string Root(string root) => Normalize(root).TrimEnd('/');

        static string Parent(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash < 0 ? string.Empty : path.Substring(0, slash);
        }
    }
}
