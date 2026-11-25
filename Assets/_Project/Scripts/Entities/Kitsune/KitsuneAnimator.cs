using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Ogni.Entities.Kitsune
{
    /// <summary>
    /// Анимация Кицунэ с плавным превращением «девочка → чудовище» (параметр form 0..1): глаза заливает чернота,
    /// улыбка растягивается до ушей, показываются зубы и длинный язык, вырастают когти, руки удлиняются, поза горбится.
    /// В облике девочки она смотрит на ближайшего игрока и зовёт, протягивая руки. Вешается на VisualRig.
    /// Работает и на иллюзиях-клонах (без KitsuneAI): тогда значения задаёт <see cref="SetManual"/>.
    /// </summary>
    public class KitsuneAnimator : MonoBehaviour
    {
        // Форма черепа в билдере (локальные координаты Head-пивота).
        private const float Rx = 0.110f, Ry = 0.125f, Rz = 0.115f, Cy = 0.12f;

        private KitsuneAI _ai;
        private bool _manual;
        private float _mForm, _mSpeed, _mAttack = -1f;

        private Transform _body, _head, _skirt, _tongue, _mouthHole;
        private Transform _shL, _shR, _elL, _elR, _handL, _handR;
        private readonly List<Transform> _smile = new(), _teeth = new(), _teethLow = new(), _hair = new(), _claws = new();
        private readonly List<Transform> _eyeBlack = new(), _eyeWhite = new();
        private readonly List<Light> _wisps = new();
        private readonly List<float> _wispBase = new();
        private Vector3 _eyeBlackScale = Vector3.one, _eyeWhiteScale = Vector3.one, _clawScale = Vector3.one, _toothScale = Vector3.one, _smileScale = Vector3.one * 0.006f;

        private float _form, _phase, _twitch, _twitchTarget, _nextTwitchAt;
        private Transform _localPlayer;
        private float _nextPlayerLookup;
        private Vector3 _lookSmoothed;

        public float Form => _form;

        public void SetManual(float form, float speed, float attack)
        {
            _manual = true; _mForm = form; _mSpeed = speed; _mAttack = attack;
        }

        private void Awake()
        {
            _ai = GetComponentInParent<KitsuneAI>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n == "Body") _body = t;
                else if (n == "Head") _head = t;
                else if (n == "Skirt") _skirt = t;
                else if (n == "Tongue") _tongue = t;
                else if (n == "MouthHole") _mouthHole = t;
                else if (n == "ShoulderL") _shL = t;
                else if (n == "ShoulderR") _shR = t;
                else if (n == "ElbowL") _elL = t;
                else if (n == "ElbowR") _elR = t;
                else if (n == "HandL") _handL = t;
                else if (n == "HandR") _handR = t;
                else if (n.StartsWith("Smile_")) _smile.Add(t);
                else if (n.StartsWith("ToothLow_")) _teethLow.Add(t);
                else if (n.StartsWith("Tooth_")) _teeth.Add(t);
                else if (n.StartsWith("Hair_")) _hair.Add(t);
                else if (n.StartsWith("Claw_")) _claws.Add(t);
                else if (n.StartsWith("EyeBlack_")) _eyeBlack.Add(t);
                else if (n.StartsWith("EyeWhite_")) _eyeWhite.Add(t);
            }

            foreach (Light l in GetComponentsInChildren<Light>(true)) { _wisps.Add(l); _wispBase.Add(l.intensity); }
            if (_eyeBlack.Count > 0) _eyeBlackScale = _eyeBlack[0].localScale;
            if (_eyeWhite.Count > 0) _eyeWhiteScale = _eyeWhite[0].localScale;
            if (_claws.Count > 0) _clawScale = _claws[0].localScale;
            if (_teeth.Count > 0) _toothScale = _teeth[0].localScale;
            if (_smile.Count > 0) _smileScale = _smile[0].localScale;
        }

        private static float FrontZ(float x, float y)
        {
            float t = 1f - (x * x) / (Rx * Rx) - ((y - Cy) * (y - Cy)) / (Ry * Ry);
            return Rz * Mathf.Sqrt(Mathf.Max(0.02f, t));
        }

        private static float Ease(float a, float b, float v) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - a) / (b - a)));

        private Transform LocalPlayer()
        {
            if (_localPlayer == null && Time.time >= _nextPlayerLookup)
            {
                _nextPlayerLookup = Time.time + 2f;
                NetworkManager nm = NetworkManager.Singleton;
                if (nm != null && nm.LocalClient != null && nm.LocalClient.PlayerObject != null) _localPlayer = nm.LocalClient.PlayerObject.transform;
            }

            return _localPlayer;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time;

            float targetForm, speed, attack;
            if (_manual) { targetForm = _mForm; speed = _mSpeed; attack = _mAttack; }
            else if (_ai != null)
            {
                KitsuneAI.Mode m = _ai.CurrentMode;
                targetForm = (m == KitsuneAI.Mode.Transforming || m == KitsuneAI.Mode.Hunt) ? 1f : 0f;
                speed = _ai.Speed;
                attack = _ai.AttackProgress;
            }
            else return;

            float rate = targetForm > _form ? 1f / 1.1f : 1f / 1.8f;
            _form = Mathf.MoveTowards(_form, targetForm, rate * dt);
            float f = Ease(0f, 1f, _form);
            float run = Mathf.Clamp01(speed / 3.5f);
            float walk = Mathf.Clamp01(speed / 1.2f);
            _phase += speed * dt * 1.7f;

            // ---- Тело
            if (_body != null)
            {
                float lean = f * 14f + run * 16f;
                float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.045f * Mathf.Max(run, walk * 0.5f);
                float sway = Mathf.Sin(t * 1.2f) * (1.5f + (1f - f) * 1.5f);
                _body.localRotation = Quaternion.Slerp(_body.localRotation, Quaternion.Euler(lean, 0f, sway), 1f - Mathf.Exp(-8f * dt));
                _body.localPosition = new Vector3(0f, bob, 0f);
            }

            if (_skirt != null) _skirt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_phase) * 5f * walk + Mathf.Sin(t * 1.5f) * 1.2f);

            // ---- Голова
            AnimateHead(f, t, dt, attack);

            // ---- Руки
            AnimateArms(f, t, dt, run, walk, attack);

            // ---- Лицо
            AnimateFace(f, t, attack);

            // ---- Волосы и огоньки-приманки
            for (int i = 0; i < _hair.Count; i++)
                _hair[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 2f + i) * 6f + run * 28f + f * 10f, 0f, Mathf.Sin(t * 1.5f + i * 1.7f) * 7f);
            for (int i = 0; i < _wisps.Count; i++)
                _wisps[i].intensity = Mathf.Lerp(_wispBase[i], 0f, f) * (0.85f + 0.15f * Mathf.Sin(t * 9f + i * 2f));
        }

        private void AnimateHead(float f, float t, float dt, float attack)
        {
            if (_head == null) return;

            // Девочка: смотрит на ближайшего игрока. Чудовище: смотрит прямо, голова кривится и дёргается.
            Vector3 look = Vector3.zero;
            Transform player = _manual ? null : LocalPlayer();
            if (player != null && f < 0.6f)
            {
                Vector3 toP = player.position + Vector3.up * 1.5f - _head.position;
                Vector3 loc = transform.InverseTransformDirection(toP);
                float yaw = Mathf.Clamp(Mathf.Atan2(loc.x, loc.z) * Mathf.Rad2Deg, -70f, 70f);
                float pitch = Mathf.Clamp(-Mathf.Atan2(loc.y, new Vector2(loc.x, loc.z).magnitude) * Mathf.Rad2Deg, -20f, 25f);
                look = new Vector3(pitch, yaw, 0f);
            }

            _lookSmoothed = Vector3.Lerp(_lookSmoothed, look, 1f - Mathf.Exp(-5f * dt));

            if (t >= _nextTwitchAt)
            {
                _nextTwitchAt = t + Random.Range(0.6f, 2.0f);
                _twitchTarget = f > 0.5f ? Random.Range(-14f, 14f) : Random.Range(-2f, 2f);
            }

            _twitch = Mathf.Lerp(_twitch, _twitchTarget, 1f - Mathf.Exp(-(f > 0.5f ? 28f : 4f) * dt));
            float tilt = Mathf.Sin(t * 0.8f) * 4f + f * 14f + _twitch;
            float strike = attack >= 0f && _ai != null ? MonsterBase.AttackCurve(attack, _ai.AttackImpact01, 0f, -16f, 22f) : 0f;
            _head.localRotation = Quaternion.Euler(_lookSmoothed.x + strike + f * 8f, _lookSmoothed.y, tilt);
        }

        private void AnimateArms(float f, float t, float dt, float run, float walk, float attack)
        {
            if (_shL == null || _shR == null) return;

            // Девочка тянет руки вперёд (просит помощи), чудовище - длинные висящие руки, на бегу машет.
            float baseX = Mathf.Lerp(-38f + Mathf.Sin(t * 1.5f) * 6f, 14f, f);
            float swing = Mathf.Sin(_phase) * Mathf.Lerp(12f, 55f, run) * Mathf.Max(walk, run);
            float spread = Mathf.Lerp(6f, 18f, f);
            float shLx = baseX + swing, shRx = baseX - swing;
            float elLx = Mathf.Lerp(-30f, -12f, f) - run * 25f, elRx = elLx;

            if (attack >= 0f)
            {
                float imp = _ai != null ? _ai.AttackImpact01 : 0.35f;
                shRx = MonsterBase.AttackCurve(attack, imp, shRx, -125f, 48f);
                shLx = MonsterBase.AttackCurve(Mathf.Max(0f, attack - 0.08f), imp, shLx, -118f, 44f);
                elRx = MonsterBase.AttackCurve(attack, imp, elRx, -70f, -6f);
                elLx = MonsterBase.AttackCurve(Mathf.Max(0f, attack - 0.08f), imp, elLx, -64f, -6f);
            }

            float k = 1f - Mathf.Exp(-16f * dt);
            _shL.localRotation = Quaternion.Slerp(_shL.localRotation, Quaternion.Euler(shLx, 0f, -spread), k);
            _shR.localRotation = Quaternion.Slerp(_shR.localRotation, Quaternion.Euler(shRx, 0f, spread), k);
            if (_elL != null) _elL.localRotation = Quaternion.Slerp(_elL.localRotation, Quaternion.Euler(elLx, 0f, 0f), k);
            if (_elR != null) _elR.localRotation = Quaternion.Slerp(_elR.localRotation, Quaternion.Euler(elRx, 0f, 0f), k);

            // Руки удлиняются при превращении.
            float stretch = Mathf.Lerp(1f, 1.45f, f);
            if (_elL != null) _elL.localScale = new Vector3(1f, stretch, 1f);
            if (_elR != null) _elR.localScale = new Vector3(1f, stretch, 1f);

            // Когти вырастают.
            float claw = Mathf.Lerp(0.05f, 1f, Ease(0.2f, 1f, f));
            for (int i = 0; i < _claws.Count; i++) _claws[i].localScale = new Vector3(_clawScale.x, _clawScale.y * claw, _clawScale.z);
        }

        private void AnimateFace(float f, float t, float attack)
        {
            // Глаза: чернота заливает глазницу, белок исчезает.
            float black = Mathf.Lerp(0.6f, 1.18f, Ease(0.1f, 0.9f, f));
            float white = Mathf.Lerp(1f, 0.3f, Ease(0.4f, 1f, f));
            for (int i = 0; i < _eyeBlack.Count; i++) _eyeBlack[i].localScale = _eyeBlackScale * black;
            for (int i = 0; i < _eyeWhite.Count; i++) _eyeWhite[i].localScale = _eyeWhiteScale * white;

            // Рот: улыбка растягивается, челюсть отвисает, в момент удара раскрывается.
            float open = attack >= 0f ? Mathf.Sin(Mathf.Clamp01(attack) * Mathf.PI) * 0.035f : 0f;
            float half = Mathf.Lerp(0.024f, 0.094f, Ease(0f, 1f, f));
            float depth = Mathf.Lerp(0.004f, 0.034f, f);
            float baseY = 0.055f - f * 0.012f - open * 0.4f;
            int n = _smile.Count;
            for (int i = 0; i < n; i++)
            {
                float s = n > 1 ? i / (float)(n - 1) * 2f - 1f : 0f;
                float x = s * half;
                float y = baseY - depth * (1f - s * s);
                _smile[i].localPosition = new Vector3(x, y, FrontZ(x, y) + 0.002f);
                _smile[i].localScale = _smileScale * Mathf.Lerp(1f, 1.5f, f) * (1f - 0.3f * Mathf.Abs(s));
            }

            float holeH = Mathf.Lerp(0.001f, 0.04f, Ease(0.2f, 1f, f)) + open;
            if (_mouthHole != null)
            {
                float hy = baseY - depth * 0.55f - holeH * 0.5f;
                _mouthHole.localPosition = new Vector3(0f, hy, FrontZ(0f, hy) - 0.004f);
                _mouthHole.localScale = new Vector3(half * 1.75f, Mathf.Max(0.0005f, holeH), 0.012f);
            }

            float teeth = Ease(0.35f, 0.9f, f);
            PlaceTeeth(_teeth, half, baseY, depth, 0f, teeth, 1f);
            PlaceTeeth(_teethLow, half * 0.9f, baseY, depth * 0.8f, -(holeH + 0.004f), teeth, -1f);

            // Язык: вываливается и извивается.
            if (_tongue != null)
            {
                float len = Mathf.Lerp(0.02f, 1f, Ease(0.55f, 1f, f));
                float ty = baseY - depth * 0.5f - holeH * 0.6f;
                _tongue.localPosition = new Vector3(0f, ty, FrontZ(0f, ty) - 0.012f);
                _tongue.localRotation = Quaternion.Euler(42f + Mathf.Sin(t * 3.1f) * 9f + open * 300f, Mathf.Sin(t * 2.3f) * 14f, 0f);
                _tongue.localScale = new Vector3(1f, 1f, len);
            }
        }

        private void PlaceTeeth(List<Transform> teeth, float half, float baseY, float depth, float yOffset, float show, float dir)
        {
            int n = teeth.Count;
            for (int i = 0; i < n; i++)
            {
                float s = n > 1 ? i / (float)(n - 1) * 1.8f - 0.9f : 0f;
                float x = s * half;
                float y = baseY - depth * (1f - s * s) + yOffset;
                teeth[i].localPosition = new Vector3(x, y, FrontZ(x, y) - 0.003f);
                teeth[i].localScale = new Vector3(_toothScale.x, _toothScale.y * show * (0.7f + 0.3f * Mathf.Cos(s * 3.2f)) * dir, _toothScale.z);
            }
        }
    }
}
