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
    /// палубы регистрируется ОДИН РАЗ в фиксированной «нав-песочнице» (уникальный per-ship
    /// nav-фрейм), а вся навигация идёт в ЛОКАЛЬНЫХ координатах палубы через прокси-агента
    /// (см. docs/Character/Skills/real-time-combat/npc-enemy/01_CREW_ON_MOVING_SHIP.md §4).
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

        [Tooltip("Разнос nav-фреймов между кораблями (м), чтобы их навмеши не пересекались в песочнице. " +
                 "Держать заведомо больше габарита палубы.")]
        [Min(100f)] [SerializeField] private float _navFrameSeparation = 5000f;

        // runtime
        private NavMeshDataInstance _instance;
        private Vector3 _navFrameOrigin;
        private bool _registered;
        private static int _nextSlot;

        /// <summary>true, если навмеш палубы зарегистрирован и валиден.</summary>
        public bool IsReady => _registered && _instance.valid;

        /// <summary>Фиксированная точка нав-песочницы этого корабля.</summary>
        public Vector3 NavFrameOrigin => _navFrameOrigin;

        // === Конвертации координат ===
        /// <summary>Мировая позиция → локальная позиция на палубе (относительно ShipRoot).</summary>
        public Vector3 WorldToDeckLocal(Vector3 world) => transform.InverseTransformPoint(world);
        /// <summary>Локальная позиция на палубе → мировая (с учётом текущей позы корабля, вкл. крен).</summary>
        public Vector3 DeckLocalToWorld(Vector3 deckLocal) => transform.TransformPoint(deckLocal);
        /// <summary>Локальная позиция на палубе → мировая точка в нав-фрейме (где лежит навмеш).</summary>
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

        private void Register()
        {
            if (_registered) return;
            if (_deckNavMeshData == null)
            {
                Debug.LogWarning($"[ShipDeckNav:{name}] Deck NavMesh Data не назначен — навигация по палубе выключена. " +
                                 $"Запеки палубу и назначь ассет (см. §5 в 01_CREW_ON_MOVING_SHIP.md).", this);
                return;
            }
            _navFrameOrigin = new Vector3(_nextSlot++ * _navFrameSeparation, 0f, 0f);
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
