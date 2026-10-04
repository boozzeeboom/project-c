# SteelGraphV2 — сталь v2 (Shader Graph + 3D-шум)

> Статус: **PROTOTYPE, compile OK, 18 свойств материала, visual NOT RUN.**
> Ассеты: `Assets/_Project/Materials/Steel/Steel_v2.shadergraph`
> (шейдер `Shader Graphs/Steel_v2`), `Assets/_Project/Materials/Steel/M_PC_Steel_v2.mat`
> и ядро шума `Assets/_Project/Shaders/ProceduralSteelNoise.hlsl`.
> Компиляция: `supported=True`, `ShaderHasError=false`, 0 warnings
> (04.10.2026, anklebreaker MCP). Визуал и F8/F9 — за пользователем.

## 1. Что это

Сталь v2 в Shader Graph. Ядро шума — **Custom Function `ProceduralSteelNoise`**
(настоящий **object-space 3D** fbm + Voronoi + domain warp), остальное —
нодовая логика (маски, кромки, цвета, металлик/гладкость). Это даёт корректный
шум **на всех 6 гранях** (без растяжения, как у 2D UV-нод) и хаотичность.

## 2. Свойства материала (Blackboard, 18)

| Свойство | Тип | Дефолт | Что крутит |
|---|---|---|---|
| `_Steel_Color` | Color | 0.42 / 0.45 / 0.48 | тон чистого металла |
| `_Transition_Color` | Color | 0.30 / 0.16 / 0.08 | переходная (оксидная) полоса |
| `_Rust_Color` | Color | 0.36 / 0.15 / 0.06 | ржавчина |
| `_Worn_Color` | Color | 0.62 / 0.66 / 0.72 | зачищенный/потёртый металл |
| `_Metallic` | Float | 0.95 | металличность чистых зон |
| `_Smoothness` | Float | 0.55 | гладкость чистого металла |
| `_Rust_Smoothness` | Float | 0.12 | гладкость ржавчины |
| `_Worn_Smoothness` | Float | 0.85 | гладкость зачищенных зон |
| `_Noise_Scale` | Float | 6.0 | масштаб шума (крупность пятен) |
| `_Noise_Detail` | Float | 2.7 | множитель частоты между октавами fbm |
| `_Noise_Cell` | Float | 2.2 | масштаб Voronoi-ячеек |
| `_Noise_Warp` | Float | 0.35 | сила domain warp (искажение → хаос) |
| `_Noise_Seed` | Float | 0.0 | сдвиг паттерна (уникализация объекта) |
| `_Rust_Amount` | Float | 1.0 | количество ржавчины |
| `_Rust_Threshold` | Float | 0.58 | с какого значения шума начинается ржавчина |
| `_Transition_Threshold` | Float | 0.40 | с какого значения начинается переход |
| `_Wear_Amount` | Float | 1.0 | сила потёртостей (шумовых) |
| `_Edge_Width` | Float | 0.13 | ширина кромочной полосы |

Примечание: инструменты anklebreaker не создают blackboard-свойства и слоты
Custom Function — они добавлены в ассет графа, после чего Property-ноды и связи
сделаны через graph-инструменты.

## 3. Ядро шума (`Assets/_Project/Shaders/ProceduralSteelNoise.hlsl`)

Custom Function `ProceduralSteelNoise` (режим **File** → `#include` .hlsl, где
можно объявлять helper-функции). Входы: `ObjectPos` (float3, из Position=Object),
`Scale`, `Detail`, `Cell`, `Warp`, `Seed`. Выход: `Noise` (float 0–1).

```
p   = ObjectPos * Scale + Seed;                       // object-space, FO-safe
w   = valueNoise3(p*0.5 + const) - 0.5;               // ГЛАДКИЙ warp (интерп.)
p  += w * Warp;
fbm = Σ 3 октавы valueNoise3(p * freq), freq *= Detail;
vor = voronoi3(p * Cell);                             // 27 ячеек
Noise = saturate(lerp(fbm, vor, 0.30));
```

Ключевые детали (исправление артефактов):
- **Хеш без `sin`** (`PCSteel_Hash13`, Hoskins-style) — `frac(sin(...)*43758)`
  на части GPU даёт регулярные «тайлы».
- **Гладкий domain warp** (интерполированный value-noise), а не `floor()`-хеш:
  постоянный внутри ячейки warp давал швы-«сетку» по границам ячеек.
- `valueNoise3` — трилинейная интерполяция 8 углов; `voronoi3` — минимум
  расстояния до 27 соседних ячеек. Всё в object-space → после F8 паттерн стоит,
  на кубе нет растяжения по граням.

## 4. Логика поверх шума (ноды)

```
- Потёртости (worn): Smoothstep(0.42 → 0.22, инверт.) × _Wear_Amount.
- Переход:           Smoothstep(_Transition_Threshold, +0.20, Noise).
- Ржавчина:          Smoothstep(_Rust_Threshold, +0.16, Noise).
- Кромки: |ObjectPos| → Smoothstep(0.5 − _Edge_Width, 0.5) → edgeMask;
  rustF = rustCore × (1 − edgeMask) × _Rust_Amount; wear = saturate(edgeMask + worn).
- Цвет: Steel → Transition → Rust → Worn (Lerp-цепочка).
- Metallic = Lerp(0, _Metallic, 1 − rustF).
- Smoothness = Lerp(_Smoothness, _Rust_Smoothness, rustF) →
  Lerp(→ _Worn_Smoothness, wear × (1 − rustF)).
```

## 5. Производительность (оценка)

Ядро тяжёлое: 3 октавы fbm (≈24 `sin`-хеша) + Voronoi 3×3×3 (27 ячеек × 3 хеша
≈81 `sin`) на пиксель. Для прототипа/героев рядом — приемлемо; для массовых
объектов/далёких LOD нужен `Far`-вариант (1 октава, без Voronoi) — TODO.

## 6. Floating Origin

Только `Position (Object)`; `AbsoluteWorld` не используется. F8/F9 — шум стоит.

## 7. Проверка

- Compile: `Shader Graphs/Steel_v2` — supported, `ShaderHasError=false`,
  0 warnings; 23 свойства шейдера (18 пользовательских). ✅
- Console после чистого Refresh+reimport: 0 ошибок/варнингов. ✅
- Visual: **NOT RUN** (агент Play Mode/скриншоты не делает).
- Manual (пользователь):
  1. Unit-куб с `M_PC_Steel_v2`; свет спереди + небо/reflection probe.
  2. Ожидание: серый металл, хаотичные ржавые пятна, светлые зачищенные кромки
     и потёртости; на всех 6 гранях шум одинаково «живой», без полос-растяжений.
  3. Крутить: `_Noise_Scale`, `_Noise_Warp`, `_Rust_Threshold`,
     `_Transition_Threshold`, `_Wear_Amount`, `_Rust_Amount`, `_Edge_Width`, цвета.
  4. F8 → объект/шум на месте, 0 errors; F9 → возврат.

## 8. TODO

- `TODO-PERF`: Near/Far вариант (Far: 1 октава, без Voronoi, без warp).
- `TODO-NORMAL`: NormalTS от шума (рельеф потёртостей/ямок).
- `TODO-CALIB`: кромочные пороги под гейм-меши (не только unit-куб).
- `TODO-VISUAL`: визуальная приёмка и подстройка градиента ржавчины.
