# CLAUDE.md — System Prompt (Cline + MiniMax)

**Project C: The Clouds** — MMO на Unity 6 URP. Ghibli + Sci-Fi.

---

## 🚨 КРИТИЧНОЕ (НЕ НАРУШАТЬ)

| Правило | Действие |
|---------|---------|
| **URP** | ❌ НЕ создавай URP ассеты через C# → ТОЛЬКО Editor UI |
| **.meta** | ❌ НЕ трогать .meta файлы |
| **Масштаб** | Скриптовые объекты ×5 (200×100 вместо 40×20) |
| **Координаты** | XZ городов ×50 (радиус мира ~350,000 units) |

---

## 📁 Структура

```
Assets/_Project/Scripts/
├── Core/       # Сеть, инвентарь, камера, облака
├── Player/     # NetworkPlayer, ShipController
├── Ship/       # Modules, Fuel, Wind, Turbulence
├── UI/         # TradeUI, InventoryUI, UIManager
└── World/      # Streaming, Clouds, Generation
```

---

## 🎯 Агенты (вызвать через @agent-name)

| Агент | Назначение |
|-------|------------|
| `@unity-specialist` | URP, MonoBehaviour, архитектура |
| `@network-programmer` | NGO, RPC, Floating Origin |
| `@ui-programmer` | HUD, TradeUI, Inventory |
| `@gameplay-programmer` | Механики, контроллеры |
| `@technical-artist` | Шейдеры, VFX, CloudGhibli |

**Навыки:** `/code-review`, `/sprint-plan`, `/tech-debt`, `/project-stage-detect`

---

## 📚 Контекст (читай по необходимости)

| Файл | Когда |
|------|-------|
| `.cline/session-recovery.md` | **Первым делом** — что сделано |
| `docs/QWEN_CONTEXT.md` | Полная история, проблемы, системы |
| `docs/context/network.md` | Сетевая архитектура (по запросу) |
| `docs/context/ui.md` | UI система (по запросу) |
| `docs/context/ship.md` | Система кораблей (по запросу) |

---

## 🔄 Collaboration Protocol

```
Вопрос → Варианты → Решение → Черновик → Утверждение
```
- ❌ НЕ пиши без спроса
- ✅ Показывай черновик → "Могу записать в [path]?"
- ✅ Спрашивай "Могу записать..." вместо "применять?"
- ❌ НЕ коммить без инструкции

---

**Версия:** 2.0 | **Обновлено:** 2026-04-15
