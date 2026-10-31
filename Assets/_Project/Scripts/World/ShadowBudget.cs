// Project C: бюджет теней для дальних корней геометрии (T-CULL01 / Stage 2b, H3)
// Слой дистанции тени не режет: дальняя геометрия продолжает попадать в shadow-culling.
// Здесь по именам корней (дефолт Primum_farms) гасится Renderer.shadowCastingMode — группами,
// по дистанции до ближайшей геймплейной камеры, с гистерезисом и обновлением ~3 Гц.
// Фермы стоят за несколько километров и в кадр теней не попадают; если игрок подлетит —
// тени включаются обратно по тому же гистерезису, поэтому визуал не ломается навсегда.
// Что НЕ делает: не трогает меши, материалы, коллайдеры, Light и SetActive.
// Floating Origin: мировые позиции не кэшируются, группы читаются живьём каждый тик.
// Хостер — тот же скрытый DontDestroyOnLoad-объект ViewDistanceRuntime. Camera.main не используется.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProjectC.World
{
    [DisallowMultipleComponent]
    public class ShadowBudget : MonoBehaviour
    {
        [Header("Корни дальней геометрии")]
        [Tooltip("Имена корней в сцене мира. Группа = прямой ребёнок корня (ферма/чанк).")]
        [SerializeField] private string[] farRootNames = { "Primum_farms" };

        [Header("Порог теней")]
        [Tooltip("Ближе этого расстояния тени включаются обратно (гистерезис).")]
        [SerializeField] private float shadowOnDistance = 50f;

        [Tooltip("Дальше этого расстояния тени выключаются (гистерезис).")]
        [SerializeField] private float shadowOffDistance = 60f;

        [Tooltip("Частота обновления бюджета, с. 2–4 Гц.")]
        [SerializeField] private float updateInterval = 0.33f;

        [Tooltip("Как часто пробовать найти корни, если их ещё нет (сцена мира грузится позже).")]
        [SerializeField] private float rebuildInterval = 2f;

        [Tooltip("Слой дверей игнорируется здесь: его тени ведёт DoorVisibilityBudget.")]
        [SerializeField] private int skippedLayer = 10;

        private class ShadowGroup
        {
            public Transform root;
            public readonly List<Renderer> renderers = new List<Renderer>();
            public readonly List<ShadowCastingMode> baseModes = new List<ShadowCastingMode>();
            public bool shadowsOn = true;
        }

        private readonly List<ShadowGroup> _groups = new List<ShadowGroup>();
        private readonly List<Camera> _cameras = new List<Camera>();

        private float _cameraTimer;
        private float _updateTimer;
        private float _rebuildTimer;
        private bool _needRebuild;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Build();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Restore();
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            _needRebuild = true;
        }

        private void Update()
        {
            _cameraTimer += Time.unscaledDeltaTime;
            if (_cameraTimer >= 1f)
            {
                _cameraTimer = 0f;
                RefreshCameras();
            }

            _rebuildTimer += Time.unscaledDeltaTime;
            if ((_needRebuild || _groups.Count == 0) && _rebuildTimer >= rebuildInterval)
            {
                _rebuildTimer = 0f;
                Build();
            }

            if (_groups.Count == 0 || _cameras.Count == 0) return;

            _updateTimer += Time.unscaledDeltaTime;
            if (_updateTimer < updateInterval) return;
            _updateTimer = 0f;
            UpdateShadows();
        }

        private void Build()
        {
            _needRebuild = false;
            _rebuildTimer = 0f;
            _groups.Clear();
            if (farRootNames == null || farRootNames.Length == 0) return;

            var transforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || !IsFarRoot(t.name)) continue;

                if (t.childCount == 0)
                {
                    AddGroup(t);
                }
                else
                {
                    for (int c = 0; c < t.childCount; c++) AddGroup(t.GetChild(c));
                }
            }

            if (_groups.Count > 0)
                Debug.Log("[ShadowBudget] Групп дальней геометрии: " + _groups.Count + ", рендереров: " + CountRenderers() +
                          ", тени off дальше " + shadowOffDistance.ToString("F0") + " м.");
        }

        private bool IsFarRoot(string name)
        {
            for (int i = 0; i < farRootNames.Length; i++)
                if (!string.IsNullOrEmpty(farRootNames[i]) && farRootNames[i] == name) return true;
            return false;
        }

        private void AddGroup(Transform t)
        {
            if (t == null) return;
            var renderers = t.GetComponentsInChildren<Renderer>(includeInactive: false);
            if (renderers == null || renderers.Length == 0) return;

            var group = new ShadowGroup { root = t, shadowsOn = true };
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.gameObject.layer == skippedLayer) continue;
                group.renderers.Add(r);
                group.baseModes.Add(r.shadowCastingMode);
            }
            if (group.renderers.Count > 0) _groups.Add(group);
        }

        private int CountRenderers()
        {
            int n = 0;
            for (int i = 0; i < _groups.Count; i++) n += _groups[i].renderers.Count;
            return n;
        }

        private void RefreshCameras()
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
                _cameras.Add(cam);
            }
        }

        private void UpdateShadows()
        {
            for (int g = _groups.Count - 1; g >= 0; g--)
            {
                ShadowGroup group = _groups[g];
                if (group.root == null || group.renderers.Count == 0)
                {
                    _groups.RemoveAt(g);
                    _needRebuild = true;
                    continue;
                }

                float nearestSqr = float.MaxValue;
                Vector3 rootPos = group.root.position;
                for (int c = 0; c < _cameras.Count; c++)
                {
                    var cam = _cameras[c];
                    if (cam == null) continue;
                    float d = (rootPos - cam.transform.position).sqrMagnitude;
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

        private void Restore()
        {
            for (int g = 0; g < _groups.Count; g++)
            {
                var group = _groups[g];
                for (int r = 0; r < group.renderers.Count; r++)
                {
                    var renderer = group.renderers[r];
                    if (renderer == null) continue;
                    renderer.shadowCastingMode = group.baseModes[r];
                }
            }
            _groups.Clear();
            _cameras.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<ShadowBudget>() != null) return;

            var host = GameObject.Find(DoorVisibilityBudget.HostName);
            if (host == null)
            {
                host = new GameObject(DoorVisibilityBudget.HostName);
                DontDestroyOnLoad(host);
            }
            if (host.GetComponent<ShadowBudget>() == null)
                host.AddComponent<ShadowBudget>();
        }

        private void OnValidate()
        {
            if (shadowOffDistance < shadowOnDistance) shadowOffDistance = shadowOnDistance;
            if (updateInterval < 0.1f) updateInterval = 0.1f;
            if (rebuildInterval < 0.1f) rebuildInterval = 0.1f;
        }
    }
}
