using System.Collections.Generic;
using Ogni.Building;
using Ogni.Entities.Darkness;
using Ogni.Entities.Kitsune;
using Ogni.Entities.Listener;
using Ogni.Entities.Mimic;
using Ogni.Net;
using Ogni.Player;
using Ogni.Resources;
using Ogni.Vehicles;
using Ogni.World;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Меню Ogni -> Build World Scene.
    /// Собирает расширенный мир 700x550 м (границы x ±320, z -230..265):
    /// - Рельефный Terrain с лесными холмами и дорогами
    /// - Лес со скалистыми валунами (3D-меш со сколами), ветками и камнями для сбора
    /// - Лагерь с верстаком (меню крафта), костром, домиком и баррикадами
    /// - Заброшенный город с 4-этажными высотками, магазином, заправкой, складом, медпунктом,
    ///   детализированными машинами и ресурсами
    /// - Детализированный гуманоидный Слушатель
    /// </summary>
    public static class WorldSceneBuilder
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string BarricadePrefabPath = "Assets/_Project/Prefabs/Barricade.prefab";
        private const string DoorPrefabPath = "Assets/_Project/Prefabs/Door.prefab";
        private const string CampfirePrefabPath = "Assets/_Project/Prefabs/Campfire.prefab";
        private const string WorkbenchPrefabPath = "Assets/_Project/Prefabs/Workbench.prefab";
        private const string ListenerPrefabPath = "Assets/_Project/Prefabs/Listener.prefab";
        private const string TreePrefabPath = "Assets/_Project/Prefabs/PineTree.prefab";
        private const string BoulderPrefabPath = "Assets/_Project/Prefabs/Boulder.prefab";
        private const string CarPrefabPath = "Assets/_Project/Prefabs/AbandonedCar.prefab";
        private const string DarknessPrefabPath = "Assets/_Project/Prefabs/Darkness.prefab";
        private const string KitsunePrefabPath = "Assets/_Project/Prefabs/Kitsune.prefab";
        private const string MimicPrefabPath = "Assets/_Project/Prefabs/Mimic.prefab";
        private const string TerrainDataPath = "Assets/_Project/Art/WorldTerrain.asset";
        private const string SkyboxMatPath = "Assets/_Project/Art/Materials/Mat_SkyProcedural.mat";
        private const string WorldScenePath = "Assets/_Project/Scenes/World_Main.unity";

        [MenuItem("Ogni/Build World Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Ogni] Сначала остановите Play Mode, затем запустите Ogni -> Build World Scene.");
                return;
            }

            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Scenes");
            EnsureFolder("Assets/_Project/Art");

            ProceduralMaterialLibrary mats = ProceduralMaterialLibrary.BuildAll();
            AbandonedMaterials abandonedMats = AbandonedMaterials.BuildAll();
            Mesh rockMesh = ProceduralMeshBuilder.GetOrCreateRockMesh();
            Mesh pineCrownMesh = ProceduralMeshBuilder.GetOrCreatePineCrownMesh();
            Material skyMat = BuildSkyboxMaterial();

            // Пересобираем Player.prefab, чтобы на нём точно был PlayerInventory.
            SandboxSceneBuilder.Build();
            GameObject playerPrefab = SandboxSceneBuilder.EnsurePlayerPrefab();

            GameObject barricadePrefab = BuildBarricadePrefab(mats);
            GameObject doorPrefab = BuildDoorPrefab(mats);
            GameObject campfirePrefab = BuildCampfirePrefab(mats, rockMesh);
            // Верстака в лагере больше нет: крафт идёт через меню игрока. Удаляем старый префаб, если он остался.
            AssetDatabase.DeleteAsset(WorkbenchPrefabPath);
            GameObject zombiePrefab = ZombieMonsterSetup.BuildPrefab();
            GameObject listenerPrefab = zombiePrefab != null ? zombiePrefab : BuildListenerPrefab(mats);

            GameObject woodPickup = BuildPickupPrefab("Pickup_Wood", ResourceType.Wood, 2, mats.TreeBark, null, mats);
            GameObject stonePickup = BuildPickupPrefab("Pickup_Stone", ResourceType.Stone, 2, mats.Stone, rockMesh, mats);
            GameObject metalPickup = BuildPickupPrefab("Pickup_Metal", ResourceType.Metal, 2, mats.RustyMetal, null, mats);
            GameObject clothPickup = BuildPickupPrefab("Pickup_Cloth", ResourceType.Cloth, 2, mats.ClothRoll, null, mats);
            GameObject fuelPickup = BuildPickupPrefab("Pickup_Fuel", ResourceType.Fuel, 1, mats.FuelCanRed, null, mats);
            GameObject medPickup = BuildPickupPrefab("Pickup_Med", ResourceType.Medicine, 1, mats.MedicalWhite, null, mats);
            GameObject ammoPickup = BuildPickupPrefab("Pickup_Ammo", ResourceType.Ammo, 6, mats.AmmoMetal, null, mats);
            GameObject batteryPickup = BuildPickupPrefab("Pickup_Battery", ResourceType.Battery, 1, mats.BatteryPlastic, null, mats);
            GameObject planksPickup = BuildPickupPrefab("Pickup_Planks", ResourceType.Planks, 2, mats.WoodPlanks, null, mats);
            GameObject arrowPickup = BuildPickupPrefab("Pickup_Arrows", ResourceType.Arrows, 4, mats.WoodPlanks, null, mats);

            GameObject treePrefab = BuildTreePrefab(mats, pineCrownMesh);
            GameObject sakuraPrefab = SakuraTreeBuilder.BuildPrefab(mats);
            GameObject boulderPrefab = BuildBoulderPrefab(mats, rockMesh);
            GameObject[] carPrefabs = BuildLoftedCarPrefabs(mats, abandonedMats);
            GameObject carPrefab = carPrefabs[0];
            GameObject darknessPrefab = BuildDarknessPrefab();
            GameObject kitsunePrefab = BuildKitsunePrefab();
            GameObject mimicPrefab = BuildMimicPrefab();

            BuildWorldScene(
                mats, abandonedMats, rockMesh, skyMat, playerPrefab, barricadePrefab, doorPrefab,
                campfirePrefab, listenerPrefab, darknessPrefab, kitsunePrefab, mimicPrefab,
                treePrefab, sakuraPrefab, boulderPrefab, carPrefab, carPrefabs,
                woodPickup, stonePickup, metalPickup, clothPickup, fuelPickup, medPickup, ammoPickup, batteryPickup,
                planksPickup, arrowPickup);

            EnsureSceneInBuildSettings(WorldScenePath);
            UrpRealismSetup.Apply();
            Debug.Log("[Ogni] Готово: мир " + WorldScenePath + " (700x550 м, высотки, часовня, промзона, ферма).");
        }

        private static Material BuildSkyboxMaterial()
        {
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader == null) return null;

            Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMatPath);
            if (sky == null)
            {
                sky = new Material(skyShader);
                AssetDatabase.CreateAsset(sky, SkyboxMatPath);
            }
            else
            {
                sky.shader = skyShader;
            }

            if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.28f, 0.36f, 0.48f));
            if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.08f, 0.10f, 0.08f));
            if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.03f);
            EditorUtility.SetDirty(sky);
            AssetDatabase.SaveAssets();
            return sky;
        }

        private static GameObject BuildBarricadePrefab(ProceduralMaterialLibrary mats)
        {
            GameObject root = new GameObject("Barricade");
            root.AddComponent<NetworkObject>();
            Barricade barricade = root.AddComponent<Barricade>();

            CreatePart(root.transform, "Threshold", new Vector3(0f, 0.1f, 0f), new Vector3(3.4f, 0.2f, 0.4f), mats.WoodPlanks, true);
            CreatePart(root.transform, "PostLeft", new Vector3(-1.65f, 0.75f, 0f), new Vector3(0.22f, 1.5f, 0.35f), mats.WoodPlanks, true);
            CreatePart(root.transform, "PostRight", new Vector3(1.65f, 0.75f, 0f), new Vector3(0.22f, 1.5f, 0.35f), mats.WoodPlanks, true);

            GameObject barrierGroup = new GameObject("BarrierPlanks");
            barrierGroup.transform.SetParent(root.transform, false);
            BoxCollider barrierCollider = barrierGroup.AddComponent<BoxCollider>();
            barrierCollider.center = new Vector3(0f, 0.85f, 0f);
            barrierCollider.size = new Vector3(3.1f, 1.3f, 0.3f);

            GameObject p1 = CreatePart(barrierGroup.transform, "PlankLow", new Vector3(0f, 0.45f, 0f), new Vector3(3.2f, 0.24f, 0.14f), mats.WoodPlanks, false);
            GameObject p2 = CreatePart(barrierGroup.transform, "PlankMid", new Vector3(0f, 0.88f, 0f), new Vector3(3.2f, 0.24f, 0.14f), mats.WoodPlanks, false);
            GameObject p3 = CreatePart(barrierGroup.transform, "PlankHigh", new Vector3(0f, 1.30f, 0f), new Vector3(3.2f, 0.24f, 0.14f), mats.WoodPlanks, false);
            GameObject brace = CreatePart(barrierGroup.transform, "CrossBrace", new Vector3(0f, 0.88f, 0.08f), new Vector3(2.9f, 0.16f, 0.08f), mats.WoodPlanks, false);
            brace.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);

            SetReference(barricade, "barrierVisual", barrierGroup);
            SetReference(barricade, "barrierCollider", barrierCollider);
            SetReferences(barricade, "barrierRenderers", new Object[]
            {
                p1.GetComponent<Renderer>(), p2.GetComponent<Renderer>(),
                p3.GetComponent<Renderer>(), brace.GetComponent<Renderer>()
            });

            PrefabUtility.SaveAsPrefabAsset(root, BarricadePrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(BarricadePrefabPath);
        }

        private static GameObject BuildDoorPrefab(ProceduralMaterialLibrary mats)
        {
            const float dw = 1.4f; // ширина полотна: проход стал шире (было 1.2 м)
            GameObject root = new GameObject("Door");
            root.AddComponent<NetworkObject>();
            Door door = root.AddComponent<Door>();

            GameObject pivot = new GameObject("DoorPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(-dw * 0.5f, 0f, 0f);

            // Полотно: тяжёлая обшарпанная дверь с филёнками, петлями и ручкой.
            CreatePart(pivot.transform, "DoorPanel", new Vector3(dw * 0.5f, 1.05f, 0f), new Vector3(dw, 2.1f, 0.1f), mats.WoodPlanks, true);
            int boards = Mathf.RoundToInt(dw / 0.2f);
            for (int i = 0; i < boards; i++)
            {
                CreatePart(pivot.transform, "Board" + i, new Vector3(0.1f + i * 0.2f, 1.05f, 0.058f), new Vector3(0.19f, 2.05f, 0.025f), mats.WoodPlanks, false);
            }

            CreatePart(pivot.transform, "LedgeHigh", new Vector3(dw * 0.5f, 1.78f, 0.075f), new Vector3(dw - 0.08f, 0.14f, 0.03f), mats.WoodPlanks, false);
            CreatePart(pivot.transform, "LedgeLow", new Vector3(dw * 0.5f, 0.34f, 0.075f), new Vector3(dw - 0.08f, 0.14f, 0.03f), mats.WoodPlanks, false);
            GameObject diag = CreatePart(pivot.transform, "Diagonal", new Vector3(dw * 0.5f, 1.06f, 0.078f), new Vector3(1.9f, 0.12f, 0.03f), mats.WoodPlanks, false);
            diag.transform.localRotation = Quaternion.Euler(0f, 0f, 42f);

            foreach (float y in new[] { 0.34f, 1.05f, 1.78f })
            {
                CreatePart(pivot.transform, "HingeStrap", new Vector3(0.16f, y, 0.095f), new Vector3(0.3f, 0.06f, 0.02f), mats.RustyMetal, false);
                CreatePart(pivot.transform, "HingePin", new Vector3(0.0f, y, 0f), new Vector3(0.04f, 0.055f, 0.04f), mats.RustyMetal, false, PrimitiveType.Cylinder);
            }

            CreatePart(pivot.transform, "HandlePlate", new Vector3(dw - 0.13f, 1.0f, 0.09f), new Vector3(0.08f, 0.22f, 0.015f), mats.RustyMetal, false);
            CreatePart(pivot.transform, "Handle", new Vector3(dw - 0.13f, 1.0f, 0.13f), new Vector3(0.14f, 0.05f, 0.07f), mats.RustyMetal, false);
            CreatePart(pivot.transform, "HandleBack", new Vector3(dw - 0.13f, 1.0f, -0.09f), new Vector3(0.14f, 0.05f, 0.07f), mats.RustyMetal, false);

            // Наличник по периметру проёма (снаружи полотна, чтобы не сужать проход).
            float tx = dw * 0.5f + 0.04f;
            CreatePart(root.transform, "TrimL", new Vector3(-tx, 1.06f, 0f), new Vector3(0.08f, 2.12f, 0.26f), mats.WoodPlanks, false);
            CreatePart(root.transform, "TrimR", new Vector3(tx, 1.06f, 0f), new Vector3(0.08f, 2.12f, 0.26f), mats.WoodPlanks, false);
            CreatePart(root.transform, "TrimTop", new Vector3(0f, 2.14f, 0f), new Vector3(dw + 0.16f, 0.08f, 0.26f), mats.WoodPlanks, false);
            CreatePart(root.transform, "Threshold", new Vector3(0f, 0.02f, 0f), new Vector3(dw + 0.08f, 0.04f, 0.28f), mats.Concrete, false);

            SetReference(door, "doorPivot", pivot.transform);

            PrefabUtility.SaveAsPrefabAsset(root, DoorPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
        }

        private static GameObject BuildCampfirePrefab(ProceduralMaterialLibrary mats, Mesh rockMesh)
        {
            GameObject root = new GameObject("Campfire");
            root.AddComponent<NetworkObject>();
            Campfire campfire = root.AddComponent<Campfire>();

            // Кольцо из угловатых камней.
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2f / 10f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * 0.72f, 0.11f, Mathf.Sin(angle) * 0.72f);
                GameObject st = CreateMeshPart(root.transform, $"Stone_{i}", pos, new Vector3(0.24f, 0.17f, 0.24f), rockMesh, mats.Stone, true);
                st.transform.localRotation = Quaternion.Euler(0f, i * 37f, 0f);
            }

            // Пепельная подушка и раскалённые угли в центре.
            CreatePart(root.transform, "AshBed", new Vector3(0f, 0.02f, 0f), new Vector3(1.25f, 0.04f, 1.25f), mats.Ash, false, PrimitiveType.Cylinder);
            CreatePart(root.transform, "Embers", new Vector3(0f, 0.05f, 0f), new Vector3(0.95f, 0.08f, 0.95f), mats.Ember, false, PrimitiveType.Cylinder);

            // 6 обугленных поленьев, сложенных шалашиком.
            for (int l = 0; l < 6; l++)
            {
                float a = l * 60f * Mathf.Deg2Rad;
                GameObject log = CreatePart(root.transform, $"Log_{l}",
                    new Vector3(Mathf.Cos(a) * 0.22f, 0.22f, Mathf.Sin(a) * 0.22f),
                    new Vector3(0.12f, 0.42f, 0.12f), mats.CharredWood, false, PrimitiveType.Cylinder);
                log.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 48f, 0f, -Mathf.Cos(a) * 48f);
            }

            // Группа живых языков пламени (5 отдельных язычков).
            GameObject flameGroup = new GameObject("FlameGroup");
            flameGroup.transform.SetParent(root.transform, false);
            Transform[] tongues = new Transform[5];
            Vector3[] offsets =
            {
                new Vector3(0f, 0.42f, 0f),
                new Vector3(0.14f, 0.34f, 0.10f),
                new Vector3(-0.14f, 0.35f, -0.08f),
                new Vector3(0.08f, 0.32f, -0.14f),
                new Vector3(-0.10f, 0.33f, 0.12f)
            };
            for (int f = 0; f < 5; f++)
            {
                GameObject tongue = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                tongue.name = $"Flame_{f}";
                tongue.transform.SetParent(flameGroup.transform, false);
                tongue.transform.localPosition = offsets[f];
                float w = f == 0 ? 0.22f : 0.14f;
                float h = f == 0 ? 0.34f : 0.24f;
                tongue.transform.localScale = new Vector3(w, h, w);
                Object.DestroyImmediate(tongue.GetComponent<Collider>());
                tongue.GetComponent<Renderer>().sharedMaterial = mats.Flame;
                tongues[f] = tongue.transform;
            }

            BuildSmokeParticles(root.transform, mats);
            BuildSparkParticles(root.transform, mats);

            GameObject lightObj = new GameObject("FireLight");
            lightObj.transform.SetParent(root.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            Light fireLight = lightObj.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.range = 20f;
            fireLight.intensity = 3.8f;
            fireLight.color = new Color(1f, 0.50f, 0.16f);
            fireLight.shadows = LightShadows.Soft;

            SetReference(campfire, "fireLight", fireLight);
            SetReference(campfire, "flameVisual", flameGroup);
            SetReferences(campfire, "flameTongues", tongues);

            PrefabUtility.SaveAsPrefabAsset(root, CampfirePrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(CampfirePrefabPath);
        }

        private static void BuildSmokeParticles(Transform parent, ProceduralMaterialLibrary mats)
        {
            GameObject go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.55f, 0f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 4.2f;
            main.startSpeed = 0.38f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.24f, 0.23f, 0.22f, 0.30f), new Color(0.42f, 0.41f, 0.40f, 0.10f));
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.02f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 7f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.18f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.35f, 0.34f, 0.33f), 0f),
                    new GradientColorKey(new Color(0.18f, 0.18f, 0.18f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.28f, 0.15f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.6f));

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            if (r == null) r = go.AddComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mats.SmokeParticle;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static void BuildSparkParticles(Transform parent, ProceduralMaterialLibrary mats)
        {
            GameObject go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.42f, 0f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.25f, 1f), new Color(1f, 0.32f, 0.05f, 1f));
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.35f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 13f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.10f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
            if (r == null) r = go.AddComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mats.EmberParticle;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static GameObject BuildListenerPrefab(ProceduralMaterialLibrary mats)
        {
            GameObject root = new GameObject("Listener");
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<ListenerAI>();

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.45f;
            collider.height = 1.9f;
            collider.center = new Vector3(0f, 0.95f, 0f);

            GameObject rig = new GameObject("VisualRig");
            rig.transform.SetParent(root.transform, false);

            if (ListenerModelSetup.LoadOrBuild(out Mesh listenerMesh, out Material listenerMat))
            {
                GameObject model = new GameObject("HorrorMonsterModel");
                model.transform.SetParent(rig.transform, false);
                MeshFilter mf = model.AddComponent<MeshFilter>();
                mf.sharedMesh = listenerMesh;
                MeshRenderer mr = model.AddComponent<MeshRenderer>();
                mr.sharedMaterial = listenerMat;

                PrefabUtility.SaveAsPrefabAsset(root, ListenerPrefabPath);
                Object.DestroyImmediate(root);
                return AssetDatabase.LoadAssetAtPath<GameObject>(ListenerPrefabPath);
            }

            // Детальная процедурная модель (меши сохраняются как ассеты, чтобы переживали пересборку префаба).
            ListenerBodyBuilder.Build(rig.transform, mats);
            root.AddComponent<ListenerLimbAnimator>();

            PrefabUtility.SaveAsPrefabAsset(root, ListenerPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(ListenerPrefabPath);
        }

        private static GameObject BuildPickupPrefab(
            string name, ResourceType type, int amount, Material mat, Mesh customMesh, ProceduralMaterialLibrary mats)
        {
            string path = $"Assets/_Project/Prefabs/{name}.prefab";
            GameObject root = new GameObject(name);
            root.AddComponent<NetworkObject>();
            ResourcePickup pickup = root.AddComponent<ResourcePickup>();
            Transform t = root.transform;

            switch (type)
            {
                case ResourceType.Wood:
                {
                    // Ветка-рогатина с косым срезом и трухой.
                    GameObject b1 = CreatePart(t, "BranchMain", new Vector3(0f, 0.08f, 0f), new Vector3(0.11f, 0.55f, 0.11f), mat, true, PrimitiveType.Cylinder);
                    b1.transform.localRotation = Quaternion.Euler(85f, 20f, 0f);
                    GameObject b2 = CreatePart(t, "BranchFork", new Vector3(0.12f, 0.11f, 0.15f), new Vector3(0.07f, 0.28f, 0.07f), mat, false, PrimitiveType.Cylinder);
                    b2.transform.localRotation = Quaternion.Euler(70f, 55f, 0f);
                    CreatePart(t, "CutEnd", new Vector3(-0.24f, 0.13f, 0.02f), new Vector3(0.12f, 0.03f, 0.12f), mats.PlankCut, false, PrimitiveType.Cylinder);
                    CreatePart(t, "Twig", new Vector3(0.02f, 0.18f, -0.12f), new Vector3(0.04f, 0.24f, 0.04f), mat, false, PrimitiveType.Cylinder);
                    break;
                }
                case ResourceType.Stone:
                {
                    CreateMeshPart(t, "RockVisual", new Vector3(0f, 0.14f, 0f), new Vector3(0.30f, 0.24f, 0.30f), customMesh, mat, true);
                    CreateMeshPart(t, "RockChipA", new Vector3(0.18f, 0.05f, 0.08f), new Vector3(0.12f, 0.08f, 0.12f), customMesh, mat, false);
                    CreateMeshPart(t, "RockChipB", new Vector3(-0.14f, 0.04f, -0.12f), new Vector3(0.09f, 0.06f, 0.09f), customMesh, mats.Stone, false);
                    break;
                }
                case ResourceType.Metal:
                {
                    CreatePart(t, "ScrapPlate", new Vector3(0f, 0.08f, 0f), new Vector3(0.44f, 0.05f, 0.30f), mat, true);
                    GameObject pipe = CreatePart(t, "Pipe", new Vector3(0.02f, 0.16f, 0.02f), new Vector3(0.07f, 0.38f, 0.07f), mats.RustyMetal, false, PrimitiveType.Cylinder);
                    pipe.transform.localRotation = Quaternion.Euler(72f, 20f, 0f);
                    CreatePart(t, "Bolt", new Vector3(-0.14f, 0.16f, 0.08f), new Vector3(0.07f, 0.07f, 0.07f), mats.AmmoMetal, false, PrimitiveType.Cylinder);
                    break;
                }
                case ResourceType.Cloth:
                {
                    CreatePart(t, "ClothRoll", new Vector3(0f, 0.10f, 0f), new Vector3(0.38f, 0.18f, 0.26f), mat, true, PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    CreatePart(t, "ClothFold", new Vector3(0.06f, 0.20f, 0.03f), new Vector3(0.30f, 0.06f, 0.22f), mats.ClothRoll, false);
                    CreatePart(t, "Strap", new Vector3(-0.08f, 0.12f, 0.14f), new Vector3(0.24f, 0.03f, 0.05f), mats.RustyMetal, false);
                    break;
                }
                case ResourceType.Fuel:
                {
                    CreatePart(t, "Canister", new Vector3(0f, 0.22f, 0f), new Vector3(0.32f, 0.44f, 0.20f), mat, true);
                    CreatePart(t, "Handle", new Vector3(0f, 0.46f, 0f), new Vector3(0.20f, 0.05f, 0.06f), mats.RustyMetal, false);
                    CreatePart(t, "Cap", new Vector3(0.08f, 0.46f, 0f), new Vector3(0.08f, 0.06f, 0.08f), mats.RustyMetal, false);
                    CreatePart(t, "Spout", new Vector3(-0.10f, 0.40f, 0f), new Vector3(0.05f, 0.18f, 0.05f), mats.RustyMetal, false, PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
                    CreatePart(t, "Label", new Vector3(0f, 0.22f, 0.105f), new Vector3(0.18f, 0.12f, 0.01f), mats.MedicalWhite, false);
                    break;
                }
                case ResourceType.Medicine:
                {
                    CreatePart(t, "MedBox", new Vector3(0f, 0.12f, 0f), new Vector3(0.36f, 0.22f, 0.26f), mat, true);
                    CreatePart(t, "CrossV", new Vector3(0f, 0.12f, 0.135f), new Vector3(0.06f, 0.15f, 0.01f), mats.FuelCanRed, false);
                    CreatePart(t, "CrossH", new Vector3(0f, 0.12f, 0.135f), new Vector3(0.15f, 0.06f, 0.01f), mats.FuelCanRed, false);
                    CreatePart(t, "Latch", new Vector3(0f, 0.24f, 0.04f), new Vector3(0.08f, 0.03f, 0.06f), mats.RustyMetal, false);
                    break;
                }
                case ResourceType.Ammo:
                {
                    CreatePart(t, "AmmoBox", new Vector3(0f, 0.10f, 0f), new Vector3(0.40f, 0.20f, 0.26f), mat, true);
                    CreatePart(t, "AmmoLid", new Vector3(0f, 0.21f, 0f), new Vector3(0.42f, 0.03f, 0.28f), mats.AmmoMetal, false);
                    for (int i = 0; i < 3; i++)
                    {
                        CreatePart(t, "Round" + i, new Vector3(-0.10f + i * 0.10f, 0.26f, 0.04f), new Vector3(0.035f, 0.10f, 0.035f), mats.AmmoMetal, false, PrimitiveType.Cylinder);
                    }
                    break;
                }
                case ResourceType.Planks:
                {
                    for (int i = 0; i < 4; i++)
                    {
                        GameObject plank = CreatePart(t, "Plank" + i, new Vector3(0.0f, 0.04f + i * 0.05f, (i % 2) * 0.03f - 0.02f), new Vector3(0.8f - i * 0.05f, 0.045f, 0.16f), i % 2 == 0 ? mat : mats.PlankCut, i == 0);
                        plank.transform.localRotation = Quaternion.Euler(0f, (i - 1.5f) * 7f, 0f);
                    }

                    CreatePart(t, "Strap", new Vector3(0.12f, 0.12f, 0f), new Vector3(0.04f, 0.2f, 0.22f), mats.ClothRoll, false);
                    CreatePart(t, "Nail", new Vector3(-0.2f, 0.22f, 0.0f), new Vector3(0.02f, 0.02f, 0.02f), mats.RustyMetal, false, PrimitiveType.Cylinder);
                    break;
                }
                case ResourceType.Arrows:
                {
                    // Связка из 5 стрел в тряпичной обмотке.
                    for (int i = 0; i < 5; i++)
                    {
                        float ox = (i - 2) * 0.028f;
                        GameObject shaft = CreatePart(t, "Shaft" + i, new Vector3(ox, 0.06f + (i % 2) * 0.02f, 0f), new Vector3(0.012f, 0.34f, 0.012f), mat, i == 2, PrimitiveType.Cylinder);
                        shaft.transform.localRotation = Quaternion.Euler(90f, 0f, i * 3f);
                        CreatePart(t, "Head" + i, new Vector3(ox, 0.06f + (i % 2) * 0.02f, 0.35f), new Vector3(0.02f, 0.02f, 0.06f), mats.AmmoMetal, false);
                        CreatePart(t, "Fletch" + i, new Vector3(ox, 0.06f + (i % 2) * 0.02f, -0.3f), new Vector3(0.05f, 0.006f, 0.1f), mats.MedicalWhite, false);
                    }

                    CreatePart(t, "Binding", new Vector3(0f, 0.07f, 0.0f), new Vector3(0.16f, 0.07f, 0.03f), mats.ClothRoll, false);
                    break;
                }
                case ResourceType.Battery:
                {
                    GameObject body = CreatePart(t, "BatteryBody", new Vector3(0f, 0.13f, 0f), new Vector3(0.16f, 0.26f, 0.16f), mat, true, PrimitiveType.Cylinder);
                    body.transform.localRotation = Quaternion.identity;
                    CreatePart(t, "BatteryTop", new Vector3(0f, 0.27f, 0f), new Vector3(0.07f, 0.03f, 0.07f), mats.AmmoMetal, false, PrimitiveType.Cylinder);
                    CreatePart(t, "BatteryLabel", new Vector3(0f, 0.13f, 0.085f), new Vector3(0.10f, 0.12f, 0.01f), mats.MedicalWhite, false);
                    break;
                }
            }

            SetInt(pickup, "resourceType", (int)type);
            SetInt(pickup, "amount", amount);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject BuildTreePrefab(ProceduralMaterialLibrary mats, Mesh pineCrownMesh)
        {
            GameObject root = new GameObject("PineTree");
            BuildPineTree(root.transform, "Model", Vector3.zero, 1f, 0f, mats, pineCrownMesh);
            // Деревья локальные (не NetworkObject): индекс выдаёт RuntimeWorldGenerator.
            root.AddComponent<TreeResource>();
            PrefabUtility.SaveAsPrefabAsset(root, TreePrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
        }

        private static GameObject BuildBoulderPrefab(ProceduralMaterialLibrary mats, Mesh rockMesh)
        {
            GameObject root = CreateMeshPart(null, "Boulder", Vector3.zero, Vector3.one, rockMesh, mats.Stone, true);
            PrefabUtility.SaveAsPrefabAsset(root, BoulderPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(BoulderPrefabPath);
        }

        private static GameObject[] BuildLoftedCarPrefabs(ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            GameObject[] prefabs = new GameObject[LoftedCarBuilder.VariantCount];
            for (int v = 0; v < LoftedCarBuilder.VariantCount; v++)
            {
                string path = $"Assets/_Project/Prefabs/AbandonedCar_{v}.prefab";
                GameObject car = LoftedCarBuilder.Build(null, $"AbandonedCar_{v}", v, mats, ab);
                MakeCarDriveable(car);
                PrefabUtility.SaveAsPrefabAsset(car, path);
                Object.DestroyImmediate(car);
                prefabs[v] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return prefabs;
        }

        private static void MakeCarDriveable(GameObject car)
        {
            Rigidbody rb = car.AddComponent<Rigidbody>();
            rb.mass = 1200f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            BoxCollider box = car.GetComponent<BoxCollider>();
            if (box != null) box.sharedMaterial = GetCarPhysicsMaterial();

            car.AddComponent<NetworkObject>();
            NetworkTransform nt = car.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            car.AddComponent<DriveableCar>();
        }

        private static PhysicsMaterial GetCarPhysicsMaterial()
        {
            const string path = "Assets/_Project/Art/Mat_CarSlide.asset";
            PhysicsMaterial m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (m == null)
            {
                m = new PhysicsMaterial("CarSlide");
                AssetDatabase.CreateAsset(m, path);
            }

            m.dynamicFriction = 0.05f;
            m.staticFriction = 0.05f;
            m.frictionCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static GameObject BuildCarPrefab(ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            GameObject root = new GameObject("AbandonedCar");
            BuildCar(root.transform, "Model", Vector3.zero, 0f, mats, ab);
            PrefabUtility.SaveAsPrefabAsset(root, CarPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
        }

        // ------------------------------------------------------------------ сущности (Тьма, Кицунэ, Подражатель)

        /// <summary>
        /// Общая сборка префаба сущности: сетевой корень (NetworkObject + NetworkTransform + ИИ + капсула) и VisualRig
        /// с процедурной моделью и аниматором. Меши/материалы сохраняются ассетами, поэтому переживают пересборку.
        /// </summary>
        private static GameObject BuildMonsterPrefab<TAI, TAnimator>(string name, string path, float radius, float height, System.Action<Transform> buildRig)
            where TAI : Component where TAnimator : Component
        {
            GameObject root = new GameObject(name);
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<TAI>();

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.radius = radius;
            collider.height = height;
            collider.center = new Vector3(0f, height * 0.5f, 0f);

            GameObject rig = new GameObject("VisualRig");
            rig.transform.SetParent(root.transform, false);
            buildRig(rig.transform);
            rig.AddComponent<TAnimator>();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject BuildDarknessPrefab()
            => BuildMonsterPrefab<DarknessAI, DarknessAnimator>("Darkness", DarknessPrefabPath, 0.6f, 1.8f, DarknessBodyBuilder.Build);

        private static GameObject BuildKitsunePrefab()
            => BuildMonsterPrefab<KitsuneAI, KitsuneAnimator>("Kitsune", KitsunePrefabPath, 0.35f, 1.7f, KitsuneBodyBuilder.Build);

        private static GameObject BuildMimicPrefab()
            => BuildMonsterPrefab<MimicAI, MimicAnimator>("Mimic", MimicPrefabPath, 0.4f, 2.1f, MimicBodyBuilder.Build);

        private static void BuildSandbagWall(Transform parent, Vector3 pos, float yaw, AbandonedMaterials ab)
        {
            GameObject g = new GameObject("SandbagBarricade");
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            for (int row = 0; row < 2; row++)
            {
                for (int col = -1; col <= 1; col++)
                {
                    CreatePart(g.transform, $"Bag_{row}_{col}",
                        new Vector3(col * 0.72f + (row * 0.2f), 0.18f + row * 0.28f, 0f),
                        new Vector3(0.7f, 0.28f, 0.42f), ab.Sandbag, true);
                }
            }
        }

        private static void BuildWorldScene(
            ProceduralMaterialLibrary mats,
            AbandonedMaterials abandonedMats,
            Mesh rockMesh,
            Material skyMat,
            GameObject playerPrefab,
            GameObject barricadePrefab,
            GameObject doorPrefab,
            GameObject campfirePrefab,
            GameObject listenerPrefab,
            GameObject darknessPrefab,
            GameObject kitsunePrefab,
            GameObject mimicPrefab,
            GameObject treePrefab,
            GameObject sakuraPrefab,
            GameObject boulderPrefab,
            GameObject carPrefab,
            GameObject[] carPrefabs,
            GameObject woodPickup,
            GameObject stonePickup,
            GameObject metalPickup,
            GameObject clothPickup,
            GameObject fuelPickup,
            GameObject medPickup,
            GameObject ammoPickup,
            GameObject batteryPickup,
            GameObject planksPickup,
            GameObject arrowPickup)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject sunObj = new GameObject("Sun Directional Light");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.0f;
            sun.shadows = LightShadows.Soft;
            // Тёплый закат (в игре тон ведёт DayNightCycle, здесь - вид сцены в редакторе).
            sun.color = new Color(1f, 0.58f, 0.30f);
            sunObj.transform.rotation = Quaternion.Euler(9f, -38f, 0f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.42f, 0.27f, 0.21f);
            RenderSettings.fogDensity = 0.016f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.16f, 0.14f);

            BuildTerrain(mats);

            BuildForestCamp(mats, abandonedMats, barricadePrefab, doorPrefab, campfirePrefab);
            BuildCityDistrict(mats, abandonedMats, doorPrefab);
            BuildEvacuationZone(mats);
            ChapelBuilder.Build(mats, abandonedMats, doorPrefab);
            BuildIndustrialYard(mats, abandonedMats, doorPrefab);
            BuildAbandonedFarm(mats, abandonedMats, doorPrefab);

            float spanX = WorldLayout.BoundX * 2f + 10f;
            float spanZ = (WorldLayout.BoundMaxZ - WorldLayout.BoundMinZ) + 10f;
            float midZ = (WorldLayout.BoundMinZ + WorldLayout.BoundMaxZ) * 0.5f;
            CreateBoundaryCollider("WorldBound_North", new Vector3(0f, 4f, WorldLayout.BoundMaxZ), new Vector3(spanX, 8f, 2f));
            CreateBoundaryCollider("WorldBound_South", new Vector3(0f, 4f, WorldLayout.BoundMinZ), new Vector3(spanX, 8f, 2f));
            CreateBoundaryCollider("WorldBound_East", new Vector3(WorldLayout.BoundX, 4f, midZ), new Vector3(2f, 8f, spanZ));
            CreateBoundaryCollider("WorldBound_West", new Vector3(-WorldLayout.BoundX, 4f, midZ), new Vector3(2f, 8f, spanZ));

            // Старые процедурные монстры (Listener, Darkness, Kitsune, Mimic) убраны со сцены.
            // По всей карте разбросаны 3 зомби (Scary Zombie Pack): юго-западный лес, город и восточные окраины.
            if (listenerPrefab != null)
            {
                Terrain sceneTerrain = Object.FindFirstObjectByType<Terrain>();
                // 20 зомби, равномерно разбросанных по всей карте (лес, город, окраины, промзона, ферма, часовня).
                Vector3[] zombieSpots =
                {
                    new Vector3(-78f, 0f, -68f),  new Vector3(-145f, 0f, -30f), new Vector3(-190f, 0f, -125f),
                    new Vector3(-55f, 0f, -155f), new Vector3(48f, 0f, -88f),   new Vector3(128f, 0f, -135f),
                    new Vector3(115f, 0f, -25f),  new Vector3(210f, 0f, -55f),  new Vector3(185f, 0f, 35f),
                    new Vector3(-125f, 0f, 38f),  new Vector3(-220f, 0f, 55f),  new Vector3(18f, 0f, 95f),
                    new Vector3(-38f, 0f, 126f),  new Vector3(54f, 0f, 134f),   new Vector3(-92f, 0f, 158f),
                    new Vector3(112f, 0f, 168f),  new Vector3(8f, 0f, 198f),    new Vector3(-142f, 0f, 212f),
                    new Vector3(148f, 0f, 215f),  new Vector3(235f, 0f, 120f)
                };

                for (int i = 0; i < zombieSpots.Length; i++)
                {
                    Vector3 pos = zombieSpots[i];
                    if (sceneTerrain != null) pos.y = sceneTerrain.SampleHeight(pos);
                    GameObject z = (GameObject)PrefabUtility.InstantiatePrefab(listenerPrefab);
                    z.name = $"Zombie_{i + 1:00}";
                    z.transform.position = pos;
                    z.transform.rotation = Quaternion.Euler(0f, (i * 67f) % 360f, 0f);
                }
            }

            if (sakuraPrefab != null)
            {
                GameObject campSakura = (GameObject)PrefabUtility.InstantiatePrefab(sakuraPrefab);
                campSakura.name = "Sakura_CampLandmark";
                campSakura.transform.position = new Vector3(18f, 0f, 17f);
                campSakura.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
                campSakura.transform.localScale = Vector3.one * 1.15f;

                GameObject groveSakura = (GameObject)PrefabUtility.InstantiatePrefab(sakuraPrefab);
                groveSakura.name = "Sakura_ForestGrove";
                groveSakura.transform.position = new Vector3(-23f, 0f, -18f);
                groveSakura.transform.rotation = Quaternion.Euler(0f, -120f, 0f);
                groveSakura.transform.localScale = Vector3.one * 1.25f;
            }

            BuildCatalogSetup.Create(mats, abandonedMats);

            GameObject worldSystems = new GameObject("WorldSystems");
            worldSystems.AddComponent<NetworkObject>();
            worldSystems.AddComponent<WorldSpawnGuard>();
            worldSystems.AddComponent<DoorNetSync>(); // синхронизация межкомнатных дверей
            worldSystems.AddComponent<WindowNetSync>(); // синхронизация разбитых окон высоток
            DayNightCycle dayNight = worldSystems.AddComponent<DayNightCycle>();
            SetReference(dayNight, "sunLight", sun);
            SetReference(dayNight, "skyboxMaterial", skyMat);
            SetFloat(dayNight, "startNormalizedTime", 0.62f); // начало игры - золотой час / закат

            RuntimeWorldGenerator worldGen = worldSystems.AddComponent<RuntimeWorldGenerator>();
            SetReference(worldGen, "treePrefab", treePrefab);
            SetReference(worldGen, "sakuraPrefab", sakuraPrefab);
            GameObject birchPrefab = ForestTreesBuilder.BuildBirchPrefab();
            GameObject oakPrefab = ForestTreesBuilder.BuildOakPrefab(mats);
            GameObject deadTreePrefab = ForestTreesBuilder.BuildDeadTreePrefab(mats);
            SetReferences(worldGen, "extraTreePrefabs", new Object[] { birchPrefab, oakPrefab, deadTreePrefab });
            SetReference(worldGen, "boulderPrefab", boulderPrefab);
            SetReference(worldGen, "carPrefab", carPrefab);
            SetReferences(worldGen, "carPrefabs", carPrefabs);
            // Порядок = порядок весов в RuntimeWorldGenerator.
            SetReferences(worldGen, "forestPickupPrefabs", new Object[] { woodPickup, stonePickup, planksPickup, clothPickup, arrowPickup });
            SetReferences(worldGen, "cityPickupPrefabs", new Object[] { metalPickup, clothPickup, fuelPickup, medPickup, ammoPickup, batteryPickup, arrowPickup, planksPickup });

            GameObject network = new GameObject("Network");
            NetworkManager manager = network.AddComponent<NetworkManager>();
            UnityTransport transport = network.AddComponent<UnityTransport>();
            GameSession session = network.AddComponent<GameSession>();
            if (manager.NetworkConfig == null) manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;

            GameObject hudObject = new GameObject("Debug HUD");
            DebugConnectionHud hud = hudObject.AddComponent<DebugConnectionHud>();
            SetReference(hud, "session", session);
            hudObject.AddComponent<Ogni.UI.DebugPromptView>();

            EditorSceneManager.SaveScene(scene, WorldScenePath);
            RevalidateNetworkObjects();
        }

        /// <summary>
        /// NetworkObject, добавленные скриптом, получают GlobalObjectIdHash только после валидации
        /// сохранённой сцены. Переоткрываем сцену, помечаем объекты и сохраняем ещё раз.
        /// </summary>
        private static void RevalidateNetworkObjects()
        {
            AssetDatabase.ImportAsset(WorldScenePath, ImportAssetOptions.ForceUpdate);
            var reopened = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
            foreach (NetworkObject no in Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(no);
                SerializedProperty p = so.FindProperty("GlobalObjectIdHash") ?? so.FindProperty("m_GlobalObjectIdHash");
                if (p != null && p.propertyType == SerializedPropertyType.Integer && p.longValue == 0)
                {
                    Debug.LogWarning("[Ogni] GlobalObjectIdHash = 0 у " + no.name + " (WorldSpawnGuard подстрахует спавн при старте).");
                }

                EditorUtility.SetDirty(no);
            }

            EditorSceneManager.MarkSceneDirty(reopened);
            EditorSceneManager.SaveScene(reopened);
        }

        private static Terrain BuildTerrain(ProceduralMaterialLibrary mats)
        {
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            const int hmRes = WorldLayout.HeightmapRes;
            const int amRes = WorldLayout.AlphamapRes;
            const float sizeX = WorldLayout.TerrainSizeX;
            const float sizeZ = WorldLayout.TerrainSizeZ;
            const float maxH = WorldLayout.MaxHeight;

            data.heightmapResolution = hmRes;
            data.size = new Vector3(sizeX, maxH, sizeZ);

            float[,] heights = new float[hmRes, hmRes];
            for (int y = 0; y < hmRes; y++)
            {
                for (int x = 0; x < hmRes; x++)
                {
                    float wx = (x / (float)(hmRes - 1)) * sizeX + WorldLayout.TerrainOriginX;
                    float wz = (y / (float)(hmRes - 1)) * sizeZ + WorldLayout.TerrainOriginZ;
                    heights[y, x] = WorldLayout.TerrainHeight01(wx, wz, 200f, 200f);
                }
            }

            data.SetHeights(0, 0, heights);

            TerrainLayer grassLayer = CreateTerrainLayer("Layer_Forest", mats.ForestGroundTex, mats.ForestGroundNormal, 10f);
            TerrainLayer dirtLayer = CreateTerrainLayer("Layer_Dirt", mats.DirtRoadTex, mats.DirtRoadNormal, 8f);
            TerrainLayer asphaltLayer = CreateTerrainLayer("Layer_Asphalt", mats.AsphaltTex, mats.AsphaltNormal, 8f);
            data.terrainLayers = new[] { grassLayer, dirtLayer, asphaltLayer };

            data.alphamapResolution = amRes;
            float[,,] splat = new float[amRes, amRes, 3];
            for (int y = 0; y < amRes; y++)
            {
                for (int x = 0; x < amRes; x++)
                {
                    float wx = (x / (float)(amRes - 1)) * sizeX + WorldLayout.TerrainOriginX;
                    float wz = (y / (float)(amRes - 1)) * sizeZ + WorldLayout.TerrainOriginZ;

                    float asphalt = 0f;
                    if (wz >= 55f && wz <= 245f && Mathf.Abs(wx) <= 8f) asphalt = 1f;
                    if (wz >= 55f && wz <= 245f && Mathf.Abs(Mathf.Abs(wx) - 68f) <= 4.5f) asphalt = 1f;
                    if (wz >= 55f && wz <= 245f && Mathf.Abs(Mathf.Abs(wx) - 104f) <= 4.5f) asphalt = 1f;
                    if (wz >= 55f && wz <= 245f && Mathf.Abs(Mathf.Abs(wx) - 140f) <= 4.5f) asphalt = 1f;
                    if (wz >= 55f && wz <= 63f && Mathf.Abs(wx) <= 150f) asphalt = 1f;
                    if (wz >= 85f && wz <= 101f && Mathf.Abs(wx) <= 150f) asphalt = 1f;
                    if (wz >= 125f && wz <= 139f && Mathf.Abs(wx) <= 150f) asphalt = 1f;
                    if (wz >= 146f && wz <= 154f && Mathf.Abs(wx) <= 150f) asphalt = 1f;
                    if (wz >= 175f && wz <= 190f && Mathf.Abs(wx) <= 150f) asphalt = 1f;
                    if (WorldLayout.InYardApron(wx, wz)) asphalt = 1f;

                    float dirt = 0f;
                    if (asphalt < 0.5f)
                    {
                        if (wz >= 6f && wz <= 60f && Mathf.Abs(wx) <= 3.8f)
                        {
                            dirt = 1f - Mathf.Clamp01((Mathf.Abs(wx) - 2.3f) / 1.5f);
                        }

                        float trackDist = WorldLayout.DistanceToTracks(wx, wz);
                        float trackDirt = 1f - Mathf.Clamp01((trackDist - 2.3f) / 1.5f);
                        dirt = Mathf.Max(dirt, trackDirt);
                    }

                    float grass = Mathf.Clamp01(1f - Mathf.Max(asphalt, dirt));
                    splat[y, x, 0] = grass;
                    splat[y, x, 1] = dirt;
                    splat[y, x, 2] = asphalt;
                }
            }

            data.SetAlphamaps(0, 0, splat);
            EditorUtility.SetDirty(data);

            GameObject terrainObj = Terrain.CreateTerrainGameObject(data);
            terrainObj.name = "Terrain";
            terrainObj.transform.position = new Vector3(WorldLayout.TerrainOriginX, 0f, WorldLayout.TerrainOriginZ);
            return terrainObj.GetComponent<Terrain>();
        }

        private static TerrainLayer CreateTerrainLayer(string name, Texture2D diffuse, Texture2D normal, float tileSize)
        {
            string path = $"Assets/_Project/Art/{name}.terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }

            layer.diffuseTexture = diffuse;
            layer.normalMapTexture = normal;
            layer.tileSize = new Vector2(tileSize, tileSize);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static void BuildForestCamp(
            ProceduralMaterialLibrary mats,
            AbandonedMaterials ab,
            GameObject barricadePrefab,
            GameObject doorPrefab,
            GameObject campfirePrefab)
        {
            GameObject camp = new GameObject("ForestCamp");

            GameObject fire = (GameObject)PrefabUtility.InstantiatePrefab(campfirePrefab);
            fire.transform.SetParent(camp.transform, false);
            fire.transform.position = new Vector3(0f, 0f, 2.5f);

            SpawnCabinBuilder.Build(camp.transform, new Vector3(-9.5f, 0f, -6.5f), mats, ab, doorPrefab);

            // Ящик, генератор и три баррикады вокруг домика убраны: они загораживали подход к дому у спавна.
            // Баррикады игрок строит сам (B), поэтому готовые заграждения в лагере не нужны.
        }

        private static Mesh _pineTrunkMesh;

        /// <summary>
        /// Ствол сосны: профиль с частыми кольцами у «рабочей» высоты ~1 м (в этом месте TreeResource выпиливает клин),
        /// шов с дублированными вершинами и UV по реальным размерам - кора не тянется и без полосы на стыке.
        /// </summary>
        private static Mesh BuildPineTrunkMesh()
        {
            Vector2[] prof =
            {
                new Vector2(0.36f, 0.00f), new Vector2(0.33f, 0.12f), new Vector2(0.27f, 0.45f), new Vector2(0.24f, 1.20f),
                new Vector2(0.22f, 2.20f), new Vector2(0.19f, 3.30f), new Vector2(0.15f, 4.30f), new Vector2(0.10f, 5.10f),
                new Vector2(0.05f, 5.60f), new Vector2(0.00f, 5.70f)
            };
            System.Collections.Generic.List<float> ys = new System.Collections.Generic.List<float> { 0f, 0.06f, 0.12f, 0.3f, 0.45f };
            for (float y = 0.55f; y <= 1.651f; y += 0.05f) ys.Add(y);
            ys.AddRange(new[] { 1.9f, 2.2f, 2.6f, 3.0f, 3.3f, 3.8f, 4.3f, 4.7f, 5.1f, 5.4f, 5.6f, 5.7f });

            const int sides = 24;
            int rings = ys.Count;
            int row = sides + 1;
            Vector3[] verts = new Vector3[rings * row];
            Vector2[] uvs = new Vector2[rings * row];
            for (int p = 0; p < rings; p++)
            {
                float y = ys[p];
                float r = 0f;
                for (int i = 0; i < prof.Length - 1; i++)
                {
                    if (y >= prof[i].y && y <= prof[i + 1].y)
                    {
                        float k = (y - prof[i].y) / Mathf.Max(0.0001f, prof[i + 1].y - prof[i].y);
                        r = Mathf.Lerp(prof[i].x, prof[i + 1].x, k);
                        break;
                    }
                }

                for (int s = 0; s <= sides; s++)
                {
                    float a = (s % sides) / (float)sides * Mathf.PI * 2f;
                    verts[p * row + s] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    uvs[p * row + s] = new Vector2(s / (float)sides, y / 5.7f);
                }
            }

            int[] tris = new int[(rings - 1) * sides * 6];
            int ti = 0;
            for (int p = 0; p < rings - 1; p++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = p * row + s, b = p * row + s + 1, c = (p + 1) * row + s, d = (p + 1) * row + s + 1;
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                    tris[ti++] = b; tris[ti++] = d; tris[ti++] = c;
                }
            }

            Mesh mesh = new Mesh { name = "PineTrunk" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildPineTree(Transform parent, string name, Vector3 pos, float scale, float yaw, ProceduralMaterialLibrary mats, Mesh pineCrownMesh)
        {
            GameObject tree = new GameObject(name);
            tree.transform.SetParent(parent, false);
            tree.transform.position = pos;
            tree.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            tree.transform.localScale = Vector3.one * scale;

            // Ствол - один цельный сужающийся меш с расширением у корней (имя TrunkBase используется TreeResource для пня).
            if (_pineTrunkMesh == null)
            {
                _pineTrunkMesh = MeshKit.SaveOrUpdate(BuildPineTrunkMesh(), "PineTrunk");
            }

            Mesh trunkMesh = _pineTrunkMesh;
            GameObject trunk = CreateMeshPart(tree.transform, "TrunkBase", Vector3.zero, Vector3.one, trunkMesh, mats.TreeBark, false);
            CapsuleCollider trunkCol = trunk.AddComponent<CapsuleCollider>();
            trunkCol.radius = 0.33f;
            trunkCol.height = 3.4f;
            trunkCol.center = new Vector3(0f, 1.7f, 0f);

            // Контрфорсные корни у земли.
            for (int r = 0; r < 6; r++)
            {
                float a = r * 60f + 17f;
                float rad = a * Mathf.Deg2Rad;
                GameObject rootPart = CreatePart(tree.transform, $"Root_{r}",
                    new Vector3(Mathf.Cos(rad) * 0.30f, 0.12f, Mathf.Sin(rad) * 0.30f),
                    new Vector3(0.15f - (r % 2) * 0.04f, 0.36f, 0.15f - (r % 2) * 0.04f), mats.TreeBark, false, PrimitiveType.Cylinder);
                rootPart.transform.localRotation = Quaternion.Euler(Mathf.Sin(rad) * 62f, 0f, -Mathf.Cos(rad) * 62f);
            }

            // Сухие нижние сучья.
            for (int b = 0; b < 5; b++)
            {
                float brad = (b * 74f + 12f) * Mathf.Deg2Rad;
                float y = 1.5f + b * 0.38f;
                GameObject dryBranch = CreatePart(tree.transform, $"DryBranch_{b}",
                    new Vector3(Mathf.Cos(brad) * 0.42f, y, Mathf.Sin(brad) * 0.42f),
                    new Vector3(0.06f - b * 0.005f, 0.42f - b * 0.03f, 0.06f - b * 0.005f), mats.TreeBark, false, PrimitiveType.Cylinder);
                dryBranch.transform.localRotation = Quaternion.Euler(Mathf.Sin(brad) * 72f, 0f, -Mathf.Cos(brad) * 72f);
            }

            // Круглый «наплыв» на стволе убран: на дереве он читался как странный шар.

            // 7 ярусов кроны с разной ротацией и лёгким смещением, чтобы силуэт не был идеальной ёлочкой.
            for (int layer = 0; layer < 7; layer++)
            {
                float y = 2.2f + layer * 0.52f;
                float w = 2.3f - layer * 0.30f;
                float h = 1.0f - layer * 0.05f;
                GameObject crown = CreateMeshPart(tree.transform, $"Crown_{layer}",
                    new Vector3(((layer * 37) % 5 - 2) * 0.025f, y, ((layer * 53) % 5 - 2) * 0.025f),
                    new Vector3(w, h, w), pineCrownMesh, mats.PineNeedles, false);
                crown.transform.localRotation = Quaternion.Euler(((layer * 7) % 5 - 2) * 1.2f, layer * 43f, ((layer * 11) % 5 - 2) * 1.2f);
            }
        }

        private static void BuildCityDistrict(ProceduralMaterialLibrary mats, AbandonedMaterials abandonedMats, GameObject doorPrefab)
        {
            ApartmentBlockBuilder.ResetDoorIds();
            GlassTowerBuilder.ResetWindowIds();
            _trimMat = mats.Concrete;
            _frameMat = mats.WoodPlanks;
            _floorMat = mats.Concrete; // внутри малоэтажных зданий бетонный пол вместо кирпичного
            GameObject city = new GameObject("AbandonedCity");

            // Магазин и заправка на первом перекрёстке.
            BuildBuilding(city.transform, "City_Shop", new Vector3(-16f, 0f, 70f), new Vector3(10f, 3.6f, 9f),
                mats.Brick, mats.Concrete, mats.DarkGlass, doorPrefab, true, 90f);

            BuildBuilding(city.transform, "City_GasStationKiosk", new Vector3(17f, 0f, 70f), new Vector3(7f, 3.2f, 6f),
                mats.Brick, mats.Concrete, mats.DarkGlass, doorPrefab, false, -90f);
            CreatePart(city.transform, "GasCanopy_Roof", new Vector3(11f, 3.4f, 70f), new Vector3(6f, 0.3f, 8f), mats.RustyMetal, true);
            CreatePart(city.transform, "GasCanopy_PillarA", new Vector3(11f, 1.7f, 67f), new Vector3(0.35f, 3.4f, 0.35f), mats.Concrete, true);
            CreatePart(city.transform, "GasCanopy_PillarB", new Vector3(11f, 1.7f, 73f), new Vector3(0.35f, 3.4f, 0.35f), mats.Concrete, true);
            CreatePart(city.transform, "FuelPump_1", new Vector3(11f, 0.7f, 69f), new Vector3(0.6f, 1.4f, 0.5f), mats.RustyMetal, true);
            CreatePart(city.transform, "FuelPump_2", new Vector3(11f, 0.7f, 71f), new Vector3(0.6f, 1.4f, 0.5f), mats.RustyMetal, true);

            // Склад и медпункт.
            BuildBuilding(city.transform, "City_Warehouse", new Vector3(-17f, 0f, 110f), new Vector3(13f, 4.5f, 12f),
                mats.Concrete, mats.RustyMetal, mats.DarkGlass, doorPrefab, false, 90f);

            BuildBuilding(city.transform, "City_Clinic", new Vector3(17f, 0f, 110f), new Vector3(11f, 3.8f, 10f),
                mats.Concrete, mats.Concrete, mats.DarkGlass, doorPrefab, true, -90f);

            // Заброшенные панельные дома с подъездами, лестницами и квартирами.
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_West", new Vector3(-28f, 0f, 138f), 90f, 4, 1001, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_East", new Vector3(28f, 0f, 138f), -90f, 4, 2002, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_North", new Vector3(0f, 0f, 212f), 180f, 6, 3003, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_SideW", new Vector3(-42f, 0f, 92f), 0f, 4, 4004, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_SideE", new Vector3(42f, 0f, 92f), 180f, 5, 5005, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_DeepW", new Vector3(-34f, 0f, 184f), 90f, 7, 6006, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_DeepE", new Vector3(34f, 0f, 184f), -90f, 6, 7007, mats, abandonedMats, doorPrefab);

            // Дальние кварталы: плотная застройка вдоль новых улиц.
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_FarW1", new Vector3(-86f, 0f, 76f), 90f, 5, 8008, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_FarE1", new Vector3(86f, 0f, 76f), -90f, 5, 9009, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_FarW2", new Vector3(-86f, 0f, 112f), 90f, 6, 1010, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_FarE2", new Vector3(86f, 0f, 112f), -90f, 6, 1111, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_FarW3", new Vector3(-86f, 0f, 164f), 90f, 7, 1212, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_FarE3", new Vector3(86f, 0f, 164f), -90f, 7, 1313, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_FarW4", new Vector3(-86f, 0f, 202f), 90f, 8, 1414, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_FarE4", new Vector3(86f, 0f, 202f), -90f, 8, 1515, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_EdgeW1", new Vector3(-122f, 0f, 112f), 90f, 6, 1616, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_EdgeE1", new Vector3(122f, 0f, 112f), -90f, 6, 1717, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_EdgeW2", new Vector3(-122f, 0f, 164f), 90f, 7, 1818, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_EdgeE2", new Vector3(122f, 0f, 164f), -90f, 7, 1919, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_EdgeW3", new Vector3(-122f, 0f, 202f), 90f, 7, 2020, mats, abandonedMats, doorPrefab);
            ApartmentBlockBuilder.Build(city.transform, "PanelBlock_EdgeE3", new Vector3(122f, 0f, 202f), -90f, 7, 2121, mats, abandonedMats, doorPrefab);

            // Малоэтажные дома с дворами между панельными кварталами.
            BuildHouseWithYard(city.transform, "City_House_SW1", new Vector3(-56f, 0f, 74f), 90f, mats, abandonedMats, doorPrefab);
            BuildHouseWithYard(city.transform, "City_House_SE1", new Vector3(56f, 0f, 74f), -90f, mats, abandonedMats, doorPrefab);
            BuildHouseWithYard(city.transform, "City_House_SW2", new Vector3(-56f, 0f, 113f), 90f, mats, abandonedMats, doorPrefab);
            BuildHouseWithYard(city.transform, "City_House_SE2", new Vector3(56f, 0f, 113f), -90f, mats, abandonedMats, doorPrefab);
            BuildHouseWithYard(city.transform, "City_House_SW3", new Vector3(-56f, 0f, 164f), 90f, mats, abandonedMats, doorPrefab);
            BuildHouseWithYard(city.transform, "City_House_SE3", new Vector3(56f, 0f, 164f), -90f, mats, abandonedMats, doorPrefab);
            BuildHouseWithYard(city.transform, "City_House_SW4", new Vector3(-56f, 0f, 220f), 90f, mats, abandonedMats, doorPrefab);
            BuildHouseWithYard(city.transform, "City_House_SE4", new Vector3(56f, 0f, 220f), -90f, mats, abandonedMats, doorPrefab);

            // Высотки на горизонте и новые высотки из WorldLayout.ExtraTowers.
            BuildHighRiseBuilding(city.transform, "HighRise_W", new Vector3(-150f, 0f, 160f), 9, mats, abandonedMats, doorPrefab, 0f);
            BuildHighRiseBuilding(city.transform, "HighRise_E", new Vector3(150f, 0f, 160f), 9, mats, abandonedMats, doorPrefab, 0f);
            BuildHighRiseBuilding(city.transform, "HighRise_NW", new Vector3(-150f, 0f, 225f), 10, mats, abandonedMats, doorPrefab, 0f);
            BuildHighRiseBuilding(city.transform, "HighRise_NE", new Vector3(150f, 0f, 225f), 10, mats, abandonedMats, doorPrefab, 0f);
            BuildHighRiseBuilding(city.transform, "HighRise_N", new Vector3(0f, 0f, 244f), 12, mats, abandonedMats, doorPrefab, 180f);

            foreach (WorldLayout.TowerSite site in WorldLayout.ExtraTowers)
            {
                BuildHighRiseBuilding(city.transform, site.Name, new Vector3(site.Pos.x, 0f, site.Pos.y),
                    site.Floors, mats, abandonedMats, doorPrefab, site.Yaw, site.Variant);
            }

            // Дополнительные дома с мебелью внутри.
            BuildBuilding(city.transform, "City_HouseNorthW", new Vector3(-20f, 0f, 158f), new Vector3(9f, 3.4f, 8f),
                mats.Brick, mats.Concrete, mats.DarkGlass, doorPrefab, true, 90f);
            CreatePart(city.transform, "HouseW_Table", new Vector3(-21f, 0.4f, 158f), new Vector3(1.2f, 0.8f, 0.9f), abandonedMats.WoodDark, true);
            CreatePart(city.transform, "HouseW_Bed", new Vector3(-22.5f, 0.3f, 160f), new Vector3(1.1f, 0.5f, 2.0f), abandonedMats.Fabric, true);

            BuildBuilding(city.transform, "City_AutoService", new Vector3(20f, 0f, 158f), new Vector3(11f, 3.8f, 9f),
                mats.Concrete, mats.RustyMetal, mats.DarkGlass, doorPrefab, false, -90f);
            CreatePart(city.transform, "Auto_Rack", new Vector3(22f, 1.0f, 158f), new Vector3(0.8f, 2.0f, 3.2f), mats.RustyMetal, true);



            for (int i = 0; i < 6; i++)
            {
                float z = 60f + i * 16f;
                CreatePart(city.transform, $"StreetPole_L_{i}", new Vector3(-7.2f, 2.3f, z), new Vector3(0.18f, 4.6f, 0.18f), mats.RustyMetal, true, PrimitiveType.Cylinder);
                CreatePart(city.transform, $"StreetPole_R_{i}", new Vector3(7.2f, 2.3f, z), new Vector3(0.18f, 4.6f, 0.18f), mats.RustyMetal, true, PrimitiveType.Cylinder);
            }

            // Эффект заброшенности на улицах: блокпосты из мешков с песком, бетонные блоки, бочки, мох и трещины на асфальте.
            BuildSandbagWall(city.transform, new Vector3(-2.5f, 0f, 68f), 12f, abandonedMats);
            BuildSandbagWall(city.transform, new Vector3(2.8f, 0f, 118f), -18f, abandonedMats);
            CreatePart(city.transform, "RoadBlock_1", new Vector3(3.5f, 0.45f, 75f), new Vector3(2.4f, 0.9f, 0.65f), mats.Concrete, true);
            CreatePart(city.transform, "RoadBlock_2", new Vector3(-3.8f, 0.45f, 106f), new Vector3(2.4f, 0.9f, 0.65f), mats.Concrete, true);
            CreatePart(city.transform, "Dumpster_1", new Vector3(-11f, 0.65f, 80f), new Vector3(1.8f, 1.3f, 1.2f), abandonedMats.MetalPainted, true);
            CreatePart(city.transform, "Dumpster_2", new Vector3(11f, 0.65f, 100f), new Vector3(1.8f, 1.3f, 1.2f), abandonedMats.MetalPainted, true);
            CreatePart(city.transform, "Barrel_1", new Vector3(-9.5f, 0.48f, 78f), new Vector3(0.58f, 0.95f, 0.58f), mats.RustyMetal, true, PrimitiveType.Cylinder);
            CreatePart(city.transform, "Barrel_2", new Vector3(9.5f, 0.48f, 102f), new Vector3(0.58f, 0.95f, 0.58f), mats.RustyMetal, true, PrimitiveType.Cylinder);

            for (int i = 0; i < 14; i++)
            {
                float dx = (i % 2 == 0 ? -1f : 1f) * ((i * 1.7f) % 5.5f);
                float dz = 60f + i * 6.5f;
                Material decal = i % 2 == 0 ? abandonedMats.MossDecal : abandonedMats.CrackDecal;
                ApartmentBlockBuilder.Decal(city.transform, decal, new Vector3(dx, 0.04f, dz), Vector3.up, new Vector2(3.2f, 3.2f), i * 47f);
            }

            // Наполнение магазина, склада и медпункта внутри.
            CreatePart(city.transform, "Shop_Counter", new Vector3(-17.5f, 0.5f, 70f), new Vector3(0.8f, 1.0f, 4.5f), abandonedMats.WoodDark, true);
            CreatePart(city.transform, "Shop_Shelf", new Vector3(-19f, 1.0f, 67.5f), new Vector3(0.6f, 2.0f, 3.5f), abandonedMats.WoodDark, true);
            CreatePart(city.transform, "Warehouse_Crate1", new Vector3(-18f, 0.6f, 112f), new Vector3(1.5f, 1.2f, 1.5f), mats.WoodPlanks, true);
            CreatePart(city.transform, "Warehouse_Crate2", new Vector3(-15f, 0.5f, 107f), new Vector3(1.2f, 1.0f, 1.2f), mats.WoodPlanks, true);
            CreatePart(city.transform, "Clinic_Bed1", new Vector3(18.5f, 0.35f, 108f), new Vector3(2.0f, 0.5f, 1.0f), abandonedMats.Fabric, true);
            CreatePart(city.transform, "Clinic_Bed2", new Vector3(18.5f, 0.35f, 112f), new Vector3(2.0f, 0.5f, 1.0f), abandonedMats.Fabric, true);

            // Шаг 3: лужи, растительность, остовы машин, столбы, остановки, силуэты башен.
            CityDetailBuilder.Build(city.transform, mats, abandonedMats);
        }

        private static void BuildHouseWithYard(
            Transform parent, string name, Vector3 pos, float yaw,
            ProceduralMaterialLibrary mats, AbandonedMaterials ab, GameObject doorPrefab)
        {
            GameObject yard = new GameObject(name + "_Yard");
            yard.transform.SetParent(parent, false);
            yard.transform.position = pos;
            yard.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            BuildBuilding(yard.transform, "House", Vector3.zero, new Vector3(9f, 3.4f, 8f),
                mats.Brick, mats.Concrete, mats.DarkGlass, doorPrefab, true, 0f);

            // Площадка вокруг дома расчищена от забора, сарая, бочек и поленницы.

            for (int b = 0; b < 4; b++)
            {
                GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bush.name = "YardBush_" + b;
                bush.transform.SetParent(yard.transform, false);
                bush.transform.localPosition = new Vector3(-5.5f + b * 3.4f, 0.32f, -5.2f);
                bush.transform.localScale = new Vector3(1.2f, 0.7f, 1.2f);
                bush.GetComponent<Renderer>().sharedMaterial = mats.BushFoliage;
                Object.DestroyImmediate(bush.GetComponent<Collider>());
            }

            ApartmentBlockBuilder.Decal(yard.transform, ab.MossDecal, new Vector3(0f, 0.03f, 2f), Vector3.up, new Vector2(5f, 4f), 0f);
        }

        /// <summary>Стеклянная высотка с разбиваемыми окнами (см. <see cref="GlassTowerBuilder"/>).</summary>
        private static void BuildHighRiseBuilding(
            Transform parent, string name, Vector3 pos, int floors,
            ProceduralMaterialLibrary mats, AbandonedMaterials ab, GameObject doorPrefab, float yaw, int variant = 0)
        {
            GlassTowerBuilder.Build(parent, name, pos, yaw, floors, 9100 + Mathf.RoundToInt(pos.x * 3f + pos.z), mats, ab, doorPrefab, variant);
        }

        private static void BuildCar(Transform parent, string name, Vector3 pos, float yaw, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            GameObject car = new GameObject(name);
            car.transform.SetParent(parent, false);
            car.transform.position = pos;
            car.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Нижняя рама, капот, салон, багажник, бамперы и фары.
            CreatePart(car.transform, "Chassis", new Vector3(0f, 0.42f, 0f), new Vector3(1.9f, 0.35f, 4.3f), mats.RustyMetal, true);
            CreatePart(car.transform, "Hood", new Vector3(0f, 0.72f, 1.25f), new Vector3(1.82f, 0.28f, 1.5f), mats.RustyMetal, true);
            CreatePart(car.transform, "Trunk", new Vector3(0f, 0.74f, -1.45f), new Vector3(1.82f, 0.30f, 1.1f), mats.RustyMetal, true);
            CreatePart(car.transform, "Roof", new Vector3(0f, 1.38f, -0.15f), new Vector3(1.62f, 0.12f, 1.85f), mats.RustyMetal, true);

            // Обшивка потолка: куб крыши изнутри невидим (грани отсекаются) - клеим плоскость, смотрящую вниз.
            GameObject liner = GameObject.CreatePrimitive(PrimitiveType.Quad);
            liner.name = "RoofLiner";
            Object.DestroyImmediate(liner.GetComponent<Collider>());
            liner.transform.SetParent(car.transform, false);
            liner.transform.localPosition = new Vector3(0f, 1.318f, -0.15f);
            liner.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            liner.transform.localScale = new Vector3(1.6f, 1.83f, 1f);
            liner.GetComponent<Renderer>().sharedMaterial = ab.Fabric;

            // Стойки крыши.
            CreatePart(car.transform, "Pillar_FL", new Vector3(-0.74f, 1.08f, 0.62f), new Vector3(0.10f, 0.56f, 0.10f), mats.RustyMetal, false);
            CreatePart(car.transform, "Pillar_FR", new Vector3(0.74f, 1.08f, 0.62f), new Vector3(0.10f, 0.56f, 0.10f), mats.RustyMetal, false);
            CreatePart(car.transform, "Pillar_BL", new Vector3(-0.74f, 1.08f, -0.95f), new Vector3(0.10f, 0.56f, 0.10f), mats.RustyMetal, false);
            CreatePart(car.transform, "Pillar_BR", new Vector3(0.74f, 1.08f, -0.95f), new Vector3(0.10f, 0.56f, 0.10f), mats.RustyMetal, false);

            // Лобовое и заднее стекло, бамперы, фары.
            GameObject ws = CreatePart(car.transform, "Windshield", new Vector3(0f, 1.08f, 0.65f), new Vector3(1.42f, 0.48f, 0.06f), mats.DarkGlass, false);
            ws.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
            CreatePart(car.transform, "FrontBumper", new Vector3(0f, 0.38f, 2.18f), new Vector3(1.94f, 0.22f, 0.18f), mats.Asphalt, false);
            CreatePart(car.transform, "RearBumper", new Vector3(0f, 0.38f, -2.18f), new Vector3(1.94f, 0.22f, 0.18f), mats.Asphalt, false);
            CreatePart(car.transform, "Headlight_L", new Vector3(-0.68f, 0.66f, 2.02f), new Vector3(0.28f, 0.16f, 0.08f), mats.Concrete, false);
            CreatePart(car.transform, "Headlight_R", new Vector3(0.68f, 0.66f, 2.02f), new Vector3(0.28f, 0.16f, 0.08f), mats.Concrete, false);
            CreatePart(car.transform, "Grille", new Vector3(0f, 0.62f, 2.03f), new Vector3(0.95f, 0.22f, 0.06f), ab.Plastic, false);
            CreatePart(car.transform, "Seat_L", new Vector3(-0.42f, 0.82f, -0.1f), new Vector3(0.52f, 0.52f, 0.48f), ab.Fabric, false);
            CreatePart(car.transform, "Seat_R", new Vector3(0.42f, 0.82f, -0.1f), new Vector3(0.52f, 0.52f, 0.48f), ab.Fabric, false);
            GameObject ajarDoor = CreatePart(car.transform, "Door_FL_Ajar", new Vector3(-0.96f, 0.78f, 0.15f), new Vector3(0.08f, 0.62f, 1.05f), ab.CarPaintBase, false);
            ajarDoor.transform.localRotation = Quaternion.Euler(0f, -38f, 0f);
            CreatePart(car.transform, "Mirror_L", new Vector3(-0.96f, 0.98f, 0.65f), new Vector3(0.18f, 0.10f, 0.10f), ab.Plastic, false);
            CreatePart(car.transform, "Mirror_R", new Vector3(0.96f, 0.98f, 0.65f), new Vector3(0.18f, 0.10f, 0.10f), ab.Plastic, false);
            GameObject steer = CreatePart(car.transform, "SteeringWheel", new Vector3(-0.42f, 0.92f, 0.34f), new Vector3(0.32f, 0.04f, 0.32f), mats.Asphalt, false, PrimitiveType.Cylinder);
            steer.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            GameObject exhaust = CreatePart(car.transform, "Exhaust", new Vector3(0.55f, 0.25f, -2.22f), new Vector3(0.08f, 0.22f, 0.08f), mats.RustyMetal, false, PrimitiveType.Cylinder);
            exhaust.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Vector3[] wheelOffsets =
            {
                new Vector3(-0.95f, 0.32f, 1.35f),
                new Vector3(0.95f, 0.32f, 1.35f),
                new Vector3(-0.95f, 0.32f, -1.35f),
                new Vector3(0.95f, 0.32f, -1.35f)
            };
            for (int i = 0; i < wheelOffsets.Length; i++)
            {
                GameObject w = CreatePart(car.transform, $"Wheel_{i}", wheelOffsets[i], new Vector3(0.64f, 0.18f, 0.64f), mats.Asphalt, false, PrimitiveType.Cylinder);
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                CreatePart(w.transform, "Rim", Vector3.zero, new Vector3(0.55f, 1.08f, 0.55f), mats.Concrete, false, PrimitiveType.Cylinder);
            }
        }

        private static void BuildIndustrialYard(ProceduralMaterialLibrary mats, AbandonedMaterials ab, GameObject doorPrefab)
        {
            _trimMat = mats.Concrete;
            _frameMat = mats.RustyMetal;
            _floorMat = mats.Concrete;

            GameObject yard = new GameObject("IndustrialYard");
            yard.transform.position = new Vector3(WorldLayout.YardCenter.x, 0f, WorldLayout.YardCenter.y);
            Transform t = yard.transform;

            // Главный ангар и диспетчерская.
            BuildBuilding(t, "Yard_Warehouse", new Vector3(12f, 0f, 10f), new Vector3(16f, 5.2f, 12f),
                mats.Concrete, mats.RustyMetal, mats.DarkGlass, doorPrefab, false, -90f);
            CreatePart(t, "Warehouse_Shelf1", new Vector3(14f, 1.1f, 7f), new Vector3(1.2f, 2.2f, 4.5f), mats.RustyMetal, true);
            CreatePart(t, "Warehouse_Crate1", new Vector3(11f, 0.6f, 12.5f), new Vector3(1.5f, 1.2f, 1.5f), mats.WoodPlanks, true);
            new GameObject("LootPoint_YardWarehouse").transform.SetParent(t, false);
            t.Find("LootPoint_YardWarehouse").localPosition = new Vector3(12f, 0.1f, 10f);

            BuildBuilding(t, "Yard_Workshop", new Vector3(-6f, 0f, -15f), new Vector3(10f, 3.6f, 8f),
                mats.Brick, mats.Concrete, mats.DarkGlass, doorPrefab, true, 0f);
            CreatePart(t, "Workshop_Bench", new Vector3(-6f, 0.45f, -16.5f), new Vector3(3.2f, 0.9f, 0.9f), ab.WoodDark, true);
            GameObject lpOffice = new GameObject("LootPoint_YardOffice");
            lpOffice.transform.SetParent(t, false);
            lpOffice.transform.localPosition = new Vector3(-6f, 0.1f, -15f);

            // Топливные/химические цистерны на бетонных опорах.
            for (int i = 0; i < 2; i++)
            {
                float tz = -11f - i * 6.5f;
                CreatePart(t, $"TankCradleA_{i}", new Vector3(15f, 0.6f, tz), new Vector3(0.7f, 1.2f, 2.6f), mats.Concrete, true);
                CreatePart(t, $"TankCradleB_{i}", new Vector3(20f, 0.6f, tz), new Vector3(0.7f, 1.2f, 2.6f), mats.Concrete, true);
                GameObject tank = CreatePart(t, $"StorageTank_{i}", new Vector3(17.5f, 2.2f, tz), new Vector3(2.8f, 3.8f, 2.8f), mats.RustyMetal, true, PrimitiveType.Cylinder);
                tank.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            // Морские контейнеры.
            CreatePart(t, "Container_1", new Vector3(-16f, 1.3f, 16f), new Vector3(6.0f, 2.6f, 2.5f), ab.MetalPainted, true);
            CreatePart(t, "Container_2", new Vector3(-16f, 1.3f, 12.6f), new Vector3(6.0f, 2.6f, 2.5f), mats.FuelCanRed, true);
            GameObject cont3 = CreatePart(t, "Container_3", new Vector3(-18f, 1.3f, -4f), new Vector3(2.5f, 2.6f, 6.0f), mats.RustyMetal, true);
            cont3.transform.localRotation = Quaternion.Euler(0f, 14f, 0f);
            GameObject lpCont = new GameObject("LootPoint_YardContainer");
            lpCont.transform.SetParent(t, false);
            lpCont.transform.localPosition = new Vector3(-12.5f, 0.1f, 14.3f);

            // Погрузочный кран-балка в центре площадки.
            CreatePart(t, "Gantry_LegFL", new Vector3(-2f, 3.0f, -2f), new Vector3(0.4f, 6.0f, 0.4f), mats.RustyMetal, true);
            CreatePart(t, "Gantry_LegFR", new Vector3(-2f, 3.0f, 8f), new Vector3(0.4f, 6.0f, 0.4f), mats.RustyMetal, true);
            CreatePart(t, "Gantry_LegBL", new Vector3(6f, 3.0f, -2f), new Vector3(0.4f, 6.0f, 0.4f), mats.RustyMetal, true);
            CreatePart(t, "Gantry_LegBR", new Vector3(6f, 3.0f, 8f), new Vector3(0.4f, 6.0f, 0.4f), mats.RustyMetal, true);
            CreatePart(t, "Gantry_BeamL", new Vector3(2f, 6.0f, -2f), new Vector3(8.8f, 0.45f, 0.45f), mats.RustyMetal, false);
            CreatePart(t, "Gantry_BeamR", new Vector3(2f, 6.0f, 8f), new Vector3(8.8f, 0.45f, 0.45f), mats.RustyMetal, false);
            CreatePart(t, "Gantry_Bridge", new Vector3(2f, 6.3f, 3f), new Vector3(0.55f, 0.4f, 10.6f), mats.FuelCanRed, false);

            // Прожекторная мачта с мигающим маяком.
            CreatePart(t, "YardMast", new Vector3(4f, 4.5f, 22f), new Vector3(0.32f, 9.0f, 0.32f), mats.RustyMetal, true, PrimitiveType.Cylinder);
            GameObject beacon = new GameObject("YardBeacon");
            beacon.transform.SetParent(t, false);
            beacon.transform.localPosition = new Vector3(4f, 9.2f, 22f);
            Light bLight = beacon.AddComponent<Light>();
            bLight.type = LightType.Point;
            bLight.range = 18f;
            bLight.color = new Color(1f, 0.3f, 0.15f);
            beacon.AddComponent<BlinkLight>();

            // Россыпь мелких объектов: поддоны, бочки, бетонные блоки, декали.
            for (int i = 0; i < 6; i++)
            {
                float bx = -8f + (i % 3) * 5.2f;
                float bz = 18f + (i / 3) * 3.2f;
                CreatePart(t, $"YardPallet_{i}", new Vector3(bx, 0.09f, bz), new Vector3(1.3f, 0.18f, 1.1f), mats.WoodPlanks, true);
                CreatePart(t, $"YardBarrel_{i}", new Vector3(bx + 1.2f, 0.48f, bz + 0.3f), new Vector3(0.58f, 0.95f, 0.58f), mats.RustyMetal, true, PrimitiveType.Cylinder);
            }

            CreatePart(t, "YardBarrier_1", new Vector3(-24f, 0.45f, 4f), new Vector3(0.65f, 0.9f, 2.4f), mats.Concrete, true);
            CreatePart(t, "YardBarrier_2", new Vector3(-24f, 0.45f, -8f), new Vector3(0.65f, 0.9f, 2.4f), mats.Concrete, true);

            ApartmentBlockBuilder.Decal(t, ab.MossDecal, new Vector3(0f, 0.03f, 0f), Vector3.up, new Vector2(6f, 6f), 15f);
            ApartmentBlockBuilder.Decal(t, ab.CrackDecal, new Vector3(-10f, 0.03f, 4f), Vector3.up, new Vector2(5f, 4f), -35f);

            // Периметровый забор по краям площадки с воротами на запад (x = -31, z = -2).
            float hx = WorldLayout.YardHalf.x - 1f;
            float hz = WorldLayout.YardHalf.y - 1f;
            CreatePart(t, "YardFence_N", new Vector3(0f, 1.1f, hz), new Vector3(hx * 2f, 2.2f, 0.18f), mats.RustyMetal, true);
            CreatePart(t, "YardFence_S", new Vector3(0f, 1.1f, -hz), new Vector3(hx * 2f, 2.2f, 0.18f), mats.RustyMetal, true);
            CreatePart(t, "YardFence_E", new Vector3(hx, 1.1f, 0f), new Vector3(0.18f, 2.2f, hz * 2f), mats.RustyMetal, true);
            CreatePart(t, "YardFence_WN", new Vector3(-hx, 1.1f, (hz + 3f) * 0.5f), new Vector3(0.18f, 2.2f, hz - 3f), mats.RustyMetal, true);
            CreatePart(t, "YardFence_WS", new Vector3(-hx, 1.1f, (-hz - 7f) * 0.5f), new Vector3(0.18f, 2.2f, hz - 7f), mats.RustyMetal, true);
        }

        private static void BuildAbandonedFarm(ProceduralMaterialLibrary mats, AbandonedMaterials ab, GameObject doorPrefab)
        {
            _trimMat = mats.Concrete;
            _frameMat = mats.WoodPlanks;
            _floorMat = mats.Concrete;

            GameObject farm = new GameObject("AbandonedFarm");
            farm.transform.position = new Vector3(WorldLayout.FarmCenter.x, 0f, WorldLayout.FarmCenter.y);
            Transform t = farm.transform;

            // Жилой дом с двором (используем BuildHouseWithYard).
            BuildHouseWithYard(t, "Farm_House", new Vector3(-11f, 0f, -6f), 45f, mats, ab, doorPrefab);
            GameObject lpHouse = new GameObject("LootPoint_FarmHouse");
            lpHouse.transform.SetParent(t, false);
            lpHouse.transform.localPosition = new Vector3(-11f, 0.1f, -6f);

            // Деревянный амбар.
            BuildBuilding(t, "Farm_Barn", new Vector3(11f, 0f, -4f), new Vector3(13f, 4.8f, 11f),
                mats.WoodPlanks, mats.RustyMetal, mats.DarkGlass, doorPrefab, false, -90f);
            SiteExtras.BuildBarnExtras(t.Find("Farm_Barn"), mats, ab);
            CreatePart(t, "Barn_Hay1", new Vector3(13f, 0.5f, -6.5f), new Vector3(1.8f, 1.0f, 1.4f), ab.Sandbag, true);
            CreatePart(t, "Barn_Hay2", new Vector3(13f, 0.5f, -1.5f), new Vector3(1.6f, 1.0f, 1.4f), ab.Sandbag, true);
            CreatePart(t, "Barn_Hay3", new Vector3(13f, 1.45f, -6.2f), new Vector3(1.4f, 0.9f, 1.2f), ab.Sandbag, true);
            GameObject lpBarn = new GameObject("LootPoint_FarmBarn");
            lpBarn.transform.SetParent(t, false);
            lpBarn.transform.localPosition = new Vector3(11f, 0.1f, -4f);

            // Зерновой силос рядом с амбаром.
            CreatePart(t, "Farm_SiloBase", new Vector3(18.5f, 0.2f, -10f), new Vector3(4.0f, 0.4f, 4.0f), mats.Concrete, true, PrimitiveType.Cylinder);
            CreatePart(t, "Farm_SiloBody", new Vector3(18.5f, 4.8f, -10f), new Vector3(3.5f, 4.6f, 3.5f), mats.RustyMetal, true, PrimitiveType.Cylinder);
            CreatePart(t, "Farm_SiloCap", new Vector3(18.5f, 9.6f, -10f), new Vector3(3.7f, 0.4f, 3.7f), mats.Concrete, false, PrimitiveType.Cylinder);
            CreatePart(t, "Farm_SiloTop", new Vector3(18.5f, 10.1f, -10f), new Vector3(2.2f, 0.6f, 2.2f), mats.RustyMetal, false, PrimitiveType.Cylinder);

            // Старый колодец в центре двора.
            CreatePart(t, "Well_Ring", new Vector3(-1f, 0.45f, 3f), new Vector3(1.4f, 0.9f, 1.4f), mats.Stone, true, PrimitiveType.Cylinder);
            CreatePart(t, "Well_PostL", new Vector3(-1.7f, 1.15f, 3f), new Vector3(0.12f, 1.5f, 0.12f), mats.WoodPlanks, false);
            CreatePart(t, "Well_PostR", new Vector3(-0.3f, 1.15f, 3f), new Vector3(0.12f, 1.5f, 0.12f), mats.WoodPlanks, false);
            CreatePart(t, "Well_Roof", new Vector3(-1f, 2.0f, 3f), new Vector3(1.8f, 0.14f, 1.4f), mats.WoodPlanks, false);

            // Загон для скота и россыпь фермерского инвентаря.
            for (int i = 0; i <= 4; i++)
            {
                CreatePart(t, $"CorralPostN_{i}", new Vector3(2f + i * 3f, 0.55f, 16f), new Vector3(0.14f, 1.1f, 0.14f), mats.TreeBark, true, PrimitiveType.Cylinder);
                CreatePart(t, $"CorralPostS_{i}", new Vector3(2f + i * 3f, 0.55f, 6f), new Vector3(0.14f, 1.1f, 0.14f), mats.TreeBark, true, PrimitiveType.Cylinder);
                if (i < 4)
                {
                    CreatePart(t, $"CorralRailN_{i}", new Vector3(3.5f + i * 3f, 0.85f, 16f), new Vector3(3.0f, 0.09f, 0.07f), mats.WoodPlanks, false);
                    CreatePart(t, $"CorralRailS_{i}", new Vector3(3.5f + i * 3f, 0.85f, 6f), new Vector3(3.0f, 0.09f, 0.07f), mats.WoodPlanks, false);
                }
            }

            CreatePart(t, "WaterTrough", new Vector3(8f, 0.28f, 11f), new Vector3(2.4f, 0.55f, 0.8f), mats.WoodPlanks, true);
            CreatePart(t, "Yard_Hay1", new Vector3(4f, 0.5f, 13f), new Vector3(1.6f, 1.0f, 1.3f), ab.Sandbag, true);
            CreatePart(t, "Yard_Hay2", new Vector3(12f, 0.5f, 9f), new Vector3(1.5f, 1.0f, 1.4f), ab.Sandbag, true);
            CreatePart(t, "Farm_WoodPile", new Vector3(2f, 0.4f, -11f), new Vector3(2.6f, 0.8f, 1.2f), mats.TreeBark, true);
            CreatePart(t, "Farm_Barrel1", new Vector3(3.5f, 0.48f, -1.5f), new Vector3(0.58f, 0.95f, 0.58f), mats.RustyMetal, true, PrimitiveType.Cylinder);
            CreatePart(t, "Farm_Barrel2", new Vector3(3.5f, 0.48f, -2.5f), new Vector3(0.58f, 0.95f, 0.58f), mats.FuelCanRed, true, PrimitiveType.Cylinder);

            // Остов старого прицепа / телеги.
            CreatePart(t, "CartBed", new Vector3(-1f, 0.65f, 13f), new Vector3(3.0f, 0.25f, 1.8f), mats.RustyMetal, true);
            CreatePart(t, "CartWheelL", new Vector3(-1f, 0.35f, 14.0f), new Vector3(0.7f, 0.14f, 0.7f), mats.Asphalt, false, PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreatePart(t, "CartWheelR", new Vector3(-1f, 0.35f, 12.0f), new Vector3(0.7f, 0.14f, 0.7f), mats.Asphalt, false, PrimitiveType.Cylinder).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            GameObject lpYard = new GameObject("LootPoint_FarmYard");
            lpYard.transform.SetParent(t, false);
            lpYard.transform.localPosition = new Vector3(7f, 0.1f, 12f);

            ApartmentBlockBuilder.Decal(t, ab.MossDecal, new Vector3(2f, 0.03f, 2f), Vector3.up, new Vector2(6f, 5f), 25f);
        }

        private static void BuildEvacuationZone(ProceduralMaterialLibrary mats)
        {
            GameObject evac = new GameObject("EvacuationZone");
            evac.transform.position = new Vector3(0f, 0f, -76f);

            CreatePart(evac.transform, "Helipad", new Vector3(0f, 0.04f, 0f), new Vector3(14f, 0.08f, 14f), mats.Concrete, true);
            CreatePart(evac.transform, "RadioTower", new Vector3(5.5f, 5f, -4.5f), new Vector3(0.55f, 10f, 0.55f), mats.RustyMetal, true, PrimitiveType.Cylinder);

            GameObject beacon = new GameObject("EvacBeaconLight");
            beacon.transform.SetParent(evac.transform, false);
            beacon.transform.localPosition = new Vector3(5.5f, 10.2f, -4.5f);
            Light light = beacon.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 20f;
            light.intensity = 2.0f;
            light.color = new Color(1f, 0.25f, 0.2f);
        }

        private static void ScatterPickups(
            Terrain terrain,
            GameObject wood, GameObject stone, GameObject metal,
            GameObject cloth, GameObject fuel, GameObject med)
        {
            GameObject group = new GameObject("WorldPickups");
            Random.State prev = Random.state;
            Random.InitState(9001);

            // Ветки и камни по лесу и вокруг лагеря.
            for (int i = 0; i < 24; i++)
            {
                float x = Random.Range(-45f, 45f);
                float z = Random.Range(-30f, 45f);
                if (Mathf.Abs(x) < 4f && Mathf.Abs(z) < 4f) continue;
                float y = terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) : 0f;
                SpawnPickup(group.transform, i % 2 == 0 ? wood : stone, new Vector3(x, y, z), Random.Range(0f, 360f));
            }

            // Городской лут (внутри и около зданий).
            Vector3[] cityLootPoints =
            {
                new Vector3(-16f, 0.1f, 70f), // Магазин — ткань
                new Vector3(-14f, 0.1f, 68f),
                new Vector3(11f, 0f, 68f),    // Заправка — топливо
                new Vector3(17f, 0.1f, 70f),
                new Vector3(-17f, 0.1f, 110f),// Склад — металл
                new Vector3(-15f, 0.1f, 112f),
                new Vector3(17f, 0.1f, 110f), // Медпункт — медикаменты
                new Vector3(15f, 0.1f, 108f),
                new Vector3(-28f, 0.1f, 138f),// Высотки
                new Vector3(28f, 0.1f, 138f),
                new Vector3(0f, 0.1f, 152f)
            };

            GameObject[] cityPrefabs = { cloth, cloth, fuel, fuel, metal, metal, med, med, metal, cloth, med };
            for (int i = 0; i < cityLootPoints.Length; i++)
            {
                SpawnPickup(group.transform, cityPrefabs[i], cityLootPoints[i], i * 45f);
            }

            Random.state = prev;
        }

        private static void SpawnPickup(Transform parent, GameObject prefab, Vector3 pos, float yaw)
        {
            GameObject p = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            p.transform.SetParent(parent, false);
            p.transform.position = pos;
            p.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // Материалы отделки фасадов малоэтажных зданий (задаются в BuildCityDistrict).
        private static Material _trimMat;   // бетон: пилястры, цоколь, карниз, наличники
        private static Material _frameMat;  // дерево: рамы окон
        private static Material _floorMat;  // пол внутри (раньше был кирпичным - это и был «кирпич в проходе»)

        private static Vector3 WinSize(bool alongX, float len, float hgt, float depth)
            => alongX ? new Vector3(len, hgt, depth) : new Vector3(depth, hgt, len);

        /// <summary>
        /// Окно малоэтажного здания: стекло, рама с импостом, подоконник и перемычка.
        /// outward - наружная нормаль стены (в системе здания), center - центр окна на поверхности стены.
        /// </summary>
        private static void BuildHouseWindow(Transform b, string name, Vector3 center, Vector3 outward, float width, float height, Material glass, bool boarded)
        {
            bool alongX = Mathf.Abs(outward.z) > 0.5f;
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            Material frame = _frameMat != null ? _frameMat : glass;
            Material trim = _trimMat != null ? _trimMat : glass;

            CreatePart(b, name + "_Glass", center + outward * 0.03f, WinSize(alongX, width, height, 0.05f), glass, false);

            float ft = 0.07f;
            CreatePart(b, name + "_FrameT", center + Vector3.up * (height * 0.5f) + outward * 0.06f, WinSize(alongX, width + ft, ft, 0.08f), frame, false);
            CreatePart(b, name + "_FrameB", center - Vector3.up * (height * 0.5f) + outward * 0.06f, WinSize(alongX, width + ft, ft, 0.08f), frame, false);
            CreatePart(b, name + "_FrameL", center - along * (width * 0.5f) + outward * 0.06f, WinSize(alongX, ft, height, 0.08f), frame, false);
            CreatePart(b, name + "_FrameR", center + along * (width * 0.5f) + outward * 0.06f, WinSize(alongX, ft, height, 0.08f), frame, false);
            CreatePart(b, name + "_MullionV", center + outward * 0.06f, WinSize(alongX, 0.05f, height, 0.07f), frame, false);
            CreatePart(b, name + "_MullionH", center + Vector3.up * (height * 0.12f) + outward * 0.06f, WinSize(alongX, width, 0.05f, 0.07f), frame, false);

            CreatePart(b, name + "_Sill", center - Vector3.up * (height * 0.5f + 0.05f) + outward * 0.1f, WinSize(alongX, width + 0.34f, 0.07f, 0.26f), trim, false);
            CreatePart(b, name + "_Lintel", center + Vector3.up * (height * 0.5f + 0.1f) + outward * 0.07f, WinSize(alongX, width + 0.34f, 0.14f, 0.2f), trim, false);

            if (boarded)
            {
                for (int i = 0; i < 3; i++)
                {
                    GameObject board = CreatePart(b, name + "_Board" + i,
                        center + Vector3.up * (height * (0.28f - i * 0.28f)) + outward * 0.12f,
                        WinSize(alongX, width + 0.2f, 0.14f, 0.04f), frame, false);
                    board.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(0f, 0f, (i - 1) * 4f);
                    board.transform.localScale = new Vector3(width + 0.2f, 0.14f, 0.04f);
                }
            }
        }

        private static void BuildBuilding(
            Transform parent, string name, Vector3 pos, Vector3 size,
            Material wallMat, Material roofMat, Material glassMat,
            GameObject doorPrefab, bool warmInteriorLight, float yaw = 0f)
        {
            GameObject b = new GameObject(name);
            b.transform.SetParent(parent, false);
            b.transform.localPosition = pos;
            b.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            float w = size.x;
            float h = size.y;
            float d = size.z;
            float t = 0.25f;

            const float doorW = 1.6f;      // проём (раньше 1.2 м): с дверью 1.4 м остаётся запас по краям
            const float pilaster = 0.34f;  // бетонная пилястра по бокам проёма - в проходе больше нет кирпича
            const float floorTop = 0.06f;
            float halfDoor = doorW * 0.5f;
            Material trim = _trimMat != null ? _trimMat : roofMat;
            Material floorMat = _floorMat != null ? _floorMat : roofMat;
            int variant = Mathf.Abs(Mathf.RoundToInt(pos.x * 13f + pos.z * 7f));

            // Пол - не кирпич: тонкая плита ровно по высоте порога, чтобы не было ступеньки в проходе.
            CreatePart(b.transform, "Floor", new Vector3(0f, floorTop * 0.5f, 0f), new Vector3(w, floorTop, d), floorMat, true);
            CreatePart(b.transform, "Roof", new Vector3(0f, h + 0.1f, 0f), new Vector3(w + 0.6f, 0.22f, d + 0.6f), roofMat, true);
            CreatePart(b.transform, "BackWall", new Vector3(0f, h * 0.5f, -d * 0.5f + t * 0.5f), new Vector3(w, h, t), wallMat, true);
            CreatePart(b.transform, "LeftWall", new Vector3(-w * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), wallMat, true);
            CreatePart(b.transform, "RightWall", new Vector3(w * 0.5f - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), wallMat, true);

            // Фасад: кирпичные простенки, бетонные пилястры у проёма и перемычка над дверью.
            float frontZ = d * 0.5f - t * 0.5f;
            float sideW = (w - doorW) * 0.5f;
            float brickW = Mathf.Max(0.2f, sideW - pilaster);
            float brickCx = halfDoor + pilaster + brickW * 0.5f;
            CreatePart(b.transform, "FrontLeft", new Vector3(-brickCx, h * 0.5f, frontZ), new Vector3(brickW, h, t), wallMat, true);
            CreatePart(b.transform, "FrontRight", new Vector3(brickCx, h * 0.5f, frontZ), new Vector3(brickW, h, t), wallMat, true);
            CreatePart(b.transform, "DoorPilasterL", new Vector3(-(halfDoor + pilaster * 0.5f), h * 0.5f, frontZ + 0.03f), new Vector3(pilaster, h, t + 0.06f), trim, true);
            CreatePart(b.transform, "DoorPilasterR", new Vector3(halfDoor + pilaster * 0.5f, h * 0.5f, frontZ + 0.03f), new Vector3(pilaster, h, t + 0.06f), trim, true);
            if (h > 2.15f)
            {
                float headerH = h - 2.1f;
                CreatePart(b.transform, "DoorHeader", new Vector3(0f, 2.1f + headerH * 0.5f, frontZ + 0.03f), new Vector3(doorW, headerH, t + 0.06f), trim, true);
            }

            // Цоколь по периметру (с разрывом у двери) и угловые пилястры.
            float plinthH = 0.45f;
            float plinthY = plinthH * 0.5f;
            CreatePart(b.transform, "PlinthBack", new Vector3(0f, plinthY, -d * 0.5f - 0.02f), new Vector3(w + 0.1f, plinthH, 0.1f), trim, false);
            CreatePart(b.transform, "PlinthL", new Vector3(-w * 0.5f - 0.02f, plinthY, 0f), new Vector3(0.1f, plinthH, d + 0.1f), trim, false);
            CreatePart(b.transform, "PlinthR", new Vector3(w * 0.5f + 0.02f, plinthY, 0f), new Vector3(0.1f, plinthH, d + 0.1f), trim, false);
            float plinthFrontW = sideW - pilaster + 0.05f;
            CreatePart(b.transform, "PlinthFrontL", new Vector3(-(w * 0.5f - plinthFrontW * 0.5f + 0.05f), plinthY, d * 0.5f + 0.02f), new Vector3(plinthFrontW, plinthH, 0.1f), trim, false);
            CreatePart(b.transform, "PlinthFrontR", new Vector3(w * 0.5f - plinthFrontW * 0.5f + 0.05f, plinthY, d * 0.5f + 0.02f), new Vector3(plinthFrontW, plinthH, 0.1f), trim, false);
            foreach (Vector2 c in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            {
                CreatePart(b.transform, "CornerPilaster", new Vector3(c.x * (w * 0.5f - 0.1f), h * 0.5f, c.y * (d * 0.5f - 0.1f)), new Vector3(0.32f, h, 0.32f), trim, false);
            }

            // Карниз под крышей.
            CreatePart(b.transform, "Cornice", new Vector3(0f, h - 0.08f, 0f), new Vector3(w + 0.3f, 0.24f, d + 0.3f), trim, false);

            // Окна фасада и боковых стен (стекло, рамы, подоконники; часть заколочена досками).
            float winW = Mathf.Min(1.3f, brickW - 0.5f);
            float winH = Mathf.Min(1.1f, h - 2.2f);
            float winY = 1.1f + winH * 0.5f;
            if (winW >= 0.7f && winH >= 0.6f)
            {
                float fz = d * 0.5f - 0.0f;
                BuildHouseWindow(b.transform, "Window_FrontL", new Vector3(-brickCx, winY, fz), Vector3.forward, winW, winH, glassMat, variant % 3 == 0);
                BuildHouseWindow(b.transform, "Window_FrontR", new Vector3(brickCx, winY, fz), Vector3.forward, winW, winH, glassMat, variant % 4 == 1);
                if (w >= 8f)
                {
                    float wx = w * 0.5f;
                    BuildHouseWindow(b.transform, "Window_SideL", new Vector3(-wx, winY, d * 0.12f), Vector3.left, winW, winH, glassMat, variant % 2 == 0);
                    BuildHouseWindow(b.transform, "Window_SideR", new Vector3(wx, winY, d * 0.12f), Vector3.right, winW, winH, glassMat, variant % 5 == 2);
                    BuildHouseWindow(b.transform, "Window_Back", new Vector3(0f, winY, -d * 0.5f), Vector3.back, winW, winH, glassMat, false);
                }
            }

            // Крыльцо без высокого порога и небольшой козырёк.
            CreatePart(b.transform, "Porch", new Vector3(0f, floorTop * 0.5f, d * 0.5f + 0.5f), new Vector3(doorW + 0.8f, floorTop, 1.0f), floorMat, true);
            if (h > 2.8f)
            {
                CreatePart(b.transform, "DoorCanopy", new Vector3(0f, 2.42f, d * 0.5f + 0.55f), new Vector3(doorW + 1.2f, 0.12f, 1.2f), trim, false);
            }

            if (warmInteriorLight)
            {
                GameObject lamp = new GameObject("InteriorLight");
                lamp.transform.SetParent(b.transform, false);
                lamp.transform.localPosition = new Vector3(0f, h - 0.35f, 0f);
                Light p = lamp.AddComponent<Light>();
                p.type = LightType.Point;
                p.range = 9f;
                p.intensity = 1.5f;
                p.color = new Color(1f, 0.78f, 0.48f);
            }

            GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
            door.transform.SetParent(b.transform, false);
            door.transform.localPosition = new Vector3(0f, floorTop, frontZ);
        }

        private static void CreateBoundaryCollider(string name, Vector3 pos, Vector3 scale)
        {
            GameObject obj = new GameObject(name);
            obj.transform.position = pos;
            BoxCollider box = obj.AddComponent<BoxCollider>();
            box.size = scale;
        }

        private static GameObject CreateMeshPart(
            Transform parent, string name, Vector3 localPos, Vector3 scale, Mesh mesh, Material mat, bool keepCollider)
        {
            GameObject part = new GameObject(name);
            if (parent != null) part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (keepCollider)
            {
                MeshCollider mc = part.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
            }
            return part;
        }

        private static GameObject CreatePart(
            Transform parent, string name, Vector3 localPos, Vector3 scale,
            Material mat, bool keepCollider, PrimitiveType primitive = PrimitiveType.Cube)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            if (parent != null) part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;
            if (mat != null)
            {
                part.GetComponent<Renderer>().sharedMaterial = mat;
                if (primitive == PrimitiveType.Cube) UvBoxes.Apply(part, scale, mat); // UV в метрах, а не 0..1 на грань
            }

            if (!keepCollider) Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }

        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene s in scenes)
            {
                if (s.path == scenePath)
                {
                    s.enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SetInt(Object target, string field, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null)
            {
                property.intValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetFloat(Object target, string field, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null)
            {
                property.floatValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetReference(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null)
            {
                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetReferences(Object target, string field, Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null)
            {
                property.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
