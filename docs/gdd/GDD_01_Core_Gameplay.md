# GDD-01: Core Gameplay & Controls — Project C: The Clouds

**Версия:** 1.2 | **Дата:** 14 июля 2026 г. | **Статус:** ✅ Документировано + реализовано (Combat MVP, SkillTree, Input rebinding, Customisation, Crafting)
**Автор:** Малков Леонид Андреевич

---

## 1. Overview

Этот документ описывает **основной геймплейный цикл** и **систему управления** Project C: The Clouds. Игра имеет три основных режима: пеший, корабль и свободная камера (для разработки).

### Core Game Loop

```
┌──────────────────────────────────────────────────────┐
│                  CORE GAME LOOP                      │
│                                                      │
│  ┌──────────┐    ┌──────────┐    ┌──────────────┐   │
│  │ ПРИНИМИ  │───▶│ ВЫПОЛНИ  │───▶│ ПРОГРЕССИРУЙ │   │
│  │ КОНТРАКТ │    │ КОНТРАКТ │    │              │   │
│  └──────────┘    └──────────┘    └──────────────┘   │
│       ▲                                  │           │
│       │                                  ▼           │
│       │                       ┌──────────────────┐   │
│       │                       │ Новый контракт   │   │
│       │                       │ Улучшение корабля│   │
│       │                       │ Рост репутации   │   │
│       │                       └──────────────────┘   │
└──────────────────────────────────────────────────────┘

Детализация «ВЫПОЛНИ»:
  ├── Управляй кораблём (навигация, стыковка, торговля)
  ├── Исследуй локации пешком (NPC, квесты, лут)
  └── Управляй ресурсами (мезий, МНП, груз)
```

### Пример цикла

> Игрок принимает контракт на доставку груза в Тертиус → Загружает груз на корабль → Летит через облака (навигация по ГРАДАР) → Прибывает в Тертиус → Сдаёт груз → Получает кредиты + XP + репутацию → Находит квест на артефакт → Летит на заброшенную платформу → Находит артефакт в сундуке → Улучшает корабль → Открывает миссии Гильдии.

---

## 2. Player Fantasy

| Режим | Ощущение | Ключевые механики |
|-------|----------|-------------------|
| **Пеший** | Исследование, взаимодействие с миром | Ходьба, бег, прыжки, подбор, сундуки, NPC диалоги |
| **Корабль** | Свобода полёта, навигация в 3D | Антигравитация, тяга, тангаж, рыскание, лифт |
| **Свободная камера** | Наблюдение, обход мира (dev) | Полёт в любом направлении, телепорт к пикам |

---

## 3. Detailed Rules

### 3.1 Пеший режим (Walking State)

**Персонаж:** capsule (модель заменена на Mixamo-анимированную)
**Камера:** ThirdPersonCamera, расстояние 5м, орбитальная

**Физика движения:**
- Движение относительно камеры (вперёд = куда смотрит камера)
- Гравитация действует нормально (CharacterController)
- Прыжок: однократный, force = 8.0
- Бег: multiplier x2 к скорости

**Взаимодействия:**
- Подбор предметов: E, радиус 3м, приоритет сундукам
- Посадка в корабль: F, ближайший < 5м, проверка скорости < 1 м/с

### 3.2 Режим корабля (Flying State)

**Корабль:** сфера [🔴 Запланировано: FBX модель, 4 класса]
**Камера:** ThirdPersonCamera, расстояние 18м, следует за кораблём

**Физика полёта:**
- Антигравитация компенсирует гравитацию полностью
- Тяга: ветровые лопасти (W/S)
- Рыскание: поворот корпуса (A/D)
- Тангаж: наклон носа (мышь Y)
- Лифт: вертикальное движение (Q/E)
- Буст: x2 тяга (Left Shift)
- Стабилизация: возврат к горизонту при отсутствии ввода
- **Нет крена** — рамка-контур стабилизирует

**Взаимодействия:**
- Выход из корабля: F, проверка высоты < 3м, скорости < 1 м/с
- Кооп-пилотирование: 2+ игрока, ввод усредняется на сервере

### 3.3 Свободная камера (WorldCamera — dev режим)

**Назначение:** обход мира для разработки и тестирования
**Управление:**

| Клавиша | Действие |
|---------|----------|
| WASD | Полёт в любом направлении |
| Мышь | Вращение камеры |
| Scroll | Вверх/вниз |
| V | Toggle свободного полёта |
| N/B | Prev/Next пик |
| R | Random пик |
| H | Возврат на высоту облаков |

---

## 4. Controls — Полная таблица

### Пеший режим

| Клавиша | Действие | Параметр | Priority |
|---------|----------|----------|----------|
| W | Движение вперёд | speed = 5 м/с | 🔴 Core |
| S | Движение назад | speed = 5 м/с | 🔴 Core |
| A | Поворот влево | rotation speed | 🔴 Core |
| D | Поворот вправо | rotation speed | 🔴 Core |
| Space | Прыжок | force = 8.0 | 🔴 Core |
| Left Shift | Бег | speed x2 | 🟡 Core |
| Мышь | Вращение камеры | sensitivity = 2.0 | 🔴 Core |
| **F** | Сесть в корабль | radius < 5м | 🔴 Core |
| **E** | Подобрать предмет / сундук | radius < 3м | 🔴 Core |
| **Tab** | Круговой инвентарь | toggle | 🔴 Core |
| **P** | CharacterWindow (личный кабинет) | 5+ табов | 🔴 Core |
| **Escape** | EscMenu (настройки, controls, выход) | overlay-пауза | 🔴 Core |
| **R** | Target Lock (бой) | Lock-on врага | 🟡 Combat |
| **Q / E** | Cycle targets prev/next (бой) | Перебор целей | 🟡 Combat |
| **1-9** | Skill slots (бой) | Быстрые слоты навыков | 🟡 Combat |
| **K** | SkillTree | Дерево навыков | 🟡 Core |

### Режим корабля

| Клавиша | Действие | Параметр | Priority |
|---------|----------|----------|----------|
| W | Тяга вперёд | thrust = 50.0 | 🔴 Core |
| S | Тяга назад | thrust = -25.0 | 🔴 Core |
| A | Рыскание влево | yawSpeed = 60°/s | 🔴 Core |
| D | Рыскание вправо | yawSpeed = 60°/s | 🔴 Core |
| Q | Лифт вниз | liftSpeed = 10 м/с | 🔴 Core |
| E | Лифт вверх | liftSpeed = 10 м/с | 🔴 Core |
| Мышь Y | Тангаж | pitchSpeed = 45°/s | 🔴 Core |
| Left Shift | Буст (x2 тяга) | thrustMultiplier = 2.0 | 🟡 Core |
| **F** | Выйти из корабля | height < 3м, speed < 1м/с | 🔴 Core |
| **Escape** | EscMenu (настройки, controls, выход) | overlay-пауза | 🔴 Core |
| **P** | CharacterWindow (личный кабинет) | 5+ табов | 🔴 Core |
| **R** | Target Lock (бой) | Lock-on врага | 🟡 Combat |
| **K** | SkillTree | Дерево навыков | 🟡 Core |

### Зарезервированные клавиши (future)

| Клавиша | Назначение | Этап |
|---------|-----------|------|
| M | Открыть карту | Этап 4 |
| C | Открыть чат | Этап 4 |
| Ctrl | Присесть | Этап 2.5 |
| Right Shift | Прицеливание | Этап 3 |
| F1 | Помощь / туториал | Future |

---

## 5. Formulas

### Физика пешего режима

| Формула | Описание |
|---------|----------|
| `moveDirection = (camForward * inputZ + camRight * inputX).normalized` | Направление движения относительно камеры |
| `velocity = moveDirection * (isRunning ? runSpeed : walkSpeed)` | Скорость движения |
| `jumpVelocity = Vector3.up * jumpForce` | Скорость прыжка |
| `gravity = Physics.gravity.y * Time.deltaTime` | Гравитация |

### Физика корабля

| Формула | Описание |
|---------|----------|
| `gravityCompensation = -Physics.gravity * rb.mass` | Компенсация гравитации |
| `thrustForce = transform.forward * inputZ * baseThrust * boostMultiplier` | Сила тяги |
| `dragForce = -rb.velocity * drag` | Сопротивление воздуха |
| `stabilizationTorque = Quaternion.Slerp(currentRot, horizontalRot, stabSpeed * dt)` | Стабилизация |
| `yawTorque = Vector3.up * inputX * yawSpeed` | Рыскание |
| `pitchTorque = transform.right * mouseY * pitchSpeed` | Тангаж |
| `liftForce = Vector3.up * (inputQ - inputE) * liftSpeed` | Вертикальный лифт |
| `boostMultiplier = isBoosting ? 2.0f : 1.0f` | Множитель буста |
| `cooperativeInput = Sum(pilotInput[i]) / pilotCount` | Усреднение кооп-ввода |

### Переключение режимов

| Формула | Описание |
|---------|----------|
| `canBoard = nearestShip.distance < 5 && ship.velocity.magnitude < 1` | Условие посадки |
| `canDisembark = ship.height < 3 && ship.velocity.magnitude < 1` | Условие выхода |

---

## 6. Edge Cases

| Ситуация | Поведение | Реализация |
|----------|-----------|-----------|
| **Игрок нажимает F, но корабль далеко** | Ничего не происходит, проверка distance < 5м | ✅ PlayerStateMachine |
| **Игрок нажимает F на движущемся корабле** | Отказ, проверка speed < 1 м/с | ✅ PlayerStateMachine |
| **Игрок подбирает предмет, инвентарь полон** | [🔴 Запланировано] Предмет остаётся на земле | — |
| **Два игрока нажимают E на один предмет** | Сервер определяет порядок, первый получает | ✅ HidePickupRpc (SendTo.Everyone) |
| **Игрок выпал из корабля в воздухе** | [🔴 Запланировано] Телепорт на корабль или на землю | — |
| **Все пилоты вышли из корабля** | Корабль снижает скорость, затем зависает | ✅ ShipController: проверка pilotCount |
| **Корабль врезался в пик** | Отскок по физике Rigidbody | ✅ Физика Unity |
| **Дисконнект во время полёта** | Корабль зависает, инвентарь сохранён | ✅ NetworkManagerController |
| **Игрок застрял в текстуре** | [🔴 Запланировано] Teleport fallback | — |

---

## 7. Dependencies

| Зависит от | Описание |
|-----------|----------|
| PlayerController.cs | Пеший контроллер |
| ShipController.cs | Контроллер корабля |
| PlayerStateMachine.cs | Переключение режимов |
| ThirdPersonCamera.cs | Орбитальная камера |
| WorldCamera.cs | Свободная камера (dev) |
| ItemPickupSystem.cs | Подбор предметов |
| Inventory.cs | Инвентарь |
| InventoryUI.cs | Круговое колесо |
| ControlHintsUI.cs | Подсказки на экране |
| NetworkManagerController.cs | Сетевой контроллер |

---

## 8. Tuning Knobs

### Пеший режим

| Параметр | Мин | Макс | Текущее | Влияние |
|----------|-----|------|---------|---------|
| `walkSpeed` | 2 | 8 | 5 | Скорость ходьбы |
| `runSpeed` | 4 | 16 | 10 | Скорость бега |
| `jumpForce` | 3 | 15 | 8 | Высота прыжка |
| `cameraDistance` | 3 | 10 | 5 | Расстояние камеры |
| `cameraSensitivity` | 0.5 | 5.0 | 2.0 | Чувствительность мыши |
| `pickupRadius` | 1 | 10 | 3 | Радиус подбора |

### Корабль

| Параметр | Мин | Макс | Текущее | Влияние |
|----------|-----|------|---------|---------|
| `baseThrust` | 10 | 200 | 50 | Сила тяги |
| `backwardThrust` | 5 | 100 | 25 | Тяга назад |
| `drag` | 0.1 | 2.0 | 0.5 | Сопротивление |
| `stabilizationSpeed` | 0.5 | 10.0 | 2.0 | Скорость стабилизации |
| `yawSpeed` | 20 | 120 | 60 | Скорость рыскания |
| `pitchSpeed` | 15 | 90 | 45 | Скорость тангажа |
| `liftSpeed` | 5 | 30 | 10 | Скорость лифта |
| `boostMultiplier` | 1.5 | 5.0 | 2.0 | Множитель буста |
| `boardingRadius` | 2 | 15 | 5 | Радиус посадки |
| `disembarkHeight` | 1 | 10 | 3 | Макс. высота выхода |
| `disembarkSpeed` | 0 | 5 | 1 | Макс. скорость выхода |
| `cameraDistanceShip` | 10 | 30 | 18 | Расстояние камеры (корабль) |

---

## 9. Acceptance Criteria

| # | Критерий | Как проверить | Статус |
|---|----------|--------------|--------|
| 1 | WASD двигает персонажа относительно камеры | Запустить, проверить движение | ✅ |
| 2 | Space вызывает прыжок | Запустить, проверить прыжок | ✅ |
| 3 | Shift ускоряет бег x2 | Запустить, сравнить скорость | ✅ |
| 4 | F сажает в корабль (< 5м) | Подойти к кораблю, нажать F. **🟡 Требуется ключ** (см. §X ниже) | ✅ |
| 5 | F выводит из корабля (< 3м высота) | На низкой высоте нажать F | ✅ |
| 6 | Корабль летает (WASD + Q/E + мышь) | Запустить, проверить все оси | ✅ |
| 7 | Shift бустит корабль x2 | Проверить скорость с/без буста | ✅ |
| 8 | E подбирает предметы (< 3м) | Подойти к предмету, нажать E | ✅ |
| 9 | Tab открывает круговое колесо | Нажать Tab, проверить UI | ✅ |
| 10 | Камера адаптируется при смене режима | Сесть/выйти, проверить distance | ✅ |
| 11 | Кооп-пилотирование усредняет ввод | 2 игрока, проверить движение | ✅ |
| 12 | ControlHintsUI обновляется | Проверить подсказки на экране | ✅ |
| 13 | WorldCamera работает (dev) | V/N/B/R/H, проверить навигацию | ✅ |
| 14 | P открывает CharacterWindow | Нажать P, проверить 5+ табов | 🟢 DONE (2026-06-05) |
| 15 | E → NPC → DialogWindow | Подойти к [Mira], нажать E | 🟢 DONE (T-Q11b+c, 2026-06-08) |
| 16 | M11 Mira quest E2E | Pickup → E → trade → quest Complete | 🟢 DONE (2026-06-08) |

---

## X. Реализация в коде (дополнения 2026-06-05..09)

> **Секция добавлена Mavis 2026-06-10, обновлена 2026-07-14.** Дизайн-контент (Core Loop, режимы, управление) остаётся в зоне game-designer'а. Здесь — **статус реализации**: F-boarding, CharacterWindow, NPC dialog, Combat, Input rebinding, SkillTree, Customisation, Crafting.

### X.1 F-boarding с физическим ключом (R2-SHIP-KEY-001, 2026-06-06)

**Изменение:** F-посадка в корабль теперь **требует наличия ключа** в инвентаре пилота. Без ключа F не сработает (или сработает, но сервер откажет через 1.5 сек timeout).

**Поток:**
1. Игрок нажимает F рядом с кораблём
2. `NetworkPlayer` отправляет pre-F RPC `RequestCanBoard` (1.5 сек timeout)
3. `ShipKeyServer.CanPlayerBoard(clientId, netId)` → `InventoryWorld.HasItem(clientId, keyItemId)`
4. Если `true` → разрешить, отправить `CanPlayerBoard` response → `SubmitSwitchModeRpc` → defense-in-depth проверка → посадка
5. Если `false` → отказать, `ShipKeyToast` UI: "Нужен ключ X для корабля Y", fade-out 3 сек

**Деталь:** см. `docs/Ships/Key-subsystem/00_OVERVIEW.md` + `docs/MetaRequirement/00_OVERVIEW.md` (Stage 2 — generic MetaRequirement).

**Что НЕ требует ключа:** пеший режим (WASD), boarding NPC кораблей через [Mira] dialog (если quest дал), boarding "украденного" корабля (TODO).

### X.2 CharacterWindow (P-key, 2026-06-05)

**Новое:** P открывает CharacterWindow — единый "личный кабинет" с 5+ табами (Персонаж / Корабль / Репутация / Контракты / Инвентарь / Квесты). Подробнее — `docs/Character-menu/00_OVERVIEW.md`.

### X.3 NPC DialogWindow (T-Q11b+c, 2026-06-08)

**Новое:** E на NPC `[Mira]` → `DialogWindow` (typewriter, F-skip, click-skip). Диалог → квесты → reputation → credits. Mira E2E полностью пройден (M11, 2026-06-08).

---

### X.4 Real-Time Combat System (T-RTC, 2026-06-25..28, updated 2026-07-14)

**Статус:** ✅ MVP DONE. Реализован полный real-time combat pipeline: DamageCalculator (ERPR), TargetLockService, AOE-формулы, Damage Numbers, Projectile/Throw visuals.

**Архитектура:**
```
Assets/_Project/Scripts/Combat/
├── Core/          — IAcker, IDamageTarget, IDamageSource, IRangePolicy, DamageResult
├── Implementations/ — MeleeRangePolicy, RangedRangePolicy, AoeRangePolicy,
│                      PlayerAttacker, PlayerTarget, NpcAttacker, NpcTarget, WeaponDamageSource
├── Client/        — TargetLockService, TargetHighlightService, DamageNumberService,
│                      DamageNumberInstance, ProjectileVisual, ThrowArcVisual
├── Config/        — CombatConfig, DamageNumberConfig
├── Lookup/        — WeaponClassCatalog, ArmorClassCatalog, WeaponTechniqueCatalog
├── Network/       — CombatServer, DamageResultDto
└── DamageCalculator.cs — статический класс, server-authoritative
```

**DamageCalculator (ERPR формула):**
```
final = max(0, (1dN + base + STR) × locMult × critMult × skillMult) − effectiveDefense
```
- `locMult = 1.0` (отключён в real-time per 2.17)
- `critMult = 2.0` если (1d100 + critMod) ≥ 100, иначе 1.0
- `skillMult = 1.0` (навыки opt-in, после T-CB01..T-CB09)
- `effectiveDefense = armorDefense × typeMultiplier` (Physical/Ballistic=1.0, Antigrav=0.5, Explosive=0.7, Mesium=0.0)
- HitChance через `IRangePolicy.CalculateHitChance()` — melee (высокий) / ranged (средний)

**AOE формулы (AoeRangePolicy):**
- `SphereDamage(Vector3 origin, float radius, float damage)` — сфера
- `BoxDamage(Vector3 center, Vector3 halfExtents, float damage)` — параллелепипед
- `CapsuleDamage(Vector3 start, Vector3 end, float radius, float damage)` — капсула
- `ConeDamage(Vector3 origin, Vector3 direction, float angle, float maxDistance, float damage)` — конус
- `RadialDamage(Vector3 origin, float innerRadius, float outerRadius, float innerDamage, float outerDamage)` — радиальное затухание

**TargetLockService (клиент):**
- `R` — Lock-on: ищет ближайшего IDamageTarget в радиусе, фиксирует
- `Q` / `E` — Cycle targets: перебор целей по расстоянию / углу экрана
- Client-only singleton, создаётся в NetworkManagerController

**TargetHighlightService (клиент):**
- Подсветка цели материалом M_TargetOutline
- Auto-expire через configurable duration

**DamageNumberService (клиент):**
- World Space Damage Numbers (UI World Canvas)
- Цвета по типу урона (Physical=white, Critical=red, Miss=gray)

**Ranged/Throw Visuals:**
- `ProjectileVisual` — для ranged оружия (летит от стрелка к цели)
- `ThrowArcVisual` — для гранат/метательного оружия (баллистическая траектория)

**Key design decisions:**
- `DamageCalculator` — **server-authoritative**, все броски кубов на сервере
- `CombatServer` — обработка атак, применение урона через NetworkVariable
- Не требует GDD_25 — вся реализация документирована в `docs/Character/Skills/real-time-combat/`

### X.5 Input System — Rebinding (Phase 1-2.3, 2026-06-25..26, updated 2026-07-14)

**Статус:** ✅ Phase 2.3 DONE. Полноценная система переназначения клавиш: InputBindingsConfig SO, InputBindingsRuntime с PlayerPrefs persistence, EscMenuWindow, InputRebindingPanel/SkillBindingWindow.

**Архитектура:**

| Компонент | Файл | Назначение |
|-----------|------|------------|
| `InputBindingsConfig` (SO) | `Assets/_Project/Scripts/Input/InputBindingsConfig.cs` | 31+ binding: move/action/combat/UI. Редактируется дизайнером |
| `InputBindingsRuntime` | `Assets/_Project/Scripts/Input/InputBindingsRuntime.cs` | Runtime singleton: rebind + PlayerPrefs persistence (Save/Load/ResetToDefaults) |
| `EscMenuWindow` | `Assets/_Project/Scripts/UI/EscMenu/EscMenuWindow.cs` | Overlay-пауза + AudioSettings/GameplaySettings/GraphicsSettings секции |
| `RebindPromptWindow` | `Assets/_Project/Scripts/UI/Settings/RebindPromptWindow.cs` | Listen → Assign → Save workflow |
| `SkillBindingWindow` | `Assets/_Project/Scripts/UI/Settings/SkillBindingWindow.cs` | Биндинг скиллов (1-9 слоты) |

**Поток:** Escape → EscMenu → Controls → RebindPromptWindow → Listen-нажатие → Assign → Save → Apply

**PlayerPrefs Persistence:**
- `InputBindingsRuntime.Save()` — сериализует текущие бинды в JSON → `PlayerPrefs.SetString(PREFS_KEY)`
- `InputBindingsRuntime.Load()` — читает из PlayerPrefs, восстанавливает
- `InputBindingsRuntime.ResetToDefaults()` — копирует DefaultConfig (загруженный из Resources) в runtime config → Save
- Ключ PlayerPrefs: `"InputBindingsRuntime_Overrides"`

**Key decisions:**
- `InputBindingsConfig` SO как центральный реестр (~31 binding) → редактируется дизайнером
- `InputBindingsRuntime` объединяет rebind + persistence (нет отдельных PlayerPrefsInputRepository/DefaultInputRestorer)
- Phase 1.5: чтение клавиш напрямую через Keyboard.current.* (хардкод в NetworkPlayer.Update)
- Phase 2.x: InputAction events + rebind UI
- Все настройки (audio/graphics/controls/gameplay) — в EscMenuWindow через отдельные Section-компоненты

### X.6 SkillTree (K-key, T-P11, 2026-06-29..07-05)

**Статус:** ✅ DONE. Полное дерево навыков: SkillTreeWindow, SkillsServer, SkillsConfig, 27+ SkillNodeConfig.

**Архитектура:**
```
Assets/_Project/Scripts/Skills/
├── SkillNodeConfig.cs        — SO одного узла (category, discipline, effects, costs, cycle detection)
├── SkillsConfig.cs           — SO со списком всех узлов
├── SkillsWorld.cs            — Server-side skill state + progression
├── SkillsServer.cs           — Network RPC: learn, refund, check
├── SkillsClientState.cs      — Client-side snapshot
├── SkillInputService.cs      — Skill activation (1-9 slots)
├── SkillEffect.cs            — Эффекты навыков
├── UI/SkillTreeWindow.cs     — UXML/USS окно дерева
├── Vfx/                      — VFX сервис (ParticleSystem, пул объектов)
└── Dto/SkillsDto.cs          — Data transfer objects
```

**Key design decisions:**
- `SkillNodeConfig` SO — один узел = один SO, редактируется дизайнером
- Prerequisites — через список ссылок на другие SkillNodeConfig (DFS cycle detection в OnValidate)
- SkillCategory: Social/Combat. CombatDiscipline: Melee/Ranged/Defense/Placed
- Server-authoritative: SkillsServer.Learn()/Refund() через RPC
- Персистентность через SkillsSave

### X.7 Character Customisation (T-CUS, 2026-07-05..14)

**Статус:** ✅ DONE (Phase 1-3). Смена пола/внешности персонажа через CustomisationWindow.

**Компоненты:**
- `CustomisationWindow` (`Scripts/Customisation/UI/CustomisationWindow.cs`) — UI Toolkit overlay
- `CustomisationClientState` — client-only state management
- `CharacterCustomisationApplier` (`Scripts/Player/`) — применяет визуал на модель
- `BodyPresetId`, `HairStyleId`, `CharacterBodyType` — enum-типы
- `CustomisationSave` — persistence (JsonCharacterDataRepository)

**Phases:**
- **Phase 1 (L1):** body type (Male/Female) ✅
- **Phase 2 (L3):** heightScale/widthScale sliders ✅
- **Phase 3 (L4):** [🔴 Запланировано] skin/hair colors + clothing overrides
- **Trigger:** кнопка "ИЗМЕНИТЬ ВНЕШНОСТЬ" в CharacterWindow header → Show()

### X.8 Crafting System (T-CRAFT, 2026-07-10..14)

**Статус:** ⏳ Stage 1 DONE. Базовая система крафта: станции, рецепты, прогресс, UI.

**Компоненты:**
```
Assets/_Project/Scripts/Crafting/
├── CraftingStation.cs        — MonoBehaviour на станции крафта (триггер зона)
├── CraftingStationConfig.cs  — Конфиг станции (какие рецепты доступны)
├── CraftingWorld.cs          — Server-side crafting state
├── CraftingServer.cs         — Network RPC: start/cancel/complete
├── CraftingClientState.cs    — Client-side snapshot
├── CraftingProgressController.cs — Визуализация прогресса
├── CraftingTimeService.cs    — Timer-сервис
├── RecipeData.cs             — SO с рецептом (ингредиенты → результат, время)
├── UI/CraftingWindow.cs      — UI Toolkit окно крафта
└── Dto/                      — Data transfer objects
```

**Key design decisions:**
- Server-authoritative: крафт идёт на сервере, клиент получает прогресс
- Recipe-based: ингредиенты + время = результат
- CraftingStation как триггер-зона в мире

---

## 9. Acceptance Criteria (обновление 2026-07-14)

| # | Критерий | Как проверить | Статус |
|---|----------|--------------|--------|
| 17 | **Бой:** DamageCalculator считает hit/miss/crit/armor | Host → атака → консоль "Damage: X (hit/crit/miss)" | 🟢 DONE (T-RTC) |
| 18 | **Бой:** AOE 5 формул (sphere/box/capsule/cone/radial) | AoeRangePolicy — через код в хост-режиме | 🟢 DONE (T-RTC) |
| 19 | **Target Lock:** R-клавиша lock-on, Q/E cycle | Нажать R → блокировка цели, Q/E → перебор | 🟢 DONE (T-LOCK) |
| 20 | **Target Highlight:** outline на цели | R → подсветка M_TargetOutline, сброс при смене цели | 🟢 DONE (T-HIGHLIGHT) |
| 21 | **Damage Numbers:** World Space урон | Атака → цифры урона над целью | 🟢 DONE (T-DNG-01) |
| 22 | **Input rebinding:** Escape → Controls → Rebind → Save → Load | Esc → Controls → клик биндинга → новая клавиша → Save → Restart | 🟢 DONE (Phase 2.3) |
| 23 | **EscMenu:** Settings (audio/graphics/gameplay), Controls (rebinding), Quit | Открыть EscMenu → 4 секции работают | 🟢 DONE (Phase 1-2) |
| 24 | **SkillTree (K-key):** дерево навыков | K → SkillTreeWindow → узлы, тратят очки, подсветка | 🟢 DONE (T-P11) |
| 25 | **Character Customisation:** P → кнопка "Изменить внешность" | P → кнопка → CustomisationWindow → пол/внешность | 🟢 DONE (T-CUS-06) |
| 26 | **Crafting:** CraftingStation → CraftingWindow → создать предмет | Подойти к станции → окно крафта → рецепт → создать | 🟢 DONE (T-CRAFT) |

**Связанные документы:** [GDD_INDEX.md](GDD_INDEX.md) | [CONTROLS.md](../CONTROLS.md) | [SHIP_SYSTEM_DOCUMENTATION.md](../SHIP_SYSTEM_DOCUMENTATION.md) | [`docs/Ships/Key-subsystem/00_OVERVIEW.md`](../Ships/Key-subsystem/00_OVERVIEW.md) | [`docs/NPC_quests/08_ROADMAP.md`](../NPC_quests/08_ROADMAP.md) | [`docs/Character-menu/00_OVERVIEW.md`](../Character-menu/00_OVERVIEW.md)
