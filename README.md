# Better Game Shuffler - Release Package

## Package Contents
- `BetterGameShuffler.exe` - Main application (self-contained)
- `COMPLETE_SUCCESS_DOCUMENTATION.md` - Full technical documentation
- `README.md` - Quick start guide
- `steam_appid.txt` (KH1.5+2.5) - Place this in your game installation folder to open the games directly (this is required to bypass the launcher and have multiple games open at the same time)
- `steam_appid.txt` (2.8)

## Quick Start

### 1. Launch the Application
- Run `BetterGameShuffler.exe`

### 2. Add Your Games
1. Start your games first, I recommend clicking on New Game in each game to prevent accidentally pressing "Back" on the title screen during shuffling
2. Click "Refresh" to scan for running games
3. Select games from the list and click "Add"
4. Games will be automatically color-coded by engine type:
   -  Blue = Melody of Memory (Unity-specific suspension)
   -  Red = KH1FM, KH Re:CoM, KH2FM, KHBBSFM, KHDDD (gentle priority-only suspension)
   -  White = KH0.2, KH3, KH 358/2 Days (MelonMix), KH Re:coded (MelonMix), KH CoM (Bizhawk), KH Dark Road (Bluestacks) (standard thread suspension)
5. If you're playing GBA Chain of Memories (through Bizhawk) or Dark Road (through Bluestacks), make sure to set the graphics renderer to OpenGL. If you're using DirectX, the game will crash.
6. If you're playing Melody of Memory, you will need to pause and unpause the game if you're in the middle of the song to resync the music with the gameplay. I'm hoping to fix this in the future, but no promises.

### 3. Configure Settings
- **Min/Max Seconds**: Set the switching interval range
- **Force Borderless**: Automatically convert games to borderless fullscreen
- **Right-click games**: Cycle through suspension modes if needed

### 4. Start Shuffling
- Click "Start" to begin automatic game switching
- The application will minimize to taskbar
- Games will switch automatically at random intervals
- Click "Stop" anytime to restore all games
- If you close the shuffler without pressing Stop first, the games will remain permanently suspended, you'll have to use Task Manager to end their task

## Successfully Tested Games
- KINGDOM HEARTS FINAL MIX (Square Enix)
- KINGDOM HEARTS Chain of Memories (Bizhawk, using OpenGL)
- KINGDOM HEARTS Re:Chain of Memories (Square Enix)
- KINGDOM HEARTS II FINAL MIX (Square Enix)
- KINGDOM HEARTS 358/2 DAYS (MelonMix)
- KINGDOM HEARTS Birth by Sleep FINAL MIX (Square Enix)
- KINGDOM HEARTS Re:coded (MelonMix)
- KINGDOM HEARTS Dream Drop Distance (Square Enix)
- KINGDOM HEARTS 0.2 Birth by Sleep (UE4)
- KINGDOM HEARTS III (UE4)
- KINGDOM HEARTS Melody of Memory (Unity)
- KINGDOM HEARTS Dark Road (Bluestacks, using OpenGL)
  

##  Troubleshooting

### Focus Issues
- Try stopping and restarting the shuffler and re-adding all of the games 
- Ensure no antivirus is blocking window manipulation
- Check that games are not running in exclusive fullscreen mode
- Sometimes you'll have to click into the game manually (this should only happen once per game)

**Compatibility**: Windows 10/11 x64










