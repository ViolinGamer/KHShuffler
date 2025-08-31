# Game Compatibility Guide for PC Game Shuffler

## Overview
This document outlines the specialized techniques developed for handling problematic games in the PC Game Shuffler. Different games require different suspension approaches to prevent crashes and ensure stable operation.

## Game Categories

### 1. Unity Games (Unity Engine)
**Examples**: Kingdom Hearts: Melody of Memory
**Issues**: Window invalidation, audio persistence, hanging operations, engine sensitivity
**Solution**: Unity-compatible suspension with timeout protection
**Documentation**: See `Unity_Game_Compatibility_Guide.md`

### 2. Kingdom Hearts Games (Square Enix Engine)
**Examples**: Re:Chain of Memories, Kingdom Hearts 1, Kingdom Hearts 2
**Issues**: Engine crashes with aggressive thread suspension
**Solution**: Priority-only suspension (no thread manipulation)

### 3. Standard Games
**Examples**: Most other games
**Issues**: Generally stable with full suspension
**Solution**: Full thread suspension + priority adjustment

## Implementation Strategy

### Detection System
```csharp
// Comprehensive Kingdom Hearts detection
private static bool IsKingdomHeartsGame(string processName, string windowTitle)
{
    var pName = processName.ToLower();
    var wTitle = windowTitle.ToLower();
    
    // Unity-based Kingdom Hearts games (special Unity handling)
    bool isMelodyOfMemory = pName.Contains("melody") || pName.Contains("memory") || 
                           wTitle.Contains("melody of memory");
    
    // Non-Unity Kingdom Hearts games (priority-only suspension)
    bool isReChainOfMemories = pName.Contains("re_chain") || pName.Contains("rechain") ||
                              pName.Contains("chain of memories") || pName.Contains("recom");
    
    bool isOtherKH = pName.Contains("kingdom hearts") || wTitle.Contains("kingdom hearts") ||
                    pName.Contains("kh1") || pName.Contains("kh2") || pName.Contains("khfm");
    
    return isMelodyOfMemory || isReChainOfMemories || isOtherKH;
}

// Unity-specific detection (subset of Kingdom Hearts)
private static bool IsMelodyOfMemory(string processName, string windowTitle)
{
    var pName = processName.ToLower();
    var wTitle = windowTitle.ToLower();
    
    return pName.Contains("melody") || pName.Contains("memory") || 
           wTitle.Contains("melody of memory") || wTitle.Contains("melody");
}
```

### Suspension Logic
```csharp
if (safeSuspension) {
    if (IsMelodyOfMemory(processName, windowTitle)) {
        // Unity game - use Unity-compatible suspension
        Debug.WriteLine("Using Unity-compatible suspension");
        SuspendUnityGame(pid);
    } else {
        // Non-Unity Kingdom Hearts or other safe games - priority only
        Debug.WriteLine("Using priority-only suspension");
        SuspendProcessPriorityOnly(pid);
    }
} else {
    // Standard games - full thread suspension
    Debug.WriteLine("Using full thread suspension");
    SuspendProcessWithThreads(pid);
}
```

## Known Problematic Games

### Kingdom Hearts: Re:Chain of Memories
- **Engine**: Square Enix proprietary
- **Process Name**: `KINGDOM HEARTS Re_Chain of Memories`
- **Window Title**: `KINGDOM HEARTS - HD 1.5+2.5 ReMIX -`
- **Thread Count**: ~40-45 threads
- **Problem**: Crashes with full thread suspension
- **Solution**: Priority-only suspension (`ProcessPriorityClass.Idle`)
- **Detection**: `pName.Contains("re_chain")` or `pName.Contains("recom")`

### Kingdom Hearts: Melody of Memory
- **Engine**: Unity
- **Process Name**: `KINGDOM HEARTS Melody of Memory`
- **Window Title**: `KINGDOM HEARTS Melody of Memory`
- **Thread Count**: ~180-190 threads
- **Problem**: Complex Unity engine issues (see Unity guide)
- **Solution**: Unity-compatible suspension with timeout protection
- **Detection**: `pName.Contains("melody")` or `pName.Contains("memory")`

### Kingdom Hearts 1 & 2
- **Engine**: Square Enix proprietary
- **Process Names**: Various (contains "kingdom hearts", "kh1", "kh2", "khfm")
- **Problem**: Engine sensitivity to thread suspension
- **Solution**: Priority-only suspension
- **Detection**: `pName.Contains("kingdom hearts")` or similar patterns

## UI Behavior

### Automatic Detection
- Kingdom Hearts games are automatically detected when added
- They default to "Safe suspend" mode (red background)
- Users can still override the mode if needed

### Visual Indicators
- **Red Background**: Safe suspend mode (priority-only or Unity-compatible)
- **Default Background**: Normal suspend mode (full thread suspension)
- **Yellow Background**: No suspend mode (priority changes only)

### Mode Switching
Right-click target games to cycle through suspension modes:
1. **Normal** (full thread suspension) - Default background
2. **Safe** (priority-only or Unity-compatible) - Red background  
3. **No Suspend** (minimal intervention) - Yellow background

## Testing Guidelines

### For New Kingdom Hearts Games
1. **Start with Safe Mode**: Always default to priority-only suspension
2. **Test Stability**: Verify game doesn't crash during suspend/resume cycles
3. **Check Audio**: Ensure audio stops during suspension (priority-only should work)
4. **Monitor Performance**: Watch for any engine-specific issues

### For New Unity Games
1. **Follow Unity Guide**: Use the comprehensive Unity compatibility system
2. **Test Window Handling**: Verify window restoration works correctly
3. **Check Timeout Protection**: Ensure no hanging during operations

### For Unknown Games
1. **Start Conservative**: Begin with priority-only suspension
2. **Test Gradually**: If stable, can try full thread suspension for better performance
3. **Watch for Crashes**: Any crashes indicate need for safer suspension

## Troubleshooting

### Game Crashes on Resume
- **Cause**: Engine sensitivity to thread suspension
- **Solution**: Use priority-only suspension instead
- **Detection**: Add game to Kingdom Hearts detection patterns

### Audio Continues During Suspension
- **Non-Unity Games**: Priority-only suspension should stop most audio
- **Unity Games**: Use Unity-compatible suspension (75% thread suspension)
- **Persistent Audio**: May indicate separate audio process or driver issue

### Window Restoration Issues
- **Kingdom Hearts Games**: Generally use standard window operations
- **Unity Games**: Use timeout-protected window operations (see Unity guide)

## Future Enhancements

### Potential Improvements
1. **Engine Detection**: Automatically detect Unity vs other engines
2. **Adaptive Suspension**: Dynamic adjustment based on game behavior
3. **Audio Thread Detection**: More precise audio suspension targeting
4. **Engine Profiles**: Pre-configured settings for known engines

### Research Areas
- Square Enix engine patterns and limitations
- Other problematic game engines (Unreal, Source, etc.)
- System-level audio suspension techniques
- Cross-platform compatibility considerations

## Conclusion

The multi-tier game compatibility system provides robust handling for various game types:

1. **Unity Games**: Specialized Unity-compatible suspension
2. **Kingdom Hearts Games**: Safe priority-only suspension  
3. **Standard Games**: Aggressive full thread suspension

This approach ensures maximum compatibility while maintaining performance where possible.