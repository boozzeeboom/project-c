# T-FO06CZ — Rebase scope (inactive roots) + native executor identity-drift survivability

Date: 2026-09-13

Источники: `Q:\Project-c_logs\f8_1.txt`, `Q:\Project-c_logs\f9_1.txt` (оба лога разобраны полностью).

Статус: **FIX IMPLEMENTED; F8/F9 RETEST REQUIRED (user)**.

## Диагноз (подтверждён обоими логами)

### Дефект 1 — неверный участник rebase

`WorldScene_0_0` содержит отключённый корень-префаб `SPAWN_TEST cult`
(`m_IsActive: 0`; внутри `NpcSpawner` + `NetworkObject` + `GlobalSceneSourceMarker`,
каталогизирован как unmanaged, `treatment=5, ownership=3, spatial=0`).
`GlobalMotionControlledRebaseSlice.TryBuildParticipants` при `_includeAllWorldSceneRoots=1`
регистрировал ВСЕ корни сцены, включая неактивные, как `CityStatic`-участников.
Контракт координатора требует `IsCurrent` (`activeInHierarchy`) → подготовка отклонялась:

```
runtimeRebase.Rejected(prepare_refused:stale_participant:WORLD_SCENE_ROOT/SPAWN_TEST cult)
```

(f8_1: frame 419; f9_1: frame 326). На F8 не было `Applied/Completed`, на F9 — настоящего rollback.

### Дефект 2 — session-killing ответ на identity drift

Хронология fault'а (одинаково в обоих логах, ровно один раз за прогон):

```
ShipPositionServer.RestoreCoroutine → Loaded 22 ships → Restored 22/22 from save
→ DockingWorld.AssignPad (Pad auto-registered ×5)
→ [ShipDeckNav:*] Registered at ... (20 пере-регистраций)
→ NpcBrain EnsureProxy (AddComponent<NavMeshAgent>, NpcBrain.cs:737 warning)
→ InvalidOperationException: Bound source/frame/scene/parent identity changed
   (GlobalSceneNativeExecutor.Advance, line 377)
→ Fault() → NetworkManager.Shutdown() → despawn всех NetworkObject → исчезновение мира
```

150/150 prepared-источников были `recorded` — fault случился на уже-recorded узле в
steady-state re-check. Проверенные код-пути окна (`ApplyRestore`, `EnterDocked/ExitDocked`,
`ApplyPersistenceFreeze`, `NpcShipController.RestoreFromSave`, `NpcShipWorld.RestoreNpcState`,
`DockingWorld.AssignPad`, `ShipDeckNav.Register`) не содержат операций над parent/scene
каталогизированных объектов — статически writer drift не определяется однозначно.
Наблюдаемое исчезновение мира вызывал не сам drift, а ответ на него: `Fault() → Shutdown()`.

## Исправления

### 1. `GlobalMotionControlledRebaseSlice.cs`

- `TryBuildParticipants` исключает корни с `activeInHierarchy == false` из participant
  scope; пропущенные корни перечисляются в логе
  `[T-FO06CZ] Inactive scene roots excluded from rebase scope: ...`.
- В этот же файл входит незакоммиченный перенос триггеров F8/F9 на
  `UnityEngine.InputSystem` (`wasPressedThisFrame`) из предыдущей сессии.

### 2. `GlobalSceneNativeExecutor.cs`

- `RequireIdentity` → `TryRequireIdentity`: именованный диагноз — какой sub-check,
  какой `sourceId`/marker, expected/actual (`parent_changed`, `scene_changed`,
  `source_id_changed`, `marker_destroyed`, `activation_flag_changed`,
  `network_object_lost`, `frame_identity_changed`). Канонический префикс сообщения
  сохранён: `Bound source/frame/scene/parent identity changed: <причина>`.
- `Advance()`: identity drift на `node.Recorded` источнике больше не бросает исключение —
  executor переходит в steady-state fault (`FaultSteadyState`): именованный
  `[T-FO06CZ]` error, `CanAcceptScenePeer = false`, ledger tickets `MarkFaulted`,
  **NGO session сохраняется** — мир остаётся видимым.
- `HasPreparedPlacement => _installed && (!_faulted || _networkRan)`: fault после
  старта сети не отзывает завершённый placement, поэтому bootstrap деградирует в
  существующую ветку `scene_admission_wait` (с `CheckQueuedDeadlines`), а не в
  `FailSession("native_scene_preparation_lost")` → `Shutdown()`.

## Что НЕ менялось

- `BootstrapScene`, сцены, префабы, каталог — без изменений.
- Writer identity drift остаётся неустановленным: следующий occurrence будет именованным
  (`[T-FO06CZ] ... parent_changed;sourceId=...;marker=...;expected=...;actual=...`).
- `SPAWN_TEST cult` остаётся в сцене (тестовый контент пользователя).
- Pre-admission fail-closed сохранён: drift на ещё не recorded источнике и все прочие
  `Fault()` пути (disable/destroy/timeout/spawn failure) по-прежнему останавливают сессию.

## Verification status

- Unity compile: pending (refresh_unity + read_console после правок).
- F8/F9 retest: NOT RUN — user-controlled.
- Legacy NavMesh warnings: IGNORED_LEGACY (вне acceptance).

## User capture procedure (F8/F9 retest)

1. Старт пилота из `BootstrapScene` как обычно; дождаться
   `[T-FO06G] Native scene readiness: ready=True;recorded=150`.
2. `F8` — ожидается БЕЗ `stale_participant:WORLD_SCENE_ROOT/SPAWN_TEST cult`;
   полная цепочка `Requested → Frozen → FramePrepared → Applied → PhysicsSynchronized →
   Rebuilt → Validated → Published → Completed → Released`; мир и объекты остаются видимыми.
3. Свежий прогон, `F9` — `RollbackRequested → RollbackCompleted`, позиции участников
   возвращаются к pre-request значениям.
4. Если появится `[T-FO06CZ] ... identity drift` — мир должен ОСТАТЬСЯ видимым
   (despawn больше не выполняется); прислать лог с именованной причиной.
5. Сохранить console capture и прислать результат.
