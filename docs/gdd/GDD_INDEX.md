# 📋 Game Design Documents (GDD) — Project C: The Clouds

**Последнее обновление:** 14 июля 2026 г. | **Версия:** `v1.0`

> **Что нового (14 июля 2026):**
> - **Актуализация всех GDD под код** — все 19 документов переписаны в соответствии с фактической реализацией
> - **Автор:** Малков Леонид Андреевич (замена Qwen Code / Game Design AI во всех документах)
> - **GDD_25_Trade_Routes.md** — добавлен в индекс (ранее отсутствовал)
> - **Убраны ссылки на WORLD_LORE_BOOK.md** — файл утрачен, лор доступен через RAG-базу
> - **Статусы приведены к фактическим**: реализовано / частично / запланировано
>
> Полный список изменений — в каждом GDD индивидуально.

---

## 📐 Структура GDD

Документы разделены на 3 уровня:

| Уровень | Описание |
|---------|----------|
| **Core** | Фундаментальные системы — геймплей, управление, мир |
| **Systems** | Подсистемы — инвентарь, корабли, сеть, UI, визуал |
| **Content** | Контентные системы — RPG, квесты, фракции, экономика, нарратив |

---

## 🎯 Core — Фундаментальные документы

| # | Файл | Описание | Статус |
|---|------|----------|--------|
| 00 | [GDD_00_Overview.md](GDD_00_Overview.md) | Обзор игры: концепция, пиллары, целевая аудитория, USP | ✅ Актуально |
| 01 | [GDD_01_Core_Gameplay.md](GDD_01_Core_Gameplay.md) | Core Loop, геймплей, управление, режимы, физика | ✅ Актуально |
| 02 | [GDD_02_World_Environment.md](GDD_02_World_Environment.md) | Мир: города, пики, фермы, Завеса, погода, цикл дня | ✅ Актуально |

## 🔧 Systems — Технические системы

| # | Файл | Описание | Статус |
|---|------|----------|--------|
| 10 | [GDD_10_Ship_System.md](GDD_10_Ship_System.md) | Корабли: классы, физика, управление, AltitudeCorridor, модули | ✅ Актуально |
| 11 | [GDD_11_Inventory_Items.md](GDD_11_Inventory_Items.md) | Инвентарь v2: ItemRegistry, NetworkList, типы предметов | ✅ Актуально |
| 12 | [GDD_12_Network_Multiplayer.md](GDD_12_Network_Multiplayer.md) | Сеть: NGO 2.x, Host/Client, scene-placed spawn | ✅ Актуально |
| 12.1 | [GDD_12_1_Scene_World_Streaming.md](GDD_12_1_Scene_World_Streaming.md) | Мир: 24 сцены, 4×6 grid, boundary-based loading | ⚠️ Код готов, не deployed |
| 13 | [GDD_13_UI_UX_System.md](GDD_13_UI_UX_System.md) | UI/UX: HUD, CharacterWindow, DialogWindow, EscMenu, UI Toolkit | ✅ Актуально |
| 14 | [GDD_14_Visual_Art_Pipeline.md](GDD_14_Visual_Art_Pipeline.md) | Визуал: URP 17.0.3, шейдеры, постобработка, Day/Night Volume | ✅ Актуально |
| 15 | [GDD_15_Audio_System.md](GDD_15_Audio_System.md) | Аудио: SFX, музыка, эмбиент, позиционный звук | 🔴 Запланировано |
| 25 | [GDD_25_Combat_Skills.md](GDD_25_Combat_Skills.md) | Бой: ERPR damage, AOE, Ranged/Throwables, TargetLock, SkillTree | ✅ Реализовано |
| 25.1 | [GDD_25_Trade_Routes.md](GDD_25_Trade_Routes.md) | 🆕 Торговля: маршруты, контракты, контрабанда, логистика | 🟡 Частично |
| 26 | [GDD_26_Character_Customisation.md](GDD_26_Character_Customisation.md) | Кастомизация: пол/пресет/цвета/волосы + Equipment Visual | ✅ Реализовано |

## 📖 Content — Контентные системы

| # | Файл | Описание | Статус |
|---|------|----------|--------|
| 20 | [GDD_20_Progression_RPG.md](GDD_20_Progression_RPG.md) | Прогрессия: уровни, навыки, SkillTree, характеристики | ✅ Актуально |
| 21 | [GDD_21_Quest_Mission_System.md](GDD_21_Quest_Mission_System.md) | Квесты: NPC диалоги, цепочки, триггеры, награды | ✅ Реализовано (M1–M19) |
| 22 | [GDD_22_Economy_Trading.md](GDD_22_Economy_Trading.md) | Экономика: Trade v2, валюта, цены, контракты | ✅ Актуально |
| 23 | [GDD_23_Faction_Reputation.md](GDD_23_Faction_Reputation.md) | Фракции: гильдии, репутация, ранги, отношения | 🟡 Частично (Stage 1) |
| 24 | [GDD_24_Narrative_World_Lore.md](GDD_24_Narrative_World_Lore.md) | Нарратив: лор, история, персонажи, сюжет | 🟡 Частично (NPC диалоги) |

### 🆕 v0.0.20 — Ресурсная система (Mining)

| # | Файл | Описание | Статус |
|---|------|----------|--------|
| — | [`docs/Mining/00_OVERVIEW.md`](../Mining/00_OVERVIEW.md) | Сбор ресурсов — архитектура, REUSE, альтернативы, дизайн | ✅ DONE |
| — | [`docs/Mining/10_DESIGN.md`](../Mining/10_DESIGN.md) | Классы, sequence-диаграммы, edge-cases, трафик | ✅ DONE |
| — | [`docs/Mining/ROADMAP.md`](../Mining/ROADMAP.md) | T-G01–T-G07: 7 тикетов, 3 milestones, риски, сессионные логи | ✅ DONE |

Сбор ресурсов (Mining) — MVP v0.0.2. Подойти к 3D-объекту → F → сбор N сек с ProgressBar → предмет в инвентарь. Tool check через MetaRequirement (Кирка). Возобновляемые узлы (maxHarvests → cooldown → Idle). Анимация scale-pulse ±15% + emissive flash на узле, scale-pulse на персонаже. **Документация:** `docs/Mining/` (5 файлов). **Статус:** ✅ 7/7 тикетов (2026-06-10).

---

## 🔄 Связь с другой документацией

| GDD | Связанные документы |
|-----|-------------------|
| GDD_00 | README.md |
| GDD_01 | CONTROLS.md, docs/Ships/Key-subsystem/ |
| GDD_02 | docs/world/, docs/DayNight/ |
| GDD_10 | docs/Ships/, SHIP_LORE_AND_MECHANICS.md |
| GDD_11 | docs/Character-menu/sub_inventory-tab/, docs/MetaRequirement/ |
| GDD_12 | NETWORK_ARCHITECTURE.md, docs/dev/INTEGRATION_SHIPS_TO_WORLD_0_0.md |
| GDD_13 | docs/Character-menu/, docs/UI/ |
| GDD_14 | ART_BIBLE.md, docs/unity6/UNITY6_URP_SETUP.md |
| GDD_15 | — (будущий документ) |
| GDD_20 | docs/Character/Skills/, docs/Stats/ |
| GDD_21 | docs/NPC_quests/08_ROADMAP.md |
| GDD_22 | docs/Markets/, GDD_25_Trade_Routes.md |
| GDD_23 | docs/NPC_quests/02_V2_ARCHITECTURE.md |
| GDD_24 | RAG-база книги (PostgreSQL: 192.168.31.227:5432/agency_contacts, таблица book_chunks) |
| GDD_25 | docs/Character/Skills/20_IMPLEMENTATION.md |
| GDD_25.1 | docs/Markets/TRADE_V2_DESIGN.md, GDD_22_Economy_Trading.md |
| GDD_26 | docs/Character/Customisation/ |

**Лор-книга:** WORLD_LORE_BOOK.md утрачен. Для сверки с книгой «Интеграл Пьявица» используйте RAG-базу:
- `psycopg2.connect(host='192.168.31.227', port=5432, user='leon', password='m2za7m7w', dbname='agency_contacts')`
- Таблица `book_chunks` (id, chapter, chunk_index, text) — 1465 чанков, 15 глав

---

## 📝 Процесс разработки GDD

1. **Анализ** — изучение кодовой базы и существующей документации
2. **Структура** — создание скелета документов с заголовками
3. **Наполнение** — поэтапное написание каждой секции
4. **Ревью** — проверка полноты и согласованности
5. **Актуализация** — обновление при изменении систем

---

**Разработано:** Малков Леонид Андреевич

**Методология:** Всё под код. Если реализовано иначе — GDD правится под код. Если не реализовано — остаётся как план.
