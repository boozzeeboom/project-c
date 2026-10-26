@echo off
rem T-CHAR-UVRB v2: явный pattern, сохранный native .blend и opt-in FBX/GLB.
rem rebake_all.bat "PC_Char_*" "--max-res 4096" [--force]
setlocal
if "%BLENDER_EXE%"=="" set "BLENDER_EXE=W:\SteamLibrary\steamapps\common\Blender\blender.exe"
if "%~1"=="" (
  echo ERROR: specify an explicit folder pattern.
  exit /b 2
)
set "HERE=%~dp0"
"%BLENDER_EXE%" -b --factory-startup --python-exit-code 1 -P "%HERE%batch_rebake.py" -- --root "%HERE%..\..\..\Assets\Generated_Models" --pattern "%~1" --extra "%~2" %3 %4
set "RC=%ERRORLEVEL%"
endlocal & exit /b %RC%
