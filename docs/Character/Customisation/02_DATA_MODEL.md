# Data Model — Customisation подсистема

> **Дата:** 2026-06-30
> **Цель:** точные .cs-сигнатуры и поля для Customisation. Companion к `00_OVERVIEW.md` и `05_PHASES_ROADMAP.md`.
> **Принцип:** additive-only — никаких изменений в существующих data-типах кроме одного нового поля в `CharacterSaveData`.

---

## 1. Архитектура типов

```
┌─────────────────────────────────────────────────────────────────┐
│                     CUSTOMISATION DATA MODEL                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────────┐    ┌──────────────────────────┐   │
│  │ CharacterCustomisation   │    │ CharacterCustomisation   │   │
│  │ Data (SO, design-time)   │    │ Preset (SO, design-time) │   │
│  │ — что МОЖНО выбрать     │    │ — пресет (Male/Female)   │   │
│  │   в игре (ranges,        │    │ — набор клипов           │   │
│  │   enums, materials)      │    │ — материалы по умолчанию │   │
│  └─────────────┬────────────┘    └─────────────┬────────────┘   │
│                │                              │                  │
│                │ configSource                │ listOfPresets      │
│                ▼                              ▼                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ CustomisationClientState (MonoBehaviour, singleton)       │   │
│  │  — CurrentSnapshot : CustomisationSnapshotDto             │   │
│  │  — OnCustomisationUpdated event                           │   │
│  │  — RequestChangeRpc → server → snapshot → OnCustomisation │   │
│  └──────────────────────────────────────────────────────────┘   │
│                │                                                  │
│                │ OnCustomisationUpdated event                     │
│                ▼                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ CharacterCustomisationApplier (MonoBehaviour on player)  │   │
│  │ — подписан на OnCustomisationUpdated                      │   │
│  │ — diff snapshot vs current → apply (mesh swap /          │   │
│  │   AnimatorOverride swap / MaterialPropertyBlock)         │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ CustomisationSave ([Serializable] — JsonUtility-friendly) │   │
│  │  — presetId : string                                       │   │
│  │  — heightScale : float                                     │   │
│  │  — bodyWeight : float                                      │   │
│  │  — skinColor : Color                                       │   │
│  │  — hairStyleId : string                                    │   │
│  │  — hairColor : Color                                       │   │
│  │  — clothingColorOverrides : SerializableDictionary         │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ CharacterSaveData (existing)                             │   │
│  │   + customisation : CustomisationSave   ← NEW (additive) │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. Enums (новые файлы)

### 2.1 `Assets/_Project/Scripts/Customisation/CharacterBodyType.cs`

```csharp
namespace ProjectC.Customisation
{
    /// <summary>
    /// Пол персонажа. Affects: base mesh (HumanM/HumanF), AnimatorOverrideController.
    /// Не влияет на: stats, skills, equipment pipeline, combat.
    /// </summary>
    public enum CharacterBodyType : byte
    {
        Male   = 0,
        Female = 1,
    }
}
```

### 2.2 `Assets/_Project/Scripts/Customisation/BodyPresetId.cs` (опционально, для L2+)

```csharp
namespace ProjectC.Customisation
{
    /// <summary>
    /// Пресет тела (для L2+). Зарезервировано на будущее — в L1 не используется,
    /// CustomisationSave.presetId = "male_default" / "female_default".
    /// </summary>
    public enum BodyPresetId : byte
    {
        Default     = 0,
        Athletic    = 1,
        Heavy       = 2,
        Slim        = 3,
        Elder       = 4,
        Young       = 5,
    }
}
```

### 2.3 `Assets/_Project/Scripts/Customisation/HairStyleId.cs` (для L4+)

```csharp
namespace ProjectC.Customisation
{
    /// <summary>
    /// Стиль волос. Hair mesh подцепляется к кости Head по аналогии с Equipment Visual.
    /// </summary>
    public enum HairStyleId : byte
    {
        Bald      = 0,
        Short     = 1,
        Medium    = 2,
        Long      = 3,
        Ponytail  = 4,
        Mohawk    = 5,
        Braids    = 6,
        // Будет расширяться по мере добавления mesh-ассетов
    }
}
```

---

## 3. CustomisationSave (DTO для persistence)

**Новый файл:** `Assets/_Project/Scripts/Customisation/CustomisationSave.cs`

```csharp
// Project C: Character Customisation — T-CUS-01
// CustomisationSave: JsonUtility-friendly DTO для persistence (additive в CharacterSaveData).
// Design: docs/Character/Customisation/02_DATA_MODEL.md §3
//
// Принципы:
//   - Все поля public (JsonUtility требует).
//   - Default values = Male preset (current behaviour, backward-compat).
//   - Color хранится как 4 float'а (Color.r/g/b/a) — JsonUtility не сериализует Color напрямую в некоторых версиях.
//   - Структура полей — плоская, БЕЗ Dictionary/array (JsonUtility-friendly). Для override colors —
//     SerializableStringColorPair array (см. §3.3).
//
// Backward-compat:
//   - Старые .json файлы БЕЗ секции "customisation" → JsonUtility создаёт default (Male, white colors).
//   - Это сохраняет существующее поведение для уже загруженных персонажей.

using System;
using UnityEngine;

namespace ProjectC.Customisation
{
    /// <summary>
    /// JsonUtility-friendly DTO для character customisation. Default = Male preset.
    /// </summary>
    [Serializable]
    public class CustomisationSave
    {
        // === Body ===

        [Tooltip("Тип тела. 0=Male (default), 1=Female.")]
        public CharacterBodyType bodyType = CharacterBodyType.Male;

        [Tooltip("Пресет тела (для L2+). 0=Default.")]
        public BodyPresetId presetId = BodyPresetId.Default;

        // === Proportions (L3 — слайдеры тела) ===

        [Range(0.8f, 1.2f)]
        [Tooltip("Общий масштаб по Y (рост). 1.0 = default. 0.9 = короче на 10%, 1.1 = выше на 10%.")]
        public float heightScale = 1.0f;

        [Range(0.7f, 1.3f)]
        [Tooltip("Масштаб по XZ (полнота/ширина). 1.0 = default. Влияет на ширину плеч, торса, ног.")]
        public float widthScale = 1.0f;

        // === Colors (L4 — покраска) ===

        [Tooltip("Цвет кожи (RGBA). Default = white (не окрашиваем, берётся material персонажа).")]
        public float skinColorR = 1.0f;
        public float skinColorG = 1.0f;
        public float skinColorB = 1.0f;
        public float skinColorA = 1.0f;

        [Tooltip("Цвет волос (RGBA).")]
        public float hairColorR = 0.4f;  // default dark brown
        public float hairColorG = 0.25f;
        public float hairColorB = 0.15f;
        public float hairColorA = 1.0f;

        // === Hair style (L4+) ===

        [Tooltip("Стиль волос. 0=Bald, 1=Short, ... См. HairStyleId.")]
        public HairStyleId hairStyle = HairStyleId.Short;

        // === Clothing color overrides (L4 — покраска экипировки) ===

        [Tooltip("Per-EquipSlot color override (макс. 13 элементов). Default = пусто (используется материал предмета).")]
        public SerializableStringColorPair[] clothingColorOverrides = Array.Empty<SerializableStringColorPair>();

        // === Helpers ===

        public Color GetSkinColor() => new Color(skinColorR, skinColorG, skinColorB, skinColorA);
        public Color GetHairColor() => new Color(hairColorR, hairColorG, hairColorB, hairColorA);

        public void SetSkinColor(Color c) { skinColorR = c.r; skinColorG = c.g; skinColorB = c.b; skinColorA = c.a; }
        public void SetHairColor(Color c) { hairColorR = c.r; hairColorG = c.g; hairColorB = c.b; hairColorA = c.a; }
    }

    /// <summary>
    /// Пара (ключ, цвет) для сериализации. Используется для clothingColorOverrides.
    /// Key = EquipSlot.ToString() (например "Head", "Chest", "WeaponMain").
    /// </summary>
    [Serializable]
    public struct SerializableStringColorPair
    {
        public string key;       // EquipSlot.ToString()
        public float colorR;
        public float colorG;
        public float colorB;
        public float colorA;

        public Color GetColor() => new Color(colorR, colorG, colorB, colorA);
        public void SetColor(Color c) { colorR = c.r; colorG = c.g; colorB = c.b; colorA = c.a; }

        public static SerializableStringColorPair From(string k, Color c) =>
            new SerializableStringColorPair { key = k, colorR = c.r, colorG = c.g, colorB = c.b, colorA = c.a };
    }
}
```

**Примечание по `clothingColorOverrides`:** используется строковый ключ (`EquipSlot.ToString()`) вместо enum-а, потому что:
- JsonUtility сериализует enum как int → при reorder/repurpose enum значения могут съехать.
- Строковый ключ stable, читаем в .json, миграция проще.

---

## 4. CustomisationSnapshotDto (network DTO)

**Новый файл:** `Assets/_Project/Scripts/Customisation/Dto/CustomisationSnapshotDto.cs`

```csharp
// Project C: Character Customisation — T-CUS-02
// CustomisationSnapshotDto: network DTO для replication.
// Pattern: parallel к EquipmentSnapshotDto (T-P09), но Compact (customisation маленький).
// Design: docs/Character/Customisation/02_DATA_MODEL.md §4

using System;
using UnityEngine;
using ProjectC.Equipment;  // EquipSlot

namespace ProjectC.Customisation.Dto
{
    /// <summary>
    /// Snapshot текущего customisation игрока. Server-replicated (если customisation replicated, см. 00_OVERVIEW §6).
    /// Compact: один struct с минимальным overhead.
    /// </summary>
    [Serializable]
    public struct CustomisationSnapshotDto
    {
        public CharacterBodyType bodyType;
        public BodyPresetId presetId;

        public float heightScale;
        public float widthScale;

        public float skinColorR, skinColorG, skinColorB, skinColorA;
        public float hairColorR, hairColorG, hairColorB, hairColorA;

        public HairStyleId hairStyle;

        // Per-slot color override (для L4)
        public ClothingColorOverrideDto[] clothingOverrides;

        public Color GetSkinColor() => new Color(skinColorR, skinColorG, skinColorB, skinColorA);
        public Color GetHairColor() => new Color(hairColorR, hairColorG, hairColorB, hairColorA);
    }

    /// <summary>
    /// Per-EquipSlot color override для экипировки. Mirror SerializableStringColorPair но в DTO.
    /// </summary>
    [Serializable]
    public struct ClothingColorOverrideDto
    {
        public EquipSlot slot;
        public float colorR, colorG, colorB, colorA;

        public Color GetColor() => new Color(colorR, colorG, colorB, colorA);
    }
}
```

**Зачем отдельный DTO от CustomisationSave:**
- `CustomisationSave` = persistence DTO (JsonUtility-friendly, default values explicit).
- `CustomisationSnapshotDto` = network DTO (compact, struct для минимизации аллокаций).
- Маппинг между ними — через extension methods в `CustomisationMappers.cs` (отдельный файл).

---

## 5. CustomisationClientState (singleton по аналогии с EquipmentClientState)

**Новый файл:** `Assets/_Project/Scripts/Customisation/CustomisationClientState.cs`

```csharp
// Project C: Character Customisation — T-CUS-03
// CustomisationClientState: client-side projection серверного state'а.
// Pattern: copy EquipmentClientState (T-P10).
// Trigger: server CustomisationServer.OnCustomisationSnapshotReceived → target RPC → client.
//
// Lifecycle:
//   - Singleton (Instance pattern, как EquipmentClientState).
//   - DontDestroyOnLoad по умолчанию.
//   - OnCustomisationUpdated event для UI и CharacterCustomisationApplier.

using System;
using ProjectC.Customisation.Dto;
using UnityEngine;

namespace ProjectC.Customisation
{
    public class CustomisationClientState : MonoBehaviour
    {
        public static CustomisationClientState Instance { get; private set; }

        [Header("Lifecycle")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        // === State ===
        public CustomisationSnapshotDto CurrentSnapshot { get; private set; }

        // === Events ===
        /// <summary>Новый snapshot пришёл. UI и CharacterCustomisationApplier подписываются.</summary>
        public event Action<CustomisationSnapshotDto> OnCustomisationUpdated;

        /// <summary>Запрос применён/отклонён (toast для UI).</summary>
        public event Action<CustomisationResultDto> OnCustomisationResult;

        // === Lifecycle ===
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // === Server → Client handlers ===
        // Вызываются из NetworkPlayer.ReceiveCustomisationSnapshotTargetRpc (T-CUS-04).

        public void OnCustomisationSnapshotReceived(CustomisationSnapshotDto snapshot)
        {
            CurrentSnapshot = snapshot;
            OnCustomisationUpdated?.Invoke(snapshot);
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[CustomisationClientState] Snapshot: body={snapshot.bodyType}, " +
                          $"h={snapshot.heightScale:F2}, w={snapshot.widthScale:F2}, " +
                          $"hair={snapshot.hairStyle}");
            }
        }

        public void OnCustomisationResultReceived(CustomisationResultDto result)
        {
            OnCustomisationResult?.Invoke(result);
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[CustomisationClientState] Result: ok={result.success}, msg='{result.message}'");
            }
        }

        /// <summary>Сброс state (для тестов / scene reload).</summary>
        public void ClearState() => CurrentSnapshot = default;
    }

    /// <summary>Результат CustomisationServer.RequestChange.</summary>
    [Serializable]
    public struct CustomisationResultDto
    {
        public bool success;
        public string message;
    }
}
```

---

## 6. CharacterCustomisationApplier (визуальный аппликатор)

**Новый файл:** `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs`

```csharp
// Project C: Character Customisation — T-CUS-05
// CharacterCustomisationApplier: применяет CustomisationSnapshotDto к визуалу персонажа.
// Pattern: copy CharacterEquipmentVisualApplier (Phase 2, 2026-06-29), но для customisation.
//
// Триггер: CustomisationClientState.OnCustomisationUpdated (T-CUS-03).
// Логика:
//   1. diff snapshot vs _currentSnapshot
//   2. Если bodyType изменился → mesh swap (M↔F)
//   3. Если controller-ы нужны → AnimatorOverrideController swap
//   4. Если heightScale/widthScale изменились → transform.localScale
//   5. Если skinColor/hairColor изменились → MaterialPropertyBlock
//   6. Если hairStyle изменился → spawn/destroy hair mesh (через Equipment Visual pattern)
//
// Additive-only: новый компонент на NetworkPlayer.prefab. Не модифицирует существующие.

using System.Collections.Generic;
using ProjectC.Customisation;
using ProjectC.Customisation.Dto;
using UnityEngine;

namespace ProjectC.Player
{
    [DisallowMultipleComponent]
    public class CharacterCustomisationApplier : MonoBehaviour
    {
        // === Inspector ===

        [Header("Refs")]
        [SerializeField] private Transform _visualRoot;      // Visual_Model child
        [SerializeField] private Animator _animator;        // Animator on Visual_Model
        [SerializeField] private SkinnedMeshRenderer _bodyRenderer;  // Основной SMR персонажа

        [Header("Override Controllers")]
        [Tooltip("AnimatorOverrideController для Male.")]
        [SerializeField] private RuntimeAnimatorController _maleController;
        [Tooltip("AnimatorOverrideController для Female.")]
        [SerializeField] private RuntimeAnimatorController _femaleController;

        [Header("Meshes")]
        [Tooltip("Mesh для Male (HumanM_Model.sharedMesh).")]
        [SerializeField] private Mesh _maleMesh;
        [Tooltip("Mesh для Female (HumanF_Model.sharedMesh).")]
        [SerializeField] private Mesh _femaleMesh;

        [Header("Hair meshes (L4)")]
        [SerializeField] private GameObject[] _hairStylePrefabs;  // индекс = HairStyleId

        [Header("Behavior")]
        [SerializeField] private bool _logWarnings = true;

        // === Runtime state ===

        private CustomisationSnapshotDto _currentSnapshot;
        private GameObject _spawnedHair;
        private MaterialPropertyBlock _mpb;
        private CustomisationClientState _clientState;

        // Shader property IDs (cache)
        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int _colorId     = Shader.PropertyToID("_Color");

        // === Lifecycle ===

        private void Awake()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_animator == null && _visualRoot != null)
                _animator = _visualRoot.GetComponentInChildren<Animator>(true);
            if (_bodyRenderer == null && _visualRoot != null)
                _bodyRenderer = _visualRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);
        }

        private void OnEnable()
        {
            _clientState = CustomisationClientState.Instance;
            if (_clientState == null)
            {
                if (_logWarnings)
                    Debug.LogWarning("[CharacterCustomisationApplier] CustomisationClientState.Instance == null — visual customisation skipped.");
                return;
            }
            _clientState.OnCustomisationUpdated += OnCustomisationUpdated;
            if (IsSnapshotValid(_clientState.CurrentSnapshot))
            {
                OnCustomisationUpdated(_clientState.CurrentSnapshot);
            }
        }

        private void OnDisable()
        {
            if (_clientState != null)
            {
                _clientState.OnCustomisationUpdated -= OnCustomisationUpdated;
                _clientState = null;
            }
            if (_spawnedHair != null) Destroy(_spawnedHair);
        }

        // === Snapshot handler ===

        private void OnCustomisationUpdated(CustomisationSnapshotDto snapshot)
        {
            if (_bodyRenderer == null || _animator == null) return;

            // 1. Body type (mesh + controller).
            if (_currentSnapshot.bodyType != snapshot.bodyType)
            {
                ApplyBodyType(snapshot.bodyType);
            }

            // 2. Proportions (transform scale).
            if (Mathf.Abs(_currentSnapshot.heightScale - snapshot.heightScale) > 0.001f ||
                Mathf.Abs(_currentSnapshot.widthScale  - snapshot.widthScale)  > 0.001f)
            {
                ApplyProportions(snapshot.heightScale, snapshot.widthScale);
            }

            // 3. Colors.
            if (ColorsDiffer(_currentSnapshot, snapshot))
            {
                ApplyColors(snapshot);
            }

            // 4. Hair style.
            if (_currentSnapshot.hairStyle != snapshot.hairStyle)
            {
                ApplyHair(snapshot.hairStyle, snapshot.GetHairColor());
            }

            _currentSnapshot = snapshot;
        }

        // === Apply methods (детали — в 04_MALE_FEMALE_SWAP.md и 03_LEVELS_OF_CUSTOMISATION.md) ===

        private void ApplyBodyType(CharacterBodyType bodyType) { /* см. §7 */ }
        private void ApplyProportions(float h, float w)         { /* см. §7 */ }
        private void ApplyColors(CustomisationSnapshotDto s)    { /* см. §7 */ }
        private void ApplyHair(HairStyleId style, Color color)  { /* см. §7 */ }

        private static bool IsSnapshotValid(CustomisationSnapshotDto s) => s.bodyType != default || s.hairStyle != default;
        private static bool ColorsDiffer(CustomisationSnapshotDto a, CustomisationSnapshotDto b) =>
            Mathf.Abs(a.skinColorR - b.skinColorR) > 0.001f || /* ... */ Mathf.Abs(a.hairColorA - b.hairColorA) > 0.001f;
    }
}
```

**Полный код методов — в `04_MALE_FEMALE_SWAP.md` §5 (для ApplyBodyType) и `03_LEVELS_OF_CUSTOMISATION.md` (для остальных).**

---

## 7. CharacterSaveData — additive-расширение

**Файл (existing):** `Assets/_Project/Scripts/Stats/Persistence/CharacterSaveData.cs`

**Изменение (минимальное):** добавить одно поле.

```csharp
// Project C: Character Customisation — T-CUS-01
// ADDITIVE: добавлено поле customisation. Default = new CustomisationSave() (Male).
// Backward-compat: старые .json без этого поля загружаются с default customisation.

using ProjectC.Customisation;  // NEW using

namespace ProjectC.Stats.Persistence
{
    [Serializable]
    public class CharacterSaveData
    {
        public PlayerStatsSave stats = new PlayerStatsSave();
        public EquipmentSave equipment = new EquipmentSave();
        public SkillsSave skills = new SkillsSave();
        // NEW: Customisation (additive). Старые файлы без этого поля загружаются с default = Male.
        public CustomisationSave customisation = new CustomisationSave();
    }

    // ... остальное без изменений
}
```

**Важно:** это **одно** изменение в одном файле. Никакой migration-логики не нужно — `JsonUtility.FromJson` сам создаст default.

---

## 8. Что НЕ идёт в data model

| Идея | Почему нет |
|---|---|
| Blend shapes в CustomisationSave | Kevin Iglesias FREE не имеет blend shapes. L3 потребует либо PRO-версию, либо отдельную mesh-morph подсистему (см. `03_LEVELS_OF_CUSTOMISATION.md` §3). |
| `Quaternion faceRotation` | Лицо не крутится — камера от третьего лица, видим всегда анфас. |
| `string[] unlockedPresets` | Unlock через gameplay — отдельная фича (post-MVP). По умолчанию все пресеты доступны. |
| `Dictionary<string, float>` в JsonUtility | JsonUtility **не** сериализует Dictionary. Используем массив `SerializableStringColorPair[]`. |
| `NetworkVariable<CustomisationSnapshotDto>` (опционально для L2) | Документировано в `00_OVERVIEW.md` §6 (Variant B). В L1 не делаем. |
| Отдельный `CustomisationServer` для каждой характеристики | Один `CustomisationServer` (опционально, см. roadmap) покрывает всё. |

---

## 9. Файловая структура после L1

```
Assets/_Project/Scripts/
├── Customisation/                              [NEW]
│   ├── CharacterBodyType.cs                    [NEW — enum]
│   ├── BodyPresetId.cs                         [NEW — enum]
│   ├── HairStyleId.cs                          [NEW — enum]
│   ├── CustomisationSave.cs                    [NEW — DTO persistence]
│   ├── CustomisationClientState.cs             [NEW — singleton]
│   └── Dto/
│       ├── CustomisationSnapshotDto.cs         [NEW — network DTO]
│       └── CustomisationResultDto.cs           [NEW — RPC ack DTO]
├── Player/
│   ├── NetworkPlayer.cs                        [+1 AddComponent для CharacterCustomisationApplier]
│   ├── CharacterEquipmentVisualApplier.cs      (без изменений)
│   └── CharacterCustomisationApplier.cs        [NEW — визуальный аппликатор]
└── Stats/
    └── Persistence/
        └── CharacterSaveData.cs                [+1 поле: customisation]

Assets/_Project/Animations/
├── PlayerAnimation.controller                  (без изменений)
├── PlayerAnimation_Default.overrideController  (без изменений)
└── PlayerAnimation_Female.overrideController   [NEW — F-клипы]

Assets/_Project/Resources/
└── Customisation/
    ├── HumanM_Avatar.asset                     [NEW — или ссылка на префаб]
    └── HumanF_Avatar.asset                     [NEW — или ссылка на префаб]
```

**Все новые файлы — additive. Никаких изменений в существующих data-типах, кроме одного поля в `CharacterSaveData`.**

---

## 10. Связь с другими документами

| Документ | Что взять |
|---|---|
| `00_OVERVIEW.md` | Архитектурные принципы |
| `01_CURRENT_CAPABILITIES.md` | Что в проекте уже есть (Animator Controller, Equipment Visual pattern, persistence) |
| `03_LEVELS_OF_CUSTOMISATION.md` | Детальное описание L1..L5, включая ApplyProportions / ApplyColors / ApplyHair |
| `04_MALE_FEMALE_SWAP.md` | Полный код `ApplyBodyType` (mesh swap + AnimatorOverrideController swap) |
| `05_PHASES_ROADMAP.md` | Тикеты T-CUS-01..T-CUS-08 — когда и в каком порядке создавать файлы |
| `docs/Character/EquipmentVisual/02_CHARACTER_APPLIER.md` | Паттерн визуального аппликатора |
| `docs/Character/EquipmentVisual/01_DATA_MODEL.md` | Паттерн additive-полей в ItemData |