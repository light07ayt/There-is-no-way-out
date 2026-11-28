using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Автоматическая настройка всех 15 3D PBR-моделей из Poly Haven (Assets/_Project/Art/PolyHaven/*):
    /// - Импортирует текстуры (Albedo, NormalMap, MetallicSmoothness, AO) в 2K.
    /// - Собирает URP/Lit материалы и сохраняет готовые префабы в Assets/_Project/Prefabs/PolyHaven/.
    /// - Подменяет визуал Pickup_Fuel на реальную 3D-канистру (metal_jerrycan).
    /// - Подменяет топор, молоток, биту, пистолет и винтовку в руках игрока (Player.prefab).
    /// - Расставляет реальную 3D-мебель, книги, фонарик, мачете, прожектор и фонари в хижине и вдоль дороги.
    /// </summary>
    public static class PolyHavenPropsSetup
    {
        private const string PolyHavenRoot = "Assets/_Project/Art/PolyHaven";
        private const string PrefabsOut = "Assets/_Project/Prefabs/PolyHaven";

        private static readonly string[] ModelNames =
        {
            "cross_pein_hammer",
            "baseball_bat",
            "metal_jerrycan",
            "painted_wooden_cabinet",
            "worn_metal_rack",
            "long_life_food",
            "large_iron_gate",
            "wooden_axe_03",
            "service_pistol",
            "bolt_action_rifle_7_62",
            "machete",
            "small_plastic_torch",
            "street_lamp_02",
            "security_light",
            "book_encyclopedia_set_01",
            "concrete_road_barrier",
            "utility_box_02",
            "exterior_aircon_unit",
            "wooden_bookshelf_worn",
            "metal_office_desk",
            "painted_wooden_chair_02",
            "old_bed_frame",
            "metal_trash_can",
            "wooden_picnic_table",
            "shelf_01",
            "rollershutter_window_01",
            "rollershutter_window_02",
            "electric_stove",
            "large_castle_door",
            "cigarette_case",
            "portable_cassette_player",
            "security_camera_01"
        };

        [MenuItem("Ogni/Setup PolyHaven 3D Props")]
        public static void SetupAll()
        {
            if (!AssetDatabase.IsValidFolder(PolyHavenRoot))
            {
                Debug.LogWarning("[Ogni] Папка " + PolyHavenRoot + " не найдена.");
                return;
            }

            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder(PrefabsOut);

            int built = 0;
            foreach (string name in ModelNames)
            {
                if (BuildModelPrefab(name) != null) built++;
            }

            UpgradeFuelPickupPrefab();
            UpgradePlayerViewModels();
            PopulateOpenSceneCabinAndRoad();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Ogni] Настроено 3D PBR-моделей Poly Haven: {built}. Префабы сохранены в {PrefabsOut}.");
        }

        private static GameObject BuildModelPrefab(string modelName)
        {
            string dir = $"{PolyHavenRoot}/{modelName}";
            string objPath = $"{dir}/{modelName}.obj";
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
            if (modelAsset == null) return null;

            string texDir = $"{dir}/textures";
            Texture2D diff = ConfigureTex($"{texDir}/{modelName}_diff.jpg", isNormal: false, srgb: true);
            Texture2D normal = ConfigureTex($"{texDir}/{modelName}_normal.jpg", isNormal: true, srgb: false);
            Texture2D ms = ConfigureTex($"{texDir}/{modelName}_metalsmooth.png", isNormal: false, srgb: false);
            Texture2D ao = ConfigureTex($"{texDir}/{modelName}_ao.jpg", isNormal: false, srgb: false);

            string matPath = $"{dir}/Mat_{modelName}.mat";
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = lit;
            }

            if (diff != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", diff);
                mat.mainTexture = diff;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            }
            else
            {
                // Если в архиве Poly Haven не было отдельных текстур (например, wooden_picnic_table),
                // подхватываем готовый 2K PBR-материал дерева, чтобы модель не была белой.
                Material fallbackWood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Mat_WoodPlanks.mat");
                if (fallbackWood != null) mat = fallbackWood;
            }
            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (ms != null && mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.SetFloat("_Metallic", 1.0f);
                mat.SetFloat("_Smoothness", 1.0f);
                if (mat.HasProperty("_SmoothnessTextureChannel")) mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (ao != null && mat.HasProperty("_OcclusionMap"))
            {
                mat.SetTexture("_OcclusionMap", ao);
                mat.SetFloat("_OcclusionStrength", 0.85f);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }
            EditorUtility.SetDirty(mat);

            string prefabPath = $"{PrefabsOut}/PH_{modelName}.prefab";
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            instance.name = "PH_" + modelName;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            return saved;
        }

        private static Texture2D ConfigureTex(string path, bool isNormal, bool srgb)
        {
            if (!File.Exists(path)) return null;
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                bool dirty = false;
                if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; dirty = true; }
                TextureImporterType targetType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (ti.textureType != targetType) { ti.textureType = targetType; dirty = true; }
                if (!isNormal && ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; dirty = true; }
                if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; dirty = true; }
                if (ti.anisoLevel < 8) { ti.anisoLevel = 8; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void UpgradeFuelPickupPrefab()
        {
            const string fuelPath = "Assets/_Project/Prefabs/Pickup_Fuel.prefab";
            GameObject jerrycan = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsOut}/PH_metal_jerrycan.prefab");
            if (jerrycan == null || !File.Exists(fuelPath)) return;

            GameObject root = PrefabUtility.LoadPrefabContents(fuelPath);
            if (root == null) return;

            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Transform ch = root.transform.GetChild(i);
                    if (ch.name == "PH_JerrycanVisual")
                    {
                        Object.DestroyImmediate(ch.gameObject);
                    }
                    else
                    {
                        foreach (Renderer r in ch.GetComponentsInChildren<Renderer>(true))
                            r.enabled = false;
                    }
                }
                foreach (Renderer r in root.GetComponents<Renderer>()) r.enabled = false;

                GameObject vis = (GameObject)PrefabUtility.InstantiatePrefab(jerrycan, root.transform);
                vis.name = "PH_JerrycanVisual";
                vis.transform.localPosition = Vector3.zero;
                vis.transform.localRotation = Quaternion.identity;
                vis.transform.localScale = Vector3.one;

                PrefabUtility.SaveAsPrefabAsset(root, fuelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void UpgradePlayerViewModels()
        {
            const string playerPath = "Assets/_Project/Prefabs/Player.prefab";
            if (!File.Exists(playerPath)) return;

            GameObject axePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsOut}/PH_wooden_axe_03.prefab");
            GameObject hammerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsOut}/PH_cross_pein_hammer.prefab");
            GameObject batPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsOut}/PH_baseball_bat.prefab");
            GameObject pistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsOut}/PH_service_pistol.prefab");
            GameObject riflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsOut}/PH_bolt_action_rifle_7_62.prefab");

            GameObject root = PrefabUtility.LoadPrefabContents(playerPath);
            if (root == null) return;

            try
            {
                // Пересобираем гладкие бесшовные кулаки (UnarmedModel), чтобы убрать старые «шариковые» пальцы.
                Transform camTr = FindDeepChild(root.transform, "PlayerCamera");
                if (camTr == null)
                {
                    Camera c = root.GetComponentInChildren<Camera>(true);
                    if (c != null) camTr = c.transform;
                }
                if (camTr != null)
                {
                    Transform oldUnarmed = camTr.Find("UnarmedModel");
                    if (oldUnarmed != null)
                    {
                        ViewModelBuilder.Mats vmMats = ViewModelBuilder.CreateMats();
                        GameObject freshUnarmed = ViewModelBuilder.BuildUnarmed(camTr, vmMats);
                        freshUnarmed.SetActive(oldUnarmed.gameObject.activeSelf);
                        // Перепривязываем ссылку unarmedModel в PlayerEquipment
                        var eq = root.GetComponent<Ogni.Player.PlayerEquipment>();
                        if (eq != null)
                        {
                            var so = new SerializedObject(eq);
                            var prop = so.FindProperty("unarmedModel");
                            if (prop != null) { prop.objectReferenceValue = freshUnarmed; so.ApplyModifiedPropertiesWithoutUndo(); }
                        }
                        Object.DestroyImmediate(oldUnarmed.gameObject);
                    }

                    // Выносим фонарик вперёд перед руками и луком, чтобы не было белого засвета.
                    foreach (Light l in camTr.GetComponentsInChildren<Light>(true))
                    {
                        if (l.type == LightType.Spot)
                            l.transform.localPosition = new Vector3(0.06f, -0.06f, 0.58f);
                    }

                    // Поправляем позу лука: сдвигаем влево-вниз и слегка наклоняем, чтобы не перекрывал центр и не светился.
                    Transform bowTr = camTr.Find("BowModel");
                    if (bowTr != null)
                    {
                        bowTr.localPosition = new Vector3(-0.22f, -0.14f, 0.46f);
                        bowTr.localRotation = Quaternion.Euler(4f, 8f, -14f);
                    }
                }

                if (axePrefab != null)
                {
                    Transform am = FindDeepChild(root.transform, "AxeModel");
                    // Лезвие в wooden_axe_03.obj смотрит строго по +Z (rotation = 0,0,0), удобный размер 1.25x.
                    ReplaceToolVisualInViewModel(am, axePrefab, "PH_AxeVisual",
                        new Vector3(0f, -0.14f, 0.04f), new Vector3(0f, 0f, 0f), Vector3.one * 1.25f);
                }

                if (hammerPrefab != null)
                {
                    Transform hm = FindDeepChild(root.transform, "HammerModel");
                    ReplaceToolVisualInViewModel(hm, hammerPrefab, "PH_HammerVisual",
                        new Vector3(0f, -0.02f, 0.02f), new Vector3(0f, 90f, 0f), Vector3.one * 1.15f);
                }

                if (batPrefab != null)
                {
                    Transform cm = FindDeepChild(root.transform, "ClubModel");
                    ReplaceToolVisualInViewModel(cm, batPrefab, "PH_BatVisual",
                        new Vector3(0f, -0.18f, 0.02f), new Vector3(0f, 0f, 0f), Vector3.one * 0.72f);
                }

                if (pistolPrefab != null)
                {
                    Transform pm = FindDeepChild(root.transform, "PistolModel");
                    ReplaceToolVisualInViewModel(pm, pistolPrefab, "PH_PistolVisual",
                        new Vector3(0f, -0.04f, 0.06f), new Vector3(0f, -90f, 0f), Vector3.one * 0.85f);
                }

                if (riflePrefab != null)
                {
                    Transform sm = FindDeepChild(root.transform, "ShotgunModel");
                    ReplaceToolVisualInViewModel(sm, riflePrefab, "PH_RifleVisual",
                        new Vector3(0f, -0.02f, 0.14f), new Vector3(0f, -90f, 0f), Vector3.one * 0.75f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, playerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ReplaceToolVisualInViewModel(Transform toolRoot, GameObject phPrefab, string visualName, Vector3 localPos, Vector3 localEuler, Vector3 localScale)
        {
            if (toolRoot == null || phPrefab == null) return;
            Transform rig = toolRoot.Find("Rig");
            if (rig == null) return;

            Transform existing = rig.Find(visualName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            foreach (Renderer r in rig.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;

            GameObject vis = (GameObject)PrefabUtility.InstantiatePrefab(phPrefab, rig);
            vis.name = visualName;
            vis.transform.localPosition = localPos;
            vis.transform.localRotation = Quaternion.Euler(localEuler);
            vis.transform.localScale = localScale;
            foreach (Renderer r in vis.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private static void PopulateOpenSceneCabinAndRoad()
        {
            GameObject cabin = GameObject.Find("Camp_Cabin");
            if (cabin != null)
            {
                for (int i = cabin.transform.childCount - 1; i >= 0; i--)
                {
                    Transform ch = cabin.transform.GetChild(i);
                    if (ch.name.StartsWith("PH_"))
                    {
                        Object.DestroyImmediate(ch.gameObject);
                    }
                    else if (ch.name.StartsWith("Bed") || ch.name.StartsWith("TableTop") || ch.name.StartsWith("TableLeg")
                             || ch.name == "TableBook" || ch.name.StartsWith("Shelf") || ch.name.StartsWith("Decor_")
                             || ch.name.StartsWith("Chair"))
                    {
                        // Прячем старую процедурную мебель из кубиков, чтобы не пересекалась с реальными 3D-моделями Poly Haven.
                        ch.gameObject.SetActive(false);
                    }
                }

                const float floorY = 0.36f;

                // Реальная 3D-кровать, письменный стол, стул и книжный шкаф внутри хижины.
                SpawnPropInParent(cabin.transform, "PH_old_bed_frame", "PH_OldBed",
                    new Vector3(2.15f, floorY, -1.95f), new Vector3(0f, -90f, 0f), Vector3.one, addCollider: true);

                SpawnPropInParent(cabin.transform, "PH_metal_office_desk", "PH_OfficeDesk",
                    new Vector3(-0.35f, floorY, -0.35f), new Vector3(0f, 0f, 0f), Vector3.one, addCollider: true);

                SpawnPropInParent(cabin.transform, "PH_painted_wooden_chair_02", "PH_Chair",
                    new Vector3(-0.35f, floorY, 0.48f), new Vector3(0f, 180f, 0f), Vector3.one, addCollider: true);

                SpawnPropInParent(cabin.transform, "PH_wooden_bookshelf_worn", "PH_Bookshelf",
                    new Vector3(3.12f, floorY, 0.45f), new Vector3(0f, -90f, 0f), Vector3.one, addCollider: true);

                SpawnPropInParent(cabin.transform, "PH_painted_wooden_cabinet", "PH_Cabinet",
                    new Vector3(-2.65f, floorY, 2.20f), new Vector3(0f, 180f, 0f), Vector3.one, addCollider: true);

                SpawnPropInParent(cabin.transform, "PH_worn_metal_rack", "PH_MetalRack",
                    new Vector3(2.85f, floorY, -2.25f), new Vector3(0f, 0f, 0f), Vector3.one, addCollider: true);

                SpawnPropInParent(cabin.transform, "PH_long_life_food", "PH_Food_1",
                    new Vector3(2.85f, floorY + 0.92f, -2.25f), new Vector3(0f, 18f, 0f), Vector3.one, addCollider: false);

                SpawnPropInParent(cabin.transform, "PH_book_encyclopedia_set_01", "PH_Books_Cabinet",
                    new Vector3(-2.65f, floorY + 1.18f, 2.20f), new Vector3(0f, 12f, 0f), Vector3.one, addCollider: false);

                SpawnPropInParent(cabin.transform, "PH_small_plastic_torch", "PH_CabinTorch",
                    new Vector3(2.85f, floorY + 1.38f, -2.18f), new Vector3(0f, 40f, 0f), Vector3.one, addCollider: false);

                SpawnPropInParent(cabin.transform, "PH_machete", "PH_CabinMachete",
                    new Vector3(-2.35f, floorY + 1.19f, 2.15f), new Vector3(90f, 35f, 0f), Vector3.one, addCollider: false);

                SpawnPropInParent(cabin.transform, "PH_metal_jerrycan", "PH_PorchJerrycan",
                    new Vector3(2.65f, floorY, 3.85f), new Vector3(0f, -35f, 0f), Vector3.one, addCollider: true);

                // Прожектор над дверью крыльца, электрощиток и наружный блок кондиционера на стене дома
                SpawnPropInParent(cabin.transform, "PH_security_light", "PH_PorchSecurityLight",
                    new Vector3(1.35f, floorY + 2.55f, 3.38f), new Vector3(0f, 0f, 0f), Vector3.one * 0.85f, addCollider: false);

                SpawnPropInParent(cabin.transform, "PH_utility_box_02", "PH_CabinUtilityBox",
                    new Vector3(3.85f, floorY + 0.0f, 1.45f), new Vector3(0f, 90f, 0f), Vector3.one, addCollider: true);

                SpawnPropInParent(cabin.transform, "PH_exterior_aircon_unit", "PH_CabinAircon",
                    new Vector3(-3.88f, floorY + 0.15f, -0.45f), new Vector3(0f, -90f, 0f), Vector3.one, addCollider: true);

                // Полка внутри дома, мусорный бак у крыльца и стол для пикника во дворе
                SpawnPropInParent(cabin.transform, "PH_shelf_01", "PH_StandingShelf",
                    new Vector3(0.35f, floorY, -2.92f), new Vector3(0f, 0f, 0f), Vector3.one, addCollider: true);

                SpawnPropInParent(cabin.transform, "PH_metal_trash_can", "PH_CabinTrashCan",
                    new Vector3(4.35f, 0f, 3.65f), new Vector3(0f, 25f, 0f), Vector3.one, addCollider: true);

                SpawnPropInParent(cabin.transform, "PH_wooden_picnic_table", "PH_YardPicnicTable",
                    new Vector3(-5.6f, 0f, 4.6f), new Vector3(0f, 18f, 0f), Vector3.one, addCollider: true);

                // Новые 3D-пропы: плита в хижине, кассетный плеер и портсигар на столе, камера наблюдения на крыльце
                SpawnPropInParent(cabin.transform, "PH_electric_stove", "PH_CabinStove",
                    new Vector3(-2.85f, floorY, -1.85f), new Vector3(0f, 90f, 0f), Vector3.one, addCollider: true);
                SpawnPropInParent(cabin.transform, "PH_portable_cassette_player", "PH_CabinCassettePlayer",
                    new Vector3(-0.18f, floorY + 0.78f, -0.30f), new Vector3(0f, -24f, 0f), Vector3.one * 1.15f, addCollider: false);
                SpawnPropInParent(cabin.transform, "PH_cigarette_case", "PH_CabinCigaretteCase",
                    new Vector3(-0.55f, floorY + 0.78f, -0.42f), new Vector3(0f, 32f, 0f), Vector3.one * 1.25f, addCollider: false);
                SpawnPropInParent(cabin.transform, "PH_security_camera_01", "PH_PorchSecurityCam",
                    new Vector3(-3.35f, floorY + 2.65f, 3.55f), new Vector3(12f, 32f, 0f), Vector3.one * 1.2f, addCollider: false);
            }

            // Железные ворота на въезде в город (z = 56).
            GameObject oldGate = GameObject.Find("PH_CityIronGate");
            if (oldGate != null) Object.DestroyImmediate(oldGate);

            GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsOut}/PH_large_iron_gate.prefab");
            if (gatePrefab != null)
            {
                GameObject gateRoot = new GameObject("PH_CityIronGate");
                gateRoot.transform.position = new Vector3(0f, 0f, 56f);

                GameObject leftWing = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab, gateRoot.transform);
                leftWing.name = "GateWing_L";
                leftWing.transform.localPosition = new Vector3(-5.8f, 0f, 0f);
                leftWing.transform.localRotation = Quaternion.Euler(0f, -22f, 0f);
                EnsureBoxCollider(leftWing);

                GameObject rightWing = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab, gateRoot.transform);
                rightWing.name = "GateWing_R";
                rightWing.transform.localPosition = new Vector3(5.8f, 0f, 0f);
                rightWing.transform.localRotation = Quaternion.Euler(0f, 200f, 0f);
                EnsureBoxCollider(rightWing);

                // Бетонные дорожные блоки и электрощиток у въезда в город
                SpawnPropInParent(gateRoot.transform, "PH_concrete_road_barrier", "PH_Barrier_L",
                    new Vector3(-4.6f, 0f, -4.5f), new Vector3(0f, 12f, 0f), Vector3.one, addCollider: true);
                SpawnPropInParent(gateRoot.transform, "PH_concrete_road_barrier", "PH_Barrier_R",
                    new Vector3(4.8f, 0f, -6.2f), new Vector3(0f, -15f, 0f), Vector3.one, addCollider: true);
                SpawnPropInParent(gateRoot.transform, "PH_utility_box_02", "PH_CityUtilityBox",
                    new Vector3(7.4f, 0f, -2.5f), new Vector3(0f, -90f, 0f), Vector3.one, addCollider: true);
                SpawnPropInParent(gateRoot.transform, "PH_metal_trash_can", "PH_CityTrashCan",
                    new Vector3(-7.2f, 0f, -1.8f), new Vector3(0f, 15f, 0f), Vector3.one, addCollider: true);
            }

            // Уличные фонари вдоль дороги от дома к городу.
            GameObject oldLamps = GameObject.Find("PH_RoadStreetLamps");
            if (oldLamps != null) Object.DestroyImmediate(oldLamps);

            GameObject lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsOut}/PH_street_lamp_02.prefab");
            if (lampPrefab != null)
            {
                GameObject lampsRoot = new GameObject("PH_RoadStreetLamps");
                float[] zPositions = { 16f, 36f, 54f };
                for (int i = 0; i < zPositions.Length; i++)
                {
                    float side = (i % 2 == 0) ? -6.8f : 6.8f;
                    float yaw = side < 0f ? 90f : -90f;
                    GameObject post = new GameObject($"StreetLampPost_{i}");
                    post.transform.SetParent(lampsRoot.transform, false);
                    post.transform.position = new Vector3(side, 0f, zPositions[i]);
                    post.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

                    // Металлический столб под светильник
                    GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pole.name = "Pole";
                    pole.transform.SetParent(post.transform, false);
                    pole.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                    pole.transform.localScale = new Vector3(0.14f, 2.2f, 0.14f);

                    GameObject lamp = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab, post.transform);
                    lamp.name = "LampHead";
                    lamp.transform.localPosition = new Vector3(0f, 3.6f, 0f);
                    lamp.transform.localRotation = Quaternion.identity;

                    GameObject lightObj = new GameObject("LampLight");
                    lightObj.transform.SetParent(post.transform, false);
                    lightObj.transform.localPosition = new Vector3(0f, 4.1f, 0.55f);
                    Light l = lightObj.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1.0f, 0.80f, 0.52f);
                    l.intensity = 14f;
                    l.range = 18f;
                    l.shadows = LightShadows.Soft;
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static void SpawnPropInParent(Transform parent, string prefabName, string objName, Vector3 localPos, Vector3 localEuler, Vector3 localScale, bool addCollider)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsOut}/{prefabName}.prefab");
            if (prefab == null) return;
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = objName;
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            if (addCollider) EnsureBoxCollider(go);
        }

        private static void EnsureBoxCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null) return;
            Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            BoxCollider col = go.AddComponent<BoxCollider>();
            col.center = go.transform.InverseTransformPoint(b.center);
            col.size = go.transform.InverseTransformVector(b.size);
            col.size = new Vector3(Mathf.Abs(col.size.x), Mathf.Abs(col.size.y), Mathf.Abs(col.size.z));
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string folder = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
