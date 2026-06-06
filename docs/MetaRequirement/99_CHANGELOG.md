# MetaRequirement — Changelog

**Документ:** история изменений MetaRequirement-подсистемы.
**Дата:** 2026-06-06
**Связанные тикеты:** R2-SHIP-KEY-002, R2-META-REQ-001

---

## [Unreleased]

_(нет изменений после первого коммита)_

---

## 2026-06-06 — R2-META-REQ-001 (Этап 1)

**Статус:** ✅ **COMPILE-OK, готово к Play-mode тесту.**

### Added (новое)

#### Код
- `Assets/_Project/Scripts/MetaRequirement/RequirementLogic.cs` — enum `All`/`Any`/`AtLeastN`
- `Assets/_Project/Scripts/MetaRequirement/ProgressInfo.cs` — struct для UI tooltip
- `Assets/_Project/Scripts/MetaRequirement/MetaRequirementDto.cs` — INetworkSerializable DTO
- `Assets/_Project/Scripts/MetaRequirement/MetaRequirement.cs` — NetworkBehaviour-компонент (14 KB)
- `Assets/_Project/Scripts/MetaRequirement/MetaRequirementRegistry.cs` — server-side hub (10 KB)
- `Assets/_Project/Scripts/MetaRequirement/MetaRequirementClientState.cs` — client singleton (8 KB)
- `Assets/_Project/Scripts/MetaRequirement/MetaRequirementToast.cs` — UI toast (7 KB)
- `Assets/_Project/Scripts/MetaRequirement/LockBox.cs` — тестовая анимация (9 KB)

#### Extensions
- `Assets/_Project/Items/Core/InventoryWorld.cs` — 4 новых метода:
  - `HasAllItems(ulong clientId, int[] itemIds)` — AND-логика
  - `HasAnyItem(ulong clientId, int[] itemIds)` — OR-логика
  - `CountOf(ulong clientId, int itemId)` — сколько штук
  - `GetMissingItems(ulong clientId, int[] itemIds)` — массив недостающих

#### Wiring
- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` — `CreateMetaRequirementClientState` (auto-spawn root GO)
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs`:
  - Поля `_lastCanUseRequestTime` / `_pendingCanUseInteractableId` / `CAN_USE_REQUEST_TIMEOUT = 1.5f`
  - Метод `TryInteractNearestMetaRequirement` (E-key entry point для НЕ-кораблей)
  - Target RPC `ReceiveMetaRequirementResponseTargetRpc` + `ReceiveMetaRequirementBindingsTargetRpc`

#### Алиасы (backward compat)
- `Assets/_Project/Scripts/Ship/Key/ShipKeyBinding.cs` — пустой subclass `MetaRequirement` с `[Obsolete]`
- `Assets/_Project/Scripts/Ship/Key/ShipKeyServer.cs` — сохранён legacy API (CanPlayerBoard, RegisterBinding, RequestCanBoardRpc, PushBindingsToClient), исправлены ссылки на `ServerKeyItemId` → `ServerItemIds[0]`
- `Assets/_Project/Scripts/Ship/Key/ShipKeyClientState.cs` — сохранён legacy API (OnCanBoardResponse, OnBindingsPushed)
- `Assets/_Project/Scripts/Ship/Key/ShipKeyToast.cs` — сохранён legacy (подписка на ShipKeyClientState.OnBoardDenied)

#### Ассеты
- `Assets/_Project/Resources/Items/Item_Key_Blue.asset` — Equipment, "Ключ: Синий Замок"
- `Assets/_Project/Resources/Items/Item_Key_Red.asset` — Equipment, "Ключ: Красный Замок"
- `Assets/_Project/Resources/Items/Item_Key_Green.asset` — Equipment, "Ключ: Зелёный Замок"
- `Assets/_Project/UI/Resources/UI/MetaRequirementPanelSettings.asset` — копия `ShipKeyPanelSettings.asset` (dedicated для нового toast)
- `Assets/_Project/MetaRequirement_Test/Materials/{Key,LockBox}_{Blue,Red,Green}.mat` — 6 URP/Lit материалов (тест)

#### Сцены
- `Assets/_Project/Scenes/World/WorldScene_0_0.unity` — добавлен parent `[MetaRequirement_Test]` с 3 Pickup-сферами (Blue/Red/Green) + 3 LockBox-кубами:
  - Координаты: X=40050/40044/40038, Y=2502.7, Z=39990
  - Каждый LockBox: Cube (1.5×1.5×1.5) + NetworkObject + BoxCollider (solid) + SphereCollider (trigger r=2.5) + MetaRequirement (1 item) + LockBox (анимация)
- `Assets/_Project/Scenes/BootstrapScene.unity` — добавлено:
  - `[MetaRequirementRegistry]` (NetworkObject + MetaRequirementRegistry) — server-side hub
  - `[MetaRequirementToast]` (UIDocument + MetaRequirementPanelSettings + MetaRequirementToast) — UI

### Changed (изменено)

- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` — добавлен вызов `CreateMetaRequirementClientState()` в `Awake` (рядом с `CreateShipKeyClientState`)
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — в E-key блок добавлен вызов `TryInteractNearestMetaRequirement()` (перед chest/pickup fallback)

### Deprecated (deprecated, не удалено)

- `ShipKeyBinding` — `[Obsolete("Use ProjectC.MetaRequirement.MetaRequirement. ...)]`
- `ShipKeyServer` — `[Obsolete("Use ProjectC.MetaRequirement.MetaRequirementRegistry. ...")]`
- `ShipKeyClientState` — `[Obsolete("Use ProjectC.MetaRequirement.MetaRequirementClientState. ...")]`
- `ShipKeyToast` — НЕ отмечен `[Obsolete]` (т.к. сохраняет legacy-функциональность; см. migration guide)

### Documentation

- `docs/dev/META_REQUIREMENT_IMPL_NOTES.md` — рабочие заметки (10 KB)
- `docs/MetaRequirement/00_OVERVIEW.md` — дизайн (уже существовал, без изменений)
- `docs/MetaRequirement/RECIPES.md` — 10 рецептов (уже существовал, без изменений)
- `docs/MetaRequirement/10_IMPLEMENTATION_GUIDE.md` — step-by-step гайд (22 KB, новый)
- `docs/MetaRequirement/20_INSPECTOR_REFERENCE.md` — описание полей Inspector (17 KB, новый)
- `docs/MetaRequirement/30_RUNTIME_FLOW.md` — sequence-диаграммы + edge cases (19 KB, новый)
- `docs/MetaRequirement/40_TESTING_GUIDE.md` — тест-сценарии (13 KB, новый)
- `docs/MetaRequirement/50_KNOWN_ISSUES.md` — баги + TODO (10 KB, новый)
- `docs/MetaRequirement/99_CHANGELOG.md` — этот файл (новый)
- `docs/Ships/Key-subsystem/SHIP_KEY_TO_META_REQUIREMENT_MIGRATION.md` — migration guide (уже существовал, без изменений)
- `docs/Ships/Key-subsystem/KNOWN_ISSUES.md` — обновлён: добавлен R2-SHIP-KEY-002 (завершён), R2-META-REQ-001
- `docs/Character-menu/sub_inventory-tab/60_KNOWN_ISSUES.md` — добавлен R3-INV-DROP-001 (drop теряет визуал, не наш баг, но связан)

### Verification

- `refresh_unity` + `read_console`: **0 errors** ✓
- Warnings: только pre-existing (`FindObjectsOfType` deprecation в несвязанных файлах, obsolete-usage warnings в NetworkPlayer/ShipKeyToast — by design, алиасы)
- Compile: `Assembly-CSharp.dll` собирается чисто

### Stats

- **+7** новых C# файлов (`Scripts/MetaRequirement/*`)
- **+1** extension файл (`Scripts/MetaRequirement/LockBox.cs`)
- **+3** SO `ItemData` (ключи)
- **+1** PanelSettings (`MetaRequirementPanelSettings.asset`)
- **+6** материалов (URP/Lit)
- **+9** GameObject'ов в сценах (3 pickup + 3 box + 1 registry + 1 toast + 1 parent)
- **+9** новых/обновлённых документов в `docs/`
- **~50 KB** нового кода
- **~100 KB** новой документации

### Next Steps (Phase 2 / Этап 2)

См. `docs/MetaRequirement/50_KNOWN_ISSUES.md` §"TODO":
- `_consumeOnUse` логика + reservation pattern
- `ProgressInfo` UI
- Multi-item UI tooltip
- Disconnect → reconnect race fix

Через 1-2 релиз-цикла:
- Удалить алиасы `ShipKeyBinding/ShipKeyServer/ShipKeyClientState/ShipKeyToast` (после миграции всех сцен)

---

## Связанные тикеты

- **R2-SHIP-KEY-001** (закрыт): bug "ключи в подпапке Resources/Items" (см. `docs/Ships/Key-subsystem/KNOWN_ISSUES.md`)
- **R2-SHIP-KEY-002** (завершён): миграция ShipKey → MetaRequirement (Этап 1)
- **R2-META-REQ-001** (завершён, в этом changelog): универсальная MetaRequirement-подсистема
- **R3-INV-DROP-001** (открыт, не наш): drop теряет визуал (см. `docs/Character-menu/sub_inventory-tab/60_KNOWN_ISSUES.md`)
