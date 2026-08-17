using System;
using System.Threading;

namespace ProjectC.Trade.Repository
{
    /// <summary>
    /// Process-wide critical section for player-economy mutations.
    ///
    /// This is a concurrency boundary, not a crash-safe multi-file commit: concrete
    /// repositories still persist their keys/files according to their own atomic-write
    /// protocol, while domain operations use compensating rollback on a failed write.
    /// </summary>
    public static class RepositoryTransactionScope
    {
        private static readonly object SyncRoot = new object();

        public static IDisposable Acquire()
        {
            Monitor.Enter(SyncRoot);
            return new Scope();
        }

        public static T Execute<T>(IPlayerDataRepository repository, Func<T> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            IDisposable scope = repository != null ? repository.AcquireTransactionLock() : null;
            try
            {
                return operation();
            }
            finally
            {
                scope?.Dispose();
            }
        }

        public static void Execute(IPlayerDataRepository repository, Action operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            IDisposable scope = repository != null ? repository.AcquireTransactionLock() : null;
            try
            {
                operation();
            }
            finally
            {
                scope?.Dispose();
            }
        }

        private sealed class Scope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Monitor.Exit(SyncRoot);
            }
        }
    }
}