using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

namespace ProjectC.Ship
{
    /// <summary>
    /// T-CREW-02 — фундамент навигации по палубе движущегося корабля.
    ///
    /// NavMeshAgent привязан к статичному мировому NavMesh, а NavMeshDataInstance нельзя
    /// перемещать (только Remove + повторный AddNavMeshData → рвёт пути). Поэтому навмеш
    /// палубы регистрируется в фиксированной «нав-песочнице» (уникальный per-ship slot),
    /// а вся навигация идёт в ЛОКАЛЬНЫХ координатах палубы через прокси-агента
    /// (см. docs/Character/Skills/real-time-combat/npc-enemy/01_CREW_ON_MOVING_SHIP.md §4).
    ///
    /// T-CREW-05/fix: для ДВИЖУЩЕГОСЯ корабля slot-based origin (slot*separation) не работает,
    /// потому что deck-local координаты отсчитываются от ShipRoot, который едет, а навмеш
    /// должен лежать "под" кораблём. Решение:
    ///   1) В Register() ставим navFrameOrigin = позиция ShipRoot в момент спавна.
    ///   2) В LateUpdate() следим за сдвигом ShipRoot относительно navFrameOrigin; при
    ///      превышении _navFrameSeparation/2 — пере-регистрируем навмеш (Remove + Add).
    ///      На медленном корабле и большом slot (5000м) это раз в несколько минут; пути
    ///      агентов внутри слота не рвутся.
    ///
    /// PERF (2026-07-26): раунд-робин очередь регистрации.
    ///   NavMesh.AddNavMeshData дорог (6-50ms) + внутри unity вызывает NotifyNavMeshAdded →
    ///   LogStringToConsole → Editor-консоль. Раньше использовался random stagger 0-10s —
    ///   при 20+ кораблях это волна спайков на 10 секунд. Теперь статическая очередь:
    ///   регистрируем строго ≤1 корабль за кадр, гарантированно без кластеризации.
    ///
    /// Компонент вешается на ShipRoot и держит запечённый NavMeshData (bake делается редактором
    /// через NavMeshSurface при корабле в origin/identity — см. §5 доки). Рантайм использует
    /// только UnityEngine.AI (без зависимости на ассембли Unity.AI.Navigation).
    /// </summary>
    public class ShipDeckNav : NetworkBehaviour
    {
        [Header("NavMesh палубы")]
        [Tooltip("Запечённый NavMeshData палубы (bake через NavMeshSurface при ShipRoot в origin/identity). " +
                 "См. инструкцию §5 в 01_CREW_ON_MOVING_SHIP.md.")]
        [SerializeField] private NavMeshData _deckNavMeshData;

        [Tooltip("Регистрировать навмеш только на сервере (NPC-навигация серверная). Клиенту навмеш не нужен.")]
        [SerializeField] private bool _registerServerOnly = true;

        [Tooltip("Размер nav-слота (м). Навмеш статичен в пределах слота; при выходе ShipRoot за " +
                 "границу слота — пере-регистрация. Должен быть заведомо больше габарита палубы. " +
                 "5000м — для дальних перемещений; 1м — фактически отключает пере-регистрацию.")]
        [Min(100f)] [SerializeField] private float _navFrameSeparation = 5000f;

        [Tooltip("Если true — навмеш регистрируется сразу под ShipRoot (в его мировой позиции в момент " +
                 "Register). Это правильно для движущегося корабля: deck-local = InverseTransformPoint(world), " +
                 "а navFrameOrigin = ShipRoot.position ⇒ navPos = точка на палубе в мире = позиция на навмеше. " +
                 "Если false — старый slot-based режим (navFrameOrigin = slot * separation), подходит только " +
                 "для кораблей, остающихся в origin.")]
        [SerializeField] private bool _registerUnderShip = true;

        // runtime
        private NavMeshDataInstance _instance;
        private Vector3 _navFrameOrigin;
        private bool _registered;
        private bool _registrationFailed;
        private float _nextReregistrationTime;
        private Vector3 _lastRegisteredShipPos;

        // Static slot counter — для старого slot-based режима (не используется при _registerUnderShip=true).
        private static int _nextSlot;

        // === Round-robin очередь регистрации ===
        // PERF: гарантирует ≤1 AddNavMeshData за кадр (вместо random stagger'а, который кластеризуется).
        private static readonly Queue<ShipDeckNav> s_pendingRegistrations = new Queue<ShipDeckNav>();
        private static int s_registrationsThisFrame;
        private const int MAX_REGISTRATIONS_PER_FRAME = 1;

        // PERF: отключаем ExtractStackTrace для обычных логов в Editor.
        // NavMeshManager.NotifyNavMeshAdded спамит LogStringToConsole на каждый AddNavMeshData,
        // а Editor для каждого лога дёргает StackTraceUtility.ExtractStackTrace.
        // RuntimeInitializeOnLoadMethod — безопасно вызывает SetStackTraceLogType ДО загрузки сцен.
#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureEditorLogging()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        }
#endif

        /// <summary>true, если навмеш палубы зарегистрирован и валиден.</summary>
        public bool IsReady => _registered && _instance.valid;

        /// <summary>Точка нав-песочницы этого корабля.</summary>
        public Vector3 NavFrameOrigin => _navFrameOrigin;

        // === Конвертации координат ===
        public Vector3 WorldToDeckLocal(Vector3 world) => transform.InverseTransformPoint(world);
        public Vector3 DeckLocalToWorld(Vector3 deckLocal) => transform.TransformPoint(deckLocal);
        public Vector3 DeckLocalToNav(Vector3 deckLocal) => _navFrameOrigin + deckLocal;
        public Vector3 NavToDeckLocal(Vector3 navPos) => navPos - _navFrameOrigin;

        public bool SampleOnDeck(Vector3 world, out Vector3 worldHit, float maxDistance)
        {
            worldHit = world;
            if (!IsReady) return false;
            Vector3 navPos = DeckLocalToNav(WorldToDeckLocal(world));
            if (NavMesh.SamplePosition(navPos, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                worldHit = DeckLocalToWorld(NavToDeckLocal(hit.position));
                return true;
            }
            return false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (_registerServerOnly && !IsServer) return;

            // PERF: ставим в round-robin очередь вместо random stagger.
            // Очередь обрабатывается в LateUpdate — строго ≤1 корабль за кадр.
            s_pendingRegistrations.Enqueue(this);
        }

        public override void OnNetworkDespawn()
        {
            Unregister();
            base.OnNetworkDespawn();
        }

        private void OnDisable() => Unregister();

        private void LateUpdate()
        {
            if (!IsServer) return;

            // === Round-robin: обрабатываем очередь регистрации (≤1 за кадр) ===
            ProcessPendingRegistrations();

            // Уже зарегистрирован — следим за дрейфом
            if (!_registered) return;
            if (!_registerUnderShip) return;

            Vector3 shipPos = transform.position;
            Vector3 delta = shipPos - _lastRegisteredShipPos;
            delta.y = 0f;
            if (delta.sqrMagnitude > (_navFrameSeparation * 0.5f) * (_navFrameSeparation * 0.5f))
            {
                if (Time.time < _nextReregistrationTime)
                    return;

                Unregister();
                // Re-registration — ставим в очередь, не блокируем кадр
                s_pendingRegistrations.Enqueue(this);
                _nextReregistrationTime = Time.time + 30f;
            }
        }

        /// <summary>
        /// PERF: обрабатывает очередь pending регистраций. Вызывается из LateUpdate любого ShipDeckNav.
        /// Гарантирует ≤MAX_REGISTRATIONS_PER_FRAME за кадр.
        /// </summary>
        private static void ProcessPendingRegistrations()
        {
            s_registrationsThisFrame = 0;
            while (s_pendingRegistrations.Count > 0 && s_registrationsThisFrame < MAX_REGISTRATIONS_PER_FRAME)
            {
                var ship = s_pendingRegistrations.Dequeue();
                if (ship == null || !ship.IsSpawned || ship._registered || ship._registrationFailed)
                    continue;

                if (ship._deckNavMeshData == null)
                {
                    ship._registrationFailed = true;
                    continue;
                }

                ship.Register();
                s_registrationsThisFrame++;
            }
        }

        private void Register()
        {
            if (_registered) return;

            if (_registerUnderShip)
                _navFrameOrigin = transform.position;
            else
                _navFrameOrigin = new Vector3(_nextSlot++ * _navFrameSeparation, 0f, 0f);

            _lastRegisteredShipPos = transform.position;

            _instance = NavMesh.AddNavMeshData(_deckNavMeshData, _navFrameOrigin, Quaternion.identity);
            if (!_instance.valid)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[ShipDeckNav:{name}] NavMesh registration failed at {_navFrameOrigin}. " +
                                 $"Ship too far from origin — deck navigation disabled.", this);
#endif
                _registrationFailed = true;
                return;
            }
            _registered = true;
#if UNITY_EDITOR
            Debug.Log($"[ShipDeckNav:{name}] Registered at {_navFrameOrigin}", this);
#endif
        }

        private void Unregister()
        {
            if (!_registered) return;
            if (_instance.valid) _instance.Remove();
            _registered = false;
        }
    }
}
