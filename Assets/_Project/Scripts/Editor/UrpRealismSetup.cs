using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Меню Ogni/Graphics: включает в URP-ассете HDR, MSAA 4x, каскадные мягкие тени (140 м) и добавляет в рендерер
    /// мягкий SSAO (затемнение в щелях и углах — сильно повышает реализм интерьеров). Вызывается и после сборки мира.
    /// </summary>
    public static class UrpRealismSetup
    {
        [MenuItem("Ogni/Graphics/Apply realistic URP settings")]
        public static void Apply()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                Debug.LogWarning("[Ogni] URP asset not found in Graphics settings.");
                return;
            }

            urp.supportsHDR = true;
            urp.msaaSampleCount = 4;
            urp.shadowDistance = 140f;
            urp.shadowCascadeCount = 4;
            // supportsSoftShadows только для чтения: включаем через сериализованное поле.
            var urpSo = new SerializedObject(urp);
            SerializedProperty soft = urpSo.FindProperty("m_SoftShadowsSupported");
            if (soft != null) soft.boolValue = true;
            urpSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(urp);

            try { AddOrTuneSsao(urp); }
            catch (System.Exception e) { Debug.LogWarning("[Ogni] SSAO was not added automatically (add it to the Renderer manually): " + e.Message); }

            AssetDatabase.SaveAssets();
            Debug.Log("[Ogni] URP: HDR, MSAA 4x, soft cascaded shadows (140m) applied; SSAO tuned.");
        }

        private static void AddOrTuneSsao(UniversalRenderPipelineAsset urp)
        {
            var so = new SerializedObject(urp);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            if (list == null || list.arraySize == 0) return;
            var rendererData = list.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData;
            if (rendererData == null) return;

            ScreenSpaceAmbientOcclusion feature = null;
            foreach (ScriptableRendererFeature f in rendererData.rendererFeatures)
            {
                if (f is ScreenSpaceAmbientOcclusion existing)
                {
                    feature = existing;
                    break;
                }
            }

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                feature.name = "SSAO";
                AssetDatabase.AddObjectToAsset(feature, rendererData);

                var rso = new SerializedObject(rendererData);
                SerializedProperty features = rso.FindProperty("m_RendererFeatures");
                SerializedProperty map = rso.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                rso.ApplyModifiedProperties();
                EditorUtility.SetDirty(rendererData);
            }

            // Мягкая настройка SSAO: подчёркивает углы мебели и стен без грязных пятен.
            var fso = new SerializedObject(feature);
            SerializedProperty settings = fso.FindProperty("m_Settings");
            if (settings != null)
            {
                SerializedProperty intensity = settings.FindPropertyRelative("Intensity");
                if (intensity != null) intensity.floatValue = 0.55f;
                SerializedProperty radius = settings.FindPropertyRelative("Radius");
                if (radius != null) radius.floatValue = 0.35f;
                SerializedProperty direct = settings.FindPropertyRelative("DirectLightingStrength");
                if (direct != null) direct.floatValue = 0.18f;
                fso.ApplyModifiedProperties();
                EditorUtility.SetDirty(feature);
            }
        }
    }
}
