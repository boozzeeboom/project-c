# SHIPS — корабли (малый / большой / огромный / NPC)

← [README](README.md) · [CHECKLISTS](CHECKLISTS.md) · **§6 — здесь**; общие правила (бюджеты, DC, ownership, мердж, материалы) — [COMMON.md](COMMON.md) §1–§5

## 6. Корабли (малый / большой / огромный / NPC)

### 6.1. Классы и бюджеты

Геймплейные классы — `ShipFlightClass {Light, Medium, Heavy, HeavyII}`; **метрических классов в коде нет**.
Полосы ниже — рабочее предложение (помечено [E] в `_evidence/01_SHIPS.md` §5), используй их как бюджет.

| Полоса | Длина | Пример | Шаги | Бюджет DC (в кадре) | LODGroup | ShadowBudget | Двери / коллайдеры |
|---|---:|---|---|---:|---|---|---|
| **S (малый)** | **<30 м** | Light 18.5 м | H1-аналог → мерж по материалу (1–3 меша) → 1 уровень | **≤30 DC** (≤10 слотов) | не нужен | не нужен | 0–1 / 5–15 |
| **M (большой)** | 30–80 м | Medium 73.1 м | H1 → мерж → LODGroup 2–3 ур. (пороги от габарита) | **≤75 DC** (≤25 слотов) | да | если в кадре 150–300 м | 1–2 / 15–40 |
| **L (крупный)** | 80–200 м | NPC 80–128 м | то же + LOD1 без greeble, LOD2 = 1 меш × ≤5 материалов | **≤180 DC** (≤60 слотов) | да, 3 ур. | да, если попадает в гейт | 2–5 / 40–120 |
| **XL (огромный)** | **>200 м** | HeavyII 324.9 м | H1 → P1 (6 префиксов) → P2 (LOD 300/900, LOD2 без cull) → `ShipShadowBudget(140/150, coverWholeLodGroup)` | **1 170 DC** (604 слота) — факт | да, но LOD0 ближе **281.6 м** недостижим | **обязателен** | 9 / 250 |

**Текущий флот — не эталон бюджета:** Light 18.5 м имеет **428 MR / 1 284 DC** (сырой FBX без мержа),
Medium 73.1 м — 118 MR / 354 DC, HeavyII — 616 MR / 604 слота. Полосы S/M/L/XL ниже — **цель после мержа**, а не текущее.

Формула порогов LOD: `d = worldSize / (2·tan(fov/2)·h)`, `h = screenRelativeTransitionHeight ≤ 1` →
**LOD0→LOD1 не может быть ближе `0.866 × worldSize`** (для heavy 324.85 м это 281.3 м). Не пытайся «просто уменьшить порог».

### 6.2. Эталон HeavyII (что именно дало эффект)

| Шаг | Что | Числа |
|---|---|---|
| H1 `Repair Heavy Prefab` | 250 `BoxCollider` + 9 `DoorController` вынесены из FBX-инстанса в дети префаба (`SHIP_COLLIDERS`); 10 leaf-коллайдеров створок остались на узлах модели | 250 боксов (240 вне FBX / 10 внутри), 9 дверей, 0 missing, позы ±1e-4 |
| P1 `Heavy Visual Merge` | 378 greeble-узлов → 10 мешей (GrbFine 219 → 3; GrbDetail 159 → 7) | 973 → **616 MR**; Σtris 15 992 → 15 992 (**0 %**) |
| P2 LODGroup | LOD0 557 / LOD1 10 / LOD2 1 (5 сабмешей); пороги 300/900 м | слоты 604 / 57 / 52; DC-модель 1 812 / 171 / 156 |
| `ShipShadowBudget` | `on=140`, `off=150`, `smallPartMaxAxis=8`, `coverWholeLodGroup=true`, тик 0.33 с | суммарно с бюджетом: **1 170 DC** на 200 м (гейт ≤1 300), 1 161 повтор, **1 236** на 300 м |
| Проверка в игре | 80 м / 200 м / 300 м / 400 м / 1200 м | 971 / **1 170** / 1 236 / 122 / 104 DC |

### 6.3. Пошаговый пайплайн нового корабля

1. **`Tools → Project C → Create Ship Preset`** — выбрать класс (Light/Medium/Heavy/HeavyII), получить префаб
   со всеми компонентами (Rigidbody, NetworkObject, ShipController, ShipDeckNav, 11 ModuleSlot и т.д.).
2. **Заменить `MainVisual`** на свою модель (root scale = 1, `MainVisual` ребёнком).
3. **Вынести коллайдеры и двери из FBX** (по образцу `ShipPrefabRepair`): коллайдеры — в `SHIP_COLLIDERS`
   как отдельные дети префаба; двери — `DoorController` + `Collider` на узлах створок, иначе reimport FBX их снесёт.
4. **Мерж greebles** (только статика, только если класс M/L/XL): dry-run → apply → verify; Σtris должна совпасть.
5. **LODGroup** для M/L/XL: пороги от `worldSize` (не ближе 0.866×worldSize), LOD1 — без мелкой greeble,
   LOD2 — 1 меш × ≤5 материалов.
6. **`ShipShadowBudget`** для L/XL (и если корабль попадает в гейт DC): Apply → Verify (9/250/11/616 MR, LOD 300/900,
   `Off` в префабе = 0, расхождений с FBX = 0).
7. **Сцена:** инстанс **в корне сцены** (как `Ship_havyII_base`), tag `Ship`, `ShipRootReference`,
   `NetworkObject`; далее **FO-каталог** (`Bake Pilot Catalog`) — корабли вне `WorldRoot` → ветка B обязательна.
8. **Замер** (Play Mode, пользователь): `Project C → Culling → Stage 3 Ship Bench → Run`: 80/200/200-repeat/300/400/1200 м,
   `lodBias 1.3`, shadow 400/4/soft; гейты: 200 м ≤1 300, 300 м ≤1 300, повтор ±5 %.
9. **Целостность:** 9 `DoorController` (open/close), 250 `BoxCollider`, 11 `ModuleSlot`, `PilotSeat`, `CargoVisual`,
   `ContrailVFX`, 2 `EngineThrusterVisual`; F8/F9.

### 6.4. Что нельзя

- Мержить двери/створки/коллайдеры/интерактив/VFX (§4.2).
- Пытаться задать LOD0→LOD1 ближе 281.6 м для heavy (упрётся в `screenRelativeTransitionHeight ≤ 1`).
- Писать `shadowCastingMode` в edit-mode без `Application.isPlaying`-guard и `RestoreShadows()`
  (дефект Stage 2 оставил 5 346 `m_CastShadows: 0` — образец, как делать нельзя).
- Забыть `ShipContrailVfx.UseShipBounds=false` + `ManualBoundsSize` после мержа — след прилипнет к новому bounds.
- Править medium/light/NPC «на всякий случай»: если мешают в кадре — выключать `MeshRenderer` runtime-ом,
  префабы не трогать (решение Stage 3b).
- Менять `ShipPositions.json` с сохранённой позицией падения (телепорт на −41 570 м, каждая сессия перезаписывалась).

### 6.5. NPC-корабли и масштаб флота

- 20 NPC-шаблонов: 6 MR / 820 тр. каждый; **LODGroup нет**. Это нормальный «дальний» профиль.
- При ×100: NPC-корабли не должны быть LOD0-тяжёлыми — держи их в «6 MR» профиле, без динамических материалов;
  если NPC-корабль подходит к игроку ближе ~150 м — ему нужен LOD0-визуал (тогда это уже класс L).
- Одновременно видимых тяжёлых кораблей: **не больше 1** (один heavy = 1 170 DC). Второй — за границей кадра/дистанции.
- Tools зашиты на HeavyII (`PrefabPath`, `FbxPath` константы). Для нового корабля либо параметризуй инструмент,
  либо повтори по образцу с собственным отчётом и инвариантами (абсолютные пороги MR не переноси).
  Готовый образец такого повтора — `AlbatrosVisualMerger.cs` (`ProjectC → Ships → Albatros (NPC)`):
  NPC «Альбатрос», класс L, 1 678 → 63 рендерера, LOD 150/450 м, тени 140/150 м; отчёт — `docs/dev/ALBATROS_MERGE_REPORT.md`.
  Второй образец, для полосы S — `LoreynVisualMerger.cs` (`ProjectC → Ships → Loreyn (Light)`):
  «Лорейн» (Light, 5.16 м), палитра `M_PC_*` + мерж 379 → 13 рендереров, LODGroup и ShadowBudget не нужны;
  отчёт — `docs/dev/LOREYN_MERGE_REPORT.md`.

---

