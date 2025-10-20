@echo off
echo ========================================
echo  BETTER GAME SHUFFLER - ADMIN MODE
echo ========================================
echo.
echo This will launch the Game Shuffler with elevated administrator privileges.
echo This is required for advanced features like GPU-accelerated Mirror Mode.
echo.
echo Features enabled with admin privileges:
echo   - Desktop Duplication API (GPU Mirror Mode)
echo   - System-level display transformations
echo   - Hardware-accelerated screen capture
echo.
pause
echo.
echo Launching with administrator privileges...
echo.

REM Change to the correct directory
cd /d "%~dp0bin\Debug\net8.0-windows\win-x64"

REM Check if the executable exists
if not exist "KHShuffler.exe" (
    echo ERROR: KHShuffler.exe not found!
    echo.
    echo Current directory: %CD%
    echo Looking for: KHShuffler.exe
    echo.
    echo Please build the project first by running:
    echo   dotnet build --configuration Debug
    echo.
    echo Or build in Visual Studio (Ctrl+Shift+B)
    pause
    exit /b 1
)

REM Launch with admin privileges
echo Starting KHShuffler.exe with elevated privileges...
powershell -Command "Start-Process 'KHShuffler.exe' -Verb RunAs"

echo.
echo Application launched! Check if the new window opened with admin privileges.
echo You should see the Desktop Duplication API initialize successfully now.
echo.
pause