using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO04B: NGO control/motion transport, deliberately NOT a Transform writer.
    /// Dormant until Activate*Server after spawn/coordinate placement. The future pose
    /// adapter must acknowledge each new baseline before publishing and must replace NT.
    /// No prefab mutation, auto-bootstrap, movement simulation or origin shift occurs here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlobalMotionReplicator : NetworkBehaviour
    {
        private const double ReliableKeyframeInterval = 1d;
        private readonly GlobalMotionControlReceiver _receiver = new GlobalMotionControlReceiver();
        private readonly GlobalMotionAdmission _admission = new GlobalMotionAdmission();
        private GlobalMotionSession _serverSession;
        private GlobalMotionControl _serverControl;
        private GlobalMotionControl _pendingControl;
        private bool _hasPendingControl;
        private bool _baselineApplied;
        private uint _publishSequence;
        private bool _hasPublishedInBinding;
        private int _lastPublishTick;
        private bool _hasPublishedTick;
        private double _nextKeyframeTime;
        private NetworkTickSystem _ticks;
        private Func<GlobalMotionSnapshot, bool> _ownerPoseValidator;

        public GlobalMotionControl Control => _receiver.Control;
        public MotionAdmissionReject LastAdmissionRejection { get; private set; }
        public bool CanPublish
        {
            get
            {
                var c = Control;
                if (!IsSpawned || !isActiveAndEnabled || NetworkManager == null || !NetworkManager.IsListening ||
                    !c.HasStream || !c.IsActive || !_baselineApplied || HasCompetingWriter()) return false;
                if (c.Authority == GlobalMotionAuthority.Server) return IsServer;
                return c.PublisherClientId == NetworkManager.LocalClientId && OwnerClientId == c.PublisherClientId;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _ticks = NetworkManager.NetworkTickSystem;
            if (_ticks != null) _ticks.Tick += OnNetworkTick;
            if (!IsServer && _hasPendingControl)
            {
                ApplyReceivedControl(_pendingControl, Unity.Netcode.NetworkManager.ServerClientId);
                _hasPendingControl = false;
                _pendingControl = default;
            }
        }

        protected override void OnNetworkPostSpawn()
        {
            base.OnNetworkPostSpawn();
            GlobalMotionPlayerBootstrap.NotifyPostSpawn(this);
        }

        protected override void OnSynchronize<T>(ref BufferSerializer<T> serializer)
        {
            var state = serializer.IsWriter ? _serverControl : default;
            state.NetworkSerialize(serializer);
            if (serializer.IsReader)
            {
                // Object id, ownership and IsSpawned may not yet be initialized on receive.
                if (IsSpawned) ApplyReceivedControl(state, Unity.Netcode.NetworkManager.ServerClientId);
                else if (!_hasPendingControl || (state.HasStream &&
                    (!_pendingControl.HasStream || state.Revision > _pendingControl.Revision)))
                {
                    _pendingControl = state;
                    _hasPendingControl = true;
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            GlobalMotionPlayerBootstrap.NotifyDespawn(this);
            Unsubscribe();
            _receiver.Reset(); _admission.Stop();
            _serverSession = null; _ownerPoseValidator = null;
            _serverControl = default; _pendingControl = default; _hasPendingControl = false;
            _baselineApplied = false; _publishSequence = 0; _hasPublishedTick = false; _hasPublishedInBinding = false;
            LastAdmissionRejection = MotionAdmissionReject.None;
            base.OnNetworkDespawn();
        }

        public override void OnDestroy() { Unsubscribe(); base.OnDestroy(); }

        public bool ActivateWorldServer(GlobalMotionSession session, GlobalMotionAuthority authority,
            GlobalPosition position, Quaternion rotation, Vector3 scale, Func<GlobalMotionSnapshot, bool> ownerPoseValidator = null,
            Func<GlobalMotionControl, bool> baselinePreflight = null)
        {
            // Custom detach must be planned before publishing; the transport never changes Transform.parent.
            bool networkParented = transform.parent != null && transform.parent.GetComponentInParent<NetworkObject>(true) != null;
            if (networkParented && (!CustomHierarchyConfigured || baselinePreflight == null)) return false;
            return ActivateServer(session, authority, MotionCoordinateSpace.World, position, Vector3.zero,
                rotation, scale, 0, 0, ownerPoseValidator, baselinePreflight);
        }

        public bool ActivateParentLocalServer(GlobalMotionSession session, GlobalMotionAuthority authority,
            GlobalMotionReplicator parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale,
            Func<GlobalMotionSnapshot, bool> ownerPoseValidator = null, Func<GlobalMotionControl, bool> baselinePreflight = null)
        {
            if (parent == null || parent == this || !parent.IsSpawned || !parent.IsServer ||
                parent.NetworkManager != NetworkManager || !parent.Control.HasStream || !parent.Control.IsActive ||
                !ReferenceEquals(parent._serverSession, session)) return false;
            if (transform.parent != parent.transform && (!CustomHierarchyConfigured || !parent.CustomHierarchyConfigured || baselinePreflight == null)) return false;
            return ActivateServer(session, authority, MotionCoordinateSpace.ParentLocal, GlobalPosition.Zero, localPosition,
                localRotation, localScale, parent.NetworkObjectId, parent.Control.Baseline.Binding.SpawnGeneration, ownerPoseValidator, baselinePreflight);
        }

        private bool ActivateServer(GlobalMotionSession session, GlobalMotionAuthority authority, MotionCoordinateSpace space,
            GlobalPosition world, Vector3 local, Quaternion rotation, Vector3 scale, ulong parentId, ulong parentLifetime,
            Func<GlobalMotionSnapshot, bool> validator, Func<GlobalMotionControl, bool> baselinePreflight)
        {
            if (!IsServer || !IsSpawned || !isActiveAndEnabled || session == null || HasCompetingWriter() ||
                (authority != GlobalMotionAuthority.Server && authority != GlobalMotionAuthority.Owner)) return false;
            if (_serverSession != null && !ReferenceEquals(_serverSession, session)) return false;
            ulong publisher = authority == GlobalMotionAuthority.Server ? Unity.Netcode.NetworkManager.ServerClientId : OwnerClientId;
            if (publisher != Unity.Netcode.NetworkManager.ServerClientId && validator == null) return false;
            if (!world.IsFinite || !GlobalMotionSnapshot.Finite(local) || !GlobalMotionSnapshot.UnitRotation(rotation) ||
                !GlobalMotionSnapshot.Finite(scale)) return false;

            MotionStreamBinding binding = _serverControl.HasStream
                ? _serverControl.Baseline.Binding : session.Allocate(NetworkObjectId);
            if (_serverControl.HasStream && (_serverControl.Authority != authority || _serverControl.PublisherClientId != publisher))
                binding = session.NextAuthority(binding);
            binding = session.NextDiscontinuity(binding, space, parentId, parentLifetime);
            var baseline = space == MotionCoordinateSpace.World
                ? GlobalMotionSnapshot.CreateWorld(binding, 0, ServerNow, world, rotation, scale)
                : GlobalMotionSnapshot.CreateParentLocal(binding, 0, ServerNow, local, rotation, scale);
            if (baselinePreflight != null)
            {
                ulong revision = _serverControl.Revision;
                ulong owner = OwnerClientId;
                var candidate = new GlobalMotionControl { HasStream = true, IsActive = true, Revision = checked(revision + 1),
                    Authority = authority, PublisherClientId = publisher, Baseline = baseline };
                try { if (!baselinePreflight(candidate)) return false; }
                catch (Exception e) { Debug.LogException(e, this); return false; }
                if (!IsSpawned || !IsServer || !isActiveAndEnabled || HasCompetingWriter() || !CustomHierarchyConfigured ||
                    NetworkManager == null || !NetworkManager.IsListening || _serverControl.Revision != revision || OwnerClientId != owner ||
                    (_serverSession != null && !ReferenceEquals(_serverSession, session))) return false;
            }
            InstallServerControl(session, validator, authority, publisher, baseline);
            return true;
        }

        /// <summary>The local pose adapter calls this only after applying this exact baseline/frame.</summary>
        public bool AcknowledgeBaselineApplied(MotionStreamBinding binding)
        {
            if (!IsSpawned || !Control.IsActive || binding != Control.Baseline.Binding || HasCompetingWriter()) return false;
            if (!_baselineApplied)
            {
                _publishSequence = GlobalMotionApplication.ResumeSequence(_hasPublishedInBinding, _publishSequence, Control.Baseline.Sequence);
                _baselineApplied = true;
            }
            return true;
        }

        /// <summary>Local readiness gate only. Does not rewind sequence or change the server binding.</summary>
        public void RevokeBaselineAcknowledgement() => _baselineApplied = false;

        public bool PublishWorld(GlobalPosition position, Quaternion rotation, Vector3 scale)
        {
            if (!CanPublish || Control.Baseline.Binding.Space != MotionCoordinateSpace.World || !position.IsFinite ||
                !GlobalMotionSnapshot.UnitRotation(rotation) || !GlobalMotionSnapshot.Finite(scale)) return false;
            var sample = GlobalMotionSnapshot.CreateWorld(Control.Baseline.Binding, unchecked(_publishSequence + 1),
                ServerNow, position, rotation, scale);
            return Publish(sample);
        }

        public bool PublishParentLocal(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!CanPublish || Control.Baseline.Binding.Space != MotionCoordinateSpace.ParentLocal ||
                !GlobalMotionSnapshot.Finite(position) || !GlobalMotionSnapshot.UnitRotation(rotation) || !GlobalMotionSnapshot.Finite(scale)) return false;
            var sample = GlobalMotionSnapshot.CreateParentLocal(Control.Baseline.Binding, unchecked(_publishSequence + 1),
                ServerNow, position, rotation, scale);
            return Publish(sample);
        }

        private bool Publish(GlobalMotionSnapshot sample)
        {
            int tick = NetworkManager.ServerTime.Tick;
            if (_hasPublishedTick && tick == _lastPublishTick) return false;
            if (IsServer)
            {
                if (!AcceptServerMotion(Unity.Netcode.NetworkManager.ServerClientId, sample)) return false;
            }
            else SubmitOwnerMotionRpc(sample);
            _publishSequence = sample.Sequence;
            _hasPublishedInBinding = true;
            _lastPublishTick = tick; _hasPublishedTick = true;
            return true; // Client: submitted, not an acknowledgement of server acceptance.
        }

        public bool TrySampleForDisplay(double renderTime, out GlobalMotionPose pose)
        {
            pose = default;
            return IsSpawned && isActiveAndEnabled && !HasCompetingWriter() && _receiver.TrySample(renderTime, out pose);
        }

        public void StopServer()
        {
            if (!IsServer || !IsSpawned || !_serverControl.HasStream || !_serverControl.IsActive) return;
            _serverControl.IsActive = false;
            _serverControl.Revision = checked(_serverControl.Revision + 1);
            _admission.Stop();
            ApplyReceivedControl(_serverControl, Unity.Netcode.NetworkManager.ServerClientId);
            ReceiveControlRpc(_serverControl);
        }

        protected override void OnOwnershipChanged(ulong previous, ulong current)
        {
            base.OnOwnershipChanged(previous, current);
            if (!IsServer || !_serverControl.HasStream || !_serverControl.IsActive || _serverControl.Authority != GlobalMotionAuthority.Owner) return;
            var baseline = _serverControl.Baseline;
            baseline.Binding = _serverSession.NextAuthority(baseline.Binding);
            baseline.Sequence = 0; baseline.SampleTime = ServerNow;
            // The previous validator may close over the previous owner. Never silently reuse it.
            _ownerPoseValidator = null;
            _serverControl.PublisherClientId = current;
            _serverControl.Baseline = baseline;
            StopServer(); // Server coordinator must reactivate with fresh rules and pose acknowledgement.
        }

        public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
        {
            base.OnNetworkObjectParentChanged(parentNetworkObject);
            // Never reinterpret old coordinates after a hierarchy change. The coordinator reactivates explicitly.
            if (IsServer && _serverControl.IsActive) StopServer();
        }

        private void InstallServerControl(GlobalMotionSession session, Func<GlobalMotionSnapshot, bool> validator,
            GlobalMotionAuthority authority, ulong publisher, GlobalMotionSnapshot baseline)
        {
            var control = new GlobalMotionControl
            {
                HasStream = true, IsActive = true, Revision = checked(_serverControl.Revision + 1),
                Authority = authority, PublisherClientId = publisher, Baseline = baseline
            };
            _admission.Begin(publisher, baseline, ServerNow);
            _serverSession = session;
            _ownerPoseValidator = validator;
            _serverControl = control;
            ApplyReceivedControl(control, Unity.Netcode.NetworkManager.ServerClientId);
            ReceiveControlRpc(control);
            _nextKeyframeTime = ServerNow + ReliableKeyframeInterval;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone, Delivery = RpcDelivery.Unreliable)]
        private void SubmitOwnerMotionRpc(GlobalMotionSnapshot sample, RpcParams rpcParams = default)
        {
            if (!IsServer || !IsSpawned || !_serverControl.IsActive || _serverControl.Authority != GlobalMotionAuthority.Owner ||
                rpcParams.Receive.SenderClientId != OwnerClientId || _serverControl.PublisherClientId != OwnerClientId) return;
            AcceptServerMotion(rpcParams.Receive.SenderClientId, sample);
        }

        private bool AcceptServerMotion(ulong sender, GlobalMotionSnapshot sample)
        {
            if (!IsServer || !IsSpawned || !_serverControl.IsActive || HasCompetingWriter()) return false;
            if (_serverControl.Authority == GlobalMotionAuthority.Owner && _serverControl.PublisherClientId != OwnerClientId) return false;
            bool remote = sender != Unity.Netcode.NetworkManager.ServerClientId;
            if (remote && _ownerPoseValidator == null) return false;
            GlobalMotionSnapshot accepted;
            MotionAdmissionReject rejection;
            try
            {
                if (!_admission.TryAccept(sender, sample, ServerNow, remote ? _ownerPoseValidator : null, out accepted, out rejection))
                { LastAdmissionRejection = rejection; return false; }
            }
            catch (Exception e)
            {
                // A failing game validator must not allow unchecked publication.
                StopServer(); Debug.LogException(e, this); return false;
            }
            LastAdmissionRejection = MotionAdmissionReject.None;
            _serverControl.Baseline = accepted;
            _receiver.TryAdd(accepted, Unity.Netcode.NetworkManager.ServerClientId, Unity.Netcode.NetworkManager.ServerClientId);
            ReceiveMotionRpc(accepted);
            return true;
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server, Delivery = RpcDelivery.Reliable)]
        private void ReceiveControlRpc(GlobalMotionControl control, RpcParams rpcParams = default)
        {
            if (IsServer || !IsSpawned) return; // Server already applied it once, including Host.
            ApplyReceivedControl(control, rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server, Delivery = RpcDelivery.Unreliable)]
        private void ReceiveMotionRpc(GlobalMotionSnapshot sample, RpcParams rpcParams = default)
        {
            if (IsServer || !IsSpawned) return;
            _receiver.TryAdd(sample, rpcParams.Receive.SenderClientId, Unity.Netcode.NetworkManager.ServerClientId);
        }

        private void ApplyReceivedControl(GlobalMotionControl control, ulong sender)
        {
            var previous = Control;
            if (!_receiver.TryApply(control, sender, Unity.Netcode.NetworkManager.ServerClientId, NetworkObjectId)) return;
            if (!control.IsActive || !previous.HasStream || previous.Baseline.Binding != control.Baseline.Binding)
            { _baselineApplied = false; _publishSequence = 0; _hasPublishedTick = false; _hasPublishedInBinding = false; }
        }

        private void OnNetworkTick()
        {
            if (IsServer && IsSpawned && isActiveAndEnabled && _serverControl.IsActive)
                GlobalMotionPlayerBootstrap.RefreshSpawnSeed(this);
            if (!IsServer || !IsSpawned || !isActiveAndEnabled || !_serverControl.IsActive || ServerNow < _nextKeyframeTime) return;
            if (HasCompetingWriter()) { StopServer(); return; }
            _serverControl.Revision = checked(_serverControl.Revision + 1);
            ApplyReceivedControl(_serverControl, Unity.Netcode.NetworkManager.ServerClientId);
            ReceiveControlRpc(_serverControl); // Reliable recovery of a lost final unreliable update (even at rest).
            _nextKeyframeTime = ServerNow + ReliableKeyframeInterval;
        }

        private bool CustomHierarchyConfigured => NetworkObject != null && !NetworkObject.AutoObjectParentSync &&
            !NetworkObject.SynchronizeTransform && GlobalMotionNetworkStartup.IsInstalled(NetworkManager);
        private double ServerNow => Math.Max(0d, NetworkManager.ServerTime.Time);
        private bool HasCompetingWriter() { var nt = GetComponent<NetworkTransform>(); return nt != null && nt.enabled; }
        private void Unsubscribe() { if (_ticks != null) _ticks.Tick -= OnNetworkTick; _ticks = null; }
    }
}
