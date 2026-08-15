# Итерации структуризации документации

## Итерация от 2026-08-15 (T-CORE12)

**Задача:** Персональная настройка edge detection для NetworkPlayer и устранение спама `submeshIndex out of range`.
**Коммит:** `c54e4102` — T-CORE12: Настроить персонификацию edge detection
**Изменения:**
- `EdgeDetectionRenderFeature.cs` — маска объектов `EdgeDetectionTarget` и корректная отрисовка всех submesh/material slots через `RasterCommandBuffer`
- `EdgeDetectionTarget.cs` — per-object параметры и исключение объекта из глобального outline
- `EdgeDetection.shader` — обработка target mask и отдельных параметров цели
- `EdgeDetectionMask.shader` — shader маски target-объектов
- `ProjectC_URP_Renderer.asset` — включение настроек per-object targets

## Итерация от 2026-08-05 (T-X5)

**Задача:** Персистенция контрактов — состояние ContractWorld должно переживать перезагрузку сервера.
**Коммит:** `d972adad` — T-X5: персистенция контрактов через IPlayerDataRepository
**Изменения:**
- `ContractSaveData.cs` — новый `[Serializable]` DTO (contracts, debts, playerContracts, locationContracts)
- `IPlayerDataRepository.cs` — + `SaveContracts` / `TryLoadContracts`
- `ServerFileRepository.cs` — реализация через JSON `ServerData/contracts.json`
- `PlayerPrefsRepository.cs` — реализация через `PlayerPrefs`
- `ContractWorld.cs` — `SaveAll()` / `LoadAll()`, load при `Initialize`, save после мутаций и `Shutdown`
- `docs/Markets/CONTRACT_PERSISTENCE.md` — документация

## Итерация от 2026-08-04 (T-CLOUD02) #2

**Задача:** Multi-Layer Cloud System — 4 слоя (800-1200, 1200-2500, 2500-4500, 4500-7000) с per-layer coverage/density/ramps.
**Коммит:** `d2ec02f4` — T-CLOUD02: Multi-Layer Cloud System (4 слоя 800-7000)
**Изменения:**
- `VolumetricCloudsRenderFeature.cs` — `CloudLayerDef` struct, `Layers[4]`, `ActiveLayerCount` 1-4
- `VolumetricClouds.shader` — `_LayerBounds[4]`, `ComputeLayerColor()`, per-layer цикл
- `IMPLEMENTATION_LOG.md` — запись фазы 3.0
- **Перф:** +5-10% GPU. Displacement работает сквозь все слои.

## Итерация от 2026-08-04 (T-CLOUD02) #1

**Задача:** Variant B — Cloud Displacement Interaction. Альтернативный метод интерактивности облаков: displacement (сдвиг 3D-шума) вместо вычитания плотности. A/B-переключение через `LocalDensityBuffer.Mode`.
**Коммит:** `f8778102` — T-CLOUD02: Variant B — Cloud Displacement Interaction
**Изменения:**
- `LocalDensity.compute` — +2 ядра: AdvectAndRelax_Disp + ApplySplats_Disp (radial push от сплатов)
- `LocalDensityBuffer.cs` — enum Mode, RGBAHalf RT, CPU mirror только для Density
- `VolumetricClouds.shader` — SampleLocalDisplacement(), keyword _LOCALDENSITY_DISPLACEMENT
- `VolumetricCloudsRenderFeature.cs` — DisplacementStrength (0-1000, default 300)
- `ShipWakeCloudCutter.cs` — конус плотнее: 16 сегментов, i=0 у корабля, радиус 50-200
- `IMPLEMENTATION_LOG.md` — запись фазы 2.2B
- **⚠️ Перф-нота:** рост CutRadius → O(radius³). Будущий фикс: indirect dispatch или analytical displacement.

## Итерация от 2026-07-31 (T-CAM15)

**Задача:** Починка SpringArmCamera — камера не успевает за персонажем при телепортациях/загрузке сохранений.
**Коммит:** `bcf2d9b` — T-CAM15: авто-снап камеры при телепортации/загрузке сохранения
**Изменения:**
- `Assets/_Project/Scripts/Core/SpringArmCamera.cs` — авто-детект скачка >10m в LateUpdate + публичный метод Snap()

## Итерация от 2026-07-31

**Задача:** Структуризация `docs/dev` — разбор 38 нерассортированных файлов по целевым папкам и архиву.
**Коммит:** `0ea054e` — T-DOCS01: структуризация документации docs/dev — разбор 38 файлов
**Изменения:**
- 16 файлов в `docs/archive/` (старые фиксы, ретроспективы, сводки, одноразовые скрипты, дубликаты)
- 21 файл перемещён в целевые подпапки по системам:
  - `docs/Character/Skills/` — 6 файлов (combat-animations, COMBAT_ENGINE, INP06, INP08, SKILLS_NEXT_STEPS, SKILLS_ROADMAP)
  - `docs/Character/` — 3 файла (npc-p2-visual-config, INVESTIGATION_CHARACTER_MICRO_JITTER, INVESTIGATION_GHOST_PLAYER_CLONE)
  - `docs/Character/Character-menu/` — 1 файл (CHARACTER_WINDOW_INVENTORY_TAB_REFACTOR)
  - `docs/Character/Character-menu/sub_inventory-tab/` — 1 файл (INVENTORY_V2_DROP_DESIGN)
  - `docs/Character/EquipmentVisual/` — 1 файл (EquipmentVisual_BUGS_TICKETS)
  - `docs/Markets/` — 2 файла (CONTRACT_V2_MIGRATION, CONTRACTS_AS_MARKET_TAB_REFACTOR)
  - `docs/MetaRequirement/` — 1 файл (META_REQUIREMENT_IMPL_NOTES)
  - `docs/NPC_quests/` — 2 файла (M19_T7_DIALOG_CSV, T_Q19_NPC_QUEST_LINKING)
  - `docs/NPC_others_peacfull/npc_ship/` — 1 файл (npc-ship-movement-refactor)
  - `docs/Ships/` — 2 файла (ship_collision_analysis, SHIP_KEY_SETUP_v11)
  - `docs/UI/` — 1 файл (PERF_UIElementsRepaintPanels_INVESTIGATION)
- `docs/dev/` очищен

## Итерация от 2026-07-31 (вторая)

**Задача:** Полный аудит документации — сверка всех подсистем с кодом, удаление дизайн-фазы (код реализован), архивация устаревших сессий, актуализация ссылок.
**Коммит:** `a86247a` — T-DOCS01: Аудит и очистка документации — ~180 файлов в архив, актуализация ссылок
**Изменения:**
- Character: убраны дизайн-документы 02-10, аудиты 11-14, подсистемные планы (Character-menu, Customisation, EquipmentVisual, input-system, ThirdpersonCamera, Knowledges, turn-based-battles)
- Ships: убран legacy (11 файлов), дизайн-планы (5 файлов)
- Crafting_system: убраны дизайн-документы 00-50
- Docking_stations: убраны дизайн-документы 01-07, 09, 11
- Markets: убран устаревший аудит, исправлена ссылка
- MetaRequirement: убраны 00-50, RECIPES
- Mining: убраны аудиты с исправленными проблемами
- NPC_quests: убраны 00-07, 09, 10, old_session_log, Complete_v2
- world: убраны планы April 2026 (6), старые сессии (~70)
- context: убран устаревший контекст
- gdd: исправлены битые ссылки (GDD_INDEX, GDD_00, GDD_01)
- Character/00_README.md: полная переработка
- Всего ~180 файлов в 23 архивные папки

## Итерация от 2026-07-28

**Задача:** Фикс краша билда (CS0103 в InvestigateAnimator.cs) + документирование и отключение ProjectCPerfHUD.
**Коммит:** `f1b8dc9` — T-FIX01: фикс краша билда + отключение и документирование PerfHUD
**Изменения:**
- `Assets/_Editor/InvestigateAnimator.cs` — обёрнут в `#if UNITY_EDITOR` (папка `_Editor` не является специальной Editor-папкой, код попадал в билд)
- `Assets/_Project/Scripts/Core/ProjectCPerfHUD.cs` — отключён через `#if FALSE`, добавлен полный header-комментарий с документацией (назначение, подключение, зависимости, инструкция по включению)
