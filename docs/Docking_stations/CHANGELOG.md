# CHANGELOG — Docking Stations

> Лог изменений каталога `docs/Docking_stations/`. Для каждой записи —
> дата, сессия, какие Q закрыты, какие файлы затронуты.

---

## 2026-06-19 — Решения приняты, документация актуализирована

**Сессия:** «проанализируй, я записал ответы и актуализируй документацию»
**Профиль:** project-c
**Статус:** ✅ Все 15 Q + 4 Phase-2 вопроса закрыты

### Сводка решений

| Q | Тема | Решение | Влияние |
|---|------|---------|---------|
| Q1 | Клавиша CommPanel | **T** (временное, инвентарь Этап 3 переедет) | `00`, `02 §10`, `05 §1.4` |
| Q2 | Фразы диспетчера | Статичный набор + шаблоны `{0}` | `02 §2.3`, `04 §2.2` |
| Q3 | Persistence pads | **Сервер — SOT** (single source of truth), NPC-корабли в архитектуре учтены (Phase 2) | `02 §6 DockingWorld` |
| Q4 | Кол-во pads | **Без хардкода**, soft-limit ≤10 на класс | `02 §2.2`, `03 §6`, `06 T-DOCK-11` |
| Q5 | Радиус OuterCommZone | Настраивается в Inspector | `02 §2.1`, `03 §3.3` |
| Q6 | Координаты Primium | **(40500, 2510, 40500)** | `03 §8.1`, `06 T-DOCK-12` |
| Q7 | Связь игрок↔диспетчер | **Двусторонняя обязательна** (простая MVP) | `02 §5.5`, `04 §1-§5` |
| Q8 | Вылет | F = boarding без блокировки; **T → «Запросить вылет»** = **отдельная подсистема Departure** | `05 §2.6`, `06 T-DEPART-*`, новый `08_DEPARTURE_SUBSYSTEM.md` |
| Q9 | F внутри CommPanel | Стандартное поведение + CommPanel закрывается | `05 §1.3`, `05 §4.2` |
| Q10 | T вне кресла | **Silently ignore** | `05 §1.4`, `06 T-DOCK-08` |
| Q11 | KeyRod в Docked | **НЕ обрабатываем** | `02 §10.4`, `06 T-DOCK-09` |
| Q12 | Звук диспетчера | Только текст | (нет кода) |
| Q13 | Floating labels | **Цифры на mesh'е** | `03 §8.3`, `06 T-DOCK-12` |
| Q14 | Live update pads | Только при Assigned | (нет кода) |
| Q15 | FSM корабля | Bool-флаги | `02 §10`, `06 T-DOCK-09` |
| F1 | Автопилот | Включается модулем заранее (Phase 2) | (нет кода MVP) |
| F2 | NPC-корабли | Phase 2 | `02 §6` (пометки) |
| F3 | Docking fee | Нет | (нет кода) |
| F4 | Reputation | Нет | (нет кода) |

### Изменения по файлам

#### Создан новый файл
- `08_DEPARTURE_SUBSYSTEM.md` (9 KB) — отдельная подсистема для Q8 (Phase 1.5).
  6 тикетов T-DEPART-00..05, ~15 часов кодинга.

#### Переименован
- `08_REFERENCES.md` → `09_REFERENCES.md` (новый `08` занят Departure).

#### Существенные изменения (по файлам)

| Файл | Изменения |
|------|-----------|
| `00_README.md` | TL;DR таблица финальных решений; навигация обновлена (8 → 11 файлов); +CHANGELOG.md |
| `01_CURRENT_STATE_AUDIT.md` | Без изменений (аудит был финальным) |
| `02_V2_ARCHITECTURE.md` | §2.2 DockPadLayout: добавлен Q4 soft-limit warning, пустой = для всех; §5.5 NEW `RequestConfirmAssignmentRpc` (Q7); §6 DockingWorld: добавлены `RegisterPendingAssignment`, `ConfirmAssignment`, `CancelPendingAssignment`, `IsPending`, `GetPadStatusSnapshot` (Q3+Q7); §7 DockingClientState: добавлен `OnAwaitingConfirmation`, `PendingAssignment`, `IsLocalPlayerPilotingShip` (Q7+Q10); §10.4 NEW Q11 (KeyRod не обрабатываем); §15 NEW раздел D8 (Q3 SOT для NPC) |
| `03_ZONES_AND_TRIGGERS.md` | §3.3 Q5: commRange настраивается в Inspector; §6.2 NEW «пустой = для всех» (Q4); §6.3 NEW Q4 без хардкода; §8.1 Q6 координаты; §8.3 NEW Q13 цифры на mesh'е |
| `04_DIALOG_AND_DISPATCHER_UI.md` | §1 NEW 1.2 (AwaitingConfirmation, Q7); §3.1 NEW состояние AwaitingConfirmation; §5.2 NEW поле `_awaitingConfirmation`; §5.2 NEW подписки `OnAwaitingConfirmation` + `OnTouchedDown`; §5.3 NEW handler `HandleAwaitingConfirmation` + `HandleAssignmentFailed`; §5.4 NEW `UpdateUI` AwaitingConfirmation ветка; §5.5 NEW `ConfirmAssignment` метод; §5.6 NEW `RequestDocking` Q10 IsLocalPlayerPilotingShip check |
| `05_FLOW_AND_INTERACTION.md` | §1 NEW Q1+Q9+Q10; §2.1 NEW Q7 двусторонняя в sequence diagram; §2.5 NEW Q9 F = boarding всегда; §2.6 NEW Q8 вылет без запроса; §3.2 Q7 edge-cases (двусторонняя); §3.3 Q11 KeyRod не обрабатываем; §3.4 Q7 disconnect pending; §3.5 Q9+Q10 UI edge-cases; §4 NEW Q9 F-key pipeline (F не меняется) |
| `06_ROADMAP.md` | §1 NEW M-DEPART-1/M-DEPART-2 milestones; §1 NEW Q-accepted list; T-DOCK-01 (+Q7); T-DOCK-08 (+Q10); T-DOCK-09 (+Q15, Q11); T-DOCK-11 (Q4: пустой layout); T-DOCK-12 (Q4+Q6+Q13); §4 NEW таблица Departure; §5 NEW 3 сессии для Departure |
| `07_OPEN_QUESTIONS.md` | **Переписан** — теперь «Финальные решения + архив Q&A» |
| `08_DEPARTURE_SUBSYSTEM.md` | NEW (см. выше) |
| `09_REFERENCES.md` | Переименован, обновлена шапка и чеклист |

### Критичные архитектурные сдвиги

1. **Q7 — двусторонняя связь обязательна.** Добавлен `RequestConfirmAssignmentRpc`,
   `PendingAssignment` state, `AwaitingConfirmation` UI state. Сервер не
   бронирует pad сразу — ждёт подтверждения игрока 30 сек.

2. **Q8 — Departure = отдельная подсистема.** `08_DEPARTURE_SUBSYSTEM.md` создан.
   F НЕ блокируется и НЕ toast-предупреждается в MVP docking. Вылет через
   T → «Запросить вылет» → ожидание → разрешение (Phase 1.5).

3. **Q4 — без хардкода.** `DockPadLayout` SO — пустой по умолчанию, дизайнер
   наполняет. Soft-limit ≤10 на класс в `OnValidate` (warning, не блок).

4. **Q3 — SOT на сервере.** `DockingWorld` — single source of truth занятости
   pads. Клиент не хранит представление, получает push по RPC. Архитектура
   совместима с NPC-кораблями (Phase 2).

### Что осталось из scope-original

Все **15 вопросов** и **4 Phase-2** закрыты. Никаких open questions.

### Что НЕ делалось

- ❌ Никакого кода (только документация).
- ❌ Никакого git commit (за тобой).
- ❌ Никаких MCP-команд.
- ❌ Никаких изменений в `docs/gdd/` или `WORLD_LORE_BOOK.md`.

---

## 2026-06-19 — Initial design (предыдущая сессия)

**Сессия:** «нее кодим. сессия про документацию и первичный анализ»
**Профиль:** project-c
**Статус:** ✅ 9 файлов, 5013 строк, 250 KB созданы

См. `git log` для точной истории.
