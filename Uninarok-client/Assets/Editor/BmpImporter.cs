using UnityEditor;
using UnityEngine;

namespace Assets.Editor
{
    //[ScriptedImporter(1, "bmp")]
    internal class BmpImporter : AssetPostprocessor
    {
        // Tolerância para capturar o rosa do Ragnarok (caso haja alguma variação mínima de compressão)
        private const byte Tolerance = 10;

        void OnPreprocessTexture()
        {
            // Verifica se o arquivo é um BMP
            if (assetPath.EndsWith(".bmp", System.StringComparison.OrdinalIgnoreCase))
            {
                TextureImporter importer = (TextureImporter)assetImporter;

                // Define automaticamente o Texture Type para Sprite (2D and UI)
                importer.textureType = TextureImporterType.Sprite;

                // Obrigatório para permitir leitura e alteração dos pixels por script
                importer.isReadable = true;

                // Garante que o formato mantenha o canal alfa ativado
                importer.alphaSource = TextureImporterAlphaSource.FromInput;

                importer.alphaIsTransparency = true;

                // Desativa compressão padrão para evitar que o rosa "vaze" ou distorça nas bordas
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                var platformTextureSettings = importer.GetDefaultPlatformTextureSettings();
                platformTextureSettings.format = TextureImporterFormat.RGBA32;
                importer.SetPlatformTextureSettings(platformTextureSettings);
            }
        }

        void OnPostprocessTexture(Texture2D texture)
        {
            if (assetPath.EndsWith(".bmp", System.StringComparison.OrdinalIgnoreCase))
            {
                Color32[] pixels = texture.GetPixels32();
                bool modified = false;

                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 p = pixels[i];

                    // Identifica o Rosa do Ragnarok: Vermelho alto, Verde baixo, Azul alto
                    if (p.r >= (255 - Tolerance) && p.g <= Tolerance && p.b >= (255 - Tolerance))
                    {
                        // Torna o pixel totalmente transparente
                        pixels[i] = new Color32(0, 0, 0, 0);
                        modified = true;
                    }
                }

                // Se encontrou pixels rosas, aplica as alterações de volta na textura
                if (modified)
                {
                    texture.SetPixels32(pixels);
                    texture.Apply();
                }
            }
        }
    }
}
