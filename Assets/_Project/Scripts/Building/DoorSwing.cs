using UnityEngine;

namespace Ogni.Building
{
    /// <summary>
    /// Общая логика распашных дверей: в какую сторону открывать (всегда ОТ игрока, чтобы дверь не била его
    /// и не упиралась в него), плавный разгон/торможение и отключение коллайдеров, пока дверь в движении
    /// (подвижное полотно не может зажать или вытолкнуть игрока из проёма).
    /// Полотно двери - потомок pivot (петли); петли лежат на оси Y pivot.
    /// </summary>
    public sealed class DoorSwing
    {
        private readonly Transform _pivot;
        private readonly Collider[] _colliders;
        private readonly Quaternion _closedRot;
        private readonly float _leafDir;
        private readonly float _maxAngle;
        private float _angle;
        private float _target;
        private float _velocity;
        private bool _moving;

        public bool IsMoving => _moving;
        public float Angle => _angle;

        public DoorSwing(Transform pivot, float maxAngle, bool startsOpen, int startSign)
        {
            _pivot = pivot;
            _maxAngle = Mathf.Abs(maxAngle) < 1f ? 95f : Mathf.Abs(maxAngle);
            _closedRot = pivot.localRotation;
            _colliders = pivot.GetComponentsInChildren<Collider>(true);

            // Полотно лежит вдоль локальной оси X от петли: знак смещения первого коллайдера (или рендерера) даёт сторону.
            float dir = 1f;
            if (_colliders.Length > 0)
            {
                float x = pivot.InverseTransformPoint(_colliders[0].bounds.center).x;
                if (Mathf.Abs(x) > 0.01f) dir = Mathf.Sign(x);
            }

            _leafDir = dir;
            _angle = _target = startsOpen ? (startSign >= 0 ? 1f : -1f) * _maxAngle : 0f;
            Apply();
        }

        /// <summary>
        /// Знак поворота, при котором полотно уходит от точки worldPos (положение игрока).
        /// Поворот на +angle вокруг Y уводит конец полотна на -Z (при полотне вдоль +X), поэтому знак = сторона игрока * направление полотна.
        /// </summary>
        public int SwingSignAwayFrom(Vector3 worldPos)
        {
            Transform frame = _pivot.parent != null ? _pivot.parent : _pivot;
            float side = frame.InverseTransformPoint(worldPos).z;
            if (Mathf.Abs(side) < 0.02f) side = 1f;
            return (side >= 0f ? 1 : -1) * (_leafDir >= 0f ? 1 : -1);
        }

        /// <summary>state: 0 - закрыта, +1/-1 - открыта в соответствующую сторону.</summary>
        public void SetState(int state)
        {
            _target = state == 0 ? 0f : Mathf.Sign(state) * _maxAngle;
            _moving = true;
            SetCollidersEnabled(false);
        }

        /// <summary>Вызывать из Update, пока IsMoving.</summary>
        public void Tick(float dt)
        {
            if (!_moving) return;

            // SmoothDamp даёт плавный разгон и мягкое торможение (вместо рывка Slerp).
            _angle = Mathf.SmoothDampAngle(_angle, _target, ref _velocity, 0.22f, 520f, dt);
            Apply();

            if (Mathf.Abs(Mathf.DeltaAngle(_angle, _target)) < 0.25f && Mathf.Abs(_velocity) < 2f)
            {
                _angle = _target;
                _velocity = 0f;
                _moving = false;
                Apply();
                SetCollidersEnabled(true);
            }
        }

        private void Apply() => _pivot.localRotation = _closedRot * Quaternion.Euler(0f, _angle, 0f);

        private void SetCollidersEnabled(bool on)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null) _colliders[i].enabled = on;
            }
        }
    }
}
