# 👤 GDD 26: Character Customisation & Equipment Visual

**Версия:** v1.1 | **Последнее обновление:** 14 июля 2026 | **Статус:** ✅ L1+L3+L4 + Bug #1 + Equipment Visual Phase 2
**Автор:** Малков Леонид Андреевич

---

## 1. Описание системы

Кастомизация внешности персонажа (пол, пресет тела, цвета, волосы, одежда) + визуальное отображение экипированных предметов на персонаже через **EquipSlotToBone маппинг (13 EquipSlot → HumanBodyBones)**.

**Связанные подсистемы:**
- GDD_11_Inventory_Items.md §X.5.2 — Equipment Visual
- GDD_13_UI_UX_System.md §X.9 — CustomisationWindow UI
- GDD_01_Core_Gameplay.md — player controller
- `docs/Character/Customisation/` — полная документация customisation
- `docs/Character/EquipmentVisual/` — полная документация Equipment Visual Phase 2

**Тикеты:** T-CUS (L1+L3+L4 + Bug #1), T-EV (Phase 2 bone mapping — EquipSlotToBone + CharacterEquipmentVisualApplier)

---

## 2. Customisation Core

### 2.1 Данные

#### CharacterBodyType (enum)
```
Male, Female
```

#### BodyPresetId (enum)
```
Default, Athletic, Heavy, Slim, Elder, Young
```

#### HairStyleId (enum)
```
Bald, Short
```

#### CustomisationSave (DTO)

**Файл:** `Assets/_Project/Scripts/Customisation/CustomisationSave.cs`

`[Serializable]` JsonUtility DTO:

| Поле | Тип | Описание |
|------|-----|----------|
| `bodyType` | CharacterBodyType | Male/Female |
| `presetId` | BodyPresetId | 6 пресетов |
| `heightScale` | float | Рост (0.8–1.2) |
| `widthScale` | float | Ширина (0.8–1.2) |
| `skinColor` | Color | Цвет кожи |
| `hairColor` | Color | Цвет волос |
| `hairStyle` | HairStyleId | Bald/Short |
| `clothingColorOverrides` | ClothingColorOverride[] | per-slot override |

### 2.2 CustomisationClientState (singleton)

**Файл:** `Assets/_Project/Scripts/Customisation/CustomisationClientState.cs`

- `CurrentSnapshot` — текущий snapshot кастомизации (struct)
- `OnCustomisationUpdated` — event для апплаера
- `ApplyCustomisationSnapshot` — broadcast через NetworkPlayer

### 2.3 CustomisationWindow (UI)

**Файл:** `Assets/_Project/Scripts/Customisation/UI/CustomisationWindow.cs`

MonoBehaviour full-screen overlay (по паттерну SkillTreeWindow → CharacterWindow).

**Разделы UI:**
- Пол: Male / Female (радио-кнопки)
- Пресет тела: 6 вариантов (Default/Athletic/Heavy/Slim/Elder/Young)
- Цвет кожи: Color picker
- Цвет волос: Color picker
- Причёска: 2 стиля (Bald/Short, с preview)
- Цвет одежды: Color override

**Поток:** P → CharacterWindow → таб "Внешность" → CustomisationWindow → Apply → Broadcast через NetworkPlayer

### 2.4 Bug #1: Domain reload → невидимый персонаж

**Причина:** После domain reload `CustomisationClientState.CurrentSnapshot` = struct default (heightScale=0, widthScale=0). `CharacterCustomisationApplier` применял `_visualRoot.localScale = (0,0,0)` → персонаж становился невидимо мелким.

**Фикс:** Инициализация snapshot'а в `Awake()`/`Start()` с дефолтными значениями (heightScale=1.0, widthScale=1.0) до broadcast.

**Тикет:** T-CUS Bug #1

---

## 3. Equipment Visual System (Phase 2)

### 3.1 EquipSlotToBone Mapping

**Файл:** `Assets/_Project/Scripts/Equipment/Visual/EquipSlotToBone.cs`
**Namespace:** `ProjectC.Equipment.Visual`

Статический класс маппинга 13 `EquipSlot` → `HumanBodyBones`:

| EquipSlot | Bone | Описание |
|-----------|------|----------|
| `Head` | Head | Шлем |
| `Chest` | Spine | Нагрудник |
| `Legs` | Hips | Штаны |
| `Feet` | LeftFoot | Сапоги (симметричные) |
| `Back` | Spine | Плащ/ранец (offset через attachPositionOffset) |
| `Hands` | LeftHand | Перчатки (симметричные) |
| `Accessory1` | Spine | Аксессуар 1 (декоративный) |
| `Accessory2` | Spine | Аксессуар 2 |
| `WeaponMain` | RightHand | Основное оружие |
| `WeaponOff` | LeftHand | Парное оружие / щит |
| `Module1` | Spine | Имплант 1 |
| `Module2` | Spine | Имплант 2 |
| `Module3` | Spine | Имплант 3 |

**Per-item override:** `ItemData.attachBoneOverride` (HumanBodyBones) позволяет переопределить кость для конкретного предмета. Если `LastBone` — используется default маппинг по EquipSlot.

### 3.2 Компоненты

#### CharacterEquipmentVisualApplier

**Файл:** `Assets/_Project/Scripts/Player/CharacterEquipmentVisualApplier.cs`
*Примечание: компонент находится в `Player/`, не в `Customisation/`.*

- Единая точка входа для equipment visual
- Подписывается на `EquipmentClientState.OnEquipmentUpdated`
- Diff snapshot ↔ `_currentItems`, spawn/destroy по слоту
- Parent к кости через `EquipSlotToBone.TryGetBoneTransform`
- Anti-restrictive: warning + no-op если Animator не humanoid

#### visualPrefab на ItemData

**Поля добавлены T-EV-03:**

| Поле | Тип | Описание |
|------|-----|----------|
| `visualPrefab` | GameObject | 3D модель предмета (null default) |
| `attachBoneOverride` | HumanBodyBones | override bone (LastBone = default slot) |
| `attachPositionOffset` / `attachRotation` / `attachScale` | Vector3 | per-item настройка позиции/поворота/масштаба |

---

## 4. Workflow: изменение внешности

1. P → CharacterWindow → таб "Внешность"
2. CustomisationWindow открывается (full-screen overlay)
3. Игрок выбирает: пол → пресет → цвета → причёска → одежда
4. Apply → `CustomisationClientState.ApplyCustomisationSnapshot()`
5. Snapshot → `CustomisationSave` (JSON) → `PlayerPrefs`
6. `NetworkPlayer` broadcast snapshot всем клиентам через RPC
7. `CharacterCustomisationApplier.OnCustomisationUpdated` → apply scale/AOC/colors
8. `CharacterEquipmentVisualApplier` синхронизирует экипировку (diff → spawn/destroy per slot)

---

## 5. Связанные документы

- [GDD_11_Inventory_Items.md](GDD_11_Inventory_Items.md) §X.5 — WeaponItemData + Equipment Visual
- [GDD_13_UI_UX_System.md](GDD_13_UI_UX_System.md) §X.9 — CustomisationWindow UI
- [GDD_INDEX.md](GDD_INDEX.md) — общая навигация
- `docs/Character/Customisation/` — полная документация customisation (AUDIT_*, IMPLEMENTATION_DESIGN.md, LOG.md)
- `docs/Character/EquipmentVisual/` — полная документация Equipment Visual Phase 2 (00_DESIGN.md, 01_DATA_MODEL.md, 02_CHARACTER_APPLIER.md)
- `docs/MMO_Development_Plan.md` §1.17 — план Character Customisation
