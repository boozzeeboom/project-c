using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using ProjectC.Core;
using ProjectC.Player;
using ProjectC.UI;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06Y: dormant, user-controlled runtime evidence probe for the isolated global-player pilot.
    /// It records ordered lifecycle markers from movement, CharacterController, NGO motion control,
    /// physics synchronization and the independently spawned SpringArmCamera. It never mutates runtime state.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(32000)]
    public sealed class GlobalMotionRuntimeEvidenceProbe : MonoBehaviour
    {
        private struct TraceEvent
        {
            public int Sequence;
            public int Frame;
            public string Category;
            public string Phase;
            public string Payload;
        }

        [Header("T-FO06Y Runtime Evidence")]
        [SerializeField] private bool _captureEnabled = false;
        [SerializeField, Min(1)] private int _sampleEveryFrames = 5;
        [SerializeField, Min(8)] private int _maxEventsPerFrame = 128;

        private readonly List<TraceEvent> _events = new List<TraceEvent>(128);
        private NetworkPlayer _player;
        private CharacterController _controller;
        private Animator _animator;
        private GlobalMotionReplicator _replicator;
        private GlobalMotionPoseAdapter _adapter;
        private SpringArmCamera _camera;
        private int _sequence;
        private int _droppedEvents;
        private int _lastFlushedFrame = -1;

        public static GlobalMotionRuntimeEvidenceProbe Active { get; private set; }
        public bool CaptureEnabled => _captureEnabled;

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                enabled = false;
                return;
            }

            Active = this;
            CacheReferences();
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        private void LateUpdate()
        {
            if (!_captureEnabled) return;
            CacheReferences();
            if (_lastFlushedFrame == Time.frameCount) return;
            _lastFlushedFrame = Time.frameCount;
            FlushFrame();
        }

        public static void RecordEvent(string category, string phase, string payload = null)
        {
            Active?.Record(category, phase, payload);
        }

        private void Record(string category, string phase, string payload)
        {
            if (!_captureEnabled) return;
            int limit = Mathf.Max(8, _maxEventsPerFrame);
            if (_events.Count >= limit)
            {
                _droppedEvents++;
                return;
            }

            _events.Add(new TraceEvent
            {
                Sequence = ++_sequence,
                Frame = Time.frameCount,
                Category = category,
                Phase = phase,
                Payload = Sanitize(payload)
            });
        }

        private void FlushFrame()
        {
            bool sampleFrame = Time.frameCount % Mathf.Max(1, _sampleEveryFrames) == 0;
            if (!sampleFrame && _events.Count == 0 && _droppedEvents == 0) return;

            var builder = new StringBuilder(1024);
            builder.Append("[T-FO06Y] frame=").Append(Time.frameCount)
                .Append(" fixedTime=").Append(Time.fixedTime.ToString("F4"))
                .Append(" inFixed=").Append(Time.inFixedTimeStep)
                .Append(" events=");

            for (int i = 0; i < _events.Count; i++)
            {
                if (i > 0) builder.Append(';');
                var trace = _events[i];
                builder.Append(trace.Sequence).Append('@').Append(trace.Frame)
                    .Append(':').Append(trace.Category).Append('.').Append(trace.Phase);
                if (!string.IsNullOrEmpty(trace.Payload)) builder.Append('(').Append(trace.Payload).Append(')');
            }

            if (_droppedEvents > 0)
                builder.Append(";dropped=").Append(_droppedEvents);

            if (sampleFrame)
            {
                CacheReferences();
                AppendPlayerSample(builder);
                AppendCameraSample(builder);
                AppendMotionSample(builder);
            }

            Debug.Log(builder.ToString(), this);
            _events.Clear();
            _droppedEvents = 0;
        }

        private void AppendPlayerSample(StringBuilder builder)
        {
            if (_player == null)
            {
                builder.Append(" player=missing");
                return;
            }

            Vector3 velocity = _player.DiagnosticVelocity;
            Vector3 platformDelta = _player.DiagnosticPlatformDelta;
            builder.Append(" playerPos=").Append(_player.transform.position.ToString("F3"))
                .Append(" vel=").Append(velocity.ToString("F3"))
                .Append(" grounded=").Append(_player.DiagnosticIsGrounded)
                .Append(" ccGrounded=").Append(_controller != null && _controller.isGrounded)
                .Append(" ccEnabled=").Append(_player.DiagnosticControllerEnabled)
                .Append(" onPlatform=").Append(_player.DiagnosticOnPlatform)
                .Append(" platformDelta=").Append(platformDelta.ToString("F4"))
                .Append(" platform=").Append(_player.DiagnosticPlatformName)
                .Append(" inShip=").Append(_player.IsInShip);

            if (_animator == null)
            {
                builder.Append(" animator=missing");
                return;
            }

            builder.Append(" animatorEnabled=").Append(_animator.enabled)
                .Append(" rootMotion=").Append(_animator.applyRootMotion)
                .Append(" deltaPos=").Append(_animator.deltaPosition.ToString("F4"))
                .Append(" deltaRot=").Append(_animator.deltaRotation.eulerAngles.ToString("F2"));
        }

        private void AppendCameraSample(StringBuilder builder)
        {
            if (_camera == null)
            {
                builder.Append(" camera=missing");
                return;
            }

            Transform target = _camera.TargetTransform;
            builder.Append(" camera=").Append(_camera.name)
                .Append(" activeOwner=").Append(Billboard.ActiveCamera == _camera.transform)
                .Append(" target=").Append(target != null ? target.name : "<none>")
                .Append(" targetHash=").Append(target != null ? target.GetHashCode() : 0)
                .Append(" lagTarget=").Append(_camera.LagTargetPosition.ToString("F3"))
                .Append(" lagSpeed=").Append(_camera.LagSpeed.ToString("F3"))
                .Append(" colliding=").Append(_camera.WasColliding)
                .Append(" collisionPos=").Append(_camera.LastCollisionPosition.ToString("F3"))
                .Append(" cameraPos=").Append(_camera.transform.position.ToString("F3"));
        }

        private void AppendMotionSample(StringBuilder builder)
        {
            if (_replicator == null)
            {
                builder.Append(" motion=missing");
                return;
            }

            var control = _replicator.Control;
            if (!control.HasStream)
            {
                builder.Append(" motion=inactive");
                return;
            }

            var binding = control.Baseline.Binding;
            builder.Append(" motionActive=").Append(control.IsActive)
                .Append(" revision=").Append(control.Revision)
                .Append(" seq=").Append(control.Baseline.Sequence)
                .Append(" binding=").Append(binding.SessionId).Append('/').Append(binding.NetworkObjectId)
                .Append('/').Append(binding.SpawnGeneration).Append('/').Append(binding.AuthorityGeneration)
                .Append('/').Append(binding.DiscontinuityGeneration)
                .Append(" adapter=").Append(_adapter != null ? _adapter.Status.ToString() : "missing")
                .Append(" baselinePlaced=").Append(_adapter != null && _adapter.IsBaselinePlaced);
        }

        private void CacheReferences()
        {
            if (_player == null) _player = GetComponent<NetworkPlayer>();
            if (_player == null) return;
            if (_controller == null) _controller = _player.DiagnosticController;
            if (_animator == null) _animator = _player.DiagnosticAnimator;
            if (_replicator == null) _replicator = _player.GetComponent<GlobalMotionReplicator>();
            if (_adapter == null) _adapter = _player.GetComponent<GlobalMotionPoseAdapter>();
            if (_camera == null)
            {
                _camera = _player.DiagnosticCamera;
                if (_camera == null)
                {
                    var cameras = FindObjectsByType<SpringArmCamera>(FindObjectsInactive.Include);
                    foreach (var candidate in cameras)
                    {
                        if (candidate != null && candidate.TargetTransform == _player.transform)
                        {
                            _camera = candidate;
                            break;
                        }
                    }
                }
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace(";", ",").Replace("(", "[").Replace(")", "]");
        }
    }
}
