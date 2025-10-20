@echo off
echo.
echo ===============================================
echo   KHShuffler OPUS File Solution Guide
echo ===============================================
echo.
echo Your .ogg files are OPUS format (not OGG Vorbis).
echo KHShuffler currently supports: WAV, MP3, OGG Vorbis
echo.
echo IMMEDIATE SOLUTIONS (No installation required):
echo.
echo [1] Online Converters (Easiest):
echo     - Go to: cloudconvert.com
echo     - Upload your .ogg files
echo     - Convert from OPUS to MP3 or OGG Vorbis
echo     - Download and place in sounds folder
echo.
echo [2] Free Software (Audacity):
echo     - Download Audacity from audacityteam.org
echo     - Open each .ogg file in Audacity
echo     - File -^> Export -^> Export as MP3
echo.
echo [3] VLC Media Player (If you have it):
echo     - Media -^> Convert/Save
echo     - Add your .ogg files
echo     - Choose MP3 output format
echo.
echo [4] For Advanced Users (FFmpeg):
echo     - Install FFmpeg from ffmpeg.org
echo     - Run: convert_opus_files.bat
echo.
echo ===============================================
echo.
echo QUICK TEST: Let's check your first few files...
echo.

if not exist "sounds" (
    echo ERROR: sounds folder not found!
    echo Make sure you're running this from BetterGameShuffler folder.
    pause
    exit /b 1
)

set /a count=0
for %%f in ("sounds\*.ogg") do (
    if !count! LSS 5 (
        echo File: %%~nxf
        echo   Size: %%~zf bytes
        echo   Likely format: OPUS ^(needs conversion^)
        echo.
        set /a count+=1
    )
)

if %count% EQU 0 (
    echo No .ogg files found in sounds folder.
) else (
    echo Found %count% .ogg files that likely need conversion.
)

echo.
echo ===============================================
echo   RECOMMENDED QUICK SOLUTION:
echo ===============================================
echo.
echo 1. Go to: https://cloudconvert.com
echo 2. Select "OPUS to MP3" converter
echo 3. Upload 2-3 of your .ogg files as a test
echo 4. Download the converted MP3 files
echo 5. Place them in your sounds folder
echo 6. Test sound effects in KHShuffler
echo 7. If they work, convert the rest!
echo.
echo This should take about 5 minutes for a few files.
echo.
pause