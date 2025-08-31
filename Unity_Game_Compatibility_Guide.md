# Unity Game Compatibility Guide for PC Game Shuffler

## Overview
This document outlines the specialized techniques developed for handling Unity games (specifically Kingdom Hearts: Melody of Memory) in the PC Game Shuffler. Unity games present unique challenges due to their engine architecture and window management behavior.

## Problem Summary
Unity games like Melody of Memory exhibit several problematic behaviors during process suspension/resumption:

### Issues Encountered:
1. **Window Invalidation**: Unity frequently destroys and recreates windows during state changes
2. **Audio Persistence**: Standard suspension methods fail to suspend audio threads
3. **Hanging Operations**: Window operations (`SendMessage`, `ShowWindow`) can hang indefinitely
4. **Process Recovery**: Games may become permanently suspended if window references are lost
5. **Engine Sensitivity**: Aggressive thread suspension can crash the Unity engine

## Solution Architecture

### 1. Detection System
```csharp
private static bool IsMelodyOfMemory(string processName, string windowTitle)
{
    var pName = processName.ToLower();
    var wTitle = windowTitle.ToLower();
    
    return pName.Contains("melody") || pName.Contains("memory") || 
           wTitle.Contains("melody of memory") || wTitle.Contains("melody") ||
           wTitle.Contains("kingdom hearts melody") || wTitle.Contains("kh melody") ||
           pName.Contains("khmelody") || pName.Contains("kh_melody");
}
```

**Key Features:**
- Multi-pattern detection (process name + window title)
- Case-insensitive matching
- Handles various naming conventions

### 2. Moderate Suspension Approach
Unlike aggressive full-thread suspension, Unity games require a balanced approach:

#### Thread Suspension Strategy:
```csharp
// Suspend 75% of threads to catch audio while maintaining stability
var threadsToSuspend = process.Threads.Cast<ProcessThread>()
    .Where(t => {
        try {
            return t.ThreadState == System.Diagnostics.ThreadState.Wait &&
                   t.PriorityLevel == ThreadPriorityLevel.Normal;
        }
        catch {
            return false;
        }
    })
    .OrderBy(t => t.Id)
    .Take(Math.Max(15, (process.Threads.Count * 3) / 4)) // 75% for audio coverage
    .ToList();
```

#### Priority Management:
- **Suspension**: Set to `ProcessPriorityClass.BelowNormal` (not Idle)
- **Resume**: Restore to `ProcessPriorityClass.Normal`

#### Window Handling:
- **Hide**: `ShowWindow(mainWindow, ShowWindowCommands.Hide)`
- **Store**: Keep window handle for restoration
- **Minimize**: Send minimize command as backup

### 3. Timeout-Protected Resume System
The most critical component - prevents hanging and ensures reliable recovery:

#### Enhanced Window Validation:
```csharp
// Multiple validation checks
bool windowValid = NativeMethods.IsWindow(storedWindow);
if (windowValid) {
    // Verify window still belongs to our process
    NativeMethods.GetWindowThreadProcessId(storedWindow, out var windowPid);
    if (windowPid != pid) {
        Debug.WriteLine("Window now belongs to different process");
        windowValid = false;
    }
}
```

#### Multi-Attempt Window Discovery:
```csharp
// Try 5 times with 100ms delays
for (int attempt = 0; attempt < 5; attempt++) {
    storedWindow = process.MainWindowHandle;
    if (storedWindow != IntPtr.Zero && NativeMethods.IsWindow(storedWindow)) {
        // Verify ownership
        NativeMethods.GetWindowThreadProcessId(storedWindow, out var windowPid);
        if (windowPid == pid) {
            Debug.WriteLine($"Found window on attempt {attempt + 1}");
            break;
        }
    }
    if (attempt < 4) Thread.Sleep(100);
}
```

#### Timeout Protection:
```csharp
// Protect potentially hanging operations
var restoreTask = Task.Run(() => {
    if (NativeMethods.IsWindow(storedWindow)) {
        NativeMethods.SendMessage(storedWindow, WM_SYSCOMMAND, SC_RESTORE, IntPtr.Zero);
        return true;
    }
    return false;
});

if (restoreTask.Wait(1000)) { // 1 second timeout
    Debug.WriteLine("Operation completed successfully");
} else {
    Debug.WriteLine("Operation timed out - continuing");
}
```

### 4. Execution Order (Critical)
The order of operations is crucial for Unity games:

1. **Resume Process Priority** (first - least risky)
2. **Resume All Threads** (before window operations)
3. **Validate Window** (ensure still valid)
4. **Timeout-Protected Window Restore** (most risky - protected)
5. **Timeout-Protected Window Show**
6. **Timeout-Protected Focus Operations**

### 5. Orphaned Process Recovery
Handle cases where window references are lost but process remains suspended:

```csharp
// During stop - find orphaned processes
foreach (var suspendedPid in _suspendedProcesses.Keys.ToList()) {
    if (!resumedProcesses.Contains(suspendedPid)) {
        using var process = Process.GetProcessById(suspendedPid);
        if (IsMelodyOfMemory(process.ProcessName, "")) {
            Debug.WriteLine("Recovering orphaned Unity game");
            ResumeUnityGame(suspendedPid);
        }
    }
}
```

## Implementation Guidelines

### For New Unity Games:
1. **Add Detection Pattern**: Update `IsMelodyOfMemory` method with new game signatures
2. **Use Safe Mode**: Default Unity games to "Safe suspend" mode (red background in UI)
3. **Test Audio Suspension**: Verify 75% thread suspension stops audio
4. **Monitor Stability**: Watch for crashes and adjust thread percentage if needed

### Debug Logging Format:
- `[UNITY-SUSPEND]`: Suspension operations
- `[UNITY-RESUME]`: Resume operations  
- `[DETECT]`: Game detection
- Include PID, operation type, and success/failure status

### Thread Percentage Tuning:
- **Start with 75%**: Good balance of audio suspension vs stability
- **Minimum 15 threads**: Ensure core functionality for small processes
- **Safe state filtering**: Only suspend threads in `Wait` state with `Normal` priority

## Testing Checklist

### Unity Game Compatibility Test:
- [ ] Game audio stops during suspension
- [ ] Game resumes without crashing
- [ ] Window restoration works correctly
- [ ] No hanging during switch operations
- [ ] Orphaned process recovery works
- [ ] Multiple suspend/resume cycles stable
- [ ] Stop operation resumes all processes

### Performance Monitoring:
- Monitor CPU usage during suspension (should drop significantly)
- Check for memory leaks during extended shuffling
- Verify audio driver stability

## Known Unity Game Signatures

### Kingdom Hearts: Melody of Memory:
- **Process Names**: `KINGDOM HEARTS Melody of Memory`, `melody`, `memory`
- **Window Titles**: `KINGDOM HEARTS Melody of Memory`, `melody of memory`
- **Thread Count**: ~180-190 threads
- **Suspension Percentage**: 75% (effective for audio)

### Pattern Templates for Future Games:
```csharp
// Add new patterns to detection method:
pName.Contains("newgame") || pName.Contains("unity") ||
wTitle.Contains("New Unity Game") || wTitle.Contains("game title")
```

## Troubleshooting

### Game Crashes on Resume:
- Reduce thread suspension percentage (try 50%)
- Check for engine-specific threads to avoid
- Verify process priority changes aren't too aggressive

### Audio Still Playing:
- Increase thread suspension percentage (try 85%)
- Check for separate audio processes
- Monitor audio driver threads

### Window Operations Hanging:
- Verify timeout protection is active
- Check for Unity version-specific window behavior
- Consider shorter timeout values (500ms)

### Recovery Failures:
- Enhance window search patterns
- Add process validation checks
- Implement fallback resume methods

## Future Enhancements

### Potential Improvements:
1. **Unity Version Detection**: Different Unity versions may need different approaches
2. **Audio Thread Identification**: More precise audio thread targeting
3. **Engine State Monitoring**: Detect Unity engine state for safer operations
4. **Adaptive Timeouts**: Dynamic timeout values based on system performance

### Research Areas:
- Unity engine internals for better suspension points
- Audio system integration patterns
- Window management in different Unity versions
- Multi-monitor handling for Unity games

## Conclusion

The Unity game compatibility system provides a robust foundation for handling challenging games like Melody of Memory. The key principles are:

1. **Detection** - Accurate game identification
2. **Moderation** - Balanced suspension approach
3. **Protection** - Timeout guards against hanging
4. **Recovery** - Multiple fallback mechanisms
5. **Validation** - Extensive error checking

This approach can be extended to other Unity games by updating detection patterns and fine-tuning suspension parameters while maintaining the core architectural principles.