@echo off
echo ========================================
echo   Better Game Shuffler - Build Script
echo ========================================
echo.

echo Building Release Version...
dotnet publish BetterGameShuffler.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o "./Release"

echo.
echo ========================================
echo Build Complete!
echo ========================================
echo.
echo Output Location: ./Release/BetterGameShuffler.exe
echo.
echo Features:
echo - Self-contained executable (no .NET installation required)
echo - Optimized for Windows x64
echo - Single file deployment
echo - Engine-specific game suspension
echo - Support for Unity, UE4, and Square Enix games
echo.
echo Successfully tested with:
echo - KINGDOM HEARTS Melody of Memory (Unity)
echo - KINGDOM HEARTS III (UE4) 
echo - KINGDOM HEARTS 0.2 Birth by Sleep (UE4)
echo - KINGDOM HEARTS Dream Drop Distance (Square Enix)
echo - KINGDOM HEARTS Birth by Sleep FINAL MIX (Square Enix)
echo - KINGDOM HEARTS II FINAL MIX (Square Enix)
echo - KINGDOM HEARTS FINAL MIX (Square Enix)
echo - KINGDOM HEARTS Re:Chain of Memories (Square Enix)
echo.
pause