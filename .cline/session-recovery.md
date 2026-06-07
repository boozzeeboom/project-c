# Session Recovery — Project C

**Авто-обновляется хуками. НЕ редактировать вручную.**

---

## 📍 Текущее Состояние

**Ветка:** `qwen-gamestudio-agent-dev`  
**Последнее обновление:** _HOOK_TIMESTAMP_

**Этап:** Этап 2.x — Визуальный прототип с сетью  
**Активный спринт:** Sprint 4 (Polish) — в ожидании

---

## ✅ Последние Достижения

_LHOOK_SESSION_SUMMARY_

---

## 🔴 Критичные Правила (НЕ нарушать)

| Правило | Описание |
|---------|---------|
| **URP** | ❌ НЕ создавать URP ассеты через C# → ТОЛЬКО Editor UI |
| **.meta** | ❌ НЕ трогать .meta файлы |
| **Масштаб ×5** | Скриптовые объекты создавать в 5 раз меньше → умножать размеры ×5 |
| **Координаты ×50** | XZ координаты городов ×50 (радиус мира ~350,000 units) |

---

## 🔴 Известные Проблемы (приоритет)

| Приоритет | Проблема | Файл | Статус |
|-----------|----------|------|--------|
| P0 | PlayerPrefs для данных игрока | PlayerDataStore | Заменить на БД |
| P0 | AltitudeUI HUD не отображается | AltitudeUI.cs | Требует @unity-ui-specialist |
| P0 | ScriptableObject state теряется | LocationMarket | Разделить Config + State |
| P1 | Нет проверки позиции в RPC | TradeMarketServer | Добавить locationId check |
| UI | InventoryUI остаётся на OnGUI | InventoryUI.cs | Canvas-based rewrite |

---

## 📋 TODO для Следующей Сессии

1. _HOOK_NEXT_STEPS_

---

## 📂 Изменённые Файлы (эта сессия)

_LHOOK_CHANGED_FILES_

---

## 🔗 Быстрые Ссылки

| Контекст | Файл |
|----------|------|
| Полный контекст | `docs/QWEN_CONTEXT.md` (910 строк) |
| Сессия 2 кораблей | `docs/world/LargeScaleMMO/SESSION_2026-04-14.md` |
| UI система | `docs/QWEN-UI-AGENTIC-SUMMARY.md` |
| Торговля | `docs/TRADE_SYSTEM_RAG.md` |

---

## 🧠 Что Помнить

> _HOOK_MEMORY_NOTE_

---

**Версия:** 1.0 | **Обновлено:** _HOOK_TIMESTAMP_
