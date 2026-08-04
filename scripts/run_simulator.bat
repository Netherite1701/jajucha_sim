@echo off
REM run_simulator.bat - Launch the standalone simulator build (Step 11.22)
REM
REM Usage:
REM   scripts\run_simulator.bat [--course NAME] [--mode MODE] [--simulation-speed N] [--no-debug-ui] [--batch-config FILE]
REM
REM Example:
REM   scripts\run_simulator.bat --course template_course --mode Drive

setlocal
set ROOT=%~dp0..

set EXE=
if exist "%ROOT%\dist\JajuchaSimulator\JajuchaSimulator.exe" set EXE=%ROOT%\dist\JajuchaSimulator\JajuchaSimulator.exe
if exist "%ROOT%\Builds\JajuchaSimulator\JajuchaSimulator.exe" set EXE=%ROOT%\Builds\JajuchaSimulator\JajuchaSimulator.exe
if exist "%ROOT%\Build\JajuchaSimulator\JajuchaSimulator.exe" set EXE=%ROOT%\Build\JajuchaSimulator\JajuchaSimulator.exe

if "%EXE%"=="" (
    echo [run][WARN] No standalone build found. Run scripts\build_windows.ps1 first.
    echo [run][WARN] Or open Assets/JajuchaSim/Scenes/JajuchaSimulator.unity in Unity and press Play.
    exit /b 1
)

echo [run] Launching simulator: %EXE% %*
"%EXE%" %*
exit /b %ERRORLEVEL%
