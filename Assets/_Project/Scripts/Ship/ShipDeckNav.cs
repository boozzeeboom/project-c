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
        private Vector3 _lastRegisteredShipPos;
        // Static slot counter — для старого slot-based режима (не используется при _registerUnderShip=true).
        private static int _nextSlot;

        /// <summary>true, если навмеш палубы зарегистрирован и валиден.</summary>
        public bool IsReady => _registered && _instance.valid;

        /// <summary>Точка нав-песочницы этого корабля.</summary>
        public Vector3 NavFrameOrigin => _navFrameOrigin;

        // === Конвертации координат ===
        /// <summary>Мировая позиция → локальная позиция на палубе (относительно ShipRoot).</summary>
        public Vector3 WorldToDeckLocal(Vector3 world) => transform.InverseTransformPoint(world);
        /// <summary>Локальная позиция на палубе → мировая (с учётом текущей позы корабля, вкл. крен).</summary>
        public Vector3 DeckLocalToWorld(Vector3 deckLocal) => transform.TransformPoint(deckLocal);
        /// <summary>Локальная позиция на палубе → точка в нав-фрейме (где лежит навмеш).</summary>
        public Vector3 DeckLocalToNav(Vector3 deckLocal) => _navFrameOrigin + deckLocal;
        /// <summary>Точка нав-фрейма → локальная позиция на палубе.</summary>
        public Vector3 NavToDeckLocal(Vector3 navPos) => navPos - _navFrameOrigin;

        /// <summary>
        /// Спроецировать мировую точку на навмеш палубы. worldHit — ближайшая точка на палубе
        /// в мировых координатах (с учётом текущей позы корабля). false, если навмеш не готов
        /// или точка вне палубы в пределах maxDistance.
        /// </summary>
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
            Register();
        }

        public override void OnNetworkDespawn()
        {
            Unregister();
            base.OnNetworkDespawn();
        }

        private void OnDisable() => Unregister();

        // T-CREW-05/fix: следим за движением ShipRoot и пере-регистрируем навмеш при выходе за слот.
        // На статичных кораблях (_navFrameSeparation огромный) LateUpdate пустой; на летающих —
        // срабатывает раз в несколько минут, перерегистрирует безболезненно.
        private void LateUpdate()
        {
            if (!_registered || !IsServer) return;
            // Проверяем только если наш navFrameOrigin привязан к ShipRoot (не slot-based).
            if (!_registerUnderShip) return;

            Vector3 shipPos = transform.position;
            // Смещение ShipRoot от навмеша (в плоскости XZ — Y нас не интересует, палуба горизонтальна).
            Vector3 delta = shipPos - _lastRegisteredShipPos;
            delta.y = 0f;
            if (delta.sqrMagnitude > (_navFrameSeparation * 0.5f) * (_navFrameSeparation * 0.5f))
            {
                Unregister();
                Register();
            }
        }

        private void Register()
        {
            if (_registered) return;
            if (_deckNavMeshData == null)
            {
                Debug.LogWarning($"[ShipDeckNav:{name}] Deck NavMesh Data не назначен — навигация по палубе выключена. " +
                                 $"Запеки палубу и назначь ассет (см. §5 в 01_CREW_ON_MOVING_SHIP.md).", this);
                return;
            }

            if (_registerUnderShip)
            {
                // T-CREW-05/fix: навмеш привязан к текущей мировой позиции ShipRoot.
                // Для движущегося корабля это работает корректно с LateUpdate re-registration.
                _navFrameOrigin = transform.position;
            }
            else
            {
                // Legacy slot-based режим (для кораблей, остающихся в origin).
                _navFrameOrigin = new Vector3(_nextSlot++ * _navFrameSeparation, 0f, 0f);
            }
            _lastRegisteredShipPos = transform.position;

            _instance = NavMesh.AddNavMeshData(_deckNavMeshData, _navFrameOrigin, Quaternion.identity);
            if (!_instance.valid)
            {
                Debug.LogError($"[ShipDeckNav:{name}] Не удалось зарегистрировать NavMeshData палубы в нав-фрейме {_navFrameOrigin}.", this);
                return;
            }
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered) return;
            if (_instance.valid) _instance.Remove();
            _registered = false;
        }
    }
}