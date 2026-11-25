using Ogni.Building;
using Ogni.World;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Стеклянные высотки (витражный фасад): бетонные угловые колонны, тёмные парапетные пояса на каждом этаже,
    /// стеклянные панели между импостами, бетонное ядро и плиты перекрытий, видимые сквозь стекло.
    /// Каждая панель - <see cref="BreakableWindow"/>: пуля, дробь, стрела или удар разбивает стекло, летят осколки,
    /// а сервер шлёт шум в NoiseBus. Нижний этаж - проходимый холл с дверью, ресепшеном и разбитым стеклом у входа.
    ///
    /// Варианты формы (параметр variant): 0 - классический блок 16x12; 1 - широкая плита 20x11;
    /// 2 - узкая башня 13x13 с уступом (верхние 3 этажа уже); 3 - ступенчатая 18x12 с верхней третью 12x9.
    /// Заброшенный вид: чем выше этаж, тем больше выбитых панелей, плюс «пробоины» 2x2 панели, оборванные шторы,
    /// офисная мебель за стеклом, подтёки и плющ на фасаде, освещённые этажи с мерцанием и красный огонь на крыше.
    /// </summary>
    public static class GlassTowerBuilder
    {
        // Размеры базового варианта (оставлены для совместимости).
        public const float Width = 16f;
        public const float Depth = 12f;
        public const float FloorHeight = 3.4f;

        private const float SlabThickness = 0.3f;
        private const float GroundTop = 0.08f;
        private const float CornerSize = 0.5f;
        private const float FacePlaneInset = 0.15f;
        private const float BandHeight = 0.85f;
        private const float PreBrokenChance = 0.05f;
        private const float TargetModuleWidth = 2.6f;

        private static int _windowCounter;

        /// <summary>Размеры одной высотки: нижний ярус W x D, верхний ярус (после уступа) UpperW x UpperD.</summary>
        private sealed class Dims
        {
            public float W, D, UpperW, UpperD;
            public int Floors, TierFloor;
            public bool HasSetback => TierFloor < Floors;
        }

        /// <summary>Вызывать один раз перед постройкой всех высоток мира (id окон уникальны на весь мир).</summary>
        public static void ResetWindowIds() => _windowCounter = 0;

        private static float FloorTop(int f) => f == 0 ? GroundTop : f * FloorHeight;

        private static int ModulesFor(float length, bool forceEven)
        {
            int m = Mathf.Max(3, Mathf.RoundToInt((length - CornerSize) / TargetModuleWidth));
            if (forceEven && m % 2 == 1) m++;
            return m;
        }

        private static Dims MakeDims(int variant, int floors)
        {
            var d = new Dims { Floors = floors, TierFloor = floors, W = Width, D = Depth };
            switch (variant)
            {
                case 1:
                    d.W = 20f; d.D = 11f;
                    break;
                case 2:
                    d.W = 13f; d.D = 13f; d.UpperW = 9f; d.UpperD = 9f;
                    if (floors > 5) d.TierFloor = floors - 3;
                    break;
                case 3:
                    d.W = 18f; d.D = 12f; d.UpperW = 12f; d.UpperD = 9f;
                    if (floors > 5) d.TierFloor = Mathf.Max(3, floors * 2 / 3);
                    break;
            }

            return d;
        }

        public static GameObject Build(
            Transform parent, string name, Vector3 position, float yaw, int floors, int seed,
            ProceduralMaterialLibrary mats, AbandonedMaterials ab, GameObject doorPrefab, int variant = 0)
        {
            var rng = new System.Random(seed);
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Transform t = root.transform;

            Dims d = MakeDims(variant, floors);
            float totalH = floors * FloorHeight;
            float tierTop = d.HasSetback ? FloorTop(d.TierFloor) : totalH;
            float hw = d.W * 0.5f;
            float hd = d.D * 0.5f;

            // Фундамент-цоколь и пол холла.
            PropKit.Box(t, "LobbyFloor", new Vector3(0f, GroundTop - 0.15f, 0f), new Vector3(d.W + 0.6f, 0.3f, d.D + 0.6f), mats.Stone);
            ApartmentBlockBuilder.Decal(t, ab.Linoleum, new Vector3(0f, GroundTop, 0f), Vector3.up, new Vector2(d.W - 1f, d.D - 1f), 0f, true);

            // Бетонное ядро (лестницы/шахты) проходит через все ярусы.
            PropKit.Box(t, "Core", new Vector3(0f, totalH * 0.5f, -1.2f), new Vector3(6f, totalH, 4.6f), mats.Concrete);

            // Колонны и угловые стойки нижнего яруса, затем верхнего.
            BuildColumns(t, mats, hw, hd, 0f, tierTop);
            BuildCornerColumns(t, mats, hw, hd, 0f, tierTop);
            if (d.HasSetback)
            {
                BuildColumns(t, mats, d.UpperW * 0.5f, d.UpperD * 0.5f, tierTop, totalH);
                BuildCornerColumns(t, mats, d.UpperW * 0.5f, d.UpperD * 0.5f, tierTop, totalH);
            }

            // Перекрытия: каждая плита видна сквозь стекло.
            for (int f = 1; f <= floors; f++)
            {
                bool upper = d.HasSetback && f > d.TierFloor;
                float sw = upper ? d.UpperW : d.W;
                float sd = upper ? d.UpperD : d.D;
                PropKit.Box(t, "Slab_" + f, new Vector3(0f, FloorTop(f) - SlabThickness * 0.5f, 0f),
                    new Vector3(sw - CornerSize, SlabThickness, sd - CornerSize), mats.Concrete);
            }

            // Фасады нижнего яруса (и верхнего после уступа).
            int tierEnd = d.TierFloor;
            BuildFacades(t, d.W, d.D, 0, tierEnd, floors, true, mats, rng);
            if (d.HasSetback) BuildFacades(t, d.UpperW, d.UpperD, d.TierFloor, floors, floors, false, mats, rng);

            BuildEntrance(t, d, mats, ab, doorPrefab);
            BuildLobby(t, d, mats, ab, rng);
            BuildInterior(t, d, mats, ab, rng);
            BuildWeathering(t, d, tierTop, mats, ab, rng);

            if (d.HasSetback)
            {
                // Крыша нижнего яруса - терраса с парапетом, верхний ярус стоит на ней.
                BuildRoof(t, d.W, d.D, FloorTop(d.TierFloor), false, mats, rng);
            }

            float topW = d.HasSetback ? d.UpperW : d.W;
            float topD = d.HasSetback ? d.UpperD : d.D;
            BuildRoof(t, topW, topD, FloorTop(floors), true, mats, rng);

            SetStatic(root);
            return root;
        }

        private static void BuildColumns(Transform t, ProceduralMaterialLibrary mats, float hw, float hd, float yFrom, float yTo)
        {
            float cx = Mathf.Min(5.2f, hw - 1.2f);
            float zF = hd - 2.8f;
            float zB = -(hd - 2.2f);
            float h = yTo - yFrom;
            foreach (Vector3 c in new[] { new Vector3(-cx, 0f, zF), new Vector3(cx, 0f, zF), new Vector3(-cx, 0f, zB), new Vector3(cx, 0f, zB) })
            {
                PropKit.Box(t, "Column", new Vector3(c.x, yFrom + h * 0.5f, c.z), new Vector3(0.5f, h, 0.5f), mats.Concrete);
            }
        }

        private static void BuildCornerColumns(Transform t, ProceduralMaterialLibrary mats, float hw, float hd, float yFrom, float yTo)
        {
            float h = yTo - yFrom;
            foreach (Vector2 c in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            {
                PropKit.Box(t, "CornerColumn", new Vector3(c.x * (hw - CornerSize * 0.5f + 0.05f), yFrom + h * 0.5f, c.y * (hd - CornerSize * 0.5f + 0.05f)),
                    new Vector3(CornerSize, h, CornerSize), mats.Concrete);
            }
        }

        private static void BuildFacades(
            Transform t, float w, float dpt, int fromFloor, int toFloor, int totalFloors, bool withEntrance,
            ProceduralMaterialLibrary mats, System.Random rng)
        {
            float hw = w * 0.5f;
            float hd = dpt * 0.5f;
            BuildFace(t, "Front", new Vector3(0f, 0f, hd - FacePlaneInset), Vector3.forward, Vector3.right, w, fromFloor, toFloor, totalFloors, withEntrance, mats, rng);
            BuildFace(t, "Back", new Vector3(0f, 0f, -hd + FacePlaneInset), Vector3.back, Vector3.left, w, fromFloor, toFloor, totalFloors, false, mats, rng);
            BuildFace(t, "Right", new Vector3(hw - FacePlaneInset, 0f, 0f), Vector3.right, Vector3.back, dpt, fromFloor, toFloor, totalFloors, false, mats, rng);
            BuildFace(t, "Left", new Vector3(-hw + FacePlaneInset, 0f, 0f), Vector3.left, Vector3.forward, dpt, fromFloor, toFloor, totalFloors, false, mats, rng);
        }

        private static void BuildFace(
            Transform parent, string name, Vector3 center, Vector3 outward, Vector3 along, float length,
            int fromFloor, int toFloor, int totalFloors, bool hasEntrance,
            ProceduralMaterialLibrary mats, System.Random rng)
        {
            GameObject faceRoot = new GameObject("Facade_" + name + (fromFloor > 0 ? "_Upper" : ""));
            faceRoot.transform.SetParent(parent, false);
            Transform ft = faceRoot.transform;

            bool alongX = Mathf.Abs(along.x) > 0.5f;
            int modules = ModulesFor(length, hasEntrance);
            float usable = length - CornerSize;
            float module = usable / modules;
            float tierTop = toFloor * FloorHeight;
            float yStart = fromFloor == 0 ? 0f : FloorTop(fromFloor) - SlabThickness;
            Material glass = mats.TowerGlass;

            // Импосты (вертикальные стойки) между панелями.
            for (int k = 1; k < modules; k++)
            {
                float s = -usable * 0.5f + k * module;
                // Центральный импост над входом начинается выше проёма (иначе он перегородил бы дверь).
                float yFrom = (hasEntrance && fromFloor == 0 && k == modules / 2) ? FloorTop(1) - SlabThickness : yStart;
                float h = tierTop - yFrom;
                Vector3 p = center + along * s + Vector3.up * (yFrom + h * 0.5f) + outward * 0.02f;
                Vector3 size = alongX ? new Vector3(0.14f, h, 0.24f) : new Vector3(0.24f, h, 0.14f);
                PropKit.Box(ft, "Mullion", p, size, mats.TowerFrame);
            }

            // Какие панели выбиты заранее: чем выше, тем чаще, плюс одна «пробоина» 2x2.
            bool[,] broken = new bool[toFloor + 1, modules];
            int firstUpper = Mathf.Max(1, fromFloor);
            for (int f = firstUpper; f < toFloor; f++)
            {
                float chance = PreBrokenChance + 0.10f * ((float)f / Mathf.Max(1, totalFloors));
                for (int k = 0; k < modules; k++) broken[f, k] = rng.NextDouble() < chance;
            }

            if (toFloor - firstUpper >= 2 && modules >= 3 && rng.NextDouble() < 0.7)
            {
                int f0 = rng.Next(firstUpper, toFloor - 1);
                int k0 = rng.Next(0, modules - 1);
                for (int df = 0; df < 2; df++)
                {
                    for (int dk = 0; dk < 2; dk++) broken[f0 + df, k0 + dk] = true;
                }
            }

            for (int f = fromFloor; f < toFloor; f++)
            {
                float yTop = FloorTop(f);
                float yNext = FloorTop(f + 1);
                bool lobby = f == 0;

                // Парапетный пояс на уровне перекрытия (тёмный металл).
                if (!lobby)
                {
                    float bandBottom = yTop - SlabThickness;
                    Vector3 bp = center + Vector3.up * (bandBottom + BandHeight * 0.5f) + outward * 0.03f;
                    Vector3 bs = alongX ? new Vector3(usable, BandHeight, 0.3f) : new Vector3(0.3f, BandHeight, usable);
                    PropKit.Box(ft, "Band_" + f, bp, bs, mats.TowerFrame);
                }

                float paneBottom = lobby ? yTop + 0.02f : yTop - SlabThickness + BandHeight;
                float paneTop = yNext - SlabThickness - 0.03f;
                float paneH = paneTop - paneBottom;
                if (paneH < 0.3f) continue;

                for (int k = 0; k < modules; k++)
                {
                    // Над входом (две центральные панели нижнего этажа) стекла нет - там портал.
                    if (hasEntrance && lobby && (k == modules / 2 - 1 || k == modules / 2)) continue;

                    float s = -usable * 0.5f + (k + 0.5f) * module;
                    Vector3 p = center + along * s + Vector3.up * (paneBottom + paneH * 0.5f);

                    if (!lobby && broken[f, k])
                    {
                        // Оборванная штора в проёме выбитой панели.
                        if (rng.NextDouble() < 0.35)
                        {
                            float cw = module * 0.35f;
                            float ch = paneH * (0.35f + (float)rng.NextDouble() * 0.4f);
                            Vector3 cp = center + along * (s + ((float)rng.NextDouble() - 0.5f) * module * 0.4f)
                                         + Vector3.up * (paneTop - ch * 0.5f) - outward * 0.35f;
                            Vector3 cs = alongX ? new Vector3(cw, ch, 0.03f) : new Vector3(0.03f, ch, cw);
                            PropKit.Box(ft, "TornCurtain", cp, cs, mats.ClothRoll, false);
                        }

                        continue;
                    }

                    float pw = module - 0.16f;
                    Vector3 size = alongX ? new Vector3(pw, paneH, 0.06f) : new Vector3(0.06f, paneH, pw);
                    CreatePane(ft, p, size, glass);
                }
            }
        }

        private static void CreatePane(Transform parent, Vector3 pos, Vector3 size, Material glass)
        {
            int id = _windowCounter++;
            GameObject pane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pane.name = "Pane_" + id;
            pane.transform.SetParent(parent, false);
            pane.transform.localPosition = pos;
            pane.transform.localScale = size;

            Renderer r = pane.GetComponent<Renderer>();
            r.sharedMaterial = glass;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            BreakableWindow window = pane.AddComponent<BreakableWindow>();
            var so = new SerializedObject(window);
            so.FindProperty("windowId").intValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildEntrance(Transform t, Dims dm, ProceduralMaterialLibrary mats, AbandonedMaterials ab, GameObject doorPrefab)
        {
            float hd = dm.D * 0.5f;
            int frontModules = ModulesFor(dm.W, true);
            float usable = dm.W - CornerSize;
            float module = usable / frontModules;
            float half = module; // два центральных модуля: от -module до +module
            float zWall = hd - FacePlaneInset;
            float doorHalf = 0.78f;                   // проём 1.56 м под дверь шириной 1.4 м
            float doorTop = 2.2f;
            float slabBottom = FloorTop(1) - SlabThickness;

            float sideW = half - doorHalf;
            float wallH = slabBottom - GroundTop;
            PropKit.Box(t, "EntranceWallL", new Vector3(-(doorHalf + sideW * 0.5f), GroundTop + wallH * 0.5f, zWall), new Vector3(sideW, wallH, 0.3f), mats.Concrete);
            PropKit.Box(t, "EntranceWallR", new Vector3(doorHalf + sideW * 0.5f, GroundTop + wallH * 0.5f, zWall), new Vector3(sideW, wallH, 0.3f), mats.Concrete);
            float headerH = slabBottom - doorTop;
            PropKit.Box(t, "EntranceHeader", new Vector3(0f, doorTop + headerH * 0.5f, zWall), new Vector3(doorHalf * 2f, headerH, 0.3f), mats.Concrete);

            // Козырёк и подсветка входа.
            PropKit.Box(t, "EntranceCanopy", new Vector3(0f, 2.75f, hd + 0.5f), new Vector3(4.6f, 0.16f, 1.6f), mats.TowerFrame, false);
            PropKit.Box(t, "CanopyPostL", new Vector3(-2.2f, 1.4f, hd + 1.15f), new Vector3(0.12f, 2.8f, 0.12f), mats.TowerFrame, false);
            PropKit.Box(t, "CanopyPostR", new Vector3(2.2f, 1.4f, hd + 1.15f), new Vector3(0.12f, 2.8f, 0.12f), mats.TowerFrame, false);
            PropKit.Box(t, "EntranceStep", new Vector3(0f, 0.04f, hd + 0.6f), new Vector3(4.0f, 0.08f, 1.4f), mats.Concrete);

            if (doorPrefab != null)
            {
                GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
                door.transform.SetParent(t, false);
                door.transform.localPosition = new Vector3(0f, GroundTop, zWall);
            }
        }

        private static void BuildLobby(Transform t, Dims dm, ProceduralMaterialLibrary mats, AbandonedMaterials ab, System.Random rng)
        {
            float hd = dm.D * 0.5f;

            // Ресепшен, ящики и мусор: холл не пустой, но проход свободен.
            PropKit.Box(t, "Reception", new Vector3(-4.2f, 0.58f, 3.4f), new Vector3(3.4f, 1.0f, 0.8f), ab.WoodDark);
            PropKit.Box(t, "ReceptionTop", new Vector3(-4.2f, 1.1f, 3.4f), new Vector3(3.6f, 0.08f, 1.0f), mats.Stone, false);
            PropKit.Box(t, "LobbyCrateA", new Vector3(5.0f, 0.55f, 4.2f), new Vector3(1.1f, 0.95f, 1.1f), mats.WoodPlanks);
            PropKit.Box(t, "LobbyCrateB", new Vector3(5.2f, 0.4f, 2.9f), new Vector3(0.8f, 0.65f, 0.8f), mats.WoodPlanks);
            PropKit.Box(t, "LobbyBench", new Vector3(3.8f, 0.3f, -(hd - 1.3f)), new Vector3(2.2f, 0.45f, 0.6f), ab.WoodDark);
            for (int i = 0; i < 9; i++)
            {
                float x = (float)(rng.NextDouble() * 12.0 - 6.0);
                float z = (float)(rng.NextDouble() * 4.5 + 1.5);
                GameObject chunk = PropKit.Box(t, "Rubble", new Vector3(x, 0.16f, z),
                    new Vector3(0.2f + (float)rng.NextDouble() * 0.3f, 0.12f + (float)rng.NextDouble() * 0.16f, 0.2f + (float)rng.NextDouble() * 0.3f), mats.Concrete, false);
                chunk.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, (float)rng.NextDouble() * 20f);
            }

            // Осколки стекла у входа внутри и снаружи.
            for (int i = 0; i < 16; i++)
            {
                bool outside = i % 2 == 0;
                float x = (float)(rng.NextDouble() * 9.0 - 4.5);
                float z = outside ? hd + 0.4f + (float)rng.NextDouble() * 3.2f : hd - 2.4f + (float)rng.NextDouble() * 1.8f;
                float s = 0.12f + (float)rng.NextDouble() * 0.34f;
                GameObject shard = PropKit.Box(t, "GlassShard", new Vector3(x, (outside ? 0.02f : GroundTop) + 0.015f, z), new Vector3(s, 0.02f, s * 0.6f), mats.TowerGlass, false);
                shard.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f);
            }

            ApartmentBlockBuilder.Decal(t, ab.DarkStainDecal, new Vector3(1f, GroundTop + 0.01f, 1.8f), Vector3.up, new Vector2(3.5f, 3f), 20f, true);
            ApartmentBlockBuilder.Decal(t, ab.PaperDecal, new Vector3(-1.5f, GroundTop + 0.02f, 3.0f), Vector3.up, new Vector2(2f, 1.6f), 70f, true);

            GameObject lightObj = new GameObject("LobbyLight");
            lightObj.transform.SetParent(t, false);
            lightObj.transform.localPosition = new Vector3(0f, 2.7f, 2.2f);
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 10f;
            l.intensity = 1.3f;
            l.color = new Color(0.72f, 0.84f, 1f);
            lightObj.AddComponent<FlickerLight>();

            // Точка лута в холле: на столе ресепшена (RuntimeWorldGenerator ставит туда предмет с шансом).
            GameObject loot = new GameObject("LootPoint_TowerLobby");
            loot.transform.SetParent(t, false);
            loot.transform.localPosition = new Vector3(-4.2f, 1.14f, 3.4f);
        }

        /// <summary>Офисная обстановка за стеклом верхних этажей: столы, перегородки, стулья, потолочные панели, пара освещённых этажей.</summary>
        private static void BuildInterior(Transform t, Dims dm, ProceduralMaterialLibrary mats, AbandonedMaterials ab, System.Random rng)
        {
            var kit = new PropKit(ab, mats, rng);
            int litA = dm.Floors > 4 ? rng.Next(2, dm.Floors - 1) : -1;
            int litB = dm.Floors > 8 ? rng.Next(2, dm.Floors - 1) : -1;

            for (int f = 1; f < dm.Floors; f++)
            {
                bool upper = dm.HasSetback && f >= dm.TierFloor;
                float w = upper ? dm.UpperW : dm.W;
                float dp = upper ? dm.UpperD : dm.D;
                float hd = dp * 0.5f;
                float y0 = FloorTop(f);
                int desks = 3 + rng.Next(0, 4);

                for (int i = 0; i < desks; i++)
                {
                    bool front = rng.NextDouble() < 0.5;
                    float x = (float)((rng.NextDouble() - 0.5) * (w - 3.4f));
                    float zMin = front ? 2.0f : -(hd - 1.0f);
                    float zMax = front ? hd - 1.0f : -3.9f;
                    if (zMax <= zMin) continue;
                    float z = zMin + (float)rng.NextDouble() * (zMax - zMin);
                    // Центральное ядро занимает x в [-3, 3], z в [-3.5, 1.1].
                    if (Mathf.Abs(x) < 3.4f && z > -4.0f && z < 1.5f) continue;

                    float yaw = rng.Next(0, 2) * 90f + (float)(rng.NextDouble() - 0.5) * 12f;
                    GameObject desk = new GameObject("OfficeDesk");
                    desk.transform.SetParent(t, false);
                    desk.transform.localPosition = new Vector3(x, y0, z);
                    desk.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    PropKit.Box(desk.transform, "Top", new Vector3(0f, 0.74f, 0f), new Vector3(1.6f, 0.05f, 0.8f), ab.WoodDark, false);
                    PropKit.Box(desk.transform, "PanelL", new Vector3(-0.72f, 0.36f, 0f), new Vector3(0.05f, 0.72f, 0.7f), ab.WoodDark, false);
                    PropKit.Box(desk.transform, "PanelR", new Vector3(0.72f, 0.36f, 0f), new Vector3(0.05f, 0.72f, 0.7f), ab.WoodDark, false);
                    if (rng.NextDouble() < 0.6) PropKit.Box(desk.transform, "Monitor", new Vector3(0.2f, 0.98f, -0.15f), new Vector3(0.5f, 0.32f, 0.05f), ab.Plastic, false);
                    kit.Chair(desk.transform, new Vector3(0f, 0f, 0.75f), 180f, rng.NextDouble() < 0.35);
                }

                if (rng.NextDouble() < 0.7)
                {
                    float px = (float)((rng.NextDouble() - 0.5) * (w - 4f));
                    float pz = rng.NextDouble() < 0.5 ? hd - 2.4f : -(hd - 1.8f);
                    if (!(Mathf.Abs(px) < 3.4f && pz > -4.0f && pz < 1.5f))
                    {
                        GameObject part = PropKit.Box(t, "OfficePartition", new Vector3(px, y0 + 0.7f, pz), new Vector3(0.05f, 1.4f, 2.2f), mats.TowerFrame, false);
                        part.transform.localRotation = Quaternion.Euler(0f, rng.Next(0, 2) * 90f, 0f);
                    }
                }

                // Провисшая потолочная панель.
                if (rng.NextDouble() < 0.45)
                {
                    float cx = (float)((rng.NextDouble() - 0.5) * (w - 4f));
                    float cz = (float)((rng.NextDouble() - 0.5) * (dp - 3f));
                    if (!(Mathf.Abs(cx) < 3.4f && cz > -4.0f && cz < 1.5f))
                    {
                        GameObject tile = PropKit.Box(t, "CeilingTile", new Vector3(cx, FloorTop(f + 1) - SlabThickness - 0.35f, cz), new Vector3(1.2f, 0.04f, 0.6f), mats.Concrete, false);
                        tile.transform.localRotation = Quaternion.Euler((float)(rng.NextDouble() * 40.0 - 20.0), (float)(rng.NextDouble() * 90.0), 0f);
                    }
                }

                // Освещённые этажи: холодный мерцающий свет видно издалека.
                if (f == litA || f == litB)
                {
                    GameObject lightObj = new GameObject("OfficeLight_" + f);
                    lightObj.transform.SetParent(t, false);
                    lightObj.transform.localPosition = new Vector3(0f, y0 + 2.3f, hd * 0.35f);
                    Light l = lightObj.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 13f;
                    l.intensity = 1.1f;
                    l.color = new Color(0.72f, 0.9f, 0.95f);
                    l.shadows = LightShadows.None;
                    lightObj.AddComponent<FlickerLight>();
                }
            }
        }

        /// <summary>Подтёки, плющ и потёки ржавчины на фасаде нижнего яруса.</summary>
        private static void BuildWeathering(Transform t, Dims dm, float tierTop, ProceduralMaterialLibrary mats, AbandonedMaterials ab, System.Random rng)
        {
            float hw = dm.W * 0.5f;
            float hd = dm.D * 0.5f;
            bool ivyReady = ab.Vine != null && ab.Vine.HasProperty("_AlphaClip") && ab.Vine.GetFloat("_AlphaClip") > 0.5f;
            float maxH = Mathf.Min(tierTop - 1f, 11f);

            int strands = 4 + rng.Next(0, 4);
            for (int i = 0; i < strands; i++)
            {
                int face = rng.Next(0, 4);
                float h = 3f + (float)rng.NextDouble() * Mathf.Max(0.5f, maxH - 3f);
                float wdt = 1.8f + (float)rng.NextDouble() * 1.8f;
                float roll = (float)(rng.NextDouble() * 16.0 - 8.0);
                Material m = (ivyReady && i % 2 == 0) ? ab.Vine : ab.DarkStainDecal;
                if (m == null) continue;

                float yc = (ivyReady && i % 2 == 0) ? h * 0.5f + 0.2f : tierTop - h * 0.5f - 0.6f;
                float along = (float)((rng.NextDouble() - 0.5) * 2.0);
                switch (face)
                {
                    case 0:
                        {
                            float x = along * (hw - 2.5f);
                            if (Mathf.Abs(x) < 2.8f) x = (x < 0f ? -1f : 1f) * 3.4f; // не закрываем вход
                            ApartmentBlockBuilder.Decal(t, m, new Vector3(x, yc, hd + 0.06f), Vector3.forward, new Vector2(wdt, h), roll);
                            break;
                        }
                    case 1:
                        ApartmentBlockBuilder.Decal(t, m, new Vector3(along * (hw - 2.5f), yc, -hd - 0.06f), Vector3.back, new Vector2(wdt, h), roll);
                        break;
                    case 2:
                        ApartmentBlockBuilder.Decal(t, m, new Vector3(hw + 0.06f, yc, along * (hd - 2f)), Vector3.right, new Vector2(wdt, h), roll);
                        break;
                    default:
                        ApartmentBlockBuilder.Decal(t, m, new Vector3(-hw - 0.06f, yc, along * (hd - 2f)), Vector3.left, new Vector2(wdt, h), roll);
                        break;
                }
            }
        }

        private static void BuildRoof(Transform t, float w, float dpt, float y, bool equipment, ProceduralMaterialLibrary mats, System.Random rng)
        {
            float hw = w * 0.5f;
            float hd = dpt * 0.5f;
            PropKit.Box(t, "RoofSlab", new Vector3(0f, y + 0.08f, 0f), new Vector3(w + 0.4f, 0.16f, dpt + 0.4f), mats.Concrete);
            PropKit.Box(t, "ParapetF", new Vector3(0f, y + 0.55f, hd + 0.1f), new Vector3(w + 0.4f, 0.9f, 0.25f), mats.Concrete, false);
            PropKit.Box(t, "ParapetB", new Vector3(0f, y + 0.55f, -hd - 0.1f), new Vector3(w + 0.4f, 0.9f, 0.25f), mats.Concrete, false);
            PropKit.Box(t, "ParapetL", new Vector3(-hw - 0.1f, y + 0.55f, 0f), new Vector3(0.25f, 0.9f, dpt + 0.4f), mats.Concrete, false);
            PropKit.Box(t, "ParapetR", new Vector3(hw + 0.1f, y + 0.55f, 0f), new Vector3(0.25f, 0.9f, dpt + 0.4f), mats.Concrete, false);
            if (!equipment) return;

            // Оборудование на крыше. Позиции масштабируются под размер яруса (эталон - 16x12).
            float sx = hw / 8f;
            float sz = hd / 6f;
            Vector3 machine = new Vector3(-2.4f * sx, 0f, -1.2f * sz);
            PropKit.Box(t, "RoofMachineRoom", new Vector3(machine.x, y + 1.5f, machine.z), new Vector3(5f, 2.8f, 4f), mats.Concrete, false);
            PropKit.Box(t, "RoofVent", new Vector3(4.2f * sx, y + 0.9f, 2.4f * sz), new Vector3(2.2f, 1.4f, 2.2f), mats.RustyMetal, false);
            GameObject tank = PropKit.Cylinder(t, "WaterTank", new Vector3(4.6f * sx, y + 1.4f, -2.8f * sz), new Vector3(2.4f, 1.2f, 2.4f), mats.RustyMetal);
            tank.transform.localRotation = Quaternion.identity;
            GameObject mast = PropKit.Cylinder(t, "Antenna", new Vector3(machine.x, y + 5.2f, machine.z), new Vector3(0.1f, 2.4f, 0.1f), mats.RustyMetal);
            mast.transform.localRotation = Quaternion.Euler((float)(rng.NextDouble() * 8.0 - 4.0), 0f, (float)(rng.NextDouble() * 8.0 - 4.0));

            // Красный сигнальный огонь на мачте.
            GameObject beacon = new GameObject("RoofBeacon");
            beacon.transform.SetParent(t, false);
            beacon.transform.localPosition = new Vector3(machine.x, y + 7.75f, machine.z);
            Light bl = beacon.AddComponent<Light>();
            bl.type = LightType.Point;
            bl.range = 16f;
            bl.color = new Color(1f, 0.12f, 0.06f);
            bl.intensity = 0f;
            bl.shadows = LightShadows.None;
            GameObject lamp = PropKit.Box(beacon.transform, "BeaconLamp", Vector3.zero, new Vector3(0.28f, 0.28f, 0.28f), mats.Ember, false);
            var blink = beacon.AddComponent<BlinkLight>();
            var so = new SerializedObject(blink);
            SerializedProperty lampProp = so.FindProperty("lamp");
            if (lampProp != null) lampProp.objectReferenceValue = lamp.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // Спутниковая тарелка и упавшая секция ограждения.
            GameObject dish = PropKit.Cylinder(t, "SatDish", new Vector3(-4.6f * sx, y + 0.9f, 3.0f * sz), new Vector3(1.6f, 0.06f, 1.6f), mats.Concrete);
            dish.transform.localRotation = Quaternion.Euler(-48f, 25f, 0f);
            GameObject fallen = PropKit.Box(t, "FallenRailing", new Vector3(1.0f * sx, y + 0.3f, hd - 0.4f), new Vector3(2.4f, 0.8f, 0.05f), mats.RustyMetal, false);
            fallen.transform.localRotation = Quaternion.Euler(70f, 8f, 0f);
        }

        private static void SetStatic(GameObject root)
        {
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.GetComponent<Light>() != null) continue;
                if (child.name == "BeaconLamp") continue;                            // мигает через MaterialPropertyBlock
                if (child.GetComponentInParent<BreakableWindow>() != null) continue; // стёкла бьются - не статика
                if (child.GetComponentInParent<Door>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
            }
        }
    }
}
