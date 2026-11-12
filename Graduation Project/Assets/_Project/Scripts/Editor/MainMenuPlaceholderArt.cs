using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RetroFPS.Editor
{
    /// <summary>Deterministic edit-time Direction A textures; existing artist replacements are preserved.</summary>
    public static class MainMenuPlaceholderArt
    {
        /// <summary>Folder containing replaceable generated PNGs.</summary>
        public static string Folder => "Assets/_Project/Art/UI/MainMenu/Placeholder";

        /// <summary>Creates all missing textures and normalizes their UI import settings.</summary>
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            Color bone = Hex(0xE8DCC0), accent = Hex(0xDB143D), soot = Hex(0x0C0A0A);
            Write("Sigil_A_BoneSeal", 9, 11, (x, y) =>
            {
                int span = 4 - Mathf.FloorToInt(Mathf.Abs(y - 5) * 0.8f);
                int dx = Mathf.Abs(x - 4);
                if (dx > span) return Color.clear;
                if (dx == span || y == 0 || y == 10) return bone;
                return x == 4 && y >= 3 && y <= 7 ? soot : accent;
            }, false, Vector4.zero);
            Write("Sigil_A_BoneSealPressed", 9, 11, (x, y) =>
            {
                int span = 4 - Mathf.FloorToInt(Mathf.Abs(y - 5) * 0.8f);
                int dx = Mathf.Abs(x - 4);
                if (dx > span) return Color.clear;
                if (dx == span || y == 0 || y == 10) return bone;
                return x == 4 && y >= 3 && y <= 7 ? soot : Hex(0xFFF4DC);
            }, false, Vector4.zero);
            Write("FocusUnderline", 16, 2, (x, y) => x < 2 || x >= 14 ? bone : accent,
                false, new Vector4(2, 0, 2, 0));
            Write("PanelFrame_A", 48, 48, (x, y) => Frame(x, y, false), false, Vector4.one * 12f);
            Write("DialogFrame", 48, 48, (x, y) => Frame(x, y, true), false, Vector4.one * 12f);
            Write("ArtSky", 1, 64, (x, y) => Color.Lerp(Hex(0x2A0E0A), soot, y / 63f), true, Vector4.zero);
            Write("LeftScrim", 256, 1, (x, y) =>
            {
                Color color = soot;
                color.a = 0.92f * (1f - Mathf.InverseLerp(0.50f, 0.62f, x / 255f));
                return color;
            }, true, Vector4.zero);
            Write("ArtFog", 128, 32, (x, y) =>
            {
                // Sample a closed circle through noise space so the two horizontal edges match.
                float angle = (x == 127 ? 0f : x / 127f * Mathf.PI * 2f);
                float noise = Mathf.PerlinNoise(4f + Mathf.Cos(angle) * 1.3f,
                    y * 0.11f + Mathf.Sin(angle) * 1.3f);
                return new Color(0.44f, 0.36f, 0.34f, noise * Mathf.Sin(y / 31f * Mathf.PI) * 0.28f);
            }, true, Vector4.zero, true);
            Write("ArtTowerFar", 640, 360, (x, y) => Tower(x, y, false), false, Vector4.zero);
            Write("ArtTowerNear", 640, 360, (x, y) => Tower(x, y, true), false, Vector4.zero);
            Write("ArtTorchGlow", 64, 64, (x, y) =>
            {
                float alpha = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f);
                return new Color(1f, 1f, 1f, alpha * alpha * 0.4f);
            }, true, Vector4.zero);
            Write("Ember", 2, 2, (x, y) => Color.white, false, Vector4.zero);
        }

        /// <summary>Loads a generated texture as a UI sprite.</summary>
        public static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + name + ".png");
        /// <summary>Loads a generated texture for RawImage or particle material use.</summary>
        public static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + name + ".png");

        private static Color Frame(int x, int y, bool heavy)
        {
            int edge = Mathf.Min(x, y, 47 - x, 47 - y);
            bool corner = (x < 12 || x >= 36) && (y < 12 || y >= 36);
            if (edge <= (heavy ? 1 : 0) || corner && edge < 4)
                return (x + y) % 2 == 0 ? Hex(0xE8DCC0) : Hex(0x7A6E5E);
            if (heavy && edge == 4) return Hex(0xDB143D);
            return new Color(20f / 255f, 15f / 255f, 16f / 255f, 0.94f);
        }
        private static Color Tower(int x, int y, bool near)
        {
            int centre = near ? 470 : 407;
            int height = near ? 272 : 312;
            int halfWidth = y < 70 ? 105 : y < 150 ? 81 : y < 230 ? 56 : 34;
            if (y > height || Mathf.Abs(x - centre) > halfWidth) return Color.clear;
            if (y > height - 15 && ((x - centre + 40) / 9 & 1) == 0) return Color.clear;
            if (near && y > 95 && y < 113 && (Mathf.Abs(x - centre) < 4 || Mathf.Abs(x - centre - 32) < 4))
                return Hex(0xFF8C2A);
            return near ? Hex(0x0F0B0D) : Hex(0x151012);
        }
        private static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        private static void Write(string name, int width, int height, Func<int, int, Color> pixel,
            bool smooth, Vector4 border, bool repeat = false)
        {
            string path = Folder + "/" + name + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++) texture.SetPixel(x, y, pixel(x, y));
                    texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = smooth ? FilterMode.Bilinear : FilterMode.Point;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            var platform = new TextureImporterPlatformSettings
            {
                name = "DefaultTexturePlatform", format = TextureImporterFormat.RGBA32,
                maxTextureSize = 2048, textureCompression = TextureImporterCompression.Uncompressed
            };
            importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();
        }
    }
}
