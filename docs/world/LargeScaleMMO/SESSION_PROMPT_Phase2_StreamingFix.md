# Session Prompt: Phase 2 — Streaming Fixes & Multiplayer Testing

**Дата:** 16 апреля 2026 г.  
**Проект:** ProjectC_client  
**Branch:** `qwen-gamestudio-agent-dev`  
**Last Commit:** `014d3578741a20ed8cb1ed542a93dda2c39cfebb`

---

## Контекст

### Что сделано (Phase 1 ✅)

Система World Streaming полностью реализована:

| Компонент | Файл | Статус |
|-----------|------|--------|
| FloatingOriginMP | `Streaming/FloatingOriginMP.cs` | ✅ Работает, исправлен graceful disable |
| WorldChunkManager | `Streaming/WorldChunkManager.cs` | ✅ Реестр чанков построен |
| ChunkLoader | `Streaming/ChunkLoader.cs` | ✅ Загрузка/выгрузка с fade |
| ProceduralChunkGenerator | `Streaming/ProceduralChunkGenerator.cs` | ✅ Детерминированная генерация |
| WorldStreamingManager | `Streaming/WorldStreamingManager.cs` | ✅ Координация систем |

### Известные проблемы (Bug List)

#### 🔴 Critical — Блокируют Phase 2

| # | Проблема | Приоритет | Документ |
|---|----------|----------|----------|
| 1 | **Контракты (C)** — RPC RequireOwnership | P0 | SUMMARY_2026-04-16.md |
| 2 | **Покупка на рынке (E)** — RPC не доходит | P0 | SUMMARY_2026-04-16.md |
| 3 | **Подбор предметов** — триггеры сломаны | P1 | SUMMARY_2026-04-16.md |

#### 🟡 Phase 2 Streaming Bugs

| # | Проблема | Приоритет | Документ |
|---|----------|----------|----------|
| 4 | **HUD слева** — другой OnGUI рисует раньше | P1 | PHASE2_AGENTS_PROMPT.md |
| 5 | **TeleportToPeak()** — не работает F5/F6 | P2 | PHASE2_AGENTS_PROMPT.md |
| 6 | **F7 показывает cubes** — OnDrawGizmos | P2 | PHASE2_AGENTS_PROMPT.md |

---

## Задачи на сессию

### 🔴 Priority 0: Multiplayer Fixes

#### Задача 1: Исправить RPC для контрактов

**Файлы:** `NetworkPlayer.cs`, `ContractSystem.cs`

**Проблема:** `[Rpc(SendTo.Server)]` с RequireOwnership блокирует вызовы.

**Нужно проверить:**
```csharp
// В ContractSystem.cs
// Убрать RequireOwnership или проверить senderClientId
[ServerRpc(RequireOwnership = false)]  // ← Проверить
void AcceptContractServerRpc(string contractId, ServerRpcParams rpcParams = default)
{
    ulong senderId = rpcParams.Receive.SenderClientId;
    // ...
}
```

**Шаги:**
1. Открыть `ContractSystem.cs`
2. Найти `AcceptContractServerRpc`, `CompleteContractServerRpc`, `FailContractServerRpc`
3. Проверить `RequireOwnership = false`
4. Добавить логирование `rpcParams.Receive.SenderClientId`
5. Протестировать: Host + Client, нажать C рядом с доской контрактов

---

#### Задача 2: Исправить RPC для рынка

**Файлы:** `TradeMarketServer.cs`, `NetworkPlayer.cs`

**Проблема:** Покупка на рынке не работает.

**Нужно проверить:**
```csharp
// В NetworkPlayer.cs
[Rpc(SendTo.Server)]
public void TradeBuyServerRpc(string itemId, int quantity, string locationId)
{
    // Серверная логика в TradeMarketServer
    if (TradeMarketServer.Instance != null)
    {
        TradeMarketServer.Instance.BuyItemServerRpc(itemId, quantity, locationId, OwnerClientId);
    }
}
```

**Шаги:**
1. Открыть `TradeMarketServer.cs`
2. Проверить `BuyItemServerRpc` — RequireOwnership?
3. Добавить логирование `OwnerClientId` vs `rpcParams.Receive.SenderClientId`
4. Проверить `TradeUI.Instance` — не null?
5. Протестировать: Host + Client, открыть TradeUI (E), купить товар

---

### 🟡 Priority 1: Streaming Debug

#### Задача 3: Найти конфликтующий OnGUI

**Файлы:** Все с `void OnGUI()`

**Команда:**
```bash
grep -rn "void OnGUI" Assets/_Project/Scripts/
```

**Ожидаемый результат:** FloatingOriginMP.OnGUI() рисует в правом верхнем углу (Screen.width - 320, 10), но что-то рисует раньше в (10, 10).

**Шаги:**
1. Найти все OnGUI в проекте
2. Определить какой рисует в (10, 10)
3. Исправить координаты или порядок

---

#### Задача 4: Исправить TeleportToPeak

**Файлы:** `WorldStreamingManager.cs`, `StreamingTest_AutoRun.cs`

**Проблема:** F5/F6 не телепортируют камеру.

**Код который должен работать:**
```csharp
// WorldStreamingManager.TeleportToPeak()
public void TeleportToPeak(Vector3 peakPosition)
{
    if (floatingOrigin != null)
    {
        // Сдвигаем мир так чтобы камера осталась близко к origin
        floatingOrigin.ResetOrigin();
    }
    // Загружаем чанки вокруг новой позиции
    LoadChunksAroundPlayer(peakPosition, loadRadius);
}
```

**Шаги:**
1. Проверить что `floatingOrigin` != null
2. Проверить что `StreamingTest_AutoRun` вызывает `TeleportToPeak`
3. Добавить Debug.Log в начало метода
4. Протестировать: Play Mode, нажать F5/F6

---

## План действий

```
Session Plan:
├── [ ] Task 1: Fix Contract RPC (P0)
│   ├── Read ContractSystem.cs
│   ├── Check RequireOwnership flags
│   ├── Add SenderClientId logging
│   └── Test: Host + Client + C key
├── [ ] Task 2: Fix Market RPC (P0)
│   ├── Read TradeMarketServer.cs
│   ├── Check RequireOwnership flags
│   ├── Verify TradeUI.Instance
│   └── Test: Host + Client + E key
├── [ ] Task 3: Find conflicting OnGUI (P1)
│   ├── grep "void OnGUI"
│   ├── Identify source of (10,10) HUD
│   └── Fix coordinates
└── [ ] Task 4: Fix TeleportToPeak (P2)
    ├── Verify floatingOrigin reference
    ├── Check StreamingTest_AutoRun calls
    └── Test F5/F6 keys
```

---

## Файлы для редактирования

| Файл | Действие |
|------|----------|
| `Assets/_Project/Scripts/Trade/ContractSystem.cs` | Проверить/исправить RequireOwnership |
| `Assets/_Project/Scripts/Trade/TradeMarketServer.cs` | Проверить/исправить RequireOwnership |
| `Assets/_Project/Scripts/UI/*.cs` | Найти конфликтующий OnGUI |
| `Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs` | Добавить логирование TeleportToPeak |

---

## Тестирование

### Тест 1: Multiplayer Contracts

1. Запустить Host (Окно 1)
2. Запустить Client (Окно 2), подключиться
3. На Client: подойти к доске контрактов, нажать C
4. **Ожидаемо:** Открывается список контрактов
5. **Если не работает:** Смотреть логи `[ContractSystem]`

### Тест 2: Multiplayer Market

1. Host + Client подключены
2. На Client: открыть TradeUI (E)
3. Купить любой товар
4. **Ожидаемо:** Credits уменьшаются, товар в инвентаре
5. **Если не работает:** Смотреть логи `[TMS]`, `[NetworkPlayer]`

### Тест 3: Streaming HUD

1. Play Mode, включить `showDebugHUD = true` на FloatingOriginMP
2. **Ожидаемо:** HUD в правом верхнем углу
3. Если HUD слева — искать конфликтующий OnGUI

### Тест 4: Teleport

1. Play Mode
2. Нажать F5 или F6
3. **Ожидаемо:** Камера перемещается, чанки загружаются
4. Проверить Console на `[WorldStreamingManager] Teleported to`

---

## 🌐 Интернет-исследования (Проверено 16.04.2026)

### Актуальные ссылки Unity 6.3 LTS

| Ресурс | URL | Статус |
|--------|-----|--------|
| **Dedicated Server Docs** | https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server.html | ✅ Работает |
| **Introduction to Dedicated Server** | https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server-introduction.html | ✅ Работает |
| **Get started with Dedicated Server** | https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server-get-started.html | ✅ Работает |
| **Build for Dedicated Server** | https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server-build.html | ✅ Работает |
| **Dedicated Server AssetBundles** | https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server-assetbundles.html | ✅ Работает |

### Unity NGO API — RequireOwnership

**Актуальная информация:**
```csharp
// ServerRpc по умолчанию требует Ownership
[ServerRpc]  // RequireOwnership = true (default)

// Для RPC без ownership:
[ServerRpc(RequireOwnership = false)]  // Разрешает любой клиент
void MyServerRpc(ServerRpcParams rpcParams = default)
{
    ulong senderId = rpcParams.Receive.SenderClientId;
    // senderId доступен для валидации
}
```

### Key Changes in Unity 6 NGO

1. **ServerRpcParams** — API не изменился, `rpcParams.Receive.SenderClientId` актуален
2. **ClientRpc** — аналогично, используйте `ClientRpcParams`
3. **Scene Management** — через `NetworkManager.Singleton.SceneManager`

### Дополнительные проверки

| # | Вопрос | Ответ |
|---|--------|-------|
| 1 | Требуется ли `RequireOwnership = false`? | Да, если RPC вызывается НЕ владельцем NetworkObject |
| 2 | `ServerRpcParams.Receive.SenderClientId` актуален? | Да, не изменился в Unity 6.3 |
| 3 | Dedicated Server API изменился? | Нет, основные концепции те же |
| 4 | Есть ли новые оптимизации для large worlds? | Да, см. Unity 6.3 dedicated server docs |

---

## Ресурсы

### Документация проекта

- `docs/world/LargeScaleMMO/SUMMARY_2026-04-16.md` — общий summary
- `docs/world/LargeScaleMMO/PHASE2_AGENTS_PROMPT.md` — known bugs
- `docs/world/LargeScaleMMO/02_Technical_Research.md` — техническое исследование (14.04.2026)
- `docs/world/LargeScaleMMO/01_Architecture_Plan.md` — архитектурный план
- `docs/CHANGELOG_HOST_FIX_2026-04-15.md` — предыдущие фиксы

### Код

- `Assets/_Project/Scripts/Trade/ContractSystem.cs`
- `Assets/_Project/Scripts/Trade/TradeMarketServer.cs`
- `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs`
- `Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs`

---

## Успешные критерии

| Тест | Критерий успеха |
|------|----------------|
| Contract RPC | Client видит список контрактов после нажатия C |
| Market RPC | Client может купить товар, credits уменьшаются |
| Streaming HUD | HUD FloatingOriginMP виден справа, не слева |
| Teleport | F5/F6 перемещают камеру, чанки загружаются |

---

**Следующий шаг:** 
1. После фикса критичных багов — перейти к Phase 2.1: Runtime Streaming Integration
2. Проверить актуальность Unity NGO API вручную через браузер
