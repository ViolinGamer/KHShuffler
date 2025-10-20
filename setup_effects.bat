@echo off
echo Setting up Twitch Effects folders and test files...

:: Create directories
if not exist "images" mkdir "images"
if not exist "sounds" mkdir "sounds"
if not exist "hud" mkdir "hud"

echo Created effect directories:
echo - images/
echo - sounds/
echo - hud/

echo.
echo To use the Twitch Effects system:
echo 1. Add image files (PNG, JPG, GIF, WebP) to the 'images' folder
echo 2. Add sound files (WAV, MP3, OGG) to the 'sounds' folder  
echo 3. Add HUD overlay images (PNG, JPG, GIF, WebP) to the 'hud' folder
echo 4. A default HUD hide overlay will be created automatically
echo 5. Use the "Test Effects" button to test all effects locally
echo.
echo Enhanced Audio Format Support:
echo - WAV: Uncompressed, best compatibility
echo - MP3: Good compression, widely supported
echo - OGG: Excellent compression with high quality, open standard
echo.
echo Enhanced Image Format Support:
echo - PNG: Static images with transparency support
echo - JPG: Standard photo format for complex images
echo - GIF: Animated and static images with full animation support
echo - WebP: Modern format with smaller file sizes
echo   * Static WebP: Efficient compression for still images
echo   * Animated WebP: Superior to GIF with better compression and quality
echo.
echo Animation Support:
echo - GIF animations: Full support with frame detection
echo - WebP animations: Full support on Windows 10+ systems
echo - Automatic detection: Static vs animated formats detected automatically
echo - Frame information: Debug output shows animation details
echo.
echo Audio Format Recommendations:
echo - Use OGG for best quality-to-size ratio
echo - Use WAV for maximum compatibility
echo - Use MP3 for wide format support
echo - Mix and match formats in the same folder
echo.
echo Note: Animated WebP support requires Windows 10 (1903+) or Windows 11.
echo      Older systems will fall back to static display or skip WebP files.
echo      The HUD hide effect will create a default overlay
echo      if no 'hud_hide.png' file is found.
echo.
pause