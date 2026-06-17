# Ships — Project C: The Clouds

**Последнее обновление:** 12 апреля 2026 | **ShipController:** v2.7

Документация по кораблям, модулям и системе управления полётом.

---

## 📚 Текущие документы

| Документ | Описание |
|----------|----------|
| [SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md](SHIP_MOVEMENT_IMPLEMENTATION_PLAN.md) | Главный план — все сессии, код ShipController v2, критерии |
| [NEXT_SESSION_CONTEXT.md](NEXT_SESSION_CONTEXT.md) | Точка входа для новой сессии |
| [NEXT_SESSION_PROMPT_SESSION5_4.md](NEXT_SESSION_PROMPT_SESSION5_4.md) | Промпт сессии 5_4 (UI, Thrust, Polish) |
| [SESSION_5_4_COMPLETE.md](SESSION_5_4_COMPLETE.md) | ✅ Итоги сессии 5_4 |
| [ShipRegistry.md](ShipRegistry.md) | Реестр: 6 классов, 10 кораблей, 12+ модулей |
| [SHIP_CLASS_PRESETS.md](SHIP_CLASS_PRESETS.md) | Пресеты 4 классов кораблей (Light/Medium/Heavy/HeavyII) |
| [HOWTO_CREATE_SHIP.md](HOWTO_CREATE_SHIP.md) | Гайд создания тестового корабля в Unity |
| [AGENTS_SHIP_SYSTEM_SUMMARY.md](AGENTS_SHIP_SYSTEM_SUMMARY.md) | Executive summary оркестрации агентов |

---

## ✅ Реализованные сессии

| # | Сессия | Тема | Коммиты | Статус |
|---|--------|------|---------|--------|
| 1 | Core Smooth Movement | SmoothDamp, инерция, стабилизация | Откачено asmdef | ✅ |
| 2 | Altitude Corridor System | Коридоры высот, турбулентность | — | ✅ |
| 3 | Wind & Environmental Forces | WindZone, WindZoneData | — | ✅ |
| 4 | Module System Foundation | ShipModule, ModuleSlot, ShipModuleManager | — | ✅ |
| 5 | Fuel + Meziy Modules | Fuel system, MODULE_ROLL, частицы | Несколько | ✅ |
| 5.2 | Continuous Mode Rewrite | Переработка между, Debug HUD | Несколько | ✅ |
| 5.3 | Passive/Active/Overheat | Новая архитектура: C/V, Z/X, Shift+A/D | `d24b9c1`, `9db7f09` | ✅ |
| 5.4 | UI, Thrust, Polish | MeziyStatusHUD, THRUST модуль, cooldown 15s | `fdc76b4`, `845ec5e` | ✅ |

---

## 📁 Архив сессий

Закрытые документы перемещены в [`docs/Old_sessions/`](../Old_sessions/):
- SESSION_1/2/3/4/5_COMPLETE.md, SESSION_5_2_COMPLETE.md
- SESSIONS_1_TO_5_3_RETROSPECTIVE.md
- NEXT_SESSION_PROMPT_SESSION2-5_3.md, SESSION_3_PROMPT.md

---

## 🐛 Баг-репорты (актуальные)

| Документ | Статус |
|----------|--------|
| [SESSION5_4_BUGS_AND_RISKS.md](../bugs/SESSION5_4_BUGS_AND_RISKS.md) | 🟡 3 риска (Shift+W конфликт, fuel расход, HUD null) |
| [SESSION5_4_MODULESLOT_VALIDATION_STATUS.md](../bugs/SESSION5_4_MODULESLOT_VALIDATION_STATUS.md) | ✅ Закрыто |

Закрытые баг-репорты → [`docs/Old_sessions/`](../Old_sessions/)

---

## 🎮 Управление кораблём

| Клавиша | Действие |
|---------|----------|
| W/S | Тяга вперёд/назад |
| A/D | Рыскание |
| Q/E | Лифт |
| Мышь Y | Тангаж |
| Left Shift | Ускорение |
| Z/X | Крен (требует MODULE_ROLL) |
| C/V | Мезиевый тангаж |
| Shift+A/D | Мезиевое рыскание |
| Shift+W/S | Мезиевый рывок/торможение |
| L | Дозаправка (stationary) |
| F3 | Debug HUD |
| F4 | Meziy Status HUD |

---

## 🔗 Связанные документы (вне папки)

| Документ | Путь |
|----------|------|
| GDD_10: Ship System | [`../gdd/GDD_10_Ship_System.md`](../gdd/GDD_10_Ship_System.md) |
| GDD_02: World | [`../gdd/GDD_02_World_Environment.md`](../gdd/GDD_02_World_Environment.md) |
| MMO Development Plan | [`../MMO_Development_Plan.md`](../MMO_Development_Plan.md) |
| WORLD_LORE_BOOK | [`../WORLD_LORE_BOOK.md`](../WORLD_LORE_BOOK.md) |
