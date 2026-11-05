// ParomRoutePreview.cs — T-PAROM-24
// Отладочный предвизуал кабинки на станциях маршрута (только редактор).
// Кнопки в инспекторе ParomRoute → «Предвизуал». Можно вызывать из меню
// ProjectC → Ships → Parom (Ferry) → «Предвизуал …».
//
// Что делает: пересоздаёт временные инстансы Trolley Prefab на КАЖДОЙ станции
// ровно там, где паром стоит в игре (позиция якоря + Cabin Hang Depth, yaw
// вдоль сегмента). Объекты помечены HideFlags.DontSave — в сцену и билд НЕ
// попадают, ParomTrolley на них не вешается (рантайм их не подхватит).
// Повторное нажатие удаляет старые превью и строит новые по актуальным якорям.

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectC.World.Parom
{
    public static class ParomRoutePreview
    {
        private const string PreviewRootName = "__ParomPreview";

        [MenuItem("ProjectC/Ships/Parom (Ferry)/Предвизуал на станциях (Selection)", false, 230)]
        private static void MenuShowAtStations()
        {
            ParomRoute route = FindSelectedRoute();
            if (route == null)
            {
                Debug.LogWarning("[ParomRoutePreview] выберите объект ParomRoute (или его ребёнка) в сцене.");
                return;
            }
            ShowAtStations(route);
        }

        [MenuItem("ProjectC/Ships/Parom (Ferry)/Предвизуал убрать (Selection)", false, 231)]
        private static void MenuClear()
        {
            ParomRoute route = FindSelectedRoute();
            if (route == null) return;
            Clear(route);
        }

        /// <summary>Показать (пересоздать) префаб кабинки на всех станциях ветки.</summary>
        public static void ShowAtStations(ParomRoute route)
        {
            if (route == null) return;

            Clear(route);

            var so = new SerializedObject(route);
            so.Update();
            var prefab = so.FindProperty("_trolleyPrefab").objectReferenceValue as GameObject;
            float hangDepth = so.FindProperty("_cabinHangDepth").floatValue;
            List<Transform> stations = CollectStations(so);

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[ParomRoutePreview:{route.name}] Trolley Prefab пуст — показывать нечего. " +
                    "Назначьте префаб кабинки.", route);
                return;
            }
            if (stations.Count == 0)
            {
                Debug.LogWarning(
                    $"[ParomRoutePreview:{route.name}] нет станций: заполните Start Anchor / End Anchor.", route);
                return;
            }

            var root = new GameObject(PreviewRootName);
            root.hideFlags = HideFlags.DontSave;
            root.transform.SetParent(route.transform, false);

            for (int i = 0; i < stations.Count; i++)
            {
                Transform anchor = stations[i];
                if (anchor == null) continue;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                inst.name = "PREVIEW_" + anchor.name;
                SetHideFlagsRecursive(inst, HideFlags.DontSave);
                // Коллайдеры превью в сцене не нужны (и не должны ловить raycast'ы редактора).
                DisableColliders(inst);

                Vector3 dir = StationDirection(stations, i);
                if (dir.sqrMagnitude > 0.0001f)
                    inst.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                inst.transform.position = anchor.position + Vector3.down * hangDepth;

                Debug.Log(
                    $"[ParomRoutePreview:{route.name}] '{anchor.name}': якорь y={anchor.position.y:0.00}, " +
                    $"кабина y={inst.transform.position.y:0.00} (hang {hangDepth:0.##} м), {DescribeBoarding(inst)}",
                    route);
            }

            SceneView.RepaintAll();
            Debug.Log(
                $"[ParomRoutePreview:{route.name}] превью на {stations.Count} станциях создано " +
                $"('{PreviewRootName}', в сцену не сохраняется). Повторное нажатие пересоздаст.", route);
        }

        /// <summary>Удалить все превью-объекты этой ветки.</summary>
        public static void Clear(ParomRoute route)
        {
            if (route == null) return;
            Transform old = route.transform.Find(PreviewRootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            SceneView.RepaintAll();
        }

        // --- helpers ---

        private static ParomRoute FindSelectedRoute()
        {
            GameObject go = Selection.activeGameObject;
            if (go == null) return null;
            ParomRoute route = go.GetComponent<ParomRoute>();
            if (route == null) route = go.GetComponentInParent<ParomRoute>();
            return route;
        }

        private static List<Transform> CollectStations(SerializedObject so)
        {
            var list = new List<Transform>();
            var start = so.FindProperty("_startAnchor").objectReferenceValue as Transform;
            var end = so.FindProperty("_endAnchor").objectReferenceValue as Transform;
            var mids = so.FindProperty("_intermediateAnchors");

            if (start != null) list.Add(start);
            if (mids != null)
                for (int i = 0; i < mids.arraySize; i++)
                {
                    var t = mids.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    if (t != null) list.Add(t);
                }
            if (end != null && end != start) list.Add(end);
            return list;
        }

        /// <summary>Yaw станции: к следующей, у конечной — к предыдущей (как паром в игре).</summary>
        private static Vector3 StationDirection(List<Transform> stations, int index)
        {
            Transform from = stations[index];
            Transform to = index < stations.Count - 1 ? stations[index + 1] : stations[index - 1];
            if (from == null || to == null) return Vector3.zero;
            Vector3 d = to.position - from.position;
            d.y = 0f;
            return d;
        }

        private static void SetHideFlagsRecursive(GameObject go, HideFlags flags)
        {
            Transform[] all = go.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) all[i].gameObject.hideFlags = flags;
        }

        private static void DisableColliders(GameObject go)
        {
            Collider[] cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
        }

        /// <summary>Куда встанет игрок: тот же расчёт, что ParomRoute.GetBoardingPoint (верх + 0.3).</summary>
        private static string DescribeBoarding(GameObject inst)
        {
            var box = inst.GetComponentInChildren<BoxCollider>();
            if (box != null)
            {
                Vector3 p = inst.transform.TransformPoint(box.center + new Vector3(0f, box.size.y * 0.5f, 0f));
                return $"точка посадки y≈{p.y + 0.3f:0.00} (верх пола +0.3)";
            }
            var rend = inst.GetComponentInChildren<Renderer>();
            if (rend != null) return $"точка посадки y≈{rend.bounds.max.y + 0.3f:0.00} (верх рендера +0.3)";
            return "нет BoxCollider/рендера — точку посадки не посчитать";
        }
    }
}
#endif
