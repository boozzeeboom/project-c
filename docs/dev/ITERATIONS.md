# Итерации разработки

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
