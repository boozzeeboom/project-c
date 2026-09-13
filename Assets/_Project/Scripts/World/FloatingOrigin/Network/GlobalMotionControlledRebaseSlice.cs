using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectC.World.FloatingOrigin.Network
{
    /// <summary>
    /// T-FO06CY: first concrete, user-controlled floating-origin vertical slice.
    /// It moves only the explicitly loaded WorldScene_0_0 roots and the local player when
    /// the player is not already contained by one of those roots. It never performs discovery
    /// outside the configured scene and never starts automatically.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(31000)]
    public sealed class GlobalMotionControlledRebaseSlice : MonoBehaviour
    {
        [Header("Explicit scope")]
        [SerializeField] private string _worldScenePath = "Assets/_Project/Scenes/World/WorldScene_0_0.unity";
        [SerializeField] private bool _includeAllWorldSceneRoots = true;
        [SerializeField] private Transform _additionalParticipantRoot;

        [Header("Frame policy")]
        [SerializeField] private double _initialOriginX;
        [SerializeField] private double _initialOriginY;
        [SerializeField] private double _initialOriginZ;
        [SerializeField, Min(1024f)] private float _maxLocalCoordinate = 100000f;
        [SerializeField, Min(1f)] private float _threshold = 256f;
        [SerializeField, Min(1f)] private float _quantum = 256f;

        [Header("User-controlled triggers")]
        [SerializeField] private KeyCode _successKey = KeyCode.F8;
        [SerializeField] private KeyCode _rollbackKey = KeyCode.F9;

        private readonly List<SceneParticipant> _participants = new List<SceneParticipant>();
        private LocalCoordinateFrame _frame;
        private ulong _frameGeneration;
        private bool _initialized;
        private bool _transactionActive;
        private bool _rollbackNextValidation;
        private CharacterController _frozenController;
        private bool _controllerWasEnabled;
        // T-FO06DB: корень игрока активной транзакции — для сдвига deathY при rollback.
        private Transform _rollbackPlayerRoot;
        // T-FO06DC: прямой сдвиг deathY был применён (только success-путь).
        // Без флага F9-поток сдвигал deathY назад, хотя вперёд его не двигали.
        private bool _respawnShiftApplied;
        // T-FO06DD: прямой сдвиг камеры был применён (только success-путь).
        // Тот же флаг-паттерн, что и для deathY: rollback сдвигает назад только при флаге.
        private bool _cameraShiftApplied;

        // T-FO06DH: именованный канал сдвига для второго клиента.
        private const string RebaseShiftMessageName = "FO06_REBASE_SHIFT";

        /// <summary>
        /// T-FO06DH: payload broadcast сдвига. Сериализация только через
        /// проверенные overloads NGO (BufferSerializer.SerializeValue для float,
        /// WriteValueSafe/ReadValueSafe для INetworkSerializable).
        /// </summary>
        private struct RebaseShiftMessage : INetworkSerializable
        {
            public float Dx;
            public float Dy;
            public float Dz;
            public ulong FrameGeneration;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Dx);
                serializer.SerializeValue(ref Dy);
                serializer.SerializeValue(ref Dz);
                serializer.SerializeValue(ref FrameGeneration);
            }
        }

        public LocalCoordinateFrame CurrentFrame => _frame;
        public ulong FrameGeneration => _frameGeneration;
        public bool IsTransactionActive => _transactionActive;
        public int RegisteredParticipantCount => _participants.Count;

        private void Awake()
        {
            _frame = new LocalCoordinateFrame(
                new GlobalPosition(_initialOriginX, _initialOriginY, _initialOriginZ),
                _maxLocalCoordinate);
            _frameGeneration = 1;
            _initialized = true;
            GlobalMotionRuntimeEvidenceProbe.RecordEvent(
                "runtimeRebase", "FrameInitialized",
                "origin=" + _frame.Origin + ";maxLocal=" + _frame.MaxLocalCoordinate.ToString("R", CultureInfo.InvariantCulture));
        }

        private void Update()
        {
            if (!Application.isPlaying || !_initialized || _transactionActive)
                return;

            TryRegisterShiftHandler();

            if (IsKeyPressed(_successKey))
                RequestControlledRebase(false);
            else if (IsKeyPressed(_rollbackKey))
                RequestControlledRebase(true);
        }

        // T-FO06DH: приём серверного broadcast сдвига (Host + второй клиент).
        // OnEnable может сработать до появления Singleton — Update добирает регистрацию.
        private bool _shiftHandlerRegistered;

        private void OnEnable()
        {
            TryRegisterShiftHandler();
        }

        private void OnDisable()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (_shiftHandlerRegistered && manager != null && manager.CustomMessagingManager != null)
                manager.CustomMessagingManager.UnregisterNamedMessageHandler(RebaseShiftMessageName);
            _shiftHandlerRegistered = false;
        }

        private void TryRegisterShiftHandler()
        {
            if (_shiftHandlerRegistered) return;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.CustomMessagingManager == null) return;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(RebaseShiftMessageName, OnRebaseShiftMessage);
            _shiftHandlerRegistered = true;
        }

        private static bool IsKeyPressed(KeyCode key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            switch (key)
            {
                case KeyCode.F8:
                    return keyboard.f8Key.wasPressedThisFrame;
                case KeyCode.F9:
                    return keyboard.f9Key.wasPressedThisFrame;
                default:
                    return false;
            }
        }

        [ContextMenu("Request Controlled Rebase")]
        public void RequestControlledRebase()
        {
            RequestControlledRebase(false);
        }

        [ContextMenu("Request Controlled Rebase With Rollback")]
        public void RequestControlledRebaseWithRollback()
        {
            RequestControlledRebase(true);
        }

        public void RequestControlledRebase(bool forceValidationFailure)
        {
            if (_transactionActive)
            {
                Debug.LogWarning("[T-FO06CY] Rebase request rejected: transaction already active.", this);
                return;
            }

            // T-FO06DH: транзакция — серверная. Клиент, нажавший F8, отклоняется:
            // локальный apply без authority рассинхронизировал бы сцену.
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
            {
                Reject("rebase_requires_server");
                return;
            }

            _transactionActive = true;
            _rollbackNextValidation = forceValidationFailure;
            _rollbackPlayerRoot = null;
            _respawnShiftApplied = false;
            _cameraShiftApplied = false;
            try
            {
                ExecuteTransaction();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "Faulted", exception.GetType().Name);
            }
            finally
            {
                _rollbackNextValidation = false;
                _transactionActive = false;
                _rollbackPlayerRoot = null;
            }
        }

        private void ExecuteTransaction()
        {
            if (!TryResolveLocalPlayer(out Transform playerRoot, out string playerError))
            {
                Reject("local_player_unavailable:" + playerError);
                return;
            }
            _rollbackPlayerRoot = playerRoot;

            if (!TryBuildParticipants(playerRoot, NetworkManager.Singleton, out GlobalMotionRebaseParticipantSet participantSet, out string participantError))
            {
                Reject("participant_scope_refused:" + participantError);
                return;
            }

            Vector3 localFocus = playerRoot.position;
            if (!OriginRebasePlan.TryCreate(_frame, localFocus, _threshold, _quantum, out OriginRebasePlan plan))
            {
                Reject("no_rebase_plan:localFocus=" + localFocus);
                return;
            }

            var request = GlobalMotionRebaseRequest.Create(
                checked(_frameGeneration + 1),
                plan,
                participantSet.Count,
                BuildParticipantDigest());

            GlobalMotionRuntimeEvidenceProbe.RecordEvent(
                "runtimeRebase", "Requested",
                "transaction=" + request.TransactionId.ToString("N") + ";frame=" + request.FrameGeneration +
                ";reason=user_controlled;focus=" + localFocus + ";translation=" + plan.LocalTranslation);

            var coordinator = new GlobalMotionRebaseCoordinator(new FreezeGate(this, playerRoot));
            if (!coordinator.TryPrepare(request, participantSet, out string error))
            {
                Reject("prepare_refused:" + error);
                return;
            }
            GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "FramePrepared", "participants=" + participantSet.Count);

            if (!ApplyParticipants(request, out error))
            {
                Rollback(coordinator, request, "apply_refused:" + error);
                return;
            }
            GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "Applied", "translation=" + plan.LocalTranslation);

            Physics.SyncTransforms();
            GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "PhysicsSynchronized", "scene=" + _worldScenePath);

            if (!RebuildParticipants(request, out error))
            {
                Rollback(coordinator, request, "rebuild_refused:" + error);
                return;
            }
            GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "Rebuilt", "participants=" + _participants.Count);

            if (!ValidateParticipants(request, out error))
            {
                Rollback(coordinator, request, "validate_refused:" + error);
                return;
            }
            GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "Validated", "localFocusAfter=" + playerRoot.position);

            // T-FO06DA: сброс интерполяции stock NetworkTransform после сдвига мира.
            PublishNetworkTeleport();

            // T-FO06DB: увести deathY вместе с миром, иначе легитимная земля
            // окажется ниже порога и игрок будет ретелепортироваться каждые 0.5с.
            ShiftPlayerFrameReferences(playerRoot, plan.LocalTranslation);
            _respawnShiftApplied = true;

            // T-FO06DD: увести камеру вместе с миром через dormant API.
            if (ShiftCameraHistory(plan.LocalTranslation))
                _cameraShiftApplied = true;

            // T-FO06DF: сбросить кулдаун пере-регистрации палуб — иначе повторный
            // сдвиг в пределах 30с дрейф-детектор проглотит и палубы останутся на stale-навмеше.
            NotifyDeckRebase();

            _frame = plan.After;
            _frameGeneration = request.FrameGeneration;
            GlobalMotionRuntimeEvidenceProbe.RecordEvent(
                "runtimeRebase", "Published",
                "origin=" + _frame.Origin + ";localFocusAfter=" + playerRoot.position);

            if (!coordinator.TryCommit(out error))
            {
                Rollback(coordinator, request, "commit_refused:" + error);
                return;
            }

            GlobalMotionRuntimeEvidenceProbe.RecordEvent(
                "runtimeRebase", "Completed",
                "transaction=" + request.TransactionId.ToString("N") + ";frame=" + _frameGeneration +
                ";origin=" + _frame.Origin);
            Debug.Log("[T-FO06CY] Controlled rebase completed: translation=" + plan.LocalTranslation + ";origin=" + _frame.Origin, this);
            // T-FO06DH: уведомить второго клиента о сдвиге (только success-путь).
            BroadcastRebaseShift(NetworkManager.Singleton, plan.LocalTranslation, request.FrameGeneration);
        }

        private bool ApplyParticipants(GlobalMotionRebaseRequest request, out string error)
        {
            error = null;
            for (int i = 0; i < _participants.Count; i++)
            {
                if (!_participants[i].TryApply(request, out error))
                    return false;
            }

            if (_rollbackNextValidation)
            {
                error = "forced_user_validation_failure";
                return false;
            }
            return true;
        }

        private bool RebuildParticipants(GlobalMotionRebaseRequest request, out string error)
        {
            error = null;
            for (int i = 0; i < _participants.Count; i++)
            {
                if (!_participants[i].TryRebuild(request, out error))
                    return false;
            }
            return true;
        }

        private bool ValidateParticipants(GlobalMotionRebaseRequest request, out string error)
        {
            error = null;
            for (int i = 0; i < _participants.Count; i++)
            {
                if (!_participants[i].TryValidate(request, out error))
                    return false;
            }
            return true;
        }

        private void Rollback(GlobalMotionRebaseCoordinator coordinator, GlobalMotionRebaseRequest request, string reason)
        {
            GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "RollbackRequested", reason);
            bool restored = coordinator.TryAbort(out string error);
            Physics.SyncTransforms();
            // T-FO06DA: тот же сброс интерполяции после возврата позиций.
            PublishNetworkTeleport();
            // T-FO06DB: вернуть deathY назад вместе с миром.
            // T-FO06DC: только если прямой сдвиг был применён — в F9-потоке
            // success-путь не выполняется, без флага deathY уходил бы в +2560.
            if (_respawnShiftApplied)
            {
                ShiftPlayerFrameReferences(_rollbackPlayerRoot, -request.Plan.LocalTranslation);
                _respawnShiftApplied = false;
            }
            // T-FO06DD: вернуть камеру назад только если прямой сдвиг был применён.
            if (_cameraShiftApplied)
            {
                ShiftCameraHistory(-request.Plan.LocalTranslation);
                _cameraShiftApplied = false;
            }
            // T-FO06DF: тот же сброс кулдауна после возврата позиций.
            NotifyDeckRebase();
            GlobalMotionRuntimeEvidenceProbe.RecordEvent(
                "runtimeRebase",
                restored ? "RollbackCompleted" : "RollbackFaulted",
                "reason=" + reason + ";error=" + (error ?? "none"));
            Debug.LogWarning("[T-FO06CY] Controlled rebase rolled back: " + reason + ";restore=" + restored, this);
        }

        /// <summary>
        /// T-FO06DA: сброс интерполяции stock NetworkTransform после сдвига/возврата мира.
        /// Значения не меняются — только флаг телепорта, иначе клиенты интерполируют
        /// сдвиг в десятки километров как обычное движение. Best-effort: ошибки и
        /// отсутствие authority уходят в счётчики evidence, транзакцию не валят.
        /// Поток пилота (GlobalMotionReplicator, global-координаты) инвариантен к сдвигу.
        /// </summary>
        private void PublishNetworkTeleport()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
            {
                GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "NetworkPublished", "skipped:no_server_authority");
                return;
            }
            int teleported = 0, skipped = 0, errors = 0;
            for (int i = 0; i < _participants.Count; i++)
            {
                Transform target = _participants[i].Target;
                if (target == null) continue;
                NetworkTransform[] writers = target.GetComponentsInChildren<NetworkTransform>(false);
                for (int j = 0; j < writers.Length; j++)
                {
                    NetworkTransform nt = writers[j];
                    if (nt == null || !nt.IsSpawned) { skipped++; continue; }
                    try
                    {
                        if (!nt.CanCommitToTransform) { skipped++; continue; }
                        Transform t = nt.transform;
                        nt.Teleport(t.position, t.rotation, t.localScale);
                        teleported++;
                    }
                    catch (Exception) { errors++; }
                }
            }
            GlobalMotionRuntimeEvidenceProbe.RecordEvent(
                "runtimeRebase", "NetworkPublished",
                "teleported=" + teleported + ";skipped=" + skipped + ";errors=" + errors);
        }

        /// <summary>
        /// T-FO06DB: сдвиг абсолютного порога падения игрока вместе с миром.
        /// T-FO06DG: плюс сдвиг кэша платформы (иначе флинг на translation
        /// первым кадром на палубе). No-op при отсутствии компонентов.
        /// </summary>
        private void ShiftPlayerFrameReferences(Transform playerRoot, Vector3 translation)
        {
            if (playerRoot == null) return;
            var tracker = playerRoot.GetComponent<ProjectC.Player.PlayerRespawnTracker>();
            if (tracker != null) tracker.ApplyRebaseTranslation(translation);
            var player = playerRoot.GetComponent<ProjectC.Player.NetworkPlayer>();
            if (player != null) player.ApplyRebaseTranslation(translation);
            GlobalMotionRuntimeEvidenceProbe.RecordEvent(
                "runtimeRebase", "RespawnShifted",
                "dy=" + translation.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// T-FO06DD: сдвиг истории камеры (позиция + lag + точка коллизии) вместе с миром
        /// через dormant API SpringArmCamera. Без этого камера в anti-pop окне
        /// (столкновение в момент F8) возвращается к досдвиговой collisionPos —
        /// визуальный поп на десятки километров. Best-effort: неуспех — в маркер,
        /// транзакцию не валит (камера догоняет через snap).
        /// </summary>
        private bool ShiftCameraHistory(Vector3 translation)
        {
            ProjectC.Core.SpringArmCamera camera = FindAnyObjectByType<ProjectC.Core.SpringArmCamera>();
            if (camera == null)
            {
                GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "CameraShifted", "ok=False;error=camera_not_found");
                return false;
            }
            if (!camera.TryApplyGlobalMotionCameraTranslation(translation, out string error))
            {
                GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "CameraShifted", "ok=False;error=" + error);
                return false;
            }
            GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "CameraShifted", "ok=True");
            return true;
        }

        /// <summary>
        /// T-FO06DF: уведомление палуб о мировом сдвиге (сброс 30с кулдауна).
        /// Саму пере-регистрацию выполняет штатный дрейф-детектор в LateUpdate
        /// (Unregister + очередь 1/кадр). Здесь только гарантия, что сдвиг
        /// не будет проглочен кулдауном. Без флага: идемпотентно и безопасно
        /// в обоих путях (успех/rollback).
        /// </summary>

        /// <summary>
        /// T-FO06DH: приём серверного сдвига вторым клиентом. Сервер входящее
        /// игнорирует (применил синхронно). Клиент сдвигает ТОЛЬКО локальное
        /// состояние: камеру и deathY/платформу локального игрока. Телепорт NT,
        /// палубы и позиции игроков — серверные, клиент их не трогает.
        /// Флаги транзакции не затрагиваются (своей транзакции у клиента нет).
        /// </summary>
        private void OnRebaseShiftMessage(ulong senderClientId, FastBufferReader reader)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer) return;
            try
            {
                reader.ReadValueSafe(out RebaseShiftMessage message);
                var translation = new Vector3(message.Dx, message.Dy, message.Dz);
                if (!GlobalPosition.IsFiniteValue(translation.x) ||
                    !GlobalPosition.IsFiniteValue(translation.y) ||
                    !GlobalPosition.IsFiniteValue(translation.z))
                {
                    GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "ClientShiftApplied", "ok=False;error=translation_not_finite");
                    return;
                }
                ShiftCameraHistory(translation);
                if (TryResolveLocalPlayer(out Transform playerRoot, out _))
                    ShiftPlayerFrameReferences(playerRoot, translation);
                GlobalMotionRuntimeEvidenceProbe.RecordEvent(
                    "runtimeRebase", "ClientShiftApplied",
                    "ok=True;frame=" + message.FrameGeneration + ";from=" + senderClientId);
            }
            catch (Exception e)
            {
                GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "ClientShiftApplied", "ok=False;error=" + e.GetType().Name);
            }
        }

        /// <summary>
        /// T-FO06DH: broadcast сдвига всем клиентам после Completed.
        /// Rollback вещественного сдвига не делает (apply + restore = net zero) —
        /// broadcast только на success-пути. Best-effort, результат в маркере.
        /// </summary>
        private void BroadcastRebaseShift(NetworkManager manager, Vector3 translation, ulong frameGeneration)
        {
            if (manager == null || manager.CustomMessagingManager == null)
            {
                GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "BroadcastShifted", "sent=False;error=no_messaging_manager");
                return;
            }
            try
            {
                var message = new RebaseShiftMessage
                {
                    Dx = translation.x,
                    Dy = translation.y,
                    Dz = translation.z,
                    FrameGeneration = frameGeneration
                };
                using (var writer = new FastBufferWriter(32, Allocator.Temp))
                {
                    writer.WriteValueSafe(message);
                    manager.CustomMessagingManager.SendNamedMessageToAll(RebaseShiftMessageName, writer);
                }
                GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "BroadcastShifted", "sent=True;frame=" + frameGeneration);
            }
            catch (Exception e)
            {
                GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "BroadcastShifted", "sent=False;error=" + e.GetType().Name);
            }
        }

        private void NotifyDeckRebase()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer)
            {
                GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "DecksNotified", "skipped:no_server_authority");
                return;
            }
            int notified = ProjectC.Ship.ShipDeckNav.NotifyWorldRebased();
            GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "DecksNotified", "decks=" + notified);
        }

        private bool TryBuildParticipants(
            Transform playerRoot,
            NetworkManager manager,
            out GlobalMotionRebaseParticipantSet participantSet,
            out string error)
        {
            participantSet = new GlobalMotionRebaseParticipantSet();
            error = null;
            _participants.Clear();

            UnityEngine.SceneManagement.Scene worldScene = SceneManager.GetSceneByPath(_worldScenePath);
            if (!worldScene.IsValid() || !worldScene.isLoaded)
            {
                error = "world_scene_not_loaded:" + _worldScenePath;
                return false;
            }

            if (_includeAllWorldSceneRoots)
            {
                GameObject[] roots = worldScene.GetRootGameObjects();
                Array.Sort(roots, CompareNames);
                var skippedInactive = new List<string>();
                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i] == null || roots[i].transform == null)
                        continue;
                    // T-FO06CZ: coordinator admission requires every participant to be current
                    // (activeInHierarchy). Inactive executor-managed roots (e.g. disabled
                    // SPAWN_TEST cult) are not rebase participants; including them fails
                    // preparation with stale_participant and blocks F8/F9 entirely.
                    if (!roots[i].activeInHierarchy)
                    {
                        skippedInactive.Add(roots[i].name);
                        continue;
                    }
                    if (!AddParticipant("WORLD_SCENE_ROOT/" + roots[i].name, roots[i].transform, GlobalMotionRebaseParticipantKind.CityStatic, participantSet, out error))
                        return false;
                }
                if (skippedInactive.Count > 0)
                    Debug.Log("[T-FO06CZ] Inactive scene roots excluded from rebase scope: " + string.Join(", ", skippedInactive), this);
            }

            if (playerRoot != null && !IsContainedByRegisteredRoot(playerRoot))
            {
                if (!AddParticipant("PLAYER_FRAME/LOCAL", playerRoot, GlobalMotionRebaseParticipantKind.PlayerFrame, participantSet, out error))
                    return false;
            }

            // T-FO06DH: остальные подключённые игроки — иначе сервер оставит
            // их PlayerObject в старых координатах, пока мир уехал.
            if (manager != null)
            {
                foreach (var clientEntry in manager.ConnectedClients)
                {
                    NetworkClient remoteClient = clientEntry.Value;
                    if (remoteClient == null || remoteClient.PlayerObject == null) continue;
                    Transform remoteRoot = remoteClient.PlayerObject.transform;
                    if (remoteRoot == null || remoteRoot == playerRoot || IsContainedByRegisteredRoot(remoteRoot)) continue;
                    if (!AddParticipant("PLAYER_FRAME/REMOTE_" + clientEntry.Key, remoteRoot,
                            GlobalMotionRebaseParticipantKind.PlayerFrame, participantSet, out error))
                        return false;
                }
            }

            if (_additionalParticipantRoot != null && !IsContainedByRegisteredRoot(_additionalParticipantRoot))
            {
                if (!AddParticipant("EXPLICIT_ADDITIONAL/" + _additionalParticipantRoot.name, _additionalParticipantRoot,
                        GlobalMotionRebaseParticipantKind.WorldAnchor, participantSet, out error))
                    return false;
            }

            if (_participants.Count == 0)
            {
                error = "participant_scope_empty";
                return false;
            }

            return true;
        }

        private bool AddParticipant(
            string id,
            Transform target,
            GlobalMotionRebaseParticipantKind kind,
            GlobalMotionRebaseParticipantSet set,
            out string error)
        {
            error = null;
            var participant = new SceneParticipant(id, kind, target);
            if (!set.TryAdd(participant, out error))
                return false;
            _participants.Add(participant);
            return true;
        }

        private bool IsContainedByRegisteredRoot(Transform candidate)
        {
            for (int i = 0; i < _participants.Count; i++)
            {
                Transform registered = _participants[i].Target;
                if (registered == null) continue;
                if (candidate == registered || candidate.IsChildOf(registered)) return true;
            }
            return false;
        }

        private bool TryResolveLocalPlayer(out Transform playerRoot, out string error)
        {
            playerRoot = null;
            error = null;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening)
            {
                error = "network_manager_not_listening";
                return false;
            }
            if (!manager.ConnectedClients.TryGetValue(manager.LocalClientId, out NetworkClient client) || client.PlayerObject == null)
            {
                error = "local_player_object_missing";
                return false;
            }
            playerRoot = client.PlayerObject.transform;
            return true;
        }

        private string BuildParticipantDigest()
        {
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < _participants.Count; i++)
            {
                if (i > 0) builder.Append('|');
                builder.Append(_participants[i].ParticipantId);
            }
            return builder.ToString();
        }

        private void Reject(string reason)
        {
            GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "Rejected", reason);
            Debug.LogWarning("[T-FO06CY] Controlled rebase rejected: " + reason, this);
        }

        private void FreezeController(Transform playerRoot)
        {
            _frozenController = playerRoot != null ? playerRoot.GetComponent<CharacterController>() : null;
            _controllerWasEnabled = _frozenController != null && _frozenController.enabled;
            if (_controllerWasEnabled) _frozenController.enabled = false;
        }

        private void ReleaseController()
        {
            if (_frozenController != null) _frozenController.enabled = _controllerWasEnabled;
            _frozenController = null;
            _controllerWasEnabled = false;
        }

        private static int CompareNames(GameObject left, GameObject right)
        {
            return string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);
        }

        private sealed class FreezeGate : IGlobalMotionRebaseGate
        {
            private readonly GlobalMotionControlledRebaseSlice _owner;
            private readonly Transform _playerRoot;

            public FreezeGate(GlobalMotionControlledRebaseSlice owner, Transform playerRoot)
            {
                _owner = owner;
                _playerRoot = playerRoot;
            }

            public bool TryFreeze(GlobalMotionRebaseRequest request, out string error)
            {
                error = null;
                _owner.FreezeController(_playerRoot);
                GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "Frozen", "controllerDisabled=" + (_owner._frozenController != null));
                return true;
            }

            public bool TryRelease(GlobalMotionRebaseRequest request, out string error)
            {
                error = null;
                _owner.ReleaseController();
                GlobalMotionRuntimeEvidenceProbe.RecordEvent("runtimeRebase", "Released", null);
                return true;
            }
        }

        private sealed class SceneParticipant : IGlobalMotionRebaseParticipant
        {
            private Snapshot _snapshot;
            private bool _captured;

            public string ParticipantId { get; }
            public GlobalMotionRebaseParticipantKind Kind { get; }
            public Transform Target { get; }
            public bool IsCurrent => Target != null && Target.gameObject.activeInHierarchy;

            public SceneParticipant(string participantId, GlobalMotionRebaseParticipantKind kind, Transform target)
            {
                ParticipantId = participantId;
                Kind = kind;
                Target = target;
            }

            public bool TryPreflight(GlobalMotionRebaseRequest request, out string error)
            {
                error = null;
                if (!request.IsValid) { error = "request_invalid"; return false; }
                if (!IsCurrent) { error = "target_missing_or_inactive"; return false; }
                if (Target.lossyScale == Vector3.zero) { error = "target_scale_invalid"; return false; }
                return true;
            }

            public bool TryCapture(GlobalMotionRebaseRequest request, out IGlobalMotionRebaseSnapshot snapshot, out string error)
            {
                snapshot = null;
                error = null;
                if (!TryPreflight(request, out error)) return false;
                _snapshot = new Snapshot(ParticipantId, Target.position, Target.rotation, Target.localScale);
                _captured = true;
                snapshot = _snapshot;
                return true;
            }

            public bool TryApply(GlobalMotionRebaseRequest request, out string error)
            {
                error = null;
                if (!_captured || !TryPreflight(request, out error)) return false;
                Target.position += request.Plan.LocalTranslation;
                return IsFinite(Target.position) && IsFinite(Target.rotation) && IsFinite(Target.localScale);
            }

            public bool TryRebuild(GlobalMotionRebaseRequest request, out string error)
            {
                error = null;
                if (!_captured || !TryPreflight(request, out error)) return false;
                return true;
            }

            public bool TryValidate(GlobalMotionRebaseRequest request, out string error)
            {
                error = null;
                if (!_captured || !TryPreflight(request, out error)) return false;
                return IsFinite(Target.position) && IsFinite(Target.rotation) && IsFinite(Target.localScale);
            }

            public bool TryRestore(GlobalMotionRebaseRequest request, IGlobalMotionRebaseSnapshot snapshot, out string error)
            {
                error = null;
                if (!(snapshot is Snapshot typed) || !string.Equals(typed.ParticipantId, ParticipantId, StringComparison.Ordinal))
                {
                    error = "snapshot_identity_mismatch";
                    return false;
                }
                if (!IsCurrent) { error = "target_missing_or_inactive"; return false; }
                Target.SetPositionAndRotation(typed.Position, typed.Rotation);
                Target.localScale = typed.LocalScale;
                _captured = false;
                return true;
            }

            private static bool IsFinite(Vector3 value) =>
                GlobalPosition.IsFiniteValue(value.x) && GlobalPosition.IsFiniteValue(value.y) && GlobalPosition.IsFiniteValue(value.z);

            private static bool IsFinite(Quaternion value) =>
                GlobalPosition.IsFiniteValue(value.x) && GlobalPosition.IsFiniteValue(value.y) &&
                GlobalPosition.IsFiniteValue(value.z) && GlobalPosition.IsFiniteValue(value.w);

            private sealed class Snapshot : IGlobalMotionRebaseSnapshot
            {
                public string ParticipantId { get; }
                public Vector3 Position { get; }
                public Quaternion Rotation { get; }
                public Vector3 LocalScale { get; }

                public Snapshot(string participantId, Vector3 position, Quaternion rotation, Vector3 localScale)
                {
                    ParticipantId = participantId;
                    Position = position;
                    Rotation = rotation;
                    LocalScale = localScale;
                }
            }
        }
    }
}