// Project C: городской бюджет теней (T-CULL01 / Stage 4b, H5b)
//
// Зачем: после включения GPU Resident Drawer (Stage 4) DC на площади держали 3 031 при гейте 2 500.
// Замер Stage 4b показал, что теневой проход — это ~1 254 draw call из 3 047, и каждый снятый кастер
// снимает ровно один вызов. Городская геометрия дальше 200 м рисует тень в каскад 3, где тексель ~92 см,
// то есть тень на такой дистанции не читается, но оплачивается.
//
// Что делает: гасит Renderer.shadowCastingMode для городской геометрии по дистанции до ближайшей
// геймплейной камеры, поштучно (у каждого слитого батча чанка свои габариты), с гистерезисом и ~3 Гц.
// Набор кандидатов повторяет A/B-полигон бенча Stage4bGapBench ровно: все Renderer активной сцены,
// кроме кораблей (см. ShipRootReference), слоя дверей (10) и тех, у кого тени уже выключены.
// Замер Stage 4b (Play Mode, Host, Midday, корабли выключены): P1 3 047 → 2 423 DC при пороге 200 м.
//
// Чего НЕ делает: не трогает меши, материалы, коллайдеры, Light, SetActive, слой дверей (10) и корабли
// (у них ShipShadowBudget). Не меняет URP shadowDistance/каскады. Не кэширует мировые позиции — FO-safe.
// Хостер — тот же скрытый DontDestroyOnLoad-объект ViewDistanceRuntime. Camera.main не используется.
using System.Collections.Generic;
using ProjectC.Ship;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProjectC.World
{
    [DisallowMultipleComponent]
    public class CityShadowBudget : MonoBehaviour
    {
        [Header("Порог теней города")]
        [Tooltip("Ближе этого расстояния тени включаются обратно (гистерезис).")]
        [SerializeField] private float shadowOnDistance = 180f;

        [Tooltip("Дальше этого расстояния тени выключаются. Замер Stage 4b: 200 м даёт P1 3 047 → 2 423 DC.")]
        [SerializeField] private float shadowOffDistance = 200f;

        [Tooltip("Частота обновления бюджета, с. 2–4 Гц.")]
        [SerializeField] private float updateInterval = 0.33f;

        [Tooltip("Как часто пробовать пересобрать список, если он ещё пуст.")]
        [SerializeField] private float rebuildInterval = 2f;

        [Tooltip("Слой дверей игнорируется: его тени ведёт DoorVisibilityBudget.")]
        [SerializeField] private int skippedLayer = 10;

        // Мир — это не корабль, даже если маркер корабля случайно окажется внутри.
        private const string WorldRootName = "WorldRoot_0_0";

        // ВЫКЛЮЧАТЕЛЬ АВТО-ЗАПУСКА.
        // Компонент включается сам при первом входе в Play Mode (RuntimeInitializeOnLoadMethod ниже),
        // потому что гейт Stage 4b (P1 ≤ 2 500 DC) держится именно на нём и без него игра снова
        // уходит в 3 047 DC. Состояние измерено бенчем Stage4bGapBench, но визуальная приёмка
        // дальних силуэтов теней за 200 м ещё не сделана — если бюджет окажется лишним,
        // достаточно поставить здесь false, и авто-запуск прекратится без правки остального кода.
        private const bool AutoBootstrap = true;

        private class Entry
        {
            public Renderer renderer;
            public ShadowCastingMode baseMode;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Camera> _cameras = new List<Camera>();
        private readonly HashSet<Renderer> _shipRenderers = new HashSet<Renderer>();

        private float _cameraTimer;
        private float _updateTimer;
        private float _rebuildTimer;
        private bool _needRebuild;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Как и у DoorVisibilityBudget: Renderer.shadowCastingMode — СЕРИАЛИЗОВАННОЕ поле,
            // и в edit mode его запись попадает в сцену (см. 5 346 override'ов m_CastShadows: 0
            // от edit-mode пробы Stage 2). Поэтому собираем и трогаем только в Play Mode.
            if (!Application.isPlaying) return;
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
            _cameraTimer = 1f;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            _cameraTimer += Time.unscaledDeltaTime;
            if (_cameraTimer >= 1f)
            {
                _cameraTimer = 0f;
                RefreshCameras();
            }

            _rebuildTimer += Time.unscaledDeltaTime;
            if ((_needRebuild || _entries.Count == 0) && _rebuildTimer >= rebuildInterval)
                Build();

            if (_entries.Count == 0 || _cameras.Count == 0) return;

            _updateTimer += Time.unscaledDeltaTime;
            if (_updateTimer < updateInterval) return;
            _updateTimer = 0f;
            UpdateShadows();
        }

        private void Build()
        {
            _needRebuild = false;
            _rebuildTimer = 0f;
            _entries.Clear();
            _shipRenderers.Clear();

            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            if (renderers.Length == 0)
            {
                _needRebuild = true;
                return;
            }

            CollectShipRenderers();

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (r.gameObject.layer == skippedLayer) continue;
                if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
                if (_shipRenderers.Contains(r)) continue;
                _entries.Add(new Entry { renderer = r, baseMode = r.shadowCastingMode });
            }

            if (_entries.Count == 0)
            {
                _needRebuild = true;
                return;
            }

            Debug.Log("[CityShadowBudget] Рендереров города: " + _entries.Count +
                      ", корабельных исключено: " + _shipRenderers.Count +
                      ", тени off дальше " + shadowOffDistance.ToString("F0") + " м, on ближе " +
                      shadowOnDistance.ToString("F0") + " м.");
        }

        /// <summary>
        /// Корабли не входят в городской бюджет: их тени ведёт ShipShadowBudget, и бенч Stage 4b
        /// их из кандидатов исключал. Маркер может стоять на любой части корабля, поэтому берётся
        /// верхний трансформ и все рендереры под ним.
        /// </summary>
        private void CollectShipRenderers()
        {
            var markers = FindObjectsByType<ShipRootReference>(FindObjectsSortMode.None);
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null) continue;

                Transform root = marker.transform;
                while (root.parent != null) root = root.parent;
                if (root.name == WorldRootName) continue;

                var rs = root.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rs.Length; r++)
                {
                    if (rs[r] != null) _shipRenderers.Add(rs[r]);
                }
            }
        }

        private void RefreshCameras()
        {
            _cameras.Clear();
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
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                var r = e.renderer;
                if (r == null)
                {
                    _needRebuild = true;
                    continue;
                }

                // Union по камерам: shadowCastingMode глобален, поэтому тени нужны, пока
                // геометрия близка хотя бы к одной геймплейной камере.
                float nearestSqr = float.MaxValue;
                Vector3 p = r.bounds.center;
                for (int c = 0; c < _cameras.Count; c++)
                {
                    var cam = _cameras[c];
                    if (cam == null) continue;
                    float d = (p - cam.transform.position).sqrMagnitude;
                    if (d < nearestSqr) nearestSqr = d;
                }
                if (nearestSqr == float.MaxValue) continue;

                float distance = Mathf.Sqrt(nearestSqr);
                bool shadowsOn = r.shadowCastingMode != ShadowCastingMode.Off;
                bool want = shadowsOn ? distance <= shadowOffDistance : distance < shadowOnDistance;
                if (want == shadowsOn) continue;

                r.shadowCastingMode = want ? e.baseMode : ShadowCastingMode.Off;
            }
        }

        private void Restore()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.renderer == null) continue;
                e.renderer.shadowCastingMode = e.baseMode;
            }
            _entries.Clear();
            _cameras.Clear();
            _shipRenderers.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!AutoBootstrap) return;
            if (FindAnyObjectByType<CityShadowBudget>() != null) return;

            var host = GameObject.Find(DoorVisibilityBudget.HostName);
            if (host == null)
            {
                host = new GameObject(DoorVisibilityBudget.HostName);
                DontDestroyOnLoad(host);
            }
            if (host.GetComponent<CityShadowBudget>() == null)
                host.AddComponent<CityShadowBudget>();
        }

        private void OnValidate()
        {
            if (shadowOffDistance < shadowOnDistance) shadowOffDistance = shadowOnDistance;
            if (updateInterval < 0.1f) updateInterval = 0.1f;
            if (rebuildInterval < 0.1f) rebuildInterval = 0.1f;
        }
    }
}
