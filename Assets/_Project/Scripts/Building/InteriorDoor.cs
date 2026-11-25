using System.Collections.Generic;
using Ogni.Core;
using UnityEngine;

namespace Ogni.Building
{
    /// <summary>
    /// Лёгкая межкомнатная дверь (квартиры в многоэтажках). Это НЕ NetworkObject: в городе таких дверей
    /// больше тысячи, поэтому состояние синхронизирует один общий <see cref="DoorNetSync"/> по числовому doorId.
    /// Состояние: 0 - закрыта, +1/-1 - открыта в одну из сторон. Дверь всегда открывается ОТ игрока,
    /// движется плавно, а пока качается - не имеет коллизии (не зажмёт и не вытолкнет игрока).
    /// </summary>
    public class InteriorDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform pivot;
        [SerializeField] private int doorId = -1;
        [SerializeField] private float openAngle = 95f;
        [SerializeField] private bool startsOpen;
        [SerializeField] private float noiseRadius = 4f;

        private static readonly Dictionary<int, InteriorDoor> Registry = new();

        private DoorSwing _swing;
        private int _state;

        public int DoorId => doorId;
        public int State => _state;
        public bool IsOpen => _state != 0;
        public int StartState => startsOpen ? 1 : 0;
        public string Prompt => _state != 0 ? "Закрыть дверь" : "Открыть дверь";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Registry.Clear();

        private void Awake()
        {
            if (pivot == null) pivot = transform;
            _state = startsOpen ? 1 : 0;
            _swing = new DoorSwing(pivot, openAngle, startsOpen, 1);

            if (doorId >= 0) Registry[doorId] = this;
            enabled = false; // Update нужен только пока дверь движется.
        }

        private void OnDestroy()
        {
            if (doorId >= 0 && Registry.TryGetValue(doorId, out InteriorDoor d) && d == this) Registry.Remove(doorId);
        }

        private void Update()
        {
            _swing.Tick(Time.deltaTime);
            if (!_swing.IsMoving) enabled = false;
        }

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            Vector3 from = interactor != null ? interactor.transform.position : transform.position;
            DoorNetSync.RequestToggle(this, _swing.SwingSignAwayFrom(from), from);
        }

        public int SwingSignAwayFrom(Vector3 worldPos) => _swing.SwingSignAwayFrom(worldPos);

        public void SetState(int state)
        {
            state = Mathf.Clamp(state, -1, 1);
            if (_state == state) return;
            _state = state;
            _swing.SetState(state);
            enabled = true;
        }

        public void EmitNoise() => NoiseBus.Emit(transform.position, noiseRadius, gameObject);

        public static bool TryGet(int id, out InteriorDoor door) => Registry.TryGetValue(id, out door) && door != null;
    }
}
