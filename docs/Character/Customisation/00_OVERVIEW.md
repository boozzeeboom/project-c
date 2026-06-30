# Customisation — обзор системного анализа

> **Подсистема:** Character Customisation (additive layer поверх Stats / Equipment / Skills)
> **Дата:** 2026-06-30
> **Статус:** ✅ **L1 (M↔F) + L3 (рост/полнота) + L4 skin color — реализовано (июнь 2026)**
> **Базируется на:** `docs/Character/00_README.md`, `EquipmentVisual/*`, `02_V2_ARCHITECTURE.md`, `04_STATS_PROGRESSION.md`, существующий `NetworkPlayer` + `PlayerAnimation.controller`.
> **Цель:** оценить сложность и архитектуру подсистемы кастомизации персонажа игроком — от простого переключения М↔Ж до сложной системы слайдеров тела/лица/покраски. **БЕЗ переписывания существующих подсистем** — только additive layer.
> **Скоуп документа:** design-only аналитика + phased roadmap. Код пишется в следующих сессиях по тикетам из `05_PHASES_ROADMAP.md`.
> **Реализованные тикеты:** T-CUS-01..06 (L1), T-CUS-09 (L3), T-CUS-10 (L4 skin).

---

## 0. TL;DR

| Аспект | Решение / Вердикт |
|---|---|
| **Корень задачи** | Сейчас персонаж жёстко привязан к `HumanM_Model` (Kevin Iglesias M) и одному `PlayerAnimation.controller`. Все подсистемы (Stats, Equipment, Skills) — **не видят** пол/тело/рост, потому что им всё равно. |
| **Что НЕ ломаем** | `ItemData`, `ClothingItemData`, `ModuleItemData`, `WeaponItemData`, `EquipmentServer`, `EquipmentClientState`, `EquipmentVisualApplier`, `SkillNodeConfig.attackClip`, `SkillAnimationPlayer`, `StatsServer`, `CharacterSaveData`, `JsonCharacterDataRepository`, `NetworkPlayer.Update`-логика движения. |
| **Базовый факт №1** | В `Assets/Kevin Iglesias/Human Animations/Models/` лежат **обе модели** — `HumanM_Model.fbx` и `HumanF_Model.fbx`. Скелет — generic Humanoid (одинаковый набор костей, Unity `HumanBodyBones` enum покрывает оба). |
| **Базовый факт №2** | В `Assets/Kevin Iglesias/Human Animations/Animations/Female/` есть **полный набор locomotion-клипов** для F: `HumanF@Walk01_Forward`, `HumanF@Run01_Forward`, `HumanF@Sprint01_Forward`, `HumanF@Idle01/02`, `HumanF@Turn01_Left/Right`, `HumanF@Jump01`, `HumanF@Fall01`, `HumanF@Land01`. Имена совпадают с M, отличается только префикс `HumanF@` vs `HumanM@`. Это значит AnimatorOverrideController может их подменить без правок стейт-машины. |
| **Базовый факт №3** | В коде уже есть инфраструктура для визуальных подмен: `CharacterEquipmentVisualApplier` (parent к `HumanBodyBones`), `EquipSlotToBone` (таблица маппинга EquipSlot → bone), `ItemData.visualPrefab`. Шаблон — additive, по аналогии с `NpcVisualApplier` (T-NPC-05). |
|| **M ↔ F переключение** | **✅ Реализовано (T-CUS-01..06)**. ~3-5 дней работы одного разработчика. Animator Override Controller с двумя наборами клипов (M / F), runtime-swap `SkinnedMeshRenderer.sharedMesh` или `Animator.runtimeAnimatorController`. Никакого нового кода в подсистемах движения/скиллов. |
|| **Кастомизация рост/вес/телосложение** | **✅ Реализовано (T-CUS-09)** через `Visual_Model.localScale = (width, height, width)`. Диапазон 0.7–1.3. Слайдеры с auto-save и кнопкой СБРОСИТЬ. |
|| **Лицевая настройка** | **Сложно**. Стандартное решение — UMA 2 / Morph3D / CC3 (Character Creator 3). Требует интеграции отдельного SDK. ~2-4 недели на настройку pipeline. (post-MVP) |
|| **Покраска (skin color — MVP)** | **✅ Реализовано (T-CUS-10)**: skin color через RGB слайдеры + MaterialPropertyBlock на SMR (`_BaseColor`). Hair/clothing deferred. |
|| **Persistence** | **✅ Реализовано**: отдельный файл `persistentDataPath/Customisation/customisation_<clientId>.json`. Изолирован от StatsServer. |
| **Скиллы и combat** | Полностью отделены от визуала через `SkillAnimationPlayer` + `AnimatorOverrideController`. SkillAnimationPlayer подменяет motion в state "Skill" — если мы подменим runtimeAnimatorController на F-версию, Skill-клипы подменятся автоматически (при условии что F-версия state-machine идентична). **Разница только в idle/walk/run** — как раз то, что нужно. |
| **UI** | `CharacterWindow.cs` уже имеет 6 top-level табов + sub-tabs в "ПРОГРЕССИЯ". Кастомизация логично встаёт как **ещё один sub-tab** "ВНЕШНОСТЬ" рядом со Статами/Одеждой/Модулями/Навыками. Или как **отдельный top-level таб** "ВНЕШНОСТЬ" — на UX-выбор. |
| **Multiplayer sync** | Выбор персонажа (пол/тело/цвета) — это **client-only** (каждый игрок видит только себя + других со своим customisation). Альтернатива — реплицировать выбор через `NetworkVariable<byte>` (1 байт на пол). Минимальное изменение, см. §6 roadmap. |

---

## 1. Что эта подсистема даёт игроку

Минимальная (L1) → максимальная (L5) градация:

| L | Что даёт | Где сложность | Трудоёмкость |
|---|---|---|---|
| **L1: Выбор М/Ж** | Переключение базовой модели. Скиллы, статы, прогрессия — общие. | Mesh swap + Animator Override Controller swap | **~3-5 дней** |
| **L2: + Базовые пресеты** | 2-4 пресета лица/тела на выбор (молодой/старый, толстый/худой). | Blend shapes или 2-4 mesh-варианта | **+5-7 дней** |
|| **L3: + Слайдеры тела** | Рост, полнота — слайдерами (реализовано через `Visual_Model.localScale`). | `transform.localScale` + CharacterController height | **✅ Done** |
|| **L4: + Покраска (skin — MVP)** | Цвет кожи (через `MaterialPropertyBlock`). Hair/clothing отложены. | Shader properties + RGB слайдеры UI | **✅ Done** (MVP: skin only) |
| **L5: + Лицевая настройка** | Слайдеры черт лица (нос, глаза, рот, подбородок). | UMA 2 / Morph3D / CC3 SDK | **+2-4 недели** |

**Итого максимум: ~6-8 недель** одного разработчика на полную систему.

**Рекомендуемая стартовая точка:** **L1 → L3 → L4 (skin)** — **✅ всё реализовано** (июнь 2026).

---

## 2. Архитектурные принципы

### 2.1 Add-only (никаких breaking changes)

Все существующие подсистемы **ничего не знают** о поле/росте/цвете персонажа. Они работают с `ItemData`, `EquipmentSnapshotDto`, `SkillNodeConfig`, `PlayerStats` — это всё data-only, без визуала.

Кастомизация — **отдельная ортогональная подсистема**, которая:
- читает **одну новую** структуру `CustomisationSave` (пол, рост, вес, цвета);
- применяет её через **один новый** компонент `CharacterCustomisationApplier` на `NetworkPlayer`;
- **не трогает** Stats, Equipment, Skills, Combat.

### 2.2 Runtime-swap (без перезапуска сцены)

Все переключения — runtime:
- Смена пола → mesh swap (instant), Animator Override Controller swap (следующий кадр), clothing visuals пересоздаются (`CharacterEquipmentVisualApplier` уже умеет diff).
- Смена пресета/слайдера → blend shape weight update (instant).
- Смена цвета → `MaterialPropertyBlock.SetColor` (instant).

Никаких перезагрузок сцены, никакого SceneManager.LoadScene.

### 2.3 Persistence через существующий JsonCharacterDataRepository

`CharacterSaveData` (T-P06) уже сериализуется через `JsonUtility` в `character_<clientId>.json`. Достаточно добавить одну новую секцию:

```csharp
public class CharacterSaveData {
    public PlayerStatsSave stats = new();
    public EquipmentSave equipment = new();
    public SkillsSave skills = new();
    // NEW: Customisation (additive — old .json без этого поля загружаются как default)
    public CustomisationSave customisation = new();
}
```

`JsonUtility` игнорирует отсутствующие поля → backward-compat с уже сохранёнными персонажами.

### 2.4 Server vs Client ownership

- **Сервер авторитативен для Stats/Equipment/Skills.** Persist происходит на сервере.
- **Кастомизация — client-only** (каждый игрок сам выбирает свой пол/рост/цвет). Persist — на клиенте (файл `customisation_<clientId>.json`) ИЛИ в той же `CharacterSaveData` (тогда сервер просто хранит и возвращает клиенту по запросу).
- **Multiplayer sync:** другой игрок видит МОЙ выбор пола/цвета → реплицировать через `NetworkVariable<CustomisationSnapshot>` на `NetworkPlayer`. Это отдельный тикет.

---

## 3. Структура документа

```
docs/Character/Customisation/
├── 00_OVERVIEW.md              ← этот файл — TL;DR + принципы
├── 01_CURRENT_CAPABILITIES.md  ← что в проекте уже есть и готово к расширению
├── 02_DATA_MODEL.md            ← CustomisationData, CustomisationSave, enum-ы
├── 03_LEVELS_OF_CUSTOMISATION.md  ← детальное описание L1..L5 (что, как, сложность)
├── 04_MALE_FEMALE_SWAP.md      ← отдельный глубокий разбор M↔F (наш первый приоритет)
├── 05_PHASES_ROADMAP.md        ← T-CUS-01..T-CUS-08 тикеты в 3 milestone'ах
└── CHANGELOG.md                ← журнал решений
```

---

## 4. Связь с другими документами

| Документ | Что взять |
|---|---|
| `docs/Character/00_README.md` | Каталог существующих подсистем персонажа |
| `docs/Character/EquipmentVisual/00_DESIGN.md` | Готовая архитектура визуального аппликатора — паттерн для Customisation |
| `docs/Character/EquipmentVisual/02_CHARACTER_APPLIER.md` | Diff-логика snapshot vs current — переиспользовать для customisation |
| `docs/Character/02_V2_ARCHITECTURE.md` | Server hub + ClientState + DTO pattern — добавляем `CustomisationClientState` |
| `docs/Character/03_DATA_MODEL.md` | Как устроены SO и save data — паттерн для `CustomisationData` |
| `docs/Character/05_CLOTHING_AND_MODULES.md` | Паттерн `EquipSlot` enum + ItemData subclass — паттерн для `BodyPreset` enum |
| `docs/Character/07_UI_TABS_IN_CHARACTER_WINDOW.md` | Как добавлять sub-tab в CharacterWindow — паттерн для таба "ВНЕШНОСТЬ" |
| `docs/Character/CHANGELOG.md` | История решений подсистемы Character |
| `Assets/_Project/Scripts/Player/CharacterEquipmentVisualApplier.cs` | 100% переиспользуемая архитектура для swap visuals |
| `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | Где добавить `CharacterCustomisationApplier` (новый компонент, additive) |
| `Assets/_Project/Scripts/Stats/Persistence/CharacterSaveData.cs` | Куда добавить `CustomisationSave` секцию |

---

## 5. Что в этой подсистеме НЕ делаем

| Не делаем | Почему |
|---|---|
| ❌ Модифицировать `ItemData` / `ClothingItemData` / etc. | Кастомизация — orthogonal слой. Одежда как раньше висит на `HumanBodyBones`, персонаж отдельно меняет пол/тело. |
| ❌ Переписывать `NetworkPlayer.Update` | Движение/ввод не зависят от пола. |
| ❌ Переписывать `SkillAnimationPlayer` | Skill clips подменяются через Animator Override Controller автоматически. |
| ❌ Трогать `EquipmentServer` / `EquipmentClientState` | Equipment pipeline не зависит от визуала персонажа. |
| ❌ Создавать новые `MonoBehaviour` для каждого цвета | Один `CharacterCustomisationApplier` с MaterialPropertyBlock покрывает все цвета. |
| ❌ Делать M/F отдельные AnimatorController-ы вручную | У нас уже есть один `PlayerAnimation.controller` + AnimatorOverrideController для skills. M↔F делается через AnimatorOverrideController swap, не дублированием стейт-машин. |

---

## 6. Multiplayer sync — отдельный вопрос

По умолчанию кастомизация — client-only. Но есть варианты:

**Вариант A: Client-only (default).**
- Persist локально в `customisation_<clientId>.json`.
- Другие игроки видят стандартную M-модель без кастомизации.
- Самое простое, без изменений в сетевом коде.
- Подходит для MVP.

**Вариант B: Server-replicated через NetworkVariable.**
- `NetworkVariable<CustomisationSnapshotDto>` на `NetworkPlayer`.
- Каждый клиент видит customisation всех остальных игроков.
- Требует расширения `NetworkPlayer` (add field) и `EquipmentSnapshotDto`-подобного DTO.
- +1-2 дня работы, можно сделать параллельно с L1.

**Вариант C: Server-authoritative (как Stats).**
- Сервер хранит customisation в `CharacterSaveData`.
- Клиент отправляет изменения через RPC.
- Избыточно для cosmetic-данных, но консистентно с остальной архитектурой.
- +3-5 дней работы.

**Рекомендация:** стартуем с **A**, переходим на **B** когда появится потребность видеть других игроков кастомизированными (это важно для MMO-sandbox).

---

## 7. Следующий шаг

**Прочитай `01_CURRENT_CAPABILITIES.md`** → там разбор что уже готово (точки расширения, которые мы нашли).
**Затем `03_LEVELS_OF_CUSTOMISATION.md`** → детальное описание каждого уровня L1..L5 с трудоёмкостью.
**Затем `04_MALE_FEMALE_SWAP.md`** → самый приоритетный уровень, разобран отдельно с кодом.
**Затем `05_PHASES_ROADMAP.md`** → тикеты T-CUS-01..T-CUS-08 в 3 milestone'ах.