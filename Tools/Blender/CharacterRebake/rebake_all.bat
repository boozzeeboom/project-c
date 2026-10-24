@echo off
rem T-CHAR-UVRB: пакетная переразвёртка UV + перепечка BaseColor для AI-персонажей.
rem Использование:  rebake_all.bat [pattern] [доп. аргументы rebake_character.py в кавычках]
rem   rebake_all.bat "PC_Char_*"
rem   rebake_all.bat "*" "--res 4096 --gpu"
rem Путь к Blender можно переопределить переменной BLENDER_EXE.
setlocal
if "%BLENDER_EXE%"=="" set "BLENDER_EXE=W:\SteamLibrary\steamapps\common\Blender\blender.exe"
set "PATTERN=%~1"
if "%PATTERN%"=="" set "PATTERN=PC_Char_*"
set "EXTRA=%~2"
set "HERE=%~dp0"
"%BLENDER_EXE%" -b --factory-startup -P "%HERE%batch_rebake.py" -- --root "%HERE%..\..\..\Assets\Generated_Models" --pattern "%PATTERN%" --extra "%EXTRA%" %3 %4
endlocal
