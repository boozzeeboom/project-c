# T-LIGHT15 — живая настройка групп в Play Mode и перенос значений в сцену

**Статус:** реализовано; Play Mode-приёмка за пользователем
**Дата:** 2026-10-23
**Файлы:**
- `Assets/_Project/Scripts/Core/Lighting/AdditionalLightIntensityController.cs`
- `Assets/_Project/Editor/Lighting/LightGroupPlayModeCapture.cs` (новый)
**Связанные:** `T-LIGHT13` (переопределения), `T-LIGHT14` (мерцание)

## 1. Проблема

Переопределения группы (`range`, `color`, `type` и т.д.) применялись только в момент **смены группы** — `state.appliedGroup`. Правка в инспекторе во время Play Mode не меняла `appliedGroup`, поэтому значение не доходило до источников: настроить параметр можно было только так — выйти из Play Mode, править, сохранить сцену, войти снова.

Вторая половина проблемы: Play Mode работает на **копии** сцены. Всё, что настроено в инспекторе во время Play Mode, Unity выбрасывает при остановке. Даже если бы значения применялись мгновенно, терять их при каждом выходе — не рабочий процесс.

## 2. Живое применение (runtime-часть)

Три механизма:

1. **`OnValidate()`** (`#if UNITY_EDITOR`) — вызывается Unity при любом изменении поля в инспекторе, включая Play Mode. Поднимает `_settingsRevision` и ставит `_forceApply`.
2. **`LightState.appliedRevision`** — у каждого источника хранится ревизия, при которой к нему применялись переопределения. В `ApplyIntensity` условие стало `!ReferenceEquals(group, state.appliedGroup) || state.appliedRevision != _settingsRevision`: правка в инспекторе прогоняет `RestoreBaseline` → `ApplyOverrides` по всем источникам независимо от смены группы.
3. **`Update()`** — при `_forceApply` вызывает `ApplyIntensity` даже если время суток не изменилось и мерцание выключено. Применение идёт `immediate: true`, то есть значение встаёт сразу, без плавного перехода — так его и оценивают как ручку.

Отдельно пришлось сделать переопределения идемпотентными: `RestoreBaseline`/`ApplyOverrides` пишут свойство только если значение реально отличается (`Mathf.Approximately`, сравнение `Color`, `LightType`, `LightShadows`). Иначе перетаскивание слайдера в инспекторе дёргало бы нативное `Light.type`/`Light.range` у ~1 900 источников каждый кадр и постоянно инвалидировало рендер-пайплайн.

**Что важно:** `RestoreBaseline` вызывается и при «только правке», а не только при смене группы. Это единственный способ корректно снять флаг `overrideRange` — иначе на источнике остался бы прежний переопределённый радиус.

## 3. Перенос в сцену (editor-часть)

`Assets/_Project/Editor/Lighting/LightGroupPlayModeCapture.cs`, `[InitializeOnLoad]`, статический конструктор подписывается на `EditorApplication.playModeStateChanged`.

| Момент | Действие |
|---|---|
| `ExitingPlayMode` | Найти контроллер, снять массив `groups` через `EditorJsonUtility`, сложить в `SessionState` (`ProjectC.Lighting.PlayModeGroups`) вместе с ключом «сцена + путь в иерархии». Play Mode-объекты на этот момент ещё живы. |
| `EnteredEditMode` | Прочитать `SessionState` (переживает domain reload), найти объект редакторской сцены по тому же ключу, сравнить JSON, при отличии — `Undo.RecordObject` → перезапись → `EditorUtility.SetDirty` → `EditorSceneManager.MarkSceneDirty`. |

Ключевые решения:

- **`SessionState`, а не статическое поле.** Выход из Play Mode обычно вызывает domain reload, статические данные теряются; `SessionState` для этого и предназначен.
- **Переносятся только поля групп, а не весь компонент.** `EditorJsonUtility.ToJson(controller)` включает `m_Script` и служебные поля сериализации; запись их обратно — лишний риск. Массив `groups` заворачивается в временный `ScriptableObject` (`GroupHolder`), сериализуется `EditorJsonUtility` (обычный `JsonUtility` не умеет `AnimationCurve`) и снимается обратно оттуда же. `DestroyImmediate` в `finally`, `HideFlags.DontSave`.
- **Сцена не пишется в обход редактора.** Инструмент только переводят объект в dirty и пишет строку в лог; сохранение — обычный Ctrl+S. Это осознанно: `BootstrapScene` содержит параллельные правки пользователя, и молчаливая запись файла недопустима.
- **Ничего не менялось — сцена не dirty.** JSON сравнивается до записи, поэтому простой прогон Play Mode без правок не оставляет грязных сцен.
- **`Undo.RecordObject`** — перенос отменяется штатным Ctrl+Z.
- Поиск объекта: `Resources.FindObjectsOfTypeAll` с фильтрами `IsPersistent`, `HideFlags.DontSave`, `IsPartOfPrefabAsset`, `scene.IsValid() && scene.isLoaded`.

Доступ к приватному массиву даёт editor-only свойство контроллера `EditorGroups` (`#if UNITY_EDITOR`); в билд оно не попадает.

## 4. Порядок работы

1. Войти в Play Mode.
2. Править группы в инспекторе — переопределения, множитель, кривые, мерцание применяются сразу.
3. Остановить Play Mode.
4. В консоли — `[LightGroups] Настройки групп из Play Mode перенесены в «BootstrapScene» / DayNightController. Сохраните сцену (Ctrl+S), если результат устраивает.`
5. Сохранить сцену. Значения остаются в ней навсегда; отменить перенос — Ctrl+Z до сохранения.

## 5. Ограничения

| Ограничение | Причина |
|---|---|
| Переносятся только настройки **групп** | Настройки `Discovery` / `Runtime Response` (`rescanInterval`, `transitionSpeed`, `logDiscovery`) в Play Mode не меняют видимого результата сразу, а их случайная правка при тесте не должна оседать в сцене. |
| Перенос работает только при выходе из Play Mode штатным Stop | `ExitingPlayMode` — единственный момент, когда Play Mode-значения ещё живы. Аварийное завершение редактора значения не сохранит. |
| Изменения не попадают в коммит автоматически | Перенос делает сцену dirty, коммит сцены остаётся решением пользователя. |
| `SanitizeFlicker` не вызывается из `OnValidate` | Иначе ноль, набранный по пути к нужному числу, перебивался бы дефолтом. Санитайз выполняется в `Awake` и в переносе из Play Mode (там уже санитизированные значения). |
| `EnsureGroupData(false)` в `OnValidate` | Создаёт блок `overrides` для старой сериализации, но не трогает числовые поля. |

## 6. Проверки

- `check_compile_errors`: `No compile errors` (после обоих шагов правки).
- **Раунд-трип JSON проверен на живом компоненте** через рефлексию приватных `ToJson`/`FromJson` инструмента:
  - `groups = 4 → restored = 4`, `jsonLen = 7532`;
  - `intensityByHour.length` сохраняется (7, 3, 7, 7);
  - `overrides != null` после восстановления, `flickerHzMin/Max = 0.5 / 3`.
- Play Mode-прогон (правка → Stop → перенос → сохранение) — **NOT RUN**, выполняет пользователь.

## 7. Замечание по данным сцены (не менялось)

У группы `PRIMUM_INLIGHT` кривая `intensityByHour` задана в диапазоне **0…1 по X** (3 ключа: `0 → 1`, `0.46 → 0.986`, `1 → 1`), тогда как `Evaluate()` читает время суток **0…24**. При времени > 1 кривая упирается в последний ключ и возвращает `1.0`, то есть группа горит круглосуточно с множителем 15. Если нужен суточный график, ключи должны быть расставлены по 0…24. Значения не изменялись — это данные пользователя.

## 8. Откат

- Живое применение: убрать `OnValidate` и условие по `appliedRevision`.
- Перенос: удалить `Assets/_Project/Editor/Lighting/LightGroupPlayModeCapture.cs` и editor-only свойство `EditorGroups`.
- Значения в сцене откатываются обычным Undo/Ctrl+Z до сохранения либо через Git после сохранения.
