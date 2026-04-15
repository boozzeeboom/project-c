# Refactoring Docs — Project C: The Clouds

## 📁 Структура

```
docs/refactoring/
├── README.md              # Этот файл
├── REFACTOR_OVERVIEW.md   # Общий обзор всех проблем
├── SPRINT_R1_Performance_Hotfix.md   # Sprint 1: Performance
├── SPRINT_R2_Network_Sync.md         # Sprint 2: Network + Input
├── SPRINT_R3_Architecture.md         # Sprint 3: Architecture cleanup
├── SPRINT_R4_Polish.md               # Sprint 4: Polish + Prefabs
└── BACKLOG.md              # Невошедшие задачи
```

## 🎯 Цели рефакторинга

1. **Performance** — устранить allocations в hot paths
2. **Network** — реализовать INetworkSerializable, унифицировать Input
3. **Architecture** — убрать reflection, Thread.Sleep, мёртвый код
4. **Polish** — создать prefabs, обновить документацию

## 📊 Timeline

| Sprint | Дата | Фокус | Points | Статус |
|--------|------|-------|--------|--------|
| R1 | 2026-04-15 | Performance Hotfix | 9 | ✅ **DONE** |
| R2 | 2026-04-21 | Network Sync Fix | 9 | 📋 Planned |
| R3 | 2026-04-28 | Architecture Cleanup | 9 | 📋 Planned |
| R4 | 2026-05-05 | Polish & Prefabs | 10 | 📋 Planned |

**Выполнено: 9/37 points (24%)**

---

## 🔗 Связанные документы

- [Code Review Report](./REFACTOR_OVERVIEW.md) — основание для рефакторинга
- [Architecture Rules](../../.clinerules/rules/engine-code.md) — стандарты кода
- [Network Rules](../../.clinerules/rules/network-code.md) — сетевые стандарты
- [Gameplay Rules](../../.clinerules/rules/gameplay-code.md) — геймплейные стандарты

## 📈 Метрики успеха

| Метрика | До | Цель |
|---------|-----|------|
| Allocations в Update() | >10/frame | 0/frame |
| Find* вызовов в hot paths | 6+ | 0 |
| INetworkSerializable классы | 0 | 3+ |
| UI prefabs | 0 | 2+ |

## 👥 Owners

- @gameplay-programmer — Player, Inventory, Input
- @network-programmer — Network, Sync
- @unity-specialist — Engine, Architecture
- @technical-artist — UI, Prefabs

---

Обновлено: 2026-04-15