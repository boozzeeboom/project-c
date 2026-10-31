// Project C: визуальный culling мелких деталей по дистанции пресета (T-LOD01)
// Хостер: скрытый DontDestroyOnLoad-объект ViewDistanceRuntime (создаёт ViewDistanceApplier).
// Куллит ТОЛЬКО Renderer.enabled групп под detailRoot (дефолт RuinsValleys, целыми хамлетами,
// не отдельными инстансами — не ломает инстансинг-батчи). Стриминг чанков не трогает.
// Позиции читает живьём каждый чек (transform.position / bounds) — между кадрами мировой
// Vector3 не хранится, хук Floating Origin не нужен. Гистерезис 10% против мигания на границе.
using System.Collections.Generic;
using UnityEngine;
using ProjectC.Core;

namespace ProjectC.World
{
    public class DetailDistanceCuller : MonoBehaviour
    {
        [Header("Дальность прорисовки (T-LOD01)")]
        [Tooltip("Корень деталей. Пусто = авто-поиск по имени (сцена может ещё грузиться).")]
        [SerializeField] private Transform detailRoot;

        [Tooltip("Имя корня для авто-поиска.")]
        [SerializeField] private string detailRootName = "RuinsValleys";

        [Tooltip("Интервал перепроверки (с), как updateInterval у WorldStreamingManager.")]
        [SerializeField] private float checkInterval = 0.5f;

        private class Group
        {
            public Transform root;
            public Renderer[] renderers;
            public bool culled;
        }

        private readonly List<Group> _groups = new List<Group>();
        private float _timer;

        // T-CULL01/Stage 0: камера-фокус и момент следующего пере-поиска. Camera.main не используется.
        private Camera _focusCamera;
        private float _nextFocusSearch;

        private void OnEnable()
        {
            Rebuild();
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < checkInterval) return;
            _timer = 0f;
            if (_groups.Count == 0)
                Rebuild();
            Check();
        }

        private void Rebuild()
        {
            _groups.Clear();
            var root = detailRoot;
            if (root == null)
            {
                foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t != null && t.name == detailRootName) { root = t; break; }
                }
            }
            if (root == null) return; // сцены с фокусом нет (Bootstrap, меню) — тихо ждём

            if (root.childCount == 0)
            {
                AddGroup(root);
            }
            else
            {
                // Целыми подгруппами (хамлетами): меньше переключений, батчи целы.
                foreach (Transform child in root)
                    AddGroup(child);
                if (_groups.Count == 0)
                    AddGroup(root); // fallback: дети без рендереров
            }
        }

        private void AddGroup(Transform t)
        {
            if (t == null) return;
            var renderers = t.GetComponentsInChildren<Renderer>(includeInactive: false);
            if (renderers == null || renderers.Length == 0) return;
            _groups.Add(new Group { root = t, renderers = renderers, culled = false });
        }

        private void Check()
        {
            var cam = ResolveFocusCamera();
            if (cam == null) return;
            Vector3 camPos = cam.transform.position;

            float cullDist = ViewDistanceApplier.GetPreset(SettingsManager.ViewDistance).detailCullDistance;
            if (cullDist <= 0f) return;
            float unCullDist = cullDist * 0.9f; // гистерезис

            foreach (var g in _groups)
            {
                if (g == null || g.root == null || g.renderers == null) continue;
                float d = Vector3.Distance(camPos, g.root.position);
                bool wantCulled = g.culled ? d > unCullDist : d > cullDist;
                if (wantCulled == g.culled) continue;
                g.culled = wantCulled;
                foreach (var r in g.renderers)
                {
                    if (r != null) r.enabled = !wantCulled;
                }
            }
        }

        /// <summary>
        /// T-CULL01/Stage 0: точка отсчёта дистанции куллинга.
        /// Camera.main здесь не годится: тег MainCamera есть и у статичной камеры BootstrapScene
        /// (она в 240 км от города), и у геймплейного рига — Camera.main возвращает статичную,
        /// поэтому дистанции считались не от игрока и руины выключались целиком.
        /// Берём активную камеру, ближайшую к самим группам деталей (тот же приём, что
        /// PrimumLightPool.FindFocusCamera). Позиции читаются живьём — мировых Vector3
        /// между кадрами не храним (правило Floating Origin); поиск не чаще раза в секунду.
        /// </summary>
        private Camera ResolveFocusCamera()
        {
            if (_focusCamera != null && _focusCamera.isActiveAndEnabled && Time.unscaledTime < _nextFocusSearch)
                return _focusCamera;

            _focusCamera = FindFocusCamera();
            _nextFocusSearch = Time.unscaledTime + 1f;
            return _focusCamera;
        }

        /// <summary>Активная камера, у которой ближайшая группа деталей ближе всех остальных камер.</summary>
        private Camera FindFocusCamera()
        {
            if (_groups.Count == 0) return null;

            // Камера должна вообще рисовать слои деталей: меню-камера AtmosphereCamera
            // (cullingMask = слой PCMM_Atmosphere, far 10) не рендерит руины и не должна задавать их дальность.
            int detailMask = 0;
            for (int g = 0; g < _groups.Count; g++)
            {
                Group grp = _groups[g];
                if (grp == null || grp.renderers == null) continue;
                for (int r = 0; r < grp.renderers.Length; r++)
                {
                    if (grp.renderers[r] == null) continue;
                    detailMask |= 1 << grp.renderers[r].gameObject.layer;
                }
            }
            if (detailMask == 0) return null;

            Camera[] cams = Camera.allCameras;
            Camera best = null;
            float bestNearestSqr = float.MaxValue;

            for (int c = 0; c < cams.Length; c++)
            {
                Camera cam = cams[c];
                if (cam == null || !cam.isActiveAndEnabled) continue;
                if (cam.targetTexture != null) continue; // RT-камеры не рисуют мир в Game View
                if ((cam.cullingMask & detailMask) == 0) continue;
                Vector3 camPos = cam.transform.position;

                float nearestSqr = float.MaxValue;
                for (int g = 0; g < _groups.Count; g++)
                {
                    Group grp = _groups[g];
                    if (grp == null || grp.root == null) continue;
                    float d = (grp.root.position - camPos).sqrMagnitude;
                    if (d < nearestSqr) nearestSqr = d;
                }

                if (nearestSqr < bestNearestSqr)
                {
                    bestNearestSqr = nearestSqr;
                    best = cam;
                }
            }

            return best;
        }
    }
}
