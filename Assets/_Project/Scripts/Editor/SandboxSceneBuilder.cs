using Ogni.Resources;
using Ogni.Building;
using Ogni.Entities.Listener;
using Ogni.Net;
using Ogni.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Меню Ogni -> Build Sandbox Scene.
    /// Создаёт префабы (Player, Barricade, Door, Listener) и тестовую сцену Sandbox
    /// для этапа 3: ночь, фонарик, баррикады (постройка/починка), домик с дверью и Слушатель.
    /// </summary>
    public static class SandboxSceneBuilder
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string BarricadePrefabPath = "Assets/_Project/Prefabs/Barricade.prefab";
        private const string DoorPrefabPath = "Assets/_Project/Prefabs/Door.prefab";
        private const string ListenerPrefabPath = "Assets/_Project/Prefabs/Listener.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox.unity";

        [MenuItem("Ogni/Build Sandbox Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Ogni] Сначала остановите Play Mode (нажмите Play ещё раз), затем запустите Ogni -> Build Sandbox Scene.");
                return;
            }

            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Scenes");

            GameObject playerPrefab = BuildPlayerPrefab();
            GameObject barricadePrefab = BuildBarricadePrefab();
            GameObject doorPrefab = BuildDoorPrefab();
            GameObject listenerPrefab = BuildListenerPrefab();

            BuildScene(playerPrefab, barricadePrefab, doorPrefab, listenerPrefab);

            Debug.Log("[Ogni] Готово (Этап 3): сцена " + ScenePath +
                      ", префабы Player, Barricade, Door, Listener. Откройте сцену и нажмите Play.");
        }

        public static GameObject EnsurePlayerPrefab() => BuildPlayerPrefab();

        private static GameObject BuildPlayerPrefab()
        {
            GameObject root = new GameObject("Player");

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.radius = 0.3f;
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            PlayerInputReader input = root.AddComponent<PlayerInputReader>();
            FirstPersonMotor motor = root.AddComponent<FirstPersonMotor>();
            PlayerInteractor interactor = root.AddComponent<PlayerInteractor>();
            PlayerHealth health = root.AddComponent<PlayerHealth>();
            FlashlightController flashlight = root.AddComponent<FlashlightController>();
            root.AddComponent<NetworkNoiseRelay>();
            root.AddComponent<PlayerInventory>();
            PlayerCraftMenu craftMenu = root.AddComponent<PlayerCraftMenu>();
            PistolController pistol = root.AddComponent<PistolController>();
            ShotgunController shotgun = root.AddComponent<ShotgunController>();
            PlayerEquipment equipment = root.AddComponent<PlayerEquipment>();
            PlayerBuilder builder = root.AddComponent<PlayerBuilder>();
            root.AddComponent<PlayerAppearanceRig>();
            root.AddComponent<PlayerAppearanceSync>();

            root.AddComponent<NetworkObject>();
            NetworkTransform networkTransform = root.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;

            NetworkPlayer networkPlayer = root.AddComponent<NetworkPlayer>();

            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            FirstPersonCamera firstPersonCamera = cameraObject.AddComponent<FirstPersonCamera>();

            GameObject lightObj = new GameObject("Flashlight");
            lightObj.transform.SetParent(cameraObject.transform, false);
            lightObj.transform.localPosition = new Vector3(0.0f, 0.03f, 0.05f); // у центра камеры: раньше свет стоял в правой руке и «выжигал» её в белый
            Light spot = lightObj.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 68f;
            spot.spotAngle = 60f;
            spot.innerSpotAngle = 30f;
            spot.intensity = 48f;
            spot.color = new Color(1f, 0.95f, 0.85f);

            SetReference(interactor, "viewOrigin", cameraObject.transform);
            SetReference(health, "characterController", controller);
            SetReference(flashlight, "spotLight", spot);

            // Детализированные модели рук/инструментов/оружия (см. ViewModelBuilder).
            ViewModelBuilder.Mats vm = ViewModelBuilder.CreateMats();
            Transform cam = cameraObject.transform;

            GameObject unarmedObj = ViewModelBuilder.BuildUnarmed(cam, vm);
            GameObject axeObj = ViewModelBuilder.BuildAxe(cam, vm);
            GameObject hammerObj = ViewModelBuilder.BuildHammer(cam, vm);
            GameObject pistolObj = ViewModelBuilder.BuildPistol(cam, vm, out Light muzzle);
            GameObject clubObj = ViewModelBuilder.BuildClub(cam, vm);
            GameObject shotgunObj = ViewModelBuilder.BuildShotgun(cam, vm, out Light sgMuzzle);
            ViewModelBuilder.BowParts bowParts = ViewModelBuilder.BuildBow(cam, vm);

            SetReference(pistol, "aimOrigin", cam);
            SetReference(pistol, "pistolModel", pistolObj.transform);
            SetReference(pistol, "muzzleLight", muzzle);
            SetReference(shotgun, "aimOrigin", cam);
            SetReference(shotgun, "shotgunModel", shotgunObj.transform);
            SetReference(shotgun, "muzzleLight", sgMuzzle);

            // Лук: раньше BowController и модель вообще не создавались - поэтому лук не появлялся в руках.
            BowController bow = root.AddComponent<BowController>();
            SetReference(bow, "aimOrigin", cam);
            SetReference(bow, "stringMid", bowParts.StringMid);
            SetReference(bow, "nockedArrow", bowParts.NockedArrow);
            SetReference(bow, "arrowMaterial", vm.Wood);
            BowStringVisual stringVisual = bowParts.Root.AddComponent<BowStringVisual>();
            SetReference(stringVisual, "tipTop", bowParts.TipTop);
            SetReference(stringVisual, "tipBottom", bowParts.TipBottom);
            SetReference(stringVisual, "mid", bowParts.StringMid);
            SetReference(stringVisual, "segTop", bowParts.SegTop);
            SetReference(stringVisual, "segBottom", bowParts.SegBottom);

            SetReference(builder, "aimOrigin", cam);
            SetReference(equipment, "unarmedModel", unarmedObj);
            SetReference(equipment, "axeModel", axeObj);
            SetReference(equipment, "clubModel", clubObj);
            SetReference(equipment, "shotgunModel", shotgunObj);
            SetReference(equipment, "hammerModel", hammerObj);
            SetReference(equipment, "pistolModel", pistolObj);
            SetReference(equipment, "bowModel", bowParts.Root);

            SetReferences(networkPlayer, "ownerOnlyBehaviours",
                new Object[] { input, motor, interactor, firstPersonCamera, flashlight, craftMenu, pistol, shotgun, bow, equipment, builder });
            SetReference(networkPlayer, "cameraRoot", cameraObject);
            SetReference(networkPlayer, "controller", controller);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        }

        private static GameObject BuildBarricadePrefab()
        {
            GameObject root = new GameObject("Barricade");
            root.AddComponent<NetworkObject>();
            Barricade barricade = root.AddComponent<Barricade>();

            // Нижний порог и две боковые стойки: остаются всегда, чтобы на баррикаду легко было навестись [E].
            CreateChildPart(root.transform, "Threshold", new Vector3(0f, 0.1f, 0f), new Vector3(3.4f, 0.2f, 0.4f), true);
            CreateChildPart(root.transform, "PostLeft", new Vector3(-1.65f, 0.75f, 0f), new Vector3(0.22f, 1.5f, 0.35f), true);
            CreateChildPart(root.transform, "PostRight", new Vector3(1.65f, 0.75f, 0f), new Vector3(0.22f, 1.5f, 0.35f), true);

            // Разрушаемая часть из деревянных досок + один общий коллайдер преграды.
            GameObject barrierGroup = new GameObject("BarrierPlanks");
            barrierGroup.transform.SetParent(root.transform, false);
            BoxCollider barrierCollider = barrierGroup.AddComponent<BoxCollider>();
            barrierCollider.center = new Vector3(0f, 0.85f, 0f);
            barrierCollider.size = new Vector3(3.1f, 1.3f, 0.3f);

            GameObject p1 = CreateChildPart(barrierGroup.transform, "PlankLow", new Vector3(0f, 0.45f, 0f), new Vector3(3.2f, 0.24f, 0.14f), false);
            GameObject p2 = CreateChildPart(barrierGroup.transform, "PlankMid", new Vector3(0f, 0.88f, 0f), new Vector3(3.2f, 0.24f, 0.14f), false);
            GameObject p3 = CreateChildPart(barrierGroup.transform, "PlankHigh", new Vector3(0f, 1.30f, 0f), new Vector3(3.2f, 0.24f, 0.14f), false);
            GameObject brace = CreateChildPart(barrierGroup.transform, "CrossBrace", new Vector3(0f, 0.88f, 0.08f), new Vector3(2.9f, 0.16f, 0.08f), false);
            brace.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);

            SetReference(barricade, "barrierVisual", barrierGroup);
            SetReference(barricade, "barrierCollider", barrierCollider);
            SetReferences(barricade, "barrierRenderers", new Object[]
            {
                p1.GetComponent<Renderer>(),
                p2.GetComponent<Renderer>(),
                p3.GetComponent<Renderer>(),
                brace.GetComponent<Renderer>()
            });

            PrefabUtility.SaveAsPrefabAsset(root, BarricadePrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(BarricadePrefabPath);
        }

        private static GameObject BuildDoorPrefab()
        {
            GameObject root = new GameObject("Door");
            root.AddComponent<NetworkObject>();
            Door door = root.AddComponent<Door>();

            GameObject pivot = new GameObject("DoorPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(-0.6f, 0f, 0f);

            GameObject panel = CreateChildPart(pivot.transform, "DoorPanel", new Vector3(0.6f, 1.05f, 0f), new Vector3(1.2f, 2.1f, 0.12f), true);
            CreateChildPart(pivot.transform, "Handle", new Vector3(1.05f, 1.0f, 0.09f), new Vector3(0.14f, 0.06f, 0.08f), false);

            SetReference(door, "doorPivot", pivot.transform);

            PrefabUtility.SaveAsPrefabAsset(root, DoorPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
        }

        private static GameObject BuildListenerPrefab()
        {
            GameObject root = new GameObject("Listener");
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<ListenerAI>();

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.45f;
            collider.height = 1.9f;
            collider.center = new Vector3(0f, 0.95f, 0f);

            CreateChildPart(root.transform, "BodyVisual", new Vector3(0f, 0.95f, 0f), new Vector3(0.85f, 0.95f, 0.85f), false, PrimitiveType.Capsule);

            PrefabUtility.SaveAsPrefabAsset(root, ListenerPrefabPath);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(ListenerPrefabPath);
        }

        private static void BuildScene(GameObject playerPrefab, GameObject barricadePrefab, GameObject doorPrefab, GameObject listenerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ночное освещение и туман.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.04f, 0.07f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.05f);
            RenderSettings.fogDensity = 0.04f;

            GameObject lightObject = new GameObject("Moon Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.15f;
            light.color = new Color(0.55f, 0.65f, 0.9f);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Пол и стены.
            CreateBlock("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(44f, 1f, 44f));
            CreateBlock("Wall North", new Vector3(0f, 1.5f, 22f), new Vector3(44f, 3f, 1f));
            CreateBlock("Wall South", new Vector3(0f, 1.5f, -22f), new Vector3(44f, 3f, 1f));
            CreateBlock("Wall East", new Vector3(22f, 1.5f, 0f), new Vector3(1f, 3f, 44f));
            CreateBlock("Wall West", new Vector3(-22f, 1.5f, 0f), new Vector3(1f, 3f, 44f));

            // Низкий проход для приседания.
            CreateBlock("Low Passage Roof", new Vector3(8f, 1.55f, 0f), new Vector3(4f, 0.3f, 6f));
            CreateBlock("Low Passage Pillar A", new Vector3(5.75f, 0.7f, 0f), new Vector3(0.5f, 1.4f, 6f));
            CreateBlock("Low Passage Pillar B", new Vector3(10.25f, 0.7f, 0f), new Vector3(0.5f, 1.4f, 6f));

            // Тестовый куб взаимодействия.
            GameObject interactable = CreateBlock("Test Interactable", new Vector3(3f, 0.5f, 3f), Vector3.one);
            interactable.AddComponent<TestInteractable>();

            // Небольшой домик с дверью в левой части поляны.
            BuildCabin(new Vector3(-9f, 0f, -6f), doorPrefab);

            // Три баррикады: целая, повреждённая (35 HP) и пустой слот для постройки (0 HP).
            GameObject b1 = (GameObject)PrefabUtility.InstantiatePrefab(barricadePrefab);
            b1.name = "Barricade_Intact";
            b1.transform.position = new Vector3(0f, 0f, 6f);

            GameObject b2 = (GameObject)PrefabUtility.InstantiatePrefab(barricadePrefab);
            b2.name = "Barricade_Damaged";
            b2.transform.position = new Vector3(-4.5f, 0f, 4f);
            SetFloat(b2.GetComponent<Barricade>(), "startHp", 35f);

            GameObject b3 = (GameObject)PrefabUtility.InstantiatePrefab(barricadePrefab);
            b3.name = "Barricade_BuildSlot";
            b3.transform.position = new Vector3(4.5f, 0f, 5f);
            SetFloat(b3.GetComponent<Barricade>(), "startHp", 0f);

            // Слушатель в дальнем углу поляны, чтобы не нападал прямо на спавне.
            GameObject listener = (GameObject)PrefabUtility.InstantiatePrefab(listenerPrefab);
            listener.name = "Listener";
            listener.transform.position = new Vector3(12f, 0f, 14f);

            // Сеть.
            GameObject network = new GameObject("Network");
            NetworkManager manager = network.AddComponent<NetworkManager>();
            UnityTransport transport = network.AddComponent<UnityTransport>();
            GameSession session = network.AddComponent<GameSession>();

            if (manager.NetworkConfig == null)
            {
                manager.NetworkConfig = new NetworkConfig();
            }

            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;

            // Временное меню подключения и HUD.
            GameObject hudObject = new GameObject("Debug HUD");
            DebugConnectionHud hud = hudObject.AddComponent<DebugConnectionHud>();
            SetReference(hud, "session", session);
            hudObject.AddComponent<Ogni.UI.DebugPromptView>();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void BuildCabin(Vector3 origin, GameObject doorPrefab)
        {
            GameObject cabin = new GameObject("Cabin");
            cabin.transform.position = origin;

            // Стены домика 6x6, вход с передней стороны (+Z) с проёмом шириной 1.2м.
            CreateChildPart(cabin.transform, "Cabin_Floor", new Vector3(0f, 0.05f, 0f), new Vector3(6f, 0.1f, 6f), true);
            CreateChildPart(cabin.transform, "Cabin_Roof", new Vector3(0f, 2.85f, 0f), new Vector3(6.4f, 0.2f, 6.4f), true);
            CreateChildPart(cabin.transform, "Cabin_BackWall", new Vector3(0f, 1.4f, -2.9f), new Vector3(6f, 2.8f, 0.25f), true);
            CreateChildPart(cabin.transform, "Cabin_LeftWall", new Vector3(-2.9f, 1.4f, 0f), new Vector3(0.25f, 2.8f, 6f), true);
            CreateChildPart(cabin.transform, "Cabin_RightWall", new Vector3(2.9f, 1.4f, 0f), new Vector3(0.25f, 2.8f, 6f), true);

            // Передняя стена с дверным проёмом (ширина проёма 1.2м, высота 2.1м).
            CreateChildPart(cabin.transform, "Cabin_FrontLeft", new Vector3(-1.8f, 1.4f, 2.9f), new Vector3(2.4f, 2.8f, 0.25f), true);
            CreateChildPart(cabin.transform, "Cabin_FrontRight", new Vector3(1.8f, 1.4f, 2.9f), new Vector3(2.4f, 2.8f, 0.25f), true);
            CreateChildPart(cabin.transform, "Cabin_DoorHeader", new Vector3(0f, 2.45f, 2.9f), new Vector3(1.2f, 0.7f, 0.25f), true);

            // Тёплая лампа внутри домика для ориентира в темноте.
            GameObject lamp = new GameObject("Cabin_Light");
            lamp.transform.SetParent(cabin.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            Light point = lamp.AddComponent<Light>();
            point.type = LightType.Point;
            point.range = 8f;
            point.intensity = 1.6f;
            point.color = new Color(1f, 0.75f, 0.45f);

            GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
            door.name = "Cabin_Door";
            door.transform.SetParent(cabin.transform, false);
            door.transform.localPosition = new Vector3(0f, 0f, 2.9f);
        }

        private static GameObject CreateChildPart(
            Transform parent,
            string name,
            Vector3 localPos,
            Vector3 scale,
            bool keepCollider,
            PrimitiveType primitive = PrimitiveType.Cube)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;

            if (!keepCollider)
            {
                Object.DestroyImmediate(part.GetComponent<Collider>());
            }

            return part;
        }

        private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            return block;
        }

        private static void SetFloat(Object target, string field, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Ogni] Поле '{field}' не найдено в {target.GetType().Name}.");
                return;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReference(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Ogni] Поле '{field}' не найдено в {target.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReferences(Object target, string field, Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Ogni] Поле '{field}' не найдено в {target.GetType().Name}.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
