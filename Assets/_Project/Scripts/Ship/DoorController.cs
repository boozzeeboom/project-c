using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

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

        [Tooltip("Куда отъезжает створка (локальные оси родителя)")]
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

        /// <summary>Направление SlideSide → локальный вектор (в пространстве родителя).</summary>
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
        /// Замерить габарит панели вдоль локальной оси (в единицах родителя).
        /// Учитывает Renderer и Collider потомков. false — мерить нечего.
        /// </summary>
        public static bool TryMeasurePanelSize(Transform panel, Vector3 localAxis, out float localSize)
        {
            localSize = 0f;
            if (panel == null || localAxis.sqrMagnitude < 1e-8f) return false;

            Transform space = panel.parent;
            Vector3 worldDir = space != null
                ? space.TransformDirection(localAxis.normalized)
                : localAxis.normalized;
            if (worldDir.sqrMagnitude < 1e-8f) return false;
            worldDir.Normalize();

            bool has = false;
            Bounds bounds = new Bounds(panel.position, Vector3.zero);

            foreach (var r in panel.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            foreach (var c in panel.GetComponentsInChildren<Collider>(true))
            {
                if (c == null) continue;
                if (!has) { bounds = c.bounds; has = true; }
                else bounds.Encapsulate(c.bounds);
            }
            if (!has) return false;

            float worldSize = Mathf.Abs(Vector3.Dot(bounds.size, worldDir));
            if (worldSize < 1e-4f) return false;

            // Перевод в локальные единицы родителя (при scale=1 — 1:1).
            float scaleAlong = 1f;
            if (space != null)
            {
                Vector3 s = space.lossyScale;
                scaleAlong = Mathf.Abs(s.x * worldDir.x) + Mathf.Abs(s.y * worldDir.y) + Mathf.Abs(s.z * worldDir.z);
                scaleAlong = Mathf.Max(scaleAlong, 1e-4f);
            }
            localSize = worldSize / scaleAlong;
            return localSize >= 1e-4f;
        }

        /// <summary>Дистанция сдвига панели: габарит × фактор либо ручная.</summary>
        public static float ResolveSlideDistance(Transform panel, Vector3 localAxis,
            OpenDistanceMode mode, float factor, float manual)
        {
            if (mode == OpenDistanceMode.AutoBySize)
            {
                if (TryMeasurePanelSize(panel, localAxis, out float size))
                    return Mathf.Max(size * Mathf.Max(factor, 0.01f), 0f);
                // Мерить нечего — безопасный фолбэк вместо «улёта».
                return Mathf.Min(Mathf.Max(manual, 0f), 3f);
            }
            return Mathf.Max(manual, 0f);
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
        }

        private void ResolveOpenPoses()
        {
            if (!_posesCached) CacheClosedPoses();
            ComputeOpenFromCachedClosed();
        }

        /// <summary>Посчитать открытые позы из закэшированных закрытых. Чистая функция от полей.</summary>
        private void ComputeOpenFromCachedClosed()
        {
            Vector3 dir = SlideSideToLocal(slideSide);
            float d = ResolveSlideDistance(SinglePanel, dir, distanceMode, sizeFactor, manualDistance);
            _singleOpen = _singleClosed + dir.normalized * d;

            // Двустворчатая сдвижная
            Vector3 axis = doubleAxis == DoubleSlideAxis.X_Horizontal ? Vector3.right : Vector3.forward;
            float dl = leftPanel != null
                ? ResolveSlideDistance(leftPanel, axis, doubleDistanceMode, doubleSizeFactor, doubleManualDistance) : 0f;
            float dr = rightPanel != null
                ? ResolveSlideDistance(rightPanel, axis, doubleDistanceMode, doubleSizeFactor, doubleManualDistance) : 0f;
            _leftOpen = _leftClosed - axis * dl;
            _rightOpen = _rightClosed + axis * dr;

            // Распашная одна: петли слева → −угол, справа → +угол (вокруг локального Y пивота)
            float signed = (hingeSide == HingeSide.Left ? -1f : 1f) * openAngle;
            _pivotOpen = _pivotClosed * Quaternion.Euler(0f, signed, 0f);

            // Распашная двойная: зеркально
            float swing = invertDoubleSwing ? -doubleOpenAngle : doubleOpenAngle;
            _leftPivotOpen = _leftPivotClosed * Quaternion.Euler(0f, -swing, 0f);
            _rightPivotOpen = _rightPivotClosed * Quaternion.Euler(0f, swing, 0f);

            // Подъёмная шторка (закрытая поза закэширована — читаем только её)
            float h = ResolveSlideDistance(LiftPanel, Vector3.up, liftDistanceMode, liftSizeFactor, liftManualHeight);
            _liftOpen = _liftClosed + Vector3.up * h;
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
                    if (hingePivot != null) hingePivot.localRotation = Quaternion.Slerp(_pivotClosed, _pivotOpen, e);
                    break;
                case DoorType.HingedDouble:
                    if (leftPivot != null) leftPivot.localRotation = Quaternion.Slerp(_leftPivotClosed, _leftPivotOpen, e);
                    if (rightPivot != null) rightPivot.localRotation = Quaternion.Slerp(_rightPivotClosed, _rightPivotOpen, e);
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
                        Vector3 axis = doubleAxis == DoubleSlideAxis.X_Horizontal ? Vector3.right : Vector3.forward;
                        if (leftPanel != null)
                            CheckSlidePanel(leftPanel, axis, doubleDistanceMode,
                                doubleSizeFactor, doubleManualDistance, "Левая створка", errors, warnings);
                        if (rightPanel != null)
                            CheckSlidePanel(rightPanel, axis, doubleDistanceMode,
                                doubleSizeFactor, doubleManualDistance, "Правая створка", errors, warnings);
                    }
                    break;

                case DoorType.HingedSingle:
                    if (hingePivot == null)
                        errors.Add("Не задан пивот петель (Hinge Pivot) — распашной двери не вокруг чего вращаться. " +
                                   "Создайте кнопкой в инспекторе.");
                    if (Mathf.Approximately(openAngle, 0f))
                        warnings.Add("Угол открытия = 0 — дверь не будет двигаться.");
                    break;

                case DoorType.HingedDouble:
                    if (leftPivot == null) errors.Add("Не задан левый пивот (Left Pivot).");
                    if (rightPivot == null) errors.Add("Не задан правый пивот (Right Pivot).");
                    break;

                case DoorType.LiftUp:
                    CheckSlidePanel(LiftPanel, Vector3.up, liftDistanceMode,
                        liftSizeFactor, liftManualHeight, "Шторка", errors, warnings);
                    break;
            }

            if (openDuration < 0.15f)
                warnings.Add("Очень быстрое открытие (< 0.15 c) — будет выглядеть как телепорт.");
        }

        private static void CheckSlidePanel(Transform panel, Vector3 localAxis,
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
                if (!TryMeasurePanelSize(panel, localAxis, out _))
                    warnings.Add($"{label}: габарит не измерился (нет Renderer/Collider) — " +
                                 "будет использован ограниченный фолбэк 3 м. Задайте панели меш/коллайдер.");
            }
            else
            {
                if (TryMeasurePanelSize(panel, localAxis, out float size) && size > 1e-4f)
                {
                    if (manual > size * 3f)
                        warnings.Add($"{label}: ручная дистанция {manual:F2} м намного больше габарита " +
                                     $"{size:F2} м — дверь «улетит». Включите «По габариту» или уменьшите.");
                    if (Mathf.Approximately(manual, 0f))
                        warnings.Add($"{label}: дистанция = 0 — дверь не будет двигаться.");
                }
            }
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
                        Vector3 dir = SlideSideToLocal(slideSide);
                        float d = ResolveSlideDistance(SinglePanel, dir, distanceMode, sizeFactor, manualDistance);
                        DrawSlideGizmo(SinglePanel, SinglePanel.localPosition, SinglePanel.localPosition + dir.normalized * d);
                    }
                    break;
                case DoorType.SlidingDouble:
                    {
                        Vector3 axis = doubleAxis == DoubleSlideAxis.X_Horizontal ? Vector3.right : Vector3.forward;
                        if (leftPanel != null)
                        {
                            float dl = ResolveSlideDistance(leftPanel, axis, doubleDistanceMode, doubleSizeFactor, doubleManualDistance);
                            DrawSlideGizmo(leftPanel, leftPanel.localPosition, leftPanel.localPosition - axis * dl);
                        }
                        if (rightPanel != null)
                        {
                            float dr = ResolveSlideDistance(rightPanel, axis, doubleDistanceMode, doubleSizeFactor, doubleManualDistance);
                            DrawSlideGizmo(rightPanel, rightPanel.localPosition, rightPanel.localPosition + axis * dr);
                        }
                    }
                    break;
                case DoorType.HingedSingle:
                    if (hingePivot != null)
                    {
                        float signed = (hingeSide == HingeSide.Left ? -1f : 1f) * openAngle;
                        DrawHingeGizmo(hingePivot, hingePivot.localRotation,
                            hingePivot.localRotation * Quaternion.Euler(0f, signed, 0f), hingePanel);
                    }
                    break;
                case DoorType.HingedDouble:
                    {
                        float swing = invertDoubleSwing ? -doubleOpenAngle : doubleOpenAngle;
                        if (leftPivot != null)
                            DrawHingeGizmo(leftPivot, leftPivot.localRotation,
                                leftPivot.localRotation * Quaternion.Euler(0f, -swing, 0f), leftHingePanel);
                        if (rightPivot != null)
                            DrawHingeGizmo(rightPivot, rightPivot.localRotation,
                                rightPivot.localRotation * Quaternion.Euler(0f, swing, 0f), rightHingePanel);
                    }
                    break;
                case DoorType.LiftUp:
                    if (LiftPanel != null)
                    {
                        float h = ResolveSlideDistance(LiftPanel, Vector3.up, liftDistanceMode, liftSizeFactor, liftManualHeight);
                        DrawSlideGizmo(LiftPanel, LiftPanel.localPosition, LiftPanel.localPosition + Vector3.up * h);
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
