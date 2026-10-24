# T-CHAR-UVRB — переразвёртка UV + перепечка BaseColor AI-персонажей

Цель: превратить рваный атлас сгенерированного персонажа (сотни мелких островов) в упорядоченную
развёртку с крупными островами и равномерной плотностью текселей — пакетно, для всех персонажей.

- Алгоритм, исследование и замеры: [01_ALGORITHM.md](01_ALGORITHM.md)
- История итераций: [ITERATIONS.md](ITERATIONS.md)
- Код: `Tools/Blender/CharacterRebake/`

| файл | назначение |
|---|---|
| `rebake_character.py` | один персонаж: сегментация → развёртка → перепечка → экспорт → QA |
| `batch_rebake.py` | прогон по папкам `Assets/Generated_Models/*` (каждый персонаж — отдельный процесс Blender) |
| `rebake_all.bat` | обёртка для Windows |
| `probe_mesh.py` | диагностика модели (read-only): грани, UV-острова исходника, оболочки |
| `exp_smart_uv.py` | эксперимент «Smart UV по мешу vs по сглаженному прокси» (обоснование алгоритма) |

## Запуск

Требуется Blender 5.2 (проверено 5.2.3 LTS, `W:\SteamLibrary\steamapps\common\Blender\blender.exe`;
другой путь — переменная `BLENDER_EXE`). Работает headless, GUI Blender и Blender MCP не нужны.

```bat
rem все персонажи серии PC_Char_*
Tools\Blender\CharacterRebake\rebake_all.bat "PC_Char_*"

rem все папки, с параметрами
Tools\Blender\CharacterRebake\rebake_all.bat "*" "--res 4096 --gpu"

rem один персонаж с сохранением рабочего .blend (ручная доводка швов)
"%BLENDER_EXE%" -b --factory-startup --python-exit-code 1 -P Tools\Blender\CharacterRebake\rebake_character.py -- ^
  --src Assets\Generated_Models\<Name>\<Name>.fbx --save-blend D:\work\<Name>_rebake.blend
```

Уже обработанные папки пропускаются (по наличию отчёта); повтор — `--force`
(в `rebake_all.bat` третьим аргументом). Исключить папки: `--exclude "PadRing,CargoPlatform_*"`
(не-персонажи из `Generated_Models` фильтровать паттерном/исключением).

## Что получается

```
Assets/Generated_Models/<Name>/Rebaked/<Name>_Rebaked.fbx            (или .glb для .glb-источника)
Assets/Generated_Models/<Name>/Rebaked/<Name>_Rebaked_BaseColor.png
Tools/Blender/CharacterRebake/_reports/<Name>/<Name>_Rebaked_report.json   метрики, параметры, тайминги, round-trip
Tools/Blender/CharacterRebake/_reports/<Name>/<Name>_Rebaked_log.txt
Tools/Blender/CharacterRebake/_uvrb_summary.csv                     сводка по всем прогнанным
```

Исходные FBX/текстура не изменяются. `Assets/Generated_Models/*` в `.gitignore` — результаты
в Git не попадают. `_reports/` и `_uvrb_summary.csv` — рабочие артефакты, не коммитить.

## Основные параметры `rebake_character.py`

| параметр | по умолч. | смысл |
|---|---|---|
| `--res` | 2048 | разрешение итоговой текстуры |
| `--supersample` | 2 | печь в `res×ss`, затем уменьшить |
| `--samples` | 4 | Cycles samples печки |
| `--smooth-iters` | 600 | сглаживание прокси для сегментации (больше → крупнее острова) |
| `--angle` | 66 | angle limit Smart UV на прокси |
| `--min-island-frac` | 0.004 | острова меньше этой доли площади сливаются с соседом |
| `--border-smooth` | 12 | сглаживание границ островов (0 — выкл) |
| `--unwrap` | MINIMUM_STRETCH | `ANGLE_BASED` / `CONFORMAL` / `MINIMUM_STRETCH` |
| `--margin-px` / `--bleed-px` | 8 / 16 | зазор упаковки / заливка за край острова, px |
| `--gpu` | выкл | печь на GPU |
| `--save-blend` | — | сохранить рабочий `.blend` со швами |

## Как читать отчёт

`uv_src` / `uv_new`: `islands`, `islands_lt_50_faces`, `seam_length`, `uv_coverage`,
`flipped_faces`, `texel_density_cv`. Признаки проблем: `non_disk_charts_left > 0`,
`flipped_faces` более ~0.1 % граней, `roundtrip.ok = false`, `texel_density_cv` выше исходника.

## Проверка пользователем (NOT RUN)

Unity-редактор во время работы был отключён — импорт результата в Unity не проверялся.

1. Unity: `Assets/Generated_Models/PC_Char_City_LowerCraftsman_APOSE_v02/Rebaked/` импортируется без ошибок;
   у FBX материал подхватил `_Rebaked_BaseColor.png` (если нет — назначить текстуру вручную
   или вынести материал в `.mat`).
2. Масштаб/ориентация `_Rebaked.fbx` совпадают с исходным FBX (положить рядом в сцену).
3. Визуально: нет видимых швов/полос на лице, кистях, у пояса; нет «размытых» зон.
4. Остальные персонажи: прогнать `rebake_all.bat`, просмотреть `_uvrb_summary.csv`.
