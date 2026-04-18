# Large Scale MMO — Streaming System

**Проект:** ProjectC_client  
**Unity:** 6.0.0+ с URP  
**Сеть:** Netcode for GameObjects (NGO)  
**Дата:** 18.04.2026  
**Версия:** `v0.0.18-deep-analysis`

---

## 📁 Структура документов

```
docs/world/LargeScaleMMO/
├── README.md                    ← Этот файл: навигация
├── CURRENT_STATE.md              ← Глубокий анализ (результат 3 subagents)
├── ITERATION_PLAN.md             ← План итераций с exact кодом
├── 01_Architecture_Plan.md       ← Полная архитектура
├── combinesessions/              ← Промпты для запуска итераций
│   ├── INDEX.md                 ← Оглавление
│   ├── ITERATION_1_SESSION.md   ← FloatingOriginMP Jitter Fix
│   ├── ITERATION_2_SESSION.md   ← WorldStreamingManager Integration
│   ├── ITERATION_3_SESSION.md   ← PlayerChunkTracker Integration
│   ├── ITERATION_4_SESSION.md   ← Setup & Test
│   └── ITERATION_5_SESSION.md   ← Multiplayer Test
├── old_sessions/                 ← Архив прошлых сессий
│   ├── README.md
│   ├── SESSION_2026-04-17_*.md   ← 17 файлов анализа
│   ├── SESSION_2026-04-18_*.md
│   ├── FLOATING_ORIGIN_*.md
│   ├── ARTIFACT_*.md
│   └── TESTING_*.md
└── [вспомогательные файлы]
    ├── AGENTS_PROMPTS.md
    ├── FLOAT_PRECISION_ISSUE.md
    ├── SOLUTION_OPTIONS.md
    ├── MAIN_SCENE_SETUP.md
    └── SESSION_PROMPT_*.md
```

---

## 🎯 Quick Start

### Хочешь узнать состояние?
→ `CURRENT_STATE.md` — **6 проблем интеграции**, диаграмма связей

### Хочешь понять что делать дальше?
→ `ITERATION_PLAN.md` — **5 итераций**, exact код для каждого исправления

### Хочешь понять архитектуру?
→ `01_Architecture_Plan.md` — полная архитектура с фазами

---

## 📊 Текущий статус (Deep Analysis: 3 Subagents)

| # | Проблема | Серьёзность | Итерация |
|---|----------|-------------|----------|
| 1 | FloatingOriginMP конфликтует с ChunkLoader | 🔴 Critical | 1 |
| 2 | FloatingOriginMP jitter после телепорта | 🔴 Critical | 1 |
| 3 | WorldStreamingManager не получает события | 🟡 Medium | 2 |
| 4 | PlayerChunkTracker слабо связан с NetworkPlayer | 🟡 Medium | 3 |
| 5 | ChunkNetworkSpawner prefabs = null | 🟡 Medium | 4 |
| 6 | StreamingTest компоненты не подключены | 🟡 Medium | 4 |

---

## 🔧 Компоненты кода

```
Assets/_Project/Scripts/World/Streaming/
├── FloatingOriginMP.cs          ← 1020 строк, MP-synced
│   └── ПРОБЛЕМА: jitter + конфликт с ChunkLoader
├── WorldStreamingManager.cs      ← 651 строка, координатор
│   └── ПРОБЛЕМА: нет обратной связи от ChunkLoader
├── WorldChunkManager.cs          ← 323 строки, реестр чанков
├── ProceduralChunkGenerator.cs   ← 392 строки, генерация
├── ChunkLoader.cs                ← 412 строк, load/unload
│   └── ПРОБЛЕМА: события не подключены
├── PlayerChunkTracker.cs         ← 383 строки, server-side
│   └── ПРОБЛЕМА: слабая связь с NetworkPlayer
├── ChunkNetworkSpawner.cs        ← 347 строк, spawn/despawn
│   └── ПРОБЛЕМА: prefabs = null
├── StreamingTest.cs              ← F5/F6/F7/F8/F9/F10
└── StreamingTest_AutoRun.cs
```

**Также:**
```
Assets/_Project/Scripts/Player/
└── NetworkPlayer.cs              ← 600+ строк
    └── ПРОБЛЕМА: не обновляет PlayerChunkTracker
```

---

## 📋 5 Итераций для исправления

| # | Итерация | Длительность | Критерий приёмки |
|---|----------|--------------|------------------|
| 1 | Fix FloatingOriginMP Jitter & Integration | 1-2 сессии | F6 без jitter |
| 2 | Fix WorldStreamingManager Integration | 1 сессия | Console "Chunk loaded" |
| 3 | Fix PlayerChunkTracker Integration | 1-2 сессии | RPC отправляется |
| 4 | Setup & Test | 1-2 сессии | F5/F6/F7/F8 работают |
| 5 | Multiplayer Test | 1-2 сессии | Host + Client синхронизированы |

---

## 📊 Component Integration Map

```
┌─────────────────────────────────────────────────────────────────────┐
│                         CLIENT SIDE                                  │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────┐     ┌─────────────────────────┐                   │
│  │ NetworkPlayer │────▶│ FloatingOriginMP        │                  │
│  │ (transform)   │     │ positionSource          │                  │
│  └──────────────┘     │ OnWorldShifted +=       │                  │
│         │              └─────────────────────────┘                  │
│         ▼                      │                                     │
│  ┌──────────────┐             │                                     │
│  │WorldStreaming │◀────────────┘                                     │
│  │  Manager      │     ⚠️ НЕТ ОБРАТНОЙ СВЯЗИ                        │
│  │ (координатор) │     ┌─────────────────────────┐                  │
│  └───────┬───────┘     │ ChunkLoader              │                  │
│          │             │ OnChunkLoaded +=        │  ← НЕ ПОДКЛЮЧЕНО │
│          ▼             │ OnChunkUnloaded +=       │  ← НЕ ПОДКЛЮЧЕНО │
│  ┌──────────────┐     └─────────────────────────┘                  │
│  │WorldChunk     │                                                  │
│  │  Manager      │     ┌─────────────────────────┐                  │
│  │ (реестр)      │     │ ProceduralChunkGenerator │                  │
│  └──────────────┘     └─────────────────────────┘                  │
│          │                      │                                    │
│          ▼             ┌─────────────────────────┐                  │
│  ┌──────────────┐     │ ChunkNetworkSpawner     │                  │
│  │ FloatingOrigin│     │ ⚠️ prefabs = null       │                  │
│  │ MP             │     └─────────────────────────┘                  │
│  └──────────────┘                                                  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                         SERVER SIDE                                  │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────┐     ┌─────────────────────────┐                  │
│  │ NetworkManager│────▶│ PlayerChunkTracker       │                  │
│  │              │     │ LoadChunkClientRpc()      │                  │
│  └──────────────┘     │ ⚠️ НЕ получает позицию  │                  │
│         │             │    от NetworkPlayer      │                  │
│         ▼             └─────────────────────────┘                  │
│  ┌──────────────┐                                                  │
│  │ NetworkPlayer │                                                 │
│  │ (Owned)       │────⚠️──▶ PlayerChunkTracker.UpdatePosition()    │
│  └──────────────┘       НУЖНО ДОБАВИТЬ                             │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 🚀 Начать с Iteration 1

**Файл:** `ITERATION_PLAN.md` → Section "Iteration 1: Fix FloatingOriginMP Jitter & Integration"

**Код для исправления уже предоставлен:**
- 1.1 GetWorldPosition() fix (строки ~500-600)
- 1.2 ShouldUseFloatingOrigin() для зон ответственности
- 1.3 События синхронизации

---

**Автор:** Claude Code + 3 Subagents  
**Анализ:** 44.9% context usage  
**Дата:** 18.04.2026