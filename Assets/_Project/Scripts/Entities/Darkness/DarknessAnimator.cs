using System.Collections.Generic;
using Ogni.World;
using UnityEngine;

namespace Ogni.Entities.Darkness
{
    /// <summary>
    /// Анимация Тьмы: щупальца извиваются (быстрее и размашистее в погоне), две «боевые» щупальца бьют с замахом,
    /// масса дышит, под светом сжимается и дрожит, днём оседает в землю, глаза разгораются при охоте.
    /// Вешается на VisualRig.
    /// </summary>
    public class DarknessAnimator : MonoBehaviour
    {
        private DarknessAI _ai;
        private readonly List<Transform> _tendrils = new();
        private readonly List<Quaternion> _base = new();
        private readonly List<float> _phase = new();
        private Transform _lash0, _lash1, _core;
        private readonly List<Transform> _eyes = new();
        private Vector3 _coreScale = Vector3.one;
        private Vector3 _eyeScale = Vector3.one * 0.05f;
        private float _shrink = 1f;
        private float _sink;
        private float _exposure;
        private float _nextExposureAt;

        private void Awake()
        {
            _ai = GetComponentInParent<DarknessAI>();
            foreach (Transform t in transform)
            {
                if (t.name.StartsWith("Tendril_")) { _tendrils.Add(t); _base.Add(t.localRotation); _phase.Add(Random.value * 6.28f); }
                else if (t.name == "Lash_0") _lash0 = t;
                else if (t.name == "Lash_1") _lash1 = t;
                else if (t.name == "Core") _core = t;
                else if (t.name.StartsWith("Eye_")) _eyes.Add(t);
            }

            if (_core != null) _coreScale = _core.localScale;
            if (_eyes.Count > 0) _eyeScale = _eyes[0].localScale;
        }

        private void Update()
        {
            if (_ai == null) return;
            float t = Time.time;
            var mode = _ai.CurrentMode;
            bool chase = mode == DarknessAI.Mode.Chase;
            bool burning = mode == DarknessAI.Mode.Burning;
            bool dormant = mode == DarknessAI.Mode.Dormant;

            if (t >= _nextExposureAt)
            {
                _nextExposureAt = t + 0.12f;
                _exposure = LightRegistry.GetIllumination(_ai.transform.position + Vector3.up * 0.9f, out _);
            }

            // Масса сжимается под светом и днём, оседает в землю.
            float targetShrink = dormant ? 0.55f : Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(_exposure * 1.6f));
            _shrink = Mathf.Lerp(_shrink, targetShrink, 1f - Mathf.Exp(-6f * Time.deltaTime));
            _sink = Mathf.Lerp(_sink, dormant ? -0.45f : 0f, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
            transform.localScale = Vector3.one * _shrink;
            Vector3 jitter = burning ? new Vector3(Random.Range(-0.04f, 0.04f), 0f, Random.Range(-0.04f, 0.04f)) : Vector3.zero;
            transform.localPosition = new Vector3(0f, _sink, 0f) + jitter;

            // Щупальца.
            float amp = burning ? 38f : chase ? 22f : 9f;
            float spd = burning ? 5f : chase ? 3.2f : 1.3f;
            for (int i = 0; i < _tendrils.Count; i++)
            {
                float p = _phase[i];
                Quaternion wob = Quaternion.Euler(Mathf.Sin(t * spd + p) * amp, Mathf.Sin(t * spd * 0.7f + p * 1.7f) * amp * 0.6f, Mathf.Sin(t * spd * 1.3f + p * 0.6f) * amp * 0.5f);
                _tendrils[i].localRotation = Quaternion.Slerp(_tendrils[i].localRotation, _base[i] * wob, 1f - Mathf.Exp(-12f * Time.deltaTime));
            }

            // Боевые щупальца бьют вперёд-вниз.
            float pr = _ai.AttackProgress;
            float imp = _ai.AttackImpact01;
            float lx0 = 0f, lx1 = 0f, lz0 = 0f, lz1 = 0f;
            if (pr >= 0f)
            {
                lx0 = MonsterBase.AttackCurve(pr, imp, 0f, -75f, 62f);
                lx1 = MonsterBase.AttackCurve(Mathf.Max(0f, pr - 0.07f), imp, 0f, -68f, 58f);
                lz0 = MonsterBase.AttackCurve(pr, imp, 0f, 18f, -14f);
                lz1 = MonsterBase.AttackCurve(Mathf.Max(0f, pr - 0.07f), imp, 0f, -18f, 14f);
            }
            else
            {
                lx0 = Mathf.Sin(t * 1.1f) * 8f; lx1 = Mathf.Sin(t * 1.3f + 1f) * 8f;
            }

            if (_lash0 != null) _lash0.localRotation = Quaternion.Slerp(_lash0.localRotation, Quaternion.Euler(lx0, 0f, lz0), 1f - Mathf.Exp(-30f * Time.deltaTime));
            if (_lash1 != null) _lash1.localRotation = Quaternion.Slerp(_lash1.localRotation, Quaternion.Euler(lx1, 0f, lz1), 1f - Mathf.Exp(-30f * Time.deltaTime));

            // Масса дышит, при ударе по ней вздрагивает.
            if (_core != null)
            {
                float breathe = 1f + Mathf.Sin(t * (chase ? 4.5f : 1.6f)) * 0.04f;
                float flinch = _ai.IsHurt ? 0.9f : 1f;
                _core.localScale = _coreScale * breathe * flinch;
            }

            // Глаза: тлеют в покое, разгораются на охоте, мигают.
            float eyeK = dormant ? 0f : chase ? 1f : mode == DarknessAI.Mode.Stalk ? 0.8f : 0.35f;
            float blink = Mathf.Sin(t * 0.9f) > 0.985f ? 0.05f : 1f;
            for (int i = 0; i < _eyes.Count; i++)
                _eyes[i].localScale = Vector3.Lerp(_eyes[i].localScale, _eyeScale * Mathf.Max(0.01f, eyeK * blink), 1f - Mathf.Exp(-10f * Time.deltaTime));
        }
    }
}
