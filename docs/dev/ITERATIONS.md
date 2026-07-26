# Итерации разработки

## Итерация от 2026-07-26 (fix 2)

**Задача:** Дополнительное подавление GC-аллокаций от `NotifyNavMeshAdded` + чистка debugMode-логов.

**Коммит:** `d9c2244` — T-PERF01-fix: подавление GC-аллокаций от NotifyNavMeshAdded (filterLogType=Exception) + #if UNITY_EDITOR

**Изменения:**
- `Assets/_Project/Scripts/Ship/ShipDeckNav.cs` — `Register()`: `filterLogType = Exception` на время `AddNavMeshData` — глушит ВСЕ логи включая C++ native
- `Assets/_Project/Scripts/Core/ShipPosition/ShipPositionServer.cs` — `debugMode`-лог в `Update()` обёрнут в `#if UNITY_EDITOR`

## Итерация от 2026-07-26

**Задача:** Архитектурный рефакторинг — устранение ритмичных лагов от NavMesh-регистрации и FindObjectsByType.

**Коммит:** `f5a5602` — T-PERF01: Архитектурный рефакторинг — устранение ритмичных лагов от NavMesh-регистрации и FindObjectsByType

**Изменения:**
- `Assets/_Project/Scripts/Ship/ShipDeckNav.cs` — round-robin очередь регистрации (≤1 AddNavMeshData/кадр) вместо random stagger 0-10s; SetStackTraceLogType(Log, None) через RuntimeInitializeOnLoadMethod
- `Assets/_Project/Scripts/Core/ShipPosition/ShipPositionServer.cs` — кэш ShipController'ов (GetCachedShips) вместо FindObjectsByType каждый save-тик; InvalidateShipCache() для внешней инвалидации
- `Assets/_Project/Trade/Scripts/Network/MarketZone.cs` — кэш FindLocalPlayer на 0.5s вместо FindObjectsByType<NetworkPlayer> каждые 0.25s на каждой MarketZone
- `Assets/_Project/Scripts/UI/UIManager.cs` — #if UNITY_EDITOR на не-guarded Debug.Log в HandleGlobalInput

## Итерация от 2025-07-18 (вечер)

**Задача:** Исправить runtime-варнинги после теста — kinematic velocity, DontDestroyOnLoad, ShipCargoVisual.

**Коммит:** `a264438` — T-CORE15: Fix runtime warnings

**Изменения:**
- `ShipController.cs` — 5 мест `_rb.linearVelocity/angularVelocity` обёрнуты в `!isKinematic`
- `ShipPositionServer.cs` — `ApplyRestore`: velocity только если `!isKinematic`
- `ShipCargoVisual.cs` — `Debug.LogError` → `Debug.LogWarning` для пустых `_boxPrefabs`
- `ConstellationController.cs` — `SetParent(null)` перед `DontDestroyOnLoad`

## Итерация от 2025-07-18


**Задача:** Исправить ~35 накопившихся compiler warnings (obsolete API Unity 6: RPC, FindObjectsSortMode, FindFirstObjectByType, FindObjectOfType; CS0414/CS0219/CS0253; TMP asset corruption).

**Коммит:** `c530371` — T-CORE14: Fix ~35 accumulated compiler warnings

**Изменения:**
- `PlayerRespawnTracker.cs` — `[ServerRpc(RequireOwnership)]` → `[Rpc(SendTo.Server, InvokePermission)]`, `FindObjectsByType<T>(FindObjectsSortMode)` → `FindObjectsByType<T>()`
- `ShipController.cs` — `[Rpc(SendTo.Server, RequireOwnership)]` → `[Rpc(SendTo.Server, InvokePermission)]`
- `TargetLockService.cs` — `FindObjectsByType<T>(FindObjectsSortMode)` → `FindObjectsByType<T>()`, `FindFirstObjectByType` → `FindAnyObjectByType`
- `ShipPositionServer.cs` — `FindObjectsByType<T>(FindObjectsSortMode)` → `FindObjectsByType<T>()`
- `PlayerPositionServer.cs` — `FindObjectsByType<T>(FindObjectsSortMode)` → `FindObjectsByType<T>()`
- `RepairManagerWindow.cs` — `FindObjectsByType<T>(FindObjectsInactive, FindObjectsSortMode)` → `FindObjectsByType<T>(FindObjectsInactive)`
- `SplineWindZone.cs` — `FindObjectsByType<T>(FindObjectsSortMode)` → `FindObjectsByType<T>()`
- `NpcWorldInspectorWindow.cs` — `FindObjectsByType<T>(FindObjectsInactive, FindObjectsSortMode)` → `FindObjectsByType<T>(FindObjectsInactive)`
- `SkillVfxService.cs` — `FindObjectOfType` → `FindAnyObjectByType`
- `NpcSocialBrain.cs` — CS0253: cast к `UnityEngine.Object` при сравнении `IDamageTarget`
- `SettingsManager.cs` — CS0414 suppress для `_initialized`
- `ThirdPersonCamera.cs` — CS0414 suppress для `mouseSensitivityX/Y`
- `ShipObservationCamera.cs` — CS0414 suppress для `_rotateSpeed`
- `WorldCamera.cs` — CS0414 suppress для `mouseSensitivityX/Y`
- `QuestNodeGraphView.cs` — CS0414 suppress для `_showAllMode`
- `ResourcesCsvImporter.cs` — CS0219: удалён неиспользуемый `reason`
- `LiberationSans SDF - Fallback.asset` — пересохранён после force refresh (corrupt metadata)

## Итерация от 2025-07-17


**Задача:** Исправить баг: визуалы двигателя (EngineThrusterVisual, ShipPartShake) реагируют на WASD после выхода из корабля (F) и перехода в пеший режим.

**Коммит:** `1812fea` — T-ENG02: фикс визуалов двигателя — реакция на WASD после выхода из корабля

**Изменения:**
- `Assets/_Project/Scripts/Ship/Engine/EngineThrusterVisual.cs` — добавлена проверка `!_shipController.enabled` в Update()
- `Assets/_Project/Scripts/Ship/ShipPartShake.cs` — добавлена проверка `!_shipController.enabled` в Update()
- `Assets/_Project/Scripts/Player/PlayerStateMachine.cs` — Disembark() отключает ShipInputReader, ApplyFlying() включает его

## Итерация от 2025-07-17 (v2)

**Задача:** Та же — первая итерация фиксила не тот код-путь. Реальный disembark идёт через NetworkPlayer, не PlayerStateMachine.

**Коммит:** `abfa9ff` — T-ENG02: фикс визуалов двигателя v2 — правильный путь disembark в NetworkPlayer

**Изменения:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — Disembark: отключает ShipInputReader; Board: включает ShipInputReader
- `Assets/_Project/Scripts/Player/ShipInputReader.cs` — OnDisable(): сброс _currentThrust/_currentYaw в ноль
- Защитные проверки `!_shipController.enabled` из v1 в EngineThrusterVisual, ShipPartShake сохранены
- Фикс в PlayerStateMachine из v1 сохранён (для офлайн/тестового режима)
