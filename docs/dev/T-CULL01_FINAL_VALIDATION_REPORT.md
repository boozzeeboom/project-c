# T-CULL01 / Final Validation — отчёт (2026-11-01)

**База:** `f9dc332a` · **HEAD на момент прогона:** без коммита (агент не коммитил) ·
**Сцена:** `WorldScene_0_0` (не сохранялась) · **Push:** нет.
**Бенч:** `Assets/_Project/Editor/Culling/FinalValidationBench.cs`
**Данные:** `08_DATA/Q_final_validation_2026-11-01.csv`, `Q_final_validation_2026-11-01_raw.txt`.

## 1. Итог: гейт НЕ подтверждён

| Конфигурация | P1 DC | Budget entries | shadowsOff |
|---|---:|---:|---:|
| Реальная игра, `CityShadowBudget` ON | **2 811** | **1 301** | 776 |
| Повтор | 2 811 | 1 301 | 776 |
| Budget OFF | 3 047 | 0 | 0 |
| Бенч Stage 4b (sb200, ожидание) | 2 423 | 1 482 | 921 |

**P1 2 811 > 2 500** — перебор **+311 DC (+12.4 %)**. Гейт Stage 4b/4 в реальной игре **не взят**.

Разница с бенчем объясняется **неполным набором кандидатов**: компонент собрал **1 301** рендерер
вместо 1 482 (−181). Вероятные причины (требуют диагностики):
- фильтр `r.shadowCastingMode == Off` в `Build()` (Stage 4b bench кандидатов считал иначе);
- момент `Build()`/загрузки мира (часть рендереров появляется позже; rebuild не срабатывает);
- набор камер в `RefreshCameras` (`AtmosphereCamera|MainCamera|ThirdPersonCamera_0` в логе) может
  давать другой union, чем один геймплейный `_cam` в бенче.

## 2. Остальные проверки

| Проверка | Результат |
|---|---|
| P1 с кораблями | 2 817 DC; компонент 1 305 entries, off 764 |
| C2 улица | 565 DC (budget off 573) |
| C3 крыша | 987 / 993 (off 1 011) — шум в пределах Stage 4b |
| F8 реальным вводом | **PASS**: сдвиг 256 м, WorldRoot/двери/корабли на месте, 0 errors |
| F9 реальным вводом | rollback `apply_refused:forced_user_validation_failure; restore=True`; bench записал `FAILED_no_transaction`, потому что ждал смену frameGen/кумулятива. **Критерий F9 в bench неверен**; отдельно подтвердить restore |
| Здоровье сцены | `sceneDirtyAtStart=False`, `sceneDirtyNow=False`, `sceneSaved=no`; prefab/FBX/корабли не менялись |
| Ошибки | errors=0, exceptions=0, shaderErrors=0; warnings=1042 |

## 3. Вывод

- **Stage 4b/бенч 2 423 — не подтверждён в реальной игре**: фактический компонент даёт 2 811.
- Нельзя закрывать T-CULL01 и переходить к Stage 6/7 с неверным baseline: сначала **Stage 4c** —
  привести набор кандидатов/камер/rebuild к паритету с бенчем и получить реальные ≤2 500; попутно
  исправить критерий F9 (rollback = успех, если restore прошёл).
- Stage 6 и Stage 7 **утверждены пользователем как фундамент** (не опциональны) и идут после Stage 4c.

## 4. Следующий шаг

`docs/dev/T-CULL01_STAGE4C_AGENT_BRIEF.md`: candidate parity + actual P1 ≤2 500 + F9 + C3.
