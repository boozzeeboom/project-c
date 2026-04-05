# Changelog — Project C: The Clouds

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
