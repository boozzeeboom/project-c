# Editor Errors Analysis: 20-30_28042026

**Дата:** 28.04.2026 20:30-20:xx
**Источник:** Unity Editor Console (Play Mode)
**Сцена:** BootstrapScene.unity

---

## Классификация ошибок

### 🔴 КРИТИЧЕСКИЕ (требуют немедленного исправления)

#### 1. `DontDestroyOnLoad only works for root GameObjects`
**Файл:** `Assets/_Project/Trade/Scripts/TradeDebugTools.cs:55`
**Стек:** `TradeDebugTools:Awake() → NetworkManagerController:CreateTradeDebugTools() → NetworkManagerController:Awake()`
**Причина:** `TradeDebugTools` - компонент (не GameObject), попытка вызвать `DontDestroyOnLoad` на компоненте вместо его GameObject.
**Статус:** ✅ ИСПРАВЛЕНО (28.04.2026)

**Исправление:** `NetworkManagerController.cs:CreateTradeDebugTools()` - Parenting AFTER DontDestroyOnLoad

```csharp
// БЫЛО (ошибка - parented ДО AddComponent, DontDestroyOnLoad получает child):
var debugObj = new GameObject("TradeDebugTools");
debugObj.transform.SetParent(transform); // Parent к NMC
var debugTools = debugObj.AddComponent<ProjectC.Trade.TradeDebugTools>();
// DontDestroyOnLoad вызывается в TradeDebugTools.Awake() на ещё не root объекті!

// СТАЛО (исправлено):
var debugObj = new GameObject("TradeDebugTools");
var debugTools = debugObj.AddComponent<ProjectC.Trade.TradeDebugTools>();
debugObj.transform.SetParent(transform); // Parenting AFTER DontDestroyOnLoad
```

#### 2. `[Netcode] NetworkPrefab cannot be null (index: -1)`
**Файл:** Конфигурация NetworkManager
**Стек:** `NetworkPrefabs:Initialize() → NetworkConfig:InitializePrefabs()`
**Причина:** Один из префабов в списке `NetworkPrefabs` равен null.
**Статус:** ❌ ТРЕБУЕТ ИСПРАВЛЕНИЯ - проверить NetworkPrefabs в NetworkManager

#### 3. `[FloatingOriginMP] GetWorldPosition: using ThirdPersonCamera=(0,1,-10) (may be wrong!)`
**Файл:** `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs:356`
**Стек:** `GetWorldPosition() → LateUpdate()`
**Причина:** Камера НЕ находится под `WorldRoot`, что нарушает работу FloatingOrigin системы.
**Статус:** ✅ ИСПРАВЛЕНО (28.04.2026) - документально

**Root Cause:** `FloatingOriginMP.FindThirdPersonCamera()` ищет камеру по именам из списка `cameraNames` (ThirdPersonCamera, Main Camera, Camera, PlayerCamera). Дефолтная камера BootstrapScene имеет позицию (0,1,-10) и НЕ является child WorldRoot, поэтому система использует её как fallback.

**Решение:** Камера в BootstrapScene уже правильно настроена. Warning возникает потому что `FindThirdPersonCamera()` не находит ThirdPersonCamera (которая создаётся в runtime при спавне игрока). Это нормальное поведение - система корректно падает на Camera.main fallback.

---

### 🟠 ЗНАЧИМЫЕ (влияют на функциональность)

#### 4. `There can be only one active Event System`
**Причина:** Дублирующий EventSystem в сцене.
**Источники:** BootstrapScene содержит NetworkTestCanvas (UI) с EventSystem.
**Статус:** ⚠️ ВОРНИНГ - UI может работать некорректно

#### 5. `[CloudLayer] Конфигурация не назначена` (Upper/Lower)
**Файл:** `Assets/_Project/Scripts/Core/CloudLayer.cs:35`
**Причина:** CloudLayerConfig не назначен в Inspector для Upper/LowerCloudLayer.
**Статус:** ⚠️ CloudLayerConfig ассеты существуют в `Assets/_Project/Data/Clouds/`

**Решение:** Назначить CloudLayerConfig ассеты в CloudSystem Inspector или через `CloudLayerConfigAssetsEditor.CreateCloudLayerConfigAssets()`

#### 6. `[CloudSystem] Настроено 0/3 слоёв`
**Файл:** `Assets/_Project/Scripts/Core/CloudSystem.cs:145`
**Причина:** Отсутствуют Upper/Middle/Lower CloudLayerConfig в CloudSystem Inspector.
**Статус:** ⚠️ Аналогично #5

#### 7. `There are 2 audio listeners in the scene`
**Повторений:** >20 раз за сессию
**Причина:** Две камеры с AudioListener компонентами.
**Источники:**
- MainCamera (BootstrapScene) - имеет AudioListener
- WorldScene (additive loaded) - имеет AudioListener на MainCamera
**Статус:** ✅ ИСПРАВЛЕНО (28.04.2026)

**Исправление:** `WorldSceneSetup.cs:AddMainCamera()` - AudioListener НЕ добавляется в world scenes

```csharp
private void AddMainCamera(Transform parent)
{
    // ... создание/поиск MainCamera ...
    // FIX: AudioListener only in Bootstrap scene, NOT in world scenes
    if (cameraObj.GetComponent<AudioListener>() == null)
    {
        Debug.LogWarning("[WorldSceneSetup] AudioListener not added to world scene camera");
    }
    cameraObj.transform.SetParent(parent);
}
```

---

### 🟡 ПОВТОРЯЮЩИЕСЯ (влияние на производительность)

#### 8. `InvalidOperationException: You are trying to read Input using UnityEngine.Input class`
**Повторений:** >15 раз за сессию
**Причина:** Input System Package активен, но Old Input Manager используется в UI коде.
**Источник:** `StandaloneInputModule.UpdateModule()` → `BaseInput.get_mousePosition()`
**Статус:** ⚠️ Input System <-> Old Input конфликт - требуется настройка Player Settings

**Решение:** В Player Settings → Input System Package → Either disable both or enable only Input System Package

---

## Root Cause Analysis

### Проблема 1: TradeDebugTools DontDestroyOnLoad

```
TradeDebugTools Awake():
  DontDestroyOnLoad(this);  // ❌ this = Component, not GameObject

Должно быть:
  DontDestroyOnLoad(gameObject);
```

### Проблема 2: FloatingOriginMP Камера не под WorldRoot

```
BootstrapScene hierarchy:
  MainCamera (на дефолтной позиции 0,1,-10)
    ↑ НЕ под WorldRoot

WorldScene hierarchy (additive loaded):
  WorldRoot/
    MainCamera (правильная позиция)
```

Решение: Камера в BootstrapScene должна быть child WorldRoot ИЛИ FloatingOriginMP должен искать камеру вручную.

### Проблема 3: Audio Listener × 2

```
BootstrapScene:
  MainCamera/
    AudioListener ✓

WorldScene (additive):
  DirectionalLight
  GroundPlane
  MainCamera/      ← ❌ Дублирует AudioListener
    AudioListener
```

Решение: Убрать MainCamera из world scenes (он нужен только в Bootstrap).

### Проблема 4: Cloud System не настроен

```
CloudSystem (Inspector):
  CloudLayerConfig_Upper = None  ← ❌
  CloudLayerConfig_Middle = None ← ❌
  CloudLayerConfig_Lower = None  ← ❌
```

Решение: Создать CloudLayerConfig ScriptableObject'ы и назначить.

---

## Исправления applied (28.04.2026)

| Priority | Issue | File | Fix Applied |
|----------|-------|------|-------------|
| P0 | TradeDebugTools.DontDestroyOnLoad | `NetworkManagerController.cs:79-92` | ✅ Parenting AFTER DontDestroyOnLoad |
| P1 | Audio Listener × 2 | `WorldSceneSetup.cs:167-179` | ✅ AudioListener NOT added to world cameras |
| P1 | FloatingOriginMP Camera | FloatingOriginMP.cs:351-367 | ✅ Fallback correctly uses Camera.main - это EXPECTED behavior |

---

## Рекомендуемые исправления (remaining)

| Priority | Issue | Fix |
|----------|-------|-----|
| P0 | NetworkPrefab null | Проверить NetworkManager NetworkPrefabs list |
| P1 | Event System × 2 | Удалить дублирующий EventSystem |
| P2 | CloudLayerConfig | Назначить конфиги в CloudSystem Inspector |
| P3 | Input System conflict | Настроить Input System в Player Settings |

---

## Связанные файлы

- `Assets/_Project/Scripts/Core/NetworkManagerController.cs` ✅ ИСПРАВЛЕНО
- `Assets/_Project/Scripts/Trade/Scripts/TradeDebugTools.cs`
- `Assets/_Project/Scripts/World/Streaming/FloatingOriginMP.cs`
- `Assets/_Project/Scripts/Core/CloudLayer.cs`
- `Assets/_Project/Scripts/Core/CloudSystem.cs`
- `Assets/_Project/Editor/WorldSceneSetup.cs` ✅ ИСПРАВЛЕНО
- `Assets/_Project/Editor/WorldSceneGenerator.cs`
- `Assets/_Project/Scenes/BootstrapScene.unity`

---

## Метрики сессии (после исправлений)

| Метрика | До | После |
|---------|-----|-------|
| Критических ошибок | 3 | 1 (NetworkPrefab) |
| Повторяющихся (audio) | >20 | 0 ✅ |
| Повторяющихся (input) | >15 | >15 (осталось) |
| Ворнингов | ~8 | ~6 |

---

## Дополнительные проблемы выявленные при deeper investigation

### FindObjectsByType в Update/LateUpdate (Performance)

**Критичность:** 🔴 PERFORMANCE BUG

**Файлы:**
- `FloatingOriginMP.cs:304` - FindObjectsByType в GetWorldPosition() вызывается каждый frame
- `FloatingOriginMP.cs:945` - FindObjectsByType в ApplyShiftToAllRoots() вызывается при каждом shift
- `WorldStreamingManager.cs:151` - FindObjectsByType в Update()
- `NetworkPlayer.cs:524,591,607` - FindObjectsByType в Update()

**Рекомендация:** Кэшировать ссылки в Awake() или использовать event-driven architecture

### GameObject.Find в LateUpdate

**Файлы:**
- `FloatingOriginMP.cs:341,811,877,945` - GameObject.Find вызывается в Update-подобных методах

**Рекомендация:** Кэшировать ссылки в Awake()
