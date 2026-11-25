using UnityEngine;
using static Ogni.EditorTools.MonsterBuildKit;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Модель Подражателя: высокое сутулое существо с серой влажной кожей, длинными руками до колен, ногами с обратным
    /// коленом, вытянутой гладкой головой без лица (только вмятины на месте глаз) и широкой пастью с множеством мелких зубов.
    /// Суставы-пивоты (PelvisRoot/Pelvis/Spine/Neck/Head/Jaw/Shoulder/Elbow/Hip/Knee) двигает MimicAnimator.
    /// </summary>
    public static class MimicBodyBuilder
    {
        public static void Build(Transform rig)
        {
            Material skin = DetailMaterials.Solid("MM_Skin", new Color(0.55f, 0.52f, 0.47f), 0f, 0.42f);
            Material skinDark = DetailMaterials.Solid("MM_SkinDark", new Color(0.30f, 0.28f, 0.26f), 0f, 0.3f);
            Material bone = DetailMaterials.Solid("MM_Bone", new Color(0.72f, 0.68f, 0.58f), 0f, 0.3f);
            Material mouth = DetailMaterials.Solid("MM_Mouth", new Color(0.14f, 0.02f, 0.03f), 0f, 0.55f);
            Material tooth = DetailMaterials.Solid("MM_Tooth", new Color(0.82f, 0.78f, 0.62f), 0f, 0.45f);
            Material claw = DetailMaterials.Solid("MM_Claw", new Color(0.07f, 0.06f, 0.05f), 0f, 0.4f);
            Material dimple = DetailMaterials.Solid("MM_Dimple", new Color(0.05f, 0.04f, 0.04f), 0f, 0.2f);
            Mesh cone = UnitCone("MM_Cone");

            // PelvisRoot держит базовую высоту, Pelvis двигает аниматор (приседание, покачивание).
            Transform root = Pivot(rig, "PelvisRoot", new Vector3(0f, 0.95f, 0f));
            Transform pelvis = Pivot(root, "Pelvis", Vector3.zero);
            Part(pelvis, "PelvisMesh", Lathe("MM_Pelvis", 12, new Vector2(0f, -0.1f), new Vector2(0.14f, -0.08f), new Vector2(0.17f, 0f), new Vector2(0.14f, 0.09f), new Vector2(0f, 0.12f)),
                skin, Vector3.zero, Vector3.zero, new Vector3(1f, 1f, 0.8f));

            // ---- Ноги с обратным коленом
            for (int s = -1; s <= 1; s += 2)
            {
                string tag = s < 0 ? "L" : "R";
                Transform hip = Pivot(pelvis, "Hip" + tag, new Vector3(s * 0.13f, 0f, 0f), new Vector3(0f, 0f, s * 2f));
                Part(hip, "Thigh", Tube("MM_Thigh", new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, -0.2f, 0.05f), new Vector3(0f, -0.42f, 0.08f) }, new[] { 0.078f, 0.066f, 0.05f }), skin, Vector3.zero, Vector3.zero);
                Prim(hip, "KneeBall", PrimitiveType.Sphere, new Vector3(0f, -0.42f, 0.085f), Vector3.zero, Vector3.one * 0.1f, skinDark);

                Transform knee = Pivot(hip, "Knee" + tag, new Vector3(0f, -0.42f, 0.08f));
                Part(knee, "Shin", Tube("MM_Shin", new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, -0.22f, -0.1f), new Vector3(0f, -0.45f, -0.12f) }, new[] { 0.05f, 0.036f, 0.03f }), skin, Vector3.zero, Vector3.zero);
                Part(knee, "Foot", Tube("MM_Foot", new[] { new Vector3(0f, -0.45f, -0.12f), new Vector3(0f, -0.5f, 0.0f), new Vector3(0f, -0.52f, 0.15f) }, new[] { 0.03f, 0.034f, 0.026f }), skin, Vector3.zero, Vector3.zero);
                for (int c = -1; c <= 1; c++)
                    Part(knee, "ToeClaw" + c, cone, claw, new Vector3(c * 0.022f, -0.52f, 0.17f), new Vector3(90f, c * 8f, 0f), new Vector3(0.011f, 0.09f, 0.011f));
            }

            // ---- Торс: костлявый, с рёбрами и позвонками
            Transform spine = Pivot(pelvis, "Spine", new Vector3(0f, 0.06f, 0.01f), new Vector3(30f, 0f, 0f));
            Part(spine, "Torso", Lathe("MM_Torso", 14, new Vector2(0f, 0f), new Vector2(0.12f, 0.02f), new Vector2(0.15f, 0.16f), new Vector2(0.18f, 0.34f), new Vector2(0.185f, 0.52f), new Vector2(0.14f, 0.64f), new Vector2(0.06f, 0.71f), new Vector2(0f, 0.73f)),
                skin, Vector3.zero, Vector3.zero, new Vector3(1f, 1f, 0.7f));
            for (int i = 0; i < 6; i++)
            {
                float y = 0.2f + i * 0.07f;
                float rx = 0.17f + Mathf.Sin(i / 5f * Mathf.PI) * 0.02f;
                for (int s = -1; s <= 1; s += 2)
                    Prim(spine, "Rib" + s + "_" + i, PrimitiveType.Sphere, new Vector3(s * rx * 0.62f, y, 0.085f), new Vector3(0f, 0f, s * -18f), new Vector3(0.1f, 0.018f, 0.03f), bone);
            }

            for (int i = 0; i < 8; i++)
                Part(spine, "Spike" + i, cone, bone, new Vector3(0f, 0.08f + i * 0.08f, -0.12f - Mathf.Sin(i / 7f * Mathf.PI) * 0.01f), new Vector3(-100f, 0f, 0f), new Vector3(0.016f, 0.05f, 0.016f));

            // ---- Шея и голова без лица
            Transform neck = Pivot(spine, "Neck", new Vector3(0f, 0.7f, 0.02f), new Vector3(-16f, 0f, 0f));
            Part(neck, "NeckMesh", Tube("MM_Neck", new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0.1f, 0.06f), new Vector3(0f, 0.22f, 0.1f) }, new[] { 0.055f, 0.046f, 0.042f }), skin, Vector3.zero, Vector3.zero);

            Transform head = Pivot(neck, "Head", new Vector3(0f, 0.24f, 0.1f));
            Part(head, "Skull", Lathe("MM_Skull", 14, new Vector2(0f, -0.09f), new Vector2(0.07f, -0.075f), new Vector2(0.10f, 0.02f), new Vector2(0.098f, 0.16f), new Vector2(0.065f, 0.26f), new Vector2(0f, 0.31f)),
                skin, new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(1f, 1f, 0.88f));
            for (int s = -1; s <= 1; s += 2)
                Prim(head, "Dimple" + s, PrimitiveType.Sphere, new Vector3(s * 0.04f, 0.15f, 0.083f), Vector3.zero, new Vector3(0.03f, 0.016f, 0.012f), dimple, false);

            // Пасть: полость, верхние зубы на черепе, нижняя челюсть с зубами - пивот Jaw.
            Prim(head, "MouthCavity", PrimitiveType.Sphere, new Vector3(0f, -0.035f, 0.07f), Vector3.zero, new Vector3(0.15f, 0.07f, 0.1f), mouth, false);
            for (int i = 0; i < 12; i++)
            {
                float x = (i - 5.5f) * 0.0125f;
                Part(head, "UTooth" + i, cone, tooth, new Vector3(x, -0.03f, 0.1f - Mathf.Abs(x) * 0.25f), new Vector3(180f, 0f, 0f), new Vector3(0.0052f, 0.016f + (i % 3) * 0.006f, 0.0052f), false);
            }

            Transform jaw = Pivot(head, "Jaw", new Vector3(0f, -0.04f, 0.02f));
            Prim(jaw, "JawBone", PrimitiveType.Sphere, new Vector3(0f, -0.03f, 0.045f), Vector3.zero, new Vector3(0.15f, 0.045f, 0.12f), skin);
            for (int i = 0; i < 12; i++)
            {
                float x = (i - 5.5f) * 0.0118f;
                Part(jaw, "LTooth" + i, cone, tooth, new Vector3(x, -0.012f, 0.08f - Mathf.Abs(x) * 0.25f), Vector3.zero, new Vector3(0.005f, 0.014f + ((i + 1) % 3) * 0.006f, 0.005f), false);
            }

            // ---- Длинные руки до колен
            for (int s = -1; s <= 1; s += 2)
            {
                string tag = s < 0 ? "L" : "R";
                Transform sh = Pivot(spine, "Shoulder" + tag, new Vector3(s * 0.21f, 0.58f, 0f), new Vector3(0f, 0f, s * 8f));
                Prim(sh, "ShoulderBall", PrimitiveType.Sphere, Vector3.zero, Vector3.zero, Vector3.one * 0.12f, skin);
                Part(sh, "UpperArm", Tube("MM_UpperArm", new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, -0.3f, 0.02f), new Vector3(0f, -0.6f, 0f) }, new[] { 0.052f, 0.044f, 0.038f }), skin, Vector3.zero, Vector3.zero);

                Transform el = Pivot(sh, "Elbow" + tag, new Vector3(0f, -0.6f, 0f), new Vector3(-14f, 0f, 0f));
                Prim(el, "ElbowBall", PrimitiveType.Sphere, Vector3.zero, Vector3.zero, Vector3.one * 0.08f, skinDark);
                Part(el, "Forearm", Tube("MM_Forearm", new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, -0.3f, 0.03f), new Vector3(0f, -0.62f, 0f) }, new[] { 0.04f, 0.034f, 0.028f }), skin, Vector3.zero, Vector3.zero);
                Prim(el, "Palm", PrimitiveType.Sphere, new Vector3(0f, -0.66f, 0f), Vector3.zero, new Vector3(0.07f, 0.09f, 0.04f), skin);
                for (int f = 0; f < 4; f++)
                {
                    float x = (f - 1.5f) * 0.02f;
                    Part(el, "Finger" + f, Tube("MM_Finger" + f, new[] { new Vector3(x, -0.7f, 0f), new Vector3(x, -0.84f, 0.02f), new Vector3(x, -0.97f, 0.07f) }, new[] { 0.014f, 0.011f, 0.008f }, 6), skin, Vector3.zero, Vector3.zero);
                    Part(el, "FClaw" + f, cone, claw, new Vector3(x, -0.97f, 0.07f), new Vector3(150f, 0f, 0f), new Vector3(0.0075f, 0.07f, 0.0075f));
                }
            }
        }
    }
}
