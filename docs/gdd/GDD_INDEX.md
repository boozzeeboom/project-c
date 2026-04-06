# 📋 Game Design Documents (GDD) — Project C: The Clouds

**Последнее обновление:** 6 апреля 2026 г. | **Ветка:** `qwen-gamestudio-agent-dev` | **Версия:** `v0.0.14-gdd`

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
| 00 | [GDD_00_Overview.md](GDD_00_Overview.md) | Обзор игры: концепция, пиллары, целевая аудитория, USP | ✅ Готово |
| 01 | [GDD_01_Core_Gameplay.md](GDD_01_Core_Gameplay.md) | Core Loop, геймплей, управление, режимы, физика | ✅ Готово |
| 02 | [GDD_02_World_Environment.md](GDD_02_World_Environment.md) | Мир: города, пики, фермы, Завеса, погода, цикл дня | ✅ Готово |

## 🔧 Systems — Технические системы

| # | Файл | Описание | Статус |
|---|------|----------|--------|
| 10 | [GDD_10_Ship_System.md](GDD_10_Ship_System.md) | Корабли: классы, физика, управление, кооп-пилотирование | ✅ Готово |
| 11 | [GDD_11_Inventory_Items.md](GDD_11_Inventory_Items.md) | Инвентарь: предметы, сундуки, подбор, круговое колесо | ✅ Готово |
| 12 | [GDD_12_Network_Multiplayer.md](GDD_12_Network_Multiplayer.md) | Сеть: архитектура, синхронизация, реконнект, сервер | ✅ Готово |
| 13 | [GDD_13_UI_UX_System.md](GDD_13_UI_UX_System.md) | UI/UX: HUD, меню, навигация, подсказки, стиль | ✅ Готово |
| 14 | [GDD_14_Visual_Art_Pipeline.md](GDD_14_Visual_Art_Pipeline.md) | Визуал: URP, шейдеры, постобработка, арт-пайплайн | ✅ Готово |
| 15 | [GDD_15_Audio_System.md](GDD_15_Audio_System.md) | Аудио: SFX, музыка, эмбиент, позиционный звук | ✅ Готово |

## 📖 Content — Контентные системы

| # | Файл | Описание | Статус |
|---|------|----------|--------|
| 20 | [GDD_20_Progression_RPG.md](GDD_20_Progression_RPG.md) | Прогрессия: уровни, навыки, характеристики, деревья | ✅ Готово |
| 21 | [GDD_21_Quest_Mission_System.md](GDD_21_Quest_Mission_System.md) | Квесты: типы, генерация, награды, цепочки | ✅ Готово |
| 22 | [GDD_22_Economy_Trading.md](GDD_22_Economy_Trading.md) | Экономика: валюта, цены, торговля, рынок | ✅ Готово |
| 23 | [GDD_23_Faction_Reputation.md](GDD_23_Faction_Reputation.md) | Фракции: гильдии, репутация, ранги, отношения | ✅ Готово |
| 24 | [GDD_24_Narrative_World_Lore.md](GDD_24_Narrative_World_Lore.md) | Нарратив: лор, история, персонажи, сюжет | ✅ Готово |

---

## 🔄 Связь с другой документацией

| GDD | Связанные документы |
|-----|-------------------|
| GDD_00 | README.md, WORLD_LORE_BOOK.md |
| GDD_01 | CONTROLS.md, SHIP_CONTROLLER_PLAN.md |
| GDD_02 | WORLD_LORE_BOOK.md, SHIP_LORE_AND_MECHANICS.md |
| GDD_10 | SHIP_SYSTEM_DOCUMENTATION.md, SHIP_LORE_AND_MECHANICS.md |
| GDD_11 | INVENTORY_SYSTEM.md |
| GDD_12 | NETWORK_ARCHITECTURE.md, NETWORK_PHASE2_PLAN.md |
| GDD_13 | CONTROLS.md |
| GDD_14 | ART_BIBLE.md, unity6/UNITY6_URP_SETUP.md |
| GDD_15 | — (будущий документ) |
| GDD_20-24 | MMO_Development_Plan.md, WORLD_LORE_BOOK.md |

---

## 📝 Процесс разработки GDD

1. **Анализ** — изучение кодовой базы и существующей документации
2. **Структура** — создание скелета документов с заголовками
3. **Наполнение** — поэтапное написание каждой секции
4. **Ревью** — проверка полноты и согласованности
5. **Актуализация** — обновление при изменении систем

---

**Разработано агентами:** @technical-director, @creative-director, @game-designer, @lead-programmer, @art-director, @narrative-director, @unity-specialist, @network-programmer, @gameplay-programmer, @ui-programmer, @audio-director, @world-builder

**Методология:** Collaborative Design Principle — Question → Options → Decision → Draft → Approval
