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
            var cam = Camera.main;
            if (cam == null || !cam.isActiveAndEnabled) return;
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
    }
}
