# T-CULL01 / Stage 3b — brief для отдельного агента (раскатка medium + light)

**Дата:** 2026-10-31 · **Проект:** `C:\Projects\ProjectC_client` · **База:** `4de67173`
**Scope:** `Ship_medium_base` + `Ship_Light_root_fbx_test`. **`Ship_havyII_base` — НЕ трогать.**
20 NPC — не трогать. Это первый scale-out после зелёного HeavyII-пилота.
**Play Mode:** нужен для замеров; запускать только с разрешением пользователя. Сцену не сохранять, push не делать.

**Прочитать:**
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_STAGE3C_REPORT.md`
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_STAGE3_REPORT.md`
- `C:\Projects\ProjectC_client\docs\dev\T-LIGHT01_SHADOW_CONFIG.md`
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_COMPLEX_CODE_HANDOFF.md` §3.6
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\10_EXECUTION_PLAN.md` §5.1

---

## 0. Предусловия (не начинать, если нет)

1. **Пользователь визуально принял** теневой бюджет HeavyII: на 150–300 м у статики корабля тень
   пропадает; на 200 м d200=1 170, d300=1 236 — визуал должен быть приемлем.
2. **Новый baseline при тенях `400 м / 4 каскада / soft on`** (T-LIGHT01). Старые цифры
   Stage 0/2 (`P1 27 131 → 6 094`) и Stage 3a (`heavy 1 538`) — **исторические, не сравнивать**.
3. Сцена чистая, `Ship_havyII_base` в текущем состоянии (616 MR + LODGroup + ShipShadowBudget), H1-R1 сохранён.
4. `Ship_medium_base` / `Ship_Light_root_fbx_test` не менялись после Stage 3 (mtime проверить).

Если визуального «да» по бюджету heavy нет — **остановиться** и отчитаться: раскатка бюджета теней
на medium/light без этого решения преждевременна.

---

## 1. Текущие данные

| Корабль | MR / slots | Коллайдеры | Особенности |
|---|---:|---|---|
| `Ship_havyII_base` | 616 / 604 LOD0 | 250 | **не трогать**; P2 закрыт; бюджет теней |
| `Ship_medium_base` | 118 / 118 | 3 Mesh + 3 Box | вложенный `Ship_CargoMedium_Base` 80 MR |
| `Ship_Light_root_fbx_test` | 428 / 428 | 2 Mesh + 15 Box | 25 материалов; 3 nested FBX |
| 20 NPC | 120 / 120 | — | **не трогать**; 6 MR/820 тр. каждый |

Ожидание: heavy 1 170/1 236 остаётся; medium/light после P1/P2 дают остаток; все корабли ≤2 500 DC
(P1/P2), после P3 heavy — ≤480; NPC 360 — не входят в бюджет.

---

## 2. Задача

### 2.1. Сначала — новый baseline (0.5 дня)

- Play Mode Host, Midday, far 30 000, fov 60, `lodBias` фактический (Medium 1.30).
- Точка у порта/города, полный HeavyII+medium+light в кадре; снять `ship_dc` A−B (как Stage 3/3c).
- Зафиксировать **текущий P1** (город + все корабли) при новых тенях; старый 6 094 не использовать.
- CSV `O_stage3b_<date>.csv`; он же baseline для Stage 4.

### 2.2. Обобщить пайплайн H2 (не дублировать)

`ShipVisualMerger.cs` жёстко зашит на `Ship_havyII_base` (PrefabPath/FbxPath/MeshPrefix/отчёт/меню).
Сделать **параметризованный** вариант для medium/light, не меняя поведение heavy:
- вынести конфиг класса корабля (prefab/FBX/mesh folder/prefix/отчёт);
- P1 greeble-мерж по тем же правилам; P2 LODGroup (пороги от габарита, не копировать 300/900 слепо);
- сохранить `ShipPrefabRepair`/H1-R1 и `ShipVisualMerger` heavy-путь рабочими;
- меню: `ProjectC/Ships/Visual Merge (medium/light)` — dry-run/apply/verify/rollback.

### 2.3. H1b — только если нужно

Medium/light **не проходили H1**. Перед мержем проверить:
- 3/15 BoxCollider внутри nested FBX? если reimport тест теряет коллайдеры — H1b (вынести user-боксы
  в prefab-корень) **до** мержа; source FBX/коллайдеры не ломать;
- `DoorController` на medium/light — если есть, не трогать; двери не клеить;
- H1b не делать «на всякий случай»: только по факту reimport.

### 2.4. Shadow budget для medium/light (если нужно для гейта)

`ShipShadowBudget` — generic, но `ShipShadowBudgetTool` зашит на heavy. Добавить компонент
на medium/light **только если их DC мешает цели ≤2 500**; параметры:
- `shadowOffDistance=150`, `shadowOnDistance=140`, `coverWholeLodGroup=true` — как heavy;
- визуально: тот же компромисс (тени статики пропадают дальше 150 м); предупредить пользователя;
- не трогать heavy.

### 2.5. Замер и приёмка

- Все 3 базовых корабля + 20 NPC: total ship DC при новых тенях; heavy должен остаться ~1 170/1 236.
- Повтор 200/300 м ±5 %; LOD1/2; двери/слоты/cargo/VFX/коллайдеры целы.
- Reimport FBX; F8/F9; сцена не сохранена; **heavy 0 diff**; NPC 0 diff.
- Commit `T-CULL01/H2c: medium+light scale-out`; assets+script+CSV; prefab/FBX — Lore, backup вне Assets.

---

## 3. Абсолютные входы

- `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\Ships\Ship_medium_base.prefab`
- `C:\Projects\ProjectC_client\Assets\_Project\Prefabs\Ships\Ship_Light_root_fbx_test.prefab`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Ships\ShipVisualMerger.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\Ship\ShipShadowBudget.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Ships\ShipShadowBudgetTool.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Ships\ShipPrefabRepair.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Culling\Stage3ShipBench.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Generated\Ships\` (сюда новые `.asset`, git)
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\08_DATA\I_ships_prefabs.csv`
- Backup: `C:\Projects\ProjectC_client\_local_backups\T-CULL01\Stage3b\` (вне git)

---

## 4. Приёмка Stage 3b

- [ ] visual sign-off heavy budget зафиксирован;
- [ ] новый baseline при тенях 400/4 снят (CSV);
- [ ] medium/light прошли P1/P2; heavy **0 изменений**; NPC **0 изменений**;
- [ ] **все корабли ≤2 500 DC** (P1/P2) при новых тенях; heavy ~1 170/1 236;
- [ ] двери/коллайдеры/слоты/cargo/VFX целы; reimport; F8/F9;
- [ ] сцена не сохранена; commit `T-CULL01/H2c`; отчёт Lead.

**Kill / stop:**
- нет visual sign-off heavy → не начинать;
- H1b ломает medium/light → rollback;
- total ships >2 500 без пути к цели → отчёт, не «дожимать» тенями вслепую;
- heavy изменился → откат.

---

## 5. Жёсткие правила

1. Только medium/light; heavy и NPC не трогать.
2. Не менять сцену, `TagManager`, URP/ProjectSettings, `ShipPositions.json`.
3. Prefab/FBX — Lore; backup вне Assets; `.asset`/скрипт/CSV — в git.
4. Не `SetActive`, не `Camera.main`, не scene-overrides; FO-safe.
5. P3/фрактуру не начинать.
