using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Uninarok.Effects.Editor
{
    /// <summary>
    /// Evidence files of the effect preview (SPEC-0012 FR-1, FR-11, T8.4): the "Save image" PNG of the stage and the
    /// "Save check report" text. The writers take the folder, so tests use a temp folder.
    /// </summary>
    public static class EffectEvidence
    {
        public const string CheckReportFileName = "check-report.txt";

        /// <summary>docs/specs/0012-effects/evidence under the project root (Application.dataPath/../..).</summary>
        public static string DefaultFolder =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "specs", "0012-effects", "evidence"));

        /// <summary>The PNG name: the key with '/' replaced by '_', then <c>-f&lt;frame&gt;.png</c>.</summary>
        public static string ImageFileName(string key, int frame)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("The key is empty.", nameof(key));
            return key.Replace('/', '_') + "-f" + frame.ToString(CultureInfo.InvariantCulture) + ".png";
        }

        /// <summary>Writes the PNG bytes to <paramref name="folder"/> (created when missing) and returns the file path.</summary>
        public static string WriteImage(byte[] png, string folder, string key, int frame)
        {
            if (png == null) throw new ArgumentNullException(nameof(png));
            if (string.IsNullOrEmpty(folder)) throw new ArgumentException("The folder is empty.", nameof(folder));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, ImageFileName(key, frame));
            File.WriteAllBytes(path, png);
            return path;
        }

        /// <summary>Writes the report text as UTF-8 without BOM, byte for byte, to <paramref name="folder"/> and returns the file path.</summary>
        public static string WriteCheckReport(string text, string folder)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (string.IsNullOrEmpty(folder)) throw new ArgumentException("The folder is empty.", nameof(folder));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, CheckReportFileName);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }

        /// <summary>Renders the stage at width x height into a temporary render texture and returns it as PNG bytes.</summary>
        public static byte[] CapturePng(EffectPreviewStage stage, int width, int height)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            try
            {
                stage.Render(rt);
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    tex.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                    tex.Apply(false);
                    return tex.EncodeToPNG();
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
