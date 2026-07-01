# 👤 GDD 26: Character Customisation & Equipment Visual

**Версия:** v1.0 | **Последнее обновление:** 30 июня 2026 | **Статус:** ✅ L1+L3+L4 реализован

---

## 1. Описание системы

Кастомизация внешности персонажа (пол, пресет тела, цвета, волосы, одежда) + визуальное отображение экипированных предметов на персонаже через bone mapping.

**Связанные подсистемы:**
- GDD_11_Inventory_Items.md §X.5.2 — Equipment Visual
- GDD_13_UI_UX_System.md §X.9 — CustomisationWindow UI
- GDD_01_Core_Gameplay.md — player controller
- `docs/Character/Customisation/` — полная документация

**Тикеты:** T-CUS (L1+L3+L4 + Bug #1), T-EV (Phase 2 bone mapping)

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

## 3. Equipment Visual System

### 3.1 Bone Mapping

| Слот | Bone (HumanBodyBones) | Описание |
|------|----------------------|----------|
| Weapon | RightHand | Оружие в правой руке |
| Shield | LeftHand | Щит в левой руке |
| Helmet | Head | Шлем |
| Chest | Spine | Нагрудник |
| Shoulders | LeftShoulder / RightShoulder | Наплечники |
| Gloves | LeftHand / RightHand | Перчатки |
| Boots | LeftFoot / RightFoot | Сапоги |
| Belt | Hips | Пояс |

### 3.2 Компоненты

#### CharacterEquipmentVisualApplier

**Файл:** `Assets/_Project/Scripts/Customisation/CharacterEquipmentVisualApplier.cs`

- Единая точка входа для customisation + equipment visual
- OnEquipmentChanged → Instantiate/Destroy visual prefab на bone
- Rate-limit N callback предотвращает duplicate equip

#### EquipmentVisualSocket

**Файл:** `Assets/_Project/Scripts/Items/EquipmentVisualSocket.cs`

Определение socket на скелете:
- `bone` (HumanBodyBones) — к какому bone крепить
- `positionOffset` (Vector3) — смещение позиции
- `rotationOffset` (Vector3) — смещение поворота
- `scale` (Vector3) — масштаб модели

#### visualPrefab на ItemData

**Файл:** `Assets/_Project/Scripts/Items/ItemData.cs`

Новые поля (добавлены T-EV-03):
- `visualPrefab` (GameObject) — 3D модель предмета (null default)
- `attachBoneOverride` (HumanBodyBones) — override bone (LastBone = default slot)
- `attachPositionOffset` / `attachRotation` / `attachScale` (Vector3) — per-item настройка

---

## 4. Workflow: изменение внешности

1. P → CharacterWindow → таб "Внешность"
2. CustomisationWindow открывается (full-screen overlay)
3. Игрок выбирает: пол → пресет → цвета → причёска → одежда
4. Apply → `CustomisationClientState.ApplyCustomisationSnapshot()`
5. Snapshot → `CustomisationSave` (JSON) → `PlayerPrefs`
6. `NetworkPlayer` broadcast snapshot всем клиентам через RPC
7. `CharacterCustomisationApplier.OnCustomisationUpdated` → apply scale/AOC/colors
8. `CharacterEquipmentVisualApplier` синхронизирует экипировку

---

## 5. Связанные документы

- [GDD_11_Inventory_Items.md](GDD_11_Inventory_Items.md) §X.5 — WeaponItemData + Equipment Visual
- [GDD_13_UI_UX_System.md](GDD_13_UI_UX_System.md) §X.9 — CustomisationWindow UI
- [GDD_INDEX.md](GDD_INDEX.md) — общая навигация
- `docs/Character/Customisation/` — полная документация (AUDIT_*, IMPLEMENTATION_DESIGN.md, LOG.md)
- `docs/MMO_Development_Plan.md` §1.17 — план Character Customisation
