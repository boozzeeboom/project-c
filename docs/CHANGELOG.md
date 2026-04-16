# Changelog — Project C: The Clouds

---

## v0.0.14-world-streaming-phase2 (16 апреля 2026)

**Ветка:** `qwen-gamestudio-agent-dev`
**Этап 3: World Streaming Phase 2 — Multiplayer Integration** — ✅ ОСНОВНЫЕ КОМПОНЕНТЫ РЕАЛИЗОВАНЫ

### 🆕 Новое

#### World Streaming Multiplayer (Phase 2)

**PlayerChunkTracker.cs:**
- Server-side компонент для отслеживания позиции игроков в чанках
- Автоматический поиск NetworkPlayer компонента
- RPC: LoadChunkClientRpc, UnloadChunkClientRpc
- Поддержка loadRadius/unloadRadius для hysteresis

**ChunkNetworkSpawner.cs:**
- Server-side спавн/деспавн NetworkObjects с чанками
- События OnChunkLoaded/OnChunkUnloaded
- Автоматическая привязка ChestContainer к чанку

**StreamingTest.cs (обновлён):**
- Поддержка локального игрока в мультиплеере
- TryFindLocalPlayer() — поиск NetworkPlayer с IsOwner
- GetCurrentPosition() — унифицированное получение позиции

**PlayerChunkTracker.cs (обновлён):**
- showDebugLogs = false по умолчанию (меньше спама)
- GetChunkIdAtPosition() — fallback для позиций за пределами мира

### 📦 Новые/обновлённые файлы

| Файл | Строк | Изменение |
|------|-------|-----------|
| `Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs` | 371 | Создан |
| `Assets/_Project/Scripts/World/Streaming/ChunkNetworkSpawner.cs` | 347 | Создан |
| `Assets/_Project/Scripts/World/Streaming/StreamingTest.cs` | 324 | Обновлён |
| `docs/world/LargeScaleMMO/PHASE2_COMPONENT_STATUS.md` | 250 | Создан |
| `docs/world/LargeScaleMMO/SESSION_PROMPT_Phase2_MultiplayerIntegration.md` | 419 | Обновлён |

### 🔧 Интеграция

- PlayerChunkTracker ↔ WorldStreamingManager — server/client RPC
- FloatingOriginMP — BroadcastWorldShiftRpc для синхронизации сдвига
- NetworkManagerController — события подключения/отключения
- ChunkLoader — события OnChunkLoaded/OnChunkUnloaded

### ⚠️ Требуется тестирование

1. Одиночная игра (F5-F10)
2. Host + Client подключение
3. Синхронизация FloatingOrigin
4. Спавн/деспавн объектов

---

## v0.0.13-urp-setup (6 апреля 2026)

**Ветка:** `qwen-gamestudio-agent-dev`
**Этап 2.5: Визуальный прототип** — 🔄 В ПРОЦЕССЕ

### 🆕 Новое

#### URP Pipeline (Universal Render Pipeline)
- ✅ URP пакет установлен (com.unity.render-pipelines.universal 17.4.0)
- ✅ URP Pipeline Asset создан и назначен в Graphics Settings
- ✅ Все материалы конвертированы: Standard → URP Lit/Unlit
- ✅ WorldGenerator.cs обновлён: использует URP/Lit и URP/Unlit
- ✅ MaterialURPUpgrader.cs — скрипт массовой конвертации материалов (ProjectC → Upgrade Materials to URP)

#### Облака Ghibli-стиль
- ✅ CloudGhibli.shader — кастомный URP Unlit шейдер
  - Noise + rim glow + vertex displacement (морфинг форм)
  - Soft edges, depth fade, scroll UV анимация
- ✅ ProceduralNoiseGenerator.cs — FBM noise текстуры 512×512
- ✅ CloudLayer.cs — авто-интеграция CloudGhibli при старте

#### Документация
- ✅ docs/ART_BIBLE.md — полная визуальная спецификация (12 секций)
  - Цветовая палитра, освещение, post-processing
  - Спецификации кораблей, персонажей, окружения, UI
  - Пайплайн ассетов, конвенция имён, референсы
- ✅ docs/unity6/UNITY6_URP_SETUP.md — справочник по URP в Unity 6
  - Breaking changes в URP 17
  - Частые ошибки и решения
  - Сериализуемые свойства Pipeline Asset
- ✅ docs/MMO_Development_Plan.md — добавлен Этап 2.5: Визуальный прототип
- ✅ docs/INDEX.md — обновлён каталог

### 📦 Новые файлы
| Файл | Назначение |
|------|-----------|
| `Assets/_Project/Art/Shaders/CloudGhibli.shader` | Кастомный шейдер облаков (URP Unlit) |
| `Assets/_Project/Material/CloudMaterial_URP.mat` | URP-совместимый материал облаков |
| `Assets/_Project/Material/character_URP.mat` | URP-совместимый материал персонажа |
| `Assets/_Project/Scripts/Core/MaterialURPConverter.cs` | Авто-конвертация материалов при запуске |
| `Assets/_Project/Scripts/Core/ProceduralNoiseGenerator.cs` | Генерация noise-текстур (FBM) |
| `Assets/_Project/Scripts/Editor/MaterialURPUpgrader.cs` | Массовая конвертация Standard → URP |
| `Assets/_Project/Scripts/Core/CloudLayer.cs` | Обновлён: авто-интеграция CloudGhibli |
| `Assets/_Project/Scripts/Core/WorldGenerator.cs` | Обновлён: URP/Lit + URP/Unlit |
| `docs/ART_BIBLE.md` | Визуальная спецификация проекта |
| `docs/unity6/UNITY6_URP_SETUP.md` | Справочник URP для Unity 6 |

### 🐛 Исправления
| Баг | Решение |
|-----|---------|
| Все материалы розовые (Standard не работает в URP) | Конвертированы в URP Lit/Unlit |
| CloudGhibli.shader не компилировался (Core.hlsl not found) | URP пакет установлен, Pipeline Asset назначен |
| CS0618 FindObjectsSortMode deprecated | Убран из MaterialURPConverter.cs |
| CS0246 UniversalRenderPipelineAsset not found | Удалён скрипт URPSetup.cs, настройка через Editor |

### ⚠️ Известные проблемы
| Приоритет | Проблема | Статус |
|-----------|----------|--------|
| 🟡 Средне | Модель корабля — примитив (сфера) | ⏳ Этап 2.5 |
| 🟡 Средне | Персонаж — capsule | ⏳ Этап 2.5 |
| 🟢 Низко | Горные пики — процедурные без текстур | ⏳ Этап 2.5 |

---

## v0.0.12-stage2-complete (5 апреля 2026)

**Ветка:** `qwen-gamestudio-agent-dev`
**Этап 2: Сетевой фундамент** — ✅ ЗАВЕРШЁН

### 🆕 Новое

#### Dedicated Server
- Кнопка "Start Server" в NetworkUI (назначается через Inspector)
- Автозапуск при аргументе `-server` или `-dedicatedserver`
- Headless режим: `-batchmode -nographics -server`
- Документация: `docs/DEDICATED_SERVER.md`

#### Reconnect система
- Авто-реконнект при `OnTransportFailure` (до 5 попыток, задержка 3с)
- Кнопка "Reconnect" в UI (появляется после Disconnect или провала авто-реконнекта)
- Сохранение последнего IP:Port для быстрого переподключения
- Упрощённый reconnect: Shutdown → ConnectToServer (без пересоздания NetworkManager)

#### Сохранение инвентаря
- `Inventory.SaveToPrefs()` — сохраняет предметы в PlayerPrefs при Disconnect
- `Inventory.LoadFromPrefs()` — восстанавливает инвентарь при OnNetworkSpawn
- Инвентарь не теряется после реконнекта

#### Синхронизация подбора
- `HidePickupRpc` (SendTo.Everyone) — подобранный предмет исчезает у ВСЕХ игроков
- `OpenChestRpc` (SendTo.Everyone) — сундук открывается/скрывается у ВСЕХ
- Раньше: клиент скрывал предмет только у себя

#### Player Count
- `playerCountText` в NetworkUI обновляется при connect/disconnect/host start
- Host учитывается как +1 к ConnectedClients

#### ItemDatabaseInitializer
- Автоматическая регистрация ВСЕХ предметов при старте:
  - `Resources/Items/` — ScriptableObject предметы
  - `PickupItem` на сцене — каждый предмет на земле
  - `ChestContainer.LootTable` — entries + guaranteedItems из сундуков

### 🐛 Исправления

| Баг | Решение |
|-----|---------|
| PickupItem.Collect() использовал FindAnyObjectByType | Убран, теперь NetworkPlayer управляет подбором |
| Дубликат HidePickupRpc в NetworkPlayer | Удалён |
| NetworkInventory (NetworkVariable<string>) не работал | Откат — NGO не поддерживает string в NetworkVariable |
| Сундуки сломались после изменений NetworkInventory | Возвращён старый рабочий подбор |
| Предметы из сундуков не работали (ID=-1) | ItemDatabaseInitializer регистрирует предметы из LootTable |
| Unity Editor UI спам ошибок после компиляции | Это баг Unity, не наш код |

### 📝 Документация

| Файл | Изменение |
|------|-----------|
| `docs/NETWORK_ARCHITECTURE.md` | Обновлена: Reconnect, Dedicated Server, Player Count, ItemDatabase |
| `docs/STEP_BY_STEP_DEVELOPMENT.md` | Добавлена запись сессии 5 апреля |
| `docs/MMO_Development_Plan.md` | Этап 2 отмечен как завершённый |
| `docs/DEDICATED_SERVER.md` | Новый файл — руководство по запуску сервера |
| `README.md` | Обновлён статус и список фич |

### ❌ Отложено (Этап 5+)

- Отдельный серверный билд (.NET 8 / Master-сервер)
- Система лобби/комнат (матчмейкинг, приглашения)
- Полная синхронизация инвентаря (NetworkVariable не работает со string)
- Серверная валидация инвентаря (anti-cheat)

### 📊 Статистика

| Метрика | Значение |
|---------|----------|
| Коммитов | 8 |
| Файлов изменено | 10+ |
| Новых файлов | 2 (`NetworkInventory.cs`, `ItemDatabaseInitializer.cs`, `DEDICATED_SERVER.md`) |
| Файлов удалено | 0 |
| Известных багов | 3 (низкий приоритет) |

---

## v0.0.11-disconnect-fix (5 апреля 2026)

**Ветка:** `qwen-gamestudio-agent-dev`

### Исправлено
- Disconnect кнопка перемещена в центр экрана
- Обработка обрывов соединения (OnClientDisconnectCallback, OnTransportFailure)
- Debug логирование Canvas и RectTransform

---

## v0.0.10-network-coop (5 апреля 2026)

**Ветка:** `qwen-gamestudio-agent-dev`

### Добавлено
- Кооп-корабли (несколько игроков, усреднение ввода)
- Boost (Shift) для кораблей через ServerRpc
- Посадка/выход из корабля (F) — синхронизация всем
- Персональная камера для каждого игрока

---

*Примечание: changelog ведётся с версии v0.0.10. Более ранние изменения не задокументированы.*
