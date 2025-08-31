# Better Game Shuffler - Release Package

## Package Contents
- `BetterGameShuffler.exe` - Main application (self-contained)
- `COMPLETE_SUCCESS_DOCUMENTATION.md` - Full technical documentation
- `README.md` - Quick start guide
- `steam_appid.txt` (KH1.5+2.5) - Place this in your game installation folder to open the games directly (this is required to bypass the launcher and have multiple games open at the same time)
- `steam_appid.txt` (2.8)

## Quick Start

### 1. Launch the Application
- Double-click `BetterGameShuffler.exe`
- No .NET installation required (self-contained)

### 2. Add Your Games
1. Start your games first (they can run simultaneously)
2. Click "Refresh" to scan for running games
3. Select games from the list and click "Add Selected"
4. Games will be automatically color-coded by engine type:
   - ** Blue = Unity games** (advanced audio stopping)
   - ** Red = Square Enix games** (gentle priority-only)
   - ** White = UE4/Other games** (standard thread suspension)
   - ** Yellow = No-suspend mode** (minimal intervention)

### 3. Configure Settings
- **Min/Max Seconds**: Set the switching interval range
- **Force Borderless**: Automatically convert games to borderless fullscreen
- **Right-click games**: Cycle through suspension modes if needed

### 4. Start Shuffling
- Click "Start" to begin automatic game switching
- The application will minimize to taskbar
- Games will switch automatically at random intervals
- Click "Stop" anytime to restore all games

## Successfully Tested Games
- KINGDOM HEARTS FINAL MIX (Square Enix)
- KINGDOM HEARTS Re:Chain of Memories (Square Enix)
- KINGDOM HEARTS II FINAL MIX (Square Enix)
- KINGDOM HEARTS Birth by Sleep FINAL MIX (Square Enix)
- KINGDOM HEARTS Dream Drop Distance (Square Enix)
- KINGDOM HEARTS 0.2 Birth by Sleep (UE4)
- KINGDOM HEARTS III (UE4)
- KINGDOM HEARTS Melody of Memory (Unity)

## Key Features
- **Complete Audio Control**: Unity games are completely silent when suspended
- **Perfect Focus Management**: All games properly clickable when active
- **Automatic Engine Detection**: Smart classification with manual override
- **Borderless Conversion**: Automatic fullscreen experience
- **Efficient Performance**: Minimal system impact

##  Troubleshooting

### Game Not Switching Properly
- Check that the game appears in the target list with correct color coding
- Try right-clicking the game to cycle through suspension modes
- Ensure no modal dialogs are blocking the game window

### Audio Still Playing (Unity Games)
- Verify the game shows **blue background** (Unity mode)
- If not blue, right-click and cycle until it shows blue
- Check debug output for "100% thread suspension" messages

### Focus Issues
- Try stopping and restarting the shuffling session
- Ensure no antivirus is blocking window manipulation
- Check that games are not running in exclusive fullscreen mode

---
**Version**: 0.5.0 Beta Release  
**Tested**: August 31st, 2025  

**Compatibility**: Windows 10/11 x64




