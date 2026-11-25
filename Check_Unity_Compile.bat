@echo off
setlocal EnableDelayedExpansion

set "PROJECT_PATH=%~1"
if "!PROJECT_PATH!"=="" set "PROJECT_PATH=%~dp0"
if "!PROJECT_PATH:~-1!"=="\" set "PROJECT_PATH=!PROJECT_PATH:~0,-1!"

set "UNITY_EXE="
for /d %%D in ("C:\Program Files\Unity\Hub\Editor\*") do (
    if exist "%%D\Editor\Unity.exe" set "UNITY_EXE=%%D\Editor\Unity.exe"
)

if "!UNITY_EXE!"=="" (
    echo [INFO] Unity.exe not found in C:\Program Files\Unity\Hub\Editor\
    echo Open the project in Unity Hub manually and run Ogni -^> Build World Scene.
    exit /b 0
)

echo Found Unity: "!UNITY_EXE!"
echo Checking compilation for "!PROJECT_PATH!"...
set "LOG_FILE=!PROJECT_PATH!\compile_check.log"

"!UNITY_EXE!" -batchmode -nographics -quit -projectPath "!PROJECT_PATH!" -logFile "!LOG_FILE!"

findstr /C:"error CS" "!LOG_FILE!" >nul
if !errorlevel!==0 (
    echo.
    echo [COMPILE ERRORS]:
    findstr /C:"error CS" "!LOG_FILE!"
    exit /b 1
) else (
    echo.
    echo [SUCCESS] Zero C# compile errors! You can open Unity and press Play.
    exit /b 0
)
