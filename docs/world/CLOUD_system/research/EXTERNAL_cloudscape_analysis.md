# Cloudscape (Fishypants) — технический разбор исходника `index.html`

Источник: `C:\Users\leon7\AppData\Local\Temp\opencode\cloudscape-research\index.html`
WebGL2 / GLSL ES 3.00, один файл, ~2167 строк, ~107 КБ. Единицы измерения — **километры**; центр планеты в `(0, -Rg, 0)`, `Rg = 6360`, `Rt = 6460` (строки 49–50, 202–203).

> Всё ниже — только прочитанный код. Там, где код не даёт числа, прямо указано «в коде не замеряется / не найдено».

---

## 1. Общая архитектура рендера

### 1.1 Пайплайн кадра

Описание в шапке файла — строки 51–58; фактический порядок — `render()`, строки 1928–2156.

| # | Pass | Разрешение / размер | Формат | Функция / строка | Когда выполняется |
|---|---|---|---|---|---|
| 0 | Бейк 3D-шума `shape` | 128³ + mips | RGBA8 | `bakeNoise()` 1736–1743, `FS_SHAPE` 597 | старт + смена `shapeNoise` |
| 0b | Бейк 3D-шума `detail` | 64³ + mips | RGBA8 | там же, `FS_DETAIL` 612 | старт + смена `detailNoise` |
| 0c | Бейк 3D `foliage` | 64³ + mips | RGBA8 | `FS_FOLIAGE` 892, 1750 | только старт |
| 0d | Weather map | 512² + mips | RGBA8 | `FS_WEATHER` 623, 1751–1753 | только старт |
| 0e | Curl noise | 128² | **RGBA16F** | `FS_CURL` 638, 1754–1755 | только старт |
| 1 | Transmittance LUT | **256×64** | RGBA16F | `FS_TRANS` 655, 2036 | только при изменении `haze` |
| 2 | Multiple-scattering LUT | **32×32** | RGBA16F | `FS_MS` 679, 2036 | только при изменении `haze` |
| 3 | Sky irradiance | **2×1** | RGBA16F (NEAREST) | `FS_IRR` 724, 2037 | **каждый кадр** (зависит от солнца) |
| 4 | Forest map (height/mask/normal) | 1024² | RGBA16F | `FS_FORESTMAP` 878, 2040–2042 | при смене снэпа камеры 0.5 км или настроек леса |
| 5 | 3 каскада теней + 2 копии | 5 × 2048² | DEPTH_COMPONENT24 (array) | `PR.tshadow`, 2043–2073 | кэшируются по солнцу/снэпу/настройкам деревьев |
| 6 | Cloud sun shadow map | `q.cshadow`² = 512/768/1024 | RGBA16F | `FS_CLOUDSHADOW` 763, 2075–2076 | **каждый кадр** |
| 7 | Террейн (MRT): HDR color + linear distance + depth RB | full `S.W×S.H` | RGBA16F + R32F + D24 | `FS_TERRAIN` 803, 2078–2083 | каждый кадр |
| 8 | Деревья, 1 instanced draw | full | те же MRT | `VS_TREE`/`FS_TREE` 908/1006, 2085–2093 | каждый кадр |
| 9 | Volumetrics (clouds + atmosphere), **MRT×4** | `S.low` = full × `volScale` | 4 × RGBA16F | `FS_VOL` 1055, 2096–2101 | каждый кадр |
| 10 | Temporal accumulation, **MRT×4**, ping-pong | `S.low` | 4 × RGBA16F | `FS_TEMPORAL` 1231, 2103–2110 | каждый кадр |
| 11 | Composite + depth-aware bilateral upsample | full | RGBA16F | `FS_COMPOSITE` 1330, 2112–2116 | каждый кадр |
| 12 | Bloom: 6 down + 5 up | ½…1/64 | RGBA16F | `FS_BLOOM_DOWN/UP` 1397/1416, 2118–2130 | каждый кадр |
| 13 | Final lens pass → default FB | full | LDR sRGB | `FS_FINAL` 1429, 2132–2141 | каждый кадр |

### 1.2 Таргеты и форматы

- `rgba16(w,h)` = **RGBA16F / HALF_FLOAT / LINEAR / CLAMP_TO_EDGE** — строка 1602. Все HDR-цветовые RT: `tColor`, `comp`, bloom, `cur`/`hist` (×3 набора по 4 текстуры), `cshadow`, `fmap`, `trans`, `ms`, `irr`, `curl`.
- `r32(w,h)` = **R32F / NEAREST** — линейная дистанция до сцены в км, строка 1603, 1833.
- Глубина террейна — `renderbuffer DEPTH_COMPONENT24` (1834–1837); теневые каскады — `texStorage3D(TEXTURE_2D_ARRAY, DEPTH_COMPONENT24, 2048, 2048, 5)` (1764–1765), hardware compare `COMPARE_REF_TO_TEXTURE` / `LEQUAL`.
- 3D-шумы и weather — `RGBA8 / UNSIGNED_BYTE`, `REPEAT`, `LINEAR_MIPMAP_LINEAR` + `generateMipmap` (1713–1719, 1751).

### 1.3 Разрешения и dynamic resolution

**QUALITY-таблица (строки 181–186):**

| Профиль | `scale` (vol) | `cloud` steps | `light` steps | `atmo` steps | `cshadow` | `maxPix` |
|---|---|---|---|---|---|---|
| Low | 0.35 | 48 | 4 | 16 | 512 | 2.4e6 |
| Medium | 0.5 | 80 | 5 | 24 | 768 | 2.4e6 |
| High | 0.5 | 120 | 6 | 32 | 1024 | 4.0e6 |
| Ultra | 0.75 | 150 | 6 | 40 | 1024 | ∞ |

- Full-res: `dpr = min(devicePixelRatio, 2)`, затем общий пиксельный кэп `s = min(1, sqrt(maxPix/(w*h)))` (1826–1830).
- Low-res: `S.low = round(S.W * S.volScale)`, `S.volScale ≤ q.scale` (1848–1852, 1947–1948).
- **Ключевая оптимизация:** `cur`/`hist` выделяются ОДИН раз по максимальному `q.scale` (`S.alloc`), а `volScale` лишь уменьшает viewport/диапазон сэмплирования — переаллокаций при изменении разрешения нет (1850–1857, 1948).
- Dynamic resolution (1932–1948): GPU-таймер `EXT_disjoint_timer_query_webgl2`; скользящее среднее по 40 кадрам. Пороги: **>14.0 мс → −0.05**, **<11.5 мс → +0.025** (с GPU-таймером); без него fallback по rAF **18.5 / 16.9 мс**. Коррекция раз в **700 мс**, минимум `volScale = 0.25`, после даунскейла **holdUp = 4 с**. `resetHistory = true` при каждом изменении масштаба.

### 1.4 Примерный объём VRAM (при 1920×1080, Ultra)

| Ресурс | Оценка |
|---|---|
| shape 128³ RGBA8 + mip | ~10.7 МБ |
| detail 64³ + foliage 64³ + weather 512² (с mip) | ~1.33 × 3 ≈ 4 МБ |
| curl 128² RGBA16F, trans 256×64, ms 32×32, irr 2×1 | <0.3 МБ |
| **10 теневых срезов** 2048² D24 (5 слоёв, обычно паддится до 4 байт) | **60–80 МБ** |
| tColor + comp RGBA16F full | 33 МБ |
| tDepth R32F + depthRB full | ~17 МБ |
| vol: 3 набора × 4 × RGBA16F @1440×810 | **~112 МБ** |
| bloom 6 уровней | ~5.5 МБ |
| cshadow 1024² + fmap 1024² RGBA16F | ~17 МБ |
| **Итого** | **~260–280 МБ** |

---

## 2. Облака (главный раздел)

### 2.1 Бейк шумов при старте

Общий механизм: `make3D(size)` (1713–1720) создаёт `RGBA8` 3D-текстуру с `REPEAT` и mip-чеином; `bake3D(tex,size,prog,extra)` (1721–1729) рендерит **по одному fullscreen-проходу на слой z**, прилинковывая слой через `framebufferTextureLayer`, затем `generateMipmap`.

| Текстура | Размер | Проходов бейка | Содержимое каналов | Строки |
|---|---|---|---|---|
| `shape` | **128³** | 128 × 128² | R = Perlin-Worley/Alligator base; G = 4+8+16 уср.; B = 8+16+32; A = 16+32+64 | `FS_SHAPE` 597–610 |
| `detail` | **64³** | 64 × 64² | R/G/B = три набора октав 4/8/16, 8/16/32, 16/32; A=1 | `FS_DETAIL` 612–621 |
| `foliage` | 64³ | 64 × 64² | RGB = градиент alligator-шума (нормаль листа), A = плотность | `FS_FOLIAGE` 892–903 |
| `weather` | 512² | 1 | R = coverage, G = type | `FS_WEATHER` 623–635 |
| `curl` | 128² | 1 | RGB = смещение xyz (r/b = ротор Perlin-потенциала в xz, g = sign Perlin для y) | `FS_CURL` 638–652 |

**Алгоритмы (`NOISE_LIB`, 547–595):**

- `perlinP(p, per)` — классический 3D Perlin с PCG3d-хэшем и periodic `mod(i, per)` (548–559).
- `worleyP` — 3×3×3 обход ячеек, `1 - sqrt(min dist²)` (560–571).
- `alligatorP` — "Houdini/Nubis 2017": в каждой ячейке сферическая шапочка со случайным радиусом `0.75..1.35` и амплитудой `0.45..1.0`, комбинируются через `max`, `v = max(v, amp*sqrt(1-d)*(1-0.35d))` (572–589) — даёт круглые пуфы и мягкие складки.
- `cellNoise(type)`: 0=Alligator, 1=Worley, 2=`saturate(abs(perlin)*1.8)` (Billow Perlin) (591–595).
- Shape base: `pf = P4 + 0.5·P8 + 0.25·P16`, нормировка `/1.75*1.4+0.5`; cell fbm `wf = 0.625w4+0.25w8+0.125w16`; `pw = remap(pf, 0,1, wf,1)` (603–609) — канонический Perlin-Worley.
- Weather: 5 октав Perlin (3/6/12/24/48) + 2 Worley (12/24) для coverage; отдельный Perlin(2) для type (628–634).
- Curl: центральная разность потенциальной функции `pot()` в 4 точках, деление на 8, `e = 0.5/uSize` (641–651).

**Время бейка:** в коде **не замеряется** (нет `performance.now()` вокруг `initStatic()`; только `setTimeout(...,30)` на строке 2159 и экран «Generating cloud noise…»). Косвенная оценка по числу фрагментов: shape ~2.1 млн фрагментов × ~270 PCG3d-хэшей (5 cellNoise × 27 ячеек × 2 hash + Perlin), detail ~0.26 млн × ~216, foliage ~0.26 млн × ~648 — это заметный одноразовый стартовый GPU-пик (порядок сотен мс на desktop GPU), но точное число в исходнике отсутствует.

### 2.2 Рэймарчинг облаков — `marchClouds()` (1082–1158)

**Пересечение сферических оболочек** (1084–1097): `rb = Rg+bottom`, `rt = Rg+top`; корректно обрабатываются 3 случая (камера ниже/внутри/выше слоя). `t1 = min(t1, limit, uCloudFar)`, `uCloudFar = 180 км` (2099).

**Число шагов** (1100–1102):
```
steps = mix(uCloudSteps * 0.4, uCloudSteps, saturate(seg / 12.0));
dt = seg / steps;
t = t0 + dt * jit;
```
Т.е. 40% от максимума для коротких отрезков, полное число (48/80/120/150) для ≥12 км. Пустые шаги (`base <= 0`) удваиваются: `t += dt*2.0` (1151). Early-out по `T < 0.01` (1115).

**Сэмплинг и MIP** (1109–1122): `pixAng = 2*tanHalf.y/uLowSize.y`; `lodS = clamp(log2(t)+log2(pixAng*SHAPE_RES/shapeScale)-uSharp, 0, 5)`; `fp = log2(t)+log2(pixAng)-uSharp`. MIP считается из углового footprint пикселя — подавление absorbing aliasing без extrasamples.

**Форма (400–436):**
- `cloudWeather()` 387–393: coverage `thr = 1 - coverage*1.3`, `cov = smoothstep(thr, thr+0.45, weather.r)`; `type = saturate(ctype + (weather.g-0.5)*0.7)`.
- `cloudProfile(type)` 394–399: stratus `(0,.07,.12,.25)`, strato-cu `(0,.12,.35,.55)`, cumulus `(0,.10,.55,1.00)` — 4.opacity-высоты для height gradient.
- `cloudBase()` 400–414: `base = remap(n.r, fbm-1, 1, 0,1)`, умножается на `smoothstep(g.x,g.y,h)*(1-smoothstep(g.z,g.w,h))`; coverage сужается кверху `cov = wt.x*mix(1,0.5,hn²)` — куполообразование вместо «экструда» погодной карты (408–412). Смещение по ветру: `p = W - uWind + uWindDir*h*0.6` (вершины наклоняются по ветру).
- `erodeOctave()` 418–423: `lod = clamp(fp+log2(DETAIL_RES/scale),0,5)`, `n = dot(textureLod(detail,...), uOctW)`; **высота переворачивает шум**: `m = mix(n, 1-n, saturate(h*8))` — перистые («wispy») низы, пуфы («billowy») верха; `remap(b, m*erosion*amt, 1, 0,1)`.
- `cloudDetail()` 429–436: сначала Curl `p += curl*(turb*0.18*(1-0.7*h))`, затем mid (укороченный сэмпл), затем **early-out `if (b<=0.003) return 0`** — экономит fine-выборку. `cloudDetailLight()` 425–428 — упрощённая версия для теневых лучей: только mid, без curl.
- `uOctW = octW(roughness) = (1, k, k²)/(1+k+k²)`, `k = 0.15+0.85*r` (1745).

**Плотность и «резкость»** (1124–1130): `sw = mix(1.0, 0.03, pow(uSharp,0.6))`; `dens = saturate((dens - uSharp*0.05)/sw)`, затем затухание на `uCloudFar`: `dens *= 1-smoothstep(far*0.6, far, t)`.

**Light marching — `lightOD()` (1067–1080):**
- до **8** шагов (фактически `uLightSteps` = 4/5/6), `ds` начинается с **0.08 км** и растёт `×1.8` (достигая ~11 км);
- первые 2 сэмпла используют `cloudDetailLight`, остальные `b*0.8`;
- `od * sigma`, `sigma = 45.0 * uCloudDensity` (1107).

**Фазы и multi-scattering** (437, 1103–1143):
- `cloudPhase(c,k) = mix(phaseHG(c, -0.25k), phaseHG(c, 0.82k), 0.72)`.
- `ph0 = phase(cosT,1.0)`, `ph1 = phase(cosT,0.5)`, `ph2 = phase(cosT,0.25)`.
- `beer = ph0·exp(-od) + 0.5·ph1·exp(-od·0.5) + 0.25·ph2·exp(-od·0.25)` — дешёвая аппроксимация многократного рассеяния (три « expansión» exporter с убывающими весами).
- `powderW = 0.6·saturate(0.5-0.5·cosT)+0.2`; `powder = mix(1, 1-exp(-sE*0.15), powderW)`.
- Crease shading (Nubis / Schneider 2017): `depthP = 0.05 + pow(saturate(dens0*1.5), clamp(remap(hf,0.3,0.85,0.5,2.0),0.5,2.0))`; `vertP = pow(clamp(remap(hf,0.07,0.14,0.1,1),0.1,1),0.8)`; `inScat = mix(1, saturate(depthP*vertP*1.6), uCrease)`.
- Ambient: `ambTop = texelFetch(uIrr,(1,0))*uAmbient`, `ambBot = texelFetch(uIrr,(0,0))*uAmbient*0.55`; `amb = mix(ambBot,ambTop,hf) * mix(1, inScat, 0.6)`.
- `S = sunL*beer*powder*inScat + amb`, где `sunL = uSunIllum*uSunBright*transmittanceToSun(...)`.
- Интегрирование: `a = 1-exp(-sE*dt)`; `col += T*S*a`; `dsum += T*a*t; wsum += T*a`; `T *= 1-a`. `dc = dsum/wsum` — глубина облака для split атмосферы.
- **Фикс утечки солнца** (1155–1157): при `T<0.01` `col /= max(1-T,1e-4); T=0` — иначе ~15000× ярче солнечный диск просачивался сквозь непрозрачное облако.

**Рandom-джиттер** (1065, 1202–1203): `ign()` interleaved gradient noise с периодом **64 кадра**; `jit2 = fract(jit + 0.61803398875)` (golden ratio) для атмосферы.

### 2.3 Cloud shadow на землю

`FS_CLOUDSHADOW` (763–785) — **отдельный fullscreen-проход** в солнечном пространстве:
- карта покрывает `uShadowExtent = 45 км` (константа `EXT`, 1992), центрирована по камере, снэпнута к целым текселям (`SNAP = 2*EXT/256`);
- для каждого текселя луч из точки на нижней/верхней сферической оболочке по `uSunDir`, **20 шагов** (777–783), сэмплируется только `cloudBase(..., lod=2.0)` (без detail!) — дёшево;
- запись: `exp(-od * 45.0 * uCloudDensity * 0.35)` в R-канал RGBA16F.
- `cloudShadowAt()` (440–448): перевод мировой позиции в sun-space, край `smoothstep(0,0.04,edge)`, `T = mix(T,1,saturate(hf))` — тень ослабает с высотой.
- Применяется в `FS_TERRAIN` (863), `FS_TREE` (1037), в `atmosphere()` для лучей (1182) и для «дальней земли» (1218).

### 2.4 Sun shafts

**Отдельного прохода нет** — «настоящие» объёмные лучи получаются внутри `FS_VOL::atmosphere()` (1160–1191):
```
if (h < 12.0) {
  vis = cloudShadowAt(W,hf) * terrainShadowAt(W,0.05);
  vis = mix(1.0, vis, uShafts);
}
S = uSunIllum * ((rs*pR + mS*pM)*sunT*vis + (rs+mS)*getMS(...));
```
Т.е. затенённый in-scattering и от облаков, и от гор/деревьев, с множителем `uShafts` (0.46 по умолчанию). Видимость солнца для линзы отдельно считается 5-тап-функцией `sunVisibility()` в `FS_FINAL` (1451–1462).

### 2.5 Взаимодействие с глубиной сцены

- `FS_TERRAIN` пишет линейную дистанцию `oD = vec4(dist,...)` в R32F (874); деревья — тоже (1051).
- `FS_VOL` (1193–1205): `sceneD = texelFetch(uSceneDepth, fp)`, `limit = terrain ? sceneD : (tPlanet>0 ? tPlanet : 1e9)` — облака обрезаются по земле/планете; `marchClouds(W0,rd,limit,...)`.
- Атмосфера делится на front/back по cloud depth `cD` (1160–1227): `Lf/Tf` до облака, `Lb/Tb` после; если есть земля, добавляется `groundL` с альбедо `(0.06,0.075,0.045)` и cloud shadow (1211–1224).
- Итоговая композиция в `FS_COMPOSITE` (1391): `col = Lf + Tf*(cloudRGB + cloudT*(Lb + Tb*bg))`. Солнечный диск добавляется **позади облака** `col += sunAdd * v0.a` (1392).

---

## 3. Атмосфера (Hillaire 2020 style)

### 3.1 Константы и medium (COMMON, 200–299)
- `RAY_S = (5.802e-3, 13.558e-3, 33.1e-3)`, `MIE_S = 3.996e-3`, `OZONE_A = (0.650e-3, 1.881e-3, 0.085e-3)`.
- Высоты затухания: Rayleigh `exp(-h/8)`, Mie `exp(-h/1.2)`, haze `exp(-h/0.8)*uHaze`, озон — треугольник `max(0,1-|h-25|/15)`.
- `mieS = MIE_S*dM + 0.012*dH`; `ext = rayS + vec3(mieS*1.11) + OZONE_A*dO`.
- Фазы: `phaseR`, `phaseM(c,0.8)` (для неба/атмосферы), `phaseHG` (для облаков).
- `transUV/transUVinv` (300–316) — параметризация края атмосферы по Hillaire; `transmittanceToSun` (318–326) добавляет мягкий терминатор `smoothstep(muH-0.012, muH+0.006, mu)`.

### 3.2 LUT
| LUT | Размер | Бейк | Сэмплинг |
|---|---|---|---|
| Transmittance | 256×64 | `FS_TRANS` 655–677: **60** шагов с `t = tMax·(i/N)²`, `exp(-od)` | `transUV(r,mu)` → `texture(uTransLUT,...)` (300–307, 325) |
| Multiple scattering | 32×32 | `FS_MS` 679–722: **8×8 направлений (SQ=8)** × **20 шагов (NS=20)**; аккумуляция `Lr`, `F`; результат `lum/(1-min(fms,0.99))`; есть член отражения земли `max(dot(n,L),0)*0.3/PI` (713–717) | `getMS(h,muS)` = `texture(uMSLUT, (muS*0.5+0.5, sqrt(h/(Rt-Rg))))` (327–329) |
| Irradiance | 2×1 | `FS_IRR` 724–760: **48** направлений по золотому углу (`phi = i*2.39996323`) с весом `z`; внутри `skyRadiance` — **16** шагов; `E/PI + (0.0002,0.0004,0.0009)` ночной пол | `texelFetch(uIrr, ivec2(0/1,0),0)` — 0 = низ, 1 = верх облаков |

Пересчёт trans+MS только при смене `haze` (2035–2036), т.к. не зависят от солнца; `irr` — каждый кадр (2037).

### 3.3 Time-of-day
`render()` 1950–1957: `theta = (time-6)/12·π`, максимальная высота **62°**, азимут `heading`, `uSunIllum = [10, 9.8, 9.4]` (2014), радиус диска `sunsize*0.5*π/180`, яркость диска ×1500 с limb darkening (1382–1389). Velocity time-lapse `25×` для ветра (1961).

---

## 4. Временная стабилизация и appearance

### 4.1 Temporal (`FS_TEMPORAL`, 1231–1328)
- **Репроекция без MV-буфера**: восстанавливаем мир `wp = camPos + rd*d` (или `rd` для неба), `pc = uPrevVP * wp`, `puv = pc.xy/pc.w*0.5+0.5` (1273–1279).
- **Depth-aware history, 2×2** (1281–1303): expected depth `expD = length(worldPos - prevCamPos)`, сравнение в логарифме; `w = bw*saturate(1-(|log(pd)-log(expD)|-0.06)*10)` — принимаем <~6% изменения глубины, отбрасываем >~16%. Если `wsum<1e-3` →/disocclusion, берём текущий кадр.
- **Адаптивная фильтрация истории** (1304–1311): если весь 2×2 footprint на одной поверхности → **9-tap Catmull-Rom** через 3 билинейных выборки (`sampleCR`, 1246–1267) — история остаётся резкой; иначе — depth-взвешенное усреднение. `conf = wsum/wbil`.
- **Neighbourhood clamp 3×3** (1313–1324): история клампится в min/max текущего кадра по каждому из 4 MRT.
- **Веса** (1326–1327): `blendA = mix(0.5, 0.12, conf)` → при полной уверенности ~**0.88 истории / 0.12 текущего** (постоянная времени ≈ 8 кадров), при низкой — 0.5/0.5. 2 history buffer ping-pong (2104), `uReset` на сброс при смене разрешения/шума/ветра (1944, 2032).

### 4.2 Depth-aware upsample (`FS_COMPOSITE`, 1353–1372)
4 билинейных тапа low-res, вес `dw = exp(-|log(sceneDepth_low) - log(sceneDepth_full)| * 6.0)`; плюс `1e-4*bw + 1e-6` чтобы избежать деления на ноль. Full-res depth — из R32F.

### 4.3 Bloom
- **6 уровней downsample** (2119–2124), `FS_BLOOM_DOWN` (1397–1414) — 13 tap (5 подгрупп 2×2 + 4 диагонали), **Karis average** на первом уровне: `kw = 1/(1+luma*0.25)`, luma `(0.2126,0.7152,0.0722)`.
- **5 уровней upsample** (2125–2129), `FS_BLOOM_UP` — 3×3 tent `/16`, аддитивный blend `ONE,ONE`.
- Итог `uBloomI = bloom*0.12` (2139).

### 4.4 Final lens pass (`FS_FINAL`, 1429–1538), порядок:
1. **Дисторсия** `q = p(1+k·r²)/(1+k·rc²)` (1464–1468).
2. **Chromatic aberration** — 3 выборки `ca = uCA*0.006`, масштабы `(1±ca)` (1469–1474).
3. **Lens flare** (1476–1500): 6 ghosts `GO=(-0.35,0.45,0.8,1.25,1.6,2.2)`, `GR=(0.03,0.06,0.02,0.1,0.045,0.16)`, цвета `0.5+0.5cos(2π(vec(0,.33,.67)+i*0.17))`; halo `exp(-((hd-0.42)*22)²)`, anamorphic streak `exp(-|ds.y|*220)*exp(-|ds.x|*2.5)`, glow `exp(-|ds|*8)`; видимость солнца — 5 тапов по depth+cloud.
4. **auto-exposure без GPU readback** (1502–1506): из `uIrr` + прямого солнца; `exposure = min(2.8/pow(lum,0.78), 60)*exp2(uEV)`.
5. **Vignette** (1508–1510), затем **ACES** с явными INPUT/OUTPUT матрицами (1440–1447, 1512).
6. **Unsharp mask по tone-mapped luminance** (1513–1525), 4 соседа, `clamp(lc-lb*0.25, -0.1, 0.1)*sharpen*2.5` — подавление ringing у солнца.
7. `toSRGB` → **contrast S-curve** `a/(a+b)` (1527–1531) → **dither** `(hash-0.5)/255` → **letterbox 2.39:1** (1533–1536).

Звёзды — в `FS_COMPOSITE::stars()` (1336–1345): 3D-хэш ячеек, порог `h.x<0.994`, ночной множитель `1-smoothstep(-0.12,0.05,sunDir.y)`.

---

## 5. Ландшафт и деревья

**Террейн** (`terrainHeight` 349–371, JS-копия 1666–1681):
- eroded fBm: до **16 октав** (фактически `uOct=9` в кадре, 8–14 в зависимости от дистанции в `FS_TERRAIN` 811); `noised()` возвращает значение и **аналитический градиент** (338–348); эрозия `a += b*n.x/(1+dot(d,d))`, ротация домена `M2 = mat2(0.8,-0.6,0.6,0.8)`; nghiên final `softplus` `0.03+log(1+exp((h-0.03)*12))/12`.
- Mountains: `mtn = smoothstep(0.3, 0.85, низкочастотный шум)`; Rolling hills: `a*0.28+...`.
- Изогнутая планета: `W.y = h - dot(xz,xz)/(2*Rg)` (797); край сглаживается на `TERRAIN_EDGE=120 км` (796). Сетка 512² с кубическим warping к горизонту (1781–1797).
- Тени: `CASC_EXT = [1.5, 6, 45] км` (полуширины → 3/12/90 км), `ZR=150`, 2048², 4-tap PCF, снэп к текселям, плавное смешивание каскадов на внешних 8% (453–475), polygonOffset(1.5,3). Слои 3–4 — **террейн-only копии** каскадов 0–1 через `blitFramebuffer` (2058–2063), чтобы затухать тени деревьев независимо (`terrainShadowC` 476–484).
- Материал: травы/лес/скалы/грязь/снег, «жилки» с domain warp, сухие пятна, тропы, контактный AO от 4 ближайших деревьев (`treeContactAO` 523–535), AO от пещер `cav`, wrapping-освещение и backlit snow spec (803–875).

**Деревья** (`VS_TREE` 908–970):
- 1 дерево на ячейку `treeCS = 0.028/sqrt(treedens)` км (~28 м), `gl_InstanceID` → индексы сетки, позиция/тип/размер/цвет/высота детерминированы PCG3d-хэшем (492–514).
- Меш — решётка **(seg+1)×(rows+1) = 7×5 = 35 вершин, 48 треугольников (36 видимых)** (1814); форма «слёзки» `crownRadius = R*k*2√(u(1-u))*(1-u)^(m/2)`, m=1.2 хвойные / 0.5 лиственные (515–522).
- Всё в вершинном шейдере: palette из 5 оттенков, lumps по нормали, sway, затухание на дистанции (`treeAt` 499), filename `vInfo/vTint`.
- Листовой cutout (973–996) общий для видимого и shadow-прохода: mip с капом 1.5 + **растяжение контраста** `0.6+(n-0.6)*mix(1,1.5,l/1.5)`, три масштаба (3 м / 12 м / 0.6 м), `thr = cutout*0.85*(1-smoothstep(...))`. Теневый меш рисуется **двусторонне** (2064–2069), чтобы дальняя сторона кроны давала тень сквозь дыры.
- Gradient базы кроны `uTreeGrad` (1045–1049).

---

## 6. Перенос в Unity 6 URP (StandaloneWindows64)

### (A) Переносится почти напрямую (C# / HLSL / ComputeShader)

| Техника | Механизм Unity | Стоимость | Выигрыш |
|---|---|---|---|
| Perlin / Worley / Alligator / Billow + FBM-комбайны | ComputeShader → `RWTexture3D<float4>`/RGBA8, [8,8,8] groups; mip через compute-downsample или `Graphics.GenerateMips` | средняя, **одноразово** | очень высокий |
| Weather 512² + curl 128² | ComputeShader / `RenderTexture` 2D | дешёвая | высокий |
| Transmittance 256×64, MS 32×32, irradiance 2×1 LUT | compute или fullscreen blit при смене medium | дешёвая | высокий |
| `marchClouds`, `lightOD`, Beer/powder/crease/3-член multi-scatter, early-out, empty-skip | HLSL fullscreen pass, MRT 4×RGBA16F | **дорогая (основной кост)** | очень высокий |
| Cloud shadow map 20 шагов | отдельный fullscreen pass в sun-space, RG16F/R16F | средняя | высокий (тени + лучи) |
| HG/Rayleigh/Mie фазы, `medium()`, LUT-сэмплинг | HLSL, `Texture2D` | дешёвая | высокий |
| Temporal reprojection + Catmull-Rom + depth clamp + neighbourhood clamp | HLSL + 2× ping-pong RTHandle, `prevViewProj` как matrix uniform | средняя | очень высокий (стабильность при 0.35–0.75×) |
| Depth-aware bilateral upsample 4 tap | HLSL composite pass | дешёвая | очень высокий |
| Karis-average bloom 6 down / 5 up, ACES с матрицами, flare, distortion, CA, vignette, sharpen, exposure, dither | кастомный `ScriptableRenderPass` / Fullscreen Shader Graph; часть — через URP Volume (Bloom, Tonemapping, Vignette, ChromaticAberration, LensDistortion, Sharpen, FilmGrain) | дешёвая/средняя | средний/высокий |
| Eroded fBm с аналитическим градиентом (для нормалей) | HLSL в terrain shader или compute для displacement | средняя | высокий |
| Instance placement/shape/sway/cutout trees entirely in VS | `SV_InstanceID`, `Graphics.DrawMeshInstancedProcedural`, `StructuredBuffer` | средняя | высокий |

### (B) Переносится с адаптацией под URP

1. **RenderGraph / MRT**: `FS_VOL`, `FS_TEMPORAL`, `FS_TERRAIN` пишут 4 / 4 / 2 цветовых таргета. В Unity 6 нужно `RenderGraph.AddRasterRenderPass` + несколько `SetRenderAttachment` (и/или `m_RequireAllColorAttachments` для legacy path); URP по умолчанию ожидает один active color target. Проверить API под 6000.5.2f1 через `unity_reflect` перед написанием кода.
2. ** Depth/normal buffers**: здесь используется **кастомная линейная дистанция в R32F**, а не `_CameraDepthTexture`. Нужны: MRT с R32F на terrain pass, привязка его как `Texture2D` к volumetrics/composite. Либо включить `supportsCameraDepthTexture` и переписать реконструкцию на `DecodeViewNormalStereo` + `ComputeWorldSpacePosition`, но тогда потеряется точность линей км.
3. **Camera motion vectors**: репроекция здесь **не использует MV texture** — только `prevVP` + depth. В URP можно просто передать `prevViewProj` как uniform (works out of box), но при включённом TAA/SMAA и jitter нужно либо отключить jitter, либо синхронизировать с `Camera.GetProjectionMatrix`/jittered matrices.
4. **Half-res pass injection**: `RTHandle` с `scaleFactor = (0.25…0.75, 0.25…0.75)`, `RenderGraph.CreateTexture` с `halfRes=true`; explicit `Build()` + `SetRenderAttachment`. Нужно выделить буферы на **максимальный** scale (как в `allocLow`) и менять только viewport — это важная оптимизация исходника.
5. **Dynamic resolution**: URP `DynamicResolutionState` / `ScalableBufferManager` масштабирует **всё**, здесь — только volume RT. Нужна кастомная привязка GPU-таймера (`ProfilingScope` + `GraphicsFrameStats` / `RenderGraph` GPU markers) к `RTHandle.ScaleFactor` без трогания basic render scale. Пороги 14/11.5 мс и holdUp 4 с — перенести как есть.
6. **Теневые каскады**: URP имеет свои 4 каскада и PCF, но здесь 3 каскада с **независимыми terrain-only копиями** и кастомным cutout для деревьев. Требуется кастомный shadow caster (cutout в `shadowCaster` pass) и, для точного повторения, отдельный `RenderTexture` shadow atlas + `Graphics.CopyTexture` для копий. Иначе — смириться с упрощением (fade tree shadows через `uTreeShadow` не получится).
7. **Compute-бейк 3D-текстур**: `Texture3D` RT с mip chain в Unity — нужно проверить поддержку `Graphics.GenerateMips` для `RenderTexture` dimension `Tex3D` на D3D11/12 (в исходнике это `gl.generateMipmap(TEXTURE_3D)`); при необходимости — compute-downsample по слоям.
8. **Единицы и floating origin**: 1 юнит = 1 км и `Rg=6360` несовместимы с текущим ребейзом проекта (сдвиг корней сцены). Нужно либо ввести `WORLD_SCALE` (1 юнит = 1 м, делить все km-константы на 1000), либо держать облачный рендер в отдельной системе координат с центром на камере и передавать `uCamPos` уже в km. Без этого — появление z-fighting/джиттера. this — самый большой архитектурный риск порта.
9. **Порядок постобработки**: URP применяет tonemapping в конце стека; здесь ACES+vignette+unsharp+contrast+dither в одном кастомном pass. Нужно отключить встроенный URP Tonemapping/ColorAdjustments и делать финальный `Blitter.BlitCameraTexture` вручную, иначе порядок нарушится.
10. **Прозрачность/небо**: `stars` + sun disk рисуются в composite, а не отдельным skybox; интеграция с URP `RenderSettings.skybox` потребует отключить последний.

### (C) НЕ переносится / не нужно

- WebGL2/GLSL-specific: `#version 300 es`, `precision highp sampler3D`, `gl_FragCoord/gl_VertexID` fullscreen triangle (заменяется на `SV_VertexID`/`Blitter`), `EXT_color_buffer_float`, `EXT_disjoint_timer_query_webgl2`, `texStorage3D`/`framebufferTextureLayer`/`blitFramebuffer`/`drawBuffers` (всё → RTHandle/RenderGraph/`Graphics.CopyTexture`), `textureLod/texelFetch` (HLSL `Texture2D.SampleLevel`/`Load`).
- JS UI (`SCHEMA`, `buildUI`, `setSlider`, `fmtTime`, `#camro`, clipboard) — заменяется на UI Toolkit/uGUI или Inspector `[SerializeField]`.
- Один HTML-файл, `setTimeout`-старт, `campos` readout, free-fly WASD-камера, 25× time-lapse ветра, `dpr≤2`, `powerPreference:'high-performance'` — демо-специфика.
- Кривизна планеты `Rg=6360`, радиус обзора 120 км и clamp камеры 400 км — завязаны на структуру сцен этого демо; для проекта с 24 сценами 6×4 и floating origin требуется перепроектирование (см. п. 8 выше).
- `DEPTH_COMPONENT24` + `sampler2DArrayShadow` hardware PCF — в URP другие shadow-map соглашения; прямой-analog нет.

---

## 7. Конкретные «фишки», которые стоит позаимствовать (20 пунктов)

| # | Фишка (где) | Что даёт |
|---|---|---|
| 1 | Всё FBM сразу в RGBA 3D-текстуре: R=Perlin-Worley, GBA=3 октавы Worley (`FS_SHAPE` 603–609) | Base shape + detail-октавы одним сэмплом |
| 2 | Alligator cell noise с `max()` сферических шапочек (574–589) | Круглые пуфы и мягкие складки дешевле, чем 3 Worley-прохода |
| 3 | Двухстадийная эрозия mid→fine + `if (b<=0.003) return 0` (429–436) | Экономит fine-текстуру там, где mid уже «съел» форму |
| 4 | 2D curl-noise как xyz-смещение (431–432, `FS_CURL`) | Закрученные перистые края без 3D curl-объёма |
| 5 | MIP из углового footprint: `fp = log2(t)+log2(2*tanHalf.y/lowH)` (1110–1120) | Стабильность при движении камеры без суперсэмплинга |
| 6 | Переворот шума по высоте `mix(n,1-n,saturate(h*8))` (421) | Wispy низ → billowy верх, реалистичный height gradient |
| 7 | `cov = wt.x*mix(1,0.5,hn²)` — сужение coverage кверху (408–412) | Купола вместо «экструдированной» плоской погодной карты |
| 8 | `beer = ph0·e^-od + .5·ph1·e^-od/2 + .25·ph2·e^-od/4` (1135) | Multiscattering почти бесплатно |
| 9 | Две HG-фазы `-0.25k`/`0.82k`, mix 0.72 (437) | Переднее рассеяние + реалистичный backscatter ореол |
| 10 | Powder `1-exp(-sE*0.15)` с весом от угла (1108, 1136) | Яркие каймы при съёмке против солнца |
| 11 | Crease shading Nubis `depthP*vertP*1.6` (1139–1141) | Тёмные складки между биллоу, объём вместо «вата» |
| 12 | Empty-space skipping `t += dt*2` (1151) | Почти 2× скорость рэймарча |
| 13 | Адаптивные шаги `mix(0.4N, N, seg/12)` (1100) | Не тратим 150 шагов на тонкие слои |
| 14 | Early-out `T<0.01` + обратная нормализация `col/(1-T)` (1155–1157) | Устраняет просачивание 15000× солнца сквозь облако |
| 15 | Разделение атмосферы front/back по cloud depth `cD` (1188–1189, 1391) | Облако **внутри** атмосферы, корректная композиция |
| 16 | Depth-aware 2×2 history + 9-tap Catmull-Rom + 3×3 clamp + blend 0.12/0.5 (1281–1327) | Резкая и стабильная история при 0.35× resolution |
| 17 | Bicubic history только если весь footprint на одной поверхности (1305–1310) | Резкость без ghosting на силуэтах |
| 18 | Terrain-only копии каскадов через `blitFramebuffer` (2058–2063) | Независимый fade теней деревьев от теней гор |
| 19 | Cutout с капом mip + растяжением контраста, общий для visible и shadow pass (973–996) | Дыры в листве не «замыливаются» вдаль и совпадают с тенями |
| 20 | Allocate-once `S.alloc`, меняем только `volScale`/viewport (1850–1857, 1948) | Dynamic resolution без переаллокации VRAM и без просадок |
| 21 | Karis average в первом bloom-down (1404) | Солнце не забивает bloom белым пятном |
| 22 | Unsharp по **tone-mapped** luminance (1513–1525) | Никаких ringing/oreol вокруг солнца |
| 23 | Auto-exposure из irradiance LUT без GPU readback (1502–1506) | Экспозиция почти бесплатна, нет stall-ов |
| 24 | Всё дерево в вершинном шейдере: placement+палитра+lumps+sway+fade (908–970) | Один instanced draw на весь лес, LOD-стабильность |
| 25 | Джиттер IGN с периодом 64 кадра + golden-ratio для атмосферы (1065, 1202–1203) | Равномерный шум без повторения на глазу |

---

### Итог для сравнения с Unity-проектом

Ключевые отличия демо от текущего проекта: (1) шкала в километрах и кривизна планеты vs 80×80 км сцены с floating origin; (2) кастомный MRT с линейной depth в R32F вместо `_CameraDepthTexture`; (3) репроекция через `prevVP` без MV-буфера; (4) весь HDR/Post в одном кастомном финальном pass вместо URP Volume-стека; (5) бейк 3D-шума на GPU старте вместо ассетов. Наиболее ценное для заимствования — **блок облаков (§2), temporal (§4.1) и depth-aware upsample (§4.2)**; наиболее рискованные при порте — **MRT в RenderGraph и масштаб/floating origin**.
