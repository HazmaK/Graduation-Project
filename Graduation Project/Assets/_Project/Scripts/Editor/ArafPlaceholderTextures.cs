using System.IO;
using UnityEditor;
using UnityEngine;

namespace RetroFPS.Editor
{
    /// <summary>
    /// Araf için yer tutucu piksel dokular üretir ve zemin/duvar/kapı materyallerine dünya uzayı triplanar olarak bağlar.
    /// PNG zaten varsa ASLA üzerine yazılmaz: kendi dokunu aynı dosya adıyla koyarsan doğrudan o kullanılır.
    /// </summary>
    public static class ArafPlaceholderTextures
    {
        /// <summary>Araf dokularının klasörü; kullanıcı dokuları buraya aynı adla konur.</summary>
        public const string TextureDir = "Assets/_Project/Data/Araf/Textures";
        /// <summary>Zemin dokusu yolu.</summary>
        public const string FloorPath = TextureDir + "/Tex_Araf_Floor.png";
        /// <summary>Duvar/sütun (Bone materyali) dokusu yolu.</summary>
        public const string WallPath = TextureDir + "/Tex_Araf_Wall.png";
        /// <summary>Arena kapısı dokusu yolu.</summary>
        public const string DoorPath = TextureDir + "/Tex_Araf_DoorStone.png";

        private const string ShaderName = "RetroFPS/Araf/TriplanarLit";
        private const string MatDir = "Assets/_Project/Data/Materials/Araf";
        private const int Size = 128;
        // 128 px / 4 m = 32 px/m: klasik boomer shooter doku yoğunluğu.
        private const float FloorTileMeters = 4f;
        private const float WallTileMeters = 4f;
        private const float DoorTileMeters = 2f;

        /// <summary>Dokuları (yoksa) üretir ve Araf materyallerine uygular; sahneyi yeniden kurmaz.</summary>
        [MenuItem("RetroFPS/Araf/Apply Placeholder Textures", false, 22)]
        public static void ApplyFromMenu()
        {
            // Mat_Araf_Bone kulede de kullanılıyor; dokulu duvar materyali Mat_Araf_Wall'dır.
            Apply(Load("Mat_Araf_Wall"), Load("Mat_Araf_Floor"), Load("Mat_Araf_DoorStone"));
            AssetDatabase.SaveAssets();
        }

        /// <summary>Verilen materyallere triplanar shader ve dokuyu bağlar; null materyal atlanır.</summary>
        public static void Apply(Material wall, Material floor, Material doorStone)
        {
            Directory.CreateDirectory(TextureDir);
            Texture2D floorTex = Ensure(FloorPath, FillFloor);
            Texture2D wallTex = Ensure(WallPath, FillWall);
            Texture2D doorTex = Ensure(DoorPath, FillDoor);
            Bind(floor, floorTex, FloorTileMeters, new Color(0.62f, 0.58f, 0.70f));
            Bind(wall, wallTex, WallTileMeters, new Color(0.85f, 0.82f, 0.90f));
            Bind(doorStone, doorTex, DoorTileMeters, new Color(0.9f, 0.88f, 0.95f));
        }

        private static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + name + ".mat");

        private static void Bind(Material material, Texture2D texture, float tileMeters, Color tint)
        {
            if (material == null) return;
            Shader shader = Shader.Find(ShaderName);
            if (shader != null && shader.isSupported)
            {
                material.shader = shader;
                material.SetFloat("_TileSize", tileMeters);
            }
            // Shader derlenemezse URP Lit kalır; doku yine görünür (UV ile).
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(material);
        }

        private delegate Color PixelFill(int x, int y);

        private static Texture2D Ensure(string path, PixelFill fill)
        {
            if (!File.Exists(path))
            {
                Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[Size * Size];
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                        pixels[y * Size + x] = fill(x, y);
                tex.SetPixels(pixels);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            ConfigureImporter(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void ConfigureImporter(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            bool dirty = importer.filterMode != FilterMode.Point
                || importer.wrapMode != TextureWrapMode.Repeat
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || !importer.mipmapEnabled;
            if (!dirty) return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // ---- Desen üreticiler (deterministik, döşenebilir) ----

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        // Periyodu Size'ı bölen değer gürültüsü: kenarlarda dikişsiz döşenir.
        private static float Noise(int x, int y, int cell, int seed)
        {
            int period = Size / cell;
            float fx = (float)x / cell, fy = (float)y / cell;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Hash(Wrap(x0, period), Wrap(y0, period), seed);
            float b = Hash(Wrap(x0 + 1, period), Wrap(y0, period), seed);
            float c = Hash(Wrap(x0, period), Wrap(y0 + 1, period), seed);
            float d = Hash(Wrap(x0 + 1, period), Wrap(y0 + 1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static int Wrap(int v, int period) => ((v % period) + period) % period;

        private static float Grain(int x, int y, int seed)
            => Noise(x, y, 16, seed) * 0.5f + Noise(x, y, 4, seed + 7) * 0.3f + Hash(x, y, seed + 13) * 0.2f;

        // Taşlı blok deseni: blok içi rengi, harç çizgisi ve 1 px pah (üst-sol açık, alt-sağ koyu).
        private static Color Blocks(int x, int y, int blockW, int blockH, bool runningBond,
            Color stone, Color mortar, float variation, int seed)
        {
            int row = y / blockH;
            int offset = runningBond && (row & 1) == 1 ? blockW / 2 : 0;
            int bx = Wrap(x + offset, Size);
            int col = bx / blockW;
            int lx = bx % blockW, ly = y % blockH;
            if (lx < 2 || ly < 2) return mortar * (0.8f + 0.4f * Hash(x, y, seed + 3));
            float stoneVar = (Hash(col, row, seed) - 0.5f) * variation;
            float g = Grain(x, y, seed + col * 31 + row * 17);
            Color c = stone * (0.78f + 0.44f * g + stoneVar);
            if (lx == 2 || ly == blockH - 1) c *= 1.18f;
            else if (lx == blockW - 1 || ly == 2) c *= 0.7f;
            // Seyrek koyu leke (kurum/yosun).
            if (Hash(col, row, seed + 99) > 0.82f) c *= 0.6f + 0.4f * Noise(x, y, 8, seed + 5);
            c.a = 1f;
            return c;
        }

        private static Color FillFloor(int x, int y) => Blocks(x, y, 32, 32, true,
            new Color(0.34f, 0.29f, 0.40f), new Color(0.07f, 0.04f, 0.09f), 0.35f, 11);

        private static Color FillWall(int x, int y)
        {
            Color c = Blocks(x, y, 32, 16, true,
                new Color(0.58f, 0.53f, 0.60f), new Color(0.12f, 0.09f, 0.14f), 0.25f, 23);
            // Yukarıdan aşağı akan kir izleri.
            float streak = Noise(x, 0, 4, 41) * (1f - y / (float)Size) * 0.35f;
            c *= 1f - streak;
            c.a = 1f;
            return c;
        }

        private static Color FillDoor(int x, int y)
        {
            Color stone = new Color(0.30f, 0.26f, 0.36f);
            float g = Grain(x, y, 57);
            Color c = stone * (0.8f + 0.4f * g);
            const int frame = 8;
            int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(Size - 1 - x, Size - 1 - y));
            if (edge < frame) c *= edge == 0 ? 0.5f : edge == frame - 1 ? 0.65f : 1.15f;
            // İç panelde yatay oyuklar.
            else if ((y - frame) % 30 < 2) c *= 0.55f;
            c.a = 1f;
            return c;
        }
    }
}
