# GDD-01: Core Gameplay & Controls — Project C: The Clouds

**Версия:** 1.1 | **Дата:** 10 июня 2026 г. (дизайн-контент без изменений с 6 апреля 2026 г.; добавлена §X «Реализация в коде») | **Статус:** ✅ Документировано + реализовано (CharacterWindow, NPC dialog, F-key key)
**Автор:** Qwen Code (Game Studio: @game-designer + @gameplay-programmer) — дизайн, Mavis 2026-06-10 — раздел реализации

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
| **Пеший** | Исследование, взаимодействие с миром | Ходьба, бег, прыжки, подбор, сундуки, NPC (future) |
| **Корабль** | Свобода полёта, навигация в 3D | Антигравитация, тяга, тангаж, рыскание, лифт |
| **Свободная камера** | Наблюдение, обход мира (dev) | Полёт в любом направлении, телепорт к пикам |

---

## 3. Detailed Rules

### 3.1 Пеший режим (Walking State)

**Персонаж:** capsule [🔴 Запланировано: Mixamo модель]
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
| **Escape** | Toggle Disconnect UI | toggle | 🟡 Network |

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
| **Escape** | Toggle Disconnect UI | toggle | 🟡 Network |

### Зарезервированные клавиши (future)

| Клавиша | Назначение | Этап |
|---------|-----------|------|
| I | Открыть инвентарь (полный) | Этап 3 |
| C | Открыть чат | Этап 4 |
| M | Открыть карту | Этап 4 |
| J | Журнал квестов | Этап 4 |
| 1-9 | Быстрые слоты | Этап 3 |
| Ctrl | Присесть | Этап 2.5 |
| Right Shift | Прицеливание | Этап 3 |

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

> **Секция добавлена Mavis 2026-06-10.** Дизайн-контент (Core Loop, режимы, управление) остаётся в зоне game-designer'а. Здесь — **только статус реализации** F-boarding с ключом, CharacterWindow, NPC dialog.

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

### X.4 Real-Time Combat System (T-RTC, 2026-06-25..28)

**Новое:** Полноценная real-time боевая система для пешего режима: DamageCalculator с формулами, AOE, raycast-прицеливание.

**Компоненты:**

| Компонент | Файл | Назначение |
|-----------|------|------------|
| `DamageCalculator` | `Scripts/Combat/DamageCalculator.cs` | hit/miss/crit/armor/skills — 5 формул, server-authoritative |
| `AOEHelper` | `Scripts/Combat/AOEHelper.cs` | 5 формул AOE: sphere, box, capsule, cone, radial |
| `CombatTargeting` | `Scripts/Combat/CombatTargeting.cs` | Raycast-прицеливание по R-клавише, подсветка цели |
| `WeaponCatalog` (SO) | `Data/Combat/WeaponCatalog.asset` | SO-каталог оружия (damage, range, attackSpeed) |
| `ArmorCatalog` (SO) | `Data/Combat/ArmorCatalog.asset` | SO-каталог брони (armor, weight, slot) |
| `TechniqueCatalog` (SO) | `Data/Combat/TechniqueCatalog.asset` | SO-каталог техник (skillType, damage, cooldown) |

**DamageCalculator формулы:**
- **Hit:** `(attacker.dex + weapon.accuracy) vs (defender.agi + armor.evasion)` — если roll > threshold, miss
- **Crit:** `(attacker.luck + weapon.critChance) * 0.01` — double damage on roll
- **Base damage:** `weapon.damage + (attacker.str * 0.5)` — flat damage before armor
- **Armor reduction:** `max(1, damage - armor.rating * 0.3)` — flat DR с min 1
- **Skill modifier:** `damage * skillModifier.multiplier` — через SkillModifier chain

**Key design decisions:**
- `DamageCalculator` — **server-authoritative**, damage deal через NetworkRPC
- `AOEHelper` — pure C#, 5 формул, no Unity dependencies, используется и сервером и клиентом
- `CombatTargeting` — рейкаст с камеры, подсветка цели через outline-эффект, R-переключение цели

**Документация:** `docs/Character/Skills/20_IMPLEMENTATION.md` §2.

### X.5 Input System — Rebinding (Phase 1-2.5, 2026-06-25..26)

**Новое:** Полноценная система переназначения клавиш: EscMenu, rebinding UI, save/load/reset.

| Компонент | Файл | Назначение |
|-----------|------|------------|
| `InputBindingsConfig` (SO) | `Data/Input/InputBindingsConfig.asset` | 31 биндинг: move/action/combat/UI |
| `EscMenuWindow` | `Scripts/UI/EscMenuWindow.cs` | Overlay-пауза, кнопки Settings/Controls/Quit |
| `InputRebindingPanel` | `Scripts/UI/InputRebindingPanel.cs` | Listen → Assign → Save/Reset workflow |
| `PlayerPrefsInputRepository` | `Scripts/Player/PlayerPrefsInputRepository.cs` | Сериализация override → PlayerPrefs |
| `DefaultInputRestorer` | `Scripts/Player/DefaultInputRestorer.cs` | Сброс на заводские defaults |

**Поток:** Escape → EscMenu → Settings/Controls → InputRebindingPanel → Listen-нажатие → Assign → Save → Apply

**Key decisions:**
- `InputBindingsConfig` SO как центральный реестр (31 binding) → редактируется дизайнером
- `PlayerPrefsInputRepository` для persistence (JSON string)
- `DefaultInputRestorer` не удаляет SO, а сбрасывает override в PlayerPrefs

---

## 9. Acceptance Criteria (обновление 2026-06-30)

| # | Критерий | Как проверить | Статус |
|---|----------|--------------|--------|
| 17 | **Бой:** DamageCalculator считает hit/miss/crit/armor/skill | Запустить хост, выполнить атаку → консоль "Damage: X (hit/crit/miss)" | 🟢 DONE (T-RTC) |
| 18 | **Бой:** AOEHelper 5 формул (sphere/box/capsule/cone/radial) | Через ExecuteCode: AOEHelper.SphereDamage(origin, radius, dmg) → список целей | 🟢 DONE (T-RTC) |
| 19 | **Прицеливание:** R-клавиша → подсветка цели | Нажать R → outline на враге, повтор R → сброс | 🟢 DONE (T-RTC) |
| 20 | **Input rebinding:** Escape → EscMenu → Controls → Listen → Assign → Save | В Play Mode: Esc → Controls → клик биндинга → новая клавиша → Save → Restart → проверка | 🟢 DONE (Phase 2.5) |
| 21 | **EscMenu:** Settings (заглушка), Controls (rebinding), Quit | Открыть EscMenu → 3 кнопки работают | 🟢 DONE (Phase 1) |

**Связанные документы:** [GDD_INDEX.md](GDD_INDEX.md) | [CONTROLS.md](../CONTROLS.md) | [SHIP_SYSTEM_DOCUMENTATION.md](../SHIP_SYSTEM_DOCUMENTATION.md) | [`docs/Ships/Key-subsystem/00_OVERVIEW.md`](../Ships/Key-subsystem/00_OVERVIEW.md) | [`docs/NPC_quests/08_ROADMAP.md`](../NPC_quests/08_ROADMAP.md) | [`docs/Character-menu/00_OVERVIEW.md`](../Character-menu/00_OVERVIEW.md)
