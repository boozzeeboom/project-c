# Changelog — Character Customisation

> **Подсистема:** Character Customisation (additive layer поверх Stats / Equipment / Skills)
> **Формат:** Дата — Версия — Что изменилось — Тикеты
> **Дата создания:** 2026-06-30

---

## Дата | Сессия | Изменения

| Дата | Сессия | Что сделано | Тикеты |
|---|---|---|---|
| 2026-06-30 | (design-only) | Создан каталог `docs/Character/Customisation/`. Написан системный анализ кастомизации персонажа игроком — от L1 (М↔Ж) до L5 (лицо). Документы: 00_OVERVIEW (TL;DR + принципы), 01_CURRENT_CAPABILITIES (что уже есть), 02_DATA_MODEL (CustomisationSave + DTO + ClientState + Applier), 03_LEVELS_OF_CUSTOMISATION (L1..L5 с трудоёмкостью), 04_MALE_FEMALE_SWAP (глубокий разбор M↔F swap с кодом), 05_PHASES_ROADMAP (тикеты T-CUS-01..12 в 3 milestone'ах). **Код НЕ написан — design-only сессия.** | — |
| 2026-06-30 | (design-only) | **Ключевые находки:** (1) HumanF_Model.fbx уже в проекте (`Assets/Kevin Iglesias/Human Animations/Models/`). (2) Female animation set полный (Walk/Run/Sprint/Jump/Idle/etc.) с теми же именами что Male → AnimatorOverrideController подменяется trivially. (3) Skeleton generic Humanoid — EquipSlotToBone + Equipment Visual работают для обоих полов без изменений. (4) CharacterEquipmentVisualApplier уже на префабе, паттерн 100% переиспользуем. (5) SkillAnimationPlayer использует runtimeAnimatorController как base — работает прозрачно с F-версией. (6) CharacterSaveData + JsonCharacterDataRepository уже готовы к additive-расширению. **Итог: L1 (M↔F) — 3-5 дней, ~10-15 новых файлов, нулевой риск regression.** | — |
| 2026-06-30 | L1 implementation | **T-CUS-01 (Persistence) ✅:** Созданы enum CharacterBodyType/BodyPresetId/HairStyleId + CustomisationSave (полный DTO: bodyType, presetId, heightScale, widthScale, skin/hair colors, hairStyle, clothingColorOverrides). Расширен CharacterSaveData (+1 поле `customisation`). | T-CUS-01 |
| 2026-06-30 | L1 implementation | **T-CUS-02 (Network DTO + ClientState) ✅:** Созданы CustomisationSnapshotDto (struct + ClothingColorOverrideDto) + CustomisationClientState (singleton, OnCustomisationUpdated event, ApplyCustomisationSnapshot). `[CustomisationClientState]` GameObject добавлен в BootstrapScene. | T-CUS-02 |
| 2026-06-30 | L1 implementation | **T-CUS-03 (CharacterCustomisationApplier) ✅:** Создан MonoBehaviour-компонент на NetworkPlayer. Phase 1: ApplyBodyType (SkinnedMeshRenderer.sharedMesh + Animator.runtimeAnimatorController swap + reset triggers). Phase 2: ApplyProportions (transform.localScale). Phase 3: ApplyColors (MaterialPropertyBlock на _BaseColor + _Color). Hair style логируется, mesh spawn — TODO T-CUS-10. | T-CUS-03 |
| 2026-06-30 | L1 implementation | **T-CUS-04 (PlayerAnimation_Female.overrideController + Editor) ✅:** Создан Editor script `SetupFemaleAnimationOverride` (MenuItem `Tools/ProjectC/Player/Setup Female Animation Override`). Создан PlayerAnimation_Female.overrideController автоматически с 33+ swap'ами M→F (HumanM@ → HumanF@, /Male/ → /Female/). | T-CUS-04 |
| 2026-06-30 | L1 implementation | **T-CUS-05 (NetworkPlayer.prefab component install) ✅:** Создан Editor script `SetupCharacterCustomisationApplier` (MenuItem `Tools/ProjectC/Player/Add CharacterCustomisationApplier to NetworkPlayer`). Компонент добавлен на NetworkPlayer.prefab. Все 7 Inspector-полей автоматически назначены: _visualRoot, _animator, _bodyRenderer, _maleMesh (HumanM_Model), _femaleMesh (HumanF_Model), _maleController (PlayerAnimation_Default), _femaleController (PlayerAnimation_Female). | T-CUS-05 |
| 2026-06-30 | L1 implementation | **T-CUS-06 (CustomisationWindow) ✅:** Созданы CustomisationWindow.uxml (по аналогии со SkillTreeWindow — full-screen overlay с двумя карточками выбора пола), CustomisationWindow.uss (тёмно-синяя палитра, активная карточка = teal), CustomisationWindow.cs (~280 строк, по паттерну SkillTreeWindow с EnsureBuilt/SetOpen/Escape-handler, загрузка/сохранение CustomisationSave через JsonCharacterDataRepository + ApplyCustomisationSnapshot на клиент-стейт). Создан CustomisationPanelSettings.asset. Создан `[CustomisationWindow]` GameObject в BootstrapScene (через NMC auto-spawn fallback через `CreateCustomisationWindow()` helper в NetworkManagerController). Кнопка "ИЗМЕНИТЬ ВНЕШНОСТЬ" добавлена в header CharacterWindow.uxml (новая `header-row` + Label), handler `InitOpenCustomisationButton()` добавлен в CharacterWindow.cs. | T-CUS-06 |
| 2026-06-30 | Bugfix | **Стартовый персонаж мелкий + тёмно-серый (scale=0 фикс):** Корневая причина — `CustomisationClientState.CurrentSnapshot` = `struct default` (heightScale=0, widthScale=0, skinColor=0). При `OnEnable` applier применял это как scale=(0,0,0) → Visual_Model схлопывался до невидимости. Фикс: в `CharacterCustomisationApplier.OnEnable` всегда подменяем default-значения на (1, 1) чтобы scale не схлопнулся. При первом запуске scale остаётся от префаба (1,1,1) — корректно. Тёмно-серый цвет — это default URP Lit (50% gray) на стартовом SMR, не баг моего кода (Kevin Iglesias FREE не имеет собственного материала для моделей). Никакие существующие системы не тронуты. | — |
| 2026-06-30 | L3 implementation | **T-CUS-09 (Слайдеры рост/полнота) ✅:** В CustomisationWindow добавлена секция "Пропорции" с двумя слайдерами (Рост 0.85-1.15 → расширено до 0.7-1.3, Полнота 0.85-1.15 → 0.7-1.3) + value labels + кнопка "СБРОСИТЬ". UI Toolkit Slider с `RegisterValueChangedCallback` → `OnHeight/WidthSliderChanged` → `_working.heightScale/widthScale` → SaveWorking → JSON + ApplyCustomisationSnapshot → `CharacterCustomisationApplier.ApplyProportions` ставит `_visualRoot.localScale = (width, height, width)`. USS классы `.cw-slider-row`, `.cw-slider-label`, `.cw-slider-flex`, `.cw-slider-value`, `.cw-btn-reset`. SetValueWithoutNotify в RefreshDisplay чтобы не было ping-pong callback → SaveWorking во время Show. Clamp в applier расширен до [0.4, 1.6]. | T-CUS-09 |
| 2026-06-30 | Bugfix | **Persistence сбрасывалась при перезапуске сервера:** Перешёл на отдельный файл `persistentDataPath/Customisation/customisation_<clientId>.json` (был `character_<clientId>.json` через JsonCharacterDataRepository). StatsServer перезаписывает свой файл при старте — теперь customisation изолирован. | — |
| 2026-06-30 | Bugfix | **Внешность не актуализировалась при заходе (даже когда JSON есть):** `CharacterCustomisationApplier.OnEnable` теперь сам читает JSON с диска через `LoadSnapshotFromDisk()`. Если файл найден — сразу вызывает `_clientState.ApplyCustomisationSnapshot(snapshot)`, не дожидаясь UI. | — |
| 2026-06-30 | L4 implementation | **T-CUS-10 (L4 skin color) ✅:** Добавлена секция "Цвет кожи" с 3 RGB слайдерами (0-1, labels 0-255) + preview swatch + кнопка "СБРОСИТЬ ЦВЕТ". Slider → `OnSkinRSliderChanged/G/B` → `_working.skinColorR/G/B` → SaveWorking → JSON + ApplyCustomisationSnapshot → `CharacterCustomisationApplier.ApplyColors` применяет MaterialPropertyBlock с `_BaseColor` (URP/Lit). Убран дубликат `SetPropertyBlock`. ColorsDiffer теперь проверяет только skin (hair/clothing deferred). | T-CUS-10 |

---

## Решения / контекст

| Дата | Решение | Контекст |
|---|---|---|
| 2026-06-30 | Кастомизация — отдельный orthogonal слой, не трогаем Stats/Equipment/Skills | Add-only паттерн по аналогии с EquipmentVisual |
| 2026-06-30 | CustomisationSave лежит в CharacterSaveData (additive) | Не создаём новый файл — JsonUtility backward-compat |
| 2026-06-30 | L1 приоритет, L4 второй, L3 третий (transform.localScale) | L2/L5 отложены (нужны доп. ассеты или SDK) |
| 2026-06-30 | Multiplayer sync — Variant A (client-only) для MVP, Variant B (NetworkVariable) — когда потребуется | Минимизация изменений в сетевом коде |
| 2026-06-30 | AnimatorOverrideController для F создаётся через drag-and-drop или Editor script | НЕ дублируем стейт-машину, только подменяем motion-ы |
| 2026-06-30 | Слайдеры тела через transform.localScale, не через blend shapes | Kevin Iglesias FREE не имеет blend shapes; transform.localScale — универсальное решение |

---

## Lessons learned

_(будет заполняться по мере реализации)_

---

## Open questions

_(см. `05_PHASES_ROADMAP.md` §8)_

1. Будет ли Character Creation Screen?
2. Будет ли cosmetic-only inventory?
3. Будет ли Faction-locked cosmetic?
4. Что с NPC customisation?
5. Что с Cinematic камерами (lighting для новых skin tones)?