using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Набор материалов для заброшенных панельных домов, квартир, мебели и декалей.
    /// Использует бесшовные процедурные текстуры 512x512 и карты нормалей из <see cref="ProceduralTextureFactory"/>;
    /// при наличии PBR-наборов в Assets/Textures/City <see cref="CityPbr"/> заменяет их.
    /// </summary>
    public sealed class AbandonedMaterials
    {
        private const string TexFolder = "Assets/_Project/Art/Textures/Abandoned";
        private const string MatFolder = "Assets/_Project/Art/Materials/Abandoned";
        private const int TexSize = 512;

        public Material PanelExterior { get; private set; }
        public Material WallPaintGreen { get; private set; }
        public Material WallPaintCream { get; private set; }
        public Material Wallpaper { get; private set; }
        public Material BathTile { get; private set; }
        public Material Linoleum { get; private set; }
        public Material Parquet { get; private set; }
        public Material WoodDark { get; private set; }
        public Material Fabric { get; private set; }
        public Material Porcelain { get; private set; }
        public Material MetalPainted { get; private set; }
        public Material BurntMetal { get; private set; }
        public Material Sandbag { get; private set; }
        public Material Foliage { get; private set; }
        public Material Plastic { get; private set; }
        public Material Rubber { get; private set; }
        public Material RoadPaint { get; private set; }
        public Material CarGlass { get; private set; }
        public Material CarPaintBase { get; private set; }

        public Material GrimeDecal { get; private set; }
        public Material StainDecal { get; private set; }
        public Material DarkStainDecal { get; private set; }
        public Material CrackDecal { get; private set; }
        public Material MossDecal { get; private set; }
        public Material PaperDecal { get; private set; }
        public Material Vine { get; private set; }

        public static AbandonedMaterials BuildAll()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Textures");
            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder(TexFolder);
            EnsureFolder(MatFolder);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            var m = new AbandonedMaterials
            {
                PanelExterior = CreateFactoryMat(lit, "PanelExterior", ProceduralTextureFactory.Kind.PanelExterior,
                    new Color(0.32f, 0.33f, 0.34f), new Color(0.45f, 0.46f, 0.47f), 0.12f, 1.4f, 2f, 101),
                WallPaintGreen = CreateFactoryMat(lit, "WallPaintGreen", ProceduralTextureFactory.Kind.PlasterPaint,
                    new Color(0.18f, 0.27f, 0.21f), new Color(0.25f, 0.35f, 0.28f), 0.18f, 1.3f, 2f, 102),
                WallPaintCream = CreateFactoryMat(lit, "WallPaintCream", ProceduralTextureFactory.Kind.PlasterPaint,
                    new Color(0.48f, 0.44f, 0.36f), new Color(0.58f, 0.54f, 0.45f), 0.14f, 1.3f, 2f, 103),
                Wallpaper = CreateFactoryMat(lit, "Wallpaper", ProceduralTextureFactory.Kind.Wallpaper,
                    new Color(0.38f, 0.34f, 0.28f), new Color(0.48f, 0.42f, 0.35f), 0.10f, 1.0f, 3f, 104),
                BathTile = CreateFactoryMat(lit, "BathTile", ProceduralTextureFactory.Kind.Tile,
                    new Color(0.24f, 0.26f, 0.26f), new Color(0.45f, 0.52f, 0.52f), 0.35f, 1.6f, 4f, 105),
                Linoleum = CreateFactoryMat(lit, "Linoleum", ProceduralTextureFactory.Kind.Linoleum,
                    new Color(0.26f, 0.22f, 0.18f), new Color(0.33f, 0.28f, 0.23f), 0.25f, 0.9f, 3f, 106),
                Parquet = CreateFactoryMat(lit, "Parquet", ProceduralTextureFactory.Kind.Parquet,
                    new Color(0.18f, 0.11f, 0.06f), new Color(0.35f, 0.23f, 0.13f), 0.28f, 1.3f, 4f, 107),
                WoodDark = CreateFactoryMat(lit, "WoodDark", ProceduralTextureFactory.Kind.DarkWood,
                    new Color(0.16f, 0.10f, 0.06f), new Color(0.25f, 0.16f, 0.09f), 0.15f, 1.2f, 2f, 108),
                Fabric = CreateFactoryMat(lit, "Fabric", ProceduralTextureFactory.Kind.Fabric,
                    new Color(0.26f, 0.21f, 0.19f), new Color(0.35f, 0.28f, 0.25f), 0.05f, 1.1f, 4f, 109),
                Porcelain = CreateNoiseMat(lit, "Porcelain", new Color(0.68f, 0.69f, 0.67f), new Color(0.78f, 0.78f, 0.75f), 0.65f, 1f, 110),
                MetalPainted = CreateFactoryMat(lit, "MetalPainted", ProceduralTextureFactory.Kind.PaintedMetal,
                    new Color(0.24f, 0.28f, 0.27f), new Color(0.32f, 0.36f, 0.34f), 0.35f, 1.2f, 2f, 111),
                BurntMetal = CreateFactoryMat(lit, "BurntMetal", ProceduralTextureFactory.Kind.RustMetal,
                    new Color(0.10f, 0.09f, 0.09f), new Color(0.18f, 0.15f, 0.14f), 0.20f, 1.4f, 2f, 112),
                Sandbag = CreateFactoryMat(lit, "Sandbag", ProceduralTextureFactory.Kind.Fabric,
                    new Color(0.34f, 0.30f, 0.22f), new Color(0.44f, 0.38f, 0.28f), 0.05f, 1.2f, 3f, 113),
                Foliage = CreateNoiseMat(lit, "Foliage", new Color(0.08f, 0.16f, 0.07f), new Color(0.15f, 0.26f, 0.11f), 0.08f, 3f, 114),
                Plastic = CreateNoiseMat(lit, "Plastic", new Color(0.14f, 0.14f, 0.15f), new Color(0.20f, 0.20f, 0.21f), 0.40f, 1f, 115),
                Rubber = CreateNoiseMat(lit, "Rubber", new Color(0.07f, 0.07f, 0.07f), new Color(0.11f, 0.11f, 0.11f), 0.20f, 2f, 116),
                RoadPaint = CreateNoiseMat(lit, "RoadPaint", new Color(0.65f, 0.63f, 0.55f), new Color(0.75f, 0.72f, 0.62f), 0.10f, 1f, 117),
                CarGlass = CreateGlassMat(lit, "CarGlass"),
                CarPaintBase = CreateNoiseMat(lit, "CarPaintBase", new Color(0.20f, 0.24f, 0.27f), new Color(0.28f, 0.32f, 0.35f), 0.55f, 1f, 118),

                GrimeDecal = CreateDecalMat(lit, "GrimeDecal", new Color(0.07f, 0.06f, 0.05f), 0.55f, 201),
                StainDecal = CreateDecalMat(lit, "StainDecal", new Color(0.14f, 0.12f, 0.08f), 0.45f, 202),
                DarkStainDecal = CreateDecalMat(lit, "DarkStainDecal", new Color(0.06f, 0.05f, 0.04f), 0.62f, 203),
                CrackDecal = CreateDecalMat(lit, "CrackDecal", new Color(0.05f, 0.05f, 0.05f), 0.70f, 204),
                MossDecal = CreateDecalMat(lit, "MossDecal", new Color(0.09f, 0.17f, 0.06f), 0.58f, 205),
                PaperDecal = CreateDecalMat(lit, "PaperDecal", new Color(0.55f, 0.52f, 0.45f), 0.50f, 206),
                Vine = CreateNoiseMat(lit, "Vine", new Color(0.07f, 0.14f, 0.05f), new Color(0.13f, 0.23f, 0.08f), 0.08f, 2f, 207)
            };

            CityPbr.Apply(m.PanelExterior, "plaster_peeling");
            CityPbr.Apply(m.Vine, "ivy_leaves", true);
            if (m.Vine != null)
            {
                Color ivyTint = new Color(0.55f, 0.66f, 0.50f); // PBR-плющ слишком ярко-салатовый в тумане
                if (m.Vine.HasProperty("_BaseColor")) m.Vine.SetColor("_BaseColor", ivyTint);
                if (m.Vine.HasProperty("_Color")) m.Vine.SetColor("_Color", ivyTint);
                EditorUtility.SetDirty(m.Vine);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return m;
        }

        private static Material CreateFactoryMat(
            Shader shader, string name, ProceduralTextureFactory.Kind kind,
            Color dark, Color light, float smoothness, float bumpStrength, float tiling, int seed)
        {
            Texture2D tex = ProceduralTextureFactory.Generate(kind, dark, light, seed, TexSize, out float[,] height);
            Texture2D normal = ProceduralTextureFactory.BuildNormal(height, bumpStrength);
            return SaveMat(shader, name, tex, normal, smoothness, tiling, false);
        }

        private static Material CreateNoiseMat(Shader shader, string name, Color dark, Color light, float smoothness, float tiling, int seed)
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise((x + seed * 19) * 0.08f, (y + seed * 31) * 0.08f);
                    tex.SetPixel(x, y, Color.Lerp(dark, light, n));
                }
            }

            tex.Apply();
            return SaveMat(shader, name, tex, null, smoothness, tiling, false);
        }

        private static Material CreateGlassMat(Shader shader, string name)
        {
            string matPath = $"{MatFolder}/Mat_{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            Color c = new Color(0.16f, 0.22f, 0.26f, 0.16f); // прозрачное стекло: через него хорошо видно дорогу
            SetTransparent(mat);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateDecalMat(Shader shader, string name, Color tint, float maxAlpha, int seed)
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            for (int y = 0; y < size; y++)
            {
                float dy = (y / (float)size - 0.5f) * 2f;
                for (int x = 0; x < size; x++)
                {
                    float dx = (x / (float)size - 0.5f) * 2f;
                    float radial = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    float n = Mathf.PerlinNoise((x + seed * 23) * 0.09f, (y + seed * 41) * 0.09f);
                    float a = radial * n * maxAlpha;
                    tex.SetPixel(x, y, new Color(tint.r, tint.g, tint.b, a));
                }
            }

            tex.Apply();
            return SaveMat(shader, name, tex, null, 0.05f, 1f, true);
        }

        private static Material SaveMat(Shader shader, string name, Texture2D tex, Texture2D normalTex, float smoothness, float tiling, bool transparent)
        {
            string texPath = $"{TexFolder}/Tex_{name}.png";
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
            Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            Texture2D importedNormal = null;
            if (normalTex != null)
            {
                string normalPath = $"{TexFolder}/Tex_{name}_Normal.png";
                File.WriteAllBytes(normalPath, normalTex.EncodeToPNG());
                Object.DestroyImmediate(normalTex);

                AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
                {
                    normalImporter.textureType = TextureImporterType.NormalMap;
                    normalImporter.SaveAndReimport();
                }

                importedNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            }

            string matPath = $"{MatFolder}/Mat_{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            if (transparent) SetTransparent(mat);
            mat.mainTexture = imported;
            mat.mainTextureScale = new Vector2(tiling, tiling);
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", imported);
                mat.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            }
            if (importedNormal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", importedNormal);
                mat.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
                mat.EnableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1.0f);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetTransparent(Material mat)
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
