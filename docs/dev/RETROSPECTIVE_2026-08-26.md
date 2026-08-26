# Ретроспектива: v0.1.20 → v0.1.31

**Период:** 17–26 августа 2026 (7 рабочих дней: 17, 18, 19, 20, 24, 25, 26)
**Генерация:** 26 августа 2026
**Основа:** git, диапазон `3b74ab5a..HEAD` (102 коммита)
**Стартовая версия:** v0.1.20/0.1.21 (Contract core refactor, 17.08)
**Текущая версия:** v0.1.31

---

## Метрики

| Метрика | Значение |
|---|---|
| Коммитов | 102 |
| Файлов изменено | 301 |
| +/− строк | +27 592 / −5 624 |
| Версий выпущено | 0.1.22 → 0.1.31 (6 бампов) |
| Рабочих дней | 7 |
| Средний темп | ~14–15 коммитов/день (пик 27) |
| Фиксов/откатов | ~16 (16% коммитов) |

### Темп по дням

| Дата | Коммиты | Фокус |
|---|---|---|
| 17.08 | 19 | Markets/Contracts аудит (T-MKT01..13), JITTER15/16 |
| 18.08 | 27 | Contract DOM/CON/каталог, distance graph, LocationId |
| 19.08 | 1 | MKT-UI-003 локализация |
| 20.08 | 5 | 0.1.22/0.1.23, faction sorting, docs |
| 24.08 | 16 | Faction identity layer, NPC cargo (0.1.26/0.1.27) |
| 25.08 | 20 | Q001 квест-данные, 0.1.29 UI hints |
| 26.08 | 14 | Quests UI вкладки, HUD слежки (0.1.31) |

---

## Timeline версий

| Версия | Дата | Содержание |
|---|---|---|
| 0.1.20/0.1.21 | 17.08 | (база) Contract core refactor: board/active/terminal разделение, Receipt flow, atomic rollback |
| 0.1.22 | 20.08 | volume right setup cargo and ships |
| 0.1.23 | 20.08 | faction sorting docs and assets |
| 0.1.26 | 24.08 | TradeItem asset reference в NPC Ship Buy Items |
| 0.1.27 | 24.08 | Cargo limits from assigned ship |
| 0.1.29 | 25.08 | T-UI11 Hints toggle for player |
| 0.1.31 | 26.08 | Quests UI: вкладки, HUD слежки, localization Q001 |

> Пропуски в нумерации (0.1.24/25/28/30) — внутренние итерации без публичного бампа; бамп ведётся по значимому релизному срезку.

---

## Направления (по подсистемам)

| Подсистема | Коммиты | Что сделано |
|---|---|---|
| **Markets & Contracts (MKT)** | ~30 | Полный цикл: аудит → integrity → persistence → retention → PlayerPrefs recovery → network feedback → ContractCatalog → distance graph (12 MarketZone) → canonical LocationId → MarketZoneRegistry lifecycle → dead-RPC cleanup → MarketWindow split |
| **Faction (FA)** | ~12 | Data-driven identity layer: stable wire IDs, registry boundary, legacy policy, CSV localization, editor tools, migrated authoring references, combat relations |
| **Quests (QST)** | ~20 | Q001 foundation: NPC definitions, item data, DialogTrees, QuestDefinitions, asset bindings, localization; Q001-09/10 fix + objective add |
| **UI (UI)** | ~15 | UIManager.IsGameplayInputBlocked() (единый гейт), context interaction hints, Q001 dialogue localization, вкладки «Квесты», HUD слежки (все цели отслеживаемого квеста) |
| **Trade (TRADE)** | ~3 | Contract timers settings |
| **JITTER** | ~5 | T-JITTER15/16: откат motion-vector гипотезы, vertex probe, зафиксирован coordinate-dependent humanoid skinning на 56 км |
| **NPC Cargo** | ~3 | Random NPC cargo trade mode, cargo limits from assigned ship, TradeItem asset ref |
| **Docs** | ~15 | Faction/quest/world docs, changelog/roadmap, western comics style migration |

---

## Что получилось хорошо

- **Contract core refactor доведён до конца** — от аудита (T-MKT01) до distance graph и canonical LocationId: 12 MarketZone с полной серверной валидацией доставки, atomic rollback, Receipt flow `Accept → Claim → Transport → Submit`. Это самое защищённое экономическое ядро проекта на данный момент.
- **Faction identity layer** — миграция legacy-ассетов на stable wire IDs без ломки runtime: registry boundary + legacy policy + editor tools. Чистая, послойная миграция.
- **Q001 доведён до полного цикла** — от NPC/item/DialogTree foundation до UI вкладки и HUD слежки с локализацией RU/EN.
- **Единый input-гейт** (`UIManager.IsGameplayInputBlocked()`) — централизовал блокировку атак при открытых окнах (диалог, рынок, инвентарь, крафт, навыки, кастомизация, CommPanel). Убирает дублирование логики.
- **Дисциплина коммитов** — каждый коммит с тикетом (T-*, MKT-*, T-QST*), hash-фиксация итераций, docs в том же коммите. Отслеживаемость высокая.

## Что пошло плохо

- **Много fix/rollback-коммитов (16 из 102, ~16%)** — особенно в UI (T-UI13 fix_1/2/3, T-UI14 fix_1/2, T-QST001-09 fix_2) и локализации (T-UI09 rollback unsafe localization rebuild, T-QST07/08 repair). UI-вкладки и локализация — зона, где первый проход стабильно требует 2–3 итерации.
- **JITTER15 откат** — motion-vector гипотеза не подтвердилась runtime-тестом, пришлось откатить и добавить vertex probe. Координатно-зависимый humanoid skinning на 56 км зафиксирован, но **root cause не устранён** — только диагностирован.
- **Вёрстка UI «по ходу дела»** — T-UI13/14/15/16 (вкладка КВЕСТЫ: перекрой левой части, fix_1/2, fix_3, высота/отступы, checkbox) — итеративный forward-patching вместо rollback-first. По правилу проекта (broken UI → rollback to last working commit FIRST) здесь шло итеративное докручивание.

## Блокираторы

| Блокиратор | Длительность | Разрешение | Профилактика |
|---|---|---|---|
| JITTER coordinate-dependent skinning (56 км) | 2+ дня | Только диагностика (vertex probe), root cause открыт | Изолированный runtime-тест origin vs WorldScene до гипотез |
| Unsafe localization rebuild | 1 день | Rollback (T-UI09) + repair по частям (T-QST07/08/11) | Не реконструировать локализацию целиком; точечные правки через официальный API |

## Технический долг

- TODO/FIXME/HACK в `Assets/_Project/Scripts`: **13** (на базе 0) — рост на +13 за период.
- Основной вклад — диагностические пробы JITTER (SkinnedVertexRuntimeProbe) и временные runtime/editor probes, которые **удалены** в T-JITTER16 (зафиксировано в коммите). Остаток — открытые точки в contract/quest.
- Тренд: **растёт умеренно**, привязан к open-диагностике JITTER и новым quest-слоям.

## Открытые вопросы (carryover)

| Пункт | Статус | Действие |
|---|---|---|
| JITTER root cause (56 км skinning) | Open | Закрыть после runtime playtest vertex probe; отдельная сессия |
| T-JITTER15 runtime playtest | Ожидает проверки | Отмечено в коммите как «нужен runtime playtest» |
| 2 локации без полного MarketZone | Disabled | Держать disabled до полного MarketZone |
| Q001 полные цели в HUD | Сдано (T-UI17) | Playtest: все цели отслеживаемого квеста видны |

## Действия на следующую итерацию

| # | Действие | Приоритет |
|---|---|---|
| 1 | Закрыть JITTER root cause (56 км skinning) — runtime-тест origin vs WorldScene, затем фикс | High |
| 2 | Ввести rollback-first для UI-вкладок: при сломанной вёрстке — откат к рабочему коммиту до forward-patch | High |
| 3 | Локализация Q001: зафиксировать canonical-процесс (официальный API, без full-rebuild) как стандарт | Med |
| 4 | Довести до полного MarketZone 2 оставшиеся disabled-локации | Low |

## Итог

Сильная неделя по объёму (102 коммита, 301 файл, 6 версий) и по защищённости: contract-ядро и faction-идентичность получили production-grade устойчивость, Q001 доведён до полного цикла с UI и локализацией. Единственный системный риск — **UI-вёрстка и локализация стабильно требуют 2–3 итерации** (16% коммитов — fix/rollback), а JITTER root cause пока только диагностирован. Главное на следующий шаг: закрыть JITTER root cause и перевести UI-правки на rollback-first, чтобы убрать итеративный forward-patching.
