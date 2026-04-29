# Scene Transition Analysis - 29.04.2026

## Проблема: Удаление свыше 79,999 не помогает

### Корневая причина

Архитектура сценовой системы НЕ использует плавающий_origin. Вместо этого:

1. **Каждая сцена 79,999 x 79,999 имеет СВОЙ локальный origin в (0,0)**
2. **World coordinates** высчитываются как: `GridX * 79999 + localX`
3. **Переход между сценами** = загрузка/выгрузка через SceneManager (additive)

### Как работает перемещение между сценами

```
Игрок движется в WorldScene_0_0
    ↓
Достигает границы (localX > 79000)
    ↓
ClientSceneLoader.Update() определяет новую сцену: SceneID.FromWorldPosition()
    ↓
Если сцена изменилась → OnSceneTransition event
    ↓
ServerSceneManager.TransitionClient() отправляет RPC
    ↓
ClientSceneLoader.LoadScene(newScene) загружает новую сцену
    ↓
ClientSceneLoader.UnloadScene(oldScene) выгружает старую
```

## Выявленные проблемы

### 1. BootstrapSceneGenerator - Дублированный код (УЖЕ ИСПРАВЛЕН)

Lines 225-258 были дубликатом после закрытия `CreatePlayerSpawner()`. Это вызывало CS1519 ошибки.

**Исправление:** Удалён дублированный код.

### 2. NetworkTestMenu - Нет кнопки "Load World"

TEST_WORKFLOW.md говорит что есть кнопка "Load World [0,0]", но в реальности её НЕТ.

**Текущие кнопки:**
- Host
- Client
- Server

**Должны быть:**
- Host
- Client
- Server
- Load World [0,0] ← ОТСУТСТВУЕТ

### 3. ClientSceneLoader - AutoLoadInitialScene работает только для Host

```csharp
if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
{
    SceneID initialScene = new SceneID(0, 0);
    yield return LoadSceneWithNeighborsCoroutine(initialScene);
}
```

Это работает, но:
- После `yield return new WaitForSeconds(1f)` IsHost уже установлен
- Однако если пользователь не нажмёт "Host", сцена не загрузится автоматически

### 4. ServerSceneManager - Ожидание спавна игрока

```csharp
private IEnumerator FindPlayerTransformCoroutine(ulong clientId)
{
    yield return new WaitForSeconds(0.5f);
    // Найти NetworkObject игрока
    // Определить SceneID по позиции
    // Зарегистрировать в _clientSceneMap
    // Отправить InitializeSceneClientRpc
}
```

Проблема: Игрок спавнится в (39999.5, 3000, 39999.5) - это Scene(0,0) центр.
Но PlayerSpawner создаётся в BootstrapGenerator.CreatePlayerSpawner() где `spawnPos = new Vector3(SCENE_SIZE / 2f, 3000f, SCENE_SIZE / 2f)` = (39999.5, 3000, 39999.5).

Это ВЕРНО для Scene(0,0), НО ServerSceneManager не получает обновление позиции.

## Что нужно исправить

### A. Добавить кнопку "Load World [0,0]" в NetworkTestMenu

В BootstrapSceneGenerator.CreateNetworkTestMenuContent():
- Добавить fourth button
- Привязать к `ClientSceneLoader.LoadInitialScene(new SceneID(0,0))`

### B. Проверить что SceneRegistry создаётся правильно

В SceneRegistry:
- GridColumns = 6
- GridRows = 4
- SceneNamePrefix = "WorldScene_"

### C. Проверить что WorldSceneGenerator создаёт сцены с правильными именами

Текущее имя: `WorldScene_{row}_{col}` (row=0-3, col=0-5)
SceneRegistry ожидает: `WorldScene_{GridX}_{GridZ}`

Проблема: WorldSceneGenerator использует `{row}_{col}` но SceneID использует `{GridX}_{GridZ}`.

Если row=X и col=Z, то `WorldScene_0_0` создаётся для row=0, col=0.
Это соответствует SceneID(0, 0).

## Проверка: Как перемещаться между сценами

1. **Запустить BootstrapScene**
2. **Нажать Host** - спавнится игрок
3. **Нажать "Load World [0,0]"** - загрузится WorldScene_0_0
4. **Двигаться** - при достижении границы, ClientSceneLoader.Update() детектит смену сцены
5. **ServerSceneManager** получает update через _playerTransforms tracking
6. **TransitionClient()** вызывается, отправляет RPC
7. **ClientSceneLoader.LoadScene()** загружает новую сцену

## Файлы требующие изменений

| Файл | Изменение |
|------|-----------|
| `BootstrapSceneGenerator.cs` | Добавить fourth button "Load World [0,0]" в CreateNetworkTestMenuContent |
| `NetworkTestMenu.cs` | Добавить fourth button и функционал |
| `ClientSceneLoader.cs` | Возможно добавить публичный метод LoadInitialSceneFromMenu() |

## Документация

- `docs/world/LargeScaleMMO/2_iteration_scene-mode/BOOTSTRAP_FIX_2026-04-29.md` - Фикс BootstrapSceneGenerator
- `docs/world/LargeScaleMMO/2_iteration_scene-mode/SCENE_ARCHITECTURE_DECISION.md` - Архитектура
- `docs/world/LargeScaleMMO/2_iteration_scene-mode/CORRECTED_ARCHITECTURE.md` - Исправленная архитектура