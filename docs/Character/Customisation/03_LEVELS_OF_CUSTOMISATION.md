# Levels of Customisation — от L1 до L5

> **Дата:** 2026-06-30
> **Цель:** детальное описание каждого уровня кастомизации — что входит, как реализуется, трудоёмкость, зависимости.
> **Принцип:** каждый уровень **опциональный**, можно делать независимо. L1 (М↔Ж) — наш первый приоритет, разобран отдельно в `04_MALE_FEMALE_SWAP.md`.

---

## 0. Карта уровней

| L | Название | Что даёт игроку | Трудоёмкость | Зависимости |
|---|---|---|---|---|
| **L1** | Выбор М/Ж | Переключение между `HumanM_Model` и `HumanF_Model`. Скиллы/статы/экипировка — общие. | **~3-5 дней** | Только AnimatorOverrideController + mesh swap |
| **L2** | Базовые пресеты | 2-4 пресета тела/лица на выбор (Athletic / Heavy / Slim). | **+5-7 дней** | L1 + blend shapes ИЛИ mesh-варианты |
| **L3** | Слайдеры тела | Рост, полнота, мускулистость — слайдерами. | **+7-14 дней** | L2 + blend shapes (PRO-версия модели) ИЛИ runtime mesh-morph |
| **L4** | Покраска | Цвет кожи, волос, одежды (через `MaterialPropertyBlock`). | **+3-5 дней** | L1 + UI palette picker + shader property override |
| **L5** | Лицевая настройка | Слайдеры черт лица (нос, глаза, рот). | **+2-4 недели** | L3 + UMA 2 / Morph3D / CC3 SDK |

**Рекомендуемый MVP:** **L1 → L4** (пропустить L2/L3/L5 в первой итерации, оставить на потом).

---

## L1 — Выбор М / Ж (минимальная кастомизация)

### Что даёт

- Игрок при создании персонажа (или через меню "ВНЕШНОСТЬ") выбирает Male или Female.
- Модель персонажа меняется на `HumanF_Model.fbx` (или обратно на `HumanM_Model.fbx`).
- Все анимации (idle/walk/run/jump/skill) автоматически переключаются на F-версии.
- Экипировка, оружие, модули — продолжают работать (parent к костям через `EquipSlotToBone` → `HumanBodyBones`, skeleton одинаковый).

### Что нужно

| Ассет | Источник | Готов? |
|---|---|---|
| `HumanM_Model.fbx` | `Assets/Kevin Iglesias/Human Animations/Models/` | ✅ Уже в `NetworkPlayer.prefab` |
| `HumanF_Model.fbx` | `Assets/Kevin Iglesias/Human Animations/Models/` | ✅ Доступен, не подцеплен |
| Male animations (Walk/Run/Sprint/Jump/...) | `Animations/Male/Movement/` | ✅ Уже подключены через `PlayerAnimation.controller` |
| Female animations | `Animations/Female/Movement/` | ✅ Доступны (те же имена, префикс `HumanF@`) |
| `PlayerAnimation_Female.overrideController` | Новый файл | ❌ Создать |

### Как реализуется (runtime)

1. `CharacterCustomisationApplier.ApplyBodyType(CharacterBodyType.Female)`:
   - `SkinnedMeshRenderer.sharedMesh = _femaleMesh`
   - `Animator.runtimeAnimatorController = _femaleController`
2. SkillAnimationPlayer подменяет motion в state "Skill" — работает прозрачно, потому что state-machine у M и F controller'ов идентична.
3. CharacterEquipmentVisualApplier получает новый EquipmentSnapshotDto → пересоздаёт spawned visuals на новом меше (mesh — child кости, не зависит от SkinnedMeshRenderer).

### Трудоёмкость

- **Создать `PlayerAnimation_Female.overrideController`** — drag-and-drop в Editor, 5 минут.
- **Создать `CharacterCustomisationApplier.cs`** — ~150 строк, см. `04_MALE_FEMALE_SWAP.md` §5.
- **Расширить `CharacterSaveData`** — 1 строка.
- **Создать `CustomisationClientState.cs`** — ~80 строк (singleton, события).
- **UI: sub-tab "ВНЕШНОСТЬ"** — кнопка Male/Female + превью — ~50 строк UXML/USS + handler в `CharacterWindow.cs`.

**Итого: ~3-5 дней** одного разработчика.

### Edge cases

| Случай | Что происходит | Нужно обработать |
|---|---|---|
| Animator не humanoid | Ловим `if (!_animator.isHuman) return` | Да, в ApplyBodyType |
| Female controller не задан в Inspector | Warning + skip | Да |
| Equipment visuals созданы на M, меняем на F | `_currentItems` диффится, но spawned GO — child кости, не меш → они продолжат висеть. SkinnedMeshRenderer.sharedMesh swap **не** ломает parent-relations. | Не проблема |
| Camera transition во время swap | Мгновенный snap, без animation | Приемлемо для MVP |
| Загрузка .json без customisation поля | `JsonUtility` создаёт `new CustomisationSave()` → `bodyType = Male` | Уже работает (backward-compat) |

---

## L2 — Базовые пресеты тела/лица

### Что даёт

- В дополнение к выбору М/Ж — 2-4 пресета: "Athletic" / "Heavy" / "Slim" / "Elder".
- Каждый пресет = набор blend shape weights ИЛИ отдельный mesh-вариант.
- Преcет определяется через `BodyPresetId` enum + опционально `CustomisationSave.presetId`.

### Вариант A: через Blend Shapes (если есть в модели)

**Зависимости:** модель должна иметь blend shapes для каждого параметра (рост, полнота, мускулистость). Kevin Iglesias FREE pack **не имеет** blend shapes (это PRO feature).

**Если в будущем купим PRO-версию или свою морф-систему:**

```csharp
// ApplyPreset:
private void ApplyPreset(BodyPresetId preset)
{
    if (_bodyRenderer == null || _bodyRenderer.sharedMesh == null) return;
    if (_bodyRenderer.sharedMesh.blendShapeCount == 0) return;

    // Reset all blend shapes.
    for (int i = 0; i < _bodyRenderer.sharedMesh.blendShapeCount; i++)
        _bodyRenderer.SetBlendShapeWeight(i, 0f);

    // Apply preset (preset → blend shape weights mapping).
    switch (preset)
    {
        case BodyPresetId.Athletic:
            _bodyRenderer.SetBlendShapeWeight(_athleticShapeIdx, 100f);
            break;
        case BodyPresetId.Heavy:
            _bodyRenderer.SetBlendShapeWeight(_heavyShapeIdx, 100f);
            break;
        // ...
    }
}
```

### Вариант B: через Mesh variants (без blend shapes)

**Решение для Kevin Iglesias FREE:** создать 2-4 mesh-варианта в Blender (вручную или через меш-деформацию), импортировать как отдельные mesh, подменять `SkinnedMeshRenderer.sharedMesh`.

**Трудоёмкость:** +1-2 дня на каждый mesh-вариант (Blender работа) + ~30 строк кода в applier.

### Рекомендация

**Для MVP — вариант B с 1-2 пресетами** (по одному extra mesh для Male и Female). Если в будущем PRO-версия или своя морф-система — мигрируем на blend shapes без изменения API.

### Трудоёмкость

- **L2.VariantA (blend shapes):** +1 день на настройку weights + 50 строк кода.
- **L2.VariantB (mesh variants):** +5-7 дней (включая Blender работу для 2 пресетов).
- **UI:** dropdown "Пресет" — +20 строк.

**Итого: +5-7 дней** для L2.

### Edge cases

| Случай | Решение |
|---|---|
| Пресет = Default | Ничего не делаем (default mesh) |
| Mesh variant не задан в Inspector | Warning + fallback на Default |
| Преcет применяется вместе с М↔Ж сменой | Применяем в порядке: bodyType → preset → proportions → colors |

---

## L3 — Слайдеры тела (рост, полнота)

### Что даёт

- Игрок видит 2-3 слайдера в UI:
  - **Рост:** 0.85 - 1.15 (default 1.0)
  - **Полнота (XZ scale):** 0.85 - 1.15 (default 1.0)
  - (опционально) **Мускулистость:** верхняя часть тела X-scale
- Слайдеры → `CustomisationSave.heightScale` / `widthScale` → `transform.localScale`.

### Как реализуется (самое простое)

**Через transform.localScale — работает для любой модели:**

```csharp
private void ApplyProportions(float heightScale, float widthScale)
{
    if (_visualRoot == null) return;
    _visualRoot.localScale = new Vector3(widthScale, heightScale, widthScale);
}
```

**Caveats:**
- `CharacterController.height` НЕ подстраивается автоматически — нужно отдельно пересчитать `height` и `center`.
- Если visual mesh растянут — equip visuals (parent к костям) тоже растянутся (потому что parent localScale).
- Camera/ThirdPerson может "уплыть" — нужно подобрать camera distance.

### Альтернатива: через blend shapes (если модель поддерживает)

Более аккуратно (нет проблемы с CharacterController), но нужны blend shapes в модели.

### Трудоёмкость

**Вариант A (transform.localScale):**
- ~30 строк кода в applier.
- +10 строк в CharacterController для подстройки height/center.
- UI: 2 Slider'а — ~40 строк.
- Тест на camera distance.

**Итого: +2-3 дня.**

**Вариант B (blend shapes):**
- Требует mesh с blend shapes (доп. ассеты).
- +5-7 дней включая работу с художником.

**Рекомендация:** стартуем с **Вариантом A** (transform.localScale). Если визуально не устраивает — мигрируем на B.

### Edge cases

| Случай | Решение |
|---|---|
| Рост < 0.5 или > 2.0 | Clamp в `ApplyProportions` |
| CharacterController.height рассинхронизирован | Пересчитать на каждый пропорциональный change |
| Equip visuals не растягиваются пропорционально | parent scale применяется автоматически — должно работать |
| Camera distance не подходит | Отдельный fix в ThirdPersonCamera |
| Другой игрок видит наш scale | Replicated через `CustomisationSnapshotDto.heightScale/widthScale` |

---

## L4 — Покраска (skin color only — MVP)

### Что даёт (MVP)

- **Цвет кожи:** RGB слайдеры 0..1 (UI labels 0-255) + preview swatch.
- Применяется через `MaterialPropertyBlock` на SkinnedMeshRenderer персонажа (`_BaseColor` URP/Lit shader property).
- Изменения в realtime при движении ползунка.
- Кнопка "СБРОСИТЬ ЦВЕТ" → (1.0, 0.8, 0.6) — светлый skin.

### Что НЕ вошло в MVP

- ❌ **Цвет волос** — deferred. Сначала нужен hair mesh asset.
- ❌ **Цвет одежды** — deferred. Требует расширения `CharacterEquipmentVisualApplier` для per-EquipSlot color override.
- ❌ **Палитра (цветовые пресеты)** — deferred. В MVP используются слайдеры RGB.

### Как реализуется (сделано)

**В UI (3 слайдера + preview):**

```xml
<ui:Slider name="cw-skin-r-slider" low-value="0" high-value="1" value="1" .../>
<ui:Slider name="cw-skin-g-slider" low-value="0" high-value="1" value="0.8" .../>
<ui:Slider name="cw-skin-b-slider" low-value="0" high-value="1" value="0.6" .../>
<ui:VisualElement name="cw-skin-preview" class="cw-skin-preview" />
<!-- CSS: .cw-skin-preview { width:28px; height:28px; border-radius:3px; background-color обновляется из C# } -->
```

**В C# (handler):**

```csharp
private void OnSkinRSliderChanged(float newValue)
{
    _working.skinColorR = Mathf.Clamp01(newValue);
    UpdateSkinPreviewAndLabel();  // обновить swatch + RGB labels
    SaveWorking();                // JSON + ApplyCustomisationSnapshot → ApplyColors
}
```

**В applier (MaterialPropertyBlock):**

```csharp
private void ApplyColors(CustomisationSnapshotDto snapshot)
{
    var mpb = new MaterialPropertyBlock();
    _bodyRenderer.GetPropertyBlock(mpb);
    Color skin = snapshot.GetSkinColor();
    mpb.SetColor(_baseColorId, skin);  // _BaseColor для URP/Lit
    _bodyRenderer.SetPropertyBlock(mpb);
}
```

### Зависимости

- URP shader на персонаже должен иметь свойство `_BaseColor` (стандарт URP/Lit — да).
- Material на одежде тоже должен быть URP/Lit (если custom — дизайнер должен предусмотреть).

### Трулоёмкость

- ~40 строк кода в CharacterCustomisationApplier (skin + hair colors).
- ~30 строк расширения в CharacterEquipmentVisualApplier (clothing override).
- UI: RGB picker / HSV slider / palette — ~100 строк (palette самая дешёвая).
- Material shader check — 1 час.

**Итого: +3-5 дней.**

### Edge cases

| Случай | Решение |
|---|---|
| Shader не имеет `_BaseColor` | Warning + skip (MaterialPropertyBlock не сломает, просто эффекта не будет) |
| Material — instanced (после Instantiate) | MaterialPropertyBlock всё равно работает per-renderer |
| Hair mesh не spawned (style = Bald) | Skip hair color apply |
| Clothing visualPrefab имеет свой материал с текстурами | MPB.SetColor смешивается — цвет применяется поверх текстуры (можно сделать tint) |
| Persistence | CustomisationSave содержит RGBA → JsonUtility сохранит как 4 float'а |

---

## L5 — Лицевая настройка (нос, глаза, рот, подбородок)

### Что даёт

- Слайдеры для каждой черты лица.
- Полная уникализация персонажа (каждый игрок выглядит уникально).

### Проблема

**Kevin Iglesias FREE pack не имеет facial morphs.** Это PRO-фича (платная).

### Решения

**Вариант A: Купить Kevin Iglesias PRO / Premium Animations pack.**
- Содержит модели с facial blend shapes.
- Цена: ~$30-50 (single purchase, royalty-free).
- После покупки — те же blend shape weights, как в L3.

**Вариант B: Интегрировать UMA 2 (Unity Multipurpose Avatar).**
- Open-source, бесплатная.
- Мощная система: слайдеры DNA, одежда, hair, расовая статистика.
- Минусы: своя архитектура рецептов, ~2-4 недели на интеграцию + переписывание pipeline.

**Вариант C: Character Creator 3 (Reallusion).**
- Pipeline для импорта/экспорта mesh + skeleton + анимаций.
- iClone + CC3 combo — для качественной лицевой анимации (lip sync).
- Минусы: платная лицензия (~$1500+), сложный pipeline, ~3-6 недель интеграции.

**Вариант D: Morph3D.**
- SDK для Unity, поддержка blend shapes + DNA.
- ~$200/месяц подписка.

**Вариант E: Своя mesh-morph подсистема.**
- Берём HumanM_Model mesh, в Blender добавляем blend shapes через shape keys.
- ~3-5 дней на mesh + ~1-2 недели на UI/интеграцию.
- Самый дешёвый, но single-artist work.

### Рекомендация

**Не делаем L5 в MVP.** Если появится запрос от игроков — стартуем с **Варианта E** (свой mesh-morph) или покупаем Kevin Iglesias PRO.

### Трудоёмкость

- **Вариант A (Kevin Iglesias PRO):** +1-2 дня после покупки (mesh swap + blend shape setup).
- **Вариант E (свой mesh-morph):** +2-3 недели (Blender + UI + integration).
- **Вариант B (UMA 2):** +2-4 недели.
- **Вариант C (CC3):** +3-6 недель.

**Итого: +2-4 недели** в зависимости от варианта.

### Edge cases

| Случай | Решение |
|---|---|
| Blend shape count = 0 | Warning + fallback на Default (как L2) |
| DNA sliders слишком агрессивны | Clamp в UI |
| Persistence | CustomisationSave расширяется массивом `float[] dnaValues` (по числу DNA слайдеров) |

---

## Сводная таблица реализации

| L | Trigger | Method | Persistence | UI |
|---|---|---|---|---|
| **L1** | snapshot.bodyType | `ApplyBodyType` — SkinnedMeshRenderer + AnimatorOverrideController swap | `CustomisationSave.bodyType` | Toggle "М / Ж" |
| **L2** | snapshot.presetId | `ApplyPreset` — blend shapes или mesh variant | `CustomisationSave.presetId` | Dropdown |
| **L3** | snapshot.heightScale/widthScale | `ApplyProportions` — transform.localScale + CharacterController.height | `CustomisationSave.heightScale/widthScale` | 2 Slider'а |
| **L4** | snapshot.skinColor/hairColor/clothingOverrides | `ApplyColors` + `ApplyHair` + extension в `CharacterEquipmentVisualApplier` — MaterialPropertyBlock | RGBA поля + массив overrides | 3 ColorPicker'а + per-slot override |
| **L4** | snapshot.skinColorR/G/B | `ApplyColors` — MaterialPropertyBlock на SMR body (_BaseColor) | RGBA поля | 3 RGB Slider'а + preview swatch |

---

## Что сделано (июнь 2026)

| Приоритет | Уровень | Статус |
|---|---|---|
| 🥇 **L1** | М↔Ж | **✅ Done** |
| 🥈 **L4 (MVP)** | Покраска skin | **✅ Done** |
| 🥉 **L3** | Слайдеры тела (transform.localScale) | **✅ Done** |
| 4 | L2 | Отложен (требует доп. mesh ассетов или blend shapes) |
| 5 | L5 | Отложен (требует SDK или доп. ассеты) |

**Финальный результат:** L1 → L4 (skin) → L3 — **✅ всё реализовано (июнь 2026)**.