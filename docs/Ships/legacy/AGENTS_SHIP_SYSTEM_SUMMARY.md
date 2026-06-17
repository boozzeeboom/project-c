# AGENTS Summary: Ship Movement Overhaul — Project C

**Дата:** Апрель 2026 | **Инициатива:** «Живые Баржи» | **Статус:** Готов к реализации

---

## Executive Summary

Текущее управление кораблём **работает технически** (посадка, тяга, повороты, лифт), но **не передаёт ощущения полёта**. Корабли ощущаются как «баржи» в худшем смысле — механически, без жизни, без связи с миром.

**Цель:** Превратить управление в **плавное, текучее, атмосферное** перемещение воздушных барж над облаками, где игрок чувствует ветер, инерцию, стабилизацию и ограничения высоты.

---

## Проблемы Текущей Реализации

| # | Проблема | Влияние на Игрока |
|---|----------|-------------------|
| 1 | Резкий yaw (A/D) | Ощущение «аркады», а не баржи |
| 2 | Резкий pitch (мышь Y) | Нет чувства массы корабля |
| 3 | Резкий lift (Q/E) | Как телепортация, не как лифт |
| 4 | Нет стабилизации | Корабль «зависает» в наклоне |
| 5 | Нет коридоров высот | Игрок не чувствует границы мира |
| 6 | Нет ветра | Мир «мёртвый», нет связи с окружением |
| 7 | Нет модульности | Все корабли одинаковые |

---

## Решение: 3-Уровневая Система

```
┌────────────────────────────────────────────────────────────┐
│  Уровень 1: Core Movement (Текущий спринт — P0)           │
│  ─────────────────────────────────────────                │
│  • Smooth yaw/pitch/lift/thrust с Lerp                    │
│  • Auto-stabilization при отсутствии ввода                │
│  • Angular drag ×3-5 для гашения вращения                 │
│  • Ограничение pitch ±20°, roll = 0                       │
│  • Ощущение: «баржа плывёт»                               │
├────────────────────────────────────────────────────────────┤
│  Уровень 2: Environment (Сессия 2-3 — P1)                │
│  ─────────────────────────────────                        │
│  • Altitude corridors: 1200м-4450м глобально             │
│  • Городские коридоры: Примум 4100-4450м, Секунд 1000-1250м│
│  • Server validation высоты + предупреждения              │
│  • Wind zones: ветер между пиками                         │
│  • Turbulence: тряска при приближении к Завесе            │
│  • Ощущение: «мир живой, ветер давит»                     │
├────────────────────────────────────────────────────────────┤
│  Уровень 3: Modules & Advanced (Сессия 4-6 — P1-P2)      │
│  ─────────────────────────────────────────                │
│  • Module system: ShipModule ScriptableObject            │
│  • MODULE_YAW_ENH, PITCH_ENH, LIFT_ENH (тир 1)           │
│  • MODULE_MEZIY_THRUST (burst maneuvers, тир 2)          │
│  • MODULE_VEIL, SPACE, STEALTH (тир 3-4)                 │
│  • MODULE_AUTO_DOCK (автопилот стыковки)                 │
│  • KeyRod система + Co-Op adaptive input                 │
│  • DockingDispatcher (Elite Dangerous style)             │
│  • Ощущение: «мой корабль, мой стиль»                     │
└────────────────────────────────────────────────────────────┘
```

---

## Роли Агентов и Задачи

### @technical-director (Стратегия)
- ✅ Определил философию: «баржи, не истребители»
- ✅ Утвердил коридоры высот и города
- ✅ Определил приоритеты: Core → Environment → Modules
- ⏳ Ревью после Сессии 1: проверить «feel» корабля

### @game-designer (Дизайн и Баланс)
- ✅ Создал GDD_10_Ship_System v4.0
- ✅ Создал ShipRegistry.md (10 кораблей, 12 модулей)
- ✅ Определил параметры для каждого класса
- ⏳ Балансировка yaw/pitch/lift значений после тестов
- ⏳ Дизайн диалогов диспетчера (Elite Dangerous style)
- ⏳ Система SOL зон и штрафов

### @lead-programmer (Архитектура)
- ⏳ Спроектировать ShipModule/ShipDefinition архитектуру
- ⏳ Рефакторинг ShipController.cs → v2
- ⏳ Интеграция altitude corridor валидации в сервер
- ⏳ Co-Op input averaging с ролями (капитан ×1.5)
- ⏳ KeyRod система (ScriptableObject + валидация)

### @engine-programmer (Движок/Физика)
- ⏳ Переписать FixedUpdate в ShipController
- ⏳ Lerp-система для yaw/pitch/lift/thrust
- ⏳ Auto-stabilization логика
- ⏳ WindZone.cs — объёмные зоны ветра
- ⏳ Turbulence system (Random.force + Cinemachine Impulse)
- ⏳ AltitudeCorridorSystem.cs

### @gameplay-programmer (Геймплей/Механики)
- ⏳ Smooth yaw: убрать резкость, настроить ×0.3-0.4
- ⏳ Smooth pitch: ±20° лимит, ×0.4-0.5
- ⏳ Smooth lift: 1.5-2.5 м/с макс
- ⏳ Meziy Thrust модули (burst maneuvers)
- ⏳ Module effects runtime (применение эффектов к статам)
- ⏳ Adaptive multi-pilot input system
- ⏳ DockingDispatcher.cs (логика диспетчера)

### @unity-specialist (Unity/Тесты)
- ⏳ Создать ShipMovementTests.cs (7+ тестов)
- ⏳ Debug HUD для отладки (F3)
- ⏳ Настроить Cinemachine Impulse для турбулентности
- ⏳ Test scene с кораблём, пиками, зоной Завесы
- ⏳ Настроить Post-processing для Veil turbulence
- ⏳ Profile производительности (FixedUpdate 50Hz)

### @devops-engineer (Сервер/Сеть)
- ⏳ Server altitude validation (каждые 0.5с)
- ⏳ Server RPC для SubmitShipInput (обновить сигнатуру)
- ⏳ Server-side corridor checking
- ⏳ SOL violation detection + notification
- ⏳ Dedicated server поддержка коридоров
- ⏳ Network sync для module effects

---

## Сессии: Краткий План

| Сессия | Фокус | Длительность | Результат |
|--------|-------|-------------|-----------|
| **1** | Core Smooth Movement | 1-2 итерации | ShipController v2, плавное управление |
| **2** | Altitude Corridors | 1 итерация | Коридоры, серверная валидация, warnings |
| **3** | Wind & Turbulence | 1 итерация | Wind zones, тряска у Завесы |
| **4** | Module System | 1-2 итерации | ShipModule SO, тир 1 модули |
| **5** | Meziy Thrust | 1 итерация | Burst maneuvers, визуал сопла |
| **6** | Co-Op + KeyRod | 1-2 итерации | Адаптивный Co-Op, ключ-стержни |
| **7** | Docking | 1-2 итерации | Диспетчер, CommPanel, автопилот |

---

## Созданные Документы

| Документ | Путь | Статус |
|----------|------|--------|
| **GDD_10: Ship System v4.0** | `docs/gdd/GDD_10_Ship_System.md` | ✅ Создан |
| **Ship Registry** | `docs/ShipRegistry.md` | ✅ Создан |
| **Implementation Plan** | `docs/SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md` | ✅ Создан |
| **GDD_02: World** | `docs/gdd/GDD_02_World_Environment.md` | ⏳ Нужно обновить |
| **MMO_Development_Plan** | `docs/MMO_Development_Plan.md` | ⏳ Нужно обновить |

---

## Риски и Меры

| Риск | Вероятность | Влияние | Мера |
|------|------------|---------|------|
| Слишком плавное = скучное | Средняя | Высокое | Настраивать на фидбеке; модули burst добавляют адреналин |
| Сервер не успевает 50Hz | Низкая | Среднее | Валидация высоты каждые 0.5с, не каждый tick |
| Co-Op конфликты ввода | Средняя | Среднее | Weighted averaging + капитан приоритет |
| Сложность модулей | Средняя | Низкое | Поэтапное введение: тир 1 → 2 → 3 → 4 |
| Turbulence раздражает | Средняя | Среднее | Настраиваемая интенсивность; только у границ |

---

## Следующий Шаг (Немедленно)

**Сессия 1: Core Smooth Movement** — переписать ShipController.cs

Конкретные шаги:
1. Открыть `SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md` → Секция 3 (код)
2. Переписать `ShipController.cs` с новыми smooth-переменными
3. Создать `ShipMovementTests.cs` → запустить все 7 тестов
4. Протестировать в Unity: «ощущается ли как баржа?»
5. Настроить параметры (yawSmoothTime, pitchSmoothTime и т.д.)
6. Закоммитить → показать пользователю

---

*Документ оркестрации создан: Апрель 2026*
*Агенты: @technical-director, @game-designer, @lead-programmer, @engine-programmer, @gameplay-programmer, @unity-specialist, @devops-engineer*
