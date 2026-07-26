# Occlusion Fade / Dither — Deep Dive

> **Файл:** `02_OCCLUSION_FADE.md`  
> **Цель:** Детальный обзор подходов к обработке объектов между камерой и персонажем  
> **Статус:** Research — анализ подходов  

---

## 1. Проблема

Когда между камерой и персонажем оказывается объект (столб, NPC, дерево, часть здания), он загораживает обзор. Игрок не видит своего персонажа — это **disorienting** (дезориентирует).

**Текущее состояние:** никакой обработки. Объекты просто закрывают персонажа.

**Требования:**
- Объект должен стать (полу)прозрачным, но не исчезать полностью
- Реакция должна быть быстрой (<0.2s)
- Не ломать sorting/rendering порядок
- Работать с URP 17.x

---

## 2. Подходы

### 2.1 Per-Object Alpha Fade (простейший)

**Как работает:**
1. Raycast от камеры к персонажу каждый кадр
2. Если hit — найти `Renderer` на объекте
3. Изменить `MaterialPropertyBlock._BaseColor.a` (или `_Alpha`)

**Плюсы:**
- ✅ Простая реализация
- ✅ Работает с URP Lit
- ✅ Нет шейдеров

**Минусы:**
- ❌ Нужно переключать `Surface Type` → Transparent (дорого для URP — пересортировка)
- ❌ Если у объекта несколько материалов — все надо менять
- ❌ Не работает с объектами без Renderer (terrain, VFX)
- ❌ Render queue меняется — могут быть артефакты сортировки
- ❌ На каждый кадр спам Raycast → CPU

**Вердикт:** ❌ **Не рекомендуется.** Слишком грязно для production.

### 2.2 Per-Object Shader Dither (средний)

**Как работает:**
1. Raycast от камеры к персонажу
2. Найти `Renderer` объекта
3. Через `MaterialPropertyBlock` установить `_DitherThreshold` (0 = непрозрачный, 1 = прозрачный)
4. Шейдер объекта использует clip/discard на основе threshold + screen-position noise

**Шейдерная часть (HLSL для URP):**

```hlsl
// Включить в Surface shader или Lit shader
// Использует screen-position noise для dither
float4 screenPos = ComputeScreenPos(positionCS);
float2 screenUV = screenPos.xy / screenPos.w;

// Dither pattern (8x8 Bayer matrix или симплексный шум)
float dither = Dither8x8(screenUV * _ScreenParams.xy, _DitherThreshold);

// Clip — пиксель либо полностью видим, либо полностью прозрачен
clip(dither - _DitherThreshold);
```

**Плюсы:**
- ✅ Не меняет Surface Type (остаётся Opaque)
- ✅ Нет пересортировки
- ✅ Визуально приятнее alpha fade (пиксели «растворяются»)
- ✅ Можно комбинировать с любым URP Lit шейдером

**Минусы:**
- ❌ Нужно модифицировать все шейдеры материалов, которые могут перекрывать персонажа
- ❌ Не работает с нативными шейдерами (TerrainLit, VFX)
- ❌ Dither может мерцать на некоторых поверхностях
- ❌ На каждый кадр спам Raycast → CPU

**Вердикт:** ⚠️ **Рискованно.** Хорошее visual quality, но требует изменения шейдеров сцены.

### 2.3 Screen-Space Occlusion (сложный) — РЕКОМЕНДУЕТСЯ

**Как работает:**
1. После рендера геометрии, до пост-эффектов
2. Рендерим персонажа в отдельный RenderTexture (Stencil или Depth mask)
3. Screen-space эффект: там где пиксели сцены перекрывают персонажа на глубине — делаем их прозрачными
4. Используем custom Renderer Feature для URP

**Архитектура URP Renderer Feature:**

```
BeforeRenderingPostProcessing
  ↓
OcclusionEffectPass
  ↓
  1. Вычислить, где на экране находится персонаж (bounds → screen rect)
  2. Для пикселей внутри rect: сравнить глубину объекта и персонажа
  3. Если объект ближе к камере → dither/fade
```

**Плюсы:**
- ✅ Работает с ЛЮБЫМИ объектами (независимо от шейдера)
- ✅ Работает с Terrain, VFX, деревьями, частицами
- ✅ Не требует изменения материалов сцены
- ✅ Один центральный шейдер

**Минусы:**
- ❌ Сложнее реализация (Custom Renderer Feature + CommandBuffer)
- ❌ CPU/GPU overhead дополнительного прохода
- ❌ Screen-space артефакты на границах объектов
- ❌ Нужно знать bounding box персонажа для оптимизации

**Вердикт:** ✅ **Рекомендовано для Project C.** Совместимость с URP 17.x, не требует изменения существующих шейдеров сцены.

### 2.4 Сравнение подходов

| Критерий | Per-Object Alpha | Per-Object Dither | Screen-Space |
|----------|-----------------|-------------------|--------------|
| Сложность | 🟢 Low | 🟡 Medium | 🔴 High |
| CPU impact | ❌ Raycast per frame | ❌ Raycast per frame | 🟢 Один проход |
| GPU impact | 🟡 Light | 🟢 Light (clip) | 🟡 Medium |
| Совместимость | ❌ Только с URP Lit | ⚠️ Модифицировать шейдеры | ✅ Любые объекты |
| Visual Quality | 🟡 Mediocre | 🟢 Good | 🟢 Very Good |
| Артефакты | Render queue | Dither noise | Edge artifacts |
| Время реализации | 1 день | 2-3 дня | 4-5 дней |

---

## 3. Screen-Space Dither — детальный дизайн

### 3.1 Когда срабатывает

Не все объекты надо дизерить. Только те, что находятся **между камерой и персонажем** и **вблизи центра экрана**.

```csharp
bool ShouldCheckOcclusion()
{
    // 1. Персонаж виден на экране? (не за пределами камеры)
    Vector3 viewportPos = _camera.WorldToViewportPoint(target.position);
    bool onScreen = viewportPos.x > 0f && viewportPos.x < 1f
                 && viewportPos.y > 0f && viewportPos.y < 1f
                 && viewportPos.z > 0f;  // перед камерой
    
    if (!onScreen) return false;
    
    // 2. Дистанция до target разумная? (не проверяем если target слишком далеко)
    float dist = Vector3.Distance(transform.position, target.position);
    if (dist > _maxOcclusionCheckDist) return false;  // max = 30m
    
    return true;
}
```

### 3.2 URP Renderer Feature

```csharp
// OcclusionDitherFeature.cs
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class OcclusionDitherFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public LayerMask occlusionMask = -1;
        public Material ditherMaterial;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        public float fadeSpeed = 5f;
    }

    public Settings settings = new Settings();
    private OcclusionDitherPass _pass;

    public override void Create()
    {
        _pass = new OcclusionDitherPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, 
                                          ref RenderingData renderingData)
    {
        if (settings.ditherMaterial == null) return;
        renderer.EnqueuePass(_pass);
    }
}
```

### 3.3 Dither Shader (FullScreen)

```hlsl
// OcclusionDither.shader
Shader "Hidden/ProjectC/OcclusionDither"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DitherAmount ("Dither Amount", Range(0, 1)) = 0
        _PlayerDepth ("Player Depth", Float) = 0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };
            
            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            
            float _DitherAmount;
            float _PlayerDepth;  // Depth of player center in screen space
            
            // 8x8 Bayer matrix for dithering
            static const float bayer8x8[64] = {
                0, 32, 8, 40, 2, 34, 10, 42,
                48, 16, 56, 24, 50, 18, 58, 26,
                12, 44, 4, 36, 14, 46, 6, 38,
                60, 28, 52, 20, 62, 30, 54, 22,
                3, 35, 11, 43, 1, 33, 9, 41,
                51, 19, 59, 27, 49, 17, 57, 25,
                15, 47, 7, 39, 13, 45, 5, 37,
                63, 31, 55, 23, 61, 29, 53, 21
            };
            
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }
            
            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.uv;
                
                // Sample scene color
                half4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, screenUV);
                
                // Sample scene depth
                float sceneDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, 
                    sampler_CameraDepthTexture, screenUV).r;
                
                // Linearize depth
                float sceneDepthLinear = LinearEyeDepth(sceneDepth, _ZBufferParams);
                float playerDepthLinear = LinearEyeDepth(_PlayerDepth, _ZBufferParams);
                
                // Check if this pixel is between camera and player
                if (sceneDepthLinear < playerDepthLinear - 0.1f)
                {
                    // This pixel is in front of the player → dither it
                    float2 screenPixel = screenUV * _ScreenParams.xy;
                    int bayerIdx = (int(screenPixel.x) % 8) + (int(screenPixel.y) % 8) * 8;
                    float threshold = bayer8x8[bayerIdx] / 64.0f;
                    
                    if (threshold < _DitherAmount)
                    {
                        // Discard pixel (transparent)
                        discard;
                    }
                }
                
                return color;
            }
            ENDHLSL
        }
    }
}
```

### 3.4 C# компонент — CameraOcclusionController

```csharp
public class CameraOcclusionController : MonoBehaviour
{
    [Header("Occlusion Settings")]
    [SerializeField] private Transform _target;           // персонаж
    [SerializeField] private float _fadeSpeed = 5f;       // скорость dither
    [SerializeField] private float _maxCheckDistance = 30f;
    [SerializeField] private LayerMask _occlusionMask = -1;
    
    private Camera _camera;
    private Material _ditherMaterial;
    private float _currentDitherAmount;
    private bool _wasOccluded;
    
    private void Awake()
    {
        _camera = GetComponent<Camera>();
        
        // Создаём material для шейдера
        _ditherMaterial = new Material(Shader.Find("Hidden/ProjectC/OcclusionDither"));
    }
    
    private void LateUpdate()
    {
        if (_target == null || _ditherMaterial == null) return;
        
        bool occluded = CheckOcclusion();
        
        if (occluded)
        {
            _currentDitherAmount = Mathf.MoveTowards(
                _currentDitherAmount, 1f, _fadeSpeed * Time.deltaTime);
        }
        else
        {
            _currentDitherAmount = Mathf.MoveTowards(
                _currentDitherAmount, 0f, _fadeSpeed * Time.deltaTime);
        }
        
        // Update shader parameters
        _ditherMaterial.SetFloat("_DitherAmount", _currentDitherAmount);
        _ditherMaterial.SetFloat("_PlayerDepth", GetPlayerDepth());
    }
    
    private bool CheckOcclusion()
    {
        // Проверка видимости персонажа
        Vector3 viewportPos = _camera.WorldToViewportPoint(_target.position);
        if (viewportPos.z < 0 || viewportPos.z > _maxCheckDistance)
            return false;
        if (viewportPos.x < 0 || viewportPos.x > 1 || 
            viewportPos.y < 0 || viewportPos.y > 1)
            return false;
        
        // Raycast от камеры к персонажу
        Vector3 dir = _target.position - transform.position;
        float dist = dir.magnitude;
        
        if (Physics.Raycast(transform.position, dir.normalized, 
                           out RaycastHit hit, dist, _occlusionMask))
        {
            if (hit.transform != _target)
                return true;  // Есть объект между камерой и персонажем
        }
        
        return false;
    }
    
    private float GetPlayerDepth()
    {
        // Convert player world position to depth
        Vector4 playerViewPos = _camera.worldToCameraMatrix * 
                                new Vector4(_target.position.x, 
                                           _target.position.y, 
                                           _target.position.z, 1);
        return -playerViewPos.z;  // Depth in view space
    }
}
```

---

## 4. Альтернатива: упрощённый подход (Phase 2)

Если screen-space подход слишком сложен для первой итерации, можно сделать **минимальный occlusion fade** через FindObjectsWithTag:

```csharp
// Минимальный подход: только для объектов с тегом "OcclusionFade"
private HashSet<Renderer> _fadedObjects = new HashSet<Renderer>();

private void HandleOcclusionFade()
{
    if (!_useOcclusionFade || target == null) return;
    
    Vector3 cameraPos = transform.position;
    Vector3 targetPos = target.position + Vector3.up * _lookAtHeight;
    Vector3 direction = targetPos - cameraPos;
    float distance = direction.magnitude;
    
    // RaycastAll собирает ВСЕ объекты на пути
    RaycastHit[] hits = Physics.RaycastAll(cameraPos, direction.normalized, 
                                            distance, _occlusionLayers);
    
    HashSet<Renderer> currentFaded = new HashSet<Renderer>();
    
    foreach (var hit in hits)
    {
        if (hit.transform == target) continue;
        
        var renderer = hit.collider.GetComponentInChildren<Renderer>();
        if (renderer != null && hit.collider.CompareTag("OcclusionFade"))
        {
            currentFaded.Add(renderer);
            
            if (!_fadedObjects.Contains(renderer))
            {
                StartCoroutine(FadeRenderer(renderer, 1f, _occlusionFadeTime));
            }
        }
    }
    
    // Восстанавливаем объекты, которые больше не в луче
    foreach (var renderer in _fadedObjects)
    {
        if (!currentFaded.Contains(renderer))
        {
            StartCoroutine(FadeRenderer(renderer, 0f, _occlusionFadeTime));
        }
    }
    
    _fadedObjects = currentFaded;
}

private IEnumerator FadeRenderer(Renderer renderer, float targetAlpha, float duration)
{
    float startTime = Time.time;
    float startAlpha = renderer.material.color.a;
    
    while (Time.time < startTime + duration)
    {
        float t = (Time.time - startTime) / duration;
        float alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
        
        Color color = renderer.material.color;
        color.a = alpha;
        renderer.material.color = color;
        
        yield return null;
    }
}
```

**Минусы упрощённого подхода:**
- ❌ `RaycastAll` каждый кадр — дорогой
- ❌ Тег `"OcclusionFade"` надо вешать на каждый объект
- ❌ Alpha fade ломает render queue (нужен Transparent)
- ❌ Coroutine для каждого объекта — overhead

---

## 5. Режимы: когда occlusion включён/выключен

```csharp
public enum OcclusionMode
{
    AlwaysOn,       // Всегда проверять (walk по умолчанию)
    ShipOnly,       // Только в режиме корабля
    Off             // Отключено (для производительности)
}
```

**Рекомендация для Project C:**
- **Walk mode:** AlwaysOn — персонаж маленький, легко загораживается
- **Ship mode:** ShipOnly (или Off) — корабль большой, редко загораживается, occlusion только для очень близких объектов

---

## 6. Производительность

| Компонент | Cost | Примечание |
|-----------|------|------------|
| Raycast (проверка occlusion) | 🟢 <0.01ms | Один Physics.Raycast |
| RaycastAll (упрощённый подход) | 🟡 0.02-0.1ms | Чем больше объектов, тем дороже |
| Screen-space pass (fullscreen) | 🟡 0.1-0.3ms | Зависит от разрешения |
| Per-object MaterialPropertyBlock | 🟢 <0.01ms | Только при изменении |
| Shader clip/discard | 🟡 Medium | Может быть дорог на tile-based GPU (Mobile) — но у нас Standalone |

**Оптимизации:**
- Проверять occlusion не каждый кадр, а каждый 2-3 кадр
- Кэшировать `_DitherAmount` — не обновлять если не изменился
- Screen-space подход: ограничить регион проверки до bounding box персонажа

---

## 7. План реализации

| Step | Что делать | Время |
|------|-----------|-------|
| 1 | Реализовать `CameraOcclusionController` с Raycast (проверка occlusion) | 0.5 дня |
| 2 | Создать URP Renderer Feature + dither shader | 2 дня |
| 3 | Настроить LayerMask для occlusion (Default, Terrain, Static) | 0.5 дня |
| 4 | Протестировать: NPC, деревья, столбы, здания | 0.5 дня |
| 5 | Протестировать производительность (Frame Debugger) | 0.5 дня |
| 6 | (Опционально) Доработать dither noise pattern | 0.5 дня |

**Всего:** ~4 дня
