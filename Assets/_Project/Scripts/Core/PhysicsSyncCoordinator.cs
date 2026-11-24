using UnityEngine;

namespace ProjectC.Core
{
    /// <summary>
    /// T-PERF20: объединяет регулярные запросы синхронизации Transform → PhysX.
    /// Телепорты и транзакции, которым нужен немедленный собственный SyncTransforms,
    /// продолжают вызывать Physics.SyncTransforms напрямую.
    /// </summary>
    public static class PhysicsSyncCoordinator
    {
        private static int _lastSyncFrame = -1;

        public static void Request()
        {
            int frame = Time.frameCount;
            if (_lastSyncFrame == frame)
                return;

            Physics.SyncTransforms();
            _lastSyncFrame = frame;
        }
    }
}
