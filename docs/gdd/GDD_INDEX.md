# 📋 Game Design Documents (GDD) — Project C: The Clouds

**Последнее обновление:** 30 июня 2026 г. | **Версия:** `v0.0.35`

> **Что нового (25–30 июня 2026):**
> - **Real-Time Combat + Skills MVP ✅** — DamageCalculator (hit/miss/crit/armor/skills), AOEHelper (5 формул), CombatTargeting (raycast), SkillTreeWindow (интерактивный граф), SkillManager, SkillAnimationPlayer (runtime AOC), 27+ SkillNodeConfig SO, 3 каталога (Weapon/Armor/Technique), SkillModifier → DamageCalculator интеграция
> - **NPC Enemy System P0-P2 ✅** — NpcBrain FSM (Idle→Chase→Attack→Dead), NpcSpawner (surface validation/rate-limit/leash 30m), goblin prefab (NetworkObject, NavMeshAgent), 5-state AnimatorController, loot tables с pickup
> - **Character Customisation L1+L3+L4 ✅** — Male/Female переключение, 6 пресетов тела, 2 стиля волос, цвета кожи/волос/одежды, AnimatorOverrideController runtime, CustomisationWindow UI (full-screen overlay), Bug #1 (domain reload → heightScale=0) исправлен
> - **Equipment Visual Phase 2 ✅** — Bone mapping (7+ slots: Weapon/Shield/Helmet/Chest), visual prefab на ItemData, CharacterEquipmentVisualApplier, Unity Avatar HumanBodyBones
> - **Input System Phase 1-2.5 ✅** — InputBindingsConfig SO (31 биндинг), EscMenuWindow (UI Toolkit), InputRebindingPanel (Listen→Assign→Save/Reset), PlayerPrefsInputRepository, DefaultInputRestorer
> - **Character Animations v0.5.1 ✅** — Animator BlendTree directional movement (8-way), combat animations (punch/kick/block), female override controller, NetworkPlayer animation event sync
>
> Подробный статус — в `docs/MMO_Development_Plan.md` (v0.0.35). Design-контент GDD (lore, формулы, дизайн-решения) остаётся в зоне game-designer'а. Секции "Реализация в коде" обновлены для всех новых подсистем.

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
| 12.1 | [GDD_12_1_Scene_World_Streaming.md](GDD_12_1_Scene_World_Streaming.md) | Мир: 24 сцены, 4×6 grid, boundary-based loading | ✅ Готово |
| 13 | [GDD_13_UI_UX_System.md](GDD_13_UI_UX_System.md) | UI/UX: HUD, меню, навигация, подсказки, стиль | ✅ Готово |
| 14 | [GDD_14_Visual_Art_Pipeline.md](GDD_14_Visual_Art_Pipeline.md) | Визуал: URP, шейдеры, постобработка, арт-пайплайн | ✅ Готово |
| 15 | [GDD_15_Audio_System.md](GDD_15_Audio_System.md) | Аудио: SFX, музыка, эмбиент, позиционный звук | ✅ Готово |
| 25 | [GDD_25_Combat_Skills.md](GDD_25_Combat_Skills.md) | 🆕 Бой: DamageCalculator, AOE, прицеливание, скиллы, SkillTree | ✅ Реализовано |
| 26 | [GDD_26_Character_Customisation.md](GDD_26_Character_Customisation.md) | 🆕 Кастомизация: пол/пресет/цвета/волосы + Equipment Visual | ✅ Реализовано |

## 📖 Content — Контентные системы

| # | Файл | Описание | Статус |
|---|------|----------|--------|
| 20 | [GDD_20_Progression_RPG.md](GDD_20_Progression_RPG.md) | Прогрессия: уровни, навыки, характеристики, деревья | ✅ Готово |
| 21 | [GDD_21_Quest_Mission_System.md](GDD_21_Quest_Mission_System.md) | Квесты: типы, генерация, награды, цепочки | ✅ Готово |
| 22 | [GDD_22_Economy_Trading.md](GDD_22_Economy_Trading.md) | Экономика: валюта, цены, торговля, рынок | ✅ Готово |
| 23 | [GDD_23_Faction_Reputation.md](GDD_23_Faction_Reputation.md) | Фракции: гильдии, репутация, ранги, отношения | ✅ Готово |
| 24 | [GDD_24_Narrative_World_Lore.md](GDD_24_Narrative_World_Lore.md) | Нарратив: лор, история, персонажи, сюжет | ✅ Готово |

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
