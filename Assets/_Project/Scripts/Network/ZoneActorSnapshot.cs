using System.Collections.Generic;
using ProjectC.Player;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Network
{
    /// <summary>
    /// T-PERF15: общий per-frame снапшот сетевых актёров для MarketZone и OuterCommZone.
    /// Исключает два одинаковых обхода SpawnedObjects и повторные чтения Transform.
    /// Мировые позиции живут только текущий кадр, поэтому Floating Origin не нарушается.
    /// </summary>
    public static class ZoneActorSnapshot
    {
        public struct PolledPlayer
        {
            public Vector3 Position;
            public ulong OwnerClientId;
            public int Layer;
        }

        public struct PolledShip
        {
            public Vector3 Position;
            public ulong NetworkObjectId;
            public int Layer;
        }

        public static readonly List<PolledPlayer> Players = new List<PolledPlayer>(16);
        public static readonly List<PolledShip> Ships = new List<PolledShip>(32);
        private static int _frame = -1;

        public static void Refresh()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            Players.Clear();
            Ships.Clear();

            var manager = NetworkManager.Singleton;
            if (manager == null || manager.SpawnManager == null) return;
            foreach (var spawned in manager.SpawnManager.SpawnedObjects)
            {
                var obj = spawned.Value;
                if (obj == null || !obj.IsSpawned) continue;
                if (obj.TryGetComponent<NetworkPlayer>(out var player))
                {
                    Players.Add(new PolledPlayer
                    {
                        Position = player.transform.position,
                        OwnerClientId = player.OwnerClientId,
                        Layer = player.gameObject.layer
                    });
                }
                else if (obj.TryGetComponent<ShipController>(out var ship))
                {
                    Ships.Add(new PolledShip
                    {
                        Position = ship.transform.position,
                        NetworkObjectId = ship.NetworkObject.NetworkObjectId,
                        Layer = ship.gameObject.layer
                    });
                }
            }
        }
    }
}
