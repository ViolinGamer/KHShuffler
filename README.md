ANTIVIRUS FALSE POSITIVE NOTICE
===============================

KHShuffler may be flagged as "Trojan:Script/Wacatac.C!ml" by Windows Defender 
and other antivirus software. This is a FALSE POSITIVE.

WHY THIS HAPPENS:
- KHShuffler uses Windows APIs to pause/resume game processes
- These same APIs are used by malware, triggering heuristic detection
- The application is legitimate and safe

TO FIX:
1. Add KHShuffler.exe to your antivirus exclusions
2. Windows Defender: Settings > Virus & threat protection > Exclusions
3. Add the entire KHShuffler folder as an exclusion

The source code is available on GitHub for verification.

# KHShuffler - Release Package

## Package Contents
- `KHShuffler.exe` - Main application
- `README.md` - Quick start guide
- `steam_appid1.5+2.5.txt` - Place this in your game installation folder for Kingdom Hearts HD 1.5 + 2.5 ReMIX and rename it to steam_appid.txt to open the games directly through their exe (this is required to bypass the launcher and have multiple games open at the same time)
- `steam_appid2.8.txt` - Place this in your game installation folder for Kingdom Hearts HD 2.8 Final Chapter Prologue and rename it to steam_appid.txt to open the games directly through their exe (this is required to bypass the launcher and have multiple games at the same time)

## Quick Start

### 1. Launch the Application
- Run `KHShuffler.exe`

### 2. Add Your Games
1. Start your games first, I recommend clicking on New Game in each game to prevent accidentally pressing "Back" on the title screen during shuffling
2. Click "Refresh" to scan for running games
3. Select games from the list and click "Add"
4. Games will be automatically color-coded by engine type:
   -  Blue = Melody of Memory (Unity-specific suspension)
   -  Red = KH1FM, KH Re:CoM, KH2FM, KHBBSFM, KHDDD (gentle priority-only suspension)
   -  White = KH0.2, KH3, KH 358/2 Days (MelonMix/Bizhawk), KH Re:coded (MelonMix/Bizhawk), KH CoM (Bizhawk), KH Dark Road (Bluestacks) (standard thread suspension)
5. If you're playing any games through the Bizhawk or Bluestacks emulator, make sure to set the graphics renderer to OpenGL. If you're using DirectX, the game will crash during shuffling.
6. If you're playing Melody of Memory, you will need to pause and unpause the game if you're in the middle of the song to resync the music with the gameplay. I'm hoping to fix this in the future, but no promises.
7. You can type in the name of the game by adding the game to the shuffle list, then double clicking under the Game Name column. This will create a custom game names text file in the same folder as the KHShuffler exe, which you can use within OBS to automatically display the name of the current game being played. I highly recommend using text-overdrive.lua (https://gist.github.com/kkartaltepe/861b02882056b464bfc3e0b329f2f174) so that the text file updates every frame within OBS. 

### 3. Configure Settings
- **Min/Max Seconds**: Set the switching interval range
- **Force Borderless**: Automatically convert games to borderless fullscreen

### 4. Start Shuffling
- Click "Start" to begin automatic game switching
- The application will minimize to taskbar
- Games will switch automatically at random intervals
- Click "Pause" to pause the shuffle timer, and click "Resume" to resume shuffling
- Click "Stop" anytime to restore all games

## Successfully Tested Games
- KINGDOM HEARTS FINAL MIX (Square Enix)
- KINGDOM HEARTS Chain of Memories (Bizhawk, using OpenGL)
- KINGDOM HEARTS Re:Chain of Memories (Square Enix)
- KINGDOM HEARTS II FINAL MIX (Square Enix)
- KINGDOM HEARTS 358/2 DAYS (MelonMix/Bizhawk, using OpenGL)
- KINGDOM HEARTS Birth by Sleep FINAL MIX (Square Enix)
- KINGDOM HEARTS Re:coded (MelonMix/Bizhawk, using OpenGL)
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

**Compatibility**: Windows 10/11 x64, 16GB RAM minimum recommended, 32-64GB RAM for 5+ games simultaneously



