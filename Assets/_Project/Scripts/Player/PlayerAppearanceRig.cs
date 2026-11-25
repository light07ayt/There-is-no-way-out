using System;
using Ogni.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ogni.Player
{
    /// <summary>
    /// Визуальная модель персонажа игрока из готовых Humanoid-префабов (Resources/Characters).
    /// Используется для других игроков в кооперативе, для 3D-превью в лобби и для отображения
    /// тела/ног локального игрока от первого лица (со скрытой головой и руками, чтобы не мешать ViewModel).
    /// Управляет параметрами Animator: Speed, IsGrounded, IsDead, Jump, Attack.
    /// </summary>
    public class PlayerAppearanceRig : MonoBehaviour
    {
        private const string CharactersResourcePath = "Characters";
        private const float LocalBodyOffsetZ = -0.22f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        private static GameObject[] _cachedPrefabs;
        private static string[] _cachedNames;

        private GameObject _visualRoot;
        private Renderer[] _visualRenderers = Array.Empty<Renderer>();
        private Animator _animator;
        private Transform _hips;
        private FirstPersonMotor _motor;
        private PlayerHealth _health;
        private bool _firstPersonLocal;
        private Vector3 _lastPos;
        private float _speed;
        private bool _isDead;
        private bool _wasLocalHiddenOnDeath;
        private bool _remoteGrounded = true;
        private bool _hasRemoteGrounded;
        private float _groundOffsetY;
        private bool _groundCalibrated;

        private bool _hasSpeedParam;
        private bool _hasGroundedParam;
        private bool _hasDeadParam;
        private bool _hasJumpParam;
        private bool _hasAttackParam;

        private void Awake()
        {
            _motor = GetComponent<FirstPersonMotor>();
            _health = GetComponent<PlayerHealth>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _cachedPrefabs = null;
            _cachedNames = null;
        }

        public static string[] GetAvailableModelNames()
        {
            EnsureCatalogLoaded();
            return _cachedNames.Length > 0 ? _cachedNames : new[] { "Стандартная модель" };
        }

        public static int GetModelCount()
        {
            EnsureCatalogLoaded();
            return Mathf.Max(1, _cachedPrefabs.Length);
        }

        private static void EnsureCatalogLoaded()
        {
            if (_cachedPrefabs != null) return;

            GameObject[] loaded = UnityEngine.Resources.LoadAll<GameObject>(CharactersResourcePath);
            if (loaded == null || loaded.Length == 0)
            {
                _cachedPrefabs = Array.Empty<GameObject>();
                _cachedNames = Array.Empty<string>();
                return;
            }

            // Отфильтровываем пустые анимационные FBX-префабы без 3D-меша (например bencao, pontera).
            var valid = new System.Collections.Generic.List<GameObject>(loaded.Length);
            for (int i = 0; i < loaded.Length; i++)
            {
                GameObject go = loaded[i];
                if (go == null) continue;
                bool hasMesh = false;
                foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (smr.sharedMesh != null) { hasMesh = true; break; }
                if (!hasMesh)
                {
                    foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh != null) { hasMesh = true; break; }
                }
                if (hasMesh) valid.Add(go);
            }

            valid.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _cachedPrefabs = valid.ToArray();
            _cachedNames = new string[_cachedPrefabs.Length];
            for (int i = 0; i < _cachedPrefabs.Length; i++)
            {
                string n = _cachedPrefabs[i].name;
                if (n.StartsWith("Char_", StringComparison.OrdinalIgnoreCase)) n = n.Substring(5);
                n = n.Replace("_nonPBR", "").Replace("nonPBR", "").Replace('_', ' ').Trim();
                _cachedNames[i] = string.IsNullOrEmpty(n) ? $"Выживший {i + 1}" : $"Выживший {i + 1} ({n})";
            }
        }

        public void SetFirstPersonLocalMode(bool isLocalOwner)
        {
            _firstPersonLocal = isLocalOwner;
            if (_visualRoot != null)
            {
                _visualRoot.SetActive(true);
                _visualRoot.transform.localPosition = new Vector3(0f, _groundOffsetY, isLocalOwner ? LocalBodyOffsetZ : 0f);
            }
            UpdateLocalDeathVisibility(force: true);
        }

        public void SetRemoteGrounded(bool grounded)
        {
            _remoteGrounded = grounded;
            _hasRemoteGrounded = true;
        }

        public void TriggerJump()
        {
            if (_animator == null || _isDead || !_hasJumpParam) return;
            _animator.SetTrigger(JumpHash);
        }

        public void TriggerAttack()
        {
            if (_animator == null || _isDead || !_hasAttackParam) return;
            _animator.SetTrigger(AttackHash);
        }

        public void TriggerAttack(bool isAlternate)
        {
            TriggerAttack();
        }

        public void TriggerAttack(int slotIndex)
        {
            TriggerAttack();
        }

        public void TriggerAttack(PlayerEquipment.ToolSlot slot)
        {
            TriggerAttack();
        }

        public void Apply(CharacterAppearance data)
        {
            if (_visualRoot != null) Destroy(_visualRoot);
            _visualRenderers = Array.Empty<Renderer>();
            _animator = null;
            _hips = null;
            _groundOffsetY = 0f;
            _groundCalibrated = false;
            _wasLocalHiddenOnDeath = false;
            ResetParamFlags();

            EnsureCatalogLoaded();

            if (_cachedPrefabs.Length > 0)
            {
                int idx = data.ModelIndex % _cachedPrefabs.Length;
                _visualRoot = Instantiate(_cachedPrefabs[idx], transform, false);
                _visualRoot.name = "BodyVisualRoot";
                _visualRoot.transform.localPosition = new Vector3(0f, 0f, _firstPersonLocal ? LocalBodyOffsetZ : 0f);
                _visualRoot.transform.localRotation = Quaternion.identity;

                // Отключаем коллайдеры на визуальной модели, чтобы не мешали CharacterController игрока.
                foreach (Collider col in _visualRoot.GetComponentsInChildren<Collider>(true))
                {
                    Destroy(col);
                }

                _animator = _visualRoot.GetComponentInChildren<Animator>(true);
                if (_animator != null)
                {
                    _animator.applyRootMotion = false;
                    if (_animator.isHuman) _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
                    CacheAnimatorParameters(_animator);
                    if (_hasGroundedParam) _animator.SetBool(IsGroundedHash, true);
                    if (_hasDeadParam) _animator.SetBool(IsDeadHash, _health != null && _health.IsDowned);
                    _animator.Update(0f);
                    PinHipsHorizontal();
                    CalibrateGroundOffset();
                }
            }
            else
            {
                // Заглушка, если в Unity ещё не запущен Ogni -> Setup Character Models.
                _visualRoot = new GameObject("BodyVisualRoot");
                _visualRoot.transform.SetParent(transform, false);
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "FallbackBody";
                Destroy(capsule.GetComponent<Collider>());
                capsule.transform.SetParent(_visualRoot.transform, false);
                capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                capsule.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            }

            _visualRenderers = _visualRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in _visualRenderers)
            {
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            UpdateLocalDeathVisibility(force: true);

            _lastPos = transform.position;
            TintViewModelsFromModel(data.ModelIndex);
        }

        private void ResetParamFlags()
        {
            _hasSpeedParam = false;
            _hasGroundedParam = false;
            _hasDeadParam = false;
            _hasJumpParam = false;
            _hasAttackParam = false;
        }

        private void CacheAnimatorParameters(Animator anim)
        {
            ResetParamFlags();
            if (anim == null || anim.runtimeAnimatorController == null) return;

            foreach (AnimatorControllerParameter p in anim.parameters)
            {
                if (p.nameHash == SpeedHash) _hasSpeedParam = true;
                else if (p.nameHash == IsGroundedHash) _hasGroundedParam = true;
                else if (p.nameHash == IsDeadHash) _hasDeadParam = true;
                else if (p.nameHash == JumpHash) _hasJumpParam = true;
                else if (p.nameHash == AttackHash) _hasAttackParam = true;
            }
        }

        private void Update()
        {
            if (_visualRoot == null || !_visualRoot.activeSelf || _animator == null) return;

            _isDead = _health != null && _health.IsDowned;
            if (_hasDeadParam) _animator.SetBool(IsDeadHash, _isDead);
            UpdateLocalDeathVisibility(force: false);

            if (_isDead)
            {
                _speed = 0f;
                _lastPos = transform.position;
                if (_hasSpeedParam) _animator.SetFloat(SpeedHash, 0f);
                if (_hasGroundedParam) _animator.SetBool(IsGroundedHash, true);
                return;
            }

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            Vector3 p = transform.position;
            Vector3 d = p - _lastPos;
            d.y = 0f;
            _lastPos = p;

            float v = Mathf.Min(8f, d.magnitude / dt);
            _speed = Mathf.Lerp(_speed, v, 1f - Mathf.Exp(-9f * dt));
            if (_hasSpeedParam) _animator.SetFloat(SpeedHash, _speed);

            if (_hasGroundedParam)
            {
                bool grounded = (_motor != null && _motor.enabled)
                    ? _motor.IsGrounded
                    : (_hasRemoteGrounded ? _remoteGrounded : true);
                _animator.SetBool(IsGroundedHash, grounded);
            }
        }

        private void LateUpdate()
        {
            if (_animator == null) return;

            // Фиксируем горизонтальный сдвиг Hips, чтобы Mixamo-клипы (Jump/Run/Attack)
            // не улетали вперёд перед камерой и не сдвигали визуальную модель с коллайдера.
            if (!_isDead)
            {
                PinHipsHorizontal();
            }

            if (!_groundCalibrated && !_isDead)
            {
                CalibrateGroundOffset();
                _groundCalibrated = true;
            }

            if (!_firstPersonLocal) return;

            if (_isDead)
            {
                // При смерти камера переходит в 3-е лицо — показываем всё тело целиком (голову и руки).
                RestoreBone(HumanBodyBones.Neck);
                RestoreBone(HumanBodyBones.Head);
                RestoreBone(HumanBodyBones.LeftShoulder);
                RestoreBone(HumanBodyBones.RightShoulder);
                RestoreBone(HumanBodyBones.LeftUpperArm);
                RestoreBone(HumanBodyBones.RightUpperArm);
                if (_visualRoot != null)
                {
                    _visualRoot.transform.localPosition = new Vector3(0f, _groundOffsetY, 0f);
                }
                return;
            }

            if (_visualRoot != null)
            {
                _visualRoot.transform.localPosition = new Vector3(0f, _groundOffsetY, LocalBodyOffsetZ);
            }

            // В виде от первого лица скрываем голову, шею и руки основной модели,
            // оставляя корпус и ноги видимыми, чтобы не перекрывать камеру и ViewModel оружия.
            HideBone(HumanBodyBones.Neck);
            HideBone(HumanBodyBones.Head);
            HideBone(HumanBodyBones.LeftShoulder);
            HideBone(HumanBodyBones.RightShoulder);
            HideBone(HumanBodyBones.LeftUpperArm);
            HideBone(HumanBodyBones.RightUpperArm);
        }

        private void UpdateLocalDeathVisibility(bool force)
        {
            bool hideLocal = false;
            if (!force && hideLocal == _wasLocalHiddenOnDeath) return;
            _wasLocalHiddenOnDeath = hideLocal;

            // При нокауте/смерти локальный игрок не должен видеть своё падающее тело «от третьего лица»
            // (у других игроков в кооперативе анимация падения продолжает проигрываться полностью).
            ShadowCastingMode mode = hideLocal ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (Renderer r in _visualRenderers)
            {
                if (r != null) r.shadowCastingMode = mode;
            }
        }

        private void PinHipsHorizontal()
        {
            if (_visualRoot == null || _hips == null) return;
            Vector3 localHips = _visualRoot.transform.InverseTransformPoint(_hips.position);
            localHips.x = 0f;
            localHips.z = 0f;
            _hips.position = _visualRoot.transform.TransformPoint(localHips);
        }

        private void CalibrateGroundOffset()
        {
            if (_visualRoot == null || _animator == null) return;

            float lowestSoleY = float.MaxValue;
            if (_animator.isHuman)
            {
                Transform lToes = _animator.GetBoneTransform(HumanBodyBones.LeftToes);
                Transform rToes = _animator.GetBoneTransform(HumanBodyBones.RightToes);
                if (lToes != null) lowestSoleY = Mathf.Min(lowestSoleY, lToes.position.y - 0.03f);
                if (rToes != null) lowestSoleY = Mathf.Min(lowestSoleY, rToes.position.y - 0.03f);

                if (lowestSoleY == float.MaxValue)
                {
                    Transform lFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    Transform rFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    if (lFoot != null) lowestSoleY = Mathf.Min(lowestSoleY, lFoot.position.y - 0.09f);
                    if (rFoot != null) lowestSoleY = Mathf.Min(lowestSoleY, rFoot.position.y - 0.09f);
                }
            }

            if (lowestSoleY == float.MaxValue)
            {
                foreach (Renderer r in _visualRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.bounds.size.sqrMagnitude > 0.0001f)
                        lowestSoleY = Mathf.Min(lowestSoleY, r.bounds.min.y);
                }
            }

            if (lowestSoleY == float.MaxValue) return;

            float deltaY = transform.position.y - lowestSoleY;
            if (Mathf.Abs(deltaY) > 0.005f && Mathf.Abs(deltaY) < 2.0f)
            {
                _groundOffsetY = Mathf.Clamp(_groundOffsetY + deltaY, -1.5f, 1.5f);
                _visualRoot.transform.localPosition = new Vector3(0f, _groundOffsetY, _firstPersonLocal ? LocalBodyOffsetZ : 0f);
            }
        }

        private void HideBone(HumanBodyBones bone)
        {
            Transform t = _animator.GetBoneTransform(bone);
            if (t != null) t.localScale = Vector3.zero;
        }

        private void RestoreBone(HumanBodyBones bone)
        {
            Transform t = _animator.GetBoneTransform(bone);
            if (t != null) t.localScale = Vector3.one;
        }

        // Реалистичные тона кожи и рукавов для вьюмодели (руки от первого лица).
        private static readonly Color[] SkinTones =
        {
            new Color(0.80f, 0.60f, 0.48f), new Color(0.70f, 0.50f, 0.38f), new Color(0.86f, 0.68f, 0.56f),
            new Color(0.58f, 0.40f, 0.30f), new Color(0.76f, 0.56f, 0.44f), new Color(0.90f, 0.74f, 0.62f)
        };

        private static readonly Color[] SleeveTones =
        {
            new Color(0.17f, 0.20f, 0.15f), new Color(0.20f, 0.22f, 0.27f), new Color(0.26f, 0.19f, 0.15f),
            new Color(0.13f, 0.14f, 0.16f), new Color(0.30f, 0.28f, 0.22f), new Color(0.22f, 0.12f, 0.12f)
        };

        // Средний цвет текстуры (даже нечитаемой): рисуем её в крошечный RenderTexture и читаем пиксели.
        private static bool TryAverageColor(Texture tex, out Color avg)
        {
            avg = Color.white;
            if (tex == null) return false;
            try
            {
                RenderTexture rt = RenderTexture.GetTemporary(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                RenderTexture prev = RenderTexture.active;
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                Texture2D t2 = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                t2.ReadPixels(new Rect(0, 0, 8, 8), 0, 0);
                t2.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Color sum = Color.black;
                Color[] px = t2.GetPixels();
                foreach (Color p in px) sum += p;
                Destroy(t2);
                avg = sum / px.Length;
                avg.a = 1f;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool LooksLikeSkin(Color c)
        {
            Color.RGBToHSV(c, out float h, out float sat, out float val);
            return (h < 0.11f || h > 0.95f) && sat > 0.18f && sat < 0.62f && val > 0.38f && val < 0.92f && c.r > c.g && c.g >= c.b * 0.9f;
        }

        private void TintViewModelsFromModel(int modelIndex)
        {
            if (_visualRoot == null) return;

            // Раньше цвет брался из _BaseColor материала модели - он почти всегда белый (цвет лежит в текстуре),
            // поэтому руки от первого лица получались белыми. Теперь берём средний цвет текстуры, а если он не похож на
            // кожу/ткань - подбираем естественный тон по номеру модели.
            int pick = Mathf.Abs(modelIndex);
            Color skin = SkinTones[pick % SkinTones.Length];
            Color sleeve = SleeveTones[pick % SleeveTones.Length];

            foreach (Renderer r in _visualRenderers)
            {
                if (r == null) continue;
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    string n = m.name.ToLowerInvariant();
                    bool isSkin = n.Contains("skin") || n.Contains("body") || n.Contains("face") || n.Contains("head");
                    bool isCloth = n.Contains("top") || n.Contains("hoodie") || n.Contains("jacket") || n.Contains("shirt") || n.Contains("cloth");
                    if (!isSkin && !isCloth) continue;

                    Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : (m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null);
                    if (!TryAverageColor(tex, out Color avg)) continue;
                    Color tint = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                    avg = new Color(avg.r * tint.r, avg.g * tint.g, avg.b * tint.b, 1f);

                    if (isSkin && LooksLikeSkin(avg)) skin = Color.Lerp(skin, avg, 0.85f);
                    else if (isCloth)
                    {
                        Color.RGBToHSV(avg, out _, out _, out float v);
                        if (v > 0.06f && v < 0.62f) sleeve = avg;
                    }
                }
            }

            var mpb = new MaterialPropertyBlock();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(_visualRoot.transform)) continue;
                string n = r.name;
                Color c;
                if (n.StartsWith("VM_Skin")) c = skin;
                else if (n.StartsWith("VM_Sleeve")) c = sleeve;
                else continue;

                c.a = 1f;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_Color", c);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
