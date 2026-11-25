using UnityEngine;

namespace Ogni.Entities.Mimic
{
    /// <summary>
    /// Анимация Подражателя: в засаде сжат и неподвижен (дрожат пальцы, челюсть чуть приоткрыта), при эхе склоняет голову
    /// и «говорит» челюстью, при броске бежит почти на четвереньках, при ударе раскрывает пасть и бьёт длинными руками.
    /// Вешается на VisualRig.
    /// </summary>
    public class MimicAnimator : MonoBehaviour
    {
        private MimicAI _ai;
        private Transform _spine, _neck, _head, _jaw, _shL, _shR, _elL, _elR, _hipL, _hipR, _knL, _knR, _pelvis;
        private float _phase;
        private float _crouch;

        private void Awake()
        {
            _ai = GetComponentInParent<MimicAI>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Pelvis": _pelvis = t; break;
                    case "Spine": _spine = t; break;
                    case "Neck": _neck = t; break;
                    case "Head": _head = t; break;
                    case "Jaw": _jaw = t; break;
                    case "ShoulderL": _shL = t; break;
                    case "ShoulderR": _shR = t; break;
                    case "ElbowL": _elL = t; break;
                    case "ElbowR": _elR = t; break;
                    case "HipL": _hipL = t; break;
                    case "HipR": _hipR = t; break;
                    case "KneeL": _knL = t; break;
                    case "KneeR": _knR = t; break;
                }
            }
        }

        private static void Set(Transform tr, float x, float y, float z, float k)
        {
            if (tr == null) return;
            tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.Euler(x, y, z), k);
        }

        private void Update()
        {
            if (_ai == null) return;
            float dt = Time.deltaTime, t = Time.time;
            float k = 1f - Mathf.Exp(-14f * dt);
            MimicAI.Mode mode = _ai.CurrentMode;
            float speed = _ai.Speed;
            bool sprint = mode == MimicAI.Mode.Ambush || mode == MimicAI.Mode.Retreat;
            bool echo = mode == MimicAI.Mode.Echo;
            float run = Mathf.Clamp01(speed / 5f);
            float walk = Mathf.Clamp01(speed / 1.2f);
            _phase += speed * dt * (sprint ? 2.1f : 3.2f);

            // Поза: в засаде сжат (приседает), в броске низко пригнут.
            float crouchTarget = sprint ? 0.55f : mode == MimicAI.Mode.Lurk ? 1f : 0.7f;
            _crouch = Mathf.Lerp(_crouch, crouchTarget, 1f - Mathf.Exp(-5f * dt));

            float spinePitch = 18f + _crouch * 22f + run * 24f;
            float jawOpen = 6f + Mathf.Sin(t * 1.3f) * 2f;
            float headRoll = Mathf.Sin(t * 0.7f) * 3f;
            float headPitch = -10f - _crouch * 6f;
            float shLx = 8f + Mathf.Sin(_phase) * 55f * run, shRx = 8f - Mathf.Sin(_phase) * 55f * run;
            float elL = -14f - run * 30f, elR = elL;
            float shZ = 10f;

            if (echo)
            {
                // «Говорит»: голова набок, челюсть ходит в такт чужому звуку.
                headRoll = 20f + Mathf.Sin(t * 0.5f) * 4f;
                headPitch = -4f;
                jawOpen = 10f + Mathf.Abs(Mathf.Sin(t * 8f)) * 14f;
            }
            else if (mode == MimicAI.Mode.Lurk)
            {
                // Пальцы подрагивают, взгляд «плывёт».
                elL += Mathf.Sin(t * 14f) * 3f; elR += Mathf.Sin(t * 13f + 1f) * 3f;
                headRoll += Mathf.Sin(t * 0.3f) * 6f;
            }
            else if (sprint) jawOpen = 38f + Mathf.Sin(t * 20f) * 4f;

            float pr = _ai.AttackProgress;
            if (pr >= 0f)
            {
                float imp = _ai.AttackImpact01;
                spinePitch = MonsterBase.AttackCurve(pr, imp, spinePitch, -8f, 62f);
                shRx = MonsterBase.AttackCurve(pr, imp, shRx, -150f, 55f);
                shLx = MonsterBase.AttackCurve(Mathf.Max(0f, pr - 0.06f), imp, shLx, -140f, 50f);
                elR = MonsterBase.AttackCurve(pr, imp, elR, -80f, -6f);
                elL = MonsterBase.AttackCurve(Mathf.Max(0f, pr - 0.06f), imp, elL, -74f, -6f);
                jawOpen = MonsterBase.AttackCurve(pr, imp, jawOpen, 24f, 62f);
                headPitch = MonsterBase.AttackCurve(pr, imp, headPitch, -22f, 14f);
            }

            Set(_spine, spinePitch, 0f, Mathf.Sin(_phase * 0.5f) * 3f * walk, k);
            Set(_neck, -spinePitch * 0.55f, 0f, 0f, k);
            Set(_head, headPitch, Mathf.Sin(t * 0.5f) * 8f, headRoll, k);
            Set(_jaw, jawOpen, 0f, 0f, k);
            Set(_shL, shLx, 0f, -shZ, k);
            Set(_shR, shRx, 0f, shZ, k);
            Set(_elL, elL, 0f, 0f, k);
            Set(_elR, elR, 0f, 0f, k);

            // Ноги: сгибаются при приседании, шагают в такт.
            float legSwing = Mathf.Sin(_phase) * 38f * Mathf.Max(walk * 0.5f, run);
            float bend = 18f + _crouch * 30f;
            Set(_hipL, -bend + legSwing, 0f, 3f, k);
            Set(_hipR, -bend - legSwing, 0f, -3f, k);
            Set(_knL, bend * 1.7f + Mathf.Max(0f, -legSwing) * 0.8f, 0f, 0f, k);
            Set(_knR, bend * 1.7f + Mathf.Max(0f, legSwing) * 0.8f, 0f, 0f, k);

            if (_pelvis != null)
            {
                float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.05f * Mathf.Max(walk * 0.5f, run);
                _pelvis.localPosition = Vector3.Lerp(_pelvis.localPosition, new Vector3(0f, -_crouch * 0.22f + bob, 0f), 1f - Mathf.Exp(-8f * dt));
            }
        }
    }
}
