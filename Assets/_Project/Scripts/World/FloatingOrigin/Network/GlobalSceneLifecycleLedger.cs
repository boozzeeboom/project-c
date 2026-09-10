using System;
using System.Collections.Generic;

namespace ProjectC.World.FloatingOrigin.Network
{
    public readonly struct GlobalSceneLoadTicket : IEquatable<GlobalSceneLoadTicket>
    {
        public ulong SessionId { get; }
        public ulong LoadGeneration { get; }
        public string SceneGuid { get; }
        public Guid LedgerId { get; }
        internal GlobalSceneLoadTicket(ulong session, ulong generation, string guid, Guid ledgerId) { SessionId = session; LoadGeneration = generation; SceneGuid = guid; LedgerId = ledgerId; }
        public bool Equals(GlobalSceneLoadTicket other) => SessionId == other.SessionId && LoadGeneration == other.LoadGeneration && SceneGuid == other.SceneGuid && LedgerId == other.LedgerId;
        public override bool Equals(object other) => other is GlobalSceneLoadTicket t && Equals(t);
        public override int GetHashCode() => SessionId.GetHashCode() ^ LoadGeneration.GetHashCode() ^ (SceneGuid?.GetHashCode() ?? 0);
    }
    public readonly struct GlobalSceneInstanceLifetime : IEquatable<GlobalSceneInstanceLifetime>
    {
        public ulong SessionId { get; }
        public ulong NetworkObjectId { get; }
        public ulong SpawnGeneration { get; }
        public bool IsValid => SessionId != 0 && SpawnGeneration != 0; // Object id zero is a valid actual identity.
        public GlobalSceneInstanceLifetime(ulong session, ulong objectId, ulong generation) { SessionId = session; NetworkObjectId = objectId; SpawnGeneration = generation; }
        public bool Equals(GlobalSceneInstanceLifetime other) => SessionId == other.SessionId && NetworkObjectId == other.NetworkObjectId && SpawnGeneration == other.SpawnGeneration;
        public override bool Equals(object other) => other is GlobalSceneInstanceLifetime t && Equals(t);
        public override int GetHashCode() => SessionId.GetHashCode() ^ NetworkObjectId.GetHashCode() ^ SpawnGeneration.GetHashCode();
    }

    public readonly struct GlobalSceneReceiptToken
    {
        public GlobalSceneLoadTicket Load { get; }
        public string SourceId { get; }
        public ulong Generation { get; }
        internal GlobalSceneReceiptToken(GlobalSceneLoadTicket load, string source, ulong generation) { Load = load; SourceId = source; Generation = generation; }
    }

    /// <summary>
    /// Receipt ledger for a future native executor. Does NOT Spawn/Despawn/SetParent/load scenes or certify native readiness.
    /// Register only after the external operation succeeded; acknowledge exclusions only after actual source retirement.
    /// </summary>
    public sealed class GlobalSceneLifecycleLedger
    {
        private sealed class Receipt { public int Frame; public GlobalSceneInstanceLifetime Lifetime; public bool Excluded; public ulong Generation; }
        private sealed class Load
        {
            public GlobalSceneLoadTicket Ticket;
            public bool Faulted;
            public readonly Dictionary<string, Receipt> Records = new Dictionary<string, Receipt>(StringComparer.Ordinal);
            public readonly HashSet<string> Accounted = new HashSet<string>(StringComparer.Ordinal);
        }
        private readonly GlobalScenePlan _plan;
        private readonly ulong _session;
        private ulong _nextLoad, _nextReceipt;
        private readonly Guid _ledgerId = Guid.NewGuid();
        private readonly Dictionary<string, Load> _loads = new Dictionary<string, Load>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, string> _objects = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, ulong> _highestSpawn = new Dictionary<ulong, ulong>();
        private readonly Dictionary<string, List<string>> _children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _sceneEntries = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        public GlobalSceneLifecycleLedger(GlobalScenePlan plan, ulong sessionId)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan)); if (sessionId == 0) throw new ArgumentOutOfRangeException(nameof(sessionId)); _session = sessionId;
            foreach (var entry in plan.SpawnOrder)
            {
                _children.Add(entry.SourceId, new List<string>());
                if (!_sceneEntries.TryGetValue(entry.SceneGuid, out var list)) { list = new List<string>(); _sceneEntries.Add(entry.SceneGuid, list); }
                list.Add(entry.SourceId);
            }
            foreach (var entry in plan.SpawnOrder) if (entry.ParentSourceId.Length != 0) _children[entry.ParentSourceId].Add(entry.SourceId);
        }
        public bool TryBeginLoad(string sceneGuid, out GlobalSceneLoadTicket ticket)
        {
            ticket = default;
            if (!_plan.ContainsScene(sceneGuid) || _loads.ContainsKey(sceneGuid)) return false;
            ticket = new GlobalSceneLoadTicket(_session, checked(++_nextLoad), sceneGuid, _ledgerId);
            _loads.Add(sceneGuid, new Load { Ticket = ticket }); return true;
        }
        public bool IsCurrent(GlobalSceneLoadTicket ticket) => Current(ticket, out _);
        public bool TryRecordLive(GlobalSceneLoadTicket ticket, string sourceId, int frameId, GlobalSceneInstanceLifetime lifetime, out GlobalSceneReceiptToken token, out string error)
        {
            error = null; token = default;
            if (!Entry(ticket, sourceId, out var load, out var entry) || load.Faulted || load.Records.ContainsKey(sourceId) || entry.Treatment == GlobalSceneTreatment.Exclude)
                return Fail("unavailable_duplicate_or_excluded_source", out error);
            bool requiresNetworkLifetime = entry.IsNetwork || entry.RequiresNetworkLifecycle;
            if ((entry.Spatial ? frameId <= 0 : frameId != 0) || (requiresNetworkLifetime ? !lifetime.IsValid || lifetime.SessionId != _session : !lifetime.Equals(default)))
                return Fail("wrong_frame_or_instance_lifetime", out error);
            if (requiresNetworkLifetime && _objects.ContainsKey(lifetime.NetworkObjectId)) return Fail("network_object_already_registered", out error);
            if (requiresNetworkLifetime && _highestSpawn.TryGetValue(lifetime.NetworkObjectId, out var previous) && lifetime.SpawnGeneration <= previous)
                return Fail("reused_or_stale_network_lifetime", out error);
            if (entry.ParentSourceId.Length != 0)
            {
                if (!load.Records.TryGetValue(entry.ParentSourceId, out var parent) || parent.Excluded) return Fail("parent_not_registered", out error);
                if (entry.Spatial && parent.Frame != frameId) return Fail("parent_frame_mismatch", out error);
            }
            ulong generation = checked(++_nextReceipt);
            load.Records.Add(sourceId, new Receipt { Frame = frameId, Lifetime = lifetime, Generation = generation });
            token = new GlobalSceneReceiptToken(ticket, sourceId, generation);
            load.Accounted.Add(sourceId);
            if (requiresNetworkLifetime) { _objects.Add(lifetime.NetworkObjectId, sourceId); _highestSpawn[lifetime.NetworkObjectId] = lifetime.SpawnGeneration; }
            return true;
        }
        public bool TryRecordExclusion(GlobalSceneLoadTicket ticket, string sourceId)
        {
            if (!Entry(ticket, sourceId, out var load, out var entry) || entry.Treatment != GlobalSceneTreatment.Exclude || load.Records.ContainsKey(sourceId)) return false;
            // Retirement acknowledgement is child-first, even for excluded descendants.
            foreach (var child in _children[sourceId])
                if (!load.Records.TryGetValue(child, out var receipt) || !receipt.Excluded) return false;
            load.Records.Add(sourceId, new Receipt { Excluded = true }); load.Accounted.Add(sourceId); return true;
        }
        public bool AllSourcesAccountedFor(GlobalSceneLoadTicket ticket)
        {
            if (!Current(ticket, out var load) || load.Faulted) return false;
            if (_sceneEntries.TryGetValue(ticket.SceneGuid, out var entries)) foreach (string id in entries) if (!load.Records.ContainsKey(id)) return false;
            return true; // Accounting only, deliberately not named IsReadyForSimulation.
        }
        public IReadOnlyList<string> GetRetireOrder(GlobalSceneLoadTicket ticket)
        {
            var result = new List<string>();
            if (Current(ticket, out var load))
                foreach (var entry in _plan.RetireOrder) if (load.Records.TryGetValue(entry.SourceId, out var receipt) && !receipt.Excluded) result.Add(entry.SourceId);
            return result.AsReadOnly();
        }
        /// <summary>Read-only admission before an irreversible native retirement; it does not consume the receipt.</summary>
        public bool CanRecordRetired(GlobalSceneReceiptToken token, out string error)
        {
            error = null; string sourceId = token.SourceId;
            if (token.Generation == 0 || !Entry(token.Load, sourceId, out var load, out _) || !load.Records.TryGetValue(sourceId, out var receipt) || receipt.Excluded ||
                receipt.Generation != token.Generation) return Fail("stale_retirement_receipt", out error);
            foreach (var child in _children[sourceId])
                if (load.Records.TryGetValue(child, out var childReceipt) && !childReceipt.Excluded)
                    return Fail("live_descendant_blocks_parent_retirement", out error);
            return true;
        }
        public bool TryRecordRetired(GlobalSceneReceiptToken token, out string error)
        {
            if (!CanRecordRetired(token, out error)) return false;
            Entry(token.Load, token.SourceId, out var load, out var entry);
            var receipt = load.Records[token.SourceId];
            load.Records.Remove(token.SourceId);
            if (entry.IsNetwork || entry.RequiresNetworkLifecycle) _objects.Remove(receipt.Lifetime.NetworkObjectId);
            return true;
        }
        public bool MarkFaulted(GlobalSceneLoadTicket ticket)
        { if (!Current(ticket, out var load)) return false; load.Faulted = true; return true; }
        public bool TryEndLoad(GlobalSceneLoadTicket ticket)
        {
            if (!Current(ticket, out var load)) return false;
            // Historical accounting is required here; this is cleanup completion, not proof everything is currently live.
            if (_sceneEntries.TryGetValue(ticket.SceneGuid, out var entries)) foreach (string id in entries) if (!load.Accounted.Contains(id)) return false;
            foreach (var receipt in load.Records.Values) if (!receipt.Excluded) return false;
            return _loads.Remove(ticket.SceneGuid); // No bulk reset that can forget live native objects.
        }
        private bool Entry(GlobalSceneLoadTicket ticket, string id, out Load load, out GlobalScenePlannedEntry entry)
        { entry = null; return Current(ticket, out load) && _plan.TryGet(id, out entry) && entry.SceneGuid == ticket.SceneGuid; }
        private bool Current(GlobalSceneLoadTicket ticket, out Load load)
        { load = null; return ticket.LedgerId == _ledgerId && ticket.SessionId == _session && ticket.SceneGuid != null && _loads.TryGetValue(ticket.SceneGuid, out load) && load.Ticket.Equals(ticket); }
        private static bool Fail(string text, out string error) { error = text; return false; }
    }
}
