using Ogni.Building;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Собирает префабы построек (порядок = BuildPieces.All) и сценовый объект BuildCatalog.
    /// Вызывается из WorldSceneBuilder.BuildWorldScene.
    /// </summary>
    public static class BuildCatalogSetup
    {
        private const string Folder = "Assets/_Project/Prefabs/Build";

        public static GameObject Create(ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Build");

            GameObject[] prefabs =
            {
                BuildWall(mats), BuildFloor(mats), BuildRoof(mats, ab), BuildBarricade(mats), BuildShelter(mats, ab),
                BuildFire(mats, "Campfire_Built", 1f, 3.2f), BuildFire(mats, "Torch_Built", 0.35f, 2.2f), BuildSpikes(mats), BuildAlarm(mats),
                // Индексы 9-12 = стройка домов (см. BuildPieces.All).
                BuildDoorwayWall(mats), BuildWindowWall(mats), BuildDoorPiece(mats), BuildStairs(mats)
            };

            GameObject catalog = new GameObject("BuildCatalog");
            BuildCatalog comp = catalog.AddComponent<BuildCatalog>();
            var so = new SerializedObject(comp);
            SerializedProperty arr = so.FindProperty("prefabs");
            arr.arraySize = prefabs.Length;
            for (int i = 0; i < prefabs.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        // ---------- Общие помощники ----------

        private static GameObject NewRoot(string name, int index, bool withPiece)
        {
            GameObject root = new GameObject(name);
            root.AddComponent<NetworkObject>();
            if (withPiece)
            {
                BuildPiece piece = root.AddComponent<BuildPiece>();
                var so = new SerializedObject(piece);
                so.FindProperty("pieceIndex").intValue = index;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return root;
        }

        private static BoxCollider AddBox(GameObject go, Vector3 center, Vector3 size)
        {
            BoxCollider c = go.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
            return c;
        }

        private static GameObject Save(GameObject root, string name)
        {
            string path = Folder + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject Tilted(Transform parent, string name, Vector3 pos, Vector3 size, Material m, Vector3 euler)
        {
            GameObject g = PropKit.Box(parent, name, pos, size, m, false);
            g.transform.localRotation = Quaternion.Euler(euler);
            return g;
        }

        // ---------- Конструкции ----------

        private static GameObject BuildWall(ProceduralMaterialLibrary mats)
        {
            GameObject root = NewRoot("Wall", 0, true);
            AddBox(root, new Vector3(0f, 1.3f, 0f), new Vector3(3f, 2.6f, 0.2f));
            for (int i = 0; i < 10; i++)
            {
                // Все доски в полную высоту стены (2.6): раньше были ниже на 0.05-0.1, и между этажами стен
                // были видны щели с небом. Неровность делаем глубиной, а не высотой.
                const float h = 2.6f;
                float d = 0.14f + (i % 3) * 0.012f;
                PropKit.Box(root.transform, "Plank" + i, new Vector3(-1.35f + i * 0.3f, h * 0.5f, 0f), new Vector3(0.28f, h, d), mats.WoodPlanks, false);
            }

            PropKit.Box(root.transform, "BeamLow", new Vector3(0f, 0.6f, 0.09f), new Vector3(3f, 0.14f, 0.06f), mats.WoodPlanks, false);
            PropKit.Box(root.transform, "BeamHigh", new Vector3(0f, 1.9f, 0.09f), new Vector3(3f, 0.14f, 0.06f), mats.WoodPlanks, false);
            Tilted(root.transform, "Brace", new Vector3(0f, 1.25f, -0.09f), new Vector3(3.1f, 0.12f, 0.05f), mats.WoodPlanks, new Vector3(0f, 0f, 38f));
            return Save(root, "Piece_Wall");
        }

        private static GameObject BuildFloor(ProceduralMaterialLibrary mats)
        {
            GameObject root = NewRoot("Floor", 1, true);
            AddBox(root, new Vector3(0f, 0.075f, 0f), new Vector3(3f, 0.15f, 3f));
            for (int i = 0; i < 6; i++)
            {
                PropKit.Box(root.transform, "Board" + i, new Vector3(0f, 0.09f, -1.25f + i * 0.5f), new Vector3(3f, 0.1f, 0.46f), mats.WoodPlanks, false);
            }

            PropKit.Box(root.transform, "JoistL", new Vector3(-1.3f, 0.03f, 0f), new Vector3(0.15f, 0.06f, 3f), mats.WoodPlanks, false);
            PropKit.Box(root.transform, "JoistR", new Vector3(1.3f, 0.03f, 0f), new Vector3(0.15f, 0.06f, 3f), mats.WoodPlanks, false);
            return Save(root, "Piece_Floor");
        }

        private static GameObject BuildRoof(ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            GameObject root = NewRoot("Roof", 2, true);
            AddBox(root, new Vector3(0f, 0.075f, 0f), new Vector3(3f, 0.15f, 3f));
            for (int i = 0; i < 6; i++)
            {
                PropKit.Box(root.transform, "Board" + i, new Vector3(0f, 0.05f, -1.25f + i * 0.5f), new Vector3(3f, 0.08f, 0.46f), mats.WoodPlanks, false);
            }

            PropKit.Box(root.transform, "MetalSheet", new Vector3(0f, 0.12f, 0f), new Vector3(3.1f, 0.04f, 3.1f), mats.RustyMetal, false);
            PropKit.Box(root.transform, "Tarp", new Vector3(0.5f, 0.15f, 0.4f), new Vector3(1.4f, 0.02f, 1.2f), ab.Fabric, false);
            return Save(root, "Piece_Roof");
        }

        private static GameObject BuildBarricade(ProceduralMaterialLibrary mats)
        {
            GameObject root = NewRoot("BarricadeBuilt", 3, true);
            AddBox(root, new Vector3(0f, 0.8f, 0f), new Vector3(2.6f, 1.6f, 0.4f));
            for (int i = 0; i < 4; i++)
            {
                PropKit.Box(root.transform, "Plank" + i, new Vector3(0f, 0.3f + i * 0.4f, 0f), new Vector3(2.6f, 0.26f, 0.14f), mats.WoodPlanks, false);
            }

            Tilted(root.transform, "CrossA", new Vector3(0f, 0.8f, 0.1f), new Vector3(2.8f, 0.14f, 0.08f), mats.WoodPlanks, new Vector3(0f, 0f, 30f));
            Tilted(root.transform, "CrossB", new Vector3(0f, 0.8f, 0.1f), new Vector3(2.8f, 0.14f, 0.08f), mats.WoodPlanks, new Vector3(0f, 0f, -30f));
            // Колья, торчащие вперёд.
            for (int i = 0; i < 4; i++)
            {
                Tilted(root.transform, "Stake" + i, new Vector3(-1.0f + i * 0.67f, 0.55f, 0.35f), new Vector3(0.07f, 0.07f, 0.7f), mats.WoodPlanks, new Vector3(-25f, 0f, 0f));
            }

            return Save(root, "Piece_Barricade");
        }

        private static GameObject BuildShelter(ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            // Навес: скат уходит вниз к задней стороне (-Z), перед открыт.
            GameObject root = NewRoot("Shelter", 4, true);
            GameObject roof = Tilted(root.transform, "RoofPanel", new Vector3(0f, 1.25f, 0f), new Vector3(2.8f, 0.08f, 2.6f), mats.WoodPlanks, new Vector3(-24f, 0f, 0f));
            roof.AddComponent<BoxCollider>();
            GameObject tarp = Tilted(root.transform, "Tarp", new Vector3(0f, 1.31f, 0f), new Vector3(3.0f, 0.03f, 2.8f), ab.Fabric, new Vector3(-24f, 0f, 0f));
            foreach (float x in new[] { -1.3f, 1.3f })
            {
                GameObject front = PropKit.Box(root.transform, "PostFront", new Vector3(x, 0.85f, 1.1f), new Vector3(0.12f, 1.7f, 0.12f), mats.WoodPlanks, true);
                GameObject back = PropKit.Box(root.transform, "PostBack", new Vector3(x, 0.4f, -1.0f), new Vector3(0.12f, 0.8f, 0.12f), mats.WoodPlanks, true);
            }

            PropKit.Box(root.transform, "BackWall", new Vector3(0f, 0.45f, -1.15f), new Vector3(2.7f, 0.9f, 0.08f), mats.WoodPlanks, true);
            PropKit.Box(root.transform, "Bedroll", new Vector3(0f, 0.07f, -0.1f), new Vector3(1.0f, 0.1f, 1.8f), ab.Fabric, false);
            return Save(root, "Piece_Shelter");
        }

        private static GameObject BuildFire(ProceduralMaterialLibrary mats, string name, float scale, float intensity)
        {
            bool torch = scale < 0.5f;
            GameObject root = NewRoot(name, torch ? 6 : 5, false);
            Campfire fire = root.AddComponent<Campfire>();

            if (torch)
            {
                PropKit.Cylinder(root.transform, "Stick", new Vector3(0f, 0.7f, 0f), new Vector3(0.05f, 0.7f, 0.05f), mats.WoodPlanks, true);
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f;
                    GameObject rock = PropKit.Box(root.transform, "Stone" + i, new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.55f, 0.1f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.55f), new Vector3(0.28f, 0.2f, 0.24f), mats.Concrete, false);
                    rock.transform.localRotation = Quaternion.Euler(0f, -a, 0f);
                }

                for (int i = 0; i < 3; i++)
                {
                    GameObject log = PropKit.Cylinder(root.transform, "Log" + i, new Vector3(0f, 0.16f, 0f), new Vector3(0.08f, 0.45f, 0.08f), mats.WoodPlanks, false);
                    log.transform.localRotation = Quaternion.Euler(80f, i * 60f, 0f);
                }

                AddBox(root, new Vector3(0f, 0.15f, 0f), new Vector3(1.0f, 0.3f, 1.0f));
            }

            GameObject flames = new GameObject("Flames");
            flames.transform.SetParent(root.transform, false);
            flames.transform.localPosition = new Vector3(0f, torch ? 1.45f : 0.25f, 0f);
            Transform[] tongues = new Transform[3];
            Material flameMat = FlameMaterial();
            for (int i = 0; i < 3; i++)
            {
                GameObject t = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                t.name = "Tongue" + i;
                Object.DestroyImmediate(t.GetComponent<Collider>());
                t.transform.SetParent(flames.transform, false);
                float s = scale * (0.5f - i * 0.1f);
                t.transform.localScale = new Vector3(s * 0.7f, s * 1.5f, s * 0.7f);
                t.transform.localPosition = new Vector3((i - 1) * 0.12f * scale, s * 0.6f, 0f);
                t.GetComponent<Renderer>().sharedMaterial = flameMat;
                t.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tongues[i] = t.transform;
            }

            GameObject lightObj = new GameObject("FireLight");
            lightObj.transform.SetParent(flames.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.4f * scale + 0.1f, 0f);
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.28f);
            light.range = torch ? 9f : 16f;
            light.intensity = intensity;
            light.shadows = torch ? LightShadows.None : LightShadows.Soft;

            var so = new SerializedObject(fire);
            so.FindProperty("fireLight").objectReferenceValue = light;
            so.FindProperty("flameVisual").objectReferenceValue = flames;
            SerializedProperty arr = so.FindProperty("flameTongues");
            arr.arraySize = tongues.Length;
            for (int i = 0; i < tongues.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = tongues[i];
            so.FindProperty("baseIntensity").floatValue = intensity;
            so.ApplyModifiedPropertiesWithoutUndo();

            return Save(root, name);
        }

        private static Material FlameMaterial()
        {
            const string path = Folder + "/Mat_Flame.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }

            Color c = new Color(1f, 0.55f, 0.15f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static GameObject BuildSpikes(ProceduralMaterialLibrary mats)
        {
            GameObject root = NewRoot("SpikeTrap", 7, true);
            TrapPiece trap = root.AddComponent<TrapPiece>();
            var so = new SerializedObject(trap);
            so.FindProperty("isAlarm").boolValue = false;
            so.FindProperty("area").vector3Value = new Vector3(1.6f, 0.6f, 1.6f);
            so.ApplyModifiedPropertiesWithoutUndo();

            PropKit.Box(root.transform, "Base", new Vector3(0f, 0.03f, 0f), new Vector3(1.6f, 0.06f, 1.6f), mats.WoodPlanks, false);
            for (int x = 0; x < 5; x++)
            {
                for (int z = 0; z < 5; z++)
                {
                    float h = 0.28f + ((x * 3 + z * 7) % 4) * 0.04f;
                    GameObject spike = PropKit.Cylinder(root.transform, "Spike", new Vector3(-0.6f + x * 0.3f, 0.06f + h * 0.5f, -0.6f + z * 0.3f), new Vector3(0.035f, h * 0.5f, 0.035f), mats.RustyMetal, false);
                    spike.transform.localRotation = Quaternion.Euler(((x + z) % 3 - 1) * 9f, 0f, ((x * 2 + z) % 3 - 1) * 9f);
                }
            }

            return Save(root, "Piece_SpikeTrap");
        }

        private static GameObject BuildAlarm(ProceduralMaterialLibrary mats)
        {
            GameObject root = NewRoot("AlarmTrap", 8, true);
            TrapPiece trap = root.AddComponent<TrapPiece>();
            var so = new SerializedObject(trap);
            so.FindProperty("isAlarm").boolValue = true;
            so.FindProperty("area").vector3Value = new Vector3(2.6f, 0.5f, 0.6f);
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (float x in new[] { -1.2f, 1.2f })
            {
                PropKit.Cylinder(root.transform, "Stake", new Vector3(x, 0.3f, 0f), new Vector3(0.04f, 0.3f, 0.04f), mats.WoodPlanks, false);
            }

            PropKit.Box(root.transform, "Wire", new Vector3(0f, 0.28f, 0f), new Vector3(2.4f, 0.012f, 0.012f), mats.RustyMetal, false);
            for (int i = 0; i < 3; i++)
            {
                PropKit.Cylinder(root.transform, "Can" + i, new Vector3(-0.5f + i * 0.5f, 0.16f, 0f), new Vector3(0.055f, 0.06f, 0.055f), mats.RustyMetal, false);
            }

            return Save(root, "Piece_AlarmTrap");
        }

        // ---------- Строительство домов ----------

        /// <summary>Вертикальные доски-плашки между x0..x1 и y0..y1 (без коллайдеров), с небольшим разбросом высоты.</summary>
        private static void PlankFill(Transform parent, string prefix, float x0, float x1, float y0, float y1, float z, Material mat)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / 0.3f));
            float w = (x1 - x0) / count;
            for (int i = 0; i < count; i++)
            {
                float jitter = ((i * 7) % 3) * 0.02f;
                float h = (y1 - y0) - jitter;
                GameObject plank = PropKit.Box(parent, prefix + i, new Vector3(x0 + (i + 0.5f) * w, y0 + h * 0.5f, z + ((i % 2) * 0.012f)),
                    new Vector3(w - 0.015f, h, 0.15f), mat, false);
                plank.transform.localRotation = Quaternion.Euler(0f, 0f, (i % 3 - 1) * 0.35f);
            }
        }

        private static void NailHeads(Transform parent, Material mat, float x, float y0, float y1, float zFront)
        {
            for (float y = y0; y <= y1 + 0.01f; y += 0.55f)
            {
                PropKit.Cylinder(parent, "Nail", new Vector3(x, y, zFront), new Vector3(0.022f, 0.006f, 0.022f), mat, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        private static GameObject BuildDoorwayWall(ProceduralMaterialLibrary mats)
        {
            GameObject root = NewRoot("DoorwayWall", 9, true);
            AddBox(root, new Vector3(-1.025f, 1.3f, 0f), new Vector3(0.95f, 2.6f, 0.2f));
            AddBox(root, new Vector3(1.025f, 1.3f, 0f), new Vector3(0.95f, 2.6f, 0.2f));
            AddBox(root, new Vector3(0f, 2.35f, 0f), new Vector3(1.1f, 0.5f, 0.2f));

            PlankFill(root.transform, "PlankL", -1.5f, -0.58f, 0f, 2.58f, 0f, mats.WoodPlanks);
            PlankFill(root.transform, "PlankR", 0.58f, 1.5f, 0f, 2.58f, 0f, mats.WoodPlanks);
            PlankFill(root.transform, "PlankTop", -0.58f, 0.58f, 2.1f, 2.58f, 0f, mats.WoodPlanks);

            // Дверная коробка: стойки, притолока, порог.
            PropKit.Box(root.transform, "JambL", new Vector3(-0.58f, 1.05f, 0f), new Vector3(0.1f, 2.1f, 0.24f), mats.WoodPlanks, false);
            PropKit.Box(root.transform, "JambR", new Vector3(0.58f, 1.05f, 0f), new Vector3(0.1f, 2.1f, 0.24f), mats.WoodPlanks, false);
            PropKit.Box(root.transform, "HeaderBeam", new Vector3(0f, 2.15f, 0f), new Vector3(1.26f, 0.12f, 0.26f), mats.WoodPlanks, false);
            PropKit.Box(root.transform, "Sill", new Vector3(0f, 0.02f, 0f), new Vector3(1.1f, 0.04f, 0.24f), mats.WoodPlanks, false);

            // Горизонтальные обвязки и диагональный подкос по бокам.
            foreach (float y in new[] { 0.5f, 1.9f })
            {
                PropKit.Box(root.transform, "BeamL", new Vector3(-1.04f, y, 0.09f), new Vector3(0.92f, 0.12f, 0.05f), mats.WoodPlanks, false);
                PropKit.Box(root.transform, "BeamR", new Vector3(1.04f, y, 0.09f), new Vector3(0.92f, 0.12f, 0.05f), mats.WoodPlanks, false);
            }

            Tilted(root.transform, "BraceL", new Vector3(-1.04f, 1.2f, -0.09f), new Vector3(1.55f, 0.1f, 0.04f), mats.WoodPlanks, new Vector3(0f, 0f, 68f));
            Tilted(root.transform, "BraceR", new Vector3(1.04f, 1.2f, -0.09f), new Vector3(1.55f, 0.1f, 0.04f), mats.WoodPlanks, new Vector3(0f, 0f, -68f));
            NailHeads(root.transform, mats.RustyMetal, -1.04f, 0.5f, 1.9f, 0.12f);
            NailHeads(root.transform, mats.RustyMetal, 1.04f, 0.5f, 1.9f, 0.12f);
            return Save(root, "Piece_DoorwayWall");
        }

        private static GameObject BuildWindowWall(ProceduralMaterialLibrary mats)
        {
            GameObject root = NewRoot("WindowWall", 10, true);
            AddBox(root, new Vector3(-1.05f, 1.3f, 0f), new Vector3(0.9f, 2.6f, 0.2f));
            AddBox(root, new Vector3(1.05f, 1.3f, 0f), new Vector3(0.9f, 2.6f, 0.2f));
            AddBox(root, new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 1.0f, 0.2f));
            AddBox(root, new Vector3(0f, 2.3f, 0f), new Vector3(1.2f, 0.6f, 0.2f));

            PlankFill(root.transform, "PlankL", -1.5f, -0.58f, 0f, 2.58f, 0f, mats.WoodPlanks);
            PlankFill(root.transform, "PlankR", 0.58f, 1.5f, 0f, 2.58f, 0f, mats.WoodPlanks);
            PlankFill(root.transform, "PlankLow", -0.58f, 0.58f, 0f, 1.0f, 0f, mats.WoodPlanks);
            PlankFill(root.transform, "PlankTop", -0.58f, 0.58f, 2.0f, 2.58f, 0f, mats.WoodPlanks);

            // Оконная рама и подоконник.
            PropKit.Box(root.transform, "FrameL", new Vector3(-0.6f, 1.5f, 0f), new Vector3(0.08f, 1.04f, 0.24f), mats.WoodPlanks, false);
            PropKit.Box(root.transform, "FrameR", new Vector3(0.6f, 1.5f, 0f), new Vector3(0.08f, 1.04f, 0.24f), mats.WoodPlanks, false);
            PropKit.Box(root.transform, "FrameTop", new Vector3(0f, 2.02f, 0f), new Vector3(1.28f, 0.08f, 0.24f), mats.WoodPlanks, false);
            PropKit.Box(root.transform, "WindowSill", new Vector3(0f, 1.02f, 0.05f), new Vector3(1.36f, 0.06f, 0.34f), mats.WoodPlanks, false);
            PropKit.Box(root.transform, "Mullion", new Vector3(0f, 1.5f, 0f), new Vector3(0.04f, 1.0f, 0.05f), mats.WoodPlanks, false);

            // Одна ставня закрыта досками, вторая висит на петле (чтобы окно выглядело обжитым).
            for (int i = 0; i < 3; i++)
            {
                GameObject board = PropKit.Box(root.transform, "ShutterBoard" + i, new Vector3(-0.3f, 1.12f + i * 0.3f, 0.12f), new Vector3(0.58f, 0.13f, 0.03f), mats.WoodPlanks, false);
                board.transform.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * 4f);
            }

            GameObject shutter = PropKit.Box(root.transform, "ShutterOpen", new Vector3(0.78f, 1.5f, 0.3f), new Vector3(0.55f, 0.92f, 0.04f), mats.WoodPlanks, false);
            shutter.transform.localRotation = Quaternion.Euler(0f, 68f, 0f);
            PropKit.Box(root.transform, "ShutterHingeTop", new Vector3(0.64f, 1.9f, 0.1f), new Vector3(0.05f, 0.04f, 0.05f), mats.RustyMetal, false);
            PropKit.Box(root.transform, "ShutterHingeLow", new Vector3(0.64f, 1.1f, 0.1f), new Vector3(0.05f, 0.04f, 0.05f), mats.RustyMetal, false);
            return Save(root, "Piece_WindowWall");
        }

        private static GameObject BuildDoorPiece(ProceduralMaterialLibrary mats)
        {
            GameObject root = NewRoot("DoorBuilt", 11, true);
            BuiltDoor door = root.AddComponent<BuiltDoor>();

            // Петли слева: pivot на краю проёма, полотно уходит вправо.
            GameObject pivot = new GameObject("DoorPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(-0.52f, 0f, 0f);

            GameObject panel = PropKit.Box(pivot.transform, "DoorPanel", new Vector3(0.52f, 1.025f, 0f), new Vector3(1.04f, 2.05f, 0.07f), mats.WoodPlanks, true);
            for (int i = 0; i < 5; i++)
            {
                PropKit.Box(pivot.transform, "Board" + i, new Vector3(0.1f + i * 0.21f, 1.025f, 0.045f), new Vector3(0.195f, 2.0f, 0.03f), mats.WoodPlanks, false);
            }

            PropKit.Box(pivot.transform, "LedgeTop", new Vector3(0.52f, 1.75f, 0.07f), new Vector3(0.95f, 0.13f, 0.03f), mats.WoodPlanks, false);
            PropKit.Box(pivot.transform, "LedgeLow", new Vector3(0.52f, 0.3f, 0.07f), new Vector3(0.95f, 0.13f, 0.03f), mats.WoodPlanks, false);
            Tilted(pivot.transform, "Diagonal", new Vector3(0.52f, 1.02f, 0.075f), new Vector3(1.6f, 0.11f, 0.03f), mats.WoodPlanks, new Vector3(0f, 0f, 41f));

            foreach (float y in new[] { 0.35f, 1.0f, 1.7f })
            {
                PropKit.Box(pivot.transform, "HingeStrap", new Vector3(0.12f, y, 0.095f), new Vector3(0.24f, 0.05f, 0.02f), mats.RustyMetal, false);
                PropKit.Cylinder(pivot.transform, "HingePin", new Vector3(0.0f, y, 0f), new Vector3(0.035f, 0.045f, 0.035f), mats.RustyMetal, false);
            }

            // Ручка и засов.
            PropKit.Box(pivot.transform, "HandlePlate", new Vector3(0.92f, 1.0f, 0.095f), new Vector3(0.07f, 0.2f, 0.015f), mats.RustyMetal, false);
            PropKit.Cylinder(pivot.transform, "Knob", new Vector3(0.92f, 1.0f, 0.12f), new Vector3(0.045f, 0.03f, 0.045f), mats.RustyMetal, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PropKit.Box(pivot.transform, "LatchBar", new Vector3(0.80f, 1.25f, 0.1f), new Vector3(0.3f, 0.03f, 0.02f), mats.RustyMetal, false);

            var so = new SerializedObject(door);
            so.FindProperty("doorPivot").objectReferenceValue = pivot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, "Piece_Door");
        }

        private static GameObject BuildStairs(ProceduralMaterialLibrary mats)
        {
            // 10 ступеней по 0.275 м (проходятся CharacterController со stepOffset 0.3), подъём вдоль +Z.
            GameObject root = NewRoot("Stairs", 12, true);
            const int steps = 10;
            const float rise = 2.75f / steps;
            const float run = 3f / steps;
            for (int i = 0; i < steps; i++)
            {
                float height = (i + 1) * rise;
                PropKit.Box(root.transform, "Step" + i, new Vector3(0f, height * 0.5f, -1.5f + (i + 0.5f) * run),
                    new Vector3(2.8f, height, run), mats.WoodPlanks, true);
                PropKit.Box(root.transform, "Tread" + i, new Vector3(0f, height + 0.012f, -1.5f + (i + 0.5f) * run + 0.015f),
                    new Vector3(2.8f, 0.03f, run + 0.04f), mats.PlankCut, false);
            }

            float slope = Mathf.Atan2(2.75f, 3f) * Mathf.Rad2Deg;
            float len = Mathf.Sqrt(2.75f * 2.75f + 9f);
            foreach (float x in new[] { -1.45f, 1.45f })
            {
                Tilted(root.transform, "Stringer", new Vector3(x, 1.375f, 0f), new Vector3(0.1f, 0.26f, len), mats.WoodPlanks, new Vector3(-slope, 0f, 0f));
                for (int p = 0; p < 3; p++)
                {
                    float t = p / 2f;
                    float pz = -1.35f + 2.7f * t;
                    float surface = 2.75f * (pz + 1.5f) / 3f;
                    PropKit.Box(root.transform, "RailPost", new Vector3(x, surface + 0.45f, pz), new Vector3(0.07f, 0.9f, 0.07f), mats.WoodPlanks, false);
                }

                Tilted(root.transform, "Rail", new Vector3(x, 1.375f + 0.9f, 0f), new Vector3(0.07f, 0.07f, len), mats.WoodPlanks, new Vector3(-slope, 0f, 0f));
            }

            return Save(root, "Piece_Stairs");
        }
    }
}
