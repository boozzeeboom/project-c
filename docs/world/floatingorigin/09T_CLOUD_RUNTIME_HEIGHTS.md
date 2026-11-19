# T-FO09T — Высоты облаков в rebased world space

## Дефект

`VolumetricCloudsRenderFeature` и `StormCellDirector` передавали в шейдер authored-высоты слоёв (`800…1200` и т. п.), тогда как камера, игрок и корни `WorldScene_*` после ребейза находятся в сдвинутом world space. При `T.y = -2500` authored-слой `800` должен иметь runtime-высоту `-1700`; оставлять его на `800` разрывает относительную геометрию.

Дополнительно `CumulativeRebaseOffset` обновлялся на сервере, но не в клиентском `OnRebaseShiftMessage`.

## Контракт

- Serialized `BottomY`, `TopY`, `CellBottomY`, `CellTopY` — authored-настройки исходного мира. Их не мутировать при F8/F9.
- `runtimeY = authoredY + GlobalMotionControlledRebaseSlice.CumulativeRebaseOffset.y`.
- Raymarch, камера, `LocalDensityBuffer`, центры штормовых ячеек и debug-геометрия остаются в rebased world space.
- На клиенте кумулятив обновляется из success/rollback broadcast до вызова локальных `ApplyRebaseTranslation`, чтобы немедленный shader push видел актуальный origin.
- Veil сохраняет прежний механизм `ApplyRebaseTranslation`; второй коррекции ему не требуется.

## Проверка

Реализация импортирована через anklebreaker MCP вне Play Mode: компиляция завершена, Console — 0 errors. Runtime и визуальная проверка не запускались агентом.

Ручная проверка:

1. До F8 записать высоту игрока и выбранного облачного слоя.
2. После F8 обе runtime-высоты должны измениться на один и тот же `T.y`; `playerY - cloudLayerY` должно сохраниться.
3. F9 должен вернуть исходные runtime-высоты без накопления ошибки.
4. Повторить с активной штормовой ячейкой: столб, маркер и молнии остаются на прежней высоте относительно игрока.
5. Проверить Veil отдельно и повторить основной сценарий на втором клиенте.
