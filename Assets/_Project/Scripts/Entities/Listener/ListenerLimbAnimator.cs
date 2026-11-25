using UnityEngine;

namespace Ogni.Entities.Listener
{
    /// <summary>
    /// Процедурная анимация конечностей Слушателя (ноги шагают по скорости, руки болтаются, голова и уши
    /// «слушают», пасть раскрывается при атаке). Общее покачивание и падение тела делает ListenerAI.
    /// Работает на всех клиентах и считает скорость по смещению (позиция реплицируется NetworkTransform).
    /// </summary>
    [RequireComponent(typeof(ListenerAI))]
    public class ListenerLimbAnimator : MonoBehaviour
    {
        private ListenerAI _ai;
        private Transform _hipL, _hipR, _kneeL, _kneeR, _spine, _head, _jaw, _shL, _shR, _elL, _elR, _earL, _earR;
        private Vector3 _last;
        private float _speed;
        private float _phase;

        private void Awake()
        {
            _ai = GetComponent<ListenerAI>();
            Transform rig = transform.Find("VisualRig");
            if (rig == null) { enabled = false; return; }
            _hipL = Find(rig, "HipL"); _hipR = Find(rig, "HipR");
            _kneeL = Find(rig, "KneeL"); _kneeR = Find(rig, "KneeR");
            _spine = Find(rig, "Spine"); _head = Find(rig, "Head"); _jaw = Find(rig, "Jaw");
            _shL = Find(rig, "ShoulderL"); _shR = Find(rig, "ShoulderR");
            _elL = Find(rig, "ElbowL"); _elR = Find(rig, "ElbowR");
            _earL = Find(rig, "EarL"); _earR = Find(rig, "EarR");
            _last = transform.position;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        private void Update()
        {
            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            float v = (transform.position - _last).magnitude / dt;
            _last = transform.position;
            _speed = Mathf.Lerp(_speed, v, 1f - Mathf.Exp(-8f * dt));

            if (_ai.IsDying) return;
            bool chase = _ai.CurrentState == ListenerAI.State.Chase;
            float move = Mathf.Clamp01(_speed / 2.2f);
            _phase += dt * (3.2f + _speed * 2.4f);
            float now = Time.time;
            float swing = Mathf.Sin(_phase) * 38f * move;
            float swing2 = Mathf.Sin(_phase + Mathf.PI) * 38f * move;

            Set(_hipL, -14f + swing, 0f, 0f); Set(_hipR, -14f + swing2, 0f, 0f);
            Set(_kneeL, 34f + Mathf.Max(0f, -Mathf.Sin(_phase + 1f)) * 40f * move, 0f, 0f);
            Set(_kneeR, 34f + Mathf.Max(0f, -Mathf.Sin(_phase + 1f + Mathf.PI)) * 40f * move, 0f, 0f);

            float pr = _ai.AttackProgress;            // -1 - не атакует
            bool atk = pr >= 0f;
            float imp = _ai.AttackImpact01;
            float armBase = chase ? 38f : 6f;
            float walkArmL = -8f + swing2 * 0.7f + armBase * 0.4f;
            float walkArmR = -8f + swing * 0.7f + armBase * 0.4f;
            float walkElbow = -26f - move * 10f;
            float idleRollL = -6f + Mathf.Sin(now * 1.9f) * 3f;
            float idleRollR = 6f - Mathf.Sin(now * 1.7f) * 3f;

            float shLx = walkArmL, shRx = walkArmR, elLx = walkElbow, elRx = walkElbow;
            float shLz = idleRollL, shRz = idleRollR;
            float jawAtk = 0f, headAtk = 0f;
            if (atk)
            {
                // Правая лапа бьёт первой, левая запаздывает - получается рубящий удар двумя когтями.
                float prR = pr;
                float prL = Mathf.Max(0f, pr - 0.06f);
                shRx = ListenerAI.AttackCurve(prR, imp, walkArmR, -140f, 28f);
                shLx = ListenerAI.AttackCurve(prL, imp, walkArmL, -128f, 24f);
                elRx = ListenerAI.AttackCurve(prR, imp, walkElbow, -75f, -8f);
                elLx = ListenerAI.AttackCurve(prL, imp, walkElbow, -70f, -10f);
                shRz = ListenerAI.AttackCurve(prR, imp, idleRollR, 22f, -18f);   // когти сходятся к центру
                shLz = ListenerAI.AttackCurve(prL, imp, idleRollL, -22f, 18f);
                jawAtk = ListenerAI.AttackCurve(pr, imp, 0f, 22f, 62f);          // пасть щёлкает в момент удара
                headAtk = ListenerAI.AttackCurve(pr, imp, 0f, -14f, 16f);        // голова откидывается и бросается вперёд
            }

            Set(_shL, shLx, 0f, shLz);
            Set(_shR, shRx, 0f, shRz);
            Set(_elL, elLx, 0f, 0f);
            Set(_elR, elRx, 0f, 0f);

            // «Слушает»: голова вертится, уши подрагивают.
            float listenYaw = Mathf.Sin(now * 0.9f) * (chase ? 4f : 18f) + (Mathf.PerlinNoise(now * 3f, 7f) > 0.8f ? Mathf.Sin(now * 40f) * 6f : 0f);
            Set(_head, -22f + Mathf.Sin(now * 1.3f) * 3f + headAtk, atk ? listenYaw * 0.2f : listenYaw, -4f + Mathf.Sin(now * 0.7f) * 4f);
            float twitch = Mathf.PerlinNoise(now * 5f, 3f) > 0.72f ? Mathf.Sin(now * 55f) * 7f : 0f;
            if (_earL != null) _earL.localRotation = Quaternion.Euler(0f, twitch, 90f + 12f);
            if (_earR != null) _earR.localRotation = Quaternion.Euler(0f, -twitch, -90f - 12f);

            float jaw = 12f + (chase ? 20f : 4f) + jawAtk + Mathf.Sin(now * 3.1f) * 3f;
            Set(_jaw, jaw, 0f, 0f);
        }

        private static void Set(Transform t, float x, float y, float z)
        {
            if (t != null) t.localRotation = Quaternion.Euler(x, y, z);
        }
    }
}
