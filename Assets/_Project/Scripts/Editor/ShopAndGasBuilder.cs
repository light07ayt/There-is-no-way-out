using Ogni.World;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Реалистичная обстановка заправок (АЗС), полноценные продуктовые магазины,
    /// наполнение клиники/автосервиса/домов и элементы хоррора и постапокалипсиса на улицах.
    /// </summary>
    public static class ShopAndGasBuilder
    {
        // ------------------------------------------------------------------ 1. ЗАПРАВКА (АЗС)

        /// <summary>
        /// Детальный интерьер киоска АЗС (размер 7x3.2x6 м, локальные оси: X -3.5..3.5, Z -3..3, вход +Z):
        /// кассовая стойка с монитором и сигаретной витриной, кофе-уголок, 2 холодильника напитков,
        /// стеллажи с автохимией и снеками, потолочные лампы (одна мерцает).
        /// </summary>
        public static void BuildGasStationInterior(Transform kiosk, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            if (kiosk == null) return;
            Transform t = new GameObject("GasStationInterior").transform;
            t.SetParent(kiosk, false);

            const float fy = 0.06f;
            Material wood = ab.WoodDark;
            Material plank = mats.WoodPlanks;
            Material metal = mats.RustyMetal;
            Material glass = mats.TowerGlass != null ? mats.TowerGlass : mats.DarkGlass;
            Material plastic = ab.Plastic;
            Material cream = ab.WallPaintCream;
            Material red = mats.FuelCanRed;
            Material green = ab.WallPaintGreen;
            Material blue = mats.StainedBlue;
            Material amber = mats.StainedAmber;

            // 1. L-образная кассовая стойка слева-вглубь.
            PropKit.Box(t, "CounterMain", new Vector3(-1.55f, fy + 0.50f, -1.10f), new Vector3(2.4f, 1.00f, 0.72f), cream, true);
            PropKit.Box(t, "CounterTop", new Vector3(-1.55f, fy + 1.02f, -1.10f), new Vector3(2.48f, 0.05f, 0.78f), wood, false);
            PropKit.Box(t, "CounterSide", new Vector3(-2.65f, fy + 0.50f, -0.15f), new Vector3(0.68f, 1.00f, 1.45f), cream, true);
            PropKit.Box(t, "CounterSideTop", new Vector3(-2.65f, fy + 1.02f, -0.15f), new Vector3(0.74f, 0.05f, 1.50f), wood, false);

            // Кассовый терминал, монитор и ящик для денег.
            PropKit.Box(t, "CashDrawer", new Vector3(-1.25f, fy + 1.11f, -1.05f), new Vector3(0.44f, 0.12f, 0.40f), plastic, false);
            PropKit.Box(t, "PosMonitor", new Vector3(-1.25f, fy + 1.32f, -1.12f), new Vector3(0.36f, 0.28f, 0.05f), plastic, false);
            PropKit.Box(t, "PosScreen", new Vector3(-1.25f, fy + 1.32f, -1.09f), new Vector3(0.30f, 0.22f, 0.01f), blue, false);

            // Сигаретный шкаф за спиной кассира на задней стене.
            PropKit.Box(t, "TobaccoRack", new Vector3(-1.6f, fy + 1.55f, -2.72f), new Vector3(1.8f, 1.45f, 0.28f), metal, true);
            for (int r = 0; r < 4; r++)
                PropKit.Box(t, "TobaccoRow_" + r, new Vector3(-1.6f, fy + 1.05f + r * 0.30f, -2.56f), new Vector3(1.65f, 0.14f, 0.06f), (r % 2 == 0) ? red : amber, false);

            // 2. Кофе-уголок справа от входа (как на настоящей АЗС).
            PropKit.Box(t, "CoffeeCounter", new Vector3(2.45f, fy + 0.48f, 1.15f), new Vector3(1.45f, 0.96f, 0.65f), wood, true);
            PropKit.Box(t, "CoffeeMachine", new Vector3(2.55f, fy + 1.24f, 1.15f), new Vector3(0.46f, 0.56f, 0.42f), plastic, false);
            PropKit.Box(t, "CoffeeNozzle", new Vector3(2.35f, fy + 1.18f, 1.15f), new Vector3(0.10f, 0.12f, 0.18f), metal, false);
            PropKit.Cylinder(t, "CupStack1", new Vector3(2.05f, fy + 1.10f, 1.10f), new Vector3(0.10f, 0.14f, 0.10f), cream, false);
            PropKit.Cylinder(t, "CupStack2", new Vector3(1.90f, fy + 1.08f, 1.12f), new Vector3(0.10f, 0.12f, 0.10f), red, false);
            PropKit.Box(t, "TrashBin", new Vector3(2.95f, fy + 0.38f, 1.85f), new Vector3(0.42f, 0.76f, 0.42f), plastic, true);

            // 3. Два больших стеклянных холодильника напитков у задней стены.
            foreach (float fx in new[] { 0.85f, 2.25f })
            {
                PropKit.Box(t, "FridgeBody", new Vector3(fx, fy + 1.05f, -2.42f), new Vector3(1.15f, 2.10f, 0.72f), ab.Porcelain, true);
                PropKit.Box(t, "FridgeGlass", new Vector3(fx, fy + 1.12f, -2.05f), new Vector3(0.96f, 1.65f, 0.03f), glass, false);
                for (int sh = 0; sh < 4; sh++)
                {
                    float sy = fy + 0.52f + sh * 0.38f;
                    PropKit.Box(t, "FridgeShelf", new Vector3(fx, sy, -2.30f), new Vector3(0.98f, 0.03f, 0.46f), metal, false);
                    for (int b = 0; b < 4; b++)
                    {
                        Material bm = (b + sh) % 3 == 0 ? red : ((b + sh) % 3 == 1 ? green : blue);
                        PropKit.Cylinder(t, "Drink", new Vector3(fx - 0.33f + b * 0.22f, sy + 0.13f, -2.20f), new Vector3(0.08f, 0.11f, 0.08f), bm, false);
                    }
                }
            }

            // 4. Торговый стеллаж с автохимией и снеками по центру.
            BuildMerchandiseShelf(t, "OilShelf", new Vector3(0.75f, fy, 0.35f), 0f, 1.8f, metal, new[] { red, amber, blue, green });

            // 5. Потолочная люминесцентная лампа с тусклым хоррор-мерцанием.
            PropKit.Box(t, "FluorescentTube", new Vector3(0f, 3.06f, 0f), new Vector3(1.8f, 0.08f, 0.22f), cream, false);
            GameObject lamp = new GameObject("GasInteriorFlickerLight");
            lamp.transform.SetParent(t, false);
            lamp.transform.localPosition = new Vector3(0f, 2.85f, 0f);
            Light l = lamp.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.86f, 0.94f, 1.0f);
            l.intensity = 6.5f;
            l.range = 10f;
            lamp.AddComponent<FlickerLight>();

            // Точка лута внутри АЗС.
            GameObject lp = new GameObject("LootPoint_GasStation");
            lp.transform.SetParent(t, false);
            lp.transform.localPosition = new Vector3(0.2f, fy + 0.05f, -1.2f);

            if (ab.GrimeDecal != null)
                ApartmentBlockBuilder.Decal(t, ab.GrimeDecal, new Vector3(0f, fy + 0.02f, 0.6f), Vector3.up, new Vector2(3.5f, 3.0f), 15f);
        }

        /// <summary>Детализация наружной зоны АЗС: ТРК с дисплеями, шлангами, отбойниками, стелой цен и вывеской.</summary>
        public static void BuildGasStationExterior(Transform cityParent, Vector3 canopyCenter, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            Transform ext = new GameObject("GasStationExterior").transform;
            ext.SetParent(cityParent, false);
            ext.localPosition = canopyCenter;

            // Яркая выцветшая полоса навеса АЗС.
            PropKit.Box(ext, "CanopyFascia_N", new Vector3(0f, 3.45f, 4.05f), new Vector3(6.2f, 0.55f, 0.12f), mats.FuelCanRed, false);
            PropKit.Box(ext, "CanopyFascia_S", new Vector3(0f, 3.45f, -4.05f), new Vector3(6.2f, 0.55f, 0.12f), mats.FuelCanRed, false);
            PropKit.Box(ext, "CanopyFascia_W", new Vector3(-3.05f, 3.45f, 0f), new Vector3(0.12f, 0.55f, 8.2f), mats.FuelCanRed, false);

            // Островок и детализированные ТРК-колонки.
            PropKit.Box(ext, "PumpIsland", new Vector3(0f, 0.08f, 0f), new Vector3(1.5f, 0.16f, 5.2f), mats.Concrete, true);
            foreach (float pz in new[] { -1.35f, 1.35f })
            {
                PropKit.Box(ext, "PumpBody", new Vector3(0f, 0.92f, pz), new Vector3(0.68f, 1.52f, 0.52f), ab.Porcelain, true);
                PropKit.Box(ext, "PumpTop", new Vector3(0f, 1.74f, pz), new Vector3(0.74f, 0.18f, 0.58f), mats.FuelCanRed, false);
                PropKit.Box(ext, "PumpDisplay", new Vector3(-0.35f, 1.24f, pz), new Vector3(0.03f, 0.34f, 0.36f), ab.Plastic, false);
                PropKit.Box(ext, "PumpHose", new Vector3(0f, 1.05f, pz + 0.30f), new Vector3(0.06f, 0.85f, 0.06f), ab.Rubber, false);
            }

            // Защитные столбики-отбойники.
            foreach (float bz in new[] { -2.45f, 2.45f })
                PropKit.Cylinder(ext, "Bollard", new Vector3(-0.55f, 0.45f, bz), new Vector3(0.14f, 0.45f, 0.14f), mats.StainedAmber ?? mats.FuelCanRed, true);

            // Стела с ценами на топливо у дороги.
            PropKit.Box(ext, "PriceSignPole", new Vector3(-4.2f, 2.4f, -4.8f), new Vector3(0.26f, 4.8f, 0.26f), mats.RustyMetal, true);
            PropKit.Box(ext, "PriceSignBoard", new Vector3(-4.2f, 3.6f, -4.8f), new Vector3(1.4f, 2.2f, 0.22f), ab.Plastic, true);
            PropKit.Box(ext, "PriceSignHeader", new Vector3(-4.2f, 4.45f, -4.8f), new Vector3(1.44f, 0.45f, 0.26f), mats.FuelCanRed, false);

            // Тусклый свет под навесом АЗС.
            GameObject lightObj = new GameObject("GasCanopyLight");
            lightObj.transform.SetParent(ext, false);
            lightObj.transform.localPosition = new Vector3(0f, 3.1f, 0f);
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.95f, 0.96f, 0.86f);
            l.intensity = 9f;
            l.range = 14f;
            lightObj.AddComponent<FlickerLight>();
        }

        // ------------------------------------------------------------------ 2. ПРОДУКТОВЫЙ МАГАЗИН

        /// <summary>
        /// Полноценный продуктовый магазин (11 x 3.6 x 9.5 м): вывеска, витрины, 2 кассовых бокса с лентами,
        /// 3 торговых ряда стеллажей с продуктами, холодильники у задней стены, складские ящики и точки лута.
        /// </summary>
        public static GameObject BuildGroceryStore(
            Transform parent, string name, Vector3 pos, float yaw,
            ProceduralMaterialLibrary mats, AbandonedMaterials ab, GameObject doorPrefab)
        {
            GameObject store = new GameObject(name);
            store.transform.SetParent(parent, false);
            store.transform.localPosition = pos;
            store.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform t = store.transform;

            const float w = 11.0f;
            const float h = 3.6f;
            const float d = 9.5f;
            const float wallT = 0.26f;
            const float fy = 0.06f;

            // Пол, крыша, стены.
            PropKit.Box(t, "Floor", new Vector3(0f, fy * 0.5f, 0f), new Vector3(w, fy, d), ab.Linoleum ?? mats.Concrete, true);
            PropKit.Box(t, "Roof", new Vector3(0f, h + 0.12f, 0f), new Vector3(w + 0.5f, 0.24f, d + 0.5f), mats.Concrete, true);
            PropKit.Box(t, "WallBack", new Vector3(0f, h * 0.5f, -d * 0.5f + wallT * 0.5f), new Vector3(w, h, wallT), mats.Brick, true);
            PropKit.Box(t, "WallLeft", new Vector3(-w * 0.5f + wallT * 0.5f, h * 0.5f, 0f), new Vector3(wallT, h, d), mats.Brick, true);
            PropKit.Box(t, "WallRight", new Vector3(w * 0.5f - wallT * 0.5f, h * 0.5f, 0f), new Vector3(wallT, h, d), mats.Brick, true);

            // Передний фасад (+Z) с дверным проёмом 1.6 м и большими витринными окнами слева и справа.
            float fz = d * 0.5f - wallT * 0.5f;
            PropKit.Box(t, "FrontSillL", new Vector3(-3.15f, 0.45f, fz), new Vector3(4.7f, 0.90f, wallT), mats.Brick, true);
            PropKit.Box(t, "FrontSillR", new Vector3(3.15f, 0.45f, fz), new Vector3(4.7f, 0.90f, wallT), mats.Brick, true);
            PropKit.Box(t, "FrontHeader", new Vector3(0f, 3.05f, fz), new Vector3(w, 1.10f, wallT), mats.Brick, true);
            PropKit.Box(t, "FrontPillarL", new Vector3(-0.95f, 1.75f, fz), new Vector3(0.32f, 2.6f, wallT), mats.Concrete, true);
            PropKit.Box(t, "FrontPillarR", new Vector3(0.95f, 1.75f, fz), new Vector3(0.32f, 2.6f, wallT), mats.Concrete, true);
            // Витринные стёкла + доски постапокалипсиса.
            PropKit.Box(t, "ShowcaseGlassL", new Vector3(-3.15f, 1.70f, fz), new Vector3(4.1f, 1.60f, 0.05f), mats.TowerGlass ?? mats.DarkGlass, true);
            PropKit.Box(t, "ShowcaseGlassR", new Vector3(3.15f, 1.70f, fz), new Vector3(4.1f, 1.60f, 0.05f), mats.TowerGlass ?? mats.DarkGlass, true);
            PropKit.Box(t, "BoardedPlank1", new Vector3(-3.15f, 1.55f, fz + 0.14f), new Vector3(3.6f, 0.18f, 0.05f), ab.WoodDark, false).transform.localRotation = Quaternion.Euler(0f, 0f, 6f);
            PropKit.Box(t, "BoardedPlank2", new Vector3(-3.15f, 1.95f, fz + 0.14f), new Vector3(3.4f, 0.18f, 0.05f), mats.WoodPlanks, false).transform.localRotation = Quaternion.Euler(0f, 0f, -4f);

            // Вывеска и козырёк «ПРОДУКТЫ».
            PropKit.Box(t, "StoreSignboard", new Vector3(0f, 3.35f, fz + 0.22f), new Vector3(9.4f, 0.68f, 0.16f), ab.WallPaintGreen, false);
            PropKit.Box(t, "StoreSignTrim", new Vector3(0f, 3.35f, fz + 0.31f), new Vector3(8.8f, 0.44f, 0.04f), ab.WallPaintCream, false);
            PropKit.Box(t, "StoreAwning", new Vector3(0f, 2.72f, fz + 0.65f), new Vector3(3.4f, 0.12f, 1.3f), mats.RustyMetal, false);

            if (doorPrefab != null)
            {
                GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
                door.transform.SetParent(t, false);
                door.transform.localPosition = new Vector3(0f, fy, fz);
            }

            // --- Интерьер торгового зала ---
            Material[] goodsMats = { mats.FuelCanRed, ab.WallPaintGreen, mats.StainedAmber, mats.StainedBlue, ab.WallPaintCream, ab.Fabric };

            // 1. Два кассовых бокса справа от входа с транспортёрной лентой и мониторами.
            foreach (float cx in new[] { 1.85f, 3.75f })
            {
                PropKit.Box(t, "CheckoutBody", new Vector3(cx, fy + 0.44f, 2.35f), new Vector3(0.75f, 0.88f, 2.1f), ab.MetalPainted, true);
                PropKit.Box(t, "CheckoutBelt", new Vector3(cx, fy + 0.90f, 2.35f), new Vector3(0.46f, 0.03f, 1.95f), ab.Rubber, false);
                PropKit.Box(t, "CheckoutMonitor", new Vector3(cx + 0.22f, fy + 1.18f, 2.85f), new Vector3(0.32f, 0.26f, 0.06f), ab.Plastic, false);
            }

            // 2. Три длинных торговых ряда (стеллажи с товарами).
            foreach (float rx in new[] { -3.55f, -1.10f, 1.35f })
            {
                BuildMerchandiseShelf(t, "GroceryAisle", new Vector3(rx, fy, -0.55f), 90f, 3.8f, mats.RustyMetal, goodsMats);
            }

            // 3. Холодильные витрины вдоль задней стены (-Z).
            for (int f = -2; f <= 2; f++)
            {
                float fx = f * 1.85f;
                PropKit.Box(t, "CoolerBody_" + f, new Vector3(fx, fy + 1.02f, -4.15f), new Vector3(1.68f, 2.04f, 0.72f), ab.Porcelain, true);
                PropKit.Box(t, "CoolerGlass_" + f, new Vector3(fx, fy + 1.10f, -3.78f), new Vector3(1.48f, 1.58f, 0.03f), mats.TowerGlass ?? mats.DarkGlass, false);
                for (int sh = 0; sh < 3; sh++)
                {
                    PropKit.Box(t, "CoolerRow", new Vector3(fx, fy + 0.62f + sh * 0.46f, -3.95f), new Vector3(1.36f, 0.18f, 0.26f), goodsMats[(Mathf.Abs(f) + sh) % goodsMats.Length], false);
                }
            }

            // 4. Складские ящики в углу слева от входа.
            PropKit.Box(t, "StockCrate1", new Vector3(-4.4f, fy + 0.42f, 3.1f), new Vector3(0.95f, 0.84f, 0.95f), mats.WoodPlanks, true);
            PropKit.Box(t, "StockCrate2", new Vector3(-4.3f, fy + 1.12f, 3.0f), new Vector3(0.72f, 0.56f, 0.72f), ab.WoodDark, false);

            // 5. Мерцающий свет в торговом зале + следы времени.
            GameObject lightObj = new GameObject("GroceryLight");
            lightObj.transform.SetParent(t, false);
            lightObj.transform.localPosition = new Vector3(0f, h - 0.35f, 0f);
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.92f, 0.95f, 0.88f);
            l.intensity = 8f;
            l.range = 14f;
            lightObj.AddComponent<FlickerLight>();

            if (ab.PaperDecal != null)
                ApartmentBlockBuilder.Decal(t, ab.PaperDecal, new Vector3(-0.2f, fy + 0.02f, 1.2f), Vector3.up, new Vector2(2.6f, 2.2f), 25f);
            if (ab.GrimeDecal != null)
                ApartmentBlockBuilder.Decal(t, ab.GrimeDecal, new Vector3(1.6f, fy + 0.02f, -2.2f), Vector3.up, new Vector2(3.2f, 2.6f), -18f);

            // Точки лута внутри магазина.
            GameObject lp1 = new GameObject("LootPoint_GroceryAisle");
            lp1.transform.SetParent(t, false);
            lp1.transform.localPosition = new Vector3(-2.3f, fy + 0.05f, -0.5f);
            GameObject lp2 = new GameObject("LootPoint_GroceryCheckout");
            lp2.transform.SetParent(t, false);
            lp2.transform.localPosition = new Vector3(2.8f, fy + 0.05f, 1.5f);

            return store;
        }

        private static void BuildMerchandiseShelf(Transform parent, string name, Vector3 pos, float yaw, float length, Material frameMat, Material[] itemMats)
        {
            Transform g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0f, yaw, 0f);

            PropKit.Box(g, "Spine", new Vector3(0f, 0.96f, 0f), new Vector3(length, 1.92f, 0.08f), frameMat, true);
            PropKit.Box(g, "EndL", new Vector3(-length * 0.5f, 0.96f, 0f), new Vector3(0.08f, 1.92f, 0.72f), frameMat, true);
            PropKit.Box(g, "EndR", new Vector3(length * 0.5f, 0.96f, 0f), new Vector3(0.08f, 1.92f, 0.72f), frameMat, true);

            int slots = Mathf.Max(3, Mathf.RoundToInt(length / 0.55f));
            for (int level = 0; level < 4; level++)
            {
                float sy = 0.24f + level * 0.44f;
                PropKit.Box(g, "Shelf_" + level, new Vector3(0f, sy, 0f), new Vector3(length - 0.06f, 0.04f, 0.70f), frameMat, false);

                for (int i = 0; i < slots; i++)
                {
                    // Часть полок пустая (ограбленный постапокалиптический магазин).
                    if ((i + level * 3) % 4 == 0) continue;
                    float ix = Mathf.Lerp(-length * 0.42f, length * 0.42f, i / (float)Mathf.Max(1, slots - 1));
                    foreach (float side in new[] { -0.20f, 0.20f })
                    {
                        Material im = itemMats[(i + level + (side > 0 ? 1 : 0)) % itemMats.Length];
                        PropKit.Box(g, "Item", new Vector3(ix, sy + 0.14f, side), new Vector3(0.24f, 0.24f, 0.18f), im, false);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ 3. НАПОЛНЕНИЕ ПРОЧИХ ЗДАНИЙ

        public static void FurnishClinic(Transform clinic, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            if (clinic == null) return;
            Transform t = new GameObject("ClinicInterior").transform;
            t.SetParent(clinic, false);
            const float fy = 0.06f;

            // Ресепшн у входа.
            PropKit.Box(t, "ReceptionDesk", new Vector3(-2.6f, fy + 0.50f, 2.4f), new Vector3(2.8f, 1.0f, 0.75f), ab.Porcelain, true);
            PropKit.Box(t, "ReceptionTop", new Vector3(-2.6f, fy + 1.02f, 2.4f), new Vector3(2.9f, 0.06f, 0.82f), mats.WoodPlanks, false);

            // 3 больничные койки вдоль стен.
            foreach (float bz in new[] { -3.2f, -1.0f, 1.2f })
            {
                PropKit.Box(t, "MedBedFrame", new Vector3(3.4f, fy + 0.36f, bz), new Vector3(2.1f, 0.18f, 0.95f), ab.MetalPainted, true);
                PropKit.Box(t, "MedBedMattress", new Vector3(3.4f, fy + 0.52f, bz), new Vector3(2.0f, 0.16f, 0.88f), mats.MedicalWhite ?? ab.Porcelain, false);
                PropKit.Box(t, "MedBedPillow", new Vector3(4.15f, fy + 0.64f, bz), new Vector3(0.38f, 0.12f, 0.62f), ab.Porcelain, false);
            }

            // Медицинские шкафы у задней стены.
            foreach (float cx in new[] { -3.2f, -1.2f })
            {
                PropKit.Box(t, "MedCabinet", new Vector3(cx, fy + 1.0f, -4.35f), new Vector3(1.1f, 2.0f, 0.52f), ab.Porcelain, true);
                PropKit.Box(t, "MedCabGlass", new Vector3(cx, fy + 1.25f, -4.08f), new Vector3(0.92f, 1.1f, 0.03f), mats.TowerGlass ?? mats.DarkGlass, false);
            }
        }

        public static void FurnishAutoService(Transform garage, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            if (garage == null) return;
            Transform t = new GameObject("AutoServiceInterior").transform;
            t.SetParent(garage, false);
            const float fy = 0.06f;

            // Стойки автоподъёмника.
            foreach (float lx in new[] { -1.6f, 1.6f })
            {
                PropKit.Box(t, "LiftPost", new Vector3(lx, fy + 1.4f, -0.5f), new Vector3(0.26f, 2.8f, 0.26f), mats.FuelCanRed, true);
                PropKit.Box(t, "LiftArm", new Vector3(lx * 0.55f, fy + 0.45f, -0.5f), new Vector3(1.2f, 0.10f, 0.14f), mats.RustyMetal, false);
            }

            // Верстак, стеллаж с шинами и бочки масла.
            PropKit.Box(t, "GarageBench", new Vector3(-3.8f, fy + 0.46f, -3.6f), new Vector3(2.8f, 0.92f, 0.85f), ab.WoodDark, true);
            PropKit.Box(t, "ToolChest", new Vector3(-1.4f, fy + 0.52f, -3.6f), new Vector3(0.95f, 1.04f, 0.55f), mats.FuelCanRed, true);
            for (int i = 0; i < 4; i++)
                PropKit.Cylinder(t, "TireStack_" + i, new Vector3(4.2f, fy + 0.14f + i * 0.24f, -3.2f), new Vector3(0.70f, 0.11f, 0.70f), ab.Rubber, i == 0);
            PropKit.Cylinder(t, "OilDrum1", new Vector3(4.2f, fy + 0.48f, 2.4f), new Vector3(0.60f, 0.48f, 0.60f), mats.RustyMetal, true);
            PropKit.Cylinder(t, "OilDrum2", new Vector3(3.4f, fy + 0.48f, 2.8f), new Vector3(0.60f, 0.48f, 0.60f), mats.FuelCanRed, true);
        }

        // ------------------------------------------------------------------ 4. ХОРРОР И ПОСТАПОКАЛИПСИС

        /// <summary>Элементы постапокалипсиса, старины и хоррора на улицах: поваленные столбы, ржавые баррикады, тлеющие бочки, обломки и пятна.</summary>
        public static void BuildPostApocalypseProps(Transform cityParent, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            Transform root = new GameObject("PostApocalypseDetails").transform;
            root.SetParent(cityParent, false);

            // Поваленные уличные фонарные столбы вдоль главного проспекта.
            Vector3[] fallenPoles = { new Vector3(-8.5f, 0.15f, 84f), new Vector3(9.2f, 0.15f, 144f), new Vector3(-10.5f, 0.15f, 196f) };
            for (int i = 0; i < fallenPoles.Length; i++)
            {
                GameObject pole = PropKit.Cylinder(root, "FallenLampPost_" + i, fallenPoles[i], new Vector3(0.16f, 3.8f, 0.16f), mats.RustyMetal, true);
                pole.transform.localRotation = Quaternion.Euler(84f, (i % 2 == 0 ? 62f : -58f), 0f);
            }

            // Тлеющие бочки (хоррор-акценты на улицах с мягким красным/оранжевым мерцанием).
            Vector3[] burnBarrels = { new Vector3(-7.8f, 0f, 96f), new Vector3(8.4f, 0f, 134f), new Vector3(-8.2f, 0f, 174f) };
            for (int i = 0; i < burnBarrels.Length; i++)
            {
                Vector3 bp = burnBarrels[i];
                PropKit.Cylinder(root, "BurnBarrel_" + i, bp + Vector3.up * 0.48f, new Vector3(0.62f, 0.48f, 0.62f), mats.RustyMetal, true);
                PropKit.Cylinder(root, "BarrelEmbers_" + i, bp + Vector3.up * 0.88f, new Vector3(0.48f, 0.08f, 0.48f), mats.Ember ?? mats.Flame, false);
                GameObject fireL = new GameObject("BurnBarrelLight_" + i);
                fireL.transform.SetParent(root, false);
                fireL.transform.localPosition = bp + Vector3.up * 1.25f;
                Light l = fireL.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1.0f, 0.46f, 0.14f);
                l.intensity = 7.5f;
                l.range = 11f;
                fireL.AddComponent<FlickerLight>();
            }

            // Ржавые предупреждающие дорожные щиты / заграждения на въезде в город.
            foreach (float sx in new[] { -5.8f, 5.8f })
            {
                PropKit.Box(root, "QuarantineSignPost", new Vector3(sx, 1.15f, 57f), new Vector3(0.10f, 2.3f, 0.10f), mats.RustyMetal, true);
                GameObject board = PropKit.Box(root, "QuarantineSignBoard", new Vector3(sx, 1.95f, 57f), new Vector3(1.25f, 0.72f, 0.06f), mats.StainedAmber ?? mats.FuelCanRed, false);
                board.transform.localRotation = Quaternion.Euler(0f, 0f, sx < 0 ? -7f : 5f);
            }

            // Пятна крови/грязи, трещины асфальта и разбросанные бумаги.
            if (ab != null)
            {
                for (int i = 0; i < 12; i++)
                {
                    float x = ((i % 2 == 0) ? -1f : 1f) * (1.8f + (i * 1.3f) % 5.5f);
                    float z = 62f + i * 13.5f;
                    Material dec = (i % 3 == 0) ? ab.CrackDecal : ((i % 3 == 1) ? ab.DarkStainDecal : ab.PaperDecal);
                    if (dec != null)
                        ApartmentBlockBuilder.Decal(root, dec, new Vector3(x, 0.03f, z), Vector3.up, new Vector2(2.6f, 2.2f), i * 37f);
                }
            }
        }
    }
}
