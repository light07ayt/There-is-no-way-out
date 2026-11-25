using System.Collections.Generic;
using Ogni.Core;
using UnityEngine;

namespace Ogni.Resources
{
    /// <summary>
    /// Дерево, которое рубят топором. Деревья генерируются локально по общему Seed (не NetworkObject),
    /// поэтому идентифицируются индексом. HP хранит только сервер; клиенты получают от PlayerEquipment
    /// события «удар» (PlayChopHit) и «срублено» (Fell).
    ///
    /// Реалистичная рубка:
    ///  - каждый удар: дерево вздрагивает, летят щепки, на стороне игрока растёт зарубка;
    ///  - последний удар: дерево трещит, начинает крениться и с ускорением падает ОТ игрока, вздрагивает при ударе
    ///    о землю (пыль и хвоя), лежит ствол ещё ~25 с и исчезает, оставляя пень.
    /// </summary>
    public class TreeResource : MonoBehaviour, IDamageable, IWorldIndexed
    {
        [SerializeField] private float health = 100f;
        [SerializeField] private int woodYield = 6;

        private static readonly Dictionary<int, TreeResource> Registry = new();
        private static readonly HashSet<int> Felled = new();
        private static Material _notchMaterial;

        /// <summary>Дерево ударилось о землю (позиция вершины). Локальный эффект: игрок рядом чувствует толчок.</summary>
        public static event System.Action<Vector3> Landed;

        private float _hp;
        private int _index = -1;

        // Визуальное состояние.
        private Transform _model;
        private float _shakeTwist;
        private bool _cracked;
        private float _damage01;
        private float _shakeStart = -10f;
        private Vector3 _shakeDir = Vector3.forward;
        private bool _falling;
        private bool _impacted;
        private float _fallStart;
        private Vector3 _fallDir;
        private Quaternion _baseRot;
        private bool _stumpMade;

        public int Index => _index;
        public int WoodYield => woodYield;
        public bool IsAlive => _hp > 0f;
        public float HealthFraction => Mathf.Clamp01(_hp / Mathf.Max(1f, health));
        public Transform TargetTransform => transform;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitStatics()
        {
            Registry.Clear();
            Felled.Clear();
            _notchMaterial = null;
            Landed = null;
            WorldEvents.Regenerating -= ResetState;
            WorldEvents.Regenerating += ResetState;
        }

        private static void ResetState()
        {
            Registry.Clear();
            Felled.Clear();
        }

        private void Awake()
        {
            _hp = health;
            _model = transform.Find("Model");
            if (_model == null && transform.childCount > 0) _model = transform.GetChild(0);
            enabled = false;
        }

        private void OnDestroy()
        {
            if (_index >= 0 && Registry.TryGetValue(_index, out TreeResource t) && t == this) Registry.Remove(_index);
        }

        public void SetWorldIndex(int index)
        {
            _index = index;
            Registry[index] = this;
            if (Felled.Contains(index)) ApplyFelled(Vector3.zero, true);
        }

        /// <summary>Только сервер: уменьшает HP.</summary>
        public void ApplyDamage(float amount, GameObject source)
        {
            if (_hp <= 0f) return;
            _hp = Mathf.Max(0f, _hp - amount);
        }

        public static bool TryGet(int index, out TreeResource tree) => Registry.TryGetValue(index, out tree);

        // ---------- События с сервера ----------

        public static void PlayChopHit(int index, float healthFraction, Vector3 awayFromPlayer)
        {
            if (Registry.TryGetValue(index, out TreeResource tree) && tree != null) tree.OnChopHit(healthFraction, awayFromPlayer);
        }

        public static void Fell(int index, Vector3 fallDirection, bool instant)
        {
            Felled.Add(index);
            if (Registry.TryGetValue(index, out TreeResource tree) && tree != null) tree.ApplyFelled(fallDirection, instant);
        }

        public static void Fell(int index) => Fell(index, Vector3.zero, true);

        public static int[] SnapshotFelled()
        {
            int[] result = new int[Felled.Count];
            Felled.CopyTo(result);
            return result;
        }

        // ---------- Удар ----------

        private void OnChopHit(float healthFraction, Vector3 awayFromPlayer)
        {
            if (_falling || _stumpMade) return;
            _hp = Mathf.Min(_hp, health * healthFraction); // клиентская копия для HealthFraction/зарубки
            _damage01 = 1f - healthFraction;
            _shakeStart = Time.time;
            _shakeTwist = Random.value < 0.5f ? -1f : 1f;
            _shakeDir = awayFromPlayer.sqrMagnitude > 0.001f ? awayFromPlayer.normalized : Vector3.forward;
            enabled = true;

            Vector3 hitPos = transform.position + Vector3.up * 1.1f * transform.localScale.y - _shakeDir * 0.2f * transform.localScale.x;
            TreeFx.Chips(hitPos, -_shakeDir);
            TreeFx.Chunks(hitPos, -_shakeDir);

            // От удара с кроны осыпается хвоя: чем сильнее повреждено дерево, тем больше.
            Vector3 crown = transform.position + Vector3.up * 3.4f * transform.localScale.y;
            TreeFx.Needles(crown, 5 + Mathf.RoundToInt(10f * _damage01));
            UpdateNotch();
        }

        // ---------- Подпил ----------
        // Настоящий клиновидный надруб: вершины ствола у высоты NotchY вдавливаются внутрь по V-профилю
        // (верхний скос пологий, нижний уступ почти горизонтальный - как у лесоруба). Грани клина идут во втором
        // сабмеше со светлой свежей древесиной. Копия меша создаётся только у тех деревьев, по которым ударили.

        private const float NotchY = 1.0f;
        private Mesh _trunkMesh;
        private Vector3[] _trunkBase;
        private int[] _trunkTris;
        private Vector3 _notchDir;
        private bool _carveFailed;

        private static Material FreshWood()
        {
            if (_notchMaterial != null) return _notchMaterial;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _notchMaterial = new Material(sh);
            Color c = new Color(0.80f, 0.63f, 0.38f);
            if (_notchMaterial.HasProperty("_BaseColor")) _notchMaterial.SetColor("_BaseColor", c);
            if (_notchMaterial.HasProperty("_Color")) _notchMaterial.SetColor("_Color", c);
            if (_notchMaterial.HasProperty("_Smoothness")) _notchMaterial.SetFloat("_Smoothness", 0.12f);
            return _notchMaterial;
        }

        private bool InitCarve()
        {
            Transform t = _model != null ? _model.Find("TrunkBase") : null;
            if (t == null) return false;
            MeshFilter mf = t.GetComponent<MeshFilter>();
            MeshRenderer mr = t.GetComponent<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return false;

            _trunkMesh = Instantiate(mf.sharedMesh);
            _trunkMesh.name = "TrunkChopped";
            _trunkBase = _trunkMesh.vertices;
            _trunkTris = _trunkMesh.triangles;
            _trunkMesh.subMeshCount = 2;
            mf.sharedMesh = _trunkMesh;
            mr.sharedMaterials = new[] { mr.sharedMaterial, FreshWood() };
            return true;
        }

        private void UpdateNotch()
        {
            if (_model == null || _carveFailed) return;
            if (_trunkMesh == null && !InitCarve()) { _carveFailed = true; return; }

            if (_notchDir == Vector3.zero)
            {
                // Клин смотрит на игрока (против направления падения) и дальше только углубляется.
                Vector3 d = _model.InverseTransformDirection(-_shakeDir);
                d.y = 0f;
                _notchDir = d.sqrMagnitude < 0.001f ? Vector3.back : d.normalized;
            }

            float dmg = Mathf.Clamp01(_damage01);
            float depthMax = Mathf.Lerp(0.04f, 0.16f, dmg);   // глубина вдавливания, м
            float halfAng = Mathf.Lerp(0.42f, 0.62f, dmg);    // половина ширины, рад
            float hu = Mathf.Lerp(0.14f, 0.26f, dmg);         // высота верхнего скоса
            const float hl = 0.05f;                           // нижний уступ почти горизонтален
            float angle0 = Mathf.Atan2(_notchDir.z, _notchDir.x);

            Vector3[] v = new Vector3[_trunkBase.Length];
            bool[] carved = new bool[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = _trunkBase[i];
                v[i] = p;
                float dy = p.y - NotchY;
                if (dy > hu || dy < -hl) continue;
                float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                if (r < 0.01f) continue;
                float dAng = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(p.z, p.x) * Mathf.Rad2Deg, angle0 * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (dAng > halfAng) continue;

                float fy = dy >= 0f ? 1f - dy / hu : 1f + dy / hl;
                float fa = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - dAng / halfAng) * 3f));
                float inward = Mathf.Min(depthMax * fy * fa, r * 0.75f);
                if (inward <= 0.002f) continue;
                float k = (r - inward) / r;
                v[i] = new Vector3(p.x * k, p.y, p.z * k);
                carved[i] = true;
            }

            System.Collections.Generic.List<int> bark = new System.Collections.Generic.List<int>(_trunkTris.Length);
            System.Collections.Generic.List<int> fresh = new System.Collections.Generic.List<int>(256);
            for (int i = 0; i < _trunkTris.Length; i += 3)
            {
                int a = _trunkTris[i], b = _trunkTris[i + 1], c = _trunkTris[i + 2];
                System.Collections.Generic.List<int> target = (carved[a] || carved[b] || carved[c]) ? fresh : bark;
                target.Add(a); target.Add(b); target.Add(c);
            }

            _trunkMesh.vertices = v;
            _trunkMesh.SetTriangles(bark, 0);
            _trunkMesh.SetTriangles(fresh, 1);
            _trunkMesh.RecalculateNormals();

            // Сшиваем нормали вершин-дубликатов на шве (одна и та же исходная позиция), иначе виден стык.
            Vector3[] n = _trunkMesh.normals;
            var sum = new System.Collections.Generic.Dictionary<Vector3Int, Vector3>();
            for (int i = 0; i < n.Length; i++)
            {
                Vector3Int key = new Vector3Int(Mathf.RoundToInt(_trunkBase[i].x * 1000f), Mathf.RoundToInt(_trunkBase[i].y * 1000f), Mathf.RoundToInt(_trunkBase[i].z * 1000f));
                sum[key] = sum.TryGetValue(key, out Vector3 acc) ? acc + n[i] : n[i];
            }

            for (int i = 0; i < n.Length; i++)
            {
                Vector3Int key = new Vector3Int(Mathf.RoundToInt(_trunkBase[i].x * 1000f), Mathf.RoundToInt(_trunkBase[i].y * 1000f), Mathf.RoundToInt(_trunkBase[i].z * 1000f));
                n[i] = sum[key].normalized;
            }

            _trunkMesh.normals = n;
            _trunkMesh.RecalculateTangents();
            _trunkMesh.RecalculateBounds();
        }

        // ---------- Падение ----------

        private void ApplyFelled(Vector3 fallDirection, bool instant)
        {
            if (_stumpMade) return;
            _hp = 0f;

            if (_model != null)
            {
                foreach (Collider c in _model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            }

            MakeStump();

            if (instant || _model == null)
            {
                if (_model != null) _model.gameObject.SetActive(false);
                _stumpMade = true;
                enabled = false;
                return;
            }

            _fallDir = fallDirection.sqrMagnitude > 0.001f ? new Vector3(fallDirection.x, 0f, fallDirection.z).normalized : transform.forward;
            _baseRot = _model.rotation;
            _fallStart = Time.time;
            _falling = true;
            _impacted = false;
            _cracked = false;
            _stumpMade = true;
            enabled = true;

            TreeFx.Chips(transform.position + Vector3.up * 0.9f, -_fallDir);
        }

        private void MakeStump()
        {
            GameObject stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stump.name = "Stump";
            stump.transform.SetParent(transform, false);
            stump.transform.localPosition = new Vector3(0f, 0.27f, 0f);
            stump.transform.localScale = new Vector3(0.68f, 0.27f, 0.68f);
            Renderer trunk = _model != null ? _model.Find("TrunkBase")?.GetComponent<Renderer>() : null;
            if (trunk != null) stump.GetComponent<Renderer>().sharedMaterial = trunk.sharedMaterial;

            GameObject cut = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cut.name = "StumpCut";
            Destroy(cut.GetComponent<Collider>());
            cut.transform.SetParent(transform, false);
            cut.transform.localPosition = new Vector3(0f, 0.545f, 0f);
            cut.transform.localScale = new Vector3(0.64f, 0.006f, 0.64f);
            cut.GetComponent<Renderer>().sharedMaterial = FreshWood();
        }

        private void Update()
        {
            if (_falling) { TickFall(); return; }

            float e = Time.time - _shakeStart;
            if (_model == null || e > 0.6f)
            {
                if (_model != null) _model.localRotation = Quaternion.identity;
                enabled = false;
                return;
            }

            // Затухающее раскачивание в сторону падения и обратно.
            float amp = (1f - e / 0.6f) * (1.2f + 2.2f * _damage01);
            float ang = Mathf.Sin(e * 38f) * amp;
            float twist = Mathf.Sin(e * 29f + 1f) * amp * 0.35f * _shakeTwist;
            Vector3 axis = Vector3.Cross(Vector3.up, _shakeDir);
            _model.rotation = Quaternion.AngleAxis(twist, Vector3.up) * Quaternion.AngleAxis(ang, axis) * transform.rotation;
        }

        private void TickFall()
        {
            float e = Time.time - _fallStart;
            Vector3 axis = Vector3.Cross(Vector3.up, _fallDir);
            float angle;

            if (e < 0.7f)
            {
                // Треск: дерево чуть кренится и дрожит, у пня летят щепки, с кроны сыплется хвоя.
                float k = e / 0.7f;
                angle = 5f * k * k + Mathf.Sin(e * 45f) * 0.6f * (1f - k);
                if (!_cracked && e > 0.2f)
                {
                    _cracked = true;
                    TreeFx.Chunks(transform.position + Vector3.up * 0.8f, -_fallDir);
                    TreeFx.Needles(transform.position + Vector3.up * 3.4f * transform.localScale.y, 14);
                }
            }
            else if (e < 2.3f)
            {
                float u = (e - 0.7f) / 1.6f;
                angle = 5f + 83f * u * u;
            }
            else
            {
                if (!_impacted)
                {
                    _impacted = true;
                    Vector3 tip = transform.position + _fallDir * (3.5f * transform.localScale.x);
                    TreeFx.Dust(tip);
                    TreeFx.Needles(tip + Vector3.up * 0.8f, 34);
                    TreeFx.Dust(transform.position + _fallDir * 1.2f);
                    Landed?.Invoke(tip);
                }

                float b = e - 2.3f;
                angle = 88f - 4.5f * Mathf.Exp(-b * 5f) * Mathf.Abs(Mathf.Sin(b * 13f));
                if (e > 25f)
                {
                    _model.gameObject.SetActive(false);
                    _falling = false;
                    enabled = false;
                    return;
                }
            }

            // Падающий ствол слегка скручивается вокруг оси - выглядит тяжелее, чем жёсткий поворот.
            float twistFall = Mathf.Sin(Mathf.Clamp01(angle / 88f) * Mathf.PI) * 4f * (_fallDir.x >= 0f ? 1f : -1f);
            _model.rotation = Quaternion.AngleAxis(angle, axis) * Quaternion.AngleAxis(twistFall, Vector3.up) * _baseRot;
        }
    }
}
