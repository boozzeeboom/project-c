# Ship Key Subsystem — Changelog

Журнал изменений документации подсистемы Key.

---

## 2026-06-18 — R2-SHIP-KEY-003 v2 (decision integration)

**Контекст**: пользователь ответил на 12 вопросов в `24_OPEN_QUESTIONS.md` (2026-06-18). Применены 3 архитектурных изменения.

**Что изменилось**:

| Изменение | Где применено |
|---|---|
| **Q4: NetworkVariable-based telemetry** (было polling RPC) | `22_SHIP_TELEMETRY_PLAN.md` — полностью переписан. `23_ROADMAP.md` T-KEY-07 (effort 2.5h → 3h). |
| **Q11: Explicit `[KeyRodInstanceBinding]`** (было auto-bootstrap через FindNearestShip) | `20_UNIQUE_KEY_INSTANCE.md` §2.4, §3.4, §6. `23_ROADMAP.md` T-KEY-04 (новое название + уточнённый scope). |
| **Q12: Persist через `IPlayerDataRepository`** (было без persist) | `20_UNIQUE_KEY_INSTANCE.md` §2.5 (новая секция). `23_ROADMAP.md` — добавлен T-KEY-PERSIST (~1.5h). |
| **Q8: pilotCount убран из MVP** | `22_SHIP_TELEMETRY_PLAN.md` §5 (убран). `23_ROADMAP.md` §6 (out of scope). |
| **Q6: DisplayName через ShipController._customDisplayName** (было отдельное inspector поле) | `21_SHIP_OWNERSHIP_MODEL.md` §2.2. `22_SHIP_TELEMETRY_PLAN.md` §2.3 (ShipController расширение). |

**Обновлены файлы** (5 патчей):
- `20_UNIQUE_KEY_INSTANCE.md` — добавлены §2.5, §2.6, уточнены §2.4, §3.4, §4 (точки вставки), §5.1, §6 edge-cases
- `21_SHIP_OWNERSHIP_MODEL.md` — displayName через ShipController (Q6)
- `22_SHIP_TELEMETRY_PLAN.md` — полностью переписан под NetworkVariable (Q4)
- `23_ROADMAP.md` — переписан: T-KEY-04, T-KEY-07, новый T-KEY-PERSIST
- `24_OPEN_QUESTIONS.md` — все Q1..Q12 resolved, архив оригиналов

**Что НЕ сделано**: код. Только документация.

**Связь с существующим**: ShipKeyBinding / ShipKeyServer / ShipKeyClientState / ShipKeyToast остаются как `[Obsolete]` legacy aliases (R2-META-REQ-001). MetaRequirement для блоков/дверей продолжает работать.

**Что отложено в фазу 2** (без изменений после decision integration):
- Крафт ключей на верфи
- `isDuplicate` (нелегальные копии)
- `KeyRodAccessLevel` (Limited / OneTime)
- NPC-продажа ключей
- Угон / pirate flow
- Salvage / repair
- Cargo items breakdown в telemetry DTO
- Multi-pilot display

---

## 2026-06-18 — R2-SHIP-KEY-003 v1 (planned, initial design)

**Что добавлено** (6 новых файлов):

| Файл | Что в нём |
|---|---|
| `20_UNIQUE_KEY_INSTANCE.md` | Концепция KeyRodInstance, POCO singleton `KeyRodInstanceWorld`, расширение `InventoryData` для instance-id слоя. |
| `21_SHIP_OWNERSHIP_MODEL.md` | Server-side реестр владельцев, новый компонент `ShipOwnershipRequirement`, расширение `MetaRequirementRegistry`. |
| `22_SHIP_TELEMETRY_PLAN.md` | Подсистема `ShipTelemetry` (v1: polling RPC + ShipTelemetryDto + ShipTelemetryServer/ClientState). |
| `23_ROADMAP.md` | Тикеты T-KEY-01..T-KEY-08. Milestones M1..M5. ~11 часов работы. |
| `24_OPEN_QUESTIONS.md` | 12 вопросов перед стартом T-KEY-01. |
| `99_CHANGELOG.md` | Этот файл. |

**Что НЕ сделано**: код. Дизайн-документы только.

**Связь с существующим**:
- `ShipKeyBinding` / `ShipKeyServer` / `ShipKeyClientState` / `ShipKeyToast` остаются как `[Obsolete]` legacy aliases.
- `MetaRequirement` для блоков/дверей продолжает работать.
- `InventoryWorld` расширяется additive-only.

---

## 2026-06-06 — R2-META-REQ-001 (resolved)

**Что сделано**: миграция с `ShipKeySubsystem` (MVP, 1 корабль ↔ 1 ключ) на обобщённую `MetaRequirement` подсистему.

См. `SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` + `00_OVERVIEW.md §12`.

---

## 2026-06-06 — R2-SHIP-KEY-001 (resolved)

**Что сделано**: первичная реализация физического ключа-предмета для запуска корабля.

См. `KNOWN_ISSUES.md` (баг с `Resources.LoadAll` не рекурсивен → ключи не подбирались).

---

*Changelog ведёт агент Mavis.*