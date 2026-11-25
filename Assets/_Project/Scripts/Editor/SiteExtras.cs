using Ogni.World;
using UnityEngine;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Дополнительная проработка двух знаковых локаций:
    /// 1) Часовня (интерьер: балки, люстра, кафедра, исповедальня, ковровая дорожка, алтарная преграда,
    ///    купель, иконы в рамах, напольные канделябры, бра, завал; экстерьер: каменный портик с колоннами,
    ///    фонари у входа, каменная дорожка к воротам кладбища).
    /// 2) Амбар фермы (настоящая двускатная крыша с коньком и фронтонами, слуховое окно, балки,
    ///    сеновал, стойла, верстак плотника, стеллаж с канистрами, боковой навес с дровами, ворота и фонари).
    /// </summary>
    public static class SiteExtras
    {
        // ------------------------------------------------------------------ часовня (интерьер)

        // Размеры нефа: ширина 10 (x от -5 до 5), длина 14 (z от -7 до 7), высота стен 5.4. Алтарь у z = -5, вход с z = +7.
        public static void BuildChapelExtras(Transform interior, System.Random rng, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            Transform t = new GameObject("ChapelExtras").transform;
            t.SetParent(interior, false);

            // Потолочные балки и стяжки.
            foreach (float z in new[] { -5.2f, -2.6f, 0f, 2.6f, 5.2f })
            {
                PropKit.Box(t, "CeilingBeam", new Vector3(0f, 5.05f, z), new Vector3(9.6f, 0.30f, 0.30f), ab.WoodDark, false);
                PropKit.Box(t, "BraceL", new Vector3(-4.2f, 4.65f, z), new Vector3(0.18f, 1.1f, 0.18f), ab.WoodDark, false).transform.localRotation = Quaternion.Euler(0f, 0f, -42f);
                PropKit.Box(t, "BraceR", new Vector3(4.2f, 4.65f, z), new Vector3(0.18f, 1.1f, 0.18f), ab.WoodDark, false).transform.localRotation = Quaternion.Euler(0f, 0f, 42f);
            }

            // Ковровая дорожка по центральному проходу от входа к алтарю.
            PropKit.Box(t, "AisleCarpet", new Vector3(0f, 0.072f, 1.0f), new Vector3(1.65f, 0.018f, 11.2f), mats.FuelCanRed ?? ab.Fabric, false);
            PropKit.Box(t, "AisleCarpetTrimL", new Vector3(-0.78f, 0.078f, 1.0f), new Vector3(0.08f, 0.014f, 11.2f), mats.BrassAged, false);
            PropKit.Box(t, "AisleCarpetTrimR", new Vector3(0.78f, 0.078f, 1.0f), new Vector3(0.08f, 0.014f, 11.2f), mats.BrassAged, false);

            // Резная алтарная преграда (перила перед алтарным помостом, с проходом по центру).
            foreach (float side in new[] { -1f, 1f })
            {
                PropKit.Box(t, "AltarRailTop", new Vector3(side * 1.95f, 0.92f, -3.55f), new Vector3(2.2f, 0.09f, 0.14f), ab.WoodDark, true);
                PropKit.Box(t, "AltarRailBot", new Vector3(side * 1.95f, 0.18f, -3.55f), new Vector3(2.2f, 0.12f, 0.14f), mats.Stone, true);
                for (int b = 0; b < 5; b++)
                {
                    float bx = side * (1.05f + b * 0.45f);
                    PropKit.Box(t, "AltarBaluster", new Vector3(bx, 0.55f, -3.55f), new Vector3(0.07f, 0.68f, 0.07f), mats.WoodPlanks, false);
                }
            }

            // Напольные латунные канделябры по бокам алтаря.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 cp = new Vector3(side * 2.1f, 0.27f, -4.9f);
                PropKit.Cylinder(t, "FloorCandelabraBase", cp + new Vector3(0f, 0.05f, 0f), new Vector3(0.36f, 0.05f, 0.36f), mats.BrassAged, false);
                PropKit.Cylinder(t, "FloorCandelabraStem", cp + new Vector3(0f, 0.72f, 0f), new Vector3(0.06f, 0.68f, 0.06f), mats.BrassAged, false);
                PropKit.Cylinder(t, "FloorCandelabraTop", cp + new Vector3(0f, 1.40f, 0f), new Vector3(0.30f, 0.04f, 0.30f), mats.BrassAged, false);
                PropKit.Cylinder(t, "FloorCandle", cp + new Vector3(0f, 1.56f, 0f), new Vector3(0.06f, 0.14f, 0.06f), mats.CandleWax, false);
                PropKit.Box(t, "FloorCandleFlame", cp + new Vector3(0f, 1.73f, 0f), new Vector3(0.035f, 0.06f, 0.035f), mats.Flame, false);
            }

            // Каменная купель у входа справа.
            Vector3 fontPos = new Vector3(3.4f, 0.06f, 5.1f);
            PropKit.Cylinder(t, "FontBase", fontPos + new Vector3(0f, 0.12f, 0f), new Vector3(0.75f, 0.12f, 0.75f), mats.Stone, true);
            PropKit.Cylinder(t, "FontStem", fontPos + new Vector3(0f, 0.55f, 0f), new Vector3(0.34f, 0.35f, 0.34f), mats.Stone, true);
            PropKit.Cylinder(t, "FontBowl", fontPos + new Vector3(0f, 0.96f, 0f), new Vector3(0.82f, 0.18f, 0.82f), mats.Stone, true);
            PropKit.Cylinder(t, "FontWater", fontPos + new Vector3(0f, 1.12f, 0f), new Vector3(0.64f, 0.02f, 0.64f), mats.DarkGlass, false);

            // Иконы / фрески в рамах на простенках боковых стен.
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -2.0f, 2.0f })
                {
                    float x = side * 4.62f;
                    PropKit.Box(t, "IconFrame", new Vector3(x, 2.55f, z), new Vector3(0.07f, 1.35f, 0.95f), ab.WoodDark, false);
                    PropKit.Box(t, "IconGold", new Vector3(x - side * 0.03f, 2.55f, z), new Vector3(0.03f, 1.15f, 0.75f), mats.BrassAged, false);
                    PropKit.Box(t, "IconCanvas", new Vector3(x - side * 0.05f, 2.55f, z), new Vector3(0.02f, 0.96f, 0.58f), mats.StainedAmber, false);
                }
            }

            // Большое деревянное распятие над алтарём.
            PropKit.Box(t, "WallCrossV", new Vector3(0f, 3.5f, -6.72f), new Vector3(0.34f, 2.8f, 0.12f), ab.WoodDark, false);
            PropKit.Box(t, "WallCrossH", new Vector3(0f, 4.1f, -6.72f), new Vector3(1.6f, 0.30f, 0.12f), ab.WoodDark, false);

            // Люстра: железное кольцо на четырёх цепях, свечи по кругу.
            Vector3 ch = new Vector3(0f, 3.9f, 0.4f);
            PropKit.Cylinder(t, "ChandelierRing", ch, new Vector3(1.9f, 0.04f, 1.9f), mats.RustyMetal, false);
            foreach (float a in new[] { 45f, 135f, 225f, 315f })
            {
                float r = a * Mathf.Deg2Rad;
                Vector3 p = ch + new Vector3(Mathf.Cos(r) * 0.8f, 0.55f, Mathf.Sin(r) * 0.8f);
                PropKit.Box(t, "ChandelierChain", p, new Vector3(0.025f, 1.1f, 0.025f), mats.RustyMetal, false);
            }

            for (int i = 0; i < 8; i++)
            {
                float r = i * 45f * Mathf.Deg2Rad;
                Vector3 p = ch + new Vector3(Mathf.Cos(r) * 0.95f, 0.1f, Mathf.Sin(r) * 0.95f);
                PropKit.Cylinder(t, "ChandelierCandle", p, new Vector3(0.05f, 0.09f, 0.05f), mats.CandleWax, false);
                if (i % 2 == 0) PropKit.Box(t, "ChandelierFlame", p + Vector3.up * 0.14f, new Vector3(0.03f, 0.05f, 0.03f), mats.Flame, false);
            }

            GameObject lamp = new GameObject("ChandelierLight");
            lamp.transform.SetParent(t, false);
            lamp.transform.localPosition = ch + Vector3.up * 0.25f;
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 14f;
            light.intensity = 6.5f;
            light.color = new Color(1f, 0.72f, 0.38f);
            lamp.AddComponent<FlickerLight>();

            // Кафедра проповедника слева от помоста.
            PropKit.Box(t, "PulpitBase", new Vector3(-3.4f, 0.65f, -4.0f), new Vector3(0.9f, 1.2f, 0.7f), ab.WoodDark, true);
            GameObject top = PropKit.Box(t, "PulpitTop", new Vector3(-3.4f, 1.34f, -4.0f), new Vector3(1.05f, 0.08f, 0.85f), ab.WoodDark, false);
            top.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            PropKit.Box(t, "PulpitBook", new Vector3(-3.4f, 1.42f, -4.0f), new Vector3(0.32f, 0.05f, 0.24f), mats.BrassAged, false).transform.localRotation = Quaternion.Euler(12f, 0f, 0f);

            // Исповедальня у входа в левом углу.
            PropKit.Box(t, "ConfBack", new Vector3(-4.1f, 1.2f, 5.7f), new Vector3(1.3f, 2.4f, 0.1f), ab.WoodDark, true);
            PropKit.Box(t, "ConfSideL", new Vector3(-4.75f, 1.2f, 5.1f), new Vector3(0.1f, 2.4f, 1.3f), ab.WoodDark, true);
            PropKit.Box(t, "ConfSideR", new Vector3(-3.45f, 1.2f, 5.1f), new Vector3(0.1f, 2.4f, 1.3f), ab.WoodDark, true);
            PropKit.Box(t, "ConfRoof", new Vector3(-4.1f, 2.45f, 5.1f), new Vector3(1.5f, 0.1f, 1.5f), ab.WoodDark, false);
            PropKit.Box(t, "ConfCurtain", new Vector3(-4.1f, 1.1f, 4.48f), new Vector3(1.0f, 2.0f, 0.04f), ab.Fabric, false);

            // Бра со свечами вдоль боковых стен.
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -3.4f, 0.6f, 4.0f })
                {
                    float x = side * 4.72f;
                    PropKit.Box(t, "SconceArm", new Vector3(x, 2.3f, z), new Vector3(0.12f, 0.06f, 0.12f), mats.BrassAged, false);
                    PropKit.Cylinder(t, "SconceCandle", new Vector3(x - side * 0.04f, 2.42f, z), new Vector3(0.05f, 0.08f, 0.05f), mats.CandleWax, false);
                    if (rng.NextDouble() < 0.6) PropKit.Box(t, "SconceFlame", new Vector3(x - side * 0.04f, 2.55f, z), new Vector3(0.03f, 0.05f, 0.03f), mats.Flame, false);
                }
            }

            // Выцветшие тканевые полотнища у алтаря.
            foreach (float side in new[] { -1f, 1f })
            {
                PropKit.Box(t, "Banner", new Vector3(side * 4.7f, 3.3f, -4.6f), new Vector3(0.05f, 2.2f, 0.9f), ab.Fabric, false);
            }

            // Обвалившаяся штукатурка и упавшая балка у восточной стены.
            for (int i = 0; i < 9; i++)
            {
                float s = 0.15f + (float)rng.NextDouble() * 0.35f;
                Vector3 p = new Vector3(3.2f + (float)rng.NextDouble() * 1.4f, s * 0.5f + 0.06f, -2.2f + (float)rng.NextDouble() * 2.2f);
                GameObject chunk = PropKit.Box(t, "Rubble", p, new Vector3(s, s * 0.7f, s * 0.9f), i % 2 == 0 ? mats.Stone : mats.Concrete, false);
                chunk.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f);
            }

            GameObject fallen = PropKit.Box(t, "FallenBeam", new Vector3(3.9f, 0.5f, -1.0f), new Vector3(0.28f, 0.28f, 3.2f), ab.WoodDark, true);
            fallen.transform.localRotation = Quaternion.Euler(-8f, 20f, 0f);
        }

        /// <summary>Экстерьер часовни: каменный портик с колоннами, кованые фонари у входа и каменная дорожка к воротам кладбища.</summary>
        public static void BuildChapelExteriorExtras(Transform chapelRoot, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            if (chapelRoot == null) return;
            Transform ext = new GameObject("ChapelExteriorExtras").transform;
            ext.SetParent(chapelRoot, false);

            // Каменный портик и козырёк над входом (z = +7..+9.2).
            foreach (float sx in new[] { -1.45f, 1.45f })
            {
                PropKit.Box(ext, "PorticoPlinth", new Vector3(sx, 0.25f, 8.5f), new Vector3(0.50f, 0.50f, 0.50f), mats.Stone, true);
                PropKit.Cylinder(ext, "PorticoColumn", new Vector3(sx, 1.75f, 8.5f), new Vector3(0.32f, 1.25f, 0.32f), mats.Stone, true);
                PropKit.Box(ext, "PorticoCapital", new Vector3(sx, 3.05f, 8.5f), new Vector3(0.48f, 0.16f, 0.48f), mats.Concrete, false);

                // Кованый фонарь у входа.
                PropKit.Box(ext, "EntryLanternBracket", new Vector3(sx * 0.92f, 2.35f, 7.25f), new Vector3(0.08f, 0.08f, 0.34f), mats.RustyMetal, false);
                PropKit.Box(ext, "EntryLanternGlass", new Vector3(sx * 0.92f, 2.15f, 7.42f), new Vector3(0.20f, 0.30f, 0.20f), mats.StainedAmber ?? mats.Flame, false);
            }

            // Двускатный козырёк портика.
            GameObject pRoofL = PropKit.Box(ext, "PorticoRoofL", new Vector3(-0.92f, 3.45f, 7.9f), new Vector3(2.15f, 0.16f, 2.1f), ab.WoodDark, false);
            pRoofL.transform.localRotation = Quaternion.Euler(0f, 0f, 24f);
            GameObject pRoofR = PropKit.Box(ext, "PorticoRoofR", new Vector3(0.92f, 3.45f, 7.9f), new Vector3(2.15f, 0.16f, 2.1f), ab.WoodDark, false);
            pRoofR.transform.localRotation = Quaternion.Euler(0f, 0f, -24f);
            PropKit.Box(ext, "PorticoCrossV", new Vector3(0f, 4.18f, 8.85f), new Vector3(0.08f, 0.65f, 0.08f), mats.BrassAged, false);
            PropKit.Box(ext, "PorticoCrossH", new Vector3(0f, 4.30f, 8.85f), new Vector3(0.38f, 0.08f, 0.08f), mats.BrassAged, false);

            // Тёплый уличный свет под портиком.
            GameObject porchLightObj = new GameObject("ChapelPorchLight");
            porchLightObj.transform.SetParent(ext, false);
            porchLightObj.transform.localPosition = new Vector3(0f, 2.55f, 8.0f);
            Light porchLight = porchLightObj.AddComponent<Light>();
            porchLight.type = LightType.Point;
            porchLight.color = new Color(1.0f, 0.70f, 0.34f);
            porchLight.intensity = 9f;
            porchLight.range = 13f;
            porchLightObj.AddComponent<FlickerLight>();

            // Дорожка из каменных плит от входа (z = 9.2) до ворот кладбища (z = 17.6).
            for (int i = 0; i < 8; i++)
            {
                float pz = 9.5f + i * 1.12f;
                float jx = ((i % 2) == 0 ? -0.10f : 0.10f);
                PropKit.Box(ext, "PathStone_" + i, new Vector3(jx, 0.025f, pz), new Vector3(2.1f, 0.05f, 0.85f), mats.Stone, false);
            }

            // Плющ на стенах часовни.
            if (ab != null && ab.Vine != null)
            {
                ApartmentBlockBuilder.Decal(ext, ab.Vine, new Vector3(-5.18f, 2.6f, 1.5f), Vector3.left, new Vector2(3.6f, 4.2f), 0f);
                ApartmentBlockBuilder.Decal(ext, ab.Vine, new Vector3(5.18f, 2.4f, -2.5f), Vector3.right, new Vector2(3.2f, 3.8f), 0f);
            }
        }

        // ------------------------------------------------------------------ амбар

        // Локальные оси амбара: x - ширина (13 м), z - глубина (11 м), вход с z = +5.5 по центру, высота 4.8.
        public static void BuildBarnExtras(Transform barn, ProceduralMaterialLibrary mats, AbandonedMaterials ab)
        {
            if (barn == null) return;
            Transform t = new GameObject("BarnExtras").transform;
            t.SetParent(barn, false);

            // 1. Настоящая двускатная крыша с коньком и деревянными фронтонами поверх плоской плиты BuildBuilding.
            const float bw = 13.0f;
            const float bd = 11.0f;
            const float eaveY = 4.92f;
            const float ridgeRise = 2.75f;
            float halfSpan = bw * 0.5f + 0.65f;
            float slopeLen = Mathf.Sqrt(halfSpan * halfSpan + ridgeRise * ridgeRise) + 0.2f;
            float slopeAng = Mathf.Atan2(ridgeRise, halfSpan) * Mathf.Rad2Deg;

            GameObject roofL = PropKit.Box(t, "BarnGableRoof_L", new Vector3(-halfSpan * 0.5f, eaveY + ridgeRise * 0.5f, 0f),
                new Vector3(slopeLen, 0.18f, bd + 1.4f), mats.RustyMetal, true);
            roofL.transform.localRotation = Quaternion.Euler(0f, 0f, slopeAng);
            GameObject roofR = PropKit.Box(t, "BarnGableRoof_R", new Vector3(halfSpan * 0.5f, eaveY + ridgeRise * 0.5f, 0f),
                new Vector3(slopeLen, 0.18f, bd + 1.4f), mats.RustyMetal, true);
            roofR.transform.localRotation = Quaternion.Euler(0f, 0f, -slopeAng);
            PropKit.Box(t, "BarnRidgeCap", new Vector3(0f, eaveY + ridgeRise + 0.05f, 0f),
                new Vector3(0.36f, 0.18f, bd + 1.5f), ab.WoodDark, false);

            // Передний и задний треугольные фронтоны.
            for (int step = 0; step < 6; step++)
            {
                float stepH = ridgeRise / 6f;
                float sy = eaveY + stepH * (step + 0.5f);
                float sw = bw * (1f - (step + 0.45f) / 6.2f);
                PropKit.Box(t, "BarnGableF_" + step, new Vector3(0f, sy, bd * 0.5f - 0.12f), new Vector3(sw, stepH + 0.03f, 0.24f), mats.WoodPlanks, false);
                PropKit.Box(t, "BarnGableB_" + step, new Vector3(0f, sy, -bd * 0.5f + 0.12f), new Vector3(sw, stepH + 0.03f, 0.24f), mats.WoodPlanks, false);
            }

            // Слуховое чердачное окно на переднем фронтоне.
            PropKit.Box(t, "GableWindowFrame", new Vector3(0f, eaveY + 1.1f, bd * 0.5f + 0.03f), new Vector3(1.2f, 1.0f, 0.10f), ab.WoodDark, false);
            PropKit.Box(t, "GableWindowGlass", new Vector3(0f, eaveY + 1.1f, bd * 0.5f + 0.06f), new Vector3(1.0f, 0.8f, 0.04f), mats.DarkGlass, false);

            // 2. Внешний боковой деревянный навес справа (+X) с поленницей и бочкой.
            GameObject leanRoof = PropKit.Box(t, "LeanToRoof", new Vector3(bw * 0.5f + 1.55f, 2.75f, -0.5f),
                new Vector3(3.4f, 0.14f, 6.8f), mats.RustyMetal, true);
            leanRoof.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
            foreach (float lz in new[] { -3.6f, -0.5f, 2.6f })
            {
                PropKit.Box(t, "LeanToPost", new Vector3(bw * 0.5f + 3.0f, 1.18f, lz), new Vector3(0.20f, 2.36f, 0.20f), ab.WoodDark, true);
            }
            PropKit.Box(t, "LeanToFirewood", new Vector3(bw * 0.5f + 1.1f, 0.55f, -2.0f), new Vector3(1.2f, 1.1f, 2.8f), mats.TreeBark, true);
            PropKit.Cylinder(t, "RainBarrel", new Vector3(bw * 0.5f + 1.0f, 0.48f, 2.4f), new Vector3(0.68f, 0.48f, 0.68f), ab.WoodDark, true);

            // 3. Балки перекрытия и стойки вдоль стен внутри.
            foreach (float z in new[] { -4.2f, -1.4f, 1.4f, 4.2f })
            {
                PropKit.Box(t, "TieBeam", new Vector3(0f, 4.15f, z), new Vector3(12.4f, 0.26f, 0.26f), mats.WoodPlanks, false);
                PropKit.Box(t, "PostL", new Vector3(-6.0f, 2.0f, z), new Vector3(0.30f, 4.0f, 0.30f), mats.WoodPlanks, false);
                PropKit.Box(t, "PostR", new Vector3(6.0f, 2.0f, z), new Vector3(0.30f, 4.0f, 0.30f), mats.WoodPlanks, false);
                PropKit.Box(t, "RafterL", new Vector3(-3.0f, 4.45f, z), new Vector3(6.4f, 0.14f, 0.14f), mats.WoodPlanks, false).transform.localRotation = Quaternion.Euler(0f, 0f, 9f);
                PropKit.Box(t, "RafterR", new Vector3(3.0f, 4.45f, z), new Vector3(6.4f, 0.14f, 0.14f), mats.WoodPlanks, false).transform.localRotation = Quaternion.Euler(0f, 0f, -9f);
            }

            // 4. Сеновал в глубине амбара.
            PropKit.Box(t, "HayloftFloor", new Vector3(0f, 2.5f, -3.3f), new Vector3(12.2f, 0.18f, 3.9f), mats.WoodPlanks, true);
            foreach (float x in new[] { -5.2f, 0f, 5.2f })
            {
                PropKit.Box(t, "HayloftPost", new Vector3(x, 1.25f, -1.4f), new Vector3(0.26f, 2.5f, 0.26f), mats.WoodPlanks, true);
            }

            PropKit.Box(t, "HayloftRail", new Vector3(0f, 3.05f, -1.4f), new Vector3(12.0f, 0.08f, 0.08f), mats.WoodPlanks, false);
            for (int i = 0; i < 4; i++)
            {
                PropKit.Box(t, "LoftHay" + i, new Vector3(-4.4f + i * 2.9f, 2.95f, -4.0f + (i % 2) * 0.6f), new Vector3(1.7f, 0.8f, 1.3f), ab.Sandbag, false);
            }

            // Приставная лестница на сеновал.
            foreach (float dx in new[] { -0.25f, 0.25f })
            {
                PropKit.Box(t, "LadderRail", new Vector3(4.6f + dx, 1.3f, -0.9f), new Vector3(0.06f, 2.75f, 0.06f), mats.WoodPlanks, false).transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            }

            for (int i = 0; i < 8; i++)
            {
                PropKit.Box(t, "LadderRung", new Vector3(4.6f, 0.3f + i * 0.3f, -0.85f - i * 0.003f), new Vector3(0.5f, 0.05f, 0.05f), mats.WoodPlanks, false);
            }

            // 5. Стойла вдоль левой стены с кормушками.
            foreach (float z in new[] { 0.2f, 2.2f, 4.2f })
            {
                PropKit.Box(t, "StallDivider", new Vector3(-5.1f, 0.75f, z), new Vector3(2.1f, 1.5f, 0.10f), mats.WoodPlanks, true);
            }

            foreach (float z in new[] { 1.2f, 3.2f })
            {
                PropKit.Box(t, "FeedTrough", new Vector3(-5.9f, 0.45f, z), new Vector3(0.5f, 0.3f, 1.6f), mats.WoodPlanks, true);
                PropKit.Box(t, "StallStraw", new Vector3(-5.0f, 0.06f, z), new Vector3(1.6f, 0.10f, 1.6f), ab.Sandbag, false);
            }

            // 6. Верстак плотника и стеллаж с канистрами под сеновалом.
            PropKit.Box(t, "BarnWorkbenchTop", new Vector3(-2.6f, 0.92f, -4.45f), new Vector3(2.4f, 0.10f, 0.85f), mats.WoodPlanks, true);
            foreach (float wx in new[] { -3.65f, -1.55f })
                foreach (float wz in new[] { -4.75f, -4.15f })
                    PropKit.Box(t, "BarnWorkbenchLeg", new Vector3(wx, 0.46f, wz), new Vector3(0.10f, 0.92f, 0.10f), ab.WoodDark, false);
            PropKit.Box(t, "BarnVise", new Vector3(-1.7f, 1.05f, -4.1f), new Vector3(0.24f, 0.18f, 0.26f), mats.RustyMetal, false);
            PropKit.Box(t, "BarnFuelCan", new Vector3(-3.3f, 1.16f, -4.45f), new Vector3(0.22f, 0.36f, 0.14f), mats.FuelCanRed, false);
            PropKit.Box(t, "BarnToolBox", new Vector3(-2.4f, 1.08f, -4.45f), new Vector3(0.48f, 0.22f, 0.24f), mats.RustyMetal, false);

            // 7. Инструменты на правой стене: вилы, лопата, колесо от телеги.
            PropKit.Cylinder(t, "PitchforkHandle", new Vector3(6.0f, 1.7f, 2.4f), new Vector3(0.05f, 0.95f, 0.05f), mats.WoodPlanks, false);
            PropKit.Box(t, "PitchforkHead", new Vector3(6.0f, 2.7f, 2.4f), new Vector3(0.05f, 0.05f, 0.42f), mats.RustyMetal, false);
            foreach (float dz in new[] { -0.18f, 0f, 0.18f })
            {
                PropKit.Box(t, "PitchforkTine", new Vector3(6.0f, 2.9f, 2.4f + dz), new Vector3(0.03f, 0.40f, 0.03f), mats.RustyMetal, false);
            }

            PropKit.Cylinder(t, "ShovelHandle", new Vector3(6.0f, 1.6f, 3.3f), new Vector3(0.05f, 0.85f, 0.05f), mats.WoodPlanks, false);
            PropKit.Box(t, "ShovelBlade", new Vector3(6.0f, 2.6f, 3.3f), new Vector3(0.04f, 0.38f, 0.28f), mats.RustyMetal, false);
            GameObject wheel = PropKit.Cylinder(t, "CartWheelOnWall", new Vector3(6.05f, 2.0f, -0.2f), new Vector3(0.95f, 0.05f, 0.95f), mats.WoodPlanks, false);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // Ящики, бочки и мешки на свободном месте у входа.
            PropKit.Box(t, "Crate1", new Vector3(4.6f, 0.4f, 3.8f), new Vector3(0.9f, 0.8f, 0.9f), mats.WoodPlanks, true);
            PropKit.Box(t, "Crate2", new Vector3(4.6f, 1.05f, 3.8f), new Vector3(0.7f, 0.5f, 0.7f), mats.WoodPlanks, false);
            PropKit.Cylinder(t, "Barrel1", new Vector3(3.4f, 0.48f, 4.6f), new Vector3(0.6f, 0.48f, 0.6f), mats.RustyMetal, true);
            PropKit.Box(t, "Sack1", new Vector3(5.2f, 0.28f, 2.4f), new Vector3(0.5f, 0.55f, 0.4f), mats.ClothRoll, false);

            // Большие створки ворот на фасаде (по бокам от двери).
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 2.55f;
                PropKit.Box(t, "BarnGate", new Vector3(x, 1.9f, 5.62f), new Vector3(2.4f, 3.8f, 0.10f), mats.WoodPlanks, false);
                PropKit.Box(t, "GateBraceA", new Vector3(x, 1.9f, 5.69f), new Vector3(3.1f, 0.16f, 0.05f), ab.WoodDark, false).transform.localRotation = Quaternion.Euler(0f, 0f, 52f);
                PropKit.Box(t, "GateBraceB", new Vector3(x, 1.9f, 5.69f), new Vector3(3.1f, 0.16f, 0.05f), ab.WoodDark, false).transform.localRotation = Quaternion.Euler(0f, 0f, -52f);
                PropKit.Box(t, "GateFrameTop", new Vector3(x, 3.85f, 5.69f), new Vector3(2.5f, 0.14f, 0.06f), ab.WoodDark, false);
            }

            // Люк сеновала, балка подъёмника и наружный фонарь над входом.
            PropKit.Box(t, "HayDoor", new Vector3(0f, 3.9f, 5.62f), new Vector3(1.1f, 0.9f, 0.08f), ab.WoodDark, false);
            PropKit.Box(t, "HoistBeam", new Vector3(0f, 4.45f, 6.15f), new Vector3(0.18f, 0.18f, 1.3f), mats.WoodPlanks, false);
            PropKit.Box(t, "HoistPulley", new Vector3(0f, 4.2f, 6.7f), new Vector3(0.12f, 0.22f, 0.22f), mats.RustyMetal, false);
            PropKit.Box(t, "BarnExteriorLantern", new Vector3(0f, 3.15f, 5.75f), new Vector3(0.24f, 0.28f, 0.24f), mats.StainedAmber ?? mats.Flame, false);

            GameObject extLamp = new GameObject("BarnExteriorLight");
            extLamp.transform.SetParent(t, false);
            extLamp.transform.localPosition = new Vector3(0f, 3.05f, 6.15f);
            Light extLight = extLamp.AddComponent<Light>();
            extLight.type = LightType.Point;
            extLight.range = 12f;
            extLight.intensity = 7.5f;
            extLight.color = new Color(1f, 0.72f, 0.36f);
            extLamp.AddComponent<FlickerLight>();

            // Подвешенный фонарь внутри амбара с мягким тёплым мерцанием.
            PropKit.Box(t, "LanternChain", new Vector3(0f, 3.55f, 0f), new Vector3(0.02f, 1.2f, 0.02f), mats.RustyMetal, false);
            PropKit.Box(t, "LanternBody", new Vector3(0f, 2.85f, 0f), new Vector3(0.22f, 0.3f, 0.22f), mats.RustyMetal, false);
            PropKit.Box(t, "LanternFlame", new Vector3(0f, 2.85f, 0f), new Vector3(0.08f, 0.14f, 0.08f), mats.Flame, false);
            GameObject lamp = new GameObject("BarnLanternLight");
            lamp.transform.SetParent(t, false);
            lamp.transform.localPosition = new Vector3(0f, 2.85f, 0f);
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 6.0f;
            light.color = new Color(1f, 0.72f, 0.40f);
            lamp.AddComponent<FlickerLight>();
        }
    }
}
