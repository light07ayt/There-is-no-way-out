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
        private static readonly int AttackVariantHash = Animator.StringToHash("AttackVariant");

        private static GameObject[] _cachedPrefabs;
        private static string[] _cachedNames;
        private static bool[] _cachedFemale;

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
        private bool _hasAttackVariantParam;
        private bool _isFemale;
        private int _femaleAttackToggle;
        private readonly System.Collections.Generic.Dictionary<Transform, Vector3> _baseVmScales = new();

        public bool IsFemale => _isFemale;

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
            _cachedFemale = null;
            _warnedRoster = false;
        }

        public static bool IsFemaleModelName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string low = name.ToLowerInvariant();
            // Явные мужские имена всегда считаем мужскими
            if (low.Contains("male") && !low.Contains("female")) return false;
            if (low.Contains("boy") || low.Contains("man") && !low.Contains("woman")) return false;
            if (low.Contains("leo") || low.Contains("joey") || low.Contains("remy") || low.Contains("adam")
                || low.Contains("douglas") || low.Contains("david") || low.Contains("bryce") || low.Contains("ch09")
                || low.Contains("ch14") || low.Contains("ch17") || low.Contains("ch24") || low.Contains("ch31"))
                return false;

            return low.Contains("ch02") || low.Contains("ch03") || low.Contains("ch07") || low.Contains("ch12")
                   || low.Contains("ch15") || low.Contains("ch22") || low.Contains("ch46")
                   || low.Contains("female") || low.Contains("girl") || low.Contains("woman")
                   || low.Contains("lady") || low.Contains("maria") || low.Contains("claire")
                   || low.Contains("elena") || low.Contains("kate") || low.Contains("anna")
                   || low.Contains("megan") || low.Contains("amy") || low.Contains("sophie");
        }

        public static bool IsModelFemale(int index)
        {
            EnsureCatalogLoaded();
            if (_cachedFemale == null || _cachedFemale.Length == 0) return false;
            int idx = Mathf.Abs(index) % _cachedFemale.Length;
            return _cachedFemale[idx];
        }

        public static string[] GetLocalizedModelNames(bool english)
        {
            EnsureCatalogLoaded();
            if (_cachedPrefabs == null || _cachedPrefabs.Length == 0)
                return new[] { english ? "Male" : "Мужчина" };

            int maleTotal = 0, femaleTotal = 0;
            for (int i = 0; i < _cachedFemale.Length; i++)
            {
                if (_cachedFemale[i]) femaleTotal++; else maleTotal++;
            }

            string[] labels = new string[_cachedPrefabs.Length];
            int mIdx = 0, fIdx = 0;
            for (int i = 0; i < _cachedPrefabs.Length; i++)
            {
                if (_cachedFemale[i])
                {
                    fIdx++;
                    string baseLabel = english ? "Female" : "Девушка";
                    labels[i] = femaleTotal > 1 ? $"{baseLabel} {fIdx}" : baseLabel;
                }
                else
                {
                    mIdx++;
                    string baseLabel = english ? "Male" : "Мужчина";
                    labels[i] = maleTotal > 1 ? $"{baseLabel} {mIdx}" : baseLabel;
                }
            }
            return labels;
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
                _cachedFemale = Array.Empty<bool>();
                return;
            }

            // Сортируем так, чтобы мужской персонаж шёл первым (индекс 0), а женский следом — старые сохранения не собьются.
            Array.Sort(loaded, (a, b) =>
            {
                bool fa = IsFemaleModelName(a.name);
                bool fb = IsFemaleModelName(b.name);
                if (fa != fb) return fa ? 1 : -1;
                return string.CompareOrdinal(a.name, b.name);
            });
            _cachedPrefabs = loaded;
            _cachedNames = new string[loaded.Length];
            _cachedFemale = new bool[loaded.Length];
            for (int i = 0; i < loaded.Length; i++)
            {
                string n = loaded[i].name;
                if (n.StartsWith("Char_", StringComparison.OrdinalIgnoreCase)) n = n.Substring(5);
                _cachedNames[i] = n.Replace('_', ' ');
                _cachedFemale[i] = IsFemaleModelName(loaded[i].name);
            }
            Debug.Log("[Ogni] Загружены модели персонажей (" + loaded.Length + "): " + string.Join(", ", _cachedNames));
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

        public void TriggerAttack(bool unarmed = false)
        {
            if (_animator == null || _isDead || !_hasAttackParam) return;
            if (_hasAttackVariantParam)
            {
                // Для девушки в рукопашной чередуем удар кулаком и отфильтрованный прямой удар ногой (bencao/pontera).
                int variant = 0;
                if (_isFemale && unarmed)
                {
                    variant = _femaleAttackToggle & 1;
                    _femaleAttackToggle++;
                }
                _animator.SetInteger(AttackVariantHash, variant);
            }
            _animator.SetTrigger(AttackHash);
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

            SquadRoster.CharacterSlot rosterSlot = SquadRoster.GetSlot(data.ModelIndex);
            _isFemale = rosterSlot.Gender == CharacterGender.Female;
            if (_cachedPrefabs.Length > 0)
            {
                int idx = ResolvePrefabIndexForRosterSlot(data.ModelIndex);
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
            ApplyRosterOutfitTint(rosterSlot.OutfitTint);
            TintViewModelsFromModel();
        }

        private static int ResolvePrefabIndexForRosterSlot(int slotIndex)
        {
            SquadRoster.CharacterSlot slot = SquadRoster.GetSlot(slotIndex);
            bool wantFemale = slot.Gender == CharacterGender.Female;
            int ordinalInGender = (slot.SlotIndex == 1 || slot.SlotIndex == 3) ? 1 : 0;

            int firstMatch = -1;
            int foundCount = 0;
            WarnIfRosterModelsMissing();
            for (int i = 0; i < _cachedPrefabs.Length; i++)
            {
                bool f = _cachedFemale != null && i < _cachedFemale.Length && _cachedFemale[i];
                if (f != wantFemale) continue;
                if (firstMatch < 0) firstMatch = i;
                if (foundCount == ordinalInGender) return i;
                foundCount++;
            }
            return firstMatch >= 0 ? firstMatch : (slot.SlotIndex % _cachedPrefabs.Length);
        }

        private static bool _warnedRoster;

        private static void WarnIfRosterModelsMissing()
        {
            if (_warnedRoster || _cachedFemale == null) return;
            _warnedRoster = true;
            int f = 0, m = 0;
            for (int i = 0; i < _cachedFemale.Length; i++) { if (_cachedFemale[i]) f++; else m++; }
            if (f < 2 || m < 2)
            {
                Debug.LogWarning("[Ogni] Для состава из 4 друзей нужны 2 женские и 2 мужские модели в Resources/" + CharactersResourcePath
                                 + ", а определено: женских " + f + ", мужских " + m + ". Названия: " + string.Join(", ", _cachedNames)
                                 + ". Проверьте имена префабов (см. IsFemaleModelName).");
            }
        }

        private void ApplyRosterOutfitTint(Color tint)
        {
            if (_visualRoot == null || tint == Color.white) return;
            var mpb = new MaterialPropertyBlock();
            foreach (Renderer r in _visualRenderers)
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string n = mats[i].name.ToLowerInvariant();
                    if (n.Contains("skin") || n.Contains("body") || n.Contains("hair") || n.Contains("lash") || n.Contains("eye"))
                        continue;
                    r.GetPropertyBlock(mpb, i);
                    mpb.SetColor("_BaseColor", tint);
                    mpb.SetColor("_Color", tint);
                    r.SetPropertyBlock(mpb, i);
                }
            }
        }

        private void ResetParamFlags()
        {
            _hasSpeedParam = false;
            _hasGroundedParam = false;
            _hasDeadParam = false;
            _hasJumpParam = false;
            _hasAttackParam = false;
            _hasAttackVariantParam = false;
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
                else if (p.nameHash == AttackVariantHash) _hasAttackVariantParam = true;
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

        private static bool IsNearWhite(Color c)
        {
            return c.r > 0.95f && c.g > 0.95f && c.b > 0.95f;
        }

        private void TintViewModelsFromModel()
        {
            if (_visualRoot == null) return;

            // Калиброванные естественные цвета кожи и рукавов под выбранного персонажа.
            // У Mixamo-моделей с текстурой _BaseColor обычно чисто-белый, поэтому игнорируем белый цвет.
            Color skin = _isFemale ? new Color(0.81f, 0.62f, 0.49f) : new Color(0.84f, 0.66f, 0.54f);
            Color sleeve = _isFemale ? new Color(0.16f, 0.17f, 0.19f) : new Color(0.24f, 0.27f, 0.22f);

            foreach (Renderer r in _visualRenderers)
            {
                if (r == null) continue;
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    string n = m.name.ToLowerInvariant();
                    Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                        : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
                    if (IsNearWhite(c)) continue;
                    if (n.Contains("skin") || n.Contains("body")) skin = c;
                    else if (n.Contains("top") || n.Contains("hoodie") || n.Contains("jacket") || n.Contains("shirt") || n.Contains("cloth")) sleeve = c;
                }
            }

            // Для девушки делаем кулаки и запястья от первого лица более изящными и тонкими.
            Vector3 handScaleMult = _isFemale ? new Vector3(0.84f, 0.85f, 0.92f) : Vector3.one;
            Vector3 forearmScaleMult = _isFemale ? new Vector3(0.82f, 0.82f, 1.0f) : Vector3.one;

            var mpb = new MaterialPropertyBlock();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(_visualRoot.transform)) continue;
                string n = r.name;

                if (n.StartsWith("VM_Sleeve") || n.StartsWith("VM_Skin_Wrist"))
                {
                    Transform tr = r.transform;
                    if (!_baseVmScales.TryGetValue(tr, out Vector3 baseScale))
                    {
                        baseScale = tr.localScale;
                        _baseVmScales[tr] = baseScale;
                    }
                    tr.localScale = Vector3.Scale(baseScale, forearmScaleMult);
                }

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

            foreach (Transform tr in GetComponentsInChildren<Transform>(true))
            {
                if (tr.IsChildOf(_visualRoot.transform)) continue;
                if (!tr.name.StartsWith("VM_Hand_")) continue;
                if (!_baseVmScales.TryGetValue(tr, out Vector3 baseScale))
                {
                    baseScale = tr.localScale;
                    _baseVmScales[tr] = baseScale;
                }
                tr.localScale = Vector3.Scale(baseScale, handScaleMult);
            }
        }
    }
}
