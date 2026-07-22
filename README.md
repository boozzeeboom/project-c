# Project C: The Clouds

**MMO-песочница** в сеттинге постапокалиптической небесной цивилизации.
Unity 6 · URP · Netcode for GameObjects

> По мотивам книги **«Интеграл Пьявица» — Бруно Арендт**

## ⚡

**Версия:** v0.0.60 · **Этап:** Визуальный прототип (Stage 2.5)
**Build target:** StandaloneWindows64

---

## ✅ Реализованные подсистемы в двух словах

**Бой и навыки:** Дальний бой (луки/арбалеты/пневматика/винтовки) · Метательное оружие (гранаты/антиграв) · Skill Tree (27+ узлов, сохранение, перезарядка, слоты) · Прицеливание Q/E · Damage Numbers · VFX-инфраструктура

**NPC:** Единая архитектура поведения (4 фазы) · Патрули, эмоции, фракции, группы, тактики боя · NPC-скиллы · Волновой спавн · Лут · **NPC Navigation: Ship-to-Build avoidance, class speed, escape corridor** · **Civilian NPC [Mira]**

**Статы:** Сквозной аудит + рефакторинг (10 проблем P0–P10) · StatBucket · Единая формула Player/NPC · Equipment multipliers

**Экономика и прогрессия:** Крафтинг (аудит + 12/12 фиксов) · Майнинг (аудит + критические фиксы) · Инвентарь v2 · Торговля · Рынок · Обмен · Система квестов (двойной аудит)

**Корабли:** Composite Ship · Стыковка + **DockPadVisualMarker v5** · NPC Ships M3.2 · Грузовая система v2 · Cargo Trade · Ремонт · Engine ON/OFF · Ship Damage · Ветер (корабль + персонаж) · NPC Crew на палубе · **Ship Preset Creator (Medium/Heavy/HeavyII)** · **ShipSummaryWindow**

**Персонаж:** Кастомизация внешности · Equipment Visual · Skill Tree оконный UI · Input System (New Input System) · **Player-ship persistence (freeze/save/restore)** · **Кнопки «СПАСЕНИЕ» + «Вызвать корабль»**

**Редакторы:** Кастомные редакторы для всех подсистем — MarketConfig, TradeDatabase, ModuleShop, DockStation, ShipController, NpcShipSchedule, NpcBrain, NpcSpawnerConfig · **NpcWorldInspector**

**Мир:** Облака (Ghibli) · 24 стриминговые сцены · День/Ночь · **Spline Wind Corridors** · **Faction Unification + Knowledge System**

---

## 🗺️ Навигация

| Куда | Зачем |
|------|-------|
| [`docs/COLABORATION.md`](docs/COLABORATION.md) | Как участвовать, CLA, контрибьюция |
| [`docs/MMO_Development_Plan.md`](docs/MMO_Development_Plan.md) | Полный roadmap и статус всех систем |
| [`docs/WORLD_LORE_BOOK.md`](docs/WORLD_LORE_BOOK.md) | Лор: технологии, фракции, сюжет |
| [`docs/gdd/GDD_INDEX.md`](docs/gdd/GDD_INDEX.md) | Все 26+ GDD документов |
| [`docs/`](docs/) | Остальная документация |

---
---

## 📜 Лицензия

| Компонент | Лицензия |
|-----------|----------|
| Исходный код (C#) | **MIT** — форкай, модифицируй, делай моды |
| Игровой контент (ассеты, лор, GDD) | **All Rights Reserved** |

Подробно: [`LICENSE`](LICENSE) · [`docs/COLABORATION.md`](docs/COLABORATION.md)

---

## 📬 Контакты

[Telegram @indeed174](https://t.me/indeed174) · [Telegram-канал @thegravity_ru](https://t.me/thegravity_ru) · [GitHub](https://github.com/boozzeeboom/project-c) · [Сайт](https://thegravity.ru/project-c/)

