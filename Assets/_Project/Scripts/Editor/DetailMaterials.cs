using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Материалы для детализированных моделей (оружие/инструменты от первого лица, Слушатель):
    /// текстурные PBR-материалы на основе <see cref="ProceduralTextureFactory"/> (альбедо + нормали) и
    /// простые металл/резина/кожа. Все ассеты сохраняются в Assets/_Project/Art/Detail и переиспользуются.
    /// </summary>
    public static class DetailMaterials
    {
        private const string Folder = "Assets/_Project/Art/Detail";

        private static Shader Lit => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        public static Material Solid(string name, Color color, float metallic, float smoothness)
        {
            EnsureFolder();
            string path = $"{Folder}/Mat_{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Lit);
                AssetDatabase.CreateAsset(m, path);
            }
            else
            {
                m.shader = Lit;
            }

            Apply(m, color, metallic, smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Textured(string name, ProceduralTextureFactory.Kind kind, Color dark, Color light,
            Color tint, float metallic, float smoothness, float tiling, float normalStrength, int seed)
        {
            EnsureFolder();
            string texPath = $"{Folder}/Tex_{name}.png";
            string nrmPath = $"{Folder}/Tex_{name}_N.png";
            string matPath = $"{Folder}/Mat_{name}.mat";

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(nrmPath);
            if (albedo == null || normal == null)
            {
                Texture2D generated = ProceduralTextureFactory.Generate(kind, dark, light, seed, 256, out float[,] height);
                File.WriteAllBytes(texPath, generated.EncodeToPNG());
                Object.DestroyImmediate(generated);

                Texture2D n = ProceduralTextureFactory.BuildNormal(height, normalStrength);
                File.WriteAllBytes(nrmPath, n.EncodeToPNG());
                Object.DestroyImmediate(n);

                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(nrmPath, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(nrmPath) is TextureImporter imp && imp.textureType != TextureImporterType.NormalMap)
                {
                    imp.textureType = TextureImporterType.NormalMap;
                    imp.SaveAndReimport();
                }

                albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(nrmPath);
            }

            Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null)
            {
                m = new Material(Lit);
                AssetDatabase.CreateAsset(m, matPath);
            }

            Apply(m, tint, metallic, smoothness);
            if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", albedo); m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling)); }
            m.mainTexture = albedo;
            m.mainTextureScale = new Vector2(tiling, tiling);
            if (normal != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
                m.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
                m.EnableKeyword("_NORMALMAP");
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1f);
            }

            EditorUtility.SetDirty(m);
            return m;
        }

        private static void Apply(Material m, Color color, float metallic, float smoothness)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        }

        public static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project")) AssetDatabase.CreateFolder("Assets", "_Project");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art")) AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Art", "Detail");
        }
    }
}
