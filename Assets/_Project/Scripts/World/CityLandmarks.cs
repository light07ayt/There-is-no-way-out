using System.Collections;
using UnityEngine;

namespace Ogni.World
{
    /// <summary>
    /// Магазин и заправка на первом перекрёстке города (-16, 70) и (17, 70).
    /// Если построек в сцене нет (старая сцена или их удалили), они достраиваются на лету; если есть - только
    /// получают вывески «МАГАЗИН» / «АЗС», чтобы их можно было отличить от обычных домов.
    /// Всё статично и одинаково у всех игроков (в сеть не уходит).
    /// </summary>
    public sealed class CityLandmarks : MonoBehaviour
    {
        private static readonly Vector3 ShopPos = new Vector3(-16f, 0f, 70f);
        private static readonly Vector3 GasPos = new Vector3(17f, 0f, 70f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CityLandmarks>() != null) return;
            new GameObject("CityLandmarks").AddComponent<CityLandmarks>();
        }

        private IEnumerator Start()
        {
            // Ждём, пока сцена и террейн полностью поднимутся.
            yield return new WaitForSeconds(1.5f);

            Material wall = MakeMat(new Color(0.42f, 0.30f, 0.26f));
            Material roof = MakeMat(new Color(0.20f, 0.20f, 0.21f));
            Material metal = MakeMat(new Color(0.45f, 0.18f, 0.12f));
            Material signMat = MakeMat(new Color(0.85f, 0.80f, 0.70f));

            Transform shop = GameObject.Find("City_Shop")?.transform;
            if (shop == null) shop = BuildBox("City_Shop", ShopPos, 90f, new Vector3(10f, 3.6f, 9f), wall, roof);
            AddSign(shop, "МАГАЗИН", 3.6f, 9f, signMat);

            Transform kiosk = GameObject.Find("City_GasStationKiosk")?.transform;
            if (kiosk == null)
            {
                kiosk = BuildBox("City_GasStationKiosk", GasPos, -90f, new Vector3(7f, 3.2f, 6f), wall, roof);
                BuildPumps(metal, roof);
            }
            AddSign(kiosk, "АЗС", 3.2f, 6f, signMat);
        }

        private static Material MakeMat(Color c)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var m = new Material(sh);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            return m;
        }

        private static float GroundY(Vector3 p)
        {
            Terrain t = Terrain.activeTerrain;
            return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y;
        }

        private static GameObject Cube(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, bool collider = true)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Destroy(g.GetComponent<Collider>());
            return g;
        }

        // Простая коробка с дверным проёмом спереди (+Z локально) и полом, чтобы внутрь можно было зайти.
        private static Transform BuildBox(string name, Vector3 pos, float yaw, Vector3 size, Material wall, Material roof)
        {
            pos.y = GroundY(pos);
            var root = new GameObject(name).transform;
            root.position = pos;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);

            float w = size.x, h = size.y, d = size.z, t = 0.25f, door = 1.6f;
            Cube(root, "Floor", new Vector3(0f, 0.03f, 0f), new Vector3(w, 0.06f, d), roof);
            Cube(root, "Roof", new Vector3(0f, h + 0.1f, 0f), new Vector3(w + 0.6f, 0.22f, d + 0.6f), roof);
            Cube(root, "BackWall", new Vector3(0f, h * 0.5f, -d * 0.5f + t * 0.5f), new Vector3(w, h, t), wall);
            Cube(root, "LeftWall", new Vector3(-w * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), wall);
            Cube(root, "RightWall", new Vector3(w * 0.5f - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), wall);
            float side = (w - door) * 0.5f;
            float cx = door * 0.5f + side * 0.5f;
            Cube(root, "FrontL", new Vector3(-cx, h * 0.5f, d * 0.5f - t * 0.5f), new Vector3(side, h, t), wall);
            Cube(root, "FrontR", new Vector3(cx, h * 0.5f, d * 0.5f - t * 0.5f), new Vector3(side, h, t), wall);
            Cube(root, "DoorHeader", new Vector3(0f, 2.1f + (h - 2.1f) * 0.5f, d * 0.5f - t * 0.5f), new Vector3(door, h - 2.1f, t), wall);
            return root;
        }

        private static void BuildPumps(Material metal, Material roof)
        {
            Vector3 c = new Vector3(11f, 0f, 70f);
            c.y = GroundY(c);
            var root = new GameObject("GasStation_Pumps").transform;
            root.position = c;
            Cube(root, "Roof", new Vector3(0f, 3.4f, 0f), new Vector3(6f, 0.3f, 8f), roof);
            Cube(root, "PillarA", new Vector3(0f, 1.7f, -3f), new Vector3(0.35f, 3.4f, 0.35f), roof);
            Cube(root, "PillarB", new Vector3(0f, 1.7f, 3f), new Vector3(0.35f, 3.4f, 0.35f), roof);
            Cube(root, "Pump1", new Vector3(0f, 0.7f, -1f), new Vector3(0.6f, 1.4f, 0.5f), metal);
            Cube(root, "Pump2", new Vector3(0f, 0.7f, 1f), new Vector3(0.6f, 1.4f, 0.5f), metal);
        }

        private static void AddSign(Transform building, string text, float height, float depth, Material backing)
        {
            if (building == null || building.Find("Landmark_Sign") != null) return;

            var root = new GameObject("Landmark_Sign").transform;
            root.SetParent(building, false);
            // Над входом, снаружи фасада (+Z локально). TextMesh читается со стороны -Z, поэтому разворачиваем на 180°.
            root.localPosition = new Vector3(0f, height + 0.55f, depth * 0.5f + 0.15f);
            Cube(root, "Board", new Vector3(0f, 0f, 0.04f), new Vector3(3.2f, 0.8f, 0.08f), backing, false);

            var go = new GameObject("Text");
            go.transform.SetParent(root, false);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = 0.05f;
            tm.color = new Color(0.12f, 0.08f, 0.06f);
            Font font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }
    }
}
