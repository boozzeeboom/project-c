using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using ProjectC.Player;

namespace ProjectC.Ship
{
    /// <summary>
    /// DoorController — дверь на составном корабле / здании.
    ///
    /// Типы (поле doorType):
    ///   SlidingSingle — одностворчатая сдвижная (каюты).
    ///   SlidingDouble — две створки разъезжаются (широкие проёмы, ангарные ворота).
    ///   HingedSingle  — распашная на петлях, петли слева/справа.
    ///   HingedDouble  — двустворчатая распашная (ворота).
    ///   LiftUp        — подъёмная шторка вверх (ангар, гараж).
    ///
    /// Дистанция открытия по умолчанию = габарит створки (AutoBySize),
    /// поэтому дверь отъезжает «на свой объём» и не улетает.
    /// Анимация всегда считается от закэшированной закрытой позы —
    /// накопления ошибки нет.
    ///
    /// Вся анимация в ЛОКАЛЬНЫХ координатах → безопасно для Floating Origin
    /// (ребейз двигает корни сцен целиком, локальные позы не меняются).
    ///
    /// E-key / F-key: NetworkPlayer вызывает Toggle(). Совместимость с v1 сохранена.
    /// Если на том же GameObject есть MetaRequirement — дверь можно запереть.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class DoorController : MonoBehaviour
    {
        /// <summary>Тип двери.</summary>
        public enum DoorType
        {
            SlidingSingle,  // Одна створка едет в сторону
            SlidingDouble,  // Две створки разъезжаются (ангар)
            HingedSingle,   // Распашная на петлях
            HingedDouble,   // Двустворчатая распашная
            LiftUp,         // Подъёмная шторка вверх (ангар)
        }

        /// <summary>Направление сдвига в ЛОКАЛЬНЫХ осях родителя двери.</summary>
        public enum SlideSide
        {
            Left,     // -X
            Right,    // +X
            Up,       // +Y
            Down,     // -Y
            Forward,  // +Z
            Back,     // -Z
        }

        /// <summary>Ось разъезда двустворчатой сдвижной двери.</summary>
        public enum DoubleSlideAxis
        {
            X_Horizontal,   // Створки едут влево-вправо
            Z_Depth,        // Створки едут вперёд-назад
        }

        /// <summary>С какой стороны петли у распашной двери (вид спереди).</summary>
        public enum HingeSide
        {
            Left,
            Right,
        }

        /// <summary>Как задаётся дистанция открытия.</summary>
        public enum OpenDistanceMode
        {
            AutoBySize, // По габариту створки — дверь не улетает (рекомендуется)
            Manual,     // Вручную, в метрах
        }

        // =====================================================================
        [Header("Тип двери")]
        [Tooltip("SlidingSingle — сдвижная. SlidingDouble — разъезд (ангар). " +
                 "HingedSingle/Double — распашные. LiftUp — шторка вверх (ангар).")]
        [SerializeField] private DoorType doorType = DoorType.SlidingSingle;

        [Header("Общее")]
        [Tooltip("Дверь открыта на старте сцены")]
        [SerializeField] private bool startOpen = false;

        [Tooltip("Длительность открытия/закрытия в секундах. Фиксированное время — дверь не улетает.")]
        [Range(0.1f, 5f)]
        [SerializeField] private float openDuration = 1f;

        [Tooltip("Плавность движения (ease in-out по умолчанию)")]
        [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Автозакрытие через N секунд после открытия. 0 — не закрывать.")]
        [Min(0f)]
        [SerializeField] private float autoCloseDelay = 0f;

        [SerializeField] private UnityEvent onOpened;
        [SerializeField] private UnityEvent onClosed;

        // =====================================================================
        [Header("Сдвижная · одна створка")]
        [Tooltip("Панель двери. Пусто = двигается сам объект (так в старых префабах кораблей)")]
        [SerializeField] private Transform doorModel;

        [Tooltip("Куда отъезжает створка (оси РАМКИ — объекта двери, его стрелки видно в Scene View)")]
        [SerializeField] private SlideSide slideSide = SlideSide.Right;

        [Tooltip("По габариту створки — отъедет ровно на свою ширину. Вручную — дистанция ниже.")]
        [SerializeField] private OpenDistanceMode distanceMode = OpenDistanceMode.AutoBySize;

        [Tooltip("Множитель габарита: 1 = ровно на свою ширину")]
        [Range(0.1f, 2f)]
        [SerializeField] private float sizeFactor = 1f;

        [Tooltip("Дистанция в метрах (только для ручного режима)")]
        [Min(0f)]
        [SerializeField] private float manualDistance = 1.5f;

        // =====================================================================
        [Header("Сдвижная · две створки (разъезд)")]
        [SerializeField] private Transform leftPanel;
        [SerializeField] private Transform rightPanel;

        [Tooltip("Ось, вдоль которой разъезжаются створки")]
        [SerializeField] private DoubleSlideAxis doubleAxis = DoubleSlideAxis.X_Horizontal;

        [SerializeField] private OpenDistanceMode doubleDistanceMode = OpenDistanceMode.AutoBySize;

        [Range(0.1f, 2f)]
        [SerializeField] private float doubleSizeFactor = 1f;

        [Tooltip("Дистанция КАЖДОЙ створки в метрах (только для ручного режима)")]
        [Min(0f)]
        [SerializeField] private float doubleManualDistance = 1.5f;

        // =====================================================================
        [Header("Распашная · одна створка")]
        [Tooltip("ОБЯЗАТЕЛЬНО: пустой объект на линии петель. Панель двери — ребёнок пивота. " +
                 "Создаётся кнопкой в инспекторе.")]
        [SerializeField] private Transform hingePivot;

        [Tooltip("Панель двери (необязательно, для гизмо). Обычно ребёнок пивота.")]
        [SerializeField] private Transform hingePanel;

        [Tooltip("С какой стороны петли (подсказка для кнопки создания пивота)")]
        [SerializeField] private HingeSide hingeSide = HingeSide.Right;

        [Tooltip("Угол открытия в градусах. Знак разворачивает сторону — если открылась не туда, поменяйте знак.")]
        [Range(-180f, 180f)]
        [SerializeField] private float openAngle = 90f;

        // =====================================================================
        [Header("Распашная · две створки")]
        [SerializeField] private Transform leftPivot;
        [SerializeField] private Transform rightPivot;
        [SerializeField] private Transform leftHingePanel;
        [SerializeField] private Transform rightHingePanel;

        [Tooltip("Угол КАЖДОЙ створки. Направления зеркалятся автоматически.")]
        [Range(5f, 170f)]
        [SerializeField] private float doubleOpenAngle = 90f;

        [Tooltip("Если створки открылись не туда — инвертировать обе")]
        [SerializeField] private bool invertDoubleSwing = false;

        // =====================================================================
        [Header("Подъёмная (ангарная шторка)")]
        [Tooltip("Панель шторки. Пусто = двигается сам объект.")]
        [SerializeField] private Transform liftPanel;

        [SerializeField] private OpenDistanceMode liftDistanceMode = OpenDistanceMode.AutoBySize;

        [Tooltip("Множитель высоты: 1 = подняться на свою высоту")]
        [Range(0.1f, 2f)]
        [SerializeField] private float liftSizeFactor = 1f;

        [Tooltip("Высота подъёма в метрах (только для ручного режима)")]
        [Min(0f)]
        [SerializeField] private float liftManualHeight = 2.5f;

        // =====================================================================
        // Legacy v1 (скрыты, только для чтения старых префабов + миграция).
        [HideInInspector][SerializeField] private Vector3 slideDirection = Vector3.right;
        [HideInInspector][SerializeField] private float slideDistance = 2f;
        [HideInInspector][SerializeField] private float slideSpeed = 1.5f;
        [HideInInspector][SerializeField] private bool legacyMigrated = false;

        /// <summary>Ссылка на корневой корабль (опционально, может быть null).</summary>
        public ShipController ShipController { get; private set; }

        /// <summary>Дверь открыта (целевое состояние — true сразу после Toggle).</summary>
        public bool IsOpen => _targetAmount > 0.5f;

        /// <summary>Дверь полностью открыта (анимация завершена).</summary>
        public bool IsFullyOpen => _openAmount >= 1f && !_animating;

        /// <summary>Идёт анимация.</summary>
        public bool IsAnimating => _animating;

        /// <summary>Прогресс открытия 0..1.</summary>
        public float OpenProgress => _openAmount;

        // Закэшированные позы (локальные — Floating Origin safe)
        private Vector3 _singleClosed, _singleOpen;
        private Vector3 _leftClosed, _leftOpen, _rightClosed, _rightOpen;
        private Vector3 _liftClosed, _liftOpen;
        private Quaternion _pivotClosed, _pivotOpen;
        private Quaternion _leftPivotClosed, _leftPivotOpen, _rightPivotClosed, _rightPivotOpen;
        private bool _posesCached;

        private float _openAmount;   // 0 закрыта, 1 открыта
        private float _targetAmount; // куда едем
        private bool _animating;
        private Coroutine _animCoroutine;
        private Coroutine _autoCloseCoroutine;

        // Панель одностворчатой / шторки: null = сам объект (legacy-префабы кораблей)
        private Transform SinglePanel => doorModel != null ? doorModel : transform;
        private Transform LiftPanel => liftPanel != null ? liftPanel : transform;

        // Панель распашной одной створки: явная → doorModel → сам объект
        private Transform HingePanelOrSelf => hingePanel != null ? hingePanel : SinglePanel;

        /// <summary>
        /// Неявный пивот: когда hingePivot не задан, створка крутится вокруг
        /// собственного края чистой математикой (без лишних объектов).
        /// Край — по доминантной ширине (X/Z рамки), ось — мировая вертикаль
        /// на момент кэширования закрытой позы. Одинаково в превью и в игре.
        /// </summary>
        private struct ImplicitHinge
        {
            public bool valid;
            public Transform panel;
            public Vector3 edgeLocal;   // край в пространстве родителя панели
            public Vector3 axisLocal;   // ось в пространстве родителя панели
            public Vector3 closedPos;
            public Quaternion closedRot;
            public float angle;
        }

        private ImplicitHinge _impSingle, _impLeft, _impRight;

        // Явный пивот крутится, только если он реально ведёт створку
        // (панель — его потомок, включая совпадение). Иначе пустой/чужой пивот
        // молча игнорируется в пользу края створки + предупреждение (не ошибка).
        private static bool UseExplicitPivot(Transform pivot, Transform panel)
        {
            return pivot != null && panel != null && IsDescendantOf(panel, pivot);
        }

        private bool _usePivot, _useLeftPivot, _useRightPivot;

        private void Awake()
        {
            var rootRef = GetComponentInParent<ShipRootReference>();
            if (rootRef != null)
                ShipController = rootRef.ShipController;

            EnsureMigrated();
            CacheClosedPoses();
            ResolveOpenPoses();

            _openAmount = _targetAmount = startOpen ? 1f : 0f;
            ApplyPose(SampleCurve(_openAmount));
        }

        // Свежий компонент: разумные дефолты + пропуск legacy-миграции.
        private void Reset()
        {
            doorType = DoorType.SlidingSingle;
            slideSide = SlideSide.Right;
            distanceMode = OpenDistanceMode.AutoBySize;
            sizeFactor = 1f;
            manualDistance = 1.5f;
            openDuration = 1f;
            openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            autoCloseDelay = 0f;
            openAngle = 90f;
            doubleOpenAngle = 90f;
            legacyMigrated = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            openDuration = Mathf.Clamp(openDuration, 0.1f, 5f);
            sizeFactor = Mathf.Clamp(sizeFactor, 0.1f, 2f);
            doubleSizeFactor = Mathf.Clamp(doubleSizeFactor, 0.1f, 2f);
            liftSizeFactor = Mathf.Clamp(liftSizeFactor, 0.1f, 2f);
            manualDistance = Mathf.Max(0f, manualDistance);
            doubleManualDistance = Mathf.Max(0f, doubleManualDistance);
            liftManualHeight = Mathf.Max(0f, liftManualHeight);
            autoCloseDelay = Mathf.Max(0f, autoCloseDelay);
            openAngle = Mathf.Clamp(openAngle, -180f, 180f);
            doubleOpenAngle = Mathf.Clamp(doubleOpenAngle, 5f, 170f);
            EnsureMigrated();
        }
#endif

        /// <summary>
        /// Миграция v1 → v2. Старые префабы переводятся в SlidingSingle + Manual,
        /// чтобы вести себя 1:1 как раньше. Выполняется один раз (флаг legacyMigrated).
        /// </summary>
        private void EnsureMigrated()
        {
            if (legacyMigrated) return;
            legacyMigrated = true;

            // Тип не трогаем (уже SlidingSingle по умолчанию — как v1).
            slideSide = ClosestSlideSide(slideDirection);
            manualDistance = Mathf.Max(0f, slideDistance);
            distanceMode = OpenDistanceMode.Manual; // сохранить старое поведение
            float speed = Mathf.Max(slideSpeed, 0.01f);
            openDuration = Mathf.Clamp(manualDistance / speed, 0.2f, 5f);
            if (openCurve == null || openCurve.length == 0)
                openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        private static SlideSide ClosestSlideSide(Vector3 dir)
        {
            if (dir.sqrMagnitude < 1e-6f) return SlideSide.Right;
            dir.Normalize();
            float best = float.NegativeInfinity;
            SlideSide bestSide = SlideSide.Right;
            for (int i = 0; i < 6; i++)
            {
                var side = (SlideSide)i;
                float d = Vector3.Dot(dir, SlideSideToLocal(side));
                if (d > best) { best = d; bestSide = side; }
            }
            return bestSide;
        }

        /// <summary>Направление SlideSide → локальный вектор В ПРОСТРАНСТВЕ РАМКИ (объекта двери).</summary>
        public static Vector3 SlideSideToLocal(SlideSide side)
        {
            switch (side)
            {
                case SlideSide.Left: return Vector3.left;
                case SlideSide.Right: return Vector3.right;
                case SlideSide.Up: return Vector3.up;
                case SlideSide.Down: return Vector3.down;
                case SlideSide.Forward: return Vector3.forward;
                case SlideSide.Back: return Vector3.back;
                default: return Vector3.right;
            }
        }

        // ============================== Публичное API ==============================

        /// <summary>Переключить дверь. Можно вызывать mid-animation — развернётся с текущего места.</summary>
        public void Toggle()
        {
            SetOpenTarget(_targetAmount < 0.5f);
        }

        public void Open() => SetOpenTarget(true);
        public void Close() => SetOpenTarget(false);

        /// <summary>Открыть/закрыть мгновенно (телепорт в позу). Для катсцен и инициализации.</summary>
        public void SetOpenInstant(bool open)
        {
            StopAnims();
            _openAmount = _targetAmount = open ? 1f : 0f;
            ApplyPose(SampleCurve(_openAmount));
        }

        private void SetOpenTarget(bool open)
        {
            if (!isActiveAndEnabled) return;
            float target = open ? 1f : 0f;
            if (Mathf.Approximately(_targetAmount, target) && !_animating) return;

            _targetAmount = target;
            StopAutoClose();
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateToTarget());
        }

        private void StopAnims()
        {
            if (_animCoroutine != null) { StopCoroutine(_animCoroutine); _animCoroutine = null; }
            StopAutoClose();
            _animating = false;
        }

        private void StopAutoClose()
        {
            if (_autoCloseCoroutine != null) { StopCoroutine(_autoCloseCoroutine); _autoCloseCoroutine = null; }
        }

        private IEnumerator AnimateToTarget()
        {
            _animating = true;
            float speed = 1f / Mathf.Max(openDuration, 0.05f);

            while (!Mathf.Approximately(_openAmount, _targetAmount))
            {
                _openAmount = Mathf.MoveTowards(_openAmount, _targetAmount, speed * Time.deltaTime);
                ApplyPose(SampleCurve(_openAmount));
                yield return null;
            }

            _openAmount = _targetAmount;
            ApplyPose(SampleCurve(_openAmount));
            _animating = false;
            _animCoroutine = null;

            if (_openAmount >= 1f)
            {
                onOpened?.Invoke();
                if (autoCloseDelay > 0f)
                    _autoCloseCoroutine = StartCoroutine(AutoCloseAfterDelay(autoCloseDelay));
            }
            else
            {
                onClosed?.Invoke();
            }
        }

        private IEnumerator AutoCloseAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            _autoCloseCoroutine = null;
            SetOpenTarget(false);
        }

        private float SampleCurve(float t)
        {
            if (openCurve == null || openCurve.length == 0) return t;
            return Mathf.Clamp01(openCurve.Evaluate(Mathf.Clamp01(t)));
        }

        // ============================== Позы ==============================

        /// <summary>
        /// Замерить габарит панели вдоль оси, заданной в пространстве space
        /// (обычно рамка двери — transform этого компонента).
        /// Учитывает Renderer потомков; Collider — только если мешей нет
        /// (в Edit Mode Collider.bounds отстают от трансформов).
        /// Возвращает размер в единицах РОДИТЕЛЯ ПАНЕЛИ. false — мерить нечего.
        /// </summary>
        public static bool TryMeasurePanelSize(Transform panel, Transform space, Vector3 localAxis, out float localSize)
        {
            localSize = 0f;
            if (panel == null || localAxis.sqrMagnitude < 1e-8f) return false;

            Vector3 worldDir = space != null
                ? space.TransformDirection(localAxis.normalized)
                : localAxis.normalized;
            if (worldDir.sqrMagnitude < 1e-8f) return false;
            worldDir.Normalize();

            Transform parent = panel.parent;

            bool has = false;
            Bounds bounds = new Bounds(panel.position, Vector3.zero);

            // Сначала меши: Renderer.bounds всегда свежие.
            foreach (var r in panel.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            // Коллайдеры — только если мешей нет: в Edit Mode Collider.bounds
            // могут отставать от трансформов (физика не степпится) и врать на глазах
            // у свеже-перепарентченных панелей.
            if (!has)
            {
                foreach (var c in panel.GetComponentsInChildren<Collider>(true))
                {
                    if (c == null) continue;
                    if (!has) { bounds = c.bounds; has = true; }
                    else bounds.Encapsulate(c.bounds);
                }
            }
            if (!has) return false;

            float worldSize = Mathf.Abs(Vector3.Dot(bounds.size, worldDir));
            if (worldSize < 1e-4f) return false;

            // Перевод в локальные единицы РОДИТЕЛЯ ПАНЕЛИ (при scale=1 — 1:1).
            float scaleAlong = 1f;
            if (parent != null)
            {
                Vector3 s = parent.lossyScale;
                scaleAlong = Mathf.Abs(s.x * worldDir.x) + Mathf.Abs(s.y * worldDir.y) + Mathf.Abs(s.z * worldDir.z);
                scaleAlong = Mathf.Max(scaleAlong, 1e-4f);
            }
            localSize = worldSize / scaleAlong;
            return localSize >= 1e-4f;
        }

        /// <summary>
        /// Дистанция сдвига панели: габарит × фактор либо ручная.
        /// Ось задана в пространстве рамки (space = transform двери).
        /// Возвращает дистанцию в единицах родителя панели.
        /// </summary>
        public static float ResolveSlideDistance(Transform panel, Transform space, Vector3 localAxis,
            OpenDistanceMode mode, float factor, float manual)
        {
            if (mode == OpenDistanceMode.AutoBySize)
            {
                if (TryMeasurePanelSize(panel, space, localAxis, out float size))
                    return Mathf.Max(size * Mathf.Max(factor, 0.01f), 0f);
                // Мерить нечего — безопасный фолбэк вместо «улёта».
                return Mathf.Min(Mathf.Max(manual, 0f), 3f);
            }
            return Mathf.Max(manual, 0f);
        }

        /// <summary>
        /// Перевести направление из пространства РАМКИ (объекта двери)
        /// в локальные оси родителя панели. Скейлы игнорируются (только поворот).
        /// </summary>
        private Vector3 FrameDirToPanelLocal(Transform panel, Vector3 frameDir)
        {
            if (frameDir.sqrMagnitude < 1e-8f) return frameDir;
            Vector3 w = transform.TransformDirection(frameDir.normalized);
            if (w.sqrMagnitude < 1e-8f) return frameDir;
            w.Normalize();
            Transform ps = panel != null ? panel.parent : null;
            Vector3 l = ps != null ? ps.InverseTransformDirection(w) : w;
            if (l.sqrMagnitude < 1e-8f) return frameDir;
            return l.normalized;
        }

        private void CacheClosedPoses()
        {
            _singleClosed = SinglePanel.localPosition;
            if (leftPanel != null) _leftClosed = leftPanel.localPosition;
            if (rightPanel != null) _rightClosed = rightPanel.localPosition;
            if (hingePivot != null) _pivotClosed = hingePivot.localRotation;
            if (leftPivot != null) _leftPivotClosed = leftPivot.localRotation;
            if (rightPivot != null) _rightPivotClosed = rightPivot.localRotation;
            _liftClosed = LiftPanel.localPosition;
            _posesCached = true;
            BuildImplicitHinges();
        }

        private void ResolveOpenPoses()
        {
            if (!_posesCached) CacheClosedPoses();
            ComputeOpenFromCachedClosed();
        }

        /// <summary>Посчитать открытые позы из закэшированных закрытых. Чистая функция от полей.</summary>
        private void ComputeOpenFromCachedClosed()
        {
            // Все направления заданы В ОСЯХ РАМКИ (объекта двери):
            // дизайнер видит стрелки рамки в Scene View и выбирает по ним.
            // В локальные оси каждой панели переводим через мир (повороты скелета/префаба учитываются).
            Vector3 dirF = SlideSideToLocal(slideSide);
            float d = ResolveSlideDistance(SinglePanel, transform, dirF, distanceMode, sizeFactor, manualDistance);
            _singleOpen = _singleClosed + FrameDirToPanelLocal(SinglePanel, dirF) * d;

            // Двустворчатая сдвижная
            Vector3 axisF = doubleAxis == DoubleSlideAxis.X_Horizontal ? Vector3.right : Vector3.forward;
            float dl = leftPanel != null
                ? ResolveSlideDistance(leftPanel, transform, axisF, doubleDistanceMode, doubleSizeFactor, doubleManualDistance) : 0f;
            float dr = rightPanel != null
                ? ResolveSlideDistance(rightPanel, transform, axisF, doubleDistanceMode, doubleSizeFactor, doubleManualDistance) : 0f;
            _leftOpen = _leftClosed - FrameDirToPanelLocal(leftPanel, axisF) * dl;
            _rightOpen = _rightClosed + FrameDirToPanelLocal(rightPanel, axisF) * dr;

            // Распашная одна: петли слева → −угол, справа → +угол (вокруг локального Y пивота)
            float signed = (hingeSide == HingeSide.Left ? -1f : 1f) * openAngle;
            _pivotOpen = _pivotClosed * Quaternion.Euler(0f, signed, 0f);

            // Распашная двойная: зеркально
            float swing = invertDoubleSwing ? -doubleOpenAngle : doubleOpenAngle;
            _leftPivotOpen = _leftPivotClosed * Quaternion.Euler(0f, -swing, 0f);
            _rightPivotOpen = _rightPivotClosed * Quaternion.Euler(0f, swing, 0f);

            // Подъёмная шторка (закрытая поза закэширована — читаем только её).
            // Едет строго по +Y РАМКИ: если рамка не выровнена по миру — сначала кнопка «🧭 Y рамки вверх».
            float h = ResolveSlideDistance(LiftPanel, transform, Vector3.up, liftDistanceMode, liftSizeFactor, liftManualHeight);
            _liftOpen = _liftClosed + FrameDirToPanelLocal(LiftPanel, Vector3.up) * h;
        }

        /// <summary>Пересобрать неявные пивоты из текущих трансформов (после CacheClosedPoses).</summary>
        private void BuildImplicitHinges()
        {
            _impSingle = default;
            _impLeft = default;
            _impRight = default;
            _usePivot = _useLeftPivot = _useRightPivot = false;

            if (doorType == DoorType.HingedSingle)
            {
                if (UseExplicitPivot(hingePivot, HingePanelOrSelf))
                {
                    _usePivot = true;
                }
                else
                {
                    float sideSign = hingeSide == HingeSide.Left ? -1f : 1f;
                    _impSingle = BuildImplicitHinge(HingePanelOrSelf, sideSign, sideSign * openAngle);
                }
            }
            else if (doorType == DoorType.HingedDouble)
            {
                float swing = invertDoubleSwing ? -doubleOpenAngle : doubleOpenAngle;
                if (UseExplicitPivot(leftPivot, leftHingePanel))
                    _useLeftPivot = true;
                else if (leftHingePanel != null)
                    _impLeft = BuildImplicitHinge(leftHingePanel, -1f, -swing);
                if (UseExplicitPivot(rightPivot, rightHingePanel))
                    _useRightPivot = true;
                else if (rightHingePanel != null)
                    _impRight = BuildImplicitHinge(rightHingePanel, 1f, swing);
            }
        }

        /// <summary>
        /// Построить неявный пивот для панели: край по доминантной ширине (X/Z рамки),
        /// ось — мировая вертикаль на текущий момент (в пространстве родителя панели).
        /// </summary>
        private ImplicitHinge BuildImplicitHinge(Transform panel, float edgeSide, float angle)
        {
            var h = new ImplicitHinge();
            if (panel == null) return h;

            bool hx = TryMeasurePanelSize(panel, transform, Vector3.right, out float sx);
            bool hz = TryMeasurePanelSize(panel, transform, Vector3.forward, out float sz);
            Vector3 widthAxisF = (hx && (!hz || sx >= sz)) ? Vector3.right : Vector3.forward;
            float w = Mathf.Max(hx ? sx : 0f, hz ? sz : 0f);
            if (w < 1e-4f) return h; // габарит не измерился — не вокруг чего строить край

            Vector3 edgeDirP = FrameDirToPanelLocal(panel, widthAxisF * edgeSide);
            Vector3 p0 = panel.localPosition;
            Transform ps = panel.parent;
            Vector3 a = ps != null ? ps.InverseTransformDirection(Vector3.up) : Vector3.up;
            if (a.sqrMagnitude < 1e-8f) return h;

            h.valid = true;
            h.panel = panel;
            h.edgeLocal = p0 + edgeDirP * (w * 0.5f);
            h.axisLocal = a.normalized;
            h.closedPos = p0;
            h.closedRot = panel.localRotation;
            h.angle = angle;
            return h;
        }

        /// <summary>Применить позу неявного пивота: дуга вокруг края (не хорда!).</summary>
        private static void ApplyImplicitHinge(ImplicitHinge h, float e)
        {
            if (!h.valid || h.panel == null) return;
            Quaternion r = Quaternion.AngleAxis(h.angle * e, h.axisLocal);
            h.panel.localPosition = h.edgeLocal + r * (h.closedPos - h.edgeLocal);
            h.panel.localRotation = r * h.closedRot;
        }

        private void ApplyPose(float e)
        {
            if (!_posesCached) return;
            switch (doorType)
            {
                case DoorType.SlidingSingle:
                    SinglePanel.localPosition = Vector3.Lerp(_singleClosed, _singleOpen, e);
                    break;
                case DoorType.SlidingDouble:
                    if (leftPanel != null) leftPanel.localPosition = Vector3.Lerp(_leftClosed, _leftOpen, e);
                    if (rightPanel != null) rightPanel.localPosition = Vector3.Lerp(_rightClosed, _rightOpen, e);
                    break;
                case DoorType.HingedSingle:
                    if (_usePivot) hingePivot.localRotation = Quaternion.Slerp(_pivotClosed, _pivotOpen, e);
                    else ApplyImplicitHinge(_impSingle, e);
                    break;
                case DoorType.HingedDouble:
                    if (_useLeftPivot) leftPivot.localRotation = Quaternion.Slerp(_leftPivotClosed, _leftPivotOpen, e);
                    else ApplyImplicitHinge(_impLeft, e);
                    if (_useRightPivot) rightPivot.localRotation = Quaternion.Slerp(_rightPivotClosed, _rightPivotOpen, e);
                    else ApplyImplicitHinge(_impRight, e);
                    break;
                case DoorType.LiftUp:
                    LiftPanel.localPosition = Vector3.Lerp(_liftClosed, _liftOpen, e);
                    break;
            }
        }

        // ============================== Диагностика ==============================

        /// <summary>
        /// Собрать проблемы настройки: errors — дверь не будет работать, warnings — подозрительно.
        /// Без UnityEditor API — можно звать и в рантайме.
        /// </summary>
        public void GetSetupIssues(List<string> errors, List<string> warnings)
        {
            if (errors == null || warnings == null) return;

            switch (doorType)
            {
                case DoorType.SlidingSingle:
                    CheckSlidePanel(SinglePanel, SlideSideToLocal(slideSide),
                        distanceMode, sizeFactor, manualDistance, "Створка", errors, warnings);
                    if (doorModel == null)
                        warnings.Add("Панель не задана — двигается сам объект вместе с коллайдером. " +
                                     "Для дверного проёма лучше child-панель.");
                    break;

                case DoorType.SlidingDouble:
                    if (leftPanel == null) errors.Add("Не задана левая створка (Left Panel).");
                    if (rightPanel == null) errors.Add("Не задана правая створка (Right Panel).");
                    {
                        Vector3 axisF = doubleAxis == DoubleSlideAxis.X_Horizontal ? Vector3.right : Vector3.forward;
                        if (leftPanel != null)
                            CheckSlidePanel(leftPanel, axisF, doubleDistanceMode,
                                doubleSizeFactor, doubleManualDistance, "Левая створка", errors, warnings);
                        if (rightPanel != null)
                            CheckSlidePanel(rightPanel, axisF, doubleDistanceMode,
                                doubleSizeFactor, doubleManualDistance, "Правая створка", errors, warnings);
                        if (leftPanel != null && rightPanel != null)
                            CheckDoubleArrangement(axisF, errors, warnings);
                    }
                    break;

                case DoorType.HingedSingle:
                    if (UseExplicitPivot(hingePivot, HingePanelOrSelf))
                    {
                        CheckHingePivot(hingePivot, hingePanel, "Пивот петель", errors, warnings);
                    }
                    else if (hingePivot != null)
                    {
                        warnings.Add($"Пивот «{hingePivot.name}» не ведёт створку — используется край створки. " +
                                     "Посадите панель под пивот (🔗) или очистите поле пивота.");
                        CheckImplicitPanel(HingePanelOrSelf, hingeSide == HingeSide.Left ? -1f : 1f,
                            (hingeSide == HingeSide.Left ? -1f : 1f) * openAngle, warnings);
                    }
                    else
                    {
                        // Неявный пивот: превью и игра работают сразу, без настройки.
                        CheckImplicitPanel(HingePanelOrSelf, hingeSide == HingeSide.Left ? -1f : 1f,
                            (hingeSide == HingeSide.Left ? -1f : 1f) * openAngle, warnings);
                    }
                    if (Mathf.Approximately(openAngle, 0f))
                        warnings.Add("Угол открытия = 0 — дверь не будет двигаться.");
                    break;

                case DoorType.HingedDouble:
                    CheckDoubleHingeSide(leftPivot, leftHingePanel, -1f, "Левый", "Левая створка", errors, warnings);
                    CheckDoubleHingeSide(rightPivot, rightHingePanel, 1f, "Правый", "Правая створка", errors, warnings);
                    break;

                case DoorType.LiftUp:
                    CheckSlidePanel(LiftPanel, Vector3.up, liftDistanceMode,
                        liftSizeFactor, liftManualHeight, "Шторка", errors, warnings);
                    break;
            }

            if (openDuration < 0.15f)
                warnings.Add("Очень быстрое открытие (< 0.15 c) — будет выглядеть как телепорт.");
        }

        /// <summary>
        /// Проверка петли: пивот задан, под ним есть геометрия,
        /// а панель (если задана) — его потомок. Иначе превью крутит пустоту,
        /// а гизмо-призрак летит без меша.
        /// </summary>
        private static void CheckHingePivot(Transform pivot, Transform panel, string label,
            List<string> errors, List<string> warnings)
        {
            if (pivot == null)
            {
                errors.Add($"{label} не задан — распашной двери не вокруг чего вращаться. " +
                           "Создайте кнопкой ⚙ в инспекторе.");
                return;
            }
            if (!HasGeometry(pivot))
            {
                errors.Add($"{label} «{pivot.name}» пустой: под ним нет ни меша, ни коллайдера — крутить нечего. " +
                           "Посадите створку ребёнком пивота (кнопка 🔗 в инспекторе).");
                return;
            }
            if (panel != null && !IsDescendantOf(panel, pivot))
                errors.Add($"Панель «{panel.name}» НЕ под «{pivot.name}» — пивот крутится, а створка стоит на месте. " +
                           "Посадите панель ребёнком пивота (кнопка 🔗 в инспекторе).");
        }

        /// <summary>Есть ли под объектом геометрия (меш или коллайдер, включая детей).</summary>
        public static bool HasGeometry(Transform t)
        {
            if (t == null) return false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                if (r != null) return true;
            foreach (var c in t.GetComponentsInChildren<Collider>(true))
                if (c != null) return true;
            return false;
        }

        private static bool IsDescendantOf(Transform t, Transform ancestor)
        {
            if (t == null || ancestor == null) return false;
            for (Transform p = t; p != null; p = p.parent)
                if (p == ancestor) return true;
            return false;
        }

        /// <summary>
        /// Проверка одной стороны двойной распашной: явный пивот (ведёт створку) →
        /// как обычно; чужой/пустой пивот → предупреждение + край створки;
        /// без пивота → край створки; без створки → ошибка.
        /// </summary>
        private void CheckDoubleHingeSide(Transform pivot, Transform panel, float edgeSide,
            string pivotAdj, string panelNoun, List<string> errors, List<string> warnings)
        {
            if (panel == null)
            {
                errors.Add($"{panelNoun} не задана — нечему открываться.");
                return;
            }
            if (UseExplicitPivot(pivot, panel))
            {
                CheckHingePivot(pivot, panel, pivotAdj + " пивот", errors, warnings);
                return;
            }
            if (pivot != null)
                warnings.Add($"{pivotAdj} пивот «{pivot.name}» не ведёт створку — используется край створки. " +
                             "Посадите панель под пивот (🔗) или очистите поле пивота.");
            CheckImplicitPanel(panel, edgeSide,
                edgeSide < 0f ? -(invertDoubleSwing ? -doubleOpenAngle : doubleOpenAngle)
                              : (invertDoubleSwing ? -doubleOpenAngle : doubleOpenAngle),
                warnings, panelNoun);
        }

        /// <summary>Проверка панели на неявном краю: есть геометрия и измеримый габарит.</summary>
        private void CheckImplicitPanel(Transform panel, float edgeSide, float angle,
            List<string> warnings, string label = "Створка")
        {
            if (!HasGeometry(panel))
            {
                warnings.Add($"{label}: под объектом нет геометрии — в превью будет видна только дужка поворота.");
                return;
            }
            if (!BuildImplicitHinge(panel, edgeSide, angle).valid)
                warnings.Add($"{label}: габарит не измерился — не вокруг чего строить край. " +
                             "Задайте меш/коллайдер.");
        }

        private void CheckSlidePanel(Transform panel, Vector3 frameAxis,
            OpenDistanceMode mode, float factor, float manual, string label,
            List<string> errors, List<string> warnings)
        {
            if (panel == null)
            {
                errors.Add($"{label}: нет панели.");
                return;
            }
            if (mode == OpenDistanceMode.AutoBySize)
            {
                if (!TryMeasurePanelSize(panel, transform, frameAxis, out float size))
                {
                    warnings.Add($"{label}: габарит не измерился (нет Renderer/Collider) — " +
                                 "будет использован ограниченный фолбэк 3 м. Задайте панели меш/коллайдер.");
                    return;
                }
                // Ловушка повёрнутых корней: ось смотрит в тонкую сторону панели
                // (как шторка ангара, поехавшая на 0.001) — дистанция исчезающе мала.
                float maxDim = MaxPanelDimension(panel);
                if (maxDim > 1e-4f && size < maxDim * 0.05f)
                    warnings.Add($"{label}: отъезд {size:F3} м — крохи на фоне габарита {maxDim:F2} м. " +
                                 "Направление смотрит в ТОНКУЮ сторону панели. " +
                                 "Проверьте направление или выровняйте рамку (кнопка 🧭).");
            }
            else
            {
                if (TryMeasurePanelSize(panel, transform, frameAxis, out float size) && size > 1e-4f)
                {
                    if (manual > size * 3f)
                        warnings.Add($"{label}: ручная дистанция {manual:F2} м намного больше габарита " +
                                     $"{size:F2} м — дверь «улетит». Включите «По габариту» или уменьшите.");
                    if (Mathf.Approximately(manual, 0f))
                        warnings.Add($"{label}: дистанция = 0 — дверь не будет двигаться.");
                }
            }
        }

        /// <summary>Максимальный габарит панели по трём осям рамки (в единицах родителя панели).</summary>
        private float MaxPanelDimension(Transform panel)
        {
            float best = 0f;
            foreach (var ax in new[] { Vector3.right, Vector3.up, Vector3.forward })
            {
                if (TryMeasurePanelSize(panel, transform, ax, out float s))
                    best = Mathf.Max(best, s);
            }
            return best;
        }

        /// <summary>
        /// Проверка расстановки створок разъезда: центры должны быть разнесены
        /// ВДОЛЬ оси разъезда. Иначе створки поедут поперёк друг друга.
        /// </summary>
        private void CheckDoubleArrangement(Vector3 frameAxis, List<string> errors, List<string> warnings)
        {
            Vector3 cl = transform.InverseTransformPoint(leftPanel.position);
            Vector3 cr = transform.InverseTransformPoint(rightPanel.position);
            Vector3 delta = cr - cl;
            float total = delta.magnitude;
            if (total < 1e-4f)
            {
                warnings.Add("Створки стоят в одной точке — разъезжаться некуда. Разнесите панели по проёму.");
                return;
            }
            float along = Mathf.Abs(Vector3.Dot(delta.normalized, frameAxis.normalized));
            if (along < 0.5f)
                warnings.Add("Створки разнесены ПОПЕРЁК оси разъезда — поедут не врозь, а друг за другом. " +
                             "Смените ось или нажмите «📏 Ось по створкам».");
        }

        // ============================== Editor-preview ==============================
#if UNITY_EDITOR
        // Закэширована ли закрытая поза для превью (чтобы повторный клик не брал открытую за закрытую)
        private bool _editPreviewActive;

        /// <summary>Пересчитать позы из текущих трансформов (для превью в Edit Mode).</summary>
        public void CachePosesInEditor()
        {
            EnsureMigrated();
            CacheClosedPoses();
            ComputeOpenFromCachedClosed();
            _editPreviewActive = false;
        }

        /// <summary>Мгновенно показать открыто/закрыто в Edit Mode (Undo — Ctrl+Z).</summary>
        public void ApplyPreviewPose(bool open)
        {
            EnsureMigrated();
            // Закрытую позу запоминаем один раз — иначе повторный «Открыть» возьмёт открытую за закрытую.
            if (!_editPreviewActive) CacheClosedPoses();
            _editPreviewActive = true;
            ComputeOpenFromCachedClosed();
            _openAmount = _targetAmount = open ? 1f : 0f;
            ApplyPose(SampleCurve(_openAmount));
        }

        /// <summary>Сбросить флаг превью (позы уже применены). Следующее превью закэширует заново.</summary>
        public void EndPreview()
        {
            _editPreviewActive = false;
        }

        private void OnDrawGizmosSelected()
        {
            try { DrawGizmosFromCurrent(); }
            catch { /* гизмо не должны ронять редактор */ }
        }

        // Гизмо считаются от ТЕКУЩИХ трансформов в локальных переменных —
        // поля закрытых поз превью не трогаем.
        private void DrawGizmosFromCurrent()
        {
            switch (doorType)
            {
                case DoorType.SlidingSingle:
                    if (SinglePanel != null)
                    {
                        Vector3 dirF = SlideSideToLocal(slideSide);
                        float d = ResolveSlideDistance(SinglePanel, transform, dirF, distanceMode, sizeFactor, manualDistance);
                        Vector3 off = FrameDirToPanelLocal(SinglePanel, dirF) * d;
                        DrawSlideGizmo(SinglePanel, SinglePanel.localPosition, SinglePanel.localPosition + off);
                    }
                    break;
                case DoorType.SlidingDouble:
                    {
                        Vector3 axisF = doubleAxis == DoubleSlideAxis.X_Horizontal ? Vector3.right : Vector3.forward;
                        if (leftPanel != null)
                        {
                            float dl = ResolveSlideDistance(leftPanel, transform, axisF, doubleDistanceMode, doubleSizeFactor, doubleManualDistance);
                            Vector3 off = FrameDirToPanelLocal(leftPanel, axisF) * dl;
                            DrawSlideGizmo(leftPanel, leftPanel.localPosition, leftPanel.localPosition - off);
                        }
                        if (rightPanel != null)
                        {
                            float dr = ResolveSlideDistance(rightPanel, transform, axisF, doubleDistanceMode, doubleSizeFactor, doubleManualDistance);
                            Vector3 off = FrameDirToPanelLocal(rightPanel, axisF) * dr;
                            DrawSlideGizmo(rightPanel, rightPanel.localPosition, rightPanel.localPosition + off);
                        }
                    }
                    break;
                case DoorType.HingedSingle:
                    if (UseExplicitPivot(hingePivot, HingePanelOrSelf))
                    {
                        float signed = (hingeSide == HingeSide.Left ? -1f : 1f) * openAngle;
                        DrawHingeGizmo(hingePivot, hingePivot.localRotation,
                            hingePivot.localRotation * Quaternion.Euler(0f, signed, 0f), hingePanel);
                    }
                    else
                    {
                        float sideSign = hingeSide == HingeSide.Left ? -1f : 1f;
                        DrawImplicitHingeGizmo(HingePanelOrSelf, sideSign, sideSign * openAngle);
                    }
                    break;
                case DoorType.HingedDouble:
                    {
                        float swing = invertDoubleSwing ? -doubleOpenAngle : doubleOpenAngle;
                        if (UseExplicitPivot(leftPivot, leftHingePanel))
                            DrawHingeGizmo(leftPivot, leftPivot.localRotation,
                                leftPivot.localRotation * Quaternion.Euler(0f, -swing, 0f), leftHingePanel);
                        else DrawImplicitHingeGizmo(leftHingePanel, -1f, -swing);
                        if (UseExplicitPivot(rightPivot, rightHingePanel))
                            DrawHingeGizmo(rightPivot, rightPivot.localRotation,
                                rightPivot.localRotation * Quaternion.Euler(0f, swing, 0f), rightHingePanel);
                        else DrawImplicitHingeGizmo(rightHingePanel, 1f, swing);
                    }
                    break;
                case DoorType.LiftUp:
                    if (LiftPanel != null)
                    {
                        float h = ResolveSlideDistance(LiftPanel, transform, Vector3.up, liftDistanceMode, liftSizeFactor, liftManualHeight);
                        Vector3 off = FrameDirToPanelLocal(LiftPanel, Vector3.up) * h;
                        DrawSlideGizmo(LiftPanel, LiftPanel.localPosition, LiftPanel.localPosition + off);
                    }
                    break;
            }
        }

        private void DrawSlideGizmo(Transform panel, Vector3 closedLocal, Vector3 openLocal)
        {
            if (panel == null) return;
            Transform space = panel.parent;
            Vector3 from = space != null ? space.TransformPoint(closedLocal) : closedLocal;
            Vector3 to = space != null ? space.TransformPoint(openLocal) : openLocal;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(from, to);
            Gizmos.DrawSphere(to, 0.12f);

            // Призрак открытого положения
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
            Gizmos.DrawWireCube(to, GetGhostSize(panel));
        }

        private void DrawHingeGizmo(Transform pivot, Quaternion closedLocal, Quaternion openLocal, Transform panel)
        {
            Vector3 center = pivot.position;
            float radius = 1f;
            if (panel != null)
            {
                Vector3 ext = GetGhostSize(panel);
                radius = Mathf.Max(ext.x, ext.z);
                if (radius < 0.2f) radius = 1f;
            }

            // Дужка поворота
            Gizmos.color = Color.green;
            Quaternion closedWorld = pivot.parent != null
                ? pivot.parent.rotation * closedLocal
                : closedLocal;
            Quaternion openWorld = pivot.parent != null
                ? pivot.parent.rotation * openLocal
                : openLocal;
            Vector3 fromDir = closedWorld * Vector3.forward;
            Vector3 toDir = openWorld * Vector3.forward;
            int steps = 12;
            Vector3 prev = center + fromDir * radius;
            for (int i = 1; i <= steps; i++)
            {
                Vector3 dir = Vector3.Slerp(fromDir, toDir, i / (float)steps).normalized;
                Vector3 p = center + dir * radius;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Gizmos.DrawSphere(center + toDir * radius, 0.12f);

            // Призрак: панель, повёрнутая в открытое положение
            if (panel != null)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
                Quaternion delta = openLocal * Quaternion.Inverse(closedLocal);
                Vector3 ghostCenter = center + delta * (panel.position - center);
                Gizmos.DrawWireCube(ghostCenter, GetGhostSize(panel));
            }
        }

        /// <summary>
        /// Дужка + призрак для НЕЯВНОГО пивота: край и ось считаются той же
        /// математикой, что в BuildImplicitHinge (поля не трогаем).
        /// </summary>
        private void DrawImplicitHingeGizmo(Transform panel, float edgeSide, float angle)
        {
            if (panel == null || panel.parent == null) return;
            if (Mathf.Approximately(angle, 0f)) return;

            bool hx = TryMeasurePanelSize(panel, transform, Vector3.right, out float sx);
            bool hz = TryMeasurePanelSize(panel, transform, Vector3.forward, out float sz);
            Vector3 widthAxisF = (hx && (!hz || sx >= sz)) ? Vector3.right : Vector3.forward;
            float w = Mathf.Max(hx ? sx : 0f, hz ? sz : 0f);
            if (w < 1e-4f) return;

            Transform space = panel.parent;
            Vector3 edgeDirP = FrameDirToPanelLocal(panel, widthAxisF * edgeSide);
            Vector3 edge = panel.localPosition + edgeDirP * (w * 0.5f);
            Vector3 a = space.InverseTransformDirection(Vector3.up);
            if (a.sqrMagnitude < 1e-8f) return;
            a.Normalize();

            Vector3 center = space.TransformPoint(edge);
            // Поворот из parent-пространства в мировое (только поворот, без скейла)
            Quaternion rFull = Quaternion.AngleAxis(angle, a);
            Quaternion rFullW = space.rotation * rFull * Quaternion.Inverse(space.rotation);

            Vector3 fromDir = panel.rotation * Vector3.forward;
            Vector3 toDir = rFullW * fromDir;
            float radius = Vector3.Distance(panel.position, center);
            if (radius < 0.2f) radius = Mathf.Max(GetGhostSize(panel).x, GetGhostSize(panel).z, 0.5f);

            Gizmos.color = Color.green;
            int steps = 12;
            Vector3 prev = center + fromDir * radius;
            for (int i = 1; i <= steps; i++)
            {
                Vector3 dir = Vector3.Slerp(fromDir, toDir, i / (float)steps).normalized;
                Vector3 p = center + dir * radius;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Gizmos.DrawSphere(center + toDir * radius, 0.12f);

            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
            Gizmos.DrawWireCube(center + rFullW * (panel.position - center), GetGhostSize(panel));
        }

        private static Vector3 GetGhostSize(Transform panel)
        {
            if (panel == null) return Vector3.one;
            bool has = false;
            Bounds b = new Bounds(panel.position, Vector3.zero);
            foreach (var r in panel.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!has)
            {
                foreach (var c in panel.GetComponentsInChildren<Collider>(true))
                {
                    if (c == null) continue;
                    if (!has) { b = c.bounds; has = true; }
                    else b.Encapsulate(c.bounds);
                }
            }
            return has ? b.size : Vector3.one * 0.5f;
        }
#endif
    }
}
