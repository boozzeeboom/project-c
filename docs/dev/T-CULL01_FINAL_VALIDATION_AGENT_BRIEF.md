# T-CULL01 / Final Validation — brief для отдельного агента (закрытие этапа)

**Дата:** 2026-11-01 · **Проект:** `C:\Projects\ProjectC_client` · **HEAD:** `7d700416`
**Scope:** **только валидация** уже сделанного. Никакой новой оптимизации, никаких B2/Stage 5/6/7,
никаких кораблей/medium/light/NPC.
**Play Mode:** нужен; запускать только с разрешением пользователя. Сцену не сохранять, push не делать.

**Прочитать:**
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_STAGE4B_REPORT.md`
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_STAGE4_REPORT.md`
- `C:\Projects\ProjectC_client\docs\world\primum_city\10_CULLING_RESEARCH\10_EXECUTION_PLAN.md` §6.1
- `C:\Projects\ProjectC_client\docs\dev\T-CULL01_COMPLEX_CODE_HANDOFF.md` §6.5

---

## 0. Где мы сейчас

- Stage 4b: **P1 3 047 → 2 423 DC** при `CityShadowBudget` 200 м; гейт **≤2 500 взят** (запас 3.1 %).
- 150 м даёт 2 302; `m_SmallMeshScreenPercentage` оставлен 0 (0.5 % ухудшает, 1–2 % дают pop-in).
- Atlas/submesh-мерж = 0; **B2 dry-run = `B2_increases`** (330 батчей против 160) → Stage 5 закрыт.
- В игру добавлен `Assets/_Project/Scripts/World/CityShadowBudget.cs` (auto-bootstrap; 200/180 м; ~3 Гц;
  исключает корабли и слой дверей; `Application.isPlaying`-guard).
- **Визуально пользователь проверил 01.11 — «вроде всё окей»**, это sign-off по теням.
- **Не проверено:** steady state самого компонента (2 423 — A/B-полигон бенча, не отдельный прогон
  с компонентом); F8/F9 реальным вводом; C3 шумит; закрытие T-CULL01.

**Цель:** подтвердить, что **реальная игра** с автозапущенным `CityShadowBudget` держит ≤2 500 DC,
F8/F9 работают, сцена/ассеты не загрязнены — и закрыть этап.

---

## 1. Задачи

### 1.1. Steady state `CityShadowBudget` (главное)

- Play Mode → Host, Midday, far 30 000, fov 60, текущие настройки (GRD on, тени 400/4, `lodBias` фактический).
- **Корабли не выключать** в этой проверке (реальная игра); если heavy/medium/light мешают в C1 —
  записать их DC отдельно, но **не менять** prefab/сцену.
- В логе должно появиться `[CityShadowBudget] Рендереров города: …` — ожидается **≈1 482** (не 859).
  Записать число; если <1 200 — компонент собрал не всё, это дефект.
- C1 площадь: `UnityStats.drawCalls`; сравнить с **2 423**; повтор ±5 %. Ожидается ≤2 500.
- C2 улица / C3 крыша: записать; C3 сравнить с 1 011 (Stage 4b) — шум/регрессия.
- Если P1 >2 500: **не менять код вслепую**; проверить, что `CityShadowBudget` включён, 200/180 м,
  кандидаты собраны; записать; возможный fallback — 150 м (2 302) только с разрешения пользователя.
- Сохранить CSV/лог `Q_final_validation_<date>.csv/.txt`.

### 1.2. F8/F9 реальным вводом (долг Stage 0/3)

- Вывести фокус/игрока за порог **>256 м** от кванта, нажать **F8**:
  - 0 errors; `WorldRoot` сдвинулся; объекты/двери/корабль на месте; `transactionsSeen` не пустой.
- **F9**: возврат, 0 errors, объекты не «уехали».
- Если снова `no_rebase_plan` — записать и указать, что долг остаётся; не «симулировать» успех.

### 1.3. Загрязнение сцены/ассетов

- После выхода из Play Mode: `WorldScene_0_0` `isDirty=False`; `git status` чистый (кроме разрешённых
  отчётов); `m_CastShadows` в сцене не появились (Stage 2-регресс не вернулся).
- `CityShadowBudget` в edit mode ничего не пишет; prefab/FBX не менялись.
- `ProjectC_URP.asset`/`GraphicsSettings.asset` — текущие GRD/тени; не менять.

### 1.4. Регрессии

- 0 shader errors; Hybrid Batch Group (`hybridBatcherDrawCalls > 0`); двери/створки/тени/эмиссия ок.
- Корабли: heavy/medium/light/NPC не изменены (0 diff); в этой проверке их можно не выключать.
- C3: если рост >5 % устойчив — записать как открытый пункт, не блокировать закрытие без решения Lead.

### 1.5. Документация и закрытие

- Отчёт `docs/dev/T-CULL01_FINAL_VALIDATION_REPORT.md`: commit, числа, лог кандидатов, F8/F9, C3,
  что осталось.
- Обновить трекер в `10_EXECUTION_PLAN.md`: Stage V ✅; T-CULL01 можно закрыть (или открытые пункты).
- Коммит `T-CULL01/V: final validation` — только docs/CSV; без кода, если не найден баг.
- Push не делать.

---

## 2. Приёмка

- [ ] реальная игра с `CityShadowBudget` даёт **P1 ≤2 500** (ожидается 2 423 ±5 %);
- [ ] лог `CityShadowBudget` показывает ≈1 482 кандидата (не 859);
- [ ] F8/F9 реальным вводом: 0 errors, объекты на месте (или зафиксировано `no_rebase_plan`);
- [ ] C3 перемерен; рост/шум зафиксирован;
- [ ] сцена не сохранена; `git status` чистый; prefab/FBX/корабли не менялись;
- [ ] 0 shader errors; Hybrid Batch Group есть; двери/тени ок;
- [ ] отчёт + коммит `T-CULL01/V`; трекер обновлён.

**Kill/stop:** P1 >2 500 и компонент собрал <1 200 кандидатов → баг, отчёт; не менять код без согласования.
F8/F9 снова `no_rebase_plan` → записать, не блокировать остальную валидацию; это отдельный долг.

---

## 3. Жёсткие правила

1. Никакой новой оптимизации; не делать B2/Stage 5/6/7; не трогать корабли/medium/light/NPC.
2. Не менять prefab/FBX/сцену; сцену не сохранять; не push.
3. `ProjectSettings`/URP-ассет — не менять (валидация текущего состояния).
4. Не `SetActive`, не `Camera.main`, не scene-overrides; FO-safe.
5. Если нужен фикс кода — сначала отчёт Lead/пользователю, не «по пути».

---

## 4. Абсолютные входы

- `C:\Projects\ProjectC_client\Assets\_Project\Scripts\World\CityShadowBudget.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Culling\Stage4bGapBench.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Editor\Culling\Stage4GrBench.cs`
- `C:\Projects\ProjectC_client\Assets\_Project\Settings\ProjectC_URP.asset` (86–88, 58–59)
- `C:\Projects\ProjectC_client\ProjectSettings\GraphicsSettings.asset` (58)
- `C:\Projects\ProjectC_client\Assets\_Project\Scenes\World\WorldScene_0_0.unity`
- Данные: `docs/world/primum_city/10_CULLING_RESEARCH/08_DATA/P_stage4b_*`, `O_stage4_*`
- Отчёты: `docs/dev/T-CULL01_STAGE4B_REPORT.md`, `T-CULL01_STAGE4_REPORT.md`
