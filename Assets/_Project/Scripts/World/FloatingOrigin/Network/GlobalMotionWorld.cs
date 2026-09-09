using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>Immutable frame registration. Its owning world/run, not the numeric id alone, defines its lifetime.</summary>
    public sealed class GlobalMotionFrame
    {
        public int Id { get; }
        public ulong RunGeneration { get; }
        public LocalCoordinateFrame Coordinates { get; }
        public PhysicsScene Physics { get; }
        internal GlobalMotionFrame(int id, ulong run, LocalCoordinateFrame coordinates, PhysicsScene physics)
        { Id = id; RunGeneration = run; Coordinates = coordinates; Physics = physics; }
    }

    /// <summary>
    /// Explicit per-NetworkManager coordinator. Attach only during the later prefab/scene migration.
    /// Registration does NOT move content; changing a populated frame requires the future rebase transaction.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-20000)]
    public sealed class GlobalMotionWorld : MonoBehaviour
    {
        private NetworkManager _manager;
        private GlobalMotionSession _serverSession;
        private ulong _run;
        private bool _running;
        private int _mainThreadId;
        private readonly Dictionary<int, GlobalMotionFrame> _frames = new Dictionary<int, GlobalMotionFrame>();
        private readonly Dictionary<ulong, GlobalMotionPoseAdapter> _actors = new Dictionary<ulong, GlobalMotionPoseAdapter>();
        private readonly List<GlobalMotionPoseAdapter> _ordered = new List<GlobalMotionPoseAdapter>();
        public NetworkManager Manager => _manager;
        public bool IsRunning => isActiveAndEnabled && _running && _manager != null && _manager.IsListening;
        public ulong RunGeneration => _run;
        public int RegisteredActorCount => _actors.Count;
        public bool IsOnWorldThread => _mainThreadId != 0 && System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        /// <summary>Read-only checkpoint scope. Never exposes the issuer or admits off-thread Unity access.</summary>
        public bool TryGetServerCheckpointScope(out ulong sessionId, out ulong runGeneration)
        {
            sessionId = 0; runGeneration = 0;
            if (!IsOnWorldThread ||
                !IsRunning || _manager != NetworkManager.Singleton || !_manager.IsServer || _manager.ShutdownInProgress ||
                _serverSession == null || !GlobalMotionNetworkStartup.IsInstalled(_manager)) return false;
            sessionId = _serverSession.SessionId; runGeneration = _run; return true;
        }

        private void OnEnable()
        {
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            if (_manager == null)
            {
                _manager = GetComponent<NetworkManager>();
                if (_manager == null) return;
                _manager.OnServerStarted += Started;
                _manager.OnClientStarted += Started;
                _manager.OnServerStopped += Stopped;
                _manager.OnClientStopped += Stopped;
            }
            if (_manager.IsListening) Started();
        }

        // NetworkManagerController may add its manager after this component's OnEnable.
        internal bool AttachManagerForStartup(NetworkManager manager)
        {
            if (manager == null || manager.gameObject != gameObject || manager.IsListening || _running) return false;
            if (_manager != null) return _manager == manager;
            _manager = manager;
            _manager.OnServerStarted += Started; _manager.OnClientStarted += Started;
            _manager.OnServerStopped += Stopped; _manager.OnClientStopped += Stopped;
            return true;
        }

        // Preserve the issuer while the SAME NGO session is alive. Events stay subscribed
        // while disabled so a real network shutdown/restart still retires the old session.
        private void OnDisable() => ReleaseFrames();

        private void OnDestroy()
        {
            if (_manager != null)
            {
                _manager.OnServerStarted -= Started; _manager.OnClientStarted -= Started;
                _manager.OnServerStopped -= Stopped; _manager.OnClientStopped -= Stopped;
            }
            EndRun(); _manager = null;
        }

        private void Started()
        {
            if (!_running) { _run = checked(_run + 1); _running = true; }
            if (_manager != null && _manager.IsServer && _serverSession == null) _serverSession = GlobalMotionSession.CreateRandom();
        }
        private void Stopped(bool wasHost) => EndRun();
        private void EndRun()
        {
            _running = false;
            ReleaseFrames(); _serverSession = null;
        }
        private void ReleaseFrames()
        {
            for (int i = _ordered.Count - 1; i >= 0; i--)
                if (i < _ordered.Count && _ordered[i] != null) _ordered[i].WorldEnded();
            _ordered.Clear(); _actors.Clear(); _frames.Clear();
        }

        public bool RegisterFrame(int id, LocalCoordinateFrame coordinates, PhysicsScene physics)
        {
            if (!IsRunning || id <= 0 || !coordinates.IsValid || !physics.IsValid() || _frames.ContainsKey(id)) return false;
            foreach (var f in _frames.Values) if (f.Physics.Equals(physics)) return false;
            _frames.Add(id, new GlobalMotionFrame(id, _run, coordinates, physics));
            return true;
        }

        public bool UnregisterFrame(int id)
        {
            if (!_frames.TryGetValue(id, out var frame)) return false;
            for (int i = 0; i < _ordered.Count; i++) if (_ordered[i] != null && ReferenceEquals(_ordered[i].Frame, frame)) return false;
            return _frames.Remove(id);
        }

        public bool TryGetFrame(int id, out GlobalMotionFrame frame) => _frames.TryGetValue(id, out frame) && IsCurrent(frame);
        public bool IsCurrent(GlobalMotionFrame frame) => IsRunning && frame != null && frame.RunGeneration == _run &&
            frame.Physics.IsValid() && _frames.TryGetValue(frame.Id, out var actual) && ReferenceEquals(actual, frame);

        internal bool RegisterActor(GlobalMotionPoseAdapter actor, ulong objectId)
        {
            if (!IsRunning || _actors.ContainsKey(objectId)) return false;
            _actors.Add(objectId, actor); _ordered.Add(actor); return true;
        }
        internal void UnregisterActor(GlobalMotionPoseAdapter actor, ulong objectId)
        {
            if (_actors.TryGetValue(objectId, out var current) && current == actor) _actors.Remove(objectId);
            _ordered.Remove(actor);
        }
        internal bool TryGetActor(ulong id, out GlobalMotionPoseAdapter actor) => _actors.TryGetValue(id, out actor) && actor != null;

        /// <summary>Explicit server activation from already-global data, not from an ambiguous legacy Transform.</summary>
        public bool StartWorldStream(GlobalMotionPoseAdapter actor, GlobalMotionAuthority authority, GlobalPosition globalPosition,
            Quaternion rotation, Vector3 scale, Func<GlobalMotionSnapshot, bool> gameRules = null)
        {
            if (!CanStart(actor) || !actor.Frame.Coordinates.TryToLocal(globalPosition, out _)) return false;
            var validator = WrapRules(actor, gameRules);
            if (!actor.Transport.ActivateWorldServer(_serverSession, authority, globalPosition, rotation, scale, validator, actor.CanPrepareControl)) return false;
            // Placement may succeed while native actor readiness is still awaiting explicit confirmation.
            if (actor.PrepareBaseline() || actor.IsBaselinePlaced) return true;
            actor.Transport.StopServer(); return false;
        }

        /// <summary>Explicit parent-local pose. Baseline application installs the hierarchy on every peer; caller must not SetParent first.</summary>
        public bool StartParentStream(GlobalMotionPoseAdapter actor, GlobalMotionAuthority authority, GlobalMotionPoseAdapter parent,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Func<GlobalMotionSnapshot, bool> gameRules = null)
        {
            if (!CanStart(actor) || parent == null || parent.World != this || !parent.IsBaselineReady ||
                !ReferenceEquals(actor.Frame, parent.Frame)) return false;
            var validator = WrapRules(actor, gameRules);
            if (!actor.Transport.ActivateParentLocalServer(_serverSession, authority, parent.Transport,
                localPosition, localRotation, localScale, validator, actor.CanPrepareControl)) return false;
            if (actor.PrepareBaseline() || actor.IsBaselinePlaced) return true;
            actor.Transport.StopServer(); return false;
        }

        /// <summary>Explicit handoff completion after game logic changed parent/owner and supplied fresh validation rules.</summary>
        public bool ReactivateFromCurrentPose(GlobalMotionPoseAdapter actor, GlobalMotionAuthority authority,
            Func<GlobalMotionSnapshot, bool> gameRules = null)
        {
            if (!CanStart(actor)) return false;
            var parentTransform = actor.transform.parent;
            var parentObject = parentTransform != null ? parentTransform.GetComponent<NetworkObject>() : null;
            if (parentObject != null)
            {
                if (!TryGetActor(parentObject.NetworkObjectId, out var parent)) return false;
                return StartParentStream(actor, authority, parent, actor.transform.localPosition,
                    actor.transform.localRotation, actor.transform.localScale, gameRules);
            }
            if (!actor.TryCaptureWorld(out var position, out var rotation, out var scale)) return false;
            return StartWorldStream(actor, authority, position, rotation, scale, gameRules);
        }

        private Func<GlobalMotionSnapshot, bool> WrapRules(GlobalMotionPoseAdapter actor, Func<GlobalMotionSnapshot, bool> gameRules)
        {
            if (gameRules == null) return null; // Remote ownership still requires game-specific authorization.
            return sample => actor.ValidateCandidate(sample) && gameRules(sample);
        }
        private bool CanStart(GlobalMotionPoseAdapter actor) => IsRunning && _manager.IsServer && _serverSession != null &&
            actor != null && actor.World == this && IsCurrent(actor.Frame) && actor.Transport != null && actor.Transport.IsSpawned;

        private void Update()
        {
            if (!IsRunning) return;
            for (int i = _ordered.Count - 1; i >= 0; i--)
                if (i < _ordered.Count && _ordered[i] != null) _ordered[i].PrepareBaseline();
        }
        private void FixedUpdate()
        {
            if (!IsRunning) return;
            for (int i = _ordered.Count - 1; i >= 0; i--)
                if (i < _ordered.Count && _ordered[i] != null) _ordered[i].ApplyFixedPose();
        }
    }
}
