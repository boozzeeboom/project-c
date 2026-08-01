# Custom Editor для SkillNodeConfig — Дизайн и руководство

> **Файл:** `Assets/_Project/Scripts/Editor/SkillNodeConfigEditor.cs`  
> **Дата:** 2026-07-28  
> **Цель:** Адаптивный инспектор — скрывает/показывает группы полей в зависимости от типа навыка.

---

## Обзор

`SkillNodeConfigEditor` заменяет стандартный инспектор Unity для `SkillNodeConfig` на структурированный вид с адаптивными группами. Дизайнер видит только те поля, которые релевантны текущему типу навыка.

## Визуальная структура инспектора

```
┌─────────────────────────────────────────┐
│         Skill: social_persuasion         │
│        ┌─────────────────────┐           │
│        │    🗣  SOCIAL        │  ← кнопка│
│        └─────────────────────┘           │
├─────────────────────────────────────────┤
│ 📋 Identity                    [развёрнут]│
│   skillId, displayName, desc, icon       │
├─────────────────────────────────────────┤
│ 🏷 Category & Discipline       [развёрнут]│
│   category, discipline, subtype          │
├─────────────────────────────────────────┤
│ 🔗 Prerequisites & Effects     [развёрнут]│
│   prerequisites[], effects[]             │
├─────────────────────────────────────────┤
│ 💰 Cost & Tier Requirements    [развёрнут]│
│   learnXpCost, STR/DEX/INT tiers         │
├─────────────────────────────────────────┤
│ 📍 UI Layout (Skill Tree)      [свёрнут] │
│   treeX, treeY                           │
├─────────────────────────────────────────┤
│   ⚔ Combat: Core               ← ТОЛЬКО │
│   🎬 Animation                 ← при     │
│   🎯 AOE Formula               ← Combat  │
│   💣 Throwables / 🏹 Bows      ← category│
│   ✨ VFX: Cast / 🚀 Projectile           │
│   💥 VFX: Impact / 🖼 2D                │
└─────────────────────────────────────────┘
```

## Правила видимости групп

| Группа | Social | Combat Passive | Combat Active | Доп. условие |
|---|---|---|---|---|
| 📋 Identity | ✅ | ✅ | ✅ | — |
| 🏷 Category & Discipline | ✅ | ✅ | ✅ | — |
| 🔗 Prerequisites & Effects | ✅ | ✅ | ✅ | — |
| 💰 Cost & Tier Reqs | ✅ | ✅ | ✅ | — |
| 📍 UI Layout | ✅ | ✅ | ✅ | — |
| ⚔ Combat: Core | ❌ | ✅ | ✅ | — |
| 🎬 Animation | ❌ | ❌ | ✅ | — |
| 🎯 AOE | ❌ | ❌ | ✅ | aoeFormula ≠ SingleTarget |
| 💣 Throwables | ❌ | ❌ | ✅ | subtype = Throwables |
| 🏹/🔩 Ranged | ❌ | ❌ | ✅ | subtype ∈ {Bows, Crossbows} |
| ✨ VFX: Cast | ❌ | ❌ | ✅ | — |
| 🚀 VFX: Projectile | ❌ | ❌ | ✅ | — |
| 💥 VFX: Impact | ❌ | ❌ | ✅ | — |
| 🖼 VFX: 2D | ❌ | ❌ | ✅ | — |

## Кнопка Social/Combat

- Зелёная `🗣 SOCIAL` и красная `⚔ COMBAT` — крупная кнопка в шапке инспектора
- При клике переключает `category`:
  - **Social → Combat:** поля AOE/VFX/анимации становятся видны, сохраняют предыдущие значения
  - **Combat → Social:** автоматический сброс:
    - `isActive = false`
    - `aoeFormula = SingleTarget`
    - Все VFX-ссылки = null
    - `discipline = None`, `subtype = None`
    - `requiredWeaponMask = None`

## Валидация в реальном времени

Инспектор показывает warning-сообщения при проблемах:

| Условие | Warning |
|---|---|
| `skillId` пуст | «skillId is empty — skill won't be findable at runtime» |
| Combat + Active + `attackClip = null` | «Active combat skill has no attackClip assigned» |
| Combat + Active + AOE≠Single + `aoeSize ≤ 0` | «AOE is {formula} but aoeSize = 0. No targets will be hit» |

## Disabled-поля для нерелевантных AOE-параметров

При AOE-формуле, не использующей некоторые поля, они показываются **затенёнными** (disabled), а не скрываются полностью — дизайнер видит что поле существует, но оно не применяется:

- `aoeConeAngleDeg` — disabled при всех формулах кроме Cone
- `aoeWidth` — disabled при всех формулах кроме Line и Box

## Совместимость с существующей OnValidate

В `SkillNodeConfig.cs` есть `#if UNITY_EDITOR OnValidate()` с:
- Авто-определением `discipline` по `skillId` prefix
- Cycle detection в prerequisites через DFS

Кастомный Editor **не конфликтует** с OnValidate — он просто управляет отображением полей. OnValidate продолжает работать как обычно (вызывается Unity при изменении любого поля).

## Тестирование

1. Открыть `Skill_Social_Persuasion.asset` → кнопка SOCIAL, видны только Identity/Category/Prereq/Effects/Cost/Layout
2. Открыть `Skill_Combat_BasicStrike.asset` → кнопка COMBAT, видны все группы
3. Нажать SOCIAL → COMBAT → появляются Combat-группы
4. Нажать COMBAT → SOCIAL → поля сбрасываются, группы скрываются
5. Выставить `isActive = false` на Combat → скрываются Animation/AOE/VFX
6. Выставить `subtype = Throwables` → появляется группа Throwables
7. Выставить `aoeFormula = SingleTarget` → AOE-параметры скрываются (показывается placeholder)

## История

| Дата | Изменения |
|---|---|
| 2026-07-28 | Создан SkillNodeConfigEditor с адаптивными группами, кнопкой Social/Combat, авто-сбросом, валидацией |
