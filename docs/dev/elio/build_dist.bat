@echo off
setlocal
cd /d "%~dp0"
set "DIST=dist"

call :copyFile "elio.html" "%DIST%\index.html"
call :copyFile "elio_engine.js" "%DIST%\elio_engine.js"
call :copyFile "elio_ai.js" "%DIST%\elio_ai.js"
call :copyFile "elio_recorder.js" "%DIST%\elio_recorder.js"
call :copyFile "vendor\three\elio-three.bundle.js" "%DIST%\vendor\three\elio-three.bundle.js"

for %%F in (
  "Minus-Corvette.fbx"
  "Minus-Destroyer.fbx"
  "Minus-Frigate.fbx"
  "Minus-HeavyCruiser.fbx"
  "Minus-Linear.fbx"
  "Plus-Corvette.fbx"
  "Plus-Destroyer.fbx"
  "Plus-Frigate.fbx"
  "Plus-HeavyCruiser.fbx"
  "Plus-Linear.fbx"
  "Zero-CaptainLinear.fbx"
  "Zero-Courier.fbx"
  "Zero-Divergent.fbx"
  "Zero-Engineer.fbx"
  "Zero-Free.fbx"
) do call :copyFile "3d\export\%%~F" "%DIST%\3d\export\%%~F"

echo.
echo Готово. Содержимое папки dist можно загружать на хостинг.
pause
exit /b 0

:copyFile
if not exist "%~1" (
  echo ! Не найден файл: %~1
  exit /b 1
)
if exist "%~2" (
  fc /b "%~1" "%~2" >nul 2>&1
  if not errorlevel 1 (
    echo = Без изменений: %~2
    exit /b 0
  )
)
for %%D in ("%~2") do if not exist "%%~dpD" mkdir "%%~dpD"
copy /y "%~1" "%~2" >nul
if errorlevel 1 (
  echo ! Не удалось обновить: %~2
  exit /b 1
)
echo + Обновлено: %~2
exit /b 0
