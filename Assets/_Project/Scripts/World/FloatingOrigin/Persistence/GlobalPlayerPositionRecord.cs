using System;

namespace ProjectC.World.FloatingOrigin.Persistence
{
    /// <summary>
    /// Origin/session-independent player checkpoint. Identity must come from a trusted persistent identity provider,
    /// never by stringifying NGO clientId. Ship affinity is restore intent, NOT a parent-local position.
    /// This value neither selects a runtime frame nor restores an actor.
    /// </summary>
    public sealed class GlobalPlayerPositionRecord
    {
        public const int MaxIdentityLength = 128;
        public const long MaxSavedAtUnix = 253402300799L;
        public string PlayerId { get; }
        public GlobalPosition Position { get; }
        public bool InShip { get; }
        public string ShipPersistentId { get; }
        public long SavedAtUnix { get; }

        public GlobalPlayerPositionRecord(string playerId, GlobalPosition position, bool inShip, string shipPersistentId, long savedAtUnix)
        {
            if (!IsValidIdentity(playerId)) throw new ArgumentException("Explicit persistent player identity required.", nameof(playerId));
            if (!position.IsFinite) throw new ArgumentException("Global point must be finite.", nameof(position));
            if (shipPersistentId == null || (inShip ? !IsValidIdentity(shipPersistentId) : shipPersistentId.Length != 0))
                throw new ArgumentException("Ship affinity and persistent ship identity must agree.", nameof(shipPersistentId));
            if (savedAtUnix < 0 || savedAtUnix > MaxSavedAtUnix) throw new ArgumentOutOfRangeException(nameof(savedAtUnix));
            PlayerId = playerId;
            // Negative zero carries no distinct point semantics; normalize it before durable serialization/checksum.
            Position = new GlobalPosition(position.X == 0d ? 0d : position.X, position.Y == 0d ? 0d : position.Y, position.Z == 0d ? 0d : position.Z);
            InShip = inShip; ShipPersistentId = shipPersistentId; SavedAtUnix = savedAtUnix;
        }
        public static bool IsValidIdentity(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaxIdentityLength) return false;
            foreach (char c in value)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == ':' || c == '.' || c == '@' || c == '/')) return false;
            return true;
        }
    }
}
