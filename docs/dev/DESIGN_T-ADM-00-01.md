# T-ADM-00/01 — дизайн-нота: удаление legacy HUD + слот AdminPanel

> Дата: 2026-09-15. Решение D1/D6 из `docs/world/admin_tool/00_ADMIN_PANEL_SURVEY.md §7`.

## T-ADM-00: что удаляем и почему безопасно

- `Ship/ShipDebugHUD.cs` (+ `.meta`, GUID `185709c7…`): F3-HUD, по S-HUD-05 заменён `ShipHudController`.
- `Ship/MeziyStatusHUD_Legacy.cs` (+ `.meta`, GUID `2a09c22a…`): F4-HUD, тот же статус (шапка файла: «LEGACY — будет заменён»).
- Проверено 2026-09-15: GUID обоих файлов **не встречаются** ни в одном `.unity`/`.prefab`/`.asset`
  (grep по `Assets/` пуст); по имени типа ссылаются только `ShipController.cs` (код ниже)
  и комментарии в `ShipHudController.cs:9-10`, `StreamingTest_AutoRun.cs:151` (не трогаем — комментарии).
- В `Player/ShipController.cs` удаляем 3 места:
  1. Поле `_showLegacyMeziyHud` (`:429-431`, `[Header("Legacy HUD…")]` + `[Tooltip]` + поле).
  2. Вызов `InitializeDebugHUD();` (`:555-556` + комментарий `:554`).
  3. Метод `InitializeDebugHUD()` целиком (`:2177-2211` со summary).
- Удаление файлов — через MCP `delete_file` (Unity прибьёт `.meta` сам); руками `.meta` не трогаем.
- После: `refresh_unity` (force, compile, wait) → `read_console` (0 errors).
  Ожидаемый риск: нулевой — guarded ветка и так не выполнялась (`_showLegacyMeziyHud=false` default).

## T-ADM-01: слот AdminPanel в InputBindingsConfig

- `Input/InputBindingsConfig.cs`: в конец `enum GameAction` (после `CameraZoom`, `:128`) —
  `AdminPanel // F12`; в конец `DefaultBindings` (после CameraZoom-записи) —
  `{ action = GameAction.AdminPanel, category = ActionCategory.Debug, key = Key.F12, … "F12" }`.
- Точный текст записи копируем из соседних Debug-записей (`:182-183`) — читаем файл перед правкой.
- Ничего больше: чтение слота будет в T-ADM-04 (окно). `PlayerInputReader` (dead code) не трогаем.
- Проверка та же: refresh + console, плюс `KeybindingsWindow` должен показать новую строку F12.
