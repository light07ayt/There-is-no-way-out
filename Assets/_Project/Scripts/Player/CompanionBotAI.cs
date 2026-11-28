using System.Collections.Generic;
using Ogni.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ogni.Player
{
    public enum CompanionOrderMode
    {
        /// <summary>Свободный режим: напарники живут своей жизнью, гуляют у дома/дороги и не мешают игроку.</summary>
        Autonomous = 0,
        /// <summary>Сопровождение: держат широкую дистанцию (6–9 м) сбоку/сзади и не лезут в экран.</summary>
        Follow = 1,
        /// <summary>Соло-этап сюжета: напарники остаются у дома, игрок идёт один.</summary>
        HoldAtCabin = 2
    }

    /// <summary>
    /// Переработанный ИИ-напарник для одиночного режима:
    /// - По умолчанию существует автономно (гуляет у дома, осматривается, не липнет к игроку и не мешает обзору).
    /// - Клавиша [G] переключает режим отряда: Свободно / Следовать на дистанции / Остаться у дома (соло-этап).
    /// - Без «ОПГ-кучи»: на одного монстра выходит максимум 1 напарник, остальные держат дистанцию и не закрывают камеру.
    /// - Поддерживает посадку и поездку внутри автомобиля на пассажирских сиденьях.
    /// - Если игрок нокаутирован (IsDowned) и рядом, один из напарников подбегает и поднимает его.
    /// </summary>
    public class CompanionBotAI : MonoBehaviour
    {
        private static CompanionOrderMode _globalOrder = CompanionOrderMode.Autonomous;
        private static readonly Dictionary<Transform, CompanionBotAI> ClaimedTargets = new();
        private static float _orderToastUntil;
        private static string _orderToastText = string.Empty;
        private static int _lastToggleFrame = -1;

        // Реестр активных напарников: заменяет FindObjectsByType<CompanionBotAI> в Update/OnGUI/посадке в машину.
        private static readonly List<CompanionBotAI> AllBots = new List<CompanionBotAI>(4);
        private static GUIStyle _toastStyle;

        public static IReadOnlyList<CompanionBotAI> All => AllBots;
        public static CompanionOrderMode GlobalOrder => _globalOrder;

        public static void SetGlobalOrder(CompanionOrderMode mode, bool showToast = true)
        {
            _globalOrder = mode;
            if (showToast)
            {
                bool en = GameSettings.Language == GameLanguage.English;
                _orderToastText = mode switch
                {
                    CompanionOrderMode.Autonomous => en ? "Squad [G]: Free roam (independent)" : "Отряд [G]: Свободно (сами по себе)",
                    CompanionOrderMode.Follow => en ? "Squad [G]: Follow at distance" : "Отряд [G]: Следовать на дистанции",
                    _ => en ? "Squad [G]: Stay at cabin (Solo)" : "Отряд [G]: Ждать у дома (соло)"
                };
                _orderToastUntil = Time.time + 3.2f;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _globalOrder = CompanionOrderMode.Autonomous;
            ClaimedTargets.Clear();
            _orderToastUntil = 0f;
            _orderToastText = string.Empty;
            _lastToggleFrame = -1;
            AllBots.Clear();
            _toastStyle = null;
        }

        private string _botName = "Напарник";
        private Vector3 _formationOffset = new Vector3(-5.5f, 0f, -5.0f);
        private Color _outfitTint = Color.white;
        private PlayerAppearanceRig _rig;
        private Light _flashlight;

        private Transform _leader;
        private PlayerHealth _leaderHealth;
        private IDamageable _combatTarget;
        private Transform _combatTargetTr;

        // Автономные прогулки (отдельная жизнь от игрока)
        private Vector3 _homeAnchor;
        private Vector3 _wanderTarget;
        private float _nextWanderPick;
        private float _wanderPauseUntil;
        private float _nextScanTime;
        private float _nextLeaderLookup;
        private float _nextAttackTime;
        private float _reviveProgress;
        private bool _tintApplied;

        // Состояние в машине
        private bool _inVehicle;
        private Transform _vehicleRoot;
        private Vector3 _vehicleSeatLocal;

        public string BotName => _botName;
        public bool IsInVehicle => _inVehicle;

        public void Initialize(string botName, CharacterAppearance appearance, Vector3 formationOffset, Color outfitTint)
        {
            _botName = botName;
            // Увеличиваем дистанцию строя, чтобы напарники никогда не дышали в спину игроку
            _formationOffset = new Vector3(
                Mathf.Sign(formationOffset.x == 0f ? 1f : formationOffset.x) * Mathf.Max(4.2f, Mathf.Abs(formationOffset.x) * 2.2f),
                0f,
                Mathf.Min(-4.5f, formationOffset.z * 2.2f));
            _outfitTint = outfitTint;
            _homeAnchor = transform.position;
            _wanderTarget = _homeAnchor;

            _rig = GetComponent<PlayerAppearanceRig>();
            if (_rig == null) _rig = gameObject.AddComponent<PlayerAppearanceRig>();
            _rig.SetFirstPersonLocalMode(false);
            _rig.Apply(appearance);

            GameObject fl = new GameObject("CompanionFlashlight");
            fl.transform.SetParent(transform, false);
            fl.transform.localPosition = new Vector3(0.15f, 1.42f, 0.28f);
            fl.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            _flashlight = fl.AddComponent<Light>();
            _flashlight.type = LightType.Spot;
            _flashlight.color = new Color(1f, 0.94f, 0.82f);
            _flashlight.intensity = 10f;
            _flashlight.range = 22f;
            _flashlight.spotAngle = 48f;
            _flashlight.shadows = LightShadows.None;

            ApplyOutfitTint();
            SnapToGround(instant: true);
            PickNewWanderPoint();
        }

        private void OnEnable()
        {
            if (!AllBots.Contains(this)) AllBots.Add(this);
        }

        private void OnDisable()
        {
            AllBots.Remove(this);
        }

        private void OnDestroy()
        {
            AllBots.Remove(this);
            ReleaseCombatClaim();
        }

        /// <summary>Посадить напарника на конкретное пассажирское место внутри машины.</summary>
        public void EnterVehicleSeat(Transform carRoot, Vector3 seatLocalPos)
        {
            if (carRoot == null) return;
            ReleaseCombatClaim();
            _inVehicle = true;
            _vehicleRoot = carRoot;
            _vehicleSeatLocal = seatLocalPos;

            transform.SetParent(carRoot, false);
            // Опускаем чуть ниже, чтобы персонаж сидел на подушке сиденья и не упирался головой в крышу
            transform.localPosition = seatLocalPos + new Vector3(0f, -0.64f, 0f);
            transform.localRotation = Quaternion.identity;
            if (_flashlight != null) _flashlight.enabled = false;
        }

        /// <summary>Высадить напарника рядом с машиной.</summary>
        public void ExitVehicleSeat(Vector3 exitWorldPos)
        {
            _inVehicle = false;
            _vehicleRoot = null;
            transform.SetParent(null, true);
            transform.position = exitWorldPos;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            SnapToGround(instant: true);
            _homeAnchor = transform.position;
            PickNewWanderPoint();
            if (_flashlight != null) _flashlight.enabled = true;
        }

        private void ApplyOutfitTint()
        {
            if (_tintApplied) return;
            _tintApplied = true;

            var mpb = new MaterialPropertyBlock();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
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
                    mpb.SetColor("_BaseColor", _outfitTint);
                    mpb.SetColor("_Color", _outfitTint);
                    r.SetPropertyBlock(mpb, i);
                }
            }
        }

        private void Update()
        {
            HandleOrderHotkey();

            if (_inVehicle)
            {
                // Пока едем в машине — сидим ровно на своём кресле
                if (_vehicleRoot != null)
                {
                    transform.localPosition = _vehicleSeatLocal + new Vector3(0f, -0.52f, 0f);
                    transform.localRotation = Quaternion.identity;
                }
                return;
            }

            if (_leader == null)
            {
                // Раньше поиск игрока (FindObjectsByType) шёл КАЖДЫЙ кадр у каждого бота, пока игрок не заспавнился.
                if (Time.time < _nextLeaderLookup) return;
                _nextLeaderLookup = Time.time + 0.5f;
                ResolveLeader();
                if (_leader == null) return;
            }

            // 1. Спасение игрока, если он упал (IsDowned) и мы не на соло-этапе слишком далеко
            if (_leaderHealth != null && _leaderHealth.IsDowned && _globalOrder != CompanionOrderMode.HoldAtCabin)
            {
                if (HorizontalDistance(transform.position, _leader.position) < 42f)
                {
                    HandleReviveLeader();
                    return;
                }
            }
            _reviveProgress = 0f;

            // 2. Поиск угрозы (с защитой от «ОПГ-кучи»: 1 зомби = максимум 1 напарник)
            if (Time.time >= _nextScanTime)
            {
                _nextScanTime = Time.time + 0.55f;
                ScanForThreat();
            }

            if (_combatTarget != null && (!_combatTarget.IsAlive || _combatTargetTr == null))
            {
                ReleaseCombatClaim();
            }

            if (_combatTarget != null && _combatTargetTr != null)
            {
                float distToSelf = HorizontalDistance(transform.position, _combatTargetTr.position);
                if (distToSelf > 16f)
                {
                    ReleaseCombatClaim();
                }
                else
                {
                    HandleCombat();
                    return;
                }
            }

            // 3. Поведение в зависимости от режима отряда
            switch (_globalOrder)
            {
                case CompanionOrderMode.Follow:
                    HandleFollowFormation();
                    break;
                case CompanionOrderMode.HoldAtCabin:
                    HandleAutonomousRoam(anchorRadius: 11f, stayNearHome: true);
                    break;
                default:
                    HandleAutonomousRoam(anchorRadius: 22f, stayNearHome: false);
                    break;
            }
        }

        private static void HandleOrderHotkey()
        {
            if (_lastToggleFrame == Time.frameCount) return;
            if (PlayerCraftMenu.IsAnyOpen) return;
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.gKey.wasPressedThisFrame)
            {
                _lastToggleFrame = Time.frameCount;
                CompanionOrderMode next = (CompanionOrderMode)(((int)_globalOrder + 1) % 3);
                SetGlobalOrder(next, showToast: true);
            }
        }

        private void HandleReviveLeader()
        {
            Vector3 targetPos = _leader.position + (_formationOffset.normalized * 1.4f);
            float dist = HorizontalDistance(transform.position, targetPos);
            if (dist > 1.5f)
            {
                MoveToward(targetPos, speed: 4.8f);
                _reviveProgress = 0f;
            }
            else
            {
                FaceTarget(_leader.position);
                _reviveProgress += Time.deltaTime;
                if (_reviveProgress >= 3.5f)
                {
                    _reviveProgress = 0f;
                    _leaderHealth.ReviveByCompanionServer();
                }
            }
        }

        private void HandleCombat()
        {
            Vector3 enemyPos = _combatTargetTr.position;
            // Не подходим с той же стороны, где стоит игрок — заходим чуть с фланга, чтобы не перекрывать экран!
            Vector3 flankDir = (transform.position - enemyPos);
            flankDir.y = 0f;
            if (flankDir.sqrMagnitude < 0.01f) flankDir = transform.right;
            flankDir.Normalize();
            Vector3 attackSpot = enemyPos + flankDir * 1.75f;

            float dist = HorizontalDistance(transform.position, enemyPos);
            if (dist > 2.05f)
            {
                MoveToward(attackSpot, speed: 4.4f);
            }
            else
            {
                FaceTarget(enemyPos);
                if (Time.time >= _nextAttackTime)
                {
                    _nextAttackTime = Time.time + 1.45f;
                    if (_rig != null) _rig.TriggerAttack(unarmed: true);
                    _combatTarget.ApplyDamage(12f, gameObject);
                }
            }
        }

        private void HandleAutonomousRoam(float anchorRadius, bool stayNearHome)
        {
            // Если игрок стоит прямо на пути — вежливо обходим его, не мешая
            Vector3 personalSpace = ComputeAvoidanceVector();
            if (personalSpace.sqrMagnitude > 0.04f)
            {
                MoveToward(transform.position + personalSpace * 2.5f, speed: 2.2f);
                return;
            }

            if (Time.time < _wanderPauseUntil)
            {
                return;
            }

            float distToTarget = HorizontalDistance(transform.position, _wanderTarget);
            if (distToTarget < 1.1f || Time.time >= _nextWanderPick)
            {
                _wanderPauseUntil = Time.time + Random.Range(2.5f, 6.5f);
                PickNewWanderPoint(anchorRadius, stayNearHome);
                return;
            }

            MoveToward(_wanderTarget, speed: 1.85f);
        }

        private void PickNewWanderPoint(float radius = 18f, bool stayNearHome = false)
        {
            Vector3 center = stayNearHome ? new Vector3(0f, 0f, 5f) : _homeAnchor;
            // В автономном режиме точка интереса остаётся вокруг дома/двора, не преследуя игрока по пятам
            for (int i = 0; i < 6; i++)
            {
                Vector2 rnd = Random.insideUnitCircle * radius;
                Vector3 candidate = center + new Vector3(rnd.x, 0f, rnd.y);
                // Не заходим прямо внутрь фундамента дома (примерно |x|<4, |z|<4)
                if (Mathf.Abs(candidate.x) < 4.5f && Mathf.Abs(candidate.z) < 4.5f) continue;
                _wanderTarget = candidate;
                break;
            }
            _nextWanderPick = Time.time + Random.Range(8f, 16f);
        }

        private void HandleFollowFormation()
        {
            Vector3 desired = _leader.TransformPoint(_formationOffset);
            float dist = HorizontalDistance(transform.position, desired);

            if (dist > 60f)
            {
                Vector3 warp = _leader.position - _leader.forward * 10f + _leader.right * _formationOffset.x;
                transform.position = warp;
                SnapToGround(instant: true);
                return;
            }

            Vector3 avoid = ComputeAvoidanceVector();
            if (dist > 2.2f || avoid.sqrMagnitude > 0.04f)
            {
                float speed = dist > 12f ? 4.8f : (dist > 5f ? 3.3f : 2.1f);
                MoveToward(desired + avoid * 2.4f, speed);
            }
            else
            {
                Quaternion targetRot = Quaternion.Euler(0f, _leader.eulerAngles.y, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 2.0f);
            }
        }

        /// <summary>
        /// Вычисляет вектор уклонения, чтобы напарники не толпились в кучу между собой
        /// и не лезли прямо перед камерой игрока.
        /// </summary>
        private Vector3 ComputeAvoidanceVector()
        {
            Vector3 push = Vector3.zero;

            // 1. Личное пространство игрока (особенно перед его взглядом!)
            if (_leader != null)
            {
                Vector3 fromLeader = transform.position - _leader.position;
                fromLeader.y = 0f;
                float d = fromLeader.magnitude;
                if (d < 3.0f && d > 0.01f)
                {
                    float dot = Vector3.Dot(fromLeader / d, _leader.forward);
                    // Если оказались прямо перед носом игрока — активно уходим в сторону
                    float weight = dot > 0.2f ? 1.8f : 0.9f;
                    push += (fromLeader / d) * ((3.0f - d) / 3.0f) * weight;
                }
            }

            // 2. Разделение между самими напарниками (минимум 3.2 м друг от друга)
            for (int i = 0; i < AllBots.Count; i++)
            {
                CompanionBotAI other = AllBots[i];
                if (other == null || other == this || other._inVehicle) continue;
                Vector3 diff = transform.position - other.transform.position;
                diff.y = 0f;
                float d = diff.magnitude;
                if (d < 3.2f && d > 0.01f)
                {
                    push += (diff / d) * ((3.2f - d) / 3.2f) * 1.4f;
                }
            }

            return push;
        }

        private void MoveToward(Vector3 target, float speed)
        {
            Vector3 delta = target - transform.position;
            delta.y = 0f;
            float mag = delta.magnitude;
            if (mag < 0.05f) return;

            Vector3 dir = delta / mag;
            float step = Mathf.Min(mag, speed * Time.deltaTime);
            transform.position += dir * step;
            FaceTarget(target);
            SnapToGround(instant: false);
        }

        private void FaceTarget(Vector3 target)
        {
            Vector3 look = target - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude < 0.001f) return;
            Quaternion targetRot = Quaternion.LookRotation(look.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 7f);
        }

        private void SnapToGround(bool instant)
        {
            Terrain t = Terrain.activeTerrain;
            if (t == null) return;
            Vector3 p = transform.position;
            float gy = t.SampleHeight(p) + t.transform.position.y;
            p.y = instant ? gy : Mathf.Lerp(p.y, gy, 0.35f);
            transform.position = p;
        }

        private void ResolveLeader()
        {
            foreach (PlayerHealth ph in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
            {
                if (ph != null && ph.IsOwner)
                {
                    _leader = ph.transform;
                    _leaderHealth = ph;
                    break;
                }
            }
        }

        private void ReleaseCombatClaim()
        {
            if (_combatTargetTr != null && ClaimedTargets.TryGetValue(_combatTargetTr, out CompanionBotAI owner) && owner == this)
            {
                ClaimedTargets.Remove(_combatTargetTr);
            }
            _combatTarget = null;
            _combatTargetTr = null;
        }

        private void ScanForThreat()
        {
            // На соло-этапе напарники обороняют только себя (радиус 7 м), не бегая за монстрами игрока
            float scanRadius = _globalOrder == CompanionOrderMode.Follow ? 11f : 7.5f;
            float bestSqr = scanRadius * scanRadius;
            IDamageable best = null;
            Transform bestTr = null;

            // Раньше: FindObjectsByType<MonoBehaviour> по всей сцене + сравнение имён типов (фриз каждые ~0.5 с у каждого бота).
            IReadOnlyList<MonoBehaviour> threats = ThreatRegistry.All;
            for (int t = 0; t < threats.Count; t++)
            {
                MonoBehaviour mb = threats[t];
                if (mb == null || !mb.isActiveAndEnabled) continue;
                if (!(mb is IDamageable dmg) || !dmg.IsAlive) continue;

                // Анти-ОПГ: если этим монстром уже занимается другой напарник — не лезем толпой!
                if (ClaimedTargets.TryGetValue(mb.transform, out CompanionBotAI claimant)
                    && claimant != null && claimant != this && !claimant._inVehicle)
                {
                    continue;
                }

                float sqr = (mb.transform.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = dmg;
                    bestTr = mb.transform;
                }
            }

            if (bestTr != _combatTargetTr)
            {
                ReleaseCombatClaim();
                if (bestTr != null)
                {
                    _combatTarget = best;
                    _combatTargetTr = bestTr;
                    ClaimedTargets[bestTr] = this;
                }
            }
        }

        private void OnGUI()
        {
            if (Time.time > _orderToastUntil || string.IsNullOrEmpty(_orderToastText)) return;
            // Рисуем тост только один раз (от первого напарника в реестре).
            // Раньше условие могло пропустить сразу двух ботов, и подсказка рисовалась дважды.
            if (AllBots.Count == 0 || AllBots[0] != this) return;

            float w = 360f;
            float h = 34f;
            Rect r = new Rect((Screen.width - w) * 0.5f, 18f, w, h);
            GUI.color = new Color(0.08f, 0.09f, 0.11f, 0.82f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (_toastStyle == null)
            {
                _toastStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    richText = true
                };
                _toastStyle.normal.textColor = Color.white;
            }
            GUI.Label(r, $"<color=#FFFFFF>{_orderToastText}</color>", _toastStyle);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
