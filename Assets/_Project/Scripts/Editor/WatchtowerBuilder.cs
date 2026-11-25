using Ogni.World;
using UnityEditor;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Атмосферная пожарная наблюдательная вышка (Firewatch Lookout Tower) на лесной поляне EvacuationZone:
    /// - 4 несущие опоры с крестовыми раскосами, проходимая 3-маршевая лестница с площадками и перилами.
    /// - Обходной балкон и смотровая будка на высоте 9.5 м с панорамными окнами, радиостанцией, столом и картой.
    /// - Антенна связи с красным маячком на крыше, прожектор, генератор и ржавое ограждение у основания.
    /// </summary>
    public static class WatchtowerBuilder
    {
        public static GameObject Build(Vector3 pos, ProceduralMaterialLibrary mats, AbandonedMaterials ab, GameObject doorPrefab)
        {
            GameObject root = new GameObject("EvacuationZone");
            root.transform.position = pos;
            Transform t = root.transform;

            Material woodDark = ab != null && ab.WoodDark != null ? ab.WoodDark : mats.TreeBark;
            Material woodPlank = mats.WoodPlanks;
            Material metal = mats.RustyMetal;
            Material concrete = mats.Concrete;
            Material glass = mats.DarkGlass;
            Material warmGlow = mats.StainedAmber ?? mats.Flame;

            // 1. Старая потрескавшаяся бетонная площадка и 4 бетонных фундамента под опоры.
            PropKit.Box(t, "BasePad", new Vector3(0f, 0.05f, 0f), new Vector3(11.5f, 0.10f, 11.5f), concrete, true);
            const float halfSpan = 2.6f;
            const float deckY = 9.2f;

            foreach (float sx in new[] { -halfSpan, halfSpan })
            {
                foreach (float sz in new[] { -halfSpan, halfSpan })
                {
                    PropKit.Box(t, "Footing", new Vector3(sx, 0.25f, sz), new Vector3(0.75f, 0.50f, 0.75f), concrete, true);
                    PropKit.Box(t, "MainLeg", new Vector3(sx, deckY * 0.5f, sz), new Vector3(0.28f, deckY, 0.28f), woodDark, true);
                }
            }

            // 2. Ярусы поперечных балок и диагональные Х-раскосы (3 яруса по ~3 м).
            float[] tierYs = { 3.0f, 6.0f, deckY };
            for (int tier = 0; tier < tierYs.Length; tier++)
            {
                float yTop = tierYs[tier];
                float yBot = tier == 0 ? 0.3f : tierYs[tier - 1];
                float yMid = (yTop + yBot) * 0.5f;
                float spanLen = halfSpan * 2f;
                float diagLen = Mathf.Sqrt(spanLen * spanLen + (yTop - yBot) * (yTop - yBot));
                float diagAng = Mathf.Atan2(yTop - yBot, spanLen) * Mathf.Rad2Deg;

                // Горизонтальные балки по периметру яруса.
                PropKit.Box(t, $"BeamN_{tier}", new Vector3(0f, yTop, halfSpan), new Vector3(spanLen + 0.3f, 0.22f, 0.22f), woodDark, false);
                PropKit.Box(t, $"BeamS_{tier}", new Vector3(0f, yTop, -halfSpan), new Vector3(spanLen + 0.3f, 0.22f, 0.22f), woodDark, false);
                PropKit.Box(t, $"BeamW_{tier}", new Vector3(-halfSpan, yTop, 0f), new Vector3(0.22f, 0.22f, spanLen + 0.3f), woodDark, false);
                PropKit.Box(t, $"BeamE_{tier}", new Vector3(halfSpan, yTop, 0f), new Vector3(0.22f, 0.22f, spanLen + 0.3f), woodDark, false);

                // Диагональные раскосы на боковых сторонах (оставляем проход для лестницы).
                foreach (float sz in new[] { -halfSpan, halfSpan })
                {
                    PropKit.Box(t, $"BraceX1_{tier}", new Vector3(0f, yMid, sz), new Vector3(diagLen, 0.12f, 0.10f), woodDark, false).transform.localRotation = Quaternion.Euler(0f, 0f, diagAng);
                    PropKit.Box(t, $"BraceX2_{tier}", new Vector3(0f, yMid, sz), new Vector3(diagLen, 0.12f, 0.10f), woodDark, false).transform.localRotation = Quaternion.Euler(0f, 0f, -diagAng);
                }
            }

            // 3. Проходимая 3-маршевая лестница внутри каркаса вышки (можно забраться наверх!).
            BuildStairFlight(t, "Stairs_1", new Vector3(-1.4f, 0.10f, -2.0f), new Vector3(-1.4f, 3.05f, 1.8f), 14, woodPlank, woodDark);
            PropKit.Box(t, "Landing_1", new Vector3(0f, 3.05f, 1.95f), new Vector3(4.8f, 0.14f, 1.3f), woodPlank, true);

            BuildStairFlight(t, "Stairs_2", new Vector3(1.4f, 3.05f, 1.8f), new Vector3(1.4f, 6.05f, -1.8f), 14, woodPlank, woodDark);
            PropKit.Box(t, "Landing_2", new Vector3(0f, 6.05f, -1.95f), new Vector3(4.8f, 0.14f, 1.3f), woodPlank, true);

            BuildStairFlight(t, "Stairs_3", new Vector3(-1.4f, 6.05f, -1.8f), new Vector3(-1.4f, deckY + 0.08f, 2.2f), 15, woodPlank, woodDark);

            // 4. Верхняя смотровая площадка (обходной балкон 6.8 x 6.8 м с перилами).
            const float platSize = 6.8f;
            PropKit.Box(t, "DeckFloor_Main", new Vector3(0.6f, deckY + 0.06f, 0f), new Vector3(platSize - 1.2f, 0.14f, platSize), woodPlank, true);
            PropKit.Box(t, "DeckFloor_Strip", new Vector3(-2.7f, deckY + 0.06f, -1.3f), new Vector3(1.4f, 0.14f, 4.2f), woodPlank, true);

            float railR = platSize * 0.5f - 0.12f;
            BuildBalconyRailing(t, new Vector3(0f, deckY + 0.12f, railR), platSize, true, woodDark, woodPlank);
            BuildBalconyRailing(t, new Vector3(0f, deckY + 0.12f, -railR), platSize, true, woodDark, woodPlank);
            BuildBalconyRailing(t, new Vector3(railR, deckY + 0.12f, 0f), platSize, false, woodDark, woodPlank);
            BuildBalconyRailing(t, new Vector3(-railR, deckY + 0.12f, 0f), platSize, false, woodDark, woodPlank);

            // 5. Смотровая будка (4.4 x 2.8 x 4.4 м) с панорамными окнами и покатой крышей.
            Transform cabin = new GameObject("LookoutCabin").transform;
            cabin.SetParent(t, false);
            cabin.localPosition = new Vector3(0.45f, deckY + 0.13f, -0.35f);

            const float cw = 4.2f;
            const float ch = 2.75f;
            const float wt = 0.18f;

            // Нижняя обшивка стен (до подоконников 1.05 м) и верхняя балка.
            PropKit.Box(cabin, "CabWallS_Low", new Vector3(0f, 0.52f, -cw * 0.5f), new Vector3(cw, 1.04f, wt), woodPlank, true);
            PropKit.Box(cabin, "CabWallE_Low", new Vector3(cw * 0.5f, 0.52f, 0f), new Vector3(wt, 1.04f, cw), woodPlank, true);
            PropKit.Box(cabin, "CabWallW_Low", new Vector3(-cw * 0.5f, 0.52f, 0f), new Vector3(wt, 1.04f, cw), woodPlank, true);
            // Передняя стена (+Z) с дверным проёмом 1.3 м.
            PropKit.Box(cabin, "CabWallN_L", new Vector3(-1.38f, ch * 0.5f, cw * 0.5f), new Vector3(1.44f, ch, wt), woodPlank, true);
            PropKit.Box(cabin, "CabWallN_R", new Vector3(1.38f, ch * 0.5f, cw * 0.5f), new Vector3(1.44f, ch, wt), woodPlank, true);
            PropKit.Box(cabin, "CabWallN_Top", new Vector3(0f, 2.42f, cw * 0.5f), new Vector3(1.36f, 0.66f, wt), woodPlank, true);

            // Угловые стойки и панорамные стёкла по 3 сторонам.
            foreach (float cx in new[] { -cw * 0.5f, cw * 0.5f })
                foreach (float cz in new[] { -cw * 0.5f, cw * 0.5f })
                    PropKit.Box(cabin, "CabCorner", new Vector3(cx, ch * 0.5f, cz), new Vector3(0.22f, ch, 0.22f), woodDark, true);

            PropKit.Box(cabin, "CabGlassS", new Vector3(0f, 1.78f, -cw * 0.5f), new Vector3(cw - 0.3f, 1.40f, 0.04f), glass, false);
            PropKit.Box(cabin, "CabGlassE", new Vector3(cw * 0.5f, 1.78f, 0f), new Vector3(0.04f, 1.40f, cw - 0.3f), glass, false);
            PropKit.Box(cabin, "CabGlassW", new Vector3(-cw * 0.5f, 1.78f, 0f), new Vector3(0.04f, 1.40f, cw - 0.3f), glass, false);

            // Шатровая крыша с широкими свесами.
            PropKit.Box(cabin, "CabRoofBase", new Vector3(0f, ch + 0.10f, 0f), new Vector3(cw + 1.4f, 0.16f, cw + 1.4f), metal, true);
            PropKit.Box(cabin, "CabRoofTier1", new Vector3(0f, ch + 0.42f, 0f), new Vector3(cw * 0.72f, 0.48f, cw * 0.72f), metal, false);
            PropKit.Box(cabin, "CabRoofCap", new Vector3(0f, ch + 0.80f, 0f), new Vector3(cw * 0.32f, 0.32f, cw * 0.32f), woodDark, false);

            if (doorPrefab != null)
            {
                GameObject door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
                door.transform.SetParent(cabin, false);
                door.transform.localPosition = new Vector3(0f, 0f, cw * 0.5f);
            }

            // Интерьер смотровой будки: стол радиста, рация, лампа и точка лута.
            PropKit.Box(cabin, "RadioDesk", new Vector3(0f, 0.44f, -1.35f), new Vector3(2.1f, 0.88f, 0.75f), woodDark, true);
            PropKit.Box(cabin, "RadioUnit", new Vector3(-0.35f, 1.04f, -1.40f), new Vector3(0.62f, 0.32f, 0.36f), ab.Plastic, false);
            PropKit.Box(cabin, "RadioDialGlow", new Vector3(-0.35f, 1.06f, -1.21f), new Vector3(0.24f, 0.10f, 0.02f), warmGlow, false);
            PropKit.Box(cabin, "MapSheet", new Vector3(0.45f, 0.90f, -1.30f), new Vector3(0.75f, 0.02f, 0.52f), ab.WallPaintCream, false);

            GameObject cabLightObj = new GameObject("WatchtowerCabinLight");
            cabLightObj.transform.SetParent(cabin, false);
            cabLightObj.transform.localPosition = new Vector3(0f, 2.35f, 0f);
            Light cabLight = cabLightObj.AddComponent<Light>();
            cabLight.type = LightType.Point;
            cabLight.color = new Color(1.0f, 0.70f, 0.34f);
            cabLight.intensity = 7.5f;
            cabLight.range = 10f;
            cabLightObj.AddComponent<FlickerLight>();

            GameObject lp = new GameObject("LootPoint_Watchtower");
            lp.transform.SetParent(cabin, false);
            lp.transform.localPosition = new Vector3(0.9f, 0.08f, 0.2f);

            // 6. Высокая радиоантенна и красный сигнальный маячок на крыше вышки.
            PropKit.Cylinder(cabin, "AntennaMast", new Vector3(0f, ch + 2.8f, 0f), new Vector3(0.10f, 2.2f, 0.10f), metal, false);
            PropKit.Box(cabin, "AntennaCross1", new Vector3(0f, ch + 3.8f, 0f), new Vector3(1.6f, 0.05f, 0.05f), metal, false);
            PropKit.Box(cabin, "AntennaCross2", new Vector3(0f, ch + 4.4f, 0f), new Vector3(1.0f, 0.05f, 0.05f), metal, false);
            PropKit.Box(cabin, "BeaconBulb", new Vector3(0f, ch + 4.95f, 0f), new Vector3(0.22f, 0.22f, 0.22f), mats.FuelCanRed, false);

            GameObject beacon = new GameObject("EvacBeaconLight");
            beacon.transform.SetParent(cabin, false);
            beacon.transform.localPosition = new Vector3(0f, ch + 5.1f, 0f);
            Light bLight = beacon.AddComponent<Light>();
            bLight.type = LightType.Point;
            bLight.range = 24f;
            bLight.intensity = 6.5f;
            bLight.color = new Color(1f, 0.16f, 0.12f);
            beacon.AddComponent<BlinkLight>();

            // 7. Внизу у подножия: дизельный генератор, бочки, ящики и ржавый забор по краю площадки.
            PropKit.Box(t, "GeneratorBody", new Vector3(3.8f, 0.65f, -3.2f), new Vector3(1.6f, 1.1f, 0.95f), ab.WallPaintGreen, true);
            PropKit.Cylinder(t, "GeneratorExhaust", new Vector3(4.3f, 1.45f, -3.2f), new Vector3(0.10f, 0.45f, 0.10f), metal, false);
            PropKit.Cylinder(t, "FuelBarrel1", new Vector3(4.2f, 0.55f, -1.6f), new Vector3(0.62f, 0.48f, 0.62f), mats.FuelCanRed, true);
            PropKit.Box(t, "SupplyCrate", new Vector3(-3.8f, 0.52f, 3.2f), new Vector3(1.1f, 0.85f, 1.0f), woodPlank, true);

            // Потрескавшиеся пятна мха и грязи на бетонной плите.
            if (ab != null)
            {
                if (ab.CrackDecal != null) ApartmentBlockBuilder.Decal(t, ab.CrackDecal, new Vector3(-2.2f, 0.11f, -2.0f), Vector3.up, new Vector2(4.5f, 4.0f), 20f);
                if (ab.MossDecal != null) ApartmentBlockBuilder.Decal(t, ab.MossDecal, new Vector3(2.8f, 0.11f, 2.5f), Vector3.up, new Vector2(4.2f, 3.8f), -35f);
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.GetComponent<Light>() != null || child.name.StartsWith("LootPoint_")) continue;
                if (child.GetComponentInParent<Ogni.Building.Door>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }

            return root;
        }

        private static void BuildStairFlight(Transform parent, string name, Vector3 start, Vector3 end, int steps, Material stepMat, Material railMat)
        {
            Transform g = new GameObject(name).transform;
            g.SetParent(parent, false);

            // Плавный невидимый пандус-коллайдер для комфортного подъёма CharacterController без рывков.
            Vector3 delta = end - start;
            float len = delta.magnitude;
            GameObject ramp = new GameObject(name + "_RampCollider");
            ramp.transform.SetParent(g, false);
            ramp.transform.localPosition = (start + end) * 0.5f - Vector3.up * 0.08f;
            ramp.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            BoxCollider bc = ramp.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.25f, 0.16f, len + 0.25f);

            for (int i = 0; i < steps; i++)
            {
                float t = (i + 0.5f) / steps;
                Vector3 p = Vector3.Lerp(start, end, t);
                PropKit.Box(g, "Step_" + i, p, new Vector3(1.25f, 0.08f, 0.30f), stepMat, false);
            }

            // Боковые тетивы лестницы.
            foreach (float sx in new[] { -0.62f, 0.62f })
            {
                GameObject str = PropKit.Box(g, "Stringer", (start + end) * 0.5f + new Vector3(sx, -0.05f, 0f),
                    new Vector3(0.08f, 0.22f, len), railMat, false);
                str.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            }
        }

        private static void BuildBalconyRailing(Transform parent, Vector3 center, float length, bool alongX, Material postMat, Material railMat)
        {
            PropKit.Box(parent, "RailTop", center + Vector3.up * 1.05f,
                alongX ? new Vector3(length, 0.08f, 0.08f) : new Vector3(0.08f, 0.08f, length), railMat, true);
            PropKit.Box(parent, "RailMid", center + Vector3.up * 0.55f,
                alongX ? new Vector3(length, 0.06f, 0.06f) : new Vector3(0.06f, 0.06f, length), postMat, false);

            int posts = 5;
            for (int i = 0; i <= posts; i++)
            {
                float off = Mathf.Lerp(-length * 0.5f + 0.15f, length * 0.5f - 0.15f, i / (float)posts);
                Vector3 p = center + (alongX ? new Vector3(off, 0.52f, 0f) : new Vector3(0f, 0.52f, off));
                PropKit.Box(parent, "RailPost", p, new Vector3(0.09f, 1.05f, 0.09f), postMat, false);
            }
        }
    }
}
