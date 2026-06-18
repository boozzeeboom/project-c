# Ship Key Subsystem — Открытые вопросы

**Подсистема:** Уникальный ключ корабля + ownership + telemetry
**Дата:** 2026-06-18 (создание) → 2026-06-18 (decision integration)
**Статус:** ✅ Все 12 вопросов решены. Дизайн актуализирован. Можно стартовать T-KEY-01.
**Связанные документы:**
- `23_ROADMAP.md` — план тикетов (v2)
- `20_UNIQUE_KEY_INSTANCE.md` — концепция KeyRodInstance (v2)
- `21_SHIP_OWNERSHIP_MODEL.md` — ownership model (v2)
- `22_SHIP_TELEMETRY_PLAN.md` — telemetry NetworkVariable-based (v2)
- `99_CHANGELOG.md` — история версий

---

## Решения Q1..Q12

Все 12 вопросов решены 2026-06-18. Ниже — итоговая таблица решений с указанием, какие документы обновлены.

| # | Тема | Решение | Применил в файлах |
|---|---|---|---|
| Q1 | ItemType для ключа | ✅ `ItemType.Key = 8` (новый тип) | 20 §2.2 (упоминается) |
| Q2 | instanceId vs keyRodId | ✅ `int instanceId` + опционально string для UI `KR-{counter:D6}` | 20 §2.2 |
| Q3 | OWNER_NONE sentinel | ✅ `const ulong OWNER_NONE = ulong.MaxValue` | 21 §2.2, 23 T-KEY-04 |
| Q4 | Polling vs NetworkVariable | 🔄 **NetworkVariable-based** (пользователь: "будут HUD и UI связанные на актуальных данных") | 22 (полностью переписан), 23 T-KEY-07 |
| Q5 | UI placement | ✅ 5-й tab "Корабли" в CharacterWindow | 23 T-KEY-08 |
| Q6 | DisplayName источник | 🔄 **ShipController._customDisplayName** (пользователь: "минимальный фикс в шипконтроллер, подтягивается к ключу") | 20 §2.2 (displayName в KeyRodInstance), 21 §2.2, 22 §2.3 |
| Q7 | Cargo breakdown | ✅ `(cargoUsed, cargoMax)` для MVP, breakdown в фазе 2 | 22 §6 |
| Q8 | Pilot count | ❌ **Убран** из MVP (пользователь: "не знаю даже для чего оно нужно... если на MVP будет 1 ключ 1 корабль без дубликатов") | 22 §5 (убран), 23 §6 |
| Q9 | TTL для state=Lost | ✅ **Instance живёт вечно** (пользователь: "пока живет вечно. чистка это про другое, не будем лезть") | 21 §4 (без TTL) |
| Q10 | Multi-pilot ownership | ✅ **Только владелец** (пользователь: "другому игроку будут другие функции доступны позже. сюда пока тоже не смотрим") | 21 §2.4 |
| Q11 | Backward compat (bootstrap vs explicit) | 🔄 **Explicit `[KeyRodInstanceBinding]` компонент** (пользователь: "вообще не понял что это и про что... Альтернативный вариант более подходящим кажется") | 20 §2.4, 20 §3.4, 23 T-KEY-04 (переписан) |
| Q12 | Persist между сессиями | 🔄 **ДА, persist** через `IPlayerDataRepository` (пользователь: "альтернатива, должно уже сохраняться, делаем для этого") | 20 §2.5 (новая секция), 23 T-KEY-PERSIST (новый ticket) |

**Итог**: 7 confirmed defaults, 3 contradictions (Q4/Q11/Q12 — основные архитектурные изменения), 1 removal (Q8 — pilotCount убран), 0 осталось открытыми.

---

## Оригинальные ответы пользователя (для истории)

> **Q1**: новый тип
>
> **Q2**: твой вариант по догадке.
>
> **Q3**: делай по догадке предложеной.
>
> **Q4**: нужен NetworkVariable-based - будут HUD и UI связанные на актуальных данных.
>
> **Q5**: есть вкладка в P - character "корабли" будет в ней главный UI по кораблям игрока
>
> **Q6**: думаю нужен минимальный фикс в шипконтроллер с указанием пола Display name оно также подтягиваться к ключу должно связанному можно будет брать оттуда.
>
> **Q7**: для MVP достаточно. на фазе 2 расширим конкретным списком.
>
> **Q8**: Не знаю даже для чего оно нужно и как лучше. если на MVP будет 1 ключ 1 корабль без дубликатов.
>
> **Q9**: пока живет вечно. чистка это про другое, не будем лезть
>
> **Q10**: только владелец. другому игроку будут другие функции доступны позже. сюда пока тоже не смотрим.
>
> **Q11**: вообще не понял что это и про что. что будет в загрузочной бустрап сцене и для чего. Альтернативный вариант более подъодящим кажется. но не пойму точно
>
> **Q12**: альтернатива, должно уже сохраняться, делаем для этого.

---

## Что было в оригинальном Q1..Q12 (для истории)

Секция ниже сохранена как архив — показывает оригинальные вопросы и рекомендации. **Не редактируйте, см. таблицу решений выше для актуальных ответов.**

<details>
<summary>Развернуть оригинальные Q1..Q12</summary>

### Q1. ItemType для ключа

`ItemType` enum (`Assets/_Project/Scripts/Core/ItemType.cs`) сейчас: `Resources=0, Equipment=1, Food=2, Fuel=3, Antigrav=4, Meziy=5, Medical=6, Tech=7`.

Сейчас `KeyRod` использует `ItemType.Equipment`.

**Вопрос**: для уникальных ключей с instance-id нужен отдельный `ItemType.Key = 8`?

**Текущая догадка**: `ItemType.Key = 8`.

**ОТВЕТ**: новый тип

### Q2. instanceId vs keyRodId (string)

**ОТВЕТ**: твой вариант по догадке (int + опционально string).

### Q3. Owner "NONE" sentinel

**ОТВЕТ**: делай по догадке предложеной (OWNER_NONE = ulong.MaxValue).

### Q4. Snapshot vs NetworkVariable для telemetry

**Текущая догадка** (ОТКЛОНЕНА): polling RPC.

**ОТВЕТ** (ПРИНЯТО): NetworkVariable-based для HUD/UI на актуальных данных.

### Q5. UI в CharacterWindow или отдельное окно?

**ОТВЕТ**: 5-й tab "Корабли" в CharacterWindow.

### Q6. ShipDisplayName — откуда брать?

**Текущая догадка** (ОТКЛОНЕНА): inspector поле на ShipController как _displayName.

**ОТВЕТ** (REFINED): минимальный фикс в ShipController — `_customDisplayName` (поле в инспекторе), подтягивается к ключу через lookup в KeyRodInstance.

### Q7. Cargo snapshot — какие данные?

**ОТВЕТ**: для MVP достаточно (used/max). Breakdown в фазе 2.

### Q8. Pilot count в snapshot

**ОТВЕТ** (УБРАНО): не знаю для чего оно нужно. Если MVP 1 ключ 1 корабль — pilotCount не нужен. Убран из дизайна.

### Q9. Транзакционность Transfer

**ОТВЕТ**: пока instance живёт вечно. Чистка — про другое.

### Q10. Multi-pilot и ownership

**ОТВЕТ**: только владелец ключа.

### Q11. Backward compat с существующими тестами

**Текущая догадка** (ОТКЛОНЕНА): bootstrap auto-detect через FindNearestShip.

**ОТВЕТ** (ПРИНЯТО): explicit `[KeyRodInstanceBinding]` компонент (как сейчас `ShipKeyBinding`). Пользователь сказал: "Альтернативный вариант более подходящим кажется".

### Q12. Persist между сессиями

**Текущая догадка** (ОТКЛОНЕНА): НЕ persist.

**ОТВЕТ** (ПРИНЯТО): persist через `IPlayerDataRepository`. Добавлен T-KEY-PERSIST.

</details>

---

## После decision integration

**Все документы обновлены** (2026-06-18, ~30 минут):
- `20_UNIQUE_KEY_INSTANCE.md` — добавлены §2.5 (Persist), §2.6 (out of scope), §5.1 (NetworkVariable-based flow)
- `21_SHIP_OWNERSHIP_MODEL.md` — displayName через ShipController
- `22_SHIP_TELEMETRY_PLAN.md` — полностью переписан на NetworkVariable-based
- `23_ROADMAP.md` — переписан: добавлен T-KEY-PERSIST, T-KEY-04 (explicit binding), T-KEY-07 (NetworkVariable)
- `99_CHANGELOG.md` — запись о decision integration

**Effort total**: ~13 часов на MVP (было ~11, +1.5h T-KEY-PERSIST, +0.5h T-KEY-07 NetworkVariable).

**Готовность к коду**: ✅ можно стартовать T-KEY-01.

---

*Создано: 2026-06-18. Decision integration: 2026-06-18. Агент: Mavis.*