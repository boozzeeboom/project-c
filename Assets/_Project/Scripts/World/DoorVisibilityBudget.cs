// Project C: бюджет видимости дверей (T-CULL01 / Stage 2, H3)
// Двери города и ферм вынесены на слой PRIMUM_DOORS (10) — здесь два независимых механизма:
//   1) Camera.layerCullDistances[10] — дистанция прорисовки дверей для геймплейных камер;
//      сцену не правит, камера runtime-рига создаётся после загрузки мира, поэтому камеры
//      пере-проверяются раз в секунду. AtmosphereCamera (far 10, слой PCMM_Atmosphere) не
//      попадает в выборку: она не рисует слой дверей и не должна задавать их дальность.
//   2) Бюджет теней: слой дистанции тени НЕ режет, поэтому shadowCastingMode гасится отдельно,
//      группами (корень двери = её Collider), с гистерезисом 50/60 м и обновлением ~3 Гц.
// Что НЕ делает: не трогает DoorController, коллайдеры, материалы и SetActive.
// Floating Origin: мировые позиции не кэшируются — камеры и группы читаются живьём каждый тик.
// Хостер — скрытый DontDestroyOnLoad-объект ViewDistanceRuntime (тот же, что у T-LOD01),
// чтобы не появлялся второй служебный корень. Camera.main не используется.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ProjectC.Ship;

namespace ProjectC.World
{
    [DisallowMultipleComponent]
    public class DoorVisibilityBudget : MonoBehaviour
    {
        public const string DoorLayerName = "PRIMUM_DOORS";
        public const int DoorLayer = 10;
        public const string HostName = "ViewDistanceRuntime";

        [Header("Дистанция прорисовки дверей (Camera.layerCullDistances)")]
        [Tooltip("Метры. 0 = не ограничивать. Тени слоем не режутся — для них бюджет ниже.")]
        [SerializeField] private float doorCullDistance = 150f;

        [Tooltip("Как часто пере-проверять геймплейные камеры (с): риг камеры создаётся после загрузки мира.")]
        [SerializeField] private float cameraRescanInterval = 1f;

        [Header("Бюджет теней дверей (Renderer.shadowCastingMode)")]
        [SerializeField] private bool shadowBudgetEnabled = true;

        [Tooltip("Ближе этого расстояния тени включаются обратно (гистерезис).")]
        [SerializeField] private float shadowOnDistance = 50f;

        [Tooltip("Дальше этого расстояния тени выключаются (гистерезис).")]
        [SerializeField] private float shadowOffDistance = 60f;

        [Tooltip("Частота обновления бюджета, с. 2–4 Гц: не переписываем рендереры каждый кадр.")]
        [SerializeField] private float shadowUpdateInterval = 0.33f;

        [Tooltip("Как часто пробовать собрать группы, если их ещё нет (сцена мира грузится позже).")]
        [SerializeField] private float rebuildInterval = 2f;

        /// <summary>Группа двери: корень с коллайдером + его рендереры с исходными режимами теней.</summary>
        private class DoorGroup
        {
            public Transform root;
            public readonly List<Renderer> renderers = new List<Renderer>();
            public readonly List<ShadowCastingMode> baseModes = new List<ShadowCastingMode>();
            public bool shadowsOn = true;
        }

        private readonly List<DoorGroup> _groups = new List<DoorGroup>();
        private readonly List<Camera> _cameras = new List<Camera>();

        private int _doorMask;
        private float _cameraTimer;
        private float _shadowTimer;
        private float _rebuildTimer;
        private bool _needRebuild;
        private bool _warnedMissingLayer;

        private void Awake()
        {
            _doorMask = 1 << DoorLayer;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Build();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RestoreCameras();
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            _needRebuild = true;
            _cameraTimer = cameraRescanInterval;
        }

        private void Update()
        {
            _cameraTimer += Time.unscaledDeltaTime;
            if (_cameraTimer >= cameraRescanInterval)
            {
                _cameraTimer = 0f;
                RefreshCameras(true);
            }

            _rebuildTimer += Time.unscaledDeltaTime;
            if ((_needRebuild || _groups.Count == 0) && _rebuildTimer >= rebuildInterval)
            {
                _rebuildTimer = 0f;
                Build();
            }

            if (!shadowBudgetEnabled || _groups.Count == 0 || _cameras.Count == 0) return;

            _shadowTimer += Time.unscaledDeltaTime;
            if (_shadowTimer < shadowUpdateInterval) return;
            _shadowTimer = 0f;
            UpdateShadows();
        }

        // ---------------------------------------------------------------- группы

        private void Build()
        {
            _needRebuild = false;
            _rebuildTimer = 0f;
            _groups.Clear();

            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var map = new Dictionary<Transform, DoorGroup>();

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.gameObject.layer != DoorLayer) continue;

                Transform root = ResolveDoorRoot(r.transform);
                if (root == null) continue;

                DoorGroup group;
                if (!map.TryGetValue(root, out group))
                {
                    group = new DoorGroup { root = root, shadowsOn = true };
                    map[root] = group;
                    _groups.Add(group);
                }

                group.renderers.Add(r);
                group.baseModes.Add(r.shadowCastingMode);
            }

            if (_groups.Count == 0 && !_warnedMissingLayer)
            {
                _warnedMissingLayer = true;
                Debug.LogWarning("[DoorBudget] На слое " + DoorLayer + " (" + DoorLayerName + ") нет ни одного рендерера " +
                                 "двери: дистанция прорисовки и бюджет теней не работают. " +
                                 "Назначьте слой дверным префабам (Project C → Culling → Door Layer).");
            }
            else if (_groups.Count > 0)
            {
                _warnedMissingLayer = false;
                Debug.Log("[DoorBudget] Групп дверей: " + _groups.Count + ", рендереров: " + CountRenderers() +
                          ", дистанция: " + doorCullDistance.ToString("F0") + " м, тени off дальше " +
                          shadowOffDistance.ToString("F0") + " м.");
            }
        }

        private int CountRenderers()
        {
            int n = 0;
            for (int i = 0; i < _groups.Count; i++) n += _groups[i].renderers.Count;
            return n;
        }

        /// <summary>
        /// Корень двери = ближайший предок с DoorController (авторитетная единица двери: на нём
        /// и коллайдер, и створки), иначе ближайший предок с коллайдером. Без этого деления
        /// группами становились бы меши с собственными коллайдерами (5488 вместо 2598).
        /// </summary>
        private static Transform ResolveDoorRoot(Transform t)
        {
            Transform colliderRoot = null;
            var node = t;
            for (int depth = 0; depth < 8 && node != null; depth++)
            {
                if (node.GetComponent<DoorController>() != null) return node;
                if (colliderRoot == null && node.GetComponent<Collider>() != null) colliderRoot = node;
                node = node.parent;
            }
            if (colliderRoot != null) return colliderRoot;
            return t.parent != null ? t.parent : t;
        }

        // --------------------------------------------------------------- камеры

        /// <summary>
        /// Геймплейные камеры: тип Game, без RenderTexture, рисуют слой дверей и имеют
        /// рабочий far (AtmosphereCamera меню имеет far 10 и слой PCMM_Atmosphere — отсекается).
        /// </summary>
        private void RefreshCameras(bool applyDistance)
        {
            _cameras.Clear();
            // Camera.allCameras заполняется только во время рендера (в edit mode он пуст),
            // поэтому перебираем объекты так же, как ViewDistanceApplier.
            var all = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var cam = all[i];
                if (cam == null || !cam.isActiveAndEnabled) continue;
                if (cam.cameraType != CameraType.Game) continue;
                if (cam.targetTexture != null) continue;
                if (cam.farClipPlane < 50f) continue;
                if ((cam.cullingMask & _doorMask) == 0) continue;

                _cameras.Add(cam);
                if (applyDistance) ApplyLayerDistance(cam);
            }
        }

        private void ApplyLayerDistance(Camera cam)
        {
            float want = doorCullDistance > 0f ? doorCullDistance : 0f;
            float[] current = cam.layerCullDistances;
            if (current != null && current.Length > DoorLayer && Mathf.Approximately(current[DoorLayer], want)) return;

            var arr = new float[32];
            if (current != null)
            {
                int copy = Mathf.Min(current.Length, 32);
                for (int i = 0; i < copy; i++) arr[i] = current[i];
            }
            arr[DoorLayer] = want;
            cam.layerCullDistances = arr;
            cam.layerCullSpherical = false;
        }

        private void RestoreCameras()
        {
            for (int i = 0; i < _cameras.Count; i++)
            {
                var cam = _cameras[i];
                if (cam == null) continue;
                float[] current = cam.layerCullDistances;
                if (current == null || current.Length <= DoorLayer || current[DoorLayer] <= 0f) continue;
                current[DoorLayer] = 0f;
                cam.layerCullDistances = current;
            }
            _cameras.Clear();
        }

        // ---------------------------------------------------------------- тени

        private void UpdateShadows()
        {
            for (int g = _groups.Count - 1; g >= 0; g--)
            {
                DoorGroup group = _groups[g];
                if (group.root == null || group.renderers.Count == 0)
                {
                    _groups.RemoveAt(g);
                    _needRebuild = true;
                    continue;
                }

                // Union по камерам: Renderer.enabled/shadowCastingMode глобальны, поэтому
                // тени нужны, пока дверь близка хотя бы к одной геймплейной камере.
                float nearestSqr = float.MaxValue;
                Vector3 doorPos = group.root.position;
                for (int c = 0; c < _cameras.Count; c++)
                {
                    var cam = _cameras[c];
                    if (cam == null) continue;
                    float d = (doorPos - cam.transform.position).sqrMagnitude;
                    if (d < nearestSqr) nearestSqr = d;
                }
                if (nearestSqr == float.MaxValue) continue;

                float distance = Mathf.Sqrt(nearestSqr);
                bool want = group.shadowsOn ? distance <= shadowOffDistance : distance < shadowOnDistance;
                if (want == group.shadowsOn) continue;

                group.shadowsOn = want;
                for (int r = 0; r < group.renderers.Count; r++)
                {
                    var renderer = group.renderers[r];
                    if (renderer == null) continue;
                    renderer.shadowCastingMode = want ? group.baseModes[r] : ShadowCastingMode.Off;
                }
            }
        }

        // ------------------------------------------------------------ хостинг

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<DoorVisibilityBudget>() != null) return;

            var host = GameObject.Find(HostName);
            if (host == null)
            {
                host = new GameObject(HostName);
                DontDestroyOnLoad(host);
            }
            if (host.GetComponent<DoorVisibilityBudget>() == null)
                host.AddComponent<DoorVisibilityBudget>();
        }

        private void OnValidate()
        {
            if (shadowOffDistance < shadowOnDistance) shadowOffDistance = shadowOnDistance;
            if (shadowUpdateInterval < 0.1f) shadowUpdateInterval = 0.1f;
            if (cameraRescanInterval < 0.1f) cameraRescanInterval = 0.1f;
            if (rebuildInterval < 0.1f) rebuildInterval = 0.1f;
        }
    }
}
