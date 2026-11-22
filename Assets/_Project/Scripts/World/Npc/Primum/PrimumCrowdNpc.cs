using ProjectC.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectC.World.NpcPrimum
{
    /// <summary>
    /// Один городской NPC пилотной толпы PRIMUM.
    ///
    /// Контракт:
    ///  • двигает только NavMeshAgent (никаких Rigidbody/CharacterController);
    ///  • дом хранится В ЛОКАЛЬНЫХ координатах WorldRoot_0_0 → Floating Origin не ломает якорь;
    ///  • тик вызывается директором с фазовым офсетом (NPC не имеют собственного Update);
    ///  • личность (имя/диалог) приходит из NpcDefinition через NpcController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrimumCrowdNpc : MonoBehaviour
    {
        public enum State
        {
            Idle = 0,
            Moving = 1
        }

        private static readonly int SpeedParam = Animator.StringToHash("Speed");

        // Буфер зонда земли под NPC (без аллокаций в Tick/спавне).
        private static readonly RaycastHit[] GroundHits = new RaycastHit[8];

        private PrimumNpcCrowdDirector _director;
        private PrimumCrowdConfig _config;
        private NavMeshAgent _agent;
        private Animator _animator;
        private NpcController _npcController;
        private TextMeshPro _nameplate;
        private Transform _visual;
        private Renderer[] _renderers;
        private Renderer[] _visualRenderers;
        private bool _hasSpeedParam;
        private bool _groundAligned;
        private float _groundAlignAt;

        private Vector3 _homeLocal;
        private Vector3 _targetLocal;
        private float _speed;
        private float _nextTick;
        private float _idleUntil;
        private float _movingSince;
        private State _state = State.Idle;

        private bool _visualActive = true;
        private bool _animatorActive = true;
        private bool _nameplateActive = true;
        private bool _initialized;

        public string NpcId { get; private set; } = string.Empty;
        public string HouseId { get; private set; } = string.Empty;
        public string ChunkId { get; private set; } = string.Empty;
        public State CurrentState => _state;
        public bool IsInitialized => _initialized;
        public NavMeshAgent Agent => _agent;
        public Vector3 HomeLocal => _homeLocal;

        /// <summary>Готовит NPC: визуал, аниматор, личность, агент, дом.</summary>
        public void Initialize(
            PrimumNpcCrowdDirector director,
            PrimumCrowdConfig config,
            NpcDefinition definition,
            string houseId,
            string chunkId,
            Vector3 homeLocal,
            Vector3 spawnWorld,
            GameObject modelPrefab,
            RuntimeAnimatorController animatorController,
            float phaseOffsetSeconds)
        {
            if (_initialized) return;
            _initialized = true;

            _director = director;
            _config = config;
            HouseId = houseId ?? string.Empty;
            ChunkId = chunkId ?? string.Empty;
            _homeLocal = homeLocal;

            _agent = GetComponent<NavMeshAgent>();
            _npcController = GetComponent<NpcController>();
            _nameplate = GetComponentInChildren<TextMeshPro>(true);

            // Визуал: инстанс FBX (в нём уже есть Animator с валидным humanoid-аватаром).
            if (modelPrefab != null)
            {
                var model = Instantiate(modelPrefab, transform, false);
                model.name = "Visual";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                _visual = model.transform;
                _animator = model.GetComponentInChildren<Animator>(true);
            }
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                _animator.cullingMode = AnimatorCullingMode.CullCompletely;
                if (animatorController != null) _animator.runtimeAnimatorController = animatorController;
                var pars = _animator.parameters;
                for (int i = 0; i < pars.Length; i++)
                    if (pars[i].nameHash == SpeedParam) { _hasSpeedParam = true; break; }
                _animator.SetFloat(SpeedParam, 0f);
            }

            _visualRenderers = _visual != null
                ? _visual.GetComponentsInChildren<Renderer>(true)
                : GetComponentsInChildren<Renderer>(true);
            // Нейм-плейт в LOD-списке не участвует: у него своя видимость по nameplateRadius.
            _renderers = _visualRenderers;

            // Позиция и агент. Агент в шаблоне выключен — включаем только на навмеше.
            transform.position = spawnWorld;
            if (_agent != null)
            {
                _agent.acceleration = config.acceleration;
                _agent.angularSpeed = config.angularSpeed;
                _agent.stoppingDistance = config.stoppingDistance;
                _agent.autoBraking = true;
                _agent.autoRepath = true;
                _agent.radius = 0.5f;
                _agent.height = 2f;
                _agent.speed = config.walkSpeedMin;
                _agent.obstacleAvoidanceType = config.disableAvoidance
                    ? ObstacleAvoidanceType.NoObstacleAvoidance
                    : ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                _agent.enabled = true;
                if (_agent.isOnNavMesh) _agent.Warp(spawnWorld);
            }

            _speed = Random.Range(config.walkSpeedMin, config.walkSpeedMax);
            if (_agent != null) _agent.speed = _speed;

            _state = State.Idle;
            _idleUntil = Time.time + Random.Range(config.idleMinSeconds, config.idleMaxSeconds) + phaseOffsetSeconds * 0.25f;
            _nextTick = Time.time + phaseOffsetSeconds;

            // Личность: NpcController регистрирует NPC в своём реестре → работает E-key пайплайн.
            if (_npcController != null)
            {
                _npcController.SetDefinition(definition, _nameplate);
                NpcId = _npcController.NpcId;
            }
            else if (definition != null)
            {
                NpcId = definition.npcId;
            }

            if (_nameplate != null)
            {
                _nameplate.text = definition != null ? definition.displayName : name;
                _nameplate.gameObject.SetActive(true);
            }

            // Пивот rigged-моделей внутри голеней — без подъёма NPC уходят в мостовую.
            AlignVisualToGround();
            PlaceNameplateAboveHead();
            _groundAlignAt = Time.time + 1f;

            ApplyVisualLod(transform.position, false);
        }

        /// <summary>Вызывается директором по фазовому расписанию.</summary>
        public void Tick(float now, Vector3 cameraPosition)
        {
            if (!_initialized || _agent == null) return;

            // Через секунду аниматор уже дал позу Idle — выравниваем по ней, а не по bind pose.
            if (!_groundAligned && now >= _groundAlignAt)
            {
                _groundAligned = true;
                AlignVisualToGround();
                PlaceNameplateAboveHead();
            }

            if (_agent.isOnNavMesh)
            {
                if (_state == State.Idle)
                {
                    if (now >= _idleUntil) ChooseTarget(now);
                }
                else
                {
                    bool arrived = !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.2f;
                    if (arrived)
                    {
                        EnterIdle(now);
                    }
                    else if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
                    {
                        EnterIdle(now);
                    }
                    else if (!_agent.pathPending && _agent.velocity.sqrMagnitude < 0.0025f)
                    {
                        // Стоим на месте слишком долго, хотя должны идти → пере-выбор цели.
                        if (now - _movingSince > 4f) EnterIdle(now);
                    }
                    else
                    {
                        _movingSince = now;
                    }
                }

                if (_animator != null && _animator.enabled && _hasSpeedParam)
                {
                    float v = _agent.velocity.magnitude;
                    _animator.SetFloat(SpeedParam, Mathf.Clamp01(v / Mathf.Max(0.1f, _config.walkAnimReferenceSpeed)));
                }
            }

            ApplyVisualLod(cameraPosition, true);
        }

        private void ChooseTarget(float now)
        {
            if (_config == null || _director == null || _director.WorldRoot == null) return;

            Vector3 homeWorld = _director.WorldRoot.TransformPoint(_homeLocal);
            float radius = Random.value < _config.farWanderChance ? _config.wanderRadiusFar : _config.wanderRadiusNear;

            for (int attempt = 0; attempt < 4; attempt++)
            {
                Vector2 circle = Random.insideUnitCircle * radius;
                if (circle.sqrMagnitude < _config.minRadiusFromHome * _config.minRadiusFromHome)
                    circle = circle.normalized * _config.minRadiusFromHome;

                Vector3 candidate = homeWorld + new Vector3(circle.x, 0f, circle.y);
                if (!NavMesh.SamplePosition(candidate, out var hit, 6f, NavMesh.AllAreas)) continue;
                if ((hit.position - homeWorld).sqrMagnitude > (radius + 8f) * (radius + 8f)) continue;

                _targetLocal = _director.WorldRoot.InverseTransformPoint(hit.position);
                _agent.speed = _speed;
                if (_agent.SetDestination(hit.position))
                {
                    _state = State.Moving;
                    _movingSince = now;
                    return;
                }
            }

            // Не нашли точку — просто постоять ещё немного.
            EnterIdle(now);
        }

        private void EnterIdle(float now)
        {
            _state = State.Idle;
            _agent.ResetPath();
            _idleUntil = now + Random.Range(_config.idleMinSeconds, _config.idleMaxSeconds);
        }

        /// <summary>
        /// Пивот rigged-моделей лежит внутри голеней (замерено: ступни на 0.075–0.313 м ниже пивота),
        /// поэтому после спавна модель садилась бы в мостовую. Поднимаем Visual так, чтобы
        /// низ меша совпал с землёй под NPC.
        /// </summary>
        private void AlignVisualToGround()
        {
            if (_visual == null) return;
            if (!TryGetVisualVerticalBounds(out float bottom, out _)) return;

            float lift = ResolveGroundY() - bottom;
            if (Mathf.Abs(lift) < 0.005f) return;

            float parentScaleY = Mathf.Max(0.0001f, transform.lossyScale.y);
            var lp = _visual.localPosition;
            _visual.localPosition = new Vector3(lp.x, lp.y + lift / parentScaleY, lp.z);
        }

        /// <summary>
        /// Земля берётся по реальной геометрии под NPC, а не по y агента: навмеш Recast квантован
        /// вокселями и висит над мостовой (замерено на 120 домах PRIMUM: медиана 0.24 м, до 0.3 м),
        /// поэтому выровненные по нему ступни визуально парят в воздухе. Свои коллайдеры (триггеры)
        /// пропускаем и берём самую верхнюю поверхность ниже пуза — то, на чём NPC реально стоит.
        /// </summary>
        private float ResolveGroundY()
        {
            Vector3 origin = transform.position + Vector3.up * 0.6f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, GroundHits, 1.8f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                var hit = GroundHits[i];
                if (hit.collider == null) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider is CharacterController) continue;   // игрок/живые актёры
                if (hit.collider.attachedRigidbody != null) continue; // динамика: корабли, грузы, бочки
                if (hit.point.y > best) best = hit.point.y;
            }
            return best == float.MinValue ? transform.position.y : best;
        }

        /// <summary>Нейм-плейт — над головой конкретной модели, а не на фиксированной высоте.</summary>
        private void PlaceNameplateAboveHead()
        {
            if (_nameplate == null) return;
            if (!TryGetVisualVerticalBounds(out _, out float top)) return;

            float parentScaleY = Mathf.Max(0.0001f, transform.lossyScale.y);
            float localTop = (top - transform.position.y) / parentScaleY;
            var lp = _nameplate.transform.localPosition;
            _nameplate.transform.localPosition = new Vector3(lp.x, localTop + 0.35f, lp.z);
        }

        private bool TryGetVisualVerticalBounds(out float bottom, out float top)
        {
            bottom = 0f;
            top = 0f;
            if (_visualRenderers == null || _visualRenderers.Length == 0) return false;

            bool has = false;
            for (int i = 0; i < _visualRenderers.Length; i++)
            {
                var r = _visualRenderers[i];
                if (r == null || !r.enabled) continue;

                var b = r.bounds;
                if (!has)
                {
                    bottom = b.min.y;
                    top = b.max.y;
                    has = true;
                }
                else
                {
                    if (b.min.y < bottom) bottom = b.min.y;
                    if (b.max.y > top) top = b.max.y;
                }
            }
            return has;
        }

        private void ApplyVisualLod(Vector3 cameraPosition, bool animate)
        {
            float sqr = (cameraPosition - transform.position).sqrMagnitude;

            bool showVisual = sqr <= _config.visualRadius * _config.visualRadius;
            if (showVisual != _visualActive && _renderers != null)
            {
                _visualActive = showVisual;
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null) _renderers[i].enabled = showVisual;
            }

            bool showAnimator = sqr <= _config.animatorRadius * _config.animatorRadius;
            if (showAnimator != _animatorActive && _animator != null)
            {
                _animatorActive = showAnimator;
                _animator.enabled = showAnimator;
            }

            bool showPlate = sqr <= _config.nameplateRadius * _config.nameplateRadius;
            if (showPlate != _nameplateActive && _nameplate != null)
            {
                _nameplateActive = showPlate;
                if (_nameplate.gameObject.activeSelf != showPlate) _nameplate.gameObject.SetActive(showPlate);
            }

            if (animate && showPlate && _nameplate != null && _director != null && _director.CameraTransform != null)
                _nameplate.transform.rotation = _director.CameraTransform.rotation;
        }

        /// <summary>
        /// Floating Origin: корень мира сдвинулся. Дом и цель — в локальных координатах, поэтому
        /// достаточно сбросить внутренний путь агента и при необходимости выдать цель заново.
        /// </summary>
        public void ApplyWorldRebase()
        {
            if (!_initialized || _agent == null || !_agent.isOnNavMesh) return;

            _agent.ResetPath();
            _agent.Warp(transform.position);

            if (_state == State.Moving && _director != null && _director.WorldRoot != null)
            {
                Vector3 world = _director.WorldRoot.TransformPoint(_targetLocal);
                if (NavMesh.SamplePosition(world, out var hit, 4f, NavMesh.AllAreas))
                    _agent.SetDestination(hit.position);
                else
                    EnterIdle(Time.time);
            }
        }

        /// <summary>Проверка инварианта «≤4 на дом» для отчётности директора.</summary>
        public void SetNextTick(float nextTick) => _nextTick = nextTick;

        public bool ReadyForTick(float now) => now >= _nextTick;
    }
}
