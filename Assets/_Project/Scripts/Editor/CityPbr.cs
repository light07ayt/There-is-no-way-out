using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Шаг 2: автоимпорт PBR-наборов из Assets/Textures/City/ и сборка URP/Lit материалов.
    /// Тип карты определяется по токену в имени файла (albedo/basecolor/color/diffuse, normal/nrm,
    /// roughness, height/displacement, ao/occlusion, metallic, opacity/alpha).
    /// Roughness конвертируется в smoothness (1 - roughness) и пакуется в альфу Metallic-карты URP.
    /// </summary>
    public class CityTextureImporter : AssetPostprocessor
    {
        public const string SourceFolder = "Assets/Textures/City";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SourceFolder)) return;
            var ti = (TextureImporter)assetImporter;
            CityPbr.Kind kind = CityPbr.Classify(Path.GetFileNameWithoutExtension(assetPath));

            ti.maxTextureSize = 1024;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 8;
            // Kaiser-фильтр мипов гасит «рябь» на мелком рисунке (кирпич, штукатурка) на расстоянии.
            ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            ti.streamingMipmaps = false;   // подгрузка мипов давала «пропадающие» текстуры дома

            switch (kind)
            {
                case CityPbr.Kind.Normal:
                    ti.textureType = TextureImporterType.NormalMap;
                    break;
                case CityPbr.Kind.Roughness:
                case CityPbr.Kind.Height:
                case CityPbr.Kind.AO:
                case CityPbr.Kind.Metallic:
                case CityPbr.Kind.Opacity:
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = false;
                    break;
                case CityPbr.Kind.Albedo:
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = true;
                    ti.alphaSource = TextureImporterAlphaSource.FromInput;
                    bool ivy = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant().Contains("ivy");
                    ti.alphaIsTransparency = ivy;
                    // Альфа-клип плюща на мипах «съедается» - листва пропадает вдали; сохраняем покрытие.
                    ti.mipMapsPreserveCoverage = ivy;
                    if (ivy) ti.alphaTestReferenceValue = 0.5f;
                    break;
            }
        }
    }

    public static class CityPbr
    {
        public enum Kind { Unknown, Albedo, Normal, Roughness, Height, AO, Metallic, Opacity }

        private const string OutFolder = "Assets/_Project/Art/Textures/City";

        public static Kind Classify(string fileName)
        {
            string n = fileName.ToLowerInvariant();
            if (n.Contains("normal") || n.EndsWith("_nrm") || n.EndsWith("_nor") || n.EndsWith("_n")) return Kind.Normal;
            if (n.Contains("rough")) return Kind.Roughness;
            if (n.Contains("height") || n.Contains("disp") || n.Contains("bump")) return Kind.Height;
            if (n.EndsWith("_ao") || n.Contains("occlusion") || n.Contains("ambient")) return Kind.AO;
            if (n.Contains("metal")) return Kind.Metallic;
            if (n.Contains("opacity") || n.EndsWith("_alpha") || n.EndsWith("_mask")) return Kind.Opacity;
            if (n.Contains("albedo") || n.Contains("basecolor") || n.Contains("base_color") || n.Contains("diffuse")
                || n.EndsWith("_color") || n.EndsWith("_col") || n.EndsWith("_diff")) return Kind.Albedo;
            return Kind.Unknown;
        }

        [MenuItem("Ogni/Reimport City Textures")]
        public static void ReimportAll()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CityTextureImporter.SourceFolder }))
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
        }

        private static string Find(string set, Kind kind)
        {
            if (!AssetDatabase.IsValidFolder(CityTextureImporter.SourceFolder)) return null;
            foreach (string guid in AssetDatabase.FindAssets(set + " t:Texture2D", new[] { CityTextureImporter.SourceFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (!file.ToLowerInvariant().StartsWith(set)) continue;
                Kind k = Classify(file);
                if (k == kind) return path;
                if (kind == Kind.Albedo && k == Kind.Unknown && file.ToLowerInvariant() == set) return path;
            }

            return null;
        }

        private static Texture2D Load(string path) => path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        private static Color32[] ReadPixels(string assetPath, out int w, out int h)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            tex.LoadImage(File.ReadAllBytes(assetPath));
            w = tex.width;
            h = tex.height;
            Color32[] px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return px;
        }

        /// <summary>URP: Metallic-карта, где A = smoothness = 1 - roughness. Для луж усиливаем глянец.</summary>
        private static Texture2D BuildMetalSmooth(string set, string roughPath, string heightPath, bool puddles, float smoothBoost)
        {
            if (roughPath == null) return null;
            Color32[] rough = ReadPixels(roughPath, out int w, out int h);
            Color32[] height = null;
            if (puddles && heightPath != null)
            {
                height = ReadPixels(heightPath, out int hw, out int hh);
                if (hw != w || hh != h) height = null;
            }

            var outPx = new Color32[rough.Length];
            for (int i = 0; i < rough.Length; i++)
            {
                float s = 1f - rough[i].r / 255f;
                s = Mathf.Clamp01(s * smoothBoost);
                if (height != null)
                {
                    // Низкие участки карты высот = лужи: почти зеркальный глянец.
                    float low = Mathf.Clamp01((0.38f - height[i].r / 255f) / 0.08f);
                    s = Mathf.Lerp(s, 0.96f, low);
                }

                outPx[i] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(s * 255f));
            }

            return SavePng(set + "_metalsmooth", outPx, w, h, false);
        }

        private static Texture2D SavePng(string name, Color32[] px, int w, int h, bool srgb)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Textures")) AssetDatabase.CreateFolder("Assets/_Project/Art", "Textures");
            if (!AssetDatabase.IsValidFolder(OutFolder)) AssetDatabase.CreateFolder("Assets/_Project/Art/Textures", "City");
            string path = OutFolder + "/" + name + ".png";
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
            t.SetPixels32(px);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.sRGBTexture = srgb;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.mipmapEnabled = true;
            ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 8;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Возвращает true, если набор найден и материал обновлён (тайлинг материала сохраняется).</summary>
        public static bool Apply(Material mat, string set, bool alphaClip = false, bool puddles = false, float smoothBoost = 1f, bool parallax = false)
        {
            if (mat == null) return false;
            string albedoP = Find(set, Kind.Albedo);
            if (albedoP == null)
            {
                Debug.LogWarning("[Ogni] PBR-набор '" + set + "' не найден в " + CityTextureImporter.SourceFolder + " — оставлена процедурная текстура.");
                return false;
            }

            Texture2D albedo = Load(albedoP);
            string normalP = Find(set, Kind.Normal);
            string roughP = Find(set, Kind.Roughness);
            string heightP = Find(set, Kind.Height);
            string aoP = Find(set, Kind.AO);
            string metalP = Find(set, Kind.Metallic);
            Vector2 tiling = mat.HasProperty("_BaseMap") ? mat.GetTextureScale("_BaseMap") : mat.mainTextureScale;

            if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", albedo); mat.SetTextureScale("_BaseMap", tiling); }
            mat.mainTexture = albedo;
            mat.mainTextureScale = tiling;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

            Texture2D normal = Load(normalP);
            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }

            Texture2D ms = BuildMetalSmooth(set, roughP, heightP, puddles, smoothBoost);
            if (ms != null && mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.SetTextureScale("_MetallicGlossMap", tiling);
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Smoothness", 1f);
                if (mat.HasProperty("_SmoothnessTextureChannel")) mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else if (metalP != null && mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", Load(metalP));
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            Texture2D ao = Load(aoP);
            if (ao != null && mat.HasProperty("_OcclusionMap"))
            {
                mat.SetTexture("_OcclusionMap", ao);
                mat.SetFloat("_OcclusionStrength", 0.8f);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }

            Texture2D height = Load(heightP);
            if (parallax && height != null && mat.HasProperty("_ParallaxMap"))
            {
                mat.SetTexture("_ParallaxMap", height);
                mat.SetFloat("_Parallax", 0.02f);
                mat.EnableKeyword("_PARALLAXMAP");
            }

            if (alphaClip)
            {
                if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
                if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // двусторонние листья
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }

            EditorUtility.SetDirty(mat);
            return true;
        }
    }
}
