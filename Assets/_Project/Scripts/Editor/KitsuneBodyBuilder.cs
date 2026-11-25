using UnityEngine;
using static Ogni.EditorTools.MonsterBuildKit;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Модель Кицунэ: рыжая девочка в рваном светлом платье. Все детали, которыми управляет KitsuneAnimator
    /// (улыбка, зубы, язык, глаза, когти, волосы), - отдельные объекты с фиксированными именами:
    /// Body, Skirt, Head, Smile_i, MouthHole, Tooth_i/ToothLow_i, Tongue, EyeWhite_/EyeBlack_, ShoulderL/R, ElbowL/R, HandL/R, Claw_*, Hair_i, Wisp_i.
    /// Размеры черепа (Rx/Ry/Rz/Cy) должны совпадать с константами аниматора.
    /// </summary>
    public static class KitsuneBodyBuilder
    {
        public static void Build(Transform rig)
        {
            Material skin = DetailMaterials.Solid("KS_Skin", new Color(0.90f, 0.82f, 0.76f), 0f, 0.28f);
            Material dress = DetailMaterials.Solid("KS_Dress", new Color(0.78f, 0.76f, 0.70f), 0f, 0.12f);
            Material dressDirt = DetailMaterials.Solid("KS_DressDirt", new Color(0.50f, 0.46f, 0.40f), 0f, 0.1f);
            Material hair = DetailMaterials.Solid("KS_Hair", new Color(0.58f, 0.16f, 0.06f), 0f, 0.35f);
            Material eyeWhite = DetailMaterials.Solid("KS_EyeWhite", new Color(0.92f, 0.92f, 0.90f), 0f, 0.6f);
            Material eyeBlack = DetailMaterials.Solid("KS_EyeBlack", new Color(0.01f, 0.01f, 0.01f), 0f, 0.9f);
            Material lip = DetailMaterials.Solid("KS_Lip", new Color(0.36f, 0.07f, 0.09f), 0f, 0.4f);
            Material mouth = DetailMaterials.Solid("KS_Mouth", new Color(0.02f, 0.0f, 0.0f), 0f, 0.5f);
            Material tooth = DetailMaterials.Solid("KS_Tooth", new Color(0.86f, 0.83f, 0.70f), 0f, 0.5f);
            Material tongue = DetailMaterials.Solid("KS_Tongue", new Color(0.55f, 0.08f, 0.12f), 0f, 0.65f);
            Material claw = DetailMaterials.Solid("KS_Claw", new Color(0.08f, 0.06f, 0.05f), 0f, 0.45f);
            Material orb = Emissive("KS_Wisp", new Color(1f, 0.70f, 0.30f), 3f);

            Mesh cone = UnitCone("KS_Cone");

            Transform body = Pivot(rig, "Body", Vector3.zero);

            // ---- Платье: расклёшенный подол и тело
            Mesh skirt = Lathe("KS_Skirt", 18,
                new Vector2(0.05f, 0.02f), new Vector2(0.34f, 0.02f), new Vector2(0.31f, 0.18f), new Vector2(0.23f, 0.50f),
                new Vector2(0.17f, 0.78f), new Vector2(0.14f, 0.96f));
            Part(body, "Skirt", skirt, dress, Vector3.zero, Vector3.zero);
            // Рваный грязный подол: тёмная кайма.
            Mesh hem = Lathe("KS_Hem", 18, new Vector2(0.345f, 0.015f), new Vector2(0.335f, 0.09f), new Vector2(0.318f, 0.14f));
            Part(body, "Hem", hem, dressDirt, Vector3.zero, Vector3.zero);

            Mesh torso = Lathe("KS_Torso", 14,
                new Vector2(0.13f, 0.94f), new Vector2(0.15f, 1.08f), new Vector2(0.145f, 1.24f), new Vector2(0.11f, 1.36f), new Vector2(0.05f, 1.42f));
            Part(body, "Torso", torso, dress, Vector3.zero, Vector3.zero, new Vector3(1f, 1f, 0.72f));
            Part(body, "Neck", Tube("KS_Neck", new[] { new Vector3(0f, 1.36f, 0f), new Vector3(0f, 1.50f, 0.005f) }, new[] { 0.042f, 0.036f }, 10), skin, Vector3.zero, Vector3.zero);

            // ---- Руки с когтями
            for (int s = -1; s <= 1; s += 2)
            {
                string tag = s < 0 ? "L" : "R";
                Transform sh = Pivot(body, "Shoulder" + tag, new Vector3(s * 0.17f, 1.34f, 0f));
                Part(sh, "Sleeve", Tube("KS_Sleeve" + tag, new[] { new Vector3(0f, 0.03f, 0f), new Vector3(0f, -0.14f, 0f) }, new[] { 0.058f, 0.05f }, 10), dress, Vector3.zero, Vector3.zero);
                Part(sh, "UpperArm", Tube("KS_UpperArm" + tag, new[] { new Vector3(0f, -0.12f, 0f), new Vector3(0f, -0.26f, 0.004f) }, new[] { 0.036f, 0.032f }, 10), skin, Vector3.zero, Vector3.zero);
                Prim(sh, "ShoulderBall", PrimitiveType.Sphere, Vector3.zero, Vector3.zero, Vector3.one * 0.1f, dress);

                Transform el = Pivot(sh, "Elbow" + tag, new Vector3(0f, -0.26f, 0f));
                Part(el, "Forearm", Tube("KS_Forearm" + tag, new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, -0.24f, 0f) }, new[] { 0.032f, 0.024f }, 10), skin, Vector3.zero, Vector3.zero);

                Transform hand = Pivot(el, "Hand" + tag, new Vector3(0f, -0.24f, 0f));
                Prim(hand, "Palm", PrimitiveType.Sphere, new Vector3(0f, -0.04f, 0f), Vector3.zero, new Vector3(0.05f, 0.08f, 0.028f), skin);
                for (int c = 0; c < 4; c++)
                {
                    float x = (c - 1.5f) * 0.014f;
                    Prim(hand, "Finger_" + tag + c, PrimitiveType.Capsule, new Vector3(x, -0.095f, 0f), Vector3.zero, new Vector3(0.011f, 0.04f, 0.011f), skin);
                    // Коготь смотрит вниз; длина растёт при превращении.
                    Part(hand, "Claw_" + tag + "_" + c, cone, claw, new Vector3(x, -0.13f, 0.002f), new Vector3(180f, 0f, 0f), new Vector3(0.0075f, 0.085f, 0.0075f));
                }
            }

            // ---- Голова
            Transform head = Pivot(body, "Head", new Vector3(0f, 1.5f, 0f));
            Prim(head, "Skull", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(0.22f, 0.25f, 0.23f), skin);
            Prim(head, "Chin", PrimitiveType.Sphere, new Vector3(0f, 0.025f, 0.03f), Vector3.zero, new Vector3(0.12f, 0.09f, 0.11f), skin);
            Prim(head, "Nose", PrimitiveType.Sphere, new Vector3(0f, 0.098f, 0.116f), Vector3.zero, new Vector3(0.026f, 0.03f, 0.03f), skin);
            for (int s = -1; s <= 1; s += 2)
            {
                string tag = s < 0 ? "L" : "R";
                Prim(head, "Ear_" + tag, PrimitiveType.Sphere, new Vector3(s * 0.108f, 0.11f, -0.005f), Vector3.zero, new Vector3(0.018f, 0.045f, 0.03f), skin);
                Prim(head, "EyeWhite_" + tag, PrimitiveType.Sphere, new Vector3(s * 0.045f, 0.14f, 0.100f), Vector3.zero, new Vector3(0.036f, 0.026f, 0.02f), eyeWhite, false);
                Prim(head, "EyeBlack_" + tag, PrimitiveType.Sphere, new Vector3(s * 0.045f, 0.14f, 0.106f), Vector3.zero, new Vector3(0.034f, 0.030f, 0.014f), eyeBlack, false);
            }

            // Рот: бусины улыбки, тёмная полость, зубы, язык - позиции каждый кадр задаёт аниматор.
            for (int i = 0; i < 11; i++)
                Prim(head, "Smile_" + i, PrimitiveType.Sphere, new Vector3(0f, 0.055f, 0.11f), Vector3.zero, Vector3.one * 0.006f, lip, false);
            Prim(head, "MouthHole", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.105f), Vector3.zero, new Vector3(0.04f, 0.001f, 0.012f), mouth, false);
            for (int i = 0; i < 10; i++)
                Part(head, "Tooth_" + i, cone, tooth, new Vector3(0f, 0.05f, 0.11f), new Vector3(180f, 0f, 0f), new Vector3(0.0065f, 0.017f, 0.0065f), false);
            for (int i = 0; i < 9; i++)
                Part(head, "ToothLow_" + i, cone, tooth, new Vector3(0f, 0.04f, 0.11f), new Vector3(180f, 0f, 0f), new Vector3(0.0058f, 0.014f, 0.0058f), false);

            Transform tong = Pivot(head, "Tongue", new Vector3(0f, 0.04f, 0.10f), new Vector3(42f, 0f, 0f));
            Part(tong, "Mesh", Tube("KS_Tongue", new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, -0.01f, 0.16f), new Vector3(0f, -0.035f, 0.32f), new Vector3(0f, -0.07f, 0.5f) },
                new[] { 0.014f, 0.012f, 0.010f, 0.006f }, 8), tongue, Vector3.zero, Vector3.zero, null, false);
            tong.localScale = new Vector3(1f, 1f, 0.02f);

            // ---- Волосы: шапка сзади и сверху (лицо открыто) и семь длинных прядей
            Prim(head, "HairCap", PrimitiveType.Sphere, new Vector3(0f, 0.14f, -0.03f), Vector3.zero, new Vector3(0.25f, 0.27f, 0.24f), hair);
            Prim(head, "Bangs", PrimitiveType.Sphere, new Vector3(0f, 0.222f, 0.045f), new Vector3(-20f, 0f, 0f), new Vector3(0.20f, 0.07f, 0.10f), hair);
            for (int i = 0; i < 7; i++)
            {
                float x = (i - 3) * 0.028f;
                Transform h = Pivot(head, "Hair_" + i, new Vector3(x, 0.10f, -0.092f));
                Vector3[] path = { new Vector3(0f, 0f, 0f), new Vector3(0f, -0.2f, -0.035f), new Vector3(x * 0.4f, -0.45f, -0.03f), new Vector3(x * 0.8f, -0.68f, -0.005f) };
                float[] r = { 0.026f, 0.022f, 0.016f, 0.006f };
                Part(h, "Mesh", Tube("KS_Hair_" + i, path, r, 7), hair, Vector3.zero, Vector3.zero);
            }

            // ---- Тёплые огоньки-приманки (обычного цвета, не синие)
            Vector3[] wisp = { new Vector3(0.85f, 1.35f, 0.4f), new Vector3(-0.75f, 1.55f, 0.2f), new Vector3(0.15f, 1.85f, -0.6f) };
            for (int i = 0; i < wisp.Length; i++)
            {
                GameObject w = new GameObject("Wisp_" + i);
                w.transform.SetParent(rig, false);
                w.transform.localPosition = wisp[i];
                Light l = w.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 6f;
                l.intensity = 1.4f;
                l.color = new Color(1f, 0.72f, 0.32f);
                Prim(w.transform, "Orb", PrimitiveType.Sphere, Vector3.zero, Vector3.zero, Vector3.one * 0.12f, orb, false);
            }
        }
    }
}
