using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Uninarok.Effects.Editor
{
    /// <summary>
    /// A preview scene with its own <see cref="EffectRuntime"/> and an orbit camera (SPEC-0012 FR-11, plan 4.11): the runtime's
    /// root, pooled objects and materials live in a <see cref="PreviewRenderUtility"/> scene, so nothing reaches an open scene.
    /// The camera is a 15 degree perspective on a sphere around <see cref="Target"/>; the background is dark, grey, a checker
    /// on a far quad or a 0.5 u ground grid. The window ticks it and calls <see cref="Render(Rect)"/>. Dispose it to release everything.
    /// </summary>
    public sealed class EffectPreviewStage : IDisposable
    {
        public enum BackgroundKind { Dark, Grey, Checker, Grid }

        public const string SettingsPath = "Assets/Settings/Effects/EffectSettings.asset";
        public const float FieldOfView = 15f;
        public const float GridCell = 0.5f;
        const float CheckerDistance = 120f;
        const float GridHalfSize = 20f;

        readonly PreviewRenderUtility preview;
        readonly GameObject root;
        readonly GameObject checkerQuad;
        readonly GameObject gridQuad;
        readonly Material checkerMaterial, gridMaterial;
        readonly Texture2D checkerTexture, gridTexture;
        readonly EffectSettings settings;
        readonly bool ownsSettings;
        BackgroundKind background;
        bool disposed;

        public EffectPreviewStage(EffectCatalog catalog, EffectSettings settings = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            Catalog = catalog;
            if (settings == null) settings = AssetDatabase.LoadAssetAtPath<EffectSettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<EffectSettings>(); ownsSettings = true; }
            this.settings = settings;

            preview = new PreviewRenderUtility();
            var cam = preview.camera;
            cam.orthographic = false;
            cam.fieldOfView = FieldOfView;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;

            root = new GameObject("Effect Preview Root") { hideFlags = HideFlags.HideAndDontSave };
            preview.AddSingleGO(root);
            Runtime = new EffectRuntime(catalog, this.settings, root.transform, 64);

            var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            checkerTexture = MakeTexture(2, 16, new Color(0.30f, 0.30f, 0.30f), new Color(0.42f, 0.42f, 0.42f), false);
            gridTexture = MakeTexture(1, 64, new Color(0.22f, 0.22f, 0.22f, 1f), new Color(0.55f, 0.55f, 0.55f, 1f), true);
            checkerMaterial = new Material(unlit) { hideFlags = HideFlags.HideAndDontSave, mainTexture = checkerTexture };
            gridMaterial = new Material(unlit) { hideFlags = HideFlags.HideAndDontSave, mainTexture = gridTexture };
            checkerQuad = MakeQuad("Checker", checkerMaterial);
            gridQuad = MakeQuad("Grid", gridMaterial);
            gridQuad.transform.SetPositionAndRotation(new Vector3(0f, -0.001f, 0f), Quaternion.Euler(90f, 0f, 0f));
            gridQuad.transform.localScale = new Vector3(GridHalfSize * 2f, GridHalfSize * 2f, 1f);
            gridMaterial.mainTextureScale = new Vector2(GridHalfSize * 2f / GridCell, GridHalfSize * 2f / GridCell);
            Background = BackgroundKind.Dark;
        }

        public EffectCatalog Catalog { get; }
        public EffectRuntime Runtime { get; }

        public float Yaw { get; set; }
        public float Pitch { get; set; } = 35f;
        public float Distance { get; set; } = 14f;
        /// <summary>The point the camera looks at.</summary>
        public Vector3 Target { get; set; } = new Vector3(0f, 0.5f, 0f);

        public Quaternion CameraRotation => Quaternion.Euler(Pitch, Yaw, 0f);
        public Camera Camera => preview.camera;

        public BackgroundKind Background
        {
            get => background;
            set
            {
                background = value;
                preview.camera.backgroundColor = value == BackgroundKind.Grey ? new Color(0.5f, 0.5f, 0.5f, 1f) : new Color(0.12f, 0.12f, 0.13f, 1f);
                checkerQuad.SetActive(value == BackgroundKind.Checker);
                gridQuad.SetActive(value == BackgroundKind.Grid);
            }
        }

        /// <summary>Plays at the origin of the stage; the caller keeps the handle.</summary>
        public EffectHandle Play(EffectRef entry, bool loop = false) => Runtime.Play(entry, Vector3.zero, loop);

        /// <summary>Plays the catalog key at the origin; <c>default</c> when the key is not in the catalog.</summary>
        public EffectHandle Play(string key, bool loop = false)
        {
            return Catalog.TryResolve(key, out var entry) ? Runtime.Play(entry, Vector3.zero, loop) : default;
        }

        /// <summary>Advances the runtime by wall seconds (the window calls it from <c>EditorApplication.update</c>).</summary>
        public void Tick(float deltaSeconds) => Runtime.Tick(deltaSeconds);

        void PlaceCamera()
        {
            var cam = preview.camera;
            cam.transform.rotation = CameraRotation;
            cam.transform.position = Target - cam.transform.forward * Mathf.Max(0.5f, Distance);
            if (checkerQuad.activeSelf)
            {
                var t = checkerQuad.transform;
                t.rotation = cam.transform.rotation;
                t.position = cam.transform.position + cam.transform.forward * CheckerDistance;
                float h = 2f * CheckerDistance * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * 1.2f;
                float w = h * Mathf.Max(1f, cam.aspect);
                t.localScale = new Vector3(w, h, 1f);
                checkerMaterial.mainTextureScale = new Vector2(w / 4f, h / 4f);
            }
        }

        /// <summary>The preview in a window rectangle.</summary>
        public Texture Render(Rect rect)
        {
            preview.BeginPreview(rect, GUIStyle.none);
            PlaceCamera();
            preview.camera.Render();
            return preview.EndPreview();
        }

        /// <summary>Renders into <paramref name="target"/> (Save image); the caller owns the texture.</summary>
        public void Render(RenderTexture target)
        {
            var cam = preview.camera;
            var previous = cam.targetTexture;
            cam.targetTexture = target;
            cam.aspect = (float)target.width / target.height;
            PlaceCamera();
            cam.Render();
            cam.ResetAspect();
            cam.targetTexture = previous;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Runtime.Dispose();
            if (root != null) Object.DestroyImmediate(root);
            if (checkerQuad != null) Object.DestroyImmediate(checkerQuad);
            if (gridQuad != null) Object.DestroyImmediate(gridQuad);
            if (checkerMaterial != null) Object.DestroyImmediate(checkerMaterial);
            if (gridMaterial != null) Object.DestroyImmediate(gridMaterial);
            if (checkerTexture != null) Object.DestroyImmediate(checkerTexture);
            if (gridTexture != null) Object.DestroyImmediate(gridTexture);
            if (ownsSettings && settings != null) Object.DestroyImmediate(settings);
            preview.Cleanup();
        }

        GameObject MakeQuad(string name, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            preview.AddSingleGO(go);
            return go;
        }

        // cells x cells squares of px pixels in two shades (checker), or one tile with a 1 px border on two edges (grid)
        static Texture2D MakeTexture(int cells, int px, Color a, Color b, bool lines)
        {
            int size = cells * px;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = lines
                        ? (x == 0 || y == 0 ? b : a)
                        : (((x / px) + (y / px)) % 2 == 0 ? a : b);
                }
            t.SetPixels(pixels);
            t.Apply(true);
            t.filterMode = lines ? FilterMode.Trilinear : FilterMode.Point;
            return t;
        }
    }
}
