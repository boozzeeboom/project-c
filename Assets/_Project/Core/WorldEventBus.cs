// T-X0: WorldEventBus — server-side static event bus.
// См. docs/NPC_quests/02_V2_ARCHITECTURE.md §2.3.12, 06_TRIGGERS_AND_INTEGRATION.md §6.7.
//
// T-X0 scope: minimal API (Publish<T>/Subscribe<T>/Unsubscribe<T>/Reset).
// Full D2 table (DayNightController, MarketServer, ContractServer publishes) — T-Q06+.
//
// Static singleton: простой, testable (Reset для EditMode), zero allocations в hot path.
// Не MonoBehaviour — не требует сцены/GameObject.

using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// Server-side world event bus. Single static point для publish/subscribe.
    /// </summary>
    /// <remarks>
    /// Subscribers: QuestTriggerService (T-Q06), HUD notifications, debug logging.
    /// Publishers: InventoryServer (T-X0), MarketServer (T-Q06+), ContractServer (T-X5),
    ///             DayNightController (T-Q06), QuestServer (T-Q06+).
    /// </remarks>
    public static class WorldEventBus
    {
        /// <summary>Sub-list per event type. Action<T> invocation — simple, fast.</summary>
        private static readonly Dictionary<Type, List<Delegate>> _subscribers = new Dictionary<Type, List<Delegate>>();

        /// <summary>Publish event to all subscribers of its runtime type. Synchronous.</summary>
        public static void Publish<T>(T ev) where T : WorldEvent
        {
            if (ev == null) return;
            if (!_subscribers.TryGetValue(typeof(T), out var list)) return;
            // Snapshot-iterate (защита от unsubscribe-while-iterating)
            for (int i = 0; i < list.Count; i++)
            {
                try
                {
                    ((Action<T>)list[i])?.Invoke(ev);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[WorldEventBus] Subscriber threw on {typeof(T).Name}: {ex}");
                }
            }
        }

        /// <summary>Subscribe handler for event type T. Возвращает токен (delegate) для удобного unsubscribe.</summary>
        public static Action<T> Subscribe<T>(Action<T> handler) where T : WorldEvent
        {
            if (handler == null) return null;
            if (!_subscribers.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _subscribers[typeof(T)] = list;
            }
            list.Add(handler);
            return handler;
        }

        /// <summary>Unsubscribe handler. Safe если handler не подписан.</summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : WorldEvent
        {
            if (handler == null) return;
            if (_subscribers.TryGetValue(typeof(T), out var list))
            {
                list.Remove(handler);
            }
        }

        /// <summary>Reset all subscribers. Editor / EditMode tests only.</summary>
        public static void Reset()
        {
            _subscribers.Clear();
        }

        /// <summary>Debug: count subscribers for type T.</summary>
        public static int SubscriberCount<T>() where T : WorldEvent
        {
            return _subscribers.TryGetValue(typeof(T), out var list) ? list.Count : 0;
        }
    }
}
