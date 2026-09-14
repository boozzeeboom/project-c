# Ретроспектива: FO-интеграция + свет (57572dc0 → 26f1cb75)

Период: 08.09.2026 → 14.09.2026. Коммитов: **263** (из них ~245 с тегом FO, ~10 с тегом LIGHT).
Диапазон: `57572dc0 T-JITTER17` → `26f1cb75 T-FO09Q`.
Суммарный дифф диапазона: **577 файлов, +116 748 / −10 868** (net +105 880 строк, включая сцены, JSON, .meta).

## 1. Floating Origin — большая интеграция

### 1.1 Код (C#)

| Категория | Файлов | Добавлено строк | Удалено |
|---|---|---|---|
| Runtime `Assets/_Project/Scripts/` (numstat по маске) | 163 пути (включая .meta) | **+18 237** | −107 |
| из них новых `.cs` (весь репо: 153 новых .cs, 26 изменённых) | ~101 новых runtime .cs | — | — |
| Editor-тулзы `Assets/_Project/Editor/` (валидаторы/аудиты FO) | **52 новых `.cs`** (+.meta, всего 72 пути) | **+9 409** | 0 |
| **Итого C#** (маска `*.cs`) | **179 файлов, 153 новых + 26 изменённых** | **+28 750** | **−110** |

Ключевое: почти весь runtime-код — новый (контракты, слайсы, участники rebase); старого кода изменено мало и точечно (26 файлов).

Изменённый старый код (неполный список, `M`):
- `Scripts/AI/NpcBrain.cs`, `NpcSocialBrain.cs` (08A/08B: warp runtime-NPC)
- `Scripts/Core/NetworkManagerController.cs`, `PickupDeckRide.cs`, `WindManager.cs`, `SpringArmCamera.cs`
- `Scripts/Core/ShipPosition/ShipPositionServer.cs`, `ShipPositionSaveData.cs` (PERSIST01–03: rebase-aware restore)
- `Scripts/Player/NetworkPlayer.cs`, `PlayerRespawnTracker.cs`, `ShipController.cs`
- `Scripts/Ship/ShipDeckNav.cs`, `AltitudeCorridorSystem.cs` (09M: сдвиг коридоров)
- `Scripts/UI/EscMenu/EscMenuWindow.cs`, `MainMenuWindow.cs`, `NetworkTestMenu.cs`
- Сцены/настройки: `WorldScene_0_0.unity` (+3413/−2285 — перенос Respawn_Default, освещение), `BootstrapScene.unity`, `ProjectC_URP.asset`, Netcode-настройки.

### 1.2 Документы (Markdown)

| Категория | Файлов | Добавлено строк |
|---|---|---|
| Всего `*.md` в диапазоне | **190 (186 новых + 4 изменённых)** | **+19 379 / −17** |
| из них `docs/world/floatingorigin/` | ~194 пути (310 с учётом переименований) | почти весь объём выше |
| ├ живые (после 09P-архивации) | README + PERSIST01 + 09H/09J/09K/09L/09M/09N/09O (~9 файлов) | — |
| └ `archive_floatingoriginintegration/` (процессный шум, заархивирован в 09P) | **185 файлов** | — |

Дополнительно вне FO: `docs/Character/*JITTER*`, `*SKINNING*` (исследования джиттера, смежные).

### 1.3 Тулзы (Editor-валидаторы)

**52 новых C# тулзы** в `Assets/_Project/Editor/FloatingOrigin/` + смежные (`tools/VerifyFo06BPilotPrefab.cs` и др.), ~9.4k строк.
Покрытие: аудит контрактов префабов, каталог сцен, readiness акторов, иерархия, network baseline, coordinator/admission/participant mapping, checkpoint capture/spawn/transactions, census миграции.

В 09Q удалены 6 orphan-контрактов — оставлен живой путь + фундамент v2.

### 1.4 Ход работ по тикетам (сжато)

- **06CU–06CZ**: границы ShipDeck identity, coordinator, native executor; `06CZ` — исключение неактивных корней из rebase scope (identity fault).
- **06DA–06DC / 06DD–06DG / 06DF**: network teleport publication, respawn threshold, camera history/collision, platform cache, deck-уведомления, rollback deathY.
- **07A–07J**: сдвиг frame origin (fail-closed), drain/rebind акторов, чистка particles, аудиты rigidbody/pool/boarding без кода, пилотируемый rebind, exempt сидящего игрока.
- **08A–08G**: NPC spawn-point + runtime-NPC участники с agent warp; вердикт по ship-адаптеру (defer); rollback broadcast; boarding combat pass.
- **PERSIST01–03**: rebase-aware restore + pilot spawn bridge; acceptance PASS.
- **09B–09Q**: spawn-anchor из любой сцены, rescue через ship-chain → возврат к default spawn (09N), auto threshold rebase (6 consecutive shifts PASS), altitude corridors (09M), wind zones (09L), rollback books (09K), spawn frame extent (09J), board-freeze fix (09H), полный ревью 09O, архив шума 09P, чистка контрактов 09Q.

### 1.5 Тесты

- **19 verify-коммитов** в диапазоне.
- **11 именованных F8/F9-прогонов**: f8_7, f8_8, f8_9, f8_10, f8_11, f8_12, f8_13, f8_28 (inconclusive → ship persistence map), f8_36 (healthy), f9_5, f9_14.
- Отдельно: **6 consecutive shifts** в `T-FO09B-verify` (auto threshold rebase PASS), boarding with controls + F-seat (09H-verify), save/load acceptance (PERSIST-verify), far-coordinate jitter cure (08F-verify), city NPCs (08B-verify).
- Финальная проверка 09O-verify (f8_36): healthy, noisy log audit — отключать нечего.

## 2. Свет — малая интеграция

- Коммиты: **10** (`T-LIGHT01` → `T-LIGHT09`, 08.09.2026).
- Состав: план + baseline + хеши итераций; `Per Pixel` для доп. источников; Lighting Settings + блокер bake; realtime-свет фонарей MD2 + хеш; фикс ночной экспозиции volume; глобальный контроль доп. света + хеш.
- Файлы: **3 дока** `docs/world/lights/` (plan, implementation-log, ITERATIONS) — **+357 / −5 строк**; плюс сцены/ассеты (`WorldScene_0_0.unity`, `LightingSettings_World.asset`, `ProjectC_URP.asset` — учтены в суммарном диффе выше).
- Отдельных тестовых прогонов вне хеш-фиксаций не было; верификация — визуальная по итерациям.

## 3. Итоговые цифры (для отчёта)

- FO: **~28.7k строк нового C#** (179 файлов: 153 новых, 26 изменённых старых) + **~19.4k строк доков** (190 md: 186 новых) + **52 Editor-тулзы** (~9.4k строк, входят в C#) + правки 2 сцен и настроек.
- Свет: **10 коммитов, 3 дока (~360 строк)**, точечные правки сцен/света.
- Тесты FO: **19 verify-коммитов, 11 именованных F8/F9-прогонов + серия из 6 shifts**, финальный статус healthy (f8_36).
- Завершающие: 09P (архив 185 процессных доков, README-индекс), 09Q (удаление 6 orphan-контрактов).

Проверено: `git diff --stat/--numstat 57572dc0..HEAD`, `git log --oneline 57572dc0..HEAD` (263 коммита), маски `*.cs` / `*.md` / `docs/world/floatingorigin/` / `Assets/_Project/Editor/`.
