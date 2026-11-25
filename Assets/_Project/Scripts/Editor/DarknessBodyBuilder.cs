using UnityEngine;
using static Ogni.EditorTools.MonsterBuildKit;

namespace Ogni.EditorTools
{
    /// <summary>
    /// Модель Тьмы: чёрная колышущаяся масса с бледными тлеющими глазами, дюжиной щупалец у земли и двумя длинными
    /// «боевыми» щупальцами (Lash_0/1), которыми она бьёт. Щупальца - пивоты Tendril_i, их двигает DarknessAnimator.
    /// </summary>
    public static class DarknessBodyBuilder
    {
        public static void Build(Transform rig)
        {
            Material mass = DetailMaterials.Solid("DK_Mass", new Color(0.012f, 0.012f, 0.016f), 0f, 0.1f);
            Material tendril = DetailMaterials.Solid("DK_Tendril", new Color(0.02f, 0.02f, 0.026f), 0f, 0.18f);
            Material eye = Emissive("DK_Eye", new Color(0.82f, 0.9f, 1f), 2.6f);
            Material shadow = DetailMaterials.Solid("DK_Shadow", new Color(0f, 0f, 0f), 0f, 0f);

            // Тело - неровная капля, чуть сутулая.
            Mesh coreMesh = Lathe("DK_Core", 14,
                new Vector2(0f, 0f), new Vector2(0.34f, 0.04f), new Vector2(0.52f, 0.30f), new Vector2(0.50f, 0.62f),
                new Vector2(0.38f, 0.98f), new Vector2(0.22f, 1.25f), new Vector2(0.09f, 1.42f), new Vector2(0f, 1.48f));
            Part(rig, "Core", coreMesh, mass, new Vector3(0f, 0.35f, 0f), new Vector3(8f, 0f, 0f), new Vector3(1f, 1f, 0.9f));

            // Глаза: три бледных огня на «лице».
            Vector3[] eyes = { new Vector3(-0.15f, 1.28f, 0.34f), new Vector3(0.15f, 1.28f, 0.34f), new Vector3(0f, 1.05f, 0.40f) };
            for (int i = 0; i < eyes.Length; i++)
                Prim(rig, "Eye_" + i, PrimitiveType.Sphere, eyes[i], Vector3.zero, new Vector3(0.075f, 0.075f, 0.04f), eye, false);

            // Щупальца у земли и «руки» повыше - равномерно по кругу, разной длины.
            for (int i = 0; i < 12; i++)
            {
                float yaw = i * 30f + (i % 3) * 7f;
                bool upper = i % 3 == 0;
                float len = 1.0f + (i * 37 % 7) * 0.09f;
                Vector3[] path = upper
                    ? new[] { new Vector3(0f, 0f, 0f), new Vector3(0.28f * len, 0.34f, 0f), new Vector3(0.62f * len, 0.42f, 0f), new Vector3(0.95f * len, 0.20f, 0f), new Vector3(1.20f * len, -0.12f, 0f) }
                    : new[] { new Vector3(0f, 0f, 0f), new Vector3(0.30f * len, 0.20f, 0f), new Vector3(0.65f * len, 0.10f, 0f), new Vector3(1.05f * len, 0.03f, 0f), new Vector3(1.40f * len, 0.01f, 0f) };
                float[] r = { 0.11f, 0.085f, 0.06f, 0.035f, 0.008f };
                Transform t = Pivot(rig, "Tendril_" + i, new Vector3(0f, upper ? 0.95f : 0.28f, 0f), new Vector3(0f, yaw, 0f));
                Part(t, "Mesh", Tube("DK_Tendril_" + i, path, r, 7), tendril, Vector3.zero, Vector3.zero);
            }

            // Боевые щупальца: вперёд, длинные, с острым концом.
            for (int s = -1; s <= 1; s += 2)
            {
                Transform lash = Pivot(rig, s < 0 ? "Lash_0" : "Lash_1", new Vector3(s * 0.28f, 0.95f, 0.18f));
                Vector3[] path = { new Vector3(0f, 0f, 0f), new Vector3(s * 0.05f, 0.06f, 0.55f), new Vector3(s * 0.04f, -0.04f, 1.1f), new Vector3(0f, -0.2f, 1.7f), new Vector3(-s * 0.04f, -0.5f, 2.2f) };
                float[] r = { 0.09f, 0.075f, 0.055f, 0.03f, 0.006f };
                Part(lash, "Mesh", Tube("DK_Lash_" + (s < 0 ? 0 : 1), path, r, 7), tendril, Vector3.zero, Vector3.zero);
            }

            // Пятно тьмы на земле.
            Prim(rig, "Shadow", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), Vector3.zero, new Vector3(3.4f, 0.004f, 3.4f), shadow, false);
        }
    }
}
