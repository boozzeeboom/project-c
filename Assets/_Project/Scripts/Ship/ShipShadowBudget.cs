// Project C: бюджет теней корабля (T-CULL01 / Stage 3c — закрытие P2 для Ship_havyII_base)
//
// Зачем: на 200 м корабль целиком в кадре рисуется LOD0 (порог LOD0 у этого LODGroup не может
// быть меньше ~281.6 м: screenRelativeTransitionHeight ограничен 1.0 при worldSize 324.85 м),
// поэтому 557 статических рендереров LOD0 дают 1 538 DC. Слой дистанции тени не режет —
// значит дешевле всего убрать ТОЛЬКО теневой проход мелкой статики.
//
// Что делает: у мелких статических частей (габарит <= smallPartMaxAxis) гасит
// Renderer.shadowCastingMode, когда САМ КОРАБЛЬ дальше shadowOffDistance от ближайшей
// геймплейной камеры, и возвращает авторский режим, когда корабль ближе shadowOnDistance.
// Решение принимается по дистанции до корабля (localReferencePoint его LODGroup), а не по
// дистанции до каждой части: корпус 325 м, и при камере в 200–300 м его ближняя половина
// всё равно ближе 150 м — поштучное правило снимало ~30 частей из 314 (замер Stage 3c:
// 1538 → 1434 DC). Дистанция до корабля срабатывает ровно один раз — на переходе через порог.
// Крупные части (корпус, палубы, башни) не трогаются вообще — они не проходят фильтр габарита.
//
// Что НЕ делает: не меняет SetActive, Renderer.enabled, коллайдеры, DoorController, материалы,
// LOD-пороги и сцену; не создаёт объектов; не кэширует мировые позиции (Floating Origin):
// дистанции считаются от текущих позиций каждый тик.
//
// Область действия — ровно этот корабль: компонент живёт на корне префаба и работает только
// с рендерерами своего LODGroup. Двери/створки, ModuleSlot, PilotSeat, CargoVisual, двигатели
// и VFX вынесены мержем (H2) ЗА пределы LODGroup, поэтому в выборку не попадают by design.
//
// Только Play Mode: OnEnable не строит список вне Application.isPlaying, иначе
// shadowCastingMode переписался бы в сериализуемый префаб/сцену (так в Stage 2 появились
// 5 346 override'ов m_CastShadows: 0). OnDisable возвращает исходные режимы.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectC.Ship
{
    [DisallowMultipleComponent]
    public class ShipShadowBudget : MonoBehaviour
    {
        [Header("Бюджет теней мелкой статики (Stage 3c / P2)")]
        [Tooltip("Выключить, чтобы вернуть авторские тени без снятия компонента.")]
        [SerializeField] private bool budgetEnabled = true;

        [Tooltip("Ближе этого расстояния (м) до корабля тень мелкой статики включается обратно — гистерезис.")]
        [SerializeField] private float shadowOnDistance = 140f;

        [Tooltip("Дальше этого расстояния (м) до корабля тень мелкой статики выключается целиком.")]
        [SerializeField] private float shadowOffDistance = 150f;

        [Tooltip("Мелкая статика: максимальная ось мирового габарита, м. Корпус и палубы крупнее — не трогаются.")]
        [SerializeField] private float smallPartMaxAxis = 8f;

        [Tooltip("Частота обновления бюджета, с. 3 Гц достаточно: дистанции меняются плавно.")]
        [SerializeField] private float updateInterval = 0.33f;

        [Tooltip("Как часто пере-проверять набор геймплейных камер, с.")]
        [SerializeField] private float cameraRescanInterval = 1f;

        [SerializeField] private bool logOnBuild = true;

        /// <summary>Одна отслеживаемая часть: её авторский режим теней и текущее состояние.</summary>
        private sealed class Entry
        {
            public Renderer renderer;
            public ShadowCastingMode baseMode;
            public bool shadowsOn = true;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Camera> _cameras = new List<Camera>();

        private LODGroup _lodGroup;
        private int _rendererLayerMask;
        private float _updateTimer;
        private float _cameraTimer;
        private bool _warnedNoLod;

        /// <summary>Текущее решение бюджета: тени мелкой статики включены или сняты.</summary>
        private bool _anchorShadowsOn = true;

        /// <summary>Сколько частей реально участвует в бюджете (диагностика/тул).</summary>
        public int TrackedRendererCount { get { return _entries.Count; } }

        public bool BudgetEnabled { get { return budgetEnabled; } }
        public float ShadowOnDistance { get { return shadowOnDistance; } }
        public float ShadowOffDistance { get { return shadowOffDistance; } }
        public float SmallPartMaxAxis { get { return smallPartMaxAxis; } }

        private void OnEnable()
        {
            // Только Play Mode — см. шапку: в edit mode переписывание shadowCastingMode
            // попадает в сериализуемый префаб/сцену.
            if (!Application.isPlaying) return;
            Build();
        }

        private void OnDisable()
        {
            RestoreShadows();
        }

        private void OnDestroy()
        {
            RestoreShadows();
        }

        private void Update()
        {
            if (!Application.isPlaying || !budgetEnabled) return;

            if (!_built())
            {
                _updateTimer += Time.unscaledDeltaTime;
                if (_updateTimer < 2f) return;
                _updateTimer = 0f;
                Build();
                return;
            }

            _cameraTimer += Time.unscaledDeltaTime;
            if (_cameraTimer >= cameraRescanInterval)
            {
                _cameraTimer = 0f;
                RefreshCameras();
            }

            if (_cameras.Count == 0) return;

            _updateTimer += Time.unscaledDeltaTime;
            if (_updateTimer < updateInterval) return;
            _updateTimer = 0f;
            UpdateShadows();
        }

        private bool _built()
        {
            return _entries.Count > 0 && _lodGroup != null;
        }

        // ------------------------------------------------------------------ сборка

        private void Build()
        {
            _entries.Clear();
            _lodGroup = GetComponentInChildren<LODGroup>(true);

            if (_lodGroup == null)
            {
                if (!_warnedNoLod && logOnBuild)
                {
                    _warnedNoLod = true;
                    Debug.LogWarning("[ShipShadowBudget] На " + name + " нет LODGroup — бюджет теней не работает.");
                }
                return;
            }

            var candidates = new List<MeshRenderer>(256);
            CollectCandidates(gameObject, _lodGroup, smallPartMaxAxis, candidates);

            int slots = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                var r = candidates[i];
                if (r == null) continue;
                if (r.shadowCastingMode == ShadowCastingMode.Off) continue; // уже не кастер
                _entries.Add(new Entry { renderer = r, baseMode = r.shadowCastingMode, shadowsOn = true });
                slots += r.sharedMaterials.Length;
            }

            _rendererLayerMask = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                var r = _entries[i].renderer;
                if (r != null) _rendererLayerMask |= 1 << r.gameObject.layer;
            }

            RefreshCameras();

            if (logOnBuild)
                Debug.Log("[ShipShadowBudget] " + name + ": мелких частей " + _entries.Count + " (слотов " + slots +
                          "), решение по дистанции до корабля: off дальше " + shadowOffDistance.ToString("F0") +
                          " м, обратно ближе " + shadowOnDistance.ToString("F0") + " м, порог габарита " +
                          smallPartMaxAxis.ToString("F0") + " м, камер " + _cameras.Count + ".");
        }

        /// <summary>
        /// Мелкая статика корабля: рендерер входит в LODGroup корабля и его мировой габарит
        /// не больше <paramref name="maxAxis"/>. Принадлежность к LODGroup — это и есть
        /// фильтр интерактива: мерж H2 оставил вне LODGroup все двери/створки, ModuleSlot,
        /// PilotSeat, CargoVisual, двигатели и VFX.
        /// Используется и рантаймом, и editor-тулом (одинаковая классификация в отчёте).
        /// </summary>
        public static void CollectCandidates(GameObject shipRoot, LODGroup lodGroup, float maxAxis, List<MeshRenderer> result)
        {
            result.Clear();
            if (shipRoot == null || lodGroup == null) return;

            var levels = lodGroup.GetLODs();
            for (int level = 0; level < levels.Length; level++)
            {
                var renderers = levels[level].renderers;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var mr = renderers[i] as MeshRenderer;
                    if (mr == null || !mr.enabled) continue;
                    if (!mr.gameObject.activeInHierarchy) continue;
                    var b = mr.bounds;
                    float axis = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    if (axis > maxAxis) continue;
                    result.Add(mr);
                }
            }
        }

        // ------------------------------------------------------------------ камеры

        /// <summary>
        /// Геймплейные камеры: тип Game, включена, без RenderTexture, рабочий far и в маске
        /// есть слой хотя бы одной отслеживаемой части. Camera.main не используется;
        /// AtmosphereCamera (far 10) отсекается по far.
        /// </summary>
        private void RefreshCameras()
        {
            _cameras.Clear();
            var all = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                var cam = all[i];
                if (cam == null || !cam.isActiveAndEnabled) continue;
                if (cam.cameraType != CameraType.Game) continue;
                if (cam.targetTexture != null) continue;
                if (cam.farClipPlane < 50f) continue;
                if ((cam.cullingMask & _rendererLayerMask) == 0) continue;
                _cameras.Add(cam);
            }
        }

        // -------------------------------------------------------------------- тени

        /// <summary>
        /// Решение принимается по дистанции до КОРАБЛЯ, а не до каждой части: корпус 325 м, поэтому
        /// поштучное правило при 200–300 м снимало только дальний хвост (~30 частей из 314, замер
        /// Stage 3c: 1538 → 1434 DC). Дистанция до корабля переключает весь бюджетный набор за один шаг
        /// и даёт предсказуемый выигрыш на любой позе.
        /// </summary>
        private void UpdateShadows()
        {
            float shipDistance = NearestCameraDistance(ShipAnchor());
            if (shipDistance < 0f) return;

            bool want = _anchorShadowsOn ? shipDistance <= shadowOffDistance : shipDistance < shadowOnDistance;
            if (want == _anchorShadowsOn) return;

            _anchorShadowsOn = want;
            ApplyShadows(want);
        }

        private void ApplyShadows(bool on)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var renderer = entry.renderer;
                if (renderer == null) continue;

                renderer.shadowCastingMode = on ? entry.baseMode : ShadowCastingMode.Off;
                entry.shadowsOn = on;
            }
        }

        /// <summary>
        /// Точка, от которой LODGroup считает дистанции переключения уровней (`localReferencePoint`),
        /// в мире. Берётся из текущих трансформов каждый тик — кэша мировых координат нет (Floating Origin).
        /// </summary>
        private Vector3 ShipAnchor()
        {
            return _lodGroup != null
                ? _lodGroup.transform.TransformPoint(_lodGroup.localReferencePoint)
                : transform.position;
        }

        /// <summary>Дистанция до ближайшей геймплейной камеры; -1, если камер нет.</summary>
        private float NearestCameraDistance(Vector3 point)
        {
            float best = float.MaxValue;
            for (int c = 0; c < _cameras.Count; c++)
            {
                var cam = _cameras[c];
                if (cam == null) continue;
                float d = (point - cam.transform.position).sqrMagnitude;
                if (d < best) best = d;
            }
            return best == float.MaxValue ? -1f : Mathf.Sqrt(best);
        }

        /// <summary>
        /// Возврат авторских режимов теней. Нужен потому, что сцена не перезагружается на выходе
        /// из Play Mode (EnterPlayModeOptions): runtime-значения shadowCastingMode остались бы
        /// в объектах и могли попасть в сейв.
        /// </summary>
        public void RestoreShadows()
        {
            _anchorShadowsOn = true;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.renderer == null) continue;
                entry.renderer.shadowCastingMode = entry.baseMode;
                entry.shadowsOn = true;
            }
        }

        private void OnValidate()
        {
            if (shadowOffDistance < shadowOnDistance) shadowOffDistance = shadowOnDistance;
            if (smallPartMaxAxis < 0.1f) smallPartMaxAxis = 0.1f;
            if (updateInterval < 0.1f) updateInterval = 0.1f;
            if (cameraRescanInterval < 0.1f) cameraRescanInterval = 0.1f;
        }
    }
}
