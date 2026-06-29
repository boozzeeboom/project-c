# Equipment Visual — Phased Implementation Plan

> Пошаговый план реализации с командами верификации.
> Каждая фаза — отдельная короткая сессия. **Пользователь сам запускает Unity и проверяет** (по AGENTS.md: Mavis не делает git commit / не запускает tests / не триггерит build).

---

## Стратегия

| Принцип | Реализация |
|---|---|
| **Add-only** | Никаких breaking changes в существующих файлах. Только новые поля и новые компоненты. |
| **Маленькие шаги** | Каждая фаза = 1 коммит, легко откатить. |
| **Stand-in визуал** | Начинаем с Cone/Capsule. Дизайнер заменит позже. |
| **Тест после каждой фазы** | Compile 0 errors + Play Mode smoke test. |

---

## Phase 1 — visualPrefab в ItemData + world visual

**Цель:** у каждого `ItemData` есть опциональное поле `visualPrefab`. `PickupItem` использует его для отображения в мире.

**Размер:** ~40 строк кода + 3 тестовых префаба. ~30 минут.

### Шаги

| # | Действие | Файл |
|---|---|---|
| 1.1 | Добавить поле `public GameObject visualPrefab;` в `ItemData` (см. `01_DATA_MODEL.md` §1) | `Assets/_Project/Scripts/Core/ItemType.cs` |
| 1.2 | Создать `Assets/_Project/Resources/Visuals/Equipment/` (новая папка) | - |
| 1.3 | Создать 3 простых префаба: `Visual_Helmet_Cone.prefab`, `Visual_Blade_Capsule.prefab`, `Visual_Boots_SmallCapsule.prefab` | `Assets/_Project/Resources/Visuals/Equipment/` |
| 1.4 | Расширить `PickupItem.cs`: если `itemData.visualPrefab != null` → spawn как child вместо SpriteRenderer | `Assets/_Project/Scripts/Core/PickupItem.cs` |
| 1.5 | Привязать тестовые префабы к 2-3 существующим .asset'ам (например, `Clothing_WorkerHelmet.asset` → `Visual_Helmet_Cone.prefab`) | `.asset` через Inspector |

### Верификация (Phase 1)

```bash
# 1. Compile — открыть Unity Editor, проверить Console:
#    Ожидаемо: 0 errors. Возможны warnings от null-check на visualPrefab (если есть — безопасно).

# 2. Edit Mode (Test Runner):
#    Window → General → Test Runner → EditMode → Run All
#    Ожидаемо: тестов нет (или все passing). Никаких regression'ов.

# 3. Play Mode smoke:
#    - Open BootstrapScene → Play.
#    - Console должен быть чистым.
#    - Drop тестового предмета через Debug-консоль или спавнер → визуал в мире (cone/capsule).
#    - Pick up предмет (E) → визуал исчезает (как раньше, иконка в UI).

# 4. Visual consistency:
#    - Inspector на любом .asset показывает новое поле "Visual Prefab".
#    - Default = None. Старые ассеты не сломаны.
```

### Чек-лист перед коммитом

- [ ] `ItemData.visualPrefab` добавлено, default = null
- [ ] `PickupItem` работает и со SpriteRenderer (старый путь), и с visualPrefab (новый)
- [ ] Compile 0 errors
- [ ] Play Mode: дроп+пикап работают
- [ ] Никаких изменений в существующих .asset (все null)
- [ ] Никаких изменений в `EquipmentServer`/`EquipmentClientState`

---

## Phase 2 — equip visual на персонаже M

**Цель:** при экипировке `ItemData.visualPrefab` спавнится на правильной кости скелета HumanM_Model.

**Размер:** ~250 строк кода + 1 editor script. ~1-2 часа.

### Шаги

| # | Действие | Файл |
|---|---|---|
| 2.1 | Создать namespace `ProjectC.Equipment.Visual` (новая папка `Assets/_Project/Scripts/Equipment/Visual/`) | - |
| 2.2 | Создать `EquipSlotToBone.cs` со static-маппингом (см. `01_DATA_MODEL.md` §2) | `Assets/_Project/Scripts/Equipment/Visual/EquipSlotToBone.cs` |
| 2.3 | Расширить `ItemData` опциональными attach-полями: `attachBoneOverride`, `attachPositionOffset`, `attachRotationOffset`, `attachScale` (см. `01_DATA_MODEL.md` §1) | `Assets/_Project/Scripts/Core/ItemType.cs` |
| 2.4 | Создать `CharacterEquipmentVisualApplier.cs` (см. `02_CHARACTER_APPLIER.md` §5) | `Assets/_Project/Scripts/Player/CharacterEquipmentVisualApplier.cs` |
| 2.5 | Создать `SetupEquipmentVisualApplier.cs` editor script (см. `02_CHARACTER_APPLIER.md` §7) | `Assets/_Project/Editor/SetupEquipmentVisualApplier.cs` |
| 2.6 | Запустить `Tools/ProjectC/Player/Add EquipmentVisualApplier to NetworkPlayer` (или через MCP) → компонент добавлен в prefab | - |
| 2.7 | Привязать `Visual_Helmet_Cone.prefab` к `Clothing_WorkerHelmet.asset` через Inspector | `.asset` |
| 2.8 | (опционально) Заполнить attach-offsets для тестового шлема — например, `attachPositionOffset = (0, 0.3, 0)`, `attachScale = (0.3, 0.3, 0.3)` | `.asset` |

### Верификация (Phase 2)

```bash
# 1. Compile — открыть Unity, проверить Console:
#    Ожидаемо: 0 errors.

# 2. Edit Mode (Test Runner):
#    Run All. Ожидаемо: passing (тесты могут быть пустыми).

# 3. Play Mode (полный flow):
#    - Open BootstrapScene → Play (Host).
#    - Подождать ~3 секунды (Equipment seed → "Рабочая каска" в Head).
#    - На персонаже должна появиться cone-визуал на голове.
#    - Console: 
#        [CharacterEquipmentVisualApplier] Spawned 'Visual_Head_Рабочая каска' on bone 'Head' (slot=Head).

# 4. Equip flow:
#    - Open Character Window (Tab).
#    - Удалить шлем (СНЯТЬ).
#    - Cone должен исчезнуть.
#    - Надеть снова → cone появился.

# 5. Unequip + Equip нового предмета:
#    - Drag "Рабочая каска" → Head → надели cone.
#    - Drag → снять → cone исчез.
#    - Надеть снова → cone снова есть.
#    - Это тестирует diff-логику (не должно быть утечек или stale visuals).

# 6. Multi-slot test (если есть одежда для ног):
#    - Надеть ботинки (Feet) + шлем (Head) → 2 visual'а, на правильных костях.

# 7. Inspector check:
#    - NetworkPlayer (runtime instance) должен иметь компонент CharacterEquipmentVisualApplier.
#    - В Visual_Model/Hierarchy должно быть 1-2 spawned GameObject'а с префиксом "Visual_*".
```

### Чек-лист перед коммитом

- [ ] `ItemData` расширен 5-ю полями (1 из Phase 1, 4 из Phase 2)
- [ ] `EquipSlotToBone.cs` создан, покрывает все 13 EquipSlot
- [ ] `CharacterEquipmentVisualApplier.cs` создан, подписывается на OnEquipmentUpdated
- [ ] Компонент добавлен в `NetworkPlayer.prefab` (через editor script или MCP)
- [ ] Compile 0 errors
- [ ] Play Mode: seed helmet отображается на голове
- [ ] Equip/unequip cycle не создаёт утечек (spawned visual count = 1 после каждого цикла)
- [ ] Никаких regression в Equipment/Stats/Skills подсистемах
- [ ] Никаких изменений в существующих .asset (только 1-2 тестовых подключения)

---

## Phase 3 — Polish (по запросу)

**Опционально.** Если Phase 2 работает, можно расширять.

| # | Действие | Размер |
|---|---|---|
| 3.1 | Нормальные low-poly меши от художника (заменяют cone/capsule) | арт-ассеты |
| 3.2 | Подключить больше .asset'ов к visualPrefab | работа дизайнера |
| 3.3 | Двуручное оружие (2 grip-точки, secondary bone) | расширение `attachBoneOverride` → массив |
| 3.4 | Hide base body mesh под бронёй | `SkinnedMeshRenderer.enabled` toggle |
| 3.5 | Multiplayer sync (другие игроки видят экипировку) | NetworkVariable per slot |
| 3.6 | Hide при boarding в ship | hook на `NetworkPlayer.IsInShip` |

---

## Команды верификации (сводка)

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
# Или ручной smoke (см. Phase 2 верификация #3-#6).
```

### Manual smoke checklist (Russian)

```
1. Открыть Unity → BootstrapScene → Play (Host).
2. Подождать seed (~3 сек).
3. На персонаже — шлем (cone)? ✓/✗
4. Console: есть лог "Spawned Visual_Head..."? ✓/✗
5. Tab → Character Window → Equipment tab.
6. Надеть ещё один предмет (если есть в инвентаре) → визуал на нужной кости?
7. Снять → визуал исчез?
8. Выйти из Play.
9. Снова Play — визуал не "прилип"? (anti-leak check)
```

---

## Связь с другими подсистемами

| Подсистема | Что проверить после Phase 2 |
|---|---|
| **Stats** | Equip шлема → STR/DEX/INT не должны измениться (visualPrefab не влияет на stat-bonus). |
| **Skills** | Skill effects применяются как раньше. |
| **Crafting** | Crafted clothing получает visualPrefab? Если нет — оставить null, дизайнер настроит позже. |
| **Inventory UI** | Иконка в UI — по-прежнему `icon` (Sprite), не visualPrefab. |
| **PickupItem** | Если ItemData в мире — использует visualPrefab. Подобран → удаляется. |
| **Network sync** | Visual — **client-side only**. На клиенте другого игрока наш шлем НЕ будет виден (anti-leak / MVP). Это нормально для MVP. |

---

## Открытые риски

| Риск | Митигация |
|---|---|
| HumanM_Model без кости `RightHand` | Проверить в Editor: Animator → Avatar → Configure. Если кости нет — Animator.isHuman == false, warning + no-op. |
| SkinnedMesh внутри visualPrefab конфликтует с SkinnedMesh персонажа | Это **не наша проблема** — designer делает visualPrefab со static MeshRenderer для типичных случаев (одежда — не скиннится). Для сложных случаев (Skinning) — Phase 3.4. |
| Множественные seed-шлемы в InventoryWorld при reconnect | Не наша проблема — это EquipmentServer.DoSeed (T-P09). Визуал просто применит последний snapshot. |
| VisualPrefab с анимациями внутри | SkinnedMesh со своим Animator конфликтует с персонаж-Animator. В MVP запрещаем: visualPrefab — static meshes only. Если нужен анимированный — Phase 3.4. |

---

## Сводная таблица: что меняется, что нет

| Файл / система | Изменяется? | Что |
|---|---|---|
| `Assets/_Project/Scripts/Core/ItemType.cs` | **да** | +5 полей |
| `Assets/_Project/Scripts/Core/PickupItem.cs` | **да** (Phase 1.4) | Опциональный spawn visualPrefab |
| `Assets/_Project/Scripts/Equipment/EquipSlot.cs` | нет | - |
| `Assets/_Project/Scripts/Equipment/ClothingItemData.cs` | нет | Наследует visualPrefab |
| `Assets/_Project/Scripts/Equipment/ModuleItemData.cs` | нет | Наследует visualPrefab |
| `Assets/_Project/Scripts/Equipment/WeaponItemData.cs` | нет | Наследует visualPrefab |
| `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` | нет | - |
| `Assets/_Project/Scripts/Equipment/EquipmentWorld.cs` | нет | - |
| `Assets/_Project/Scripts/Equipment/EquipmentClientState.cs` | нет | Используем как hook |
| `Assets/_Project/Scripts/Equipment/Visual/EquipSlotToBone.cs` | **новый** | Phase 2.2 |
| `Assets/_Project/Scripts/Player/CharacterEquipmentVisualApplier.cs` | **новый** | Phase 2.4 |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | нет | - |
| `Assets/_Project/Editor/SetupEquipmentVisualApplier.cs` | **новый** | Phase 2.5 |
| `Assets/_Project/Prefabs/NetworkPlayer.prefab` | **да** | +1 компонент (CharacterEquipmentVisualApplier) |
| `Assets/_Project/Resources/Visuals/Equipment/*` | **новые** | 3 тестовых префаба |
| `Assets/_Project/Resources/Items/**` | только .asset assignments | Designer вручную подключает visualPrefab |

---

## Связанные документы

| Документ | Назначение |
|---|---|
| `00_DESIGN.md` | Главный план (что/почему/как) |
| `01_DATA_MODEL.md` | Точные .cs-сигнатуры и поля |
| `02_CHARACTER_APPLIER.md` | Подробный разбор компонента |
| `docs/Character/05_CLOTHING_AND_MODULES.md` | Базовый дизайн Clothing/Module |
| `docs/Character/CHANGELOG.md` | История изменений подсистемы (добавим запись после Phase 1/2) |