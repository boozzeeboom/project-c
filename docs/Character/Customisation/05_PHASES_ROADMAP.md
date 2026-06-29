# Phases Roadmap — тикеты и порядок реализации

> **Дата:** 2026-06-30
> **Цель:** разбить всю работу на 8 тикетов **T-CUS-01..T-CUS-08** в 3 milestone'ах. Каждый тикет = отдельная сессия, ~1-5 дней работы.
> **Паттерн:** аналог `08_ROADMAP.md` для Character Progression (T-P01..T-P18).

---

## 0. Стратегия

| Принцип | Реализация |
|---|---|
| **Add-only** | Никаких breaking changes в существующих файлах кроме одного поля в `CharacterSaveData`. |
| **Маленькие шаги** | Каждый тикет = 1 коммит, легко откатить. |
| **Постепенность** | M1 (L1) → M2 (L3 + L4) → M3 (L2 + L5 + sync). M1 обязателен, остальное по запросу. |
| **Тест после каждого тикета** | Compile 0 errors + Play Mode smoke test. |
| **Persistence с самого начала** | Даже L1 пишется в JSON — чтобы не переделывать потом. |

---

## 1. Roadmap сводка

| Milestone | Тикеты | Уровни | Трудоёмкость |
|---|---|---|---|
| **M1: L1 (M↔F swap)** | T-CUS-01, T-CUS-02, T-CUS-03, T-CUS-04, T-CUS-05, T-CUS-06 | L1 | ~5-7 дней |
| **M2: L3 (sliders) + L4 (color)** | T-CUS-09, T-CUS-10 | L3, L4 | ~5-7 дней |
| **M3: L2 (presets) + L5 (face) + sync** | T-CUS-07, T-CUS-08, T-CUS-11, T-CUS-12 | L2, L5 | ~3-6 недель |

**T-CUS-07/08 делаются параллельно с M1 если нужны раньше.**

---

## 2. Milestone 1 — L1: M↔F Swap

**Цель:** игрок может переключать пол персонажа между Male и Female. Скиллы/статы/экипировка общие.

**Definition of Done:**
- ✅ `CharacterSaveData.customisation.bodyType` сохраняется в JSON.
- ✅ UI sub-tab "ВНЕШНОСТЬ" с двумя кнопками М/Ж.
- ✅ Клик на "Ж" → мгновенный swap на F-модель + F-анимации.
- ✅ Equip visuals продолжают работать (parent к костям — skeleton одинаковый).
- ✅ SkillAnimationPlayer не ломается (использует новый controller как base).
- ✅ Persist + restore через reconnect.

---

### T-CUS-01: Persistence layer (CustomisationSave + CharacterSaveData extension)

**Размер:** ~70 строк кода, ~30 минут.

**Файлы:**
- `Assets/_Project/Scripts/Customisation/CharacterBodyType.cs` (NEW) — enum.
- `Assets/_Project/Scripts/Customisation/BodyPresetId.cs` (NEW) — enum (заготовка, используется в L2).
- `Assets/_Project/Scripts/Customisation/HairStyleId.cs` (NEW) — enum (заготовка, используется в L4).
- `Assets/_Project/Scripts/Customisation/CustomisationSave.cs` (NEW) — [Serializable] DTO.
- `Assets/_Project/Scripts/Stats/Persistence/CharacterSaveData.cs` (MODIFY) — +1 поле.

**Подробности:** см. `02_DATA_MODEL.md` §3 и §7.

**Верификация:**
```bash
# 1. Compile: 0 errors.
# 2. Open BootstrapScene → Play (Host). Подождать save → выйти.
# 3. Проверить character_<clientId>.json:
#    Должна быть секция "customisation":{"bodyType":0,...}
# 4. Удалить "customisation" секцию → загрузить → bodyType = Male (default). Backward-compat OK.
```

---

### T-CUS-02: Network DTO + ClientState singleton

**Размер:** ~120 строк кода, ~30 минут.

**Файлы:**
- `Assets/_Project/Scripts/Customisation/Dto/CustomisationSnapshotDto.cs` (NEW).
- `Assets/_Project/Scripts/Customisation/Dto/CustomisationResultDto.cs` (NEW) — можно объединить с предыдущим.
- `Assets/_Project/Scripts/Customisation/CustomisationClientState.cs` (NEW).

**Подробности:** см. `02_DATA_MODEL.md` §4 и §5.

**Верификация:**
```bash
# 1. Compile: 0 errors.
# 2. Создать [CustomisationClientState] GameObject в BootstrapScene (по аналогии с [EquipmentClientState]).
# 3. Назначить PanelSettings/UIDocument? Нет, это singleton без UI.
# 4. Play → Console: "[CustomisationClientState] Awake: Instance set".
# 5. Manual invocation: CustomisationClientState.Instance.OnCustomisationSnapshotReceived(default) →
#    Console: "Snapshot: body=Male, h=1.00, w=1.00, hair=Short".
```

---

### T-CUS-03: CharacterCustomisationApplier (L1 — только mesh + controller swap)

**Размер:** ~150 строк кода, ~45 минут.

**Файлы:**
- `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs` (NEW).

**Подробности:** см. `04_MALE_FEMALE_SWAP.md` §5 (полный код).

**Верификация:**
```bash
# 1. Compile: 0 errors.
# 2. Play → Console: "[CharacterCustomisationApplier] Applied bodyType=Male (...)" (default).
# 3. Manual invocation через ContextMenu "DEBUG: Force re-apply current snapshot" →
#    Console: snapshot applied (если не default).
```

---

### T-CUS-04: PlayerAnimation_Female.overrideController + Editor setup

**Размер:** ~80 строк Editor script + drag-and-drop в Inspector, ~30 минут.

**Файлы:**
- `Assets/_Project/Animations/PlayerAnimation_Female.overrideController` (NEW) — AnimatorOverrideController.
- `Assets/_Project/Editor/SetupFemaleAnimationOverride.cs` (NEW) — MenuItem для автогенерации.

**Шаги:**
1. Создать AnimatorOverrideController → `Controller = PlayerAnimation.controller`.
2. Запустить `Tools/ProjectC/Player/Setup Female Animation Override`.
3. Console: "[SetupFemale] Swapped N clips → PlayerAnimation_Female.overrideController".

**Верификация:**
```bash
# 1. Открыть PlayerAnimation_Female.overrideController в Inspector.
#    Все motion-ы должны быть Female (HumanF@Walk01_Forward и т.д.).
# 2. Play (с назначенным _femaleController в CharacterCustomisationApplier) →
#    персонаж с F-клипами (если bodyType=Female).
```

---

### T-CUS-05: Component installation в NetworkPlayer.prefab + Editor script

**Размер:** ~30 строк Editor script, ~10 минут.

**Файлы:**
- `Assets/_Project/Editor/SetupCharacterCustomisationApplier.cs` (NEW).

**Шаги:**
1. Запустить `Tools/ProjectC/Player/Add CharacterCustomisationApplier to NetworkPlayer`.
2. Открыть NetworkPlayer.prefab → назначить `_maleMesh = HumanM_Model.sharedMesh`, `_femaleMesh = HumanF_Model.sharedMesh`, `_maleController = PlayerAnimation_Default.overrideController`, `_femaleController = PlayerAnimation_Female.overrideController`.

**Верификация:**
```bash
# 1. NetworkPlayer.prefab содержит компонент CharacterCustomisationApplier.
# 2. Все Inspector-поля заполнены.
# 3. Compile: 0 errors.
```

---

### T-CUS-06: UI sub-tab "ВНЕШНОСТЬ" (L1 — только M/F toggle)

**Размер:** ~100 строк (UXML + handler в CharacterWindow.cs), ~1.5 часа.

**Файлы:**
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uxml` (MODIFY) — добавить sub-tab button + section.
- `Assets/_Project/UI/Resources/UI/CharacterWindow.uss` (MODIFY) — добавить стили для toggle-row.
- `Assets/_Project/Scripts/UI/Client/CharacterWindow.cs` (MODIFY) — handler для sub-tab + toggle callbacks.

**Верификация:**
```bash
# 1. Compile: 0 errors.
# 2. Play → Tab → CharacterWindow.
# 3. Внутри таба "ПЕРСОНАЖ" появляется sub-tab "ВНЕШНОСТЬ".
# 4. Кликнуть "ВНЕШНОСТЬ" → видим две кнопки М/Ж.
# 5. Кликнуть "Ж" → Console: "[CharacterCustomisationApplier] Applied bodyType=Female ...".
#    Визуально: персонаж становится F-моделью.
# 6. Equip шлем → шлем на F-голове (parent к Head bone — работает).
# 7. Cast скилла (ЛКМ) → skill animation на F-персонаже.
```

---

## 3. Milestone 2 — L3 + L4 (Sliders + Colors)

**Цель:** игрок может настраивать рост/полноту слайдерами и менять цвет кожи/волос/одежды.

**Зависимость:** M1 (T-CUS-01..06) должен быть complete.

**Definition of Done:**
- ✅ Слайдеры роста/полноты в UI sub-tab "ВНЕШНОСТЬ" работают runtime (transform.localScale).
- ✅ ColorPicker кожи меняет цвет через MaterialPropertyBlock.
- ✅ ColorPicker волос работает (если есть hair mesh).
- ✅ ColorPicker одежды меняет цвет надетого visualPrefab.
- ✅ Persistence работает для всех параметров.

---

### T-CUS-09: L3 — Слайдеры тела (height/width scale)

**Размер:** ~80 строк кода, ~1 день.

**Изменения:**
- `CustomisationSave` — добавить `heightScale`, `widthScale` (default 1.0).
- `CustomisationSnapshotDto` — добавить `heightScale`, `widthScale`.
- `CharacterCustomisationApplier` — расширить `OnCustomisationUpdated` + `ApplyProportions(h, w)`.
- `NetworkPlayer` (опционально) — подстройка CharacterController.height/center.
- UI: 2 Slider'а в CharacterWindow.uxml + handler.

**Подробности:** см. `03_LEVELS_OF_CUSTOMISATION.md` §L3.

**Верификация:**
```bash
# 1. Compile: 0 errors.
# 2. Play → ВНЕШНОСТЬ → двигаем слайдер "Рост".
# 3. Персонаж визуально увеличивается/уменьшается.
# 4. Equip шлем → шлем тоже растягивается (parent scale).
# 5. Cast скилла → анимация на изменённом масштабе (Humanoid retargeting справится).
# 6. Сохранение → character_<id>.json содержит heightScale=1.05.
```

---

### T-CUS-10: L4 — Покраска (skin / hair / clothing colors)

**Размер:** ~120 строк кода, ~3 дня.

**Изменения:**
- `CustomisationSave` — добавить RGBA-поля + `clothingColorOverrides[]`.
- `CustomisationSnapshotDto` — добавить RGBA-поля + `clothingOverrides[]`.
- `CharacterCustomisationApplier` — расширить `OnCustomisationUpdated` + `ApplyColors(snapshot)`.
- `CharacterEquipmentVisualApplier` (опционально) — добавить `ApplyColorOverride(visual, slot, color)`.
- UI: 3 ColorPicker'а в CharacterWindow + per-EquipSlot picker.

**Зависимость:** если hair mesh ещё не создан — hair color применяется только когда hair style = Short и есть базовый материал волос. Можно отложить до T-CUS-11.

**Подробности:** см. `03_LEVELS_OF_CUSTOMISATION.md` §L4.

**Верификация:**
```bash
# 1. Compile: 0 errors.
# 2. Play → ВНЕШНОСТЬ → ColorPicker "Кожа" → выбираем тёмный оттенок.
# 3. Персонаж визуально темнеет (MaterialPropertyBlock на SMR).
# 4. Equip шлем → ColorPicker "Шлем" → шлем меняет цвет.
# 5. Unequip → цвет шлема сбрасывается (визуал удаляется).
# 6. Re-equip → цвет применяется заново из customisation.
```

---

## 4. Milestone 3 — L2, L5, Multiplayer Sync

**Цель:** пресеты тела (L2), лицевая настройка (L5), репликация customisation между игроками (sync).

**Definition of Done:**
- ✅ Dropdown "Пресет" в UI: Default / Athletic / Heavy / Slim.
- ✅ (Опционально) Слайдеры черт лица.
- ✅ Другой игрок видит мой пол/цвет/тело.

---

### T-CUS-07: L2 — Базовые пресеты тела (mesh variants ИЛИ blend shapes)

**Размер:** +5-7 дней (включая Blender работу).

**Зависимости:** mesh-варианты в Assets/ или blend shapes в модели.

**Изменения:**
- `CharacterCustomisationApplier` — добавить `ApplyPreset(BodyPresetId)` с переключением mesh variant.
- UI: Dropdown "Пресет" в CharacterWindow.

**Подробности:** см. `03_LEVELS_OF_CUSTOMISATION.md` §L2.

---

### T-CUS-08: Multiplayer Sync (NetworkVariable<CustomisationSnapshotDto>)

**Размер:** ~150 строк кода + UI синхронизация, ~3-5 дней.

**Зависимости:** M1 complete.

**Изменения:**
- `NetworkPlayer` — добавить `NetworkVariable<CustomisationSnapshotDto> _replicatedCustomisation`.
- `NetworkPlayer.RequestCustomisationRpc(snapshot)` — клиент → сервер → broadcast.
- `CharacterCustomisationApplier` — подписаться на `_replicatedCustomisation.OnValueChanged` (в дополнение к CustomisationClientState).

**Зачем:** другой игрок видит мой выбор пола/цвета. Без sync все видят M.

**Подробности:** см. `00_OVERVIEW.md` §6 (Variant B).

---

### T-CUS-11: L5 — Лицевая настройка (blend shapes / Morph3D / UMA 2)

**Размер:** +2-4 недели (зависит от варианта).

**Зависимости:** blend shapes в модели (Kevin Iglesias PRO) или интеграция UMA 2 / Morph3D.

**Изменения:**
- `CustomisationSave` — добавить `float[] dnaValues` (по числу DNA-слайдеров).
- `CharacterCustomisationApplier` — добавить `ApplyDna(values)`.

---

### T-CUS-12: Cosmetics catalog (per-class/per-faction presets unlock)

**Размер:** +1-2 недели (gameplay design + persistence).

**Зависимости:** M2 complete.

**Изменения:**
- Gameplay progression → unlock пресетов.
- CustomisationServer.GrantPresetAccess(playerId, presetId).

---

## 5. Anti-break список (по тикетам)

| Тикет | Файлы, которые меняются | Что НЕ трогаем |
|---|---|---|
| T-CUS-01 | `CharacterSaveData.cs`, новые файлы в `Customisation/` | Stats, Equipment, Skills |
| T-CUS-02 | Новые файлы DTO/ClientState | StatsServer, EquipmentServer |
| T-CUS-03 | `CharacterCustomisationApplier.cs` (new) | NetworkPlayer (только AddComponent) |
| T-CUS-04 | `PlayerAnimation_Female.overrideController` (new) | `PlayerAnimation.controller` (без изменений) |
| T-CUS-05 | `SetupCharacterCustomisationApplier.cs` (new) + Inspector настройки | Никаких .cs изменений |
| T-CUS-06 | `CharacterWindow.cs`, `CharacterWindow.uxml`, `CharacterWindow.uss` | SwitchTab pattern (только новая ветка) |
| T-CUS-09 | `CustomisationSave`, `CharacterCustomisationApplier`, UI | Stats, Equipment, Skills |
| T-CUS-10 | `CustomisationSave`, `CharacterEquipmentVisualApplier` (optional extension) | Шейдеры (требование URP/Lit) |
| T-CUS-07 | UI + CharacterCustomisationApplier | NetworkPlayer (без изменений) |
| T-CUS-08 | `NetworkPlayer.cs` (NetworkVariable + RPC) | Stats, Equipment, Skills |
| T-CUS-11 | CustomisationSave + CharacterCustomisationApplier | (нет breaking changes) |
| T-CUS-12 | CustomisationServer (new) + UI | Существующие серверы (только publish в WorldEventBus) |

---

## 6. Команды верификации (сводка)

### Compile check

```bash
# Открыть Unity 6000.4.1f1 → Console.
# Ожидаемо: 0 errors.
```

### EditMode tests

```bash
# Window → General → Test Runner → EditMode → Run All.
# Ожидаемо: passing (если тесты есть).
```

### PlayMode test

```bash
# Window → General → Test Runner → PlayMode → Run All.
# Ожидаемо: passing.
# Или ручной smoke (см. Phase-specific верификации).
```

### Manual smoke checklist (Russian)

```
M1 (после T-CUS-06):
1. Открыть Unity → BootstrapScene → Play (Host).
2. Подождать ~3 сек.
3. Tab → CharacterWindow → sub-tab "ВНЕШНОСТЬ".
4. Кликнуть "Ж" → персонаж = F-модель. ✓/✗
5. Console: "Applied bodyType=Female ..." ✓/✗
6. WASD — персонаж бегает на F-клипах (визуально отличается). ✓/✗
7. Надеть шлем → шлем на F-голове. ✓/✗
8. Cast скилла (ЛКМ) → skill animation работает. ✓/✗
9. Выйти из Play.
10. Открыть character_<id>.json → "customisation":{"bodyType":1,...}. ✓/✗
11. Снова Play → персонаж загружается как F. ✓/✗
```

---

## 7. Команды для разработчика (типичная сессия M1)

```bash
# === T-CUS-01 ===
1. Создать Assets/_Project/Scripts/Customisation/ папку.
2. Создать CharacterBodyType.cs, BodyPresetId.cs, HairStyleId.cs (enum-ы).
3. Создать CustomisationSave.cs (Serializable DTO).
4. Расширить CharacterSaveData.cs (+1 поле customisation).
5. Compile. Refresh_unity + read_console.

# === T-CUS-02 ===
6. Создать Assets/_Project/Scripts/Customisation/Dto/ папку.
7. Создать CustomisationSnapshotDto.cs.
8. Создать CustomisationClientState.cs.
9. Создать [CustomisationClientState] GameObject в BootstrapScene (MCP или ручная).
10. Compile.

# === T-CUS-03 ===
11. Создать CharacterCustomisationApplier.cs.
12. Compile.

# === T-CUS-04 ===
13. Создать Assets/_Project/Animations/PlayerAnimation_Female.overrideController:
    - ПКМ → Create → Animator Override Controller.
    - Controller = PlayerAnimation.controller.
14. Создать Editor/SetupFemaleAnimationOverride.cs.
15. Запустить Tools/ProjectC/Player/Setup Female Animation Override.
16. Проверить: PlayerAnimation_Female.overrideController → все motion-ы = HumanF@*.
17. Compile.

# === T-CUS-05 ===
18. Создать Editor/SetupCharacterCustomisationApplier.cs.
19. Запустить Tools/ProjectC/Player/Add CharacterCustomisationApplier to NetworkPlayer.
20. Открыть NetworkPlayer.prefab → назначить _maleMesh, _femaleMesh, _maleController, _femaleController.
21. Compile.

# === T-CUS-06 ===
22. Расширить CharacterWindow.uxml — sub-tab button + section.
23. Расширить CharacterWindow.uss — стили для toggle.
24. Расширить CharacterWindow.cs — handler InitCustomisationTab + RefreshCustomisationDisplay.
25. Compile.
26. Smoke test (см. §6).
```

---

## 8. Открытые вопросы (для будущих сессий)

1. **Будет ли Character Creation Screen?** Сейчас персонаж создаётся при первом входе (default Male). Нужен ли UI с выбором М/Ж при первом запуске? Или default = Male, потом через CharacterWindow → ВНЕШНОСТЬ меняется?
2. **Будет ли cosmetic-only inventory?** Отдельный магазин пресетов/цветов? Или unlock через gameplay?
3. **Будет ли Faction-locked cosmetic?** Разные фракции = разные базовые пресеты?
4. **Что с NPC?** NPC тоже должны иметь customisation (пол, цвет)? Если да — расширение `NpcVisualApplier`.
5. **Что с Cinematic камерами?** Крупные планы в катсценах — нужно ли дополнительное освещение для новых skin tones?

---

## 9. Связь с другими документами

| Документ | Назначение |
|---|---|
| `00_OVERVIEW.md` | Главный план, TL;DR |
| `01_CURRENT_CAPABILITIES.md` | Что уже есть в проекте |
| `02_DATA_MODEL.md` | Data types + signatures |
| `03_LEVELS_OF_CUSTOMISATION.md` | Детальное L1..L5 |
| `04_MALE_FEMALE_SWAP.md` | Глубокий разбор M↔F |
| `docs/Character/EquipmentVisual/00_DESIGN.md` | Паттерн для applier-а |
| `docs/Character/EquipmentVisual/03_PHASES.md` | Пример roadmap (Equipment Visual) |
| `docs/Character/08_ROADMAP.md` | Канонический шаблон Character roadmap |
| `docs/Character/CHANGELOG.md` | История изменений подсистемы Character |