# T-CULL01 / Stage 7 — brief для отдельного агента (HLOD/L1 + L2-импостор, H7)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **База:** `95890c51`
**Статус:** Stage 7 **утверждён пользователем как фундамент**, не опциональный. Стартовый baseline:
**P1 2 362–2 378 DC**, Stage 6 PASS, F8/F9 PASS. **L0 не ухудшать.**
**Scope:** HLOD/L1 + L2-импостор; PVS — только если после L1/L2 реально нужен L0 <1k.
Корабли/medium/light/NPC — не трогать. B2/Stage 5 — закрыт, не делать.
**Play Mode:** нужен для замеров; только с разрешением пользователя. Сцену без разрешения не сохранять,
push не делать.

**Прочитать:**
- `C:\Projects\ProjectC_client\docs\world\primum_city\01_SETUP\04_LOD_AND_DISTANCE.md` (главный дизайн L0/L1/L2)
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_STAGE6_REPORT.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\ROADMAP.md` (Э6)
- `C:\Projects\ProjectC_client\docs\world\distantfocus\README.md` (пороги 800/4000 не менять)
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_COMPLEX_CODE_HANDOFF.md` §8

---

## 0. Цель Stage 7

Сделать **дальний мир читаемым и дешёвым**:
- **L1 (3–12 км):** HLOD кварталов/блоков — 1 меш на блок, окна эмиссией, мелочь скрыта;
- **L2 (12–80 км):** 1 билборд-квад на город (силуэт) + квад ночных огней, camera-facing;
- bake — **в редакторе**, не в рантайме; ассеты в git; FO-safe;
- L0 (0–3 км) остаётся текущим полным городом, **не хуже 2 362–2 378 DC**.

**PVS** — только если после L1/L2 реально требуется L0 <1k; сейчас L0 ~2.4k, поэтому PVS **не первый шаг**.
Если делаешь PVS, только при замороженном городе и локальных координатах ячеек.

---

## 1. Что уже есть (не ломать)

| Система | Владелец | Как учитывать |
|---|---|---|
| L0 город | `PRIMUM` под `WorldRoot_0_0`, merge chunk×material, GRD | HLOD не должен менять L0-меши/коллайдеры |
| `VisibilityDirector` | Stage 6 | HLOD-группы регистрировать как провайдер/наблюдаемые; не дублировать `forceRenderingOff` |
| `CityShadowBudget` / `DoorVisibilityBudget` / `ShipShadowBudget` | бюджеты | HLOD не пишет их свойства без согласования |
| `DistantFocus` | пост-эффект | пороги 800/4000 **не менять** |
| `ViewDistanceConfig` | Near/Medium/Far | L1/L2 дистанции не конфликтуют с пресетами |

Город **не static** (FO двигает мир) — HLOD/L2 не помечать static, не запекать в статик-батчи.

---

## 2. Порядок работы (пилот → раскатка)

### 2.1. Аудит (1–2 дня)

- Найти фактические блоки/чанки города: `PRIMUM_*` корни, имена `PRIMUM_BLK_*`/`PRIMUM_HOUSES_*`;
  сверить с `04_LOD_AND_DISTANCE.md`; выбрать **один** пилотный блок/квартал.
- Зафиксировать: bounds, материалы, submesh-слоты, tris, текущий DC блока на 5–10 км.
- Проверить, как HLOD будет сосуществовать с GRD и `VisibilityDirector` (ownership: кто включает L0/L1/L2).

### 2.2. L1 HLOD pilot (1–2 недели)

- Editor-инструмент: собрать упрощённый меш блока:
  - убрать интерьеры и мелочь <0.25 м;
  - окна — плоские квады с emissive-материалом;
  - крыши/парапеты/силуэт сохранить;
  - `CombineInstance`, `mergeSubMeshes` по материалам, `UInt32` при >65k вершин, bounds.
- Выгрузить `.asset` в `Assets/_Project/Generated/HLOD/<block>_L1.asset` (git).
- Переключение: `LODGroup` на HLOD-корне блока / отдельный `HLODGroup`, LOD0 = L0, LOD1 = L1;
  дистанции **3–12 км**; не трогать L0-рендереры до переключения.
- Проверить: с 3–12 км силуэт узнаваем, окна читаются, DC L1 ≤500 на блок/город; L0 не изменился.

### 2.3. L1 раскатка (2–4 недели)

- Все блоки/кварталы; батчи по материалам; ассеты в git; prefab/HLOD-ассеты; без scene-overrides.
- Интеграция с `VisibilityDirector`: группы HLOD, камеры union, FO-сброс; без двойного toggle.
- Проверить три пресета Near/Medium/Far; LODGroup и `DistantFocus` не конфликтуют.

### 2.4. L2 impostor (1–2 недели)

- Предрендер силуэта: ортокамера с **4–8 направлений** → RenderTexture → спрайт с альфой;
  отдельный спрайт ночных огней (emissive, без теней).
- `PRIMUM_IMPOSTOR` под корнем сцены/HLOD; camera-facing; **хранить только скаляры**
  (текущий LOD, дистанция, направление) — FO-класс 🟢.
- Дистанция **12–80 км**; с 20–80 км = 1–2 draw calls на город; силуэт узнаваем (купол/шпили/трубы).
- **Запрещено:** генерировать импостор в рантайме из сида; выключать дальний террейн.

### 2.5. PVS — только по условию

- Если после L1/L2 L0 всё ещё >1k и PVS реально нужен — отдельный подэтап; иначе **не делать**.
- Город заморожен; данные в локальных координатах; rebake-процесс; FO.

---

## 3. Замеры и приёмка

- Play Mode Host, Midday, far 30 000, fov 60; камеры на 0.5/3/5/10/20/40/80 км.
- Метрики: DC/SetPass/tris; L1/L2 DC; L0 на 0.5–3 км **не хуже 2 362–2 378**.
- **Gate:** L1 ≤500 DC, L2 ≤10 DC; силуэт узнаваем; ночь огни читаются; FO F8/F9 PASS;
  нет rebake-катастрофы (повторный bake детерминирован); L0 не ухудшен.
- Визуальная приёмка пользователем (LOD-переходы, silhouette, night); сцена/ассеты чисто.
- Отчёт `docs/dev/T-CULL01_STAGE7_REPORT.md`, CSV `08_DATA/U_stage7_<date>.csv`;
  commit `T-CULL01/H7`; без push.

**Kill/stop:** L1 не укладывается в ≤500 → упростить/уменьшить блок; L2 силуэт не узнаётся → переделать
bake-ракурс/подсветку, не геометрию; L0 ухудшен → откат; rebake недетерминирован → остановиться.

---

## 4. Абсолютные входы

- `C:\Projects\ProjectC_client\docs\world\primum_city\01_SETUP\04_LOD_AND_DISTANCE.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\ROADMAP.md` (Э6)
- `C:\Projects\ProjectC_client\docs\world\distantfocus\README.md`
- `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\City\PRIMUM_CITY.prefab`
- `C:\Projects\ProjectC_client\Assets\_Project\Art\Models\Primum\Rebased\**` (16 809 мешей)
- `C:\Projects\ProjectC_client\Assets\_Project\Generated\HLOD\` (новая папка; `.asset` в git)
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\VisibilityDirector.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Ships\ShipVisualMerger.cs` (референс Combine)
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Culling\FinalValidationBench.cs` (референс замеров)
- `C:\Projects\ProjectC_client\Assets\_Project\Scenes\World\WorldScene_0_0.unity` — без сохранения без разрешения.

---

## 5. Жёсткие правила

1. Только Stage 7 (HLOD/L1/L2); PVS — по условию. Stage 6/B2/корабли не трогать.
2. Bake только в редакторе; никакой рантайм-генерации; никаких scene-overrides.
3. Город не static; HLOD/L2 FO-safe; скалярное состояние.
4. Не менять `DistantFocus` пороги, URP/GRD, бюджеты без согласования.
5. Prefab/FBX — Lore; `.asset`/скрипты/CSV — в git; сцену без разрешения не сохранять; push нет.
