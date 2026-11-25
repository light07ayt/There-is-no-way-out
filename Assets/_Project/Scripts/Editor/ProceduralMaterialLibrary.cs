using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Генерирует и сохраняет в Assets/_Project/Art бесшовные процедурные текстуры 512x512 (.png)
    /// через <see cref="ProceduralTextureFactory"/>, карты нормалей (_BumpMap) и PBR-материалы (.mat).
    /// Если в Assets/Textures/City лежит готовый PBR-набор, <see cref="CityPbr"/> заменяет им процедурную текстуру.
    /// </summary>
    public sealed class ProceduralMaterialLibrary
    {
        private const string TexturesFolder = "Assets/_Project/Art/Textures";
        private const string MaterialsFolder = "Assets/_Project/Art/Materials";
        private const int TexSize = 512;

        public Material ForestGround { get; private set; }
        public Material DirtRoad { get; private set; }
        public Material Asphalt { get; private set; }
        public Material TreeBark { get; private set; }
        public Material PineNeedles { get; private set; }
        public Material WoodPlanks { get; private set; }
        public Material Concrete { get; private set; }
        public Material Brick { get; private set; }
        public Material RustyMetal { get; private set; }
        public Material Stone { get; private set; }
        public Material DarkGlass { get; private set; }
        public Material TowerGlass { get; private set; }
        public Material TowerFrame { get; private set; }
        public Material StainedRed { get; private set; }
        public Material StainedBlue { get; private set; }
        public Material StainedAmber { get; private set; }
        public Material BrassAged { get; private set; }
        public Material CandleWax { get; private set; }
        public Material MossGround { get; private set; }
        public Material CharredWood { get; private set; }
        public Material Ash { get; private set; }
        public Material Ember { get; private set; }
        public Material Flame { get; private set; }
        public Material FuelCanRed { get; private set; }
        public Material MedicalWhite { get; private set; }
        public Material BatteryPlastic { get; private set; }
        public Material AmmoMetal { get; private set; }
        public Material ClothRoll { get; private set; }
        public Material PlankCut { get; private set; }
        public Material BushFoliage { get; private set; }
        public Material SmokeParticle { get; private set; }
        public Material EmberParticle { get; private set; }

        public Texture2D ForestGroundTex { get; private set; }
        public Texture2D ForestGroundNormal { get; private set; }
        public Texture2D DirtRoadTex { get; private set; }
        public Texture2D DirtRoadNormal { get; private set; }
        public Texture2D AsphaltTex { get; private set; }
        public Texture2D AsphaltNormal { get; private set; }

        public static ProceduralMaterialLibrary BuildAll()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder(TexturesFolder);
            EnsureFolder(MaterialsFolder);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var lib = new ProceduralMaterialLibrary();

            lib.ForestGround = CreateFactoryMaterial(shader, "ForestGround", ProceduralTextureFactory.Kind.ForestGround,
                new Color(0.09f, 0.14f, 0.07f), new Color(0.19f, 0.25f, 0.11f),
                0.08f, 1.1f, new Vector2(24f, 24f), 11, out Texture2D fgT, out Texture2D fgN);
            lib.ForestGroundTex = fgT;
            lib.ForestGroundNormal = fgN;

            lib.DirtRoad = CreateFactoryMaterial(shader, "DirtRoad", ProceduralTextureFactory.Kind.DirtRoad,
                new Color(0.19f, 0.14f, 0.09f), new Color(0.31f, 0.24f, 0.16f),
                0.08f, 1.2f, new Vector2(6f, 18f), 22, out Texture2D drT, out Texture2D drN);
            lib.DirtRoadTex = drT;
            lib.DirtRoadNormal = drN;

            lib.Asphalt = CreateFactoryMaterial(shader, "Asphalt", ProceduralTextureFactory.Kind.Asphalt,
                new Color(0.08f, 0.08f, 0.09f), new Color(0.16f, 0.16f, 0.18f),
                0.22f, 1.3f, new Vector2(8f, 20f), 33, out Texture2D asT, out Texture2D asN);
            lib.AsphaltTex = asT;
            lib.AsphaltNormal = asN;

            lib.TreeBark = CreateFactoryMaterial(shader, "TreeBark", ProceduralTextureFactory.Kind.Bark,
                new Color(0.12f, 0.08f, 0.05f), new Color(0.26f, 0.18f, 0.11f),
                0.08f, 1.8f, new Vector2(2f, 4f), 41, out _, out _);
            lib.PineNeedles = CreateNoiseMaterial(shader, "PineNeedles",
                new Color(0.05f, 0.13f, 0.06f), new Color(0.11f, 0.22f, 0.09f),
                0.05f, 1.4f, new Vector2(3f, 3f), 44, out _, out _);
            if (lib.PineNeedles != null && lib.PineNeedles.HasProperty("_Cull")) { lib.PineNeedles.SetFloat("_Cull", 0f); EditorUtility.SetDirty(lib.PineNeedles); }
            lib.WoodPlanks = CreateFactoryMaterial(shader, "WoodPlanks", ProceduralTextureFactory.Kind.Planks,
                new Color(0.25f, 0.16f, 0.09f), new Color(0.44f, 0.29f, 0.16f),
                0.14f, 1.6f, new Vector2(2f, 2f), 48, out _, out _);
            lib.Concrete = CreateFactoryMaterial(shader, "Concrete", ProceduralTextureFactory.Kind.Concrete,
                new Color(0.30f, 0.31f, 0.32f), new Color(0.45f, 0.46f, 0.47f),
                0.14f, 1.0f, new Vector2(3f, 3f), 55, out _, out _);
            lib.Brick = CreateFactoryMaterial(shader, "Brick", ProceduralTextureFactory.Kind.Brick,
                new Color(0.34f, 0.15f, 0.12f), new Color(0.47f, 0.22f, 0.16f),
                0.12f, 1.7f, new Vector2(3f, 2f), 61, out _, out _);
            lib.RustyMetal = CreateFactoryMaterial(shader, "RustyMetal", ProceduralTextureFactory.Kind.RustMetal,
                new Color(0.18f, 0.14f, 0.12f), new Color(0.31f, 0.25f, 0.21f),
                0.42f, 1.1f, new Vector2(2f, 2f), 66, out _, out _);
            lib.Stone = CreateNoiseMaterial(shader, "Stone",
                new Color(0.20f, 0.21f, 0.23f), new Color(0.35f, 0.36f, 0.38f),
                0.18f, 1.5f, new Vector2(2f, 2f), 77, out _, out _);
            lib.DarkGlass = CreateSolidMaterial(shader, "DarkGlass", new Color(0.07f, 0.10f, 0.13f), 0.85f);
            lib.TowerGlass = CreateTransparentGlass(shader, "TowerGlass", new Color(0.34f, 0.50f, 0.56f, 0.30f), 0.96f);
            lib.TowerFrame = CreateSolidMaterial(shader, "TowerFrame", new Color(0.10f, 0.115f, 0.13f), 0.55f);
            if (lib.TowerFrame.HasProperty("_Metallic")) lib.TowerFrame.SetFloat("_Metallic", 0.7f);
            // Витражи часовни: тусклое цветное свечение, как стекло в сумерках.
            lib.StainedRed = CreateEmissiveMaterial(shader, "StainedRed", new Color(0.55f, 0.06f, 0.05f), 0.9f);
            lib.StainedBlue = CreateEmissiveMaterial(shader, "StainedBlue", new Color(0.08f, 0.17f, 0.55f), 0.9f);
            lib.StainedAmber = CreateEmissiveMaterial(shader, "StainedAmber", new Color(0.70f, 0.42f, 0.07f), 0.9f);
            lib.BrassAged = CreateSolidMaterial(shader, "BrassAged", new Color(0.36f, 0.27f, 0.10f), 0.55f);
            if (lib.BrassAged.HasProperty("_Metallic")) lib.BrassAged.SetFloat("_Metallic", 0.8f);
            lib.CandleWax = CreateSolidMaterial(shader, "CandleWax", new Color(0.78f, 0.74f, 0.62f), 0.2f);
            lib.MossGround = CreateFactoryMaterial(shader, "MossGround", ProceduralTextureFactory.Kind.ForestGround,
                new Color(0.10f, 0.16f, 0.07f), new Color(0.22f, 0.31f, 0.13f),
                0.06f, 1.2f, new Vector2(8f, 8f), 88, out _, out _);
            lib.CharredWood = CreateFactoryMaterial(shader, "CharredWood", ProceduralTextureFactory.Kind.Bark,
                new Color(0.05f, 0.04f, 0.03f), new Color(0.15f, 0.11f, 0.08f),
                0.04f, 1.8f, new Vector2(2f, 3f), 99, out _, out _);
            lib.Ash = CreateNoiseMaterial(shader, "Ash",
                new Color(0.24f, 0.24f, 0.25f), new Color(0.42f, 0.42f, 0.43f),
                0.02f, 0.8f, new Vector2(4f, 4f), 111, out _, out _);
            lib.BushFoliage = CreateNoiseMaterial(shader, "BushFoliage",
                new Color(0.06f, 0.14f, 0.05f), new Color(0.16f, 0.28f, 0.10f),
                0.08f, 1.4f, new Vector2(3f, 3f), 122, out _, out _);
            lib.Ember = CreateEmissiveMaterial(shader, "Ember", new Color(1f, 0.28f, 0.04f), 3.5f);
            lib.Flame = CreateEmissiveMaterial(shader, "Flame", new Color(1f, 0.55f, 0.12f), 5.5f);
            lib.FuelCanRed = CreateSolidMaterial(shader, "FuelCanRed", new Color(0.48f, 0.07f, 0.05f), 0.45f);
            lib.MedicalWhite = CreateSolidMaterial(shader, "MedicalWhite", new Color(0.82f, 0.82f, 0.78f), 0.35f);
            lib.BatteryPlastic = CreateSolidMaterial(shader, "BatteryPlastic", new Color(0.05f, 0.12f, 0.07f), 0.55f);
            lib.AmmoMetal = CreateSolidMaterial(shader, "AmmoMetal", new Color(0.35f, 0.30f, 0.18f), 0.65f);
            lib.ClothRoll = CreateSolidMaterial(shader, "ClothRoll", new Color(0.44f, 0.38f, 0.29f), 0.05f);
            lib.PlankCut = CreateFactoryMaterial(shader, "PlankCut", ProceduralTextureFactory.Kind.Planks,
                new Color(0.28f, 0.18f, 0.09f), new Color(0.50f, 0.34f, 0.18f),
                0.12f, 1.4f, new Vector2(1.5f, 1.5f), 133, out _, out _);
            lib.SmokeParticle = CreateParticleMaterial("SmokeParticle", new Color(0.28f, 0.27f, 0.26f, 0.35f));
            lib.EmberParticle = CreateParticleMaterial("EmberParticle", new Color(1f, 0.45f, 0.08f, 1f));

            // PBR-наборы из Assets/Textures/City (если найдены, заменяют процедурные текстуры).
            CityPbr.Apply(lib.Asphalt, "asphalt_wet", false, true, 1.25f, true);
            CityPbr.Apply(lib.Concrete, "concrete_mossy");
            CityPbr.Apply(lib.Brick, "brick_overgrown");
            CityPbr.Apply(lib.RustyMetal, "rust_metal");
            CityPbr.Apply(lib.MossGround, "grass_overgrowth");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return lib;
        }

        private static Material CreateFactoryMaterial(
            Shader shader, string name, ProceduralTextureFactory.Kind kind,
            Color dark, Color light, float smoothness, float bumpStrength, Vector2 tiling, int seed,
            out Texture2D outTex, out Texture2D outNormal)
        {
            Texture2D tex = ProceduralTextureFactory.Generate(kind, dark, light, seed, TexSize, out float[,] height);
            return SaveTextureAndMaterial(shader, name, tex, height, bumpStrength, smoothness, tiling, out outTex, out outNormal);
        }

        private static Material CreateSolidMaterial(Shader shader, string name, Color color, float smoothness)
        {
            string matPath = $"{MaterialsFolder}/Mat_{name}.mat";
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

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Прозрачное стекло витража для URP/Lit: альфа-смешивание, высокий глянец (отражает небо и туман),
        /// без записи в глубину. Для Standard-шейдера включается режим Fade.
        /// </summary>
        private static Material CreateTransparentGlass(Shader shader, string name, Color color, float smoothness)
        {
            string matPath = $"{MaterialsFolder}/Mat_{name}.mat";
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

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.15f);
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateEmissiveMaterial(Shader shader, string name, Color color, float intensity)
        {
            Material mat = CreateSolidMaterial(shader, name, color, 0.15f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * intensity);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateParticleMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            string matPath = $"{MaterialsFolder}/Mat_{name}.mat";
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

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateNoiseMaterial(
            Shader shader, string name, Color dark, Color light, float smoothness, float bumpStrength, Vector2 tiling, int seed,
            out Texture2D outTex, out Texture2D outNormal)
        {
            const int size = 256;
            float[,] height = new float[size, size];
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n1 = Mathf.PerlinNoise((x + seed * 17) * 0.045f, (y + seed * 31) * 0.045f);
                    float n2 = Mathf.PerlinNoise((x + seed * 53) * 0.14f, (y + seed * 19) * 0.14f) * 0.4f;
                    float n3 = Mathf.PerlinNoise((x + seed * 71) * 0.35f, (y + seed * 43) * 0.35f) * 0.18f;
                    float t = Mathf.Clamp01(n1 * 0.58f + n2 + n3);
                    height[x, y] = t;
                    tex.SetPixel(x, y, Color.Lerp(dark, light, t));
                }
            }

            tex.Apply();
            return SaveTextureAndMaterial(shader, name, tex, height, bumpStrength, smoothness, tiling, out outTex, out outNormal);
        }

        private static Material SaveTextureAndMaterial(
            Shader shader, string name, Texture2D tex, float[,] height, float bumpStrength,
            float smoothness, Vector2 tiling, out Texture2D importedAlbedo, out Texture2D importedNormal)
        {
            string texPath = $"{TexturesFolder}/Tex_{name}.png";
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            string normalPath = $"{TexturesFolder}/Tex_{name}_Normal.png";
            Texture2D normalTex = ProceduralTextureFactory.BuildNormal(height, bumpStrength);
            File.WriteAllBytes(normalPath, normalTex.EncodeToPNG());
            Object.DestroyImmediate(normalTex);

            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            importedAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            importedNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            string matPath = $"{MaterialsFolder}/Mat_{name}.mat";
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

            mat.mainTexture = importedAlbedo;
            mat.mainTextureScale = tiling;
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", importedAlbedo);
                mat.SetTextureScale("_BaseMap", tiling);
            }

            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", importedNormal);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1.0f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
