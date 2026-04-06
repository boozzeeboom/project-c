# Unity 6 + URP — Краткий справочник

**Дата:** 6 апреля 2026 г. | **Unity версия:** 6000.4.1f1 | **URP:** 17.4.0

---

## 🔥 КРИТИЧНО: Как настроить URP в Unity 6

### Проблема: скрипты не компилируются
Если вы создаёте `.cs` скрипт с `using UnityEngine.Rendering.Universal` **до** настройки URP — Unity выдаст ошибку компиляции `CS0246: The type or namespace name 'UniversalRenderPipelineAsset' could not be found`. Это потому что URP-ассембли ещё не загружены.

### Правильный порядок действий:

#### Шаг 1: Установка пакета
URP должен быть в `Packages/manifest.json`:
```json
"com.unity.render-pipelines.universal": "17.4.0"
```

#### Шаг 2: Создание Pipeline Asset (БЕЗ СКРИПТОВ)
1. **Правой кнопкой в Project окне** → папка `Assets/Settings/` (или любая другая)
2. **Create → Rendering → URP Pipeline Asset**
3. Назовите: `ProjectC_URP`
4. Появятся 2 файла:
   - `ProjectC_URP` — Pipeline Asset
   - `ProjectC_URP_Renderer` — Universal Renderer Data

#### Шаг 3: Активация
1. **Edit → Project Settings → Graphics**
2. Поле **Default Render Pipeline** — перетащите `ProjectC_URP`
3. **Edit → Render Pipeline → Universal Render Pipeline → Upgrade Project Materials to URP Materials**
4. Нажмите **Upgrade**

#### Шаг 4: Проверка
- В Console не должно быть красных ошибок
- CloudGhibli.shader должен скомпилироваться
- В `Edit → Project Settings → Graphics` поле Default Render Pipeline показывает `ProjectC_URP`

### ❌ Что НЕ работает:
- Создавать URP ассеты через C# скрипты (Editor scripts) — API в Unity 6 сильно отличается
- `ForwardRendererData` → переименован в `UniversalRendererData` (с URP 14+)
- Писать YAML-файлы вручную для Pipeline Asset — Unity их не распознаёт
- `ScriptableRenderer` как generic type для `AssetDatabase.LoadAssetAtPath<T>` — не является `UnityEngine.Object`
- `pipelineAsset.rendererDataList = ...` — это readonly поле, нельзя напрямую присвоить
- `GraphicsSettings.renderPipelineAsset` → это read-only свойство, не сеттер

### ✅ Что работает:
- Через редактор: Create → Rendering → URP Pipeline Asset
- `GraphicsSettings.defaultRenderPipeline = pipelineAsset` — установка pipeline
- `AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset")` — поиск существующих
- SerializedObject для настройки свойств Pipeline Asset
- `ScriptableObject.CreateInstance<UniversalRendererData>()` — создание Renderer

---

## 📦 Версии пакетов (Unity 6 / URP 17)

| Пакет | Версия | Примечание |
|-------|--------|-----------|
| `com.unity.render-pipelines.universal` | 17.4.0 | URP для Unity 6 |
| `com.unity.render-pipelines.core` | 17.4.0 | Базовый SRP |
| `com.unity.shadergraph` | 17.4.0 | Shader Graph |
| `com.unity.burst` | 1.8.28 | Burst compiler |
| `com.unity.mathematics` | 1.3.3 | Math library |
| `com.unity.collections` | 6.4.0 | Native collections |

---

## 🏗️ Классы и пространства имён

### Основные классы

| Класс | Namespace | Примечание |
|-------|-----------|-----------|
| `UniversalRenderPipelineAsset` | `UnityEngine.Rendering.Universal` | Pipeline Asset |
| `UniversalRendererData` | `UnityEngine.Rendering.Universal` | Renderer (бывший ForwardRendererData) |
| `ScriptableRenderer` | `UnityEngine.Rendering.Universal` | Базовый класс рендерера |
| `ScriptableRendererData` | `UnityEngine.Rendering.Universal` | Данные рендерера |
| `GraphicsSettings.defaultRenderPipeline` | `UnityEngine.Rendering` | Глобальный pipeline |
| `GraphicsSettings.currentRenderPipeline` | `UnityEngine.Rendering` | Активный pipeline (с учётом QualitySettings) |

### Editor-классы

| Класс | Namespace | Примечание |
|-------|-----------|-----------|
| `UniversalRenderPipelineAssetEditor` | `UnityEditor.Rendering.Universal` | Инспектор Pipeline |
| `UniversalRendererDataEditor` | `UnityEditor.Rendering.Universal` | Инспектор Renderer |

### ⚠️ Breaking Changes в URP 17

| Старое API | Новое API | Примечание |
|-----------|-----------|-----------|
| `ScriptableRenderer.cameraColorTarget` | `cameraColorTargetHandle` | Render Handles |
| `ScriptableRenderer.cameraDepthTarget` | `cameraDepthTargetHandle` | Render Handles |
| `ForwardRendererData` | `UniversalRendererData` | Переименован в URP 14+ |
| `RenderPasses.AddRenderPasses` | `SetupRenderPasses` | Инициализация перенесена |

### Render Graph

- Разработка кастомных Render Passes вне Render Graph **прекращена**
- Нужен переход на Render Graph API или включение **Compatibility Mode**
- `ClearFlag.Depth` больше неявно не очищает Stencil — используйте `ClearFlag.Stencil`

---

## 🔧 Сериализуемые свойства

### UniversalRendererData (SerializedObject)

| Свойство | Тип | Описание |
|----------|-----|----------|
| `m_MSAA` | int | MSAASamples: 1=Off, 2=2x, 4=4x, 8=8x |
| `m_RenderScale` | float | Render scale (1.0 = full) |
| `m_IntermediateTextureSize` | int | Auto=1, Max=2 |
| `m_RendererFeatures` | List | Render Features |
| `m_TransparentSortMode` | TransparentSortMode | Сортировка прозрачных |

### UniversalRenderPipelineAsset (SerializedObject)

| Свойство | Тип | Описание |
|----------|-----|----------|
| `m_RendererDataList` | ScriptableRendererData[] | Список рендереров (READONLY через код) |
| `m_RendererType` | int | Тип рендерера |
| `m_MSAA` | int | MSAASamples |
| `m_RenderScale` | float | Render scale |
| `m_SupportsHDR` | bool | HDR поддержка |
| `m_HDRColorBufferPrecision` | int | HDR precision |
| `m_RequireDepthTexture` | bool | Нужна depth texture |
| `m_RequireOpaqueTexture` | bool | Нужна opaque texture |
| `m_OpaqueDownsampling` | int | Opaque downsampling |
| `m_AntiAliasing` | int | Anti-aliasing mode |
| `m_SupportsTerrainHoles` | bool | Terrain holes |
| `m_ShaderVariantLogLevel` | int | Лог компиляции шейдеров |
| `m_UpscalingFilter` | int | Фильтр апскейлинга |
| `m_LightProbeSystem` | int | Light probe system |

### Пример использования SerializedObject

```csharp
// Создание Pipeline Asset через Editor Script
var pipelineAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
pipelineAsset.name = "CustomURPPipeline";

var pipelineSO = new SerializedObject(pipelineAsset);
pipelineSO.FindProperty("m_RendererDataList").arraySize = 1;
pipelineSO.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
pipelineSO.FindProperty("m_MSAA").intValue = 1; // Off
pipelineSO.FindProperty("m_SupportsHDR").boolValue = true;
pipelineSO.ApplyModifiedProperties();

string pipelinePath = "Assets/Settings/CustomURPPipeline.asset";
AssetDatabase.CreateAsset(pipelineAsset, pipelinePath);
```

---

## 🎨 Шейдеры

### URP-совместимые шейдеры

| Шейдер | Путь в Unity |
|--------|-------------|
| Lit (PBR) | `Universal Render Pipeline/Lit` |
| Unlit | `Universal Render Pipeline/Unlit` |
| Particles/Lit | `Universal Render Pipeline/Particles/Lit` |
| Particles/Unlit | `Universal Render Pipeline/Particles/Unlit` |
| Post Processing | `Universal Render Pipeline/Post Processing` |

### Кастомные URP шейдеры

**Include файлы:**
```hlsl
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
```

**SubShader Tags:**
```hlsl
Tags
{
    "RenderPipeline" = "UniversalPipeline"
    "RenderType" = "Opaque" // или "Transparent"
    "Queue" = "Geometry"    // или "Transparent"
}
```

**Для прозрачных шейдеров:**
```hlsl
Blend SrcAlpha OneMinusSrcAlpha
ZWrite Off
Cull Off // или Back/Front
```

---

##  Quality Settings

**Важно:** `QualitySettings.renderPipeline` имеет приоритет над `GraphicsSettings.defaultRenderPipeline`.

```csharp
// Проверка текущего pipeline
var current = GraphicsSettings.currentRenderPipeline;

// Установка глобального (будет перезаписан QualitySettings)
GraphicsSettings.defaultRenderPipeline = pipelineAsset;

// Установка для конкретного Quality Level
QualitySettings.GetQualityLevel(); // текущий уровень
// В Unity Editor: Edit → Project Settings → Quality
// Каждый Quality Level может иметь свой Render Pipeline Asset override
```

---

## 🔄 Конвертация материалов

### Через меню Unity
**Edit → Render Pipeline → Universal Render Pipeline → Upgrade Project Materials to URP Materials**

Это автоматически:
- Заменяет `Standard` → `Universal Render Pipeline/Lit`
- Заменяет `Standard (Specular setup)` → `Universal Render Pipeline/Lit`
- Конвертирует свойства материалов (Metallic, Smoothness, etc.)
- Обновляет шейдеры частиц

### Программная конвертация
```csharp
// Ищем все материалы со Standard шейдером
var guids = AssetDatabase.FindAssets("t:Material");
foreach (var guid in guids)
{
    var path = AssetDatabase.GUIDToAssetPath(guid);
    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
    
    if (mat.shader.name == "Standard")
    {
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        EditorUtility.SetDirty(mat);
    }
}
AssetDatabase.SaveAssets();
```

---

## 📋 Чек-лист настройки URP

- [ ] URP пакет установлен (`com.unity.render-pipelines.universal`)
- [ ] Pipeline Asset создан (Create → Rendering → URP Pipeline Asset)
- [ ] Pipeline Asset назначен в Graphics Settings
- [ ] Материалы сконвертированы (Upgrade Project Materials to URP Materials)
- [ ] Кастомные шейдеры обновлены (`Standard` → `Universal Render Pipeline/Lit`)
- [ ] CloudGhibli.shader компилируется (находит URP includes)
- [ ] Нет красных ошибок в Console
- [ ] Quality Level не переопределяет Pipeline Asset

---

## 🐛 Частые ошибки и решения

| Ошибка | Причина | Решение |
|--------|---------|---------|
| `CS0246: UniversalRenderPipelineAsset` | URP не установлен или скрипт компилируется до загрузки URP | Удалить скрипт, настроить URP через редактор, потом создать скрипт |
| `Core.hlsl not found` | URP pipeline asset не назначен | Назначить Pipeline Asset в Graphics Settings |
| `ForwardRendererData not found` | Переименован в URP 14+ | Использовать `UniversalRendererData` |
| Материалы розовые | Standard шейдер не работает в URP | Upgrade Project Materials to URP Materials |
| Pipeline Asset не сохраняется | YAML создан вручную | Создать через Create → Rendering → URP Pipeline Asset |
| `rendererDataList` readonly | Нельзя напрямую присвоить | Использовать SerializedObject |

---

## 🔗 Полезные ссылки

- [Unity 6 Manual](https://docs.unity3d.com/6000.4/Documentation/Manual/index.html)
- [URP Documentation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.4/manual/index.html)
- [Shader Graph Documentation](https://docs.unity3d.com/Packages/com.unity.shadergraph@17.4/manual/index.html)
- [URP Breaking Changes](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.4/changelog/CHANGELOG.html)

---

**Последнее обновление:** 6 апреля 2026 г.
**Автор:** Qwen Code
