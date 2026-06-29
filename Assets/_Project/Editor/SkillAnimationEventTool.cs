// Project C: Skills/Battle — T-INP-08 Editor tool
// AddOnAttackImpactToClips: добавляет AnimationEvent "OnAttackImpact" (и опционально
// "OnSkillAnimationEnd") к выбранным .anim/.fbx AnimationClip ассетам.
//
// Зачем: AnimationEvent сериализуется в .fbx/.anim. Дизайнер тащит клип в SkillNodeConfig.attackClip,
// но без event SkillAnimationPlayer будет ждать fallback 0.5с для RPC. Этот tool позволяет
// за один клик добавить event на нужный normalizedTime (default 0.6 = 60%).
//
// Использование:
//   1. Выделить 1..N AnimationClip ассетов (.anim или импортированный .fbx) в Project view.
//   2. Tools → ProjectC/Skills/Add OnAttackImpact Event (60%)
//      или Add OnSkillAnimationEnd Event (100%)
//      или Add Both Events (impact 60% + end 100%)
//   3. Скрипт идемпотентен: если event уже есть — обновляет time, не дублирует.
//
// Дизайнер не открывает Animation window, не кликает на таймлайн вручную.

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ProjectC.Skills.EditorTools
{
    public static class SkillAnimationEventTool
    {
        private const string MENU_ROOT = "Tools/ProjectC/Skills/";

        // Имена функций, которые Unity будет вызывать через SendMessage.
        // Должны совпадать с методами в SkillAnimationEventPassthrough / SkillAnimationPlayer.
        private const string FUNC_IMPACT = "OnAttackImpact";
        private const string FUNC_END = "OnSkillAnimationEnd";

        // Дефолтные таймлайны (нормализованное время 0..1).
        private const float DEFAULT_IMPACT_TIME = 0.6f;
        private const float DEFAULT_END_TIME = 1.0f;

        // ============================================================
        // Menu: Add OnAttackImpact event (60%)
        // ============================================================
        [MenuItem(MENU_ROOT + "Add OnAttackImpact Event (60%)")]
        public static void AddImpactMenu()
        {
            AddEventToSelectedClips(FUNC_IMPACT, DEFAULT_IMPACT_TIME);
        }

        // ============================================================
        // Menu: Add OnSkillAnimationEnd event (100%)
        // ============================================================
        [MenuItem(MENU_ROOT + "Add OnSkillAnimationEnd Event (100%)")]
        public static void AddEndMenu()
        {
            AddEventToSelectedClips(FUNC_END, DEFAULT_END_TIME);
        }

        // ============================================================
        // Menu: Add BOTH events (impact 60% + end 100%)
        // ============================================================
        [MenuItem(MENU_ROOT + "Add BOTH Events (impact 60% + end 100%)")]
        public static void AddBothMenu()
        {
            int impactCount = AddEventToSelectedClips(FUNC_IMPACT, DEFAULT_IMPACT_TIME, saveAssets: false);
            int endCount = AddEventToSelectedClips(FUNC_END, DEFAULT_END_TIME, saveAssets: false);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SkillAnimationEventTool] Added events: OnAttackImpact={impactCount} clips, OnSkillAnimationEnd={endCount} clips. Saved.");
        }

        // ============================================================
        // Menu: Remove ALL custom events from selected clips (cleanup)
        // ============================================================
        [MenuItem(MENU_ROOT + "Remove Skill Animation Events")]
        public static void RemoveMenu()
        {
            var clips = GetSelectedAnimationClips();
            if (clips.Count == 0)
            {
                Debug.LogWarning("[SkillAnimationEventTool] No AnimationClips selected. Select 1+ .anim or .fbx assets in Project view.");
                return;
            }
            int totalRemoved = 0;
            int clipsModified = 0;
            foreach (var clip in clips)
            {
                int before = clip.events != null ? clip.events.Length : 0;
                if (RemoveAllCustomEvents(clip))
                {
                    int after = clip.events != null ? clip.events.Length : 0;
                    totalRemoved += (before - after);
                    clipsModified++;
                    EditorUtility.SetDirty(clip);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[SkillAnimationEventTool] Removed {totalRemoved} custom event(s) from {clipsModified} clip(s).");
        }

        // ============================================================
        // Menu validation: пункты активно только если выбран хотя бы 1 AnimationClip
        // ============================================================
        [MenuItem(MENU_ROOT + "Add OnAttackImpact Event (60%)", true)]
        [MenuItem(MENU_ROOT + "Add OnSkillAnimationEnd Event (100%)", true)]
        [MenuItem(MENU_ROOT + "Add BOTH Events (impact 60% + end 100%)", true)]
        [MenuItem(MENU_ROOT + "Remove Skill Animation Events", true)]
        public static bool MenuValidate()
        {
            return GetSelectedAnimationClips().Count > 0;
        }

        // ============================================================
        // Core logic
        // ============================================================

        /// <summary>
        /// Добавить AnimationEvent с указанным functionName и normalizedTime в каждый выбранный клип.
        /// Идемпотентно: если event с таким functionName уже есть — обновляет time до желаемого.
        /// </summary>
        /// <param name="functionName">Имя функции (например "OnAttackImpact")</param>
        /// <param name="normalizedTime">Нормализованное время 0..1 (0=начало, 1=конец)</param>
        /// <param name="saveAssets">Сохранять AssetDatabase сразу (true) или отложенно (false для batch)</param>
        /// <returns>Количество обработанных клипов</returns>
        private static int AddEventToSelectedClips(string functionName, float normalizedTime, bool saveAssets = true)
        {
            var clips = GetSelectedAnimationClips();
            if (clips.Count == 0)
            {
                Debug.LogWarning($"[SkillAnimationEventTool] No AnimationClips selected. Select 1+ .anim or .fbx assets in Project view. " +
                                 "Tip: if you selected an .fbx but no clips found, the .fbx may have nested clips — select the parent .fbx asset instead, " +
                                 "the tool will iterate all sub-clips.");
                return 0;
            }

            int processed = 0;
            foreach (var clip in clips)
            {
                float eventTime = normalizedTime * clip.length;
                bool wasAdded = AddOrUpdateEvent(clip, functionName, eventTime, normalizedTime);
                if (wasAdded)
                {
                    processed++;
                    EditorUtility.SetDirty(clip);
                    WarnIfFbxSubAsset(clip);
                }
            }
            if (saveAssets) AssetDatabase.SaveAssets();
            Debug.Log($"[SkillAnimationEventTool] '{functionName}' at {normalizedTime:P0} ({normalizedTime * (clips.Count > 0 ? clips[0].length : 1f):F3}s): processed {processed} clip(s).");
            return processed;
        }

        /// <summary>
        /// Добавить или обновить event в клипе. Возвращает true если клип был изменён.
        /// ВАЖНО: для импортированных .fbx sub-assets события НЕ persistent — пропадут при Reimport.
        /// Tool выводит warning + инструкцию, но всё равно добавляет (для удобства preview в Editor).
        /// </summary>
        private static bool AddOrUpdateEvent(AnimationClip clip, string functionName, float timeInSeconds, float normalizedTime)
        {
            if (clip == null) return false;

            var events = clip.events != null ? new List<AnimationEvent>(clip.events) : new List<AnimationEvent>();

            // Ищем существующий event с тем же functionName
            int existingIndex = -1;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].functionName == functionName)
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                // Обновить время
                var existing = events[existingIndex];
                if (Mathf.Approximately(existing.time, timeInSeconds) && Mathf.Approximately(existing.time / Mathf.Max(0.0001f, clip.length), normalizedTime))
                {
                    // Уже на нужном месте — ничего не делаем
                    return false;
                }
                existing.time = timeInSeconds;
                events[existingIndex] = existing;
            }
            else
            {
                // Добавить новый
                events.Add(new AnimationEvent
                {
                    time = timeInSeconds,
                    functionName = functionName,
                    stringParameter = "",
                    floatParameter = 0f,
                    intParameter = 0,
                    messageOptions = SendMessageOptions.DontRequireReceiver,
                });
            }

            // Unity 6 требует AnimationUtility.SetAnimationEvents для persistent events.
            // AnimationUtility.SetAnimationEvents + AssetDatabase.WriteImportSettingsIfDirty(path)
            // обеспечивают что event запишется в .meta и переживёт reimport.
            // ⚠️ Для .fbx sub-clips: events НЕ persistent (Unity не пишет их в .fbx импорт).
            //   См. WarnIfFbxSubAsset() ниже.
            UnityEditor.AnimationUtility.SetAnimationEvents(clip, events.ToArray());

            var clipPath = AssetDatabase.GetAssetPath(clip);
            if (!string.IsNullOrEmpty(clipPath))
            {
                AssetDatabase.WriteImportSettingsIfDirty(clipPath);
            }

            return true;
        }

        /// <summary>
        /// Предупреждение если клип — sub-asset .fbx (events не persistent).
        /// </summary>
        private static void WarnIfFbxSubAsset(AnimationClip clip)
        {
            if (clip == null) return;
            var path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path)) return;
            // Если путь заканчивается на .fbx/.dae/.obj, и имя != "mixamo.com" / стандартное —
            // это sub-asset (не сам fbx-ассет).
            bool isFbx = path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ||
                         path.EndsWith(".dae", System.StringComparison.OrdinalIgnoreCase);
            if (isFbx)
            {
                Debug.LogWarning(
                    $"[SkillAnimationEventTool] ⚠️ Event added to sub-asset '{clip.name}' of '{path}'.\n" +
                    "Events on .fbx sub-clips are NOT persistent — they will be LOST on Reimport!\n" +
                    "To make it persistent, do ONE of:\n" +
                    "  • Open the .fbx in Blender/Maya, add event at 60%, re-export, then Reimport in Unity.\n" +
                    "  • Or duplicate the .fbx → convert to .anim: select clip in Animation window → " +
                    "open Curves → select all → Ctrl+C → create new .anim asset → Ctrl+V.\n" +
                    "  • Or: in Project, right-click the .fbx → 'Extract Animation' (if option available).");
            }
        }

        /// <summary>
        /// Удалить все кастомные events из клипа (OnAttackImpact / OnSkillAnimationEnd).
        /// </summary>
        private static bool RemoveAllCustomEvents(AnimationClip clip)
        {
            if (clip == null || clip.events == null || clip.events.Length == 0) return false;

            var kept = new List<AnimationEvent>();
            foreach (var e in clip.events)
            {
                if (e.functionName != FUNC_IMPACT && e.functionName != FUNC_END)
                {
                    kept.Add(e);
                }
            }
            if (kept.Count == clip.events.Length) return false; // ничего не удалили

            UnityEditor.AnimationUtility.SetAnimationEvents(clip, kept.ToArray());
            var clipPath = AssetDatabase.GetAssetPath(clip);
            if (!string.IsNullOrEmpty(clipPath))
            {
                AssetDatabase.WriteImportSettingsIfDirty(clipPath);
            }
            return true;
        }

        /// <summary>
        /// Собрать все AnimationClip из выделения Project view.
        /// Поддерживает:
        ///   - прямой выбор .anim-клипа
        ///   - выбор .fbx (Model Importer) — берём все sub-AnimationClip из fbx (КРОМЕ __preview__)
        ///   - выбор папки — обходим рекурсивно
        /// </summary>
        private static List<AnimationClip> GetSelectedAnimationClips()
        {
            var result = new List<AnimationClip>();
            var seen = new HashSet<string>(); // дедуп по GUID (не InstanceID — для Unity 6+)

            foreach (var obj in Selection.objects)
            {
                if (obj == null) continue;

                // Случай 1: AnimationClip напрямую
                if (obj is AnimationClip clip)
                {
                    if (TryAddClip(clip, seen)) result.Add(clip);
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                // Случай 2a: .fbx / .dae / .obj — собираем все sub-AnimationClip (пропускаем __preview__)
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".dae", System.StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase))
                {
                    var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var sub in subAssets)
                    {
                        if (sub is AnimationClip subClip && !subClip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                        {
                            if (TryAddClip(subClip, seen)) result.Add(subClip);
                        }
                    }
                    continue;
                }

                // Случай 2b: .anim-файл
                if (path.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase))
                {
                    var animClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (animClip != null && TryAddClip(animClip, seen)) result.Add(animClip);
                    continue;
                }

                // Случай 2c: папка — обходим рекурсивно
                if (AssetDatabase.IsValidFolder(path))
                {
                    var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { path });
                    foreach (var g in guids)
                    {
                        var p = AssetDatabase.GUIDToAssetPath(g);
                        var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                        if (c != null && !c.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                        {
                            if (TryAddClip(c, seen)) result.Add(c);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Добавить клип в список, используя GUID для дедупа (Unity 6+ GetInstanceID deprecated).
        /// </summary>
        private static bool TryAddClip(AnimationClip clip, HashSet<string> seen)
        {
            if (clip == null) return false;
            var path = AssetDatabase.GetAssetPath(clip);
            var guid = AssetDatabase.AssetPathToGUID(path);
            // Если не .anim/.fbx (in-memory clip), guid пустой → используем asset name.
            var key = !string.IsNullOrEmpty(guid) ? guid : (path + "::" + clip.name);
            return seen.Add(key);
        }
    }
}
