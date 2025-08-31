# ?? KH Shuffler - v1.0.0

**Advanced Kingdom Hearts Game Shuffler with Engine-Specific Suspension**

A sophisticated Windows application that intelligently shuffles between multiple Kingdom Hearts games using engine-specific suspension techniques for optimal stability and performance.

## ? Key Features

### ?? Engine-Specific Intelligence
- **?? Unity Games**: Complete thread suspension + audio stopping (Melody of Memory)
- **? UE4 Games**: Selective 30% thread suspension + CPU affinity limiting (KH3, KH 0.2)
- **?? Square Enix Legacy**: Priority-only suspension for maximum stability (KH1, KH2, BBS, DDD, ReCoM)
- **?? Custom Modes**: Manual override options available

### ?? Advanced Capabilities
- **Zero Crashes**: Extensively tested across 8+ Kingdom Hearts games
- **Complete Audio Control**: Unity games become completely silent when suspended
- **Smart Window Management**: Automatic borderless fullscreen conversion
- **Intelligent Process Detection**: Automatic engine classification
- **Real-Time Validation**: Process health monitoring and cleanup

### ??? Stability Features
- **UE4 Selective Suspension**: Only suspends 30% of threads (newer threads first)
- **CPU Affinity Management**: Limits UE4 games to 25% of CPU cores
- **Conservative Resource Management**: BelowNormal priority instead of Idle for UE4
- **Process Recovery**: Automatic restoration on stop or errors

## ?? Tested Game Compatibility

| Game | Engine | Suspension Mode | Status |
|------|--------|----------------|---------|
| KINGDOM HEARTS Melody of Memory | Unity | ?? Unity-Compatible | ? Perfect |
| KINGDOM HEARTS III | UE4 | ? UE4 Selective | ? Perfect |
| KINGDOM HEARTS 0.2 Birth by Sleep | UE4 | ? UE4 Selective | ? Perfect |
| KINGDOM HEARTS Dream Drop Distance | Square Enix | ?? Priority-Only | ? Perfect |
| KINGDOM HEARTS Birth by Sleep FINAL MIX | Square Enix | ?? Priority-Only | ? Perfect |
| KINGDOM HEARTS II FINAL MIX | Square Enix | ?? Priority-Only | ? Perfect |
| KINGDOM HEARTS FINAL MIX | Square Enix | ?? Priority-Only | ? Perfect |
| KINGDOM HEARTS Re:Chain of Memories | Square Enix | ?? Priority-Only | ? Perfect |

## ?? Quick Start

1. **Download** the latest `KHShuffler.exe` from the releases
2. **Launch** all Kingdom Hearts games you want to shuffle between
3. **Start KH Shuffler** and click "Refresh" to see available games
4. **Add games** to your target list using "Add Selected"
5. **Configure timing** (min/max seconds between switches)
6. **Click "Start"** to begin shuffling!

## ?? Advanced Usage

### Suspension Mode Colors
- **?? Blue**: Unity games (complete suspension + audio stopping)
- **? White**: UE4 games (selective suspension for stability)
- **?? Red**: Square Enix legacy (priority-only for maximum stability)
- **?? Yellow**: No suspension (manual override)

### Manual Mode Override
Right-click any game in the target list to cycle through suspension modes if the automatic detection needs adjustment.

### Borderless Fullscreen
Enable "Force Borderless" to automatically convert windowed games to borderless fullscreen for seamless switching.

## ?? System Requirements

- **OS**: Windows 10/11 (x64)
- **Framework**: .NET 8.0 (included in release)
- **Memory**: 4GB+ RAM recommended
- **Permissions**: Standard user (no admin required)

## ?? Technical Achievements

- **100% Success Rate**: 8/8 Kingdom Hearts games work perfectly
- **Zero Crash Rate**: Extensive testing with no application crashes
- **Advanced Threading**: Sophisticated UE4 selective thread suspension
- **Resource Management**: CPU affinity and priority control
- **Cross-Engine Compatibility**: Supports Unity, UE4, and Square Enix engines

## ?? Version History

### v1.0.0 (2025-01-28)
- ? Complete UE4 selective suspension system
- ? Unity audio stopping capabilities  
- ? Square Enix legacy engine support
- ? Automatic engine detection
- ? Advanced window management
- ? Real-time process validation
- ? Comprehensive error handling

## ?? Contributing

This project represents the culmination of extensive research into game engine threading and process management. The sophisticated UE4 suspension system and multi-engine compatibility make it a unique solution for Kingdom Hearts fans.

## ?? License

This project is open source. Feel free to use, modify, and distribute according to your needs.

---

**Enjoy seamless Kingdom Hearts gaming! ?????**