using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RetroFPS.Editor
{
    /// <summary>Generates the fourteen replaceable HUD masks with point-filtered sprite import contracts.</summary>
    public static class HudPlaceholderArt
    {
        /// <summary>Gets the generated HUD texture folder.</summary>
        public static string Folder => "Assets/_Project/Art/UI/CombatHud/Placeholder";

        /// <summary>Creates missing/empty PNGs; overwrite also repairs textures left by a failed build.</summary>
        public static void Generate(bool overwrite = false)
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            Write("HUD_Plate_9S", 12, 12, (x, y) =>
                (x == 0 || x == 11) && (y == 0 || y == 11) ? Color.clear : Color.white,
                Vector4.one * 2f, overwrite);
            Write("HUD_Chip_9S", 6, 6, (x, y) => Color.white, Vector4.one, overwrite);
            Write("HUD_Bar_Track", 8, 4, (x, y) => Color.white, new Vector4(1, 0, 1, 0), overwrite);
            Write("HUD_Bar_Fill", 8, 4, (x, y) => Color.white, new Vector4(1, 0, 1, 0), overwrite);
            Mask("HUD_Cross", new[] { "000111000", "000111000", "000111000", "111111111", "111111111", "111111111", "000111000", "000111000", "000111000" }, overwrite);
            Mask("HUD_Diamond", new[] { "000010000", "000111000", "001111100", "011111110", "111111111", "011111110", "001111100", "000111000", "000010000" }, overwrite);
            Mask("HUD_Shield", new[] { "011111110", "111111111", "111111111", "111111111", "011111110", "011111110", "001111100", "000111000", "000010000" }, overwrite);
            Mask("HUD_Hook", new[] { "000011000", "000011000", "000011000", "000011000", "000011000", "110011000", "110011000", "011111000", "001110000" }, overwrite);
            Mask("HUD_Weapon_Bolt", new[] { "000001100000", "000011110000", "011111111110", "111111111111", "000001100000", "000001100000", "000011110000", "000011110000", "000001100000" }, overwrite);
            Mask("HUD_Weapon_Shell", new[] { "000000000000", "001110011100", "001110011100", "001110011100", "001110011100", "001110011100", "001110011100", "011111111110", "011111111110" }, overwrite);
            Mask("HUD_Weapon_Blade", new[] { "000000000110", "000000001110", "000000011100", "000000111000", "000001110000", "001111100000", "000111000000", "001101100000", "011000000000" }, overwrite);
            Mask("HUD_Sigil_Araf", new[] { "00100", "01110", "11111", "11011", "11111", "01110", "00100" }, overwrite);
            Mask("HUD_Arrow", new[] { "00100", "00010", "11111", "00010", "00100" }, overwrite);
            Write("HUD_Hatch", 4, 4, (x, y) => (x + y) % 4 < 2 ? Color.white : Color.clear, Vector4.zero, overwrite);
        }

        /// <summary>Loads a named HUD sprite without copying any menu art.</summary>
        public static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + name + ".png");

        private static void Mask(string name, string[] rows, bool overwrite)
            => Write(name, rows[0].Length, rows.Length,
                (x, y) => rows[rows.Length - 1 - y][x] == '1' ? Color.white : Color.clear, Vector4.zero, overwrite);

        private static void Write(string name, int width, int height, Func<int, int, Color> pixel, Vector4 border, bool overwrite)
        {
            string path = Folder + "/" + name + ".png";
            if (overwrite || !File.Exists(path) || new FileInfo(path).Length == 0)
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++) texture.SetPixel(x, y, pixel(x, y));
                    texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.npotScale = TextureImporterNPOTScale.None;
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.RGBA32; platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform); importer.SaveAndReimport();
        }
    }
}
