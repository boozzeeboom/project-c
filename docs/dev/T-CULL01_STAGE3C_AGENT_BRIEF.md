# T-CULL01 / Stage 3c — brief для отдельного агента (P2 closure: только `Ship_havyII_base`)

**Дата:** 2026-10-31 · **Проект:** `C:\Projects\ProjectC_client` · **База:** `bd975340`
**Scope:** **только `Ship_havyII_base`**. `Ship_medium_base`, `Ship_Light_root_fbx_test`, 20 NPC — **не трогать**
(future scale-out заблокирован до закрытия P2).
**Play Mode:** нужен для замеров; запускать только с разрешением пользователя. Сцену не сохранять, push не делать.

**Прочитать:**
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_STAGE3_REPORT.md`
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_STAGE3_PLAYMODE_2026-10-31.md`
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_COMPLEX_CODE_HANDOFF.md` §3
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\10_EXECUTION_PLAN.md` §5/5.0

---

## 0. Где мы сейчас

- P1 принят: `Ship_havyII_base` **973 → 616 MR**, 20 merged-мешей; Σtris не изменились.
- P2: LODGroup есть; **LOD0 200 м — 1 538 DC**, цель была **≤1 300** → **FAIL** (разрыв 238 DC / 18 %).
- LOD1 400 м — 235 DC; LOD2 1200 м — 104 DC; пороги raw 300/900, runtime `lodBias=1.30` → эффективно **390/1170**.
- Двери/9 коллайдеров/слоты/cargo/VFX/250 коллайдеров целы; reimport проверен; другие корабли 0 diff.
- R0.2: корабль без пилота падает на `DECK` и стоит; `ShipPositions.json` ранее телепортировал его в пустоту;
  агент исправил save на позицию покоя, **но сохранение перекрывает авторский hover 28.42 м над DECK**.
  Файл сохранения — решение пользователя; **не менять без разрешения**.

**Задача Stage 3c:** закрыть P2 (≤1 300 DC на полном корабле в кадре на 200 м) **без поломки визуала**.
Другие корабли не трогать; P3/фрактуру не делать.

---

## 1. Абсолютные входы

- Префаб: `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\Ships\Ship_havyII_base.prefab`
  (LODGroup на `SHIP_VISUAL_H2`)
- FBX: `C:\Projects\ProjectC_client\Assets\_Project\Art\Models\Ships\HeavyII_base\project-c_ship_heavyII_1_cline_deepseek_1_glm-53_1_aura_1_game-ready_1.fbx`
- Тул мержа: `C:\Projects\ProjectC_client\Assets\_Project\Editor\Ships\ShipVisualMerger.cs`
- Ремонт H1: `C:\Projects\ProjectC_client\Assets\_Project\Editor\Ships\ShipPrefabRepair.cs`
- Bench: `C:\Projects\ProjectC_client\Assets\_Project\Editor\Culling\Stage3ShipBench.cs`
- Данные: `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\M_stage3_heavy_runtime_2026-10-31.csv`,
  `M_stage3_heavy_2026-10-31.csv`, `I_ships_compartments.csv`, `I_ships_dc_model.csv`.
- Backup префаба: `C:\Projects\ProjectC_client\_local_backups\T-CULL01\Stage3c\Ship_havyII_base.prefab.bak`
  (вне git; папка не коммитится).
- Отчёт/CSV: `08_DATA\N_stage3c_heavy_<date>.csv` + отчёт `docs/dev/T-CULL01_STAGE3C_REPORT.md`.

---

## 2. Варианты закрытия P2 (проверить по порядку, замерить каждый)

### A. LOD-порог (быстрый, 0.5–1 день)

Цель — чтобы на 200 м корабль рисовался LOD1 (235 DC), а LOD0 оставался для ближней дистанции.
Сейчас raw 300/900 при `lodBias=1.30`. Чтобы 200 м попало в LOD1, raw-порог LOD0 должен быть
**≈150 м** (150×1.3≈195 м). Проверить:
- LOD0 end 150, LOD1 end 900 (или 750–900); замер 80/120/200/400 м;
- визуал на 150–250 м: нет «дыр», силуэт/двери читаются; тень не отваливается.
**Плюс:** быстро, без новой геометрии. **Минус:** 200 м — уже LOD1; визуальный компромисс.

### B. Домержить LOD0-статику (сильнее, 2–4 дня)

Сейчас LOD0 — 557 статических рендереров после greeble-мержа. Смержить оставшуюся **статику**
(корпус/палубы/интерьер) по **материалу × крупной зоне** (например, 4–8 зон, без дробления на 8
фрактурных ячеек) → LOD0 ~50–120 слотов; цель ≤1 300 при LOD0 на 200 м.
Требования:
- не трогать 9 дверей/10 leaf, `PilotSeat`, 11 `ModuleSlot`, `CargoVisual`, `EngineThrusterVisual`,
  `ContrailVFX`, 250 коллайдеров;
- **не блокировать будущую фрактуру**: сохранить тул/исходники для повторной нарезки; если сомнения —
  сначала A, B только по согласованию с Lead;
- assets в `Generated/Ships/HeavyII_VIS_*` (git), prefab — Lore, не scene-overrides.

### C. Теневой бюджет корабля (0.5–1 день, визуальный риск)

На 200 м `shadowCasters=593`; если выключить тени **только мелкой статики/палубы** дальше ~100–150 м
(не всего корабля), DC может упасть ниже 1 300. Проверить визуал теней на палубе/корпусе; не менять
URP `shadowDistance`.

### D. Принять 1 538 и изменить gate (только по решению пользователя)

Если A/B/C дают визуальный регресс, зафиксировать 1 538 как принятый P2-результат и **не запускать**
future scale-out без отдельного решения. Это не «победа» — это остановка с данными.

**Рекомендация:** сначала A + замер/визуал; если 200 м LOD1 неприемлем — B (или C как временный).

---

## 3. Методика замеров

- Play Mode → Host; `Stage3ShipBench` можно расширить/переиспользовать; камера на 80/120/200/300/400/1200 м.
- Midday forced; far 30 000; fov 60; `lodBias` фактический 1.30; медиана 3 сэмплов; warmup 1500.
- `ship_dc = dc_all − dc_withoutShip`; повторить 200 м дважды (±5 %).
- Frame Debugger: состав проходов; двери/слоты/cargo/VFX; F8/F9 — по возможности реальным вводом
  (если снова `no_rebase_plan`, записать).
- Сцену не сохранять; `isDirty=false` до/после; prefab/FBX не менять кроме разрешённого префаба.

---

## 4. Приёмка Stage 3c

- [ ] heavy на **200 м, полный корабль в кадре: ≤1 300 DC** (или письменно согласованный иной gate);
- [ ] LOD1/LOD2 не хуже 235/104; LOD-переходы без «дыр» (визуально);
- [ ] 9 дверей/10 leaf/250 Collider/11 ModuleSlot/CargoVisual/VFX целы; reimport FBX OK;
- [ ] **другие корабли 0 diff**; сцена чистая; commit `T-CULL01/H2b` (assets+script+CSV);
- [ ] отчёт Lead: до/после DC, выбранный вариант, визуальные замечания, что осталось.

**Kill/stop:** визуальный регресс на 200 м → откат; двери/слоты ломаются → откат; reimport ломает →
stop; вариант B без согласования не делать.

---

## 5. Жёсткие правила

1. Только HeavyII; medium/light/NPC не трогать.
2. Не менять сцену, `TagManager`, URP/ProjectSettings, `ShipPositions.json`.
3. Prefab/FBX — Lore; backup вне Assets; `.asset`/скрипт/CSV — в git.
4. Не `SetActive`, не `Camera.main`, не scene-overrides; FO-safe.
5. P3/фрактуру не начинать.
