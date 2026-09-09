using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ProjectC.World.FloatingOrigin.Network
{
    public enum MotionAdapterStatus { Unbound, WaitingForControl, WaitingForParent, InvalidFrame, DriverBlocked, OutOfRange, Ready, Faulted, WaitingForActors }

    /// <summary>
    /// Explicitly bound Unity pose adapter. Does not register itself, disable gameplay scripts,
    /// change origins, set parents, toggle isKinematic or replace NetworkTransform.
    /// Existing movement/root-motion systems must use IsReadyForSimulation in the later actor integration.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    public sealed class GlobalMotionPoseAdapter : NetworkBehaviour
    {
        [SerializeField, Tooltip("Opt-in for coordinated prefab migration only. Keep false in the current legacy game.")]
        private bool _coordinatesRequired = false;
        public bool CoordinatesRequired => _coordinatesRequired;
        private bool _preparingBaseline;
        private bool _checkingParticipants;
        private readonly List<MonoBehaviour> _rootScripts = new List<MonoBehaviour>();
        private readonly List<IGlobalMotionActorParticipant> _participants = new List<IGlobalMotionActorParticipant>();
        private GlobalMotionReplicator _transport;
        private Rigidbody _body;
        private CharacterController _controller;
        private NavMeshAgent _agent;
        private ulong _objectId;
        private MotionStreamBinding _appliedBinding;
        private bool _hasApplied;
        private bool _faulted;
        private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
        private readonly List<Joint> _joints = new List<Joint>();
        public GlobalMotionWorld World { get; private set; }
        public GlobalMotionFrame Frame { get; private set; }
        public GlobalMotionReplicator Transport => _transport;
        public MotionAdapterStatus Status { get; private set; } = MotionAdapterStatus.Unbound;
        public double InterpolationDelay { get; set; } = 0.1d;
        public bool IsBaselinePlaced => !_preparingBaseline && !_faulted && _hasApplied && ContextValid() &&
            !HasCompetingWriter() && _transport.Control.IsActive && _transport.Control.Baseline.Binding == _appliedBinding &&
            Frame.Coordinates.ContainsLocal(CurrentPosition) && HierarchyReady();
        public bool IsBaselineReady => Status == MotionAdapterStatus.Ready && IsBaselinePlaced &&
            ResolveRole(out var role) && ParticipantsReady(role);
        public bool IsReadyForSimulation => IsBaselineReady && ResolveRole(out var role) && role == MotionPoseRole.Authority;

        public bool Bind(GlobalMotionWorld world, int frameId)
        {
            if (!CoordinatesRequired || World != null || !isActiveAndEnabled || world == null || !world.TryGetFrame(frameId, out var frame)) return false;
            _transport = GetComponent<GlobalMotionReplicator>();
            if (_transport == null || !_transport.IsSpawned || _transport.NetworkManager != world.Manager ||
                _transport.NetworkObject == null || _transport.NetworkObject.gameObject != gameObject || HasCompetingWriter() ||
                !gameObject.scene.GetPhysicsScene().Equals(frame.Physics)) return false;
            _body = GetComponent<Rigidbody>(); _controller = GetComponent<CharacterController>(); _agent = GetComponent<NavMeshAgent>();
            if (!SupportedStructure() || !CollectParticipants()) return false;
            _objectId = _transport.NetworkObjectId;
            if (!world.RegisterActor(this, _objectId)) return false;
            World = world; Frame = frame; _hasApplied = false; _faulted = false;
            Status = MotionAdapterStatus.WaitingForControl;
            _transport.RevokeBaselineAcknowledgement();
            return true;
        }

        public void Unbind()
        {
            var world = World;
            if (world == null) return;
            WorldEnded();
            world.UnregisterActor(this, _objectId);
        }
        public override void OnNetworkDespawn()
        {
            // Pool despawn+respawn can happen before any Update observes IsSpawned=false.
            var world = World;
            if (_transport != null) _transport.RevokeBaselineAcknowledgement();
            World = null; Frame = null; _hasApplied = false; Status = MotionAdapterStatus.Unbound;
            if (world != null) world.UnregisterActor(this, _objectId);
            base.OnNetworkDespawn();
        }
        private void OnDisable() => Unbind();
        internal void WorldEnded()
        {
            if (_transport != null)
            {
                _transport.RevokeBaselineAcknowledgement();
                if (_transport.IsSpawned && _transport.IsServer && _transport.NetworkManager.IsListening) _transport.StopServer();
            }
            World = null; Frame = null; _hasApplied = false; Status = MotionAdapterStatus.Unbound;
        }

        public bool PrepareBaseline()
        {
            if (_preparingBaseline) return false;
            if (!ContextValid()) return Block(MotionAdapterStatus.InvalidFrame);
            if (!ResolveRole(out var role)) return Block(MotionAdapterStatus.WaitingForControl);
            if (!SupportedStructure()) return Block(MotionAdapterStatus.DriverBlocked);
            var snapshot = _transport.Control.Baseline;
            var frame = Frame;
            if (_hasApplied && _appliedBinding == snapshot.Binding)
            {
                if (role == MotionPoseRole.Authority && !Frame.Coordinates.ContainsLocal(CurrentPosition)) return Block(MotionAdapterStatus.OutOfRange);
                if (role != MotionPoseRole.Authority && ((_body != null && !_body.isKinematic) ||
                    (_agent != null && _agent.enabled && (_agent.updatePosition || _agent.updateRotation)))) return Block(MotionAdapterStatus.DriverBlocked);
                if (!TryPlan(new GlobalMotionPose(snapshot), out _)) return false;
            }
            else
            {
                if (!TryPlan(new GlobalMotionPose(snapshot), out var plan)) return false;
                _transport.RevokeBaselineAcknowledgement();
                _preparingBaseline = true;
                try
                {
                    foreach (var actor in _participants)
                        if (!ParticipantAlive(actor) || !actor.CanApplyGlobalBaseline(role)) return Block(MotionAdapterStatus.WaitingForActors);
                    if (!ContextValid() || !ReferenceEquals(frame, Frame) || _transport.Control.Baseline.Binding != snapshot.Binding) return Block(MotionAdapterStatus.WaitingForControl);
                    if (!WritePose(plan, role, true)) return false;
                    foreach (var actor in _participants)
                    {
                        if (!ParticipantAlive(actor)) throw new InvalidOperationException("A required baseline participant was destroyed.");
                        actor.OnGlobalBaselineApplied(snapshot.Binding);
                        if (!ContextValid() || !ReferenceEquals(frame, Frame) || _transport.Control.Baseline.Binding != snapshot.Binding)
                            return Block(MotionAdapterStatus.WaitingForControl);
                    }
                    _appliedBinding = snapshot.Binding; _hasApplied = true;
                }
                catch (Exception e) { return FaultActor(e); }
                finally { _preparingBaseline = false; }
            }
            if (!ParticipantsReady(role)) return Block(MotionAdapterStatus.WaitingForActors);
            if (!_transport.AcknowledgeBaselineApplied(snapshot.Binding)) return Block(MotionAdapterStatus.DriverBlocked);
            Status = MotionAdapterStatus.Ready; return true;
        }

        private bool CollectParticipants()
        {
            _participants.Clear(); _rootScripts.Clear(); GetComponents(_rootScripts);
            foreach (var script in _rootScripts)
            {
                if (script == null) return false;
                if (script is IGlobalMotionActorParticipant participant) _participants.Add(participant);
            }
            return true;
        }
        private static bool ParticipantAlive(IGlobalMotionActorParticipant actor) => actor != null &&
            (!(actor is UnityEngine.Object obj) || obj != null);
        private bool ParticipantsReady(MotionPoseRole role)
        {
            if (_checkingParticipants || _preparingBaseline || _faulted) return false;
            _checkingParticipants = true;
            try
            {
                foreach (var actor in _participants)
                    if (!ParticipantAlive(actor) || !actor.IsGlobalMotionReady(role)) return false;
                return true;
            }
            catch (Exception e) { return FaultActor(e); }
            finally { _checkingParticipants = false; }
        }
        private bool FaultActor(Exception e)
        {
            _faulted = true; Status = MotionAdapterStatus.Faulted;
            if (_transport != null)
            {
                _transport.RevokeBaselineAcknowledgement();
                if (_transport.IsServer && _transport.IsSpawned && _transport.NetworkManager.IsListening) _transport.StopServer();
            }
            Debug.LogException(e, this); return false;
        }

        internal void ApplyFixedPose()
        {
            if (!PrepareBaseline() || !ResolveRole(out var role) || role == MotionPoseRole.Authority) return;
            if (role != MotionPoseRole.ServerReplica && _body == null) return;
            double time = role == MotionPoseRole.ServerReplica ? _transport.Control.Baseline.SampleTime : RenderTime;
            if (!_transport.TrySampleForDisplay(time, out var pose) || !TryPlan(pose, out var plan)) return;
            WritePose(plan, role, false);
        }

        private void LateUpdate()
        {
            if (World == null) return;
            if (_transport == null || !_transport.IsSpawned) { Unbind(); return; }
            if (!PrepareBaseline() || !ResolveRole(out var role)) return;
            if (role == MotionPoseRole.Authority)
            {
                if (_transport.Control.Baseline.Binding.Space == MotionCoordinateSpace.World)
                {
                    if (!TryCaptureWorld(out var position, out var rotation, out var scale)) { Block(MotionAdapterStatus.OutOfRange); return; }
                    _transport.PublishWorld(position, rotation, scale);
                }
                else
                {
                    if (!TryParent(_transport.Control.Baseline.Binding, out _) || !Frame.Coordinates.ContainsLocal(transform.localPosition))
                    { Block(MotionAdapterStatus.WaitingForParent); return; }
                    _transport.PublishParentLocal(transform.localPosition, transform.localRotation, transform.localScale);
                }
            }
            else if (role == MotionPoseRole.ClientReplica && _body == null)
            {
                if (_transport.TrySampleForDisplay(RenderTime, out var pose) && TryPlan(pose, out var plan)) WritePose(plan, role, false);
            }
        }

        public bool TryCaptureWorld(out GlobalPosition position, out Quaternion rotation, out Vector3 scale)
        {
            position = default; rotation = default; scale = default;
            if (!ContextValid()) return false;
            Vector3 local = _body != null ? _body.position : transform.position;
            if (!Frame.Coordinates.ContainsLocal(local)) return false;
            position = Frame.Coordinates.ToGlobal(local);
            rotation = _body != null ? _body.rotation : transform.rotation;
            scale = transform.localScale;
            return GlobalMotionSnapshot.UnitRotation(rotation) && GlobalMotionSnapshot.Finite(scale);
        }

        public bool ValidateCandidate(GlobalMotionSnapshot snapshot)
        {
            return IsBaselineReady && snapshot.TryValidate(out _) && snapshot.Binding == _transport.Control.Baseline.Binding &&
                TryPlan(new GlobalMotionPose(snapshot), out _, false);
        }

        private bool TryPlan(GlobalMotionPose pose, out MotionUnityPose plan, bool reportFailure = true)
        {
            plan = default;
            if (!ContextValid()) return PlanningFailure(MotionAdapterStatus.InvalidFrame, reportFailure);
            if (pose.Binding.Space == MotionCoordinateSpace.World)
            {
                if (transform.parent != null && transform.parent.GetComponent<NetworkObject>() != null)
                    return PlanningFailure(MotionAdapterStatus.WaitingForParent, reportFailure);
                if (GlobalMotionApplication.TryWorldPlan(pose, Frame.Coordinates, out plan)) return true;
                return PlanningFailure(MotionAdapterStatus.OutOfRange, reportFailure);
            }
            if (!TryParent(pose.Binding, out var parent)) return PlanningFailure(MotionAdapterStatus.WaitingForParent, reportFailure);
            if (!ResolveRole(out var role) || !GlobalMotionApplication.CanUseParentTransform(role,
                parent._body != null && parent._body.interpolation != RigidbodyInterpolation.None))
                return PlanningFailure(MotionAdapterStatus.DriverBlocked, reportFailure);
            if (GlobalMotionApplication.TryParentPlan(pose, Frame.Coordinates, parent.Transport.Control.Baseline.Binding,
                parent.transform.localToWorldMatrix, parent.transform.rotation, out plan)) return true;
            return PlanningFailure(MotionAdapterStatus.OutOfRange, reportFailure);
        }

        private bool PlanningFailure(MotionAdapterStatus status, bool reportFailure)
        {
            if (reportFailure) Block(status);
            return false;
        }

        private bool TryParent(MotionStreamBinding binding, out GlobalMotionPoseAdapter parent)
        {
            parent = null;
            if (World == null || !World.TryGetActor(binding.ParentNetworkObjectId, out parent) || parent == this ||
                !ReferenceEquals(parent.Frame, Frame) || transform.parent != parent.transform || !parent.IsBaselineReady) return false;
            var p = parent.Transport.Control.Baseline.Binding;
            return p.SessionId == binding.SessionId && p.NetworkObjectId == binding.ParentNetworkObjectId && p.SpawnGeneration == binding.ParentSpawnGeneration;
        }

        private bool HierarchyReady()
        {
            if (_appliedBinding.Space == MotionCoordinateSpace.ParentLocal) return TryParent(_appliedBinding, out _);
            return transform.parent == null || transform.parent.GetComponent<NetworkObject>() == null;
        }

        private bool WritePose(MotionUnityPose plan, MotionPoseRole role, bool baseline)
        {
            bool navWrites = _agent != null && _agent.enabled && (_agent.updatePosition || _agent.updateRotation);
            bool matches = (CurrentPosition - plan.Position).sqrMagnitude <= 1e-10f &&
                Quaternion.Angle(CurrentRotation, plan.Rotation) <= 0.001f && transform.localScale.Equals(plan.Scale);
            if (!plan.IsValid || HasCompetingWriter() || !GlobalMotionApplication.CanWrite(role, baseline, _body != null,
                _body == null || _body.isKinematic, navWrites, matches)) return Block(MotionAdapterStatus.DriverBlocked);
            if ((_body != null || _controller != null) && (plan.Scale.x <= 0f || plan.Scale.y <= 0f || plan.Scale.z <= 0f)) return Block(MotionAdapterStatus.DriverBlocked);
            if (_body != null && plan.Binding.Space == MotionCoordinateSpace.ParentLocal) return Block(MotionAdapterStatus.DriverBlocked);
            if (navWrites) return true; // Already-matching authoritative baseline: do not touch a live NavMeshAgent.
            bool controllerEnabled = _controller != null && _controller.enabled;
            var interpolation = _body != null ? _body.interpolation : RigidbodyInterpolation.None;
            bool sleeping = _body != null && _body.IsSleeping();
            try
            {
                if (_controller != null && controllerEnabled) _controller.enabled = false;
                if (_body != null)
                {
                    if (baseline)
                    {
                        _body.interpolation = RigidbodyInterpolation.None;
                        _body.position = plan.Position; _body.rotation = plan.Rotation;
                        transform.SetPositionAndRotation(plan.Position, plan.Rotation);
                    }
                    else { _body.MovePosition(plan.Position); _body.MoveRotation(plan.Rotation); }
                }
                else if (plan.Binding.Space == MotionCoordinateSpace.ParentLocal)
                { transform.localPosition = plan.ParentLocalPosition; transform.localRotation = plan.ParentLocalRotation; }
                else transform.SetPositionAndRotation(plan.Position, plan.Rotation);
                if (!transform.localScale.Equals(plan.Scale)) transform.localScale = plan.Scale;
                if (baseline || _body == null) Physics.SyncTransforms();
                return true;
            }
            catch (Exception e)
            {
                _faulted = true; Status = MotionAdapterStatus.Faulted;
                _transport.RevokeBaselineAcknowledgement();
                if (_transport.IsServer && _transport.IsSpawned) _transport.StopServer();
                Debug.LogException(e, this); return false;
            }
            finally
            {
                if (_controller != null) _controller.enabled = controllerEnabled;
                if (_body != null && baseline) { _body.interpolation = interpolation; if (sleeping) _body.Sleep(); }
            }
        }

        private bool SupportedStructure()
        {
            _body = GetComponent<Rigidbody>(); _controller = GetComponent<CharacterController>(); _agent = GetComponent<NavMeshAgent>();
            _bodies.Clear(); _joints.Clear();
            GetComponentsInChildren(true, _bodies); GetComponentsInChildren(true, _joints);
            if (_joints.Count != 0 || _bodies.Count > (_body != null ? 1 : 0) || (_body != null && _controller != null)) return false;
            if (_body != null && transform.parent != null && transform.parent.GetComponentInParent<Rigidbody>() != null) return false;
            return true;
        }
        private bool ContextValid() => CoordinatesRequired && !_faulted && isActiveAndEnabled && World != null && World.IsCurrent(Frame) &&
            _transport != null && _transport.IsSpawned && _transport.NetworkObjectId == _objectId &&
            _transport.NetworkManager == World.Manager && gameObject.scene.GetPhysicsScene().Equals(Frame.Physics);
        private bool ResolveRole(out MotionPoseRole role)
        {
            role = MotionPoseRole.Unavailable;
            return _transport != null && GlobalMotionApplication.TryResolveRole(_transport.Control, _transport.IsServer,
                _transport.NetworkManager.LocalClientId, _transport.OwnerClientId, out role);
        }
        private bool Block(MotionAdapterStatus status)
        {
            if (!_faulted) Status = status;
            if (_transport != null) _transport.RevokeBaselineAcknowledgement();
            return false;
        }
        private bool HasCompetingWriter() { var nt = GetComponent<NetworkTransform>(); return nt != null && nt.enabled; }
        private Vector3 CurrentPosition => _body != null ? _body.position : transform.position;
        private Quaternion CurrentRotation => _body != null ? _body.rotation : transform.rotation;
        private double RenderTime => _transport.NetworkManager.ServerTime.Time -
            (GlobalPosition.IsFiniteValue(InterpolationDelay) ? Math.Max(0d, Math.Min(0.5d, InterpolationDelay)) : 0.1d);
    }
}
