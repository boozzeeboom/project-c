// =====================================================================================
// PickupDeckRide.cs — pickup'ы (PickupItem, NpcLootPickup) на движущейся палубе (T-PICKUP-RIDE-01)
// =====================================================================================
// Документация:
//   • docs/NPC_others_peacfull/pc_ship/09_MOVING_PLATFORM_CHARACTER_PHYSICS.md — L3
//
// Что делает (CARRY-формула, как NetworkPlayer / NpcBrain fallback):
//   • LateUpdate: SphereCast вниз по _platformMask → если нашли палубу,
//     считаем Δпозиции платформы за кадр через PlatformRideHelper.ComputeCarryDelta
//     и прибавляем к transform.position.
//   • Pitch/roll палубы НЕ применяются (дисциплина уровня проекта, см.
//     PlatformRideHelper.cs:8-9). Только translation + yaw.
//   • Каждый peer (server + clients) считает carry локально. Это тот же подход,
//     что у NetworkPlayer.ApplyPlatformCarry (owner-only) и NpcBrain.FixedUpdate
//     (server-side, fallback). Геометрия сцены одинакова на server и клиентах →
//     результат идентичен.
//
// Почему НЕ NGO TrySetParent (2026-07-02, R1-fix-fail):
//   • NetworkObject.TrySetParent требует spawned parent + specific NetworkObject
//     на direct parent. У NPC-корабля root — ShipController : NetworkBehaviour,
//     но дочерние коллайдеры палубы (BoxCollider) — на child GO без NetworkObject.
//     Даже с подъёмом по иерархии NGO проверка `transform.parent.TryGetComponent`
//     ругалась, и в результате pickup не цеплялся.
//   • Carry-формула проще и проверена на персонаже + NPC — без новых edge cases.
//
// Уровень L3 в трёхуровневой системе:
//   L1 NetworkPlayer.ApplyPlatformCarry (owner-only, в ProcessMovement)
//   L2 NpcBrain.FixedUpdate (server-side, NGO-parent или carry fallback)
//   L3 PickupDeckRide (этот файл, чистая carry-формула)
//
// Контракт с владельцем (PickupItem / NpcLootPickup):
//   • Update владельца НЕ пишет transform.position, если DeckParent != null.
//   • Update владельца вызывает UpdateWorldBaseToCurrent() перед записью позиции,
//     чтобы база для бобаинга обновлялась на текущую мировую (без неё pickup
//     «прыгнет» в старую _startPosition при отцепке и дрейфит).

using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// T-PICKUP-RIDE-01: компонент, который удерживает pickup на палубе движущегося корабля.
    /// Использует carry-формулу (Δпозиции + опц. Δ yaw) — аналогично NetworkPlayer.ApplyPlatformCarry.
    /// </summary>
    [DisallowMultipleComponent]
    public class PickupDeckRide : MonoBehaviour
    {
        [Header("Платформа (moving-platform carry)")]
        [Tooltip("Включить carry: probe вниз и применять Δпозиции палубы каждый кадр.")]
        [SerializeField] private bool _platformCarryEnabled = true;

        [Tooltip("Слои палуб/движущихся платформ. 0 = выключено (как у NpcBrain/NetworkPlayer).")]
        [SerializeField] private LayerMask _platformMask = ~0;

        [Tooltip("Добавочная дальность probe вниз ниже пивота pickup'а (м).")]
        [SerializeField] private float _probeDistance = 0.4f;

        [Tooltip("Радиус SphereCast для поиска палубы (м). Меньше = точнее попадание на конкретную палубу.")]
        [SerializeField] private float _probeRadius = 0.3f;

        [Tooltip("Переносить курсовой поворот (yaw) палубы на pickup. Pitch/roll НЕ переносятся.")]
        [SerializeField] private bool _carryYaw = true;

        [Tooltip("Сколько кадров без опоры терпим, прежде чем считать что сошли (гистерезис). 8 = устойчиво к прыжкам через стыки коллайдеров палубы.")]
        [Min(1)]
        [SerializeField] private int _missFramesToClear = 8;

        [Header("Дебаг")]
        [Tooltip("Логировать attach/detach/miss в Console.")]
        [SerializeField] private bool _debugLogging = false;

        // --- runtime ---
        private Transform _platform;           // текущая палуба (к которой мы «приклеены»)
        private Vector3 _platformLastPos;
        private Quaternion _platformLastRot;
        private Vector3 _worldBasePosition;   // база для бобаинга когда не на палубе
        private int _missFrames;
        private bool _warnedMaskEmpty;

        /// <summary>Текущий transform палубы или null. Используется PickupItem/NpcLootPickup для выбора
        /// «на палубе» vs «не на палубе». На палубе Update НЕ трогает transform.position (carry делает это сам).</summary>
        public Transform DeckParent => _platform;

        /// <summary>
        /// T-PICKUP-RIDE-01 final fix: pickup вызывает этот метод в Update в тот момент,
        /// когда собирается писать transform.position (= бобаинг в свободном режиме).
        /// Без этого база устаревает после carry: pickup спавнится в точке A, едет с
        /// палубой в B, потом сошёл с палубы — и «прыгает» обратно в A.
        /// </summary>
        public void RefreshWorldBase()
        {
            _worldBasePosition = transform.position;
        }

        /// <summary>Текущая мировая «база» для бобаинга (обновляется через RefreshWorldBase).</summary>
        public Vector3 WorldBasePosition => _worldBasePosition;

        private void Awake()
        {
            _worldBasePosition = transform.position;
        }

        private void OnDisable()
        {
            // R4: сбросить платформу чтобы не «висеть» на уничтоженной палубе
            _platform = null;
            _missFrames = 0;
        }

        private void OnDestroy()
        {
            _platform = null;
        }

        private void LateUpdate()
        {
            if (!_platformCarryEnabled) return;

            if (_platformMask == 0)
            {
                if (!_warnedMaskEmpty)
                {
                    Debug.LogWarning($"[PickupDeckRide:{name}] _platformMask пуст — moving-platform carry не работает. Назначь слои палуб/платформ в инспекторе.");
                    _warnedMaskEmpty = true;
                }
                return;
            }

            // Probe вниз (та же логика, что NetworkPlayer.DetectGroundPlatform).
            // Origin чуть выше pickup'а — чтобы не цеплять собственный collider.
            Vector3 origin = transform.position + Vector3.up * _probeRadius;
            float castDist = _probeRadius + _probeDistance;

            Transform platform = PlatformRideHelper.DetectPlatform(origin, _probeRadius, castDist, _platformMask);

            if (platform == null)
            {
                _missFrames++;
                if (_missFrames >= _missFramesToClear && _platform != null)
                {
                    if (_debugLogging) Debug.Log($"[PickupDeckRide:{name}] detached from '{_platform.name}' (miss {_missFrames}f, world={transform.position})");
                    _platform = null;
                }
                return;
            }

            _missFrames = 0;

            // Первичная привязка или смена платформы: инициализировать кэш.
            // Не делаем carry в первом кадре attach (избегаем рывка если lastPos был
            // инициализирован позицией старой платформы).
            if (platform != _platform)
            {
                _platform = platform;
                _platformLastPos = platform.position;
                _platformLastRot = platform.rotation;
                if (_debugLogging) Debug.Log($"[PickupDeckRide:{name}] attached to '{platform.name}' (world={transform.position})");
                return;
            }

            // Carry-формула (Δ позиции + Δ yaw). PlatformRideHelper.ComputeCarryDelta вернёт
            // мировую дельту переноса, включая орбитальную поправку при повороте палубы.
            Vector3 deltaPos = PlatformRideHelper.ComputeCarryDelta(
                platform, transform.position,
                _platformLastPos, _platformLastRot,
                _carryYaw, out float deltaYaw);

            if (deltaPos.sqrMagnitude > 0f)
            {
                transform.position += deltaPos;
            }
            if (_carryYaw && Mathf.Abs(deltaYaw) > 0.0001f)
            {
                transform.rotation = Quaternion.AngleAxis(deltaYaw, Vector3.up) * transform.rotation;
            }

            _platformLastPos = platform.position;
            _platformLastRot = platform.rotation;
        }
    }
}
