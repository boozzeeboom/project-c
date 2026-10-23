using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectC.Core;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Сохранение групп света, настроенных в Play Mode.
    ///
    /// Причина существования: Play Mode работает на копии сцены, и правки инспектора живут только
    /// до остановки — Unity выбрасывает их вместе с Play Mode-инстансом. Инструмент запоминает
    /// массив `groups` на выходе из Play Mode (SessionState переживает domain reload) и переносит
    /// значения в объект редактируемой сцены, помечая её грязной. Финальное сохранение — обычный
    /// Ctrl+S, никакой записи YAML сцены в обход редактора нет.
    ///
    /// Переносятся только поля групп (кривые, множители, переопределения, мерцание), а не весь
    /// компонент: ссылка на скрипт и служебные поля сериализации не перезаписываются.
    /// </summary>
    [InitializeOnLoad]
    internal static class LightGroupPlayModeCapture
    {
        private const string SessionKey = "ProjectC.Lighting.PlayModeGroups";

        [Serializable]
        private sealed class Entry
        {
            public string owner;
            public string groupsJson;
        }

        [Serializable]
        private sealed class Snapshot
        {
            public List<Entry> entries = new List<Entry>();
        }

        /// <summary>Контейнер нужен только ради EditorJsonUtility: он умеет AnimationCurve, обычный JsonUtility — нет.</summary>
        private sealed class GroupHolder : ScriptableObject
        {
            public AdditionalLightIntensityController.LightGroup[] groups;
        }

        static LightGroupPlayModeCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Capture();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                Restore();
            }
        }

        private static void Capture()
        {
            Snapshot snapshot = new Snapshot();

            foreach (AdditionalLightIntensityController controller in FindControllers())
            {
                string json = ToJson(controller.EditorGroups);
                if (string.IsNullOrEmpty(json))
                {
                    continue;
                }

                snapshot.entries.Add(new Entry { owner = OwnerId(controller), groupsJson = json });
            }

            if (snapshot.entries.Count == 0)
            {
                SessionState.EraseString(SessionKey);
                return;
            }

            SessionState.SetString(SessionKey, JsonUtility.ToJson(snapshot));
        }

        private static void Restore()
        {
            string raw = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            SessionState.EraseString(SessionKey);

            Snapshot snapshot = JsonUtility.FromJson<Snapshot>(raw);
            if (snapshot == null || snapshot.entries == null || snapshot.entries.Count == 0)
            {
                return;
            }

            foreach (AdditionalLightIntensityController controller in FindControllers())
            {
                Entry match = snapshot.entries.Find(entry => entry != null && entry.owner == OwnerId(controller));
                if (match == null || string.IsNullOrEmpty(match.groupsJson))
                {
                    continue;
                }

                // Ничего не меняли — сцену не трогаем и грязной не помечаем.
                if (ToJson(controller.EditorGroups) == match.groupsJson)
                {
                    continue;
                }

                Undo.RecordObject(controller, "Группы света из Play Mode");
                controller.EditorGroups = FromJson(match.groupsJson);
                EditorUtility.SetDirty(controller);

                Scene scene = controller.gameObject.scene;
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }

                Debug.Log(
                    $"[LightGroups] Настройки групп из Play Mode перенесены в «{scene.name}» / {controller.name}. Сохраните сцену (Ctrl+S), если результат устраивает.",
                    controller);
            }
        }

        private static List<AdditionalLightIntensityController> FindControllers()
        {
            List<AdditionalLightIntensityController> result = new List<AdditionalLightIntensityController>();

            AdditionalLightIntensityController[] all =
                Resources.FindObjectsOfTypeAll<AdditionalLightIntensityController>();

            foreach (AdditionalLightIntensityController controller in all)
            {
                if (controller == null)
                {
                    continue;
                }

                GameObject go = controller.gameObject;
                if (go == null || EditorUtility.IsPersistent(controller) || EditorUtility.IsPersistent(go))
                {
                    continue;
                }

                if ((controller.hideFlags & HideFlags.DontSave) != 0)
                {
                    continue;
                }

                if (PrefabUtility.IsPartOfPrefabAsset(controller))
                {
                    continue;
                }

                Scene scene = go.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                result.Add(controller);
            }

            return result;
        }

        /// <summary>Сцена + путь в иерархии: Play Mode-инстанс и объект редактора ищутся по одному ключу.</summary>
        private static string OwnerId(AdditionalLightIntensityController controller)
        {
            Scene scene = controller.gameObject.scene;
            string sceneId = string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
            return sceneId + "|" + GetTransformPath(controller.transform);
        }

        private static string GetTransformPath(Transform transform)
        {
            string path = transform.name;

            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static string ToJson(AdditionalLightIntensityController.LightGroup[] groups)
        {
            GroupHolder holder = ScriptableObject.CreateInstance<GroupHolder>();
            holder.hideFlags = HideFlags.DontSave;

            try
            {
                holder.groups = groups;
                return EditorJsonUtility.ToJson(holder);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private static AdditionalLightIntensityController.LightGroup[] FromJson(string json)
        {
            GroupHolder holder = ScriptableObject.CreateInstance<GroupHolder>();
            holder.hideFlags = HideFlags.DontSave;

            try
            {
                EditorJsonUtility.FromJsonOverwrite(json, holder);
                return holder.groups;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }
    }
}
