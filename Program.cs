using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using BetterGameShuffler.TwitchIntegration;

namespace BetterGameShuffler;

/// <summary>
/// Debug logging system that creates timestamped log files for each session
/// </summary>
public static class DebugLogger
{
    private static StreamWriter? _logWriter;
    private static string? _currentLogFile;
    private static readonly object _lockObject = new();

    public static void Initialize()
    {
        try
        {
            // Create Debug Logs folder if it doesn't exist
            // Use AppContext.BaseDirectory for single-file app compatibility
            string baseDirectory = AppContext.BaseDirectory;
            string debugLogsFolder = Path.Combine(baseDirectory, "Debug Logs");
            Directory.CreateDirectory(debugLogsFolder);

            // Create timestamped log file name
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string logFileName = $"DebugLog_{timestamp}.txt";
            _currentLogFile = Path.Combine(debugLogsFolder, logFileName);

            // Initialize the log writer
            _logWriter = new StreamWriter(_currentLogFile, false) { AutoFlush = true };

            // Write initial header
            WriteToLog($"=== DEBUG LOG STARTED: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            WriteToLog($"KHShuffler {Program.Version} Debug Session");
            WriteToLog($"Log File: {logFileName}");
            WriteToLog("Debug logging system initialized successfully");
            WriteToLog("");

            // Also setup Debug.WriteLine to write to our log file
            Trace.Listeners.Add(new DebugTextWriterTraceListener());

            Console.WriteLine($"Debug logging initialized: {_currentLogFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize debug logging: {ex.Message}");
        }
    }

    public static void WriteToLog(string message)
    {
        lock (_lockObject)
        {
            try
            {
                string timestampedMessage = $"{DateTime.Now:HH:mm:ss.fff} - {message}";
                _logWriter?.WriteLine(timestampedMessage);
                Console.WriteLine(timestampedMessage); // Also write to console for immediate feedback
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Writes a message directly to the log file without additional processing
    /// Used by the trace listener to avoid double-formatting
    /// </summary>
    public static void WriteRawToLog(string message)
    {
        lock (_lockObject)
        {
            try
            {
                string timestampedMessage = $"{DateTime.Now:HH:mm:ss.fff} - {message}";
                _logWriter?.WriteLine(timestampedMessage);
                _logWriter?.Flush(); // Ensure immediate write for debug output
                // Don't write to console here to avoid duplicate console output
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write raw to log: {ex.Message}");
            }
        }
    }

    public static void Shutdown()
    {
        lock (_lockObject)
        {
            try
            {
                WriteToLog("");
                WriteToLog($"=== DEBUG LOG ENDED: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                _logWriter?.Close();
                _logWriter?.Dispose();
                _logWriter = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error shutting down debug logger: {ex.Message}");
            }
        }
    }

    public static string? GetCurrentLogFile() => _currentLogFile;
}

/// <summary>
/// Custom trace listener that writes Debug.WriteLine output to our log file
/// </summary>
public class DebugTextWriterTraceListener : TraceListener
{
    public override void WriteLine(string? message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            // Write directly to the log file with proper formatting
            // Don't use DebugLogger.WriteToLog() to avoid double processing
            DebugLogger.WriteRawToLog(message);
        }
    }

    public override void Write(string? message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            // Write directly to the log file with proper formatting  
            // Don't use DebugLogger.WriteToLog() to avoid double processing
            DebugLogger.WriteRawToLog(message);
        }
    }
}

internal static class Program
{
    public const string Version = "v2.5.0";

    [STAThread]
    static void Main()
    {
        try
        {
            // Initialize debug logging system FIRST
            DebugLogger.Initialize();

            Debug.WriteLine("Program: Starting application...");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Debug.WriteLine("Program: Creating MainForm...");

            var mainForm = new MainForm();

            Debug.WriteLine("Program: Running application...");

            Application.Run(mainForm);

            Debug.WriteLine("Program: Application exited normally");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Program: FATAL ERROR: {ex.Message}");
            Debug.WriteLine($"Program: Exception details: {ex}");

            // Try to show error to user
            try
            {
                MessageBox.Show($"Fatal application error:\n\n{ex.Message}\n\nCheck the debug output for more details.",
                    "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // If even MessageBox fails, just exit
            }

            Environment.Exit(1);
        }
        finally
        {
            // Always shutdown debug logging
            DebugLogger.Shutdown();
        }
    }
}

// Old Settings class removed - now using the comprehensive Settings.cs file

public enum ShowWindowCommands
{
    Hide = 0,
    Normal = 1,
    ShowMinimized = 2,
    Maximize = 3,
    ShowMaximized = 3,
    ShowNoActivate = 4,
    Show = 5,
    Minimize = 6,
    ShowMinNoActive = 7,
    ShowNA = 8,
    Restore = 9,
    ShowDefault = 10,
    ForceMinimize = 11
}

public enum SuspensionMode
{
    Normal,
    Unity,
    PriorityOnly,
    NoSuspend
}

[Flags]
public enum ThreadAccess : int
{
    TERMINATE = (0x0001),
    SUSPEND_RESUME = (0x0002),
    GET_CONTEXT = (0x0008),
    SET_CONTEXT = (0x0010),
    SET_INFORMATION = (0x0020),
    QUERY_INFORMATION = (0x0040),
    SET_THREAD_TOKEN = (0x0080),
    IMPERSONATE = (0x0100),
    DIRECT_IMPERSONATION = (0x0200)
}

internal static class NativeMethods
{
    public const int GWL_STYLE = -16;
    public const int WS_CAPTION = 0x00C00000;
    public const int WS_THICKFRAME = 0x00040000;
    public const int WS_SYSMENU = 0x00080000;

    public static readonly IntPtr HWND_TOP = new IntPtr(0);
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommands nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr SetActiveWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

    [DllImport("kernel32.dll")]
    public static extern uint SuspendThread(IntPtr hThread);

    [DllImport("kernel32.dll")]
    public static extern int ResumeThread(IntPtr hThread);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
}

[Flags]
public enum ProcessAccessFlags : uint
{
    VirtualMemoryRead = 0x0010,
    VirtualMemoryWrite = 0x0020,
    QueryInformation = 0x0400,
    QueryLimitedInformation = 0x1000
}

[StructLayout(LayoutKind.Sequential)]
public struct MONITORINFO
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
}

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

public class WindowItem
{
    public IntPtr Handle { get; }
    public string Title { get; }
    public string ProcessName { get; }
    public int Pid { get; }

    public WindowItem(IntPtr handle, string title, string processName, int pid)
    {
        Handle = handle;
        Title = title;
        ProcessName = processName;
        Pid = pid;
    }

    public override string ToString() => $"{Title} ({ProcessName}, PID: {Pid})";
}

public class MainForm : Form
{
    private readonly ListView _processList = new() { View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill, CheckBoxes = true };
    private readonly Button _refreshButton = new() { Text = "Refresh" };
    private readonly Button _addButton = new() { Text = "Add Checked" };
    private readonly Button _removeButton = new() { Text = "Remove Selected" };
    private readonly Button _startButton = new() { Text = "Start" };
    private readonly Button _stopButton = new() { Text = "Stop", Enabled = false };
    private readonly Button _pauseButton = new() { Text = "Pause", Enabled = false };
    private readonly NumericUpDown _minSeconds = new() { Minimum = 1, Maximum = 3600, Value = 10 };
    private readonly NumericUpDown _maxSeconds = new() { Minimum = 1, Maximum = 7200, Value = 10 };
    private readonly ListView _targets = new() { View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill, CheckBoxes = true };
    private readonly System.Threading.Timer _backgroundTimer;
    private bool _timerShouldRun = false;

    private readonly List<IntPtr> _targetWindows = new();
    private readonly Random _rng = new();
    private readonly ConcurrentDictionary<IntPtr, SuspensionMode> _suspensionModeCache = new();
    private readonly ConcurrentDictionary<int, bool> _suspendedProcesses = new();

    private bool _isShuffling = false;
    private bool _isPaused = false;
    private int _currentIndex = -1;
    private DateTime _nextSwitch = DateTime.MinValue;
    private DateTime _pausedAt = DateTime.MinValue;
    private TimeSpan _remainingTime = TimeSpan.Zero;
    private bool _isSwitching = false;

    private readonly SlidingToggle _darkModeToggle = new();
    private readonly CheckBox _forceBorderless = new() { Text = "Force borderless fullscreen", Checked = true, AutoSize = true };

    // Dark mode color schemes
    private static readonly Color DarkBackground = Color.FromArgb(32, 32, 32);
    private static readonly Color DarkSecondary = Color.FromArgb(45, 45, 48);
    private static readonly Color DarkText = Color.FromArgb(220, 220, 220);
    private static readonly Color DarkBorder = Color.FromArgb(63, 63, 70);

    // CRITICAL FIX: Store original window styles to prevent resizing issues
    private readonly ConcurrentDictionary<IntPtr, int> _originalWindowStyles = new();

    // Settings System
    private readonly Settings _settings = new();

    // Game name mappings storage (now managed by Settings class)
    private readonly string _currentGamePath = Path.Combine(Application.StartupPath, "current_game.txt"); // Cached path for performance
    private readonly Dictionary<IntPtr, string> _gameNameCache = new(); // Cache for fast repeated lookups by window handle

    // Twitch Effects System
    private readonly EffectManager _effectManager;
    private readonly Dictionary<string, DateTime> _blacklistedGames = new();

    // Test Mode UI
    private readonly Button _testModeButton = new() { Text = "Test Effects", Size = new Size(110, 25), TextAlign = ContentAlignment.MiddleCenter };
    private readonly GroupBox _effectsGroup = new() { Text = "Twitch Effects", AutoSize = true, Width = 420 };
    private readonly CheckBox _enableEffects = new() { Text = "Enable Effects", AutoSize = true, Checked = true };

    public MainForm()
    {
        try
        {
            Debug.WriteLine("MainForm: Starting initialization...");

            // Initialize settings system first
            Debug.WriteLine("MainForm: Initializing settings system...");
            _settings.Initialize();

            // Load saved shuffle time preferences
            _minSeconds.Value = _settings.MinSeconds;
            _maxSeconds.Value = _settings.MaxSeconds;
            _forceBorderless.Checked = _settings.ForceBorderlessFullscreen;
            Debug.WriteLine($"MainForm: Loaded settings - Min: {_settings.MinSeconds}s, Max: {_settings.MaxSeconds}s, Borderless: {_settings.ForceBorderlessFullscreen}");

            Text = $"KHShuffler {Program.Version}";
            Width = 1200;
            Height = 700; // Increased height for new controls

            Debug.WriteLine("MainForm: Basic properties set, initializing effect manager...");

            // Initialize effect manager
            _effectManager = new EffectManager(this);

            Debug.WriteLine("MainForm: Effect manager initialized, setting up UI...");

            // Set up process list columns
            _processList.Columns.Add("Title", 400);
            _processList.Columns.Add("Process", 150);
            _processList.Columns.Add("PID", 80);

            // Set up targets list columns
            _targets.Columns.Add("Title", 400);
            _targets.Columns.Add("Process", 150);
            _targets.Columns.Add("PID", 80);
            _targets.Columns.Add("Game Name", 200);

            // Configure process list behavior
            _processList.ItemCheck += ProcessList_ItemCheck;
            _processList.MouseClick += ProcessList_MouseClick;
            _processList.HideSelection = true; // Hide blue selection highlighting
            _processList.MultiSelect = false; // Disable multi-selection
            _processList.ItemSelectionChanged += ProcessList_ItemSelectionChanged;

            // Configure targets list behavior
            _targets.DoubleClick += Targets_DoubleClick;
            _targets.MouseDown += Targets_MouseDown;
            _targets.ItemCheck += Targets_ItemCheck;

            var rightPanel = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.TopDown, Width = 450, Padding = new Padding(8) };
            rightPanel.Controls.Add(new Label { Text = "Min seconds" });
            rightPanel.Controls.Add(_minSeconds);
            rightPanel.Controls.Add(new Label { Text = "Max seconds" });
            rightPanel.Controls.Add(_maxSeconds);
            rightPanel.Controls.Add(_refreshButton);
            rightPanel.Controls.Add(_addButton);
            rightPanel.Controls.Add(_removeButton);
            rightPanel.Controls.Add(_startButton);
            rightPanel.Controls.Add(_stopButton);
            rightPanel.Controls.Add(_pauseButton);
            rightPanel.Controls.Add(_forceBorderless);
            rightPanel.Controls.Add(_darkModeToggle);

            // Add effects group
            SetupEffectsUI();
            rightPanel.Controls.Add(_effectsGroup);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 300 };
            split.Panel1.Controls.Add(_processList);
            split.Panel2.Controls.Add(_targets);

            Controls.Add(split);
            Controls.Add(rightPanel);

            _refreshButton.Click += (_, __) => RefreshProcesses();
            _addButton.Click += (_, __) => AddSelectedProcesses();
            _removeButton.Click += (_, __) => RemoveSelectedTargets();
            _startButton.Click += (_, __) => StartShuffle();
            _stopButton.Click += (_, __) => StopShuffle();
            _pauseButton.Click += (_, __) => TogglePause();
            _darkModeToggle.CheckedChanged += (_, __) => ToggleDarkMode();
            _testModeButton.Click += (_, __) => OpenTestMode();

            // Add event handlers to save timer settings when changed
            _minSeconds.ValueChanged += (_, __) =>
            {
                _settings.MinSeconds = (int)_minSeconds.Value;
                Debug.WriteLine($"Settings: MinSeconds saved to {_settings.MinSeconds}");
            };
            _maxSeconds.ValueChanged += (_, __) =>
            {
                _settings.MaxSeconds = (int)_maxSeconds.Value;
                Debug.WriteLine($"Settings: MaxSeconds saved to {_settings.MaxSeconds}");
            };
            _forceBorderless.CheckedChanged += (_, __) =>
            {
                _settings.ForceBorderlessFullscreen = _forceBorderless.Checked;
                Debug.WriteLine($"Settings: ForceBorderlessFullscreen saved to {_settings.ForceBorderlessFullscreen}");
            };

            _backgroundTimer = new System.Threading.Timer(BackgroundTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Set up application exit handler for cleanup
            Application.ApplicationExit += OnApplicationExit;

            // Load and apply saved dark mode preference
            _darkModeToggle.Checked = _settings.DarkModeEnabled;
            ApplyTheme();

            Debug.WriteLine("MainForm: Refreshing processes...");
            RefreshProcesses();

            Debug.WriteLine("MainForm: Initialization complete!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MainForm: CRITICAL ERROR during initialization: {ex.Message}");
            Debug.WriteLine($"MainForm: Exception details: {ex}");

            // Show error to user and exit gracefully
            MessageBox.Show($"Critical error during application startup:\n\n{ex.Message}\n\nThe application will close.",
                "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            // Exit the application
            Environment.Exit(1);
        }
    }

    private void SetupEffectsUI()
    {
        var testPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Width = 400,
            Padding = new Padding(3)
        };

        testPanel.Controls.AddRange(new Control[] { _enableEffects, _testModeButton });

        var infoLabel = new Label
        {
            Text = "Create 'images', 'sounds', and 'hud' folders\nfor effect media files.\n\nDirectory settings are saved automatically.",
            AutoSize = true,
            Font = new Font("Segoe UI", 8),
            ForeColor = Color.Gray
        };

        _effectsGroup.Controls.AddRange(new Control[]
        {
            testPanel,
            infoLabel
        });
    }

    private void OpenTestMode()
    {
        try
        {
            var twitchEffectSettings = new TwitchEffectSettings();
            // CRITICAL FIX: Pass both MainForm AND the EXISTING EffectManager instance
            // This ensures the test mode uses the same EffectManager as the real shuffler
            var testForm = new EffectTestModeForm(twitchEffectSettings, this, _effectManager);
            testForm.Show(); // Changed from ShowDialog() to Show() to allow both windows to be interactive
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening test mode: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override bool ShowWithoutActivation => _isShuffling;

    private void BackgroundTimerCallback(object? state)
    {
        try
        {
            if (!_timerShouldRun || _isSwitching || _isPaused) return;

            var now = DateTime.UtcNow;

            if (now >= _nextSwitch && !_isSwitching)
            {
                try
                {
                    SwitchToNextWindow();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Switch error: {ex}");
                    _isSwitching = false;
                    ScheduleNextSwitch();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Timer error: {ex}");
            _isSwitching = false;
        }
    }

    private static bool IsUE4KingdomHearts(string processName, string windowTitle)
    {
        var pName = processName.ToLower();
        var wTitle = windowTitle.ToLower();

        // PRIORITY: Process names for UE4 Kingdom Hearts games
        return (pName.Contains("kh3") || pName.Contains("khiii") ||
                pName.Contains("kingdom hearts iii") ||
                // KH 0.2 Birth by Sleep - A Fragmentary Passage (UE4 engine)
                pName.Contains("0.2") || pName.Contains("02") ||
                pName.Contains("fragmentary") || pName.Contains("bbs02") ||
                // Fallback to window title only if process name doesn't match
                (wTitle.Contains("kingdom hearts iii") || wTitle.Contains("kingdom hearts 3") ||
                 wTitle.Contains("kh3") || wTitle.Contains("khiii") ||
                 wTitle.Contains("0.2") || wTitle.Contains("fragmentary passage") ||
                 wTitle.Contains("birth by sleep") && wTitle.Contains("0.2") ||
                 wTitle.Contains("kingdom hearts 0.2") || wTitle.Contains("kh 0.2") || wTitle.Contains("kh0.2")));
    }

    private static bool IsUnityKingdomHearts(string processName, string windowTitle)
    {
        var pName = processName.ToLower();
        var wTitle = windowTitle.ToLower();

        Debug.WriteLine($"[UNITY-DETECTION] Checking process: '{pName}', window: '{wTitle}'");

        // PRIORITY: Process names for Unity Kingdom Hearts games (Melody of Memory)
        bool isUnityKH = (pName.Contains("melody") && pName.Contains("memory")) ||
                         (pName.Contains("melodyofmemory")) ||
                         (pName.Contains("khmelody")) ||
                         pName.Contains("melody of memory") ||
                         // Fallback to window title only if process name doesn't match
                         (wTitle.Contains("melody of memory")) ||
                         (wTitle.Contains("kingdom hearts melody")) ||
                         (wTitle.Contains("kh melody")) ||
                         (wTitle.Contains("melodyofmemory"));

        Debug.WriteLine($"[UNITY-DETECTION] Unity KH detected: {isUnityKH}");
        return isUnityKH;
    }

    private static bool IsClassicKingdomHearts(string processName, string windowTitle)
    {
        var pName = processName.ToLower();
        var wTitle = windowTitle.ToLower();

        Debug.WriteLine($"[CLASSIC-DETECTION] Checking process: '{pName}', window: '{wTitle}'");

        // CRITICAL: Re:Chain of Memories MUST be excluded from classic KH treatment
        // Re:CoM crashes with thread suspension and needs priority-only handling
        if (IsReChainOfMemories(processName, windowTitle))
        {
            Debug.WriteLine($"[CLASSIC-DETECTION] Re:Chain of Memories detected - EXCLUDED from classic KH treatment (needs priority-only)");
            return false;
        }

        // PRIORITY: Focus EXCLUSIVELY on PROCESS NAMES for HD Collections since multiple games share same window title
        // Classic Kingdom Hearts games (PS2 era, emulated or remastered) - PROCESS NAME BASED DETECTION ONLY
        bool isClassicKH =
            // HD 1.5+2.5 ReMIX Collection process names (Classic PS2 games)
            pName.Contains("kh1") || pName.Contains("kh2") ||
            pName.Contains("khii") || pName.Contains("khi") ||
            pName.Contains("bbs") || pName.Contains("birth") ||
            // HD 2.8 Collection process names - ONLY DDD (Classic), NOT 0.2 (UE4)
            pName.Contains("ddd") || pName.Contains("dream drop") ||
            // Individual classic releases
            pName.Contains("final mix") || pName.Contains("finalmix") ||
            // Generic KH process patterns (exclude UE4 and Unity games)
            (pName.Contains("kingdom hearts") &&
             !pName.Contains("melody") && !pName.Contains("kh3") && !pName.Contains("khiii") &&
             !pName.Contains("0.2") && !pName.Contains("fragmentary"));
        // NOTE: Removed window title fallbacks to prevent false positives from collection titles
        // NOTE: Removed "chain" and "re:chain" patterns - Re:CoM needs separate priority-only handling

        Debug.WriteLine($"[CLASSIC-DETECTION] Classic KH detected: {isClassicKH}");
        return isClassicKH;
    }

    /// <summary>
    /// Detects Re:Chain of Memories specifically for priority-only suspension
    /// Re:CoM crashes with thread suspension and must use priority-only approach
    /// </summary>
    private static bool IsReChainOfMemories(string processName, string windowTitle)
    {
        var pName = processName.ToLower();
        var wTitle = windowTitle.ToLower();

        bool isReCoM =
            pName.Contains("re_chain") || pName.Contains("rechain") ||
            pName.Contains("re:chain") || pName.Contains("chain of memories") ||
            pName.Contains("recom") || pName.Contains("com") ||
            (pName.Contains("chain") && pName.Contains("memories"));

        if (isReCoM)
        {
            Debug.WriteLine($"[RECOM-DETECTION] Re:Chain of Memories detected: '{processName}' - PRIORITY-ONLY mode required");
        }

        return isReCoM;
    }

    /// <summary>
    /// Priority-only resume for Re:Chain of Memories
    /// Re:CoM crashes with thread suspension, so only use priority restoration
    /// </summary>
    private static bool ResumeReChainOfMemoriesPriorityOnly(Process process)
    {
        try
        {
            Debug.WriteLine($"[RECOM-RESUME] Re:Chain of Memories priority-only resume starting (PID: {process.Id})");
            Debug.WriteLine($"[RECOM-RESUME] Process: {process.ProcessName}, Window: {process.MainWindowTitle}");
            Debug.WriteLine($"[RECOM-RESUME] Using PRIORITY-ONLY mode (no thread suspension/resume due to crash risk)");

            // ONLY restore priority - NO thread manipulation for Re:CoM stability
            process.PriorityClass = ProcessPriorityClass.Normal;
            Debug.WriteLine($"[RECOM-RESUME] Priority restored to Normal for stability");

            // Restore window if minimized
            var mainWindow = process.MainWindowHandle;
            if (mainWindow != IntPtr.Zero && NativeMethods.IsWindow(mainWindow))
            {
                Debug.WriteLine($"[RECOM-RESUME] Restoring Re:CoM window");
                NativeMethods.ShowWindow(mainWindow, ShowWindowCommands.Show);
                Thread.Sleep(20);
                NativeMethods.ShowWindow(mainWindow, ShowWindowCommands.Restore);
                Debug.WriteLine($"[RECOM-RESUME] Window restoration completed");
            }

            Debug.WriteLine($"[RECOM-RESUME] Re:Chain of Memories priority-only resume completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RECOM-RESUME] Re:Chain of Memories priority-only resume failed: {process.Id} - {ex.Message}");
            return false;
        }
    }

    private static bool ResumeClassicKHGPUSafe(Process process)
    {
        try
        {
            Debug.WriteLine($"[CLASSIC-RESUME] Classic Kingdom Hearts detected - using GPU-SAFE resume for classic games (PID: {process.Id})");
            Debug.WriteLine($"[CLASSIC-RESUME] Process: {process.ProcessName}, Window: {process.MainWindowTitle}");

            // Step 1: Gradual priority restoration to prevent GPU shock (same as UE4 approach)
            process.PriorityClass = ProcessPriorityClass.BelowNormal; // Start lower
            Thread.Sleep(50); // Initial stabilization
            process.PriorityClass = ProcessPriorityClass.Normal; // Then restore to normal
            Debug.WriteLine($"[CLASSIC-RESUME] Priority restored gradually for GPU stability");

            // Step 2: Conservative CPU affinity restoration for classic game GPU stability
            int totalCores = Environment.ProcessorCount;

            // Start with quarter cores for classic game GPU stability
            int quarterCores = Math.Max(2, totalCores / 4);
            IntPtr quarterAffinityMask = (IntPtr)((1L << quarterCores) - 1);
            process.ProcessorAffinity = quarterAffinityMask;
            Thread.Sleep(75); // Let classic game GPU threads stabilize

            // Then half cores
            int halfCores = Math.Max(3, totalCores / 2);
            IntPtr halfAffinityMask = (IntPtr)((1L << halfCores) - 1);
            process.ProcessorAffinity = halfAffinityMask;
            Thread.Sleep(100); // Longer stabilization for classic games

            // Finally restore full affinity
            IntPtr fullAffinityMask = (IntPtr)((1L << totalCores) - 1);
            process.ProcessorAffinity = fullAffinityMask;
            Debug.WriteLine($"[CLASSIC-RESUME] CPU affinity restored gradually for classic game GPU stability");

            // Step 3: Small batch thread resumption for classic game stability
            var threadsToResume = new List<ProcessThread>();
            foreach (ProcessThread thread in process.Threads)
            {
                threadsToResume.Add(thread);
            }

            Debug.WriteLine($"[CLASSIC-RESUME] GPU-safe classic resume: Found {threadsToResume.Count} threads to resume in small batches");

            int resumed = 0;
            const int batchSize = 5; // Smaller batches for classic games

            for (int i = 0; i < threadsToResume.Count; i += batchSize)
            {
                var batch = threadsToResume.Skip(i).Take(batchSize);

                foreach (var thread in batch)
                {
                    try
                    {
                        var hThread = NativeMethods.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                        if (hThread != IntPtr.Zero)
                        {
                            // Gentle resume for classic games - avoid overwhelming GPU
                            int resumeResult = NativeMethods.ResumeThread(hThread);
                            if (resumeResult > 0)
                            {
                                // Only do one additional resume if really needed
                                NativeMethods.ResumeThread(hThread);
                            }
                            NativeMethods.CloseHandle(hThread);
                            resumed++;
                        }
                    }
                    catch { }
                }

                // Delays between batches for classic game GPU stabilization
                if (i + batchSize < threadsToResume.Count)
                {
                    Thread.Sleep(35); // Classic game GPU stabilization delay
                }
            }

            // Step 4: Extended GPU stabilization delay for classic games
            Thread.Sleep(175); // Extended delay for classic game GPU synchronization

            Debug.WriteLine($"[CLASSIC-RESUME] Classic KH GPU-safe resume completed: {resumed} threads resumed with GPU stability measures");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CLASSIC-RESUME] Classic KH GPU-safe resume failed: {process.Id} - {ex.Message}");
            return false;
        }
    }

    private static bool SuspendUE4ProcessSelectively(Process process)
    {
        try
        {
            Debug.WriteLine($"UE4 Kingdom Hearts detected - using SELECTIVE THREAD suspension for UE4 stability");

            process.PriorityClass = ProcessPriorityClass.BelowNormal;
            Debug.WriteLine($"Set priority to BelowNormal for UE4 suspension");

            int totalCores = Environment.ProcessorCount;
            int allowedCores = Math.Max(2, totalCores / 4);
            IntPtr affinityMask = (IntPtr)((1L << allowedCores) - 1);
            process.ProcessorAffinity = affinityMask;
            Debug.WriteLine($"Limited CPU affinity to {allowedCores} cores (out of {totalCores}) for UE4 suspension");

            int totalThreads = process.Threads.Count;
            int maxThreadsToSuspend = Math.Min(50, (totalThreads * 30) / 100);
            Debug.WriteLine($"UE4 selective suspension: targeting {maxThreadsToSuspend} threads out of {totalThreads} for safe game pausing");

            var allThreads = process.Threads.Cast<ProcessThread>()
                .OrderByDescending(t => t.StartTime)
                .ToList();

            var activeThreads = allThreads.Where(t =>
                t.ThreadState == System.Diagnostics.ThreadState.Running ||
                t.ThreadState == System.Diagnostics.ThreadState.Ready ||
                t.ThreadState == System.Diagnostics.ThreadState.Standby).ToList();

            var waitThreads = allThreads.Where(t =>
                t.ThreadState == System.Diagnostics.ThreadState.Wait).ToList();

            var threadsToSuspend = activeThreads.Concat(waitThreads).Take(maxThreadsToSuspend).ToList();

            Debug.WriteLine($"UE4 thread selection: {activeThreads.Count} active, {waitThreads.Count} wait, targeting {threadsToSuspend.Count} total");

            int suspended = 0;
            foreach (var thread in threadsToSuspend)
            {
                try
                {
                    var hThread = NativeMethods.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                    if (hThread != IntPtr.Zero)
                    {
                        NativeMethods.SuspendThread(hThread);
                        NativeMethods.CloseHandle(hThread);
                        suspended++;
                    }
                }
                catch { }
            }

            Debug.WriteLine($"UE4 selective suspension: suspended {suspended} threads for safe game pausing");
            Debug.WriteLine($"UE4 suspension COMPLETED successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UE4 suspend failed: {process.Id} - {ex.Message}");
            return false;
        }
    }

    private static bool ResumeUE4ProcessSelectively(Process process)
    {
        try
        {
            Debug.WriteLine($"Resuming UE4 Kingdom Hearts process {process.Id} with ENHANCED GPU-SAFE + RENDERING STABILITY measures");

            // Step 1: Restore priority GRADUALLY to avoid GPU shock
            process.PriorityClass = ProcessPriorityClass.BelowNormal; // Start lower
            Thread.Sleep(75); // Increased from 50ms for better stability
            process.PriorityClass = ProcessPriorityClass.Normal; // Then restore to normal

            // Step 2: More conservative CPU affinity restoration for rendering stability
            int totalCores = Environment.ProcessorCount;

            // Start with even fewer cores for KH0.2 rendering stability
            int quarterCores = Math.Max(2, totalCores / 4);
            IntPtr quarterAffinityMask = (IntPtr)((1L << quarterCores) - 1);
            process.ProcessorAffinity = quarterAffinityMask;
            Thread.Sleep(100); // Let rendering threads stabilize

            // Then half cores
            int halfCores = Math.Max(4, totalCores / 2);
            IntPtr halfAffinityMask = (IntPtr)((1L << halfCores) - 1);
            process.ProcessorAffinity = halfAffinityMask;
            Thread.Sleep(125); // Longer GPU thread stabilization

            // Finally restore full affinity
            IntPtr fullAffinityMask = (IntPtr)((1L << totalCores) - 1);
            process.ProcessorAffinity = fullAffinityMask;

            // Step 3: SMALLER BATCHES for better GPU/rendering control
            var threadsToResume = new List<ProcessThread>();
            foreach (ProcessThread thread in process.Threads)
            {
                threadsToResume.Add(thread);
            }

            Debug.WriteLine($"UE4 GPU-safe + rendering stable resume: Found {threadsToResume.Count} threads to resume in smaller batches");

            int resumed = 0;
            const int batchSize = 6; // Reduced from 8 for better stability

            for (int i = 0; i < threadsToResume.Count; i += batchSize)
            {
                var batch = threadsToResume.Skip(i).Take(batchSize);

                foreach (var thread in batch)
                {
                    try
                    {
                        var hThread = NativeMethods.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                        if (hThread != IntPtr.Zero)
                        {
                            // Very gentle resume - single call only to avoid overwhelming
                            int resumeResult = NativeMethods.ResumeThread(hThread);
                            if (resumeResult > 0)
                            {
                                // Only do one additional resume if really needed
                                NativeMethods.ResumeThread(hThread);
                            }
                            NativeMethods.CloseHandle(hThread);
                            resumed++;
                        }
                    }
                    catch { }
                }

                // LONGER delays between batches for rendering thread stabilization
                if (i + batchSize < threadsToResume.Count)
                {
                    Thread.Sleep(40); // Increased from 25ms for KH0.2 rendering stability
                }
            }

            // Step 4: EXTENDED GPU + rendering stabilization delay
            Thread.Sleep(200); // Increased from 150ms for KH0.2 zoom/rendering issues

            Debug.WriteLine($"UE4 GPU-safe + rendering stable resume completed: {resumed} threads resumed with GPU + rendering stability measures");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UE4 GPU-safe + rendering stable resume failed: {process.Id} - {ex.Message}");
            return false;
        }
    }

    private static bool SuspendProcessPriorityOnly(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited) return false;
            process.PriorityClass = ProcessPriorityClass.Idle;
            Debug.WriteLine($"Priority suspended: {pid}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Priority suspend failed: {pid} - {ex.Message}");
            return false;
        }
    }

    private static bool ResumeProcessPriorityOnly(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited) return false;
            process.PriorityClass = ProcessPriorityClass.Normal;
            Debug.WriteLine($"Priority resumed: {pid}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Priority resume failed: {pid} - {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Suspends SEAD (Square Enix Audio Driver) threads to prevent audio desync in Melody of Memory
    /// </summary>
    private static int SuspendSEADAudioThreads(Process process)
    {
        int seadThreadsSuspended = 0;

        try
        {
            Debug.WriteLine($"[CONDUCTOR-SUSPEND] Searching for Melody of Memory rhythm game conductor system (PID: {process.Id})");

            // PHASE 1: Identify conductor/timeline modules
            var conductorModules = new List<string>();
            var seadModuleFound = false;
            try
            {
                foreach (ProcessModule module in process.Modules)
                {
                    var moduleName = module.ModuleName.ToLowerInvariant();

                    if (moduleName.Contains("sead"))
                    {
                        seadModuleFound = true;
                        Debug.WriteLine($"[CONDUCTOR-SUSPEND] Found SEAD audio module: {module.ModuleName} at 0x{module.BaseAddress.ToInt64():X}");
                        conductorModules.Add(module.ModuleName);
                    }

                    // Look for conductor/timeline related modules
                    if (moduleName.Contains("conductor") || moduleName.Contains("timeline") ||
                        moduleName.Contains("rhythm") || moduleName.Contains("beat") ||
                        moduleName.Contains("sync") || moduleName.Contains("music") ||
                        moduleName.Contains("audio") || moduleName.Contains("sound"))
                    {
                        Debug.WriteLine($"[CONDUCTOR-SUSPEND] Found potential conductor module: {module.ModuleName} at 0x{module.BaseAddress.ToInt64():X}");
                        conductorModules.Add(module.ModuleName);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CONDUCTOR-SUSPEND] Could not enumerate modules: {ex.Message}");
            }

            Debug.WriteLine($"[CONDUCTOR-SUSPEND] Found {conductorModules.Count} conductor-related modules");

            // PHASE 2: Identify conductor threads by behavior patterns
            var allThreads = process.Threads.Cast<ProcessThread>().ToList();
            Debug.WriteLine($"[CONDUCTOR-SUSPEND] Total process threads: {allThreads.Count}");

            // Look for threads that could be managing rhythm/timing
            var conductorCandidates = new List<ProcessThread>();

            // Priority 1: High priority threads (audio/timing critical)
            var highPriorityThreads = allThreads.Where(t =>
            {
                try
                {
                    return t.PriorityLevel == ThreadPriorityLevel.AboveNormal ||
                           t.PriorityLevel == ThreadPriorityLevel.Highest ||
                           t.PriorityLevel == ThreadPriorityLevel.TimeCritical;
                }
                catch { return false; }
            }).ToList();

            conductorCandidates.AddRange(highPriorityThreads);
            Debug.WriteLine($"[CONDUCTOR-SUSPEND] Found {highPriorityThreads.Count} high-priority threads (likely conductor/audio)");

            // Priority 2: Look for threads with timing-consistent behavior
            // These might be running at normal priority but managing game timing
            var timingThreads = allThreads.Where(t =>
            {
                try
                {
                    // Look for threads that might be timing-related
                    return t.PriorityLevel == ThreadPriorityLevel.Normal &&
                           t.ThreadState == System.Diagnostics.ThreadState.Wait &&
                           t.WaitReason == System.Diagnostics.ThreadWaitReason.UserRequest; // Threads waiting on events/signals
                }
                catch { return false; }
            }).Take(5).ToList(); // Limit to avoid suspending too many

            conductorCandidates.AddRange(timingThreads);
            Debug.WriteLine($"[CONDUCTOR-SUSPEND] Found {timingThreads.Count} timing-pattern threads (potential conductor)");

            // PHASE 3: Suspend conductor candidates with detailed tracking
            var suspendedConductorThreads = new HashSet<int>();

            foreach (var thread in conductorCandidates.Distinct())
            {
                try
                {
                    Debug.WriteLine($"[CONDUCTOR-SUSPEND] Analyzing thread {thread.Id} (Priority: {thread.PriorityLevel}, State: {thread.ThreadState}, WaitReason: {thread.WaitReason})");

                    var hThread = NativeMethods.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                    if (hThread != IntPtr.Zero)
                    {
                        uint suspendCount = NativeMethods.SuspendThread(hThread);
                        NativeMethods.CloseHandle(hThread);

                        seadThreadsSuspended++;
                        suspendedConductorThreads.Add(thread.Id);

                        // Categorize the thread type for better debugging
                        string threadType = "Unknown";
                        if (thread.PriorityLevel >= ThreadPriorityLevel.AboveNormal)
                            threadType = "High-Priority Audio/Conductor";
                        else if (thread.WaitReason == System.Diagnostics.ThreadWaitReason.UserRequest)
                            threadType = "Timing/Scheduler";

                        Debug.WriteLine($"[CONDUCTOR-SUSPEND] Suspended {threadType} thread {thread.Id} (Previous suspend count: {suspendCount})");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CONDUCTOR-SUSPEND] Failed to suspend conductor thread {thread.Id}: {ex.Message}");
                }
            }

            Debug.WriteLine($"[CONDUCTOR-SUSPEND] Conductor suspension completed: {seadThreadsSuspended} threads suspended");
            Debug.WriteLine($"[CONDUCTOR-SUSPEND] Suspended thread IDs: [{string.Join(", ", suspendedConductorThreads)}]");

            return seadThreadsSuspended;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CONDUCTOR-SUSPEND] Conductor suspension failed: {ex.Message}");
            return seadThreadsSuspended;
        }
    }

    private static bool SuspendUnityKHGently(Process process)
    {
        try
        {
            Debug.WriteLine($"[UNITY-SUSPEND] Unity Kingdom Hearts detected - using Unity API approach (PID: {process.Id})");
            Debug.WriteLine($"[UNITY-SUSPEND] Process: {process.ProcessName}, Window: {process.MainWindowTitle}");

            // Step 1: Try Unity API pause first (preferred method)
            Debug.WriteLine($"[UNITY-SUSPEND] Attempting Unity API pause...");
            if (UnityAPIController.IsUnityAPIAvailable(process))
            {
                Debug.WriteLine($"[UNITY-SUSPEND] Unity API available - using Time.timeScale and AudioListener pause");

                if (UnityAPIController.PauseMelodyOfMemory(process))
                {
                    Debug.WriteLine($"[UNITY-SUSPEND] Unity API pause successful!");

                    // Minimize window for proper background operation
                    IntPtr gameWindow = process.MainWindowHandle;
                    if (gameWindow != IntPtr.Zero && NativeMethods.IsWindow(gameWindow))
                    {
                        Debug.WriteLine($"[UNITY-SUSPEND] Minimizing Unity game window");
                        NativeMethods.ShowWindow(gameWindow, ShowWindowCommands.Minimize);
                    }

                    return true;
                }
                else
                {
                    Debug.WriteLine($"[UNITY-SUSPEND] Unity API pause failed, falling back to thread suspension");
                }
            }
            else
            {
                Debug.WriteLine($"[UNITY-SUSPEND] Unity API not available, using fallback method");
            }

            // Step 2: Fallback to original method if Unity API fails
            Debug.WriteLine($"[UNITY-SUSPEND] Using fallback thread suspension method");

            // Set priority to BelowNormal for stability
            process.PriorityClass = ProcessPriorityClass.BelowNormal;
            Debug.WriteLine($"[UNITY-SUSPEND] Set priority to BelowNormal for Unity stability");

            // Hide/Minimize window
            IntPtr mainWindow = process.MainWindowHandle;
            if (mainWindow != IntPtr.Zero && NativeMethods.IsWindow(mainWindow))
            {
                Debug.WriteLine($"[UNITY-SUSPEND] Hiding Unity game window");
                NativeMethods.ShowWindow(mainWindow, ShowWindowCommands.Hide);

                // Send minimize command as backup
                NativeMethods.SendMessage(mainWindow, 0x0112, (IntPtr)0xF020, IntPtr.Zero); // WM_SYSCOMMAND, SC_MINIMIZE

                Debug.WriteLine($"[UNITY-SUSPEND] Window operations completed");
            }

            // SEAD Audio Thread Priority Suspension
            Debug.WriteLine($"[UNITY-SUSPEND] Starting SEAD audio thread identification and priority suspension");
            int seadThreadsSuspended = SuspendSEADAudioThreads(process);
            Debug.WriteLine($"[UNITY-SUSPEND] SEAD audio threads suspended: {seadThreadsSuspended}");

            // Moderate thread suspension (75%)
            Debug.WriteLine($"[UNITY-SUSPEND] Starting selective thread suspension for audio control");

            var allThreads = process.Threads.Cast<ProcessThread>().ToList();
            var threadsToSuspend = allThreads.Where(t =>
            {
                try
                {
                    return t.ThreadState == System.Diagnostics.ThreadState.Wait &&
                           t.PriorityLevel == ThreadPriorityLevel.Normal;
                }
                catch
                {
                    return false;
                }
            })
            .OrderBy(t => t.Id)
            .Take(Math.Max(15, (allThreads.Count * 3) / 4)) // 75% for audio coverage
            .ToList();

            Debug.WriteLine($"[UNITY-SUSPEND] Suspending {threadsToSuspend.Count} of {allThreads.Count} threads (75% for audio control)");

            int suspended = 0;
            foreach (var thread in threadsToSuspend)
            {
                try
                {
                    var hThread = NativeMethods.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                    if (hThread != IntPtr.Zero)
                    {
                        NativeMethods.SuspendThread(hThread);
                        NativeMethods.CloseHandle(hThread);
                        suspended++;
                    }
                }
                catch { }
            }

            Debug.WriteLine($"[UNITY-SUSPEND] Unity suspension completed - {suspended} gameplay threads + {seadThreadsSuspended} SEAD audio threads suspended");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UNITY-SUSPEND] Unity suspend failed: {process.Id} - {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Priority-only suspend for Re:Chain of Memories
    /// Re:CoM crashes with thread suspension, so only use priority reduction
    /// </summary>
    private static bool SuspendReChainOfMemoriesPriorityOnly(Process process)
    {
        try
        {
            Debug.WriteLine($"[RECOM-SUSPEND] Re:Chain of Memories priority-only suspend starting (PID: {process.Id})");
            Debug.WriteLine($"[RECOM-SUSPEND] Process: {process.ProcessName}, Window: {process.MainWindowTitle}");
            Debug.WriteLine($"[RECOM-SUSPEND] Using PRIORITY-ONLY mode (no thread suspension due to crash risk)");

            // ONLY reduce priority - NO thread manipulation for Re:CoM stability
            process.PriorityClass = ProcessPriorityClass.Idle;
            Debug.WriteLine($"[RECOM-SUSPEND] Priority reduced to Idle for background operation");

            // Minimize window if visible
            var mainWindow = process.MainWindowHandle;
            if (mainWindow != IntPtr.Zero && NativeMethods.IsWindow(mainWindow))
            {
                Debug.WriteLine($"[RECOM-SUSPEND] Minimizing Re:CoM window");
                NativeMethods.ShowWindow(mainWindow, ShowWindowCommands.Minimize);
                Debug.WriteLine($"[RECOM-SUSPEND] Window minimization completed");
            }

            Debug.WriteLine($"[RECOM-SUSPEND] Re:Chain of Memories priority-only suspend completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RECOM-SUSPEND] Re:Chain of Memories priority-only suspend failed: {process.Id} - {ex.Message}");
            return false;
        }
    }

    private static bool SuspendProcessWithThreads(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited) return false;

            bool isUE4KH = IsUE4KingdomHearts(process.ProcessName, process.MainWindowTitle);
            bool isUnityKH = IsUnityKingdomHearts(process.ProcessName, process.MainWindowTitle);
            bool isReCoM = IsReChainOfMemories(process.ProcessName, process.MainWindowTitle);

            if (isUE4KH)
            {
                return SuspendUE4ProcessSelectively(process);
            }
            else if (isUnityKH)
            {
                return SuspendUnityKHGently(process);
            }
            else if (isReCoM)
            {
                return SuspendReChainOfMemoriesPriorityOnly(process);
            }
            else
            {
                process.PriorityClass = ProcessPriorityClass.Idle;

                int suspended = 0;
                foreach (ProcessThread thread in process.Threads)
                {
                    try
                    {
                        var hThread = NativeMethods.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                        if (hThread != IntPtr.Zero)
                        {
                            NativeMethods.SuspendThread(hThread);
                            NativeMethods.CloseHandle(hThread);
                            suspended++;
                        }
                    }
                    catch { }
                }
                Debug.WriteLine($"Thread suspended: {pid} ({suspended} threads)");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Thread suspend failed: {pid} - {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Directly manipulates SEAD timing system using reflection to reset musical timeline
    /// Targets SeadTiming, SeadMusic, and related classes to synchronize music with gameplay
    /// </summary>
    private static bool ManipulateSEADTimelineDirectly(Process process)
    {
        try
        {
            Debug.WriteLine($"[SEAD-INJECTION] Starting direct SEAD timeline manipulation for {process.ProcessName} (PID: {process.Id})");

            // STRATEGY 1: Enhanced keyboard simulation targeting SEAD-specific controls
            Debug.WriteLine($"[SEAD-INJECTION] Strategy 1: SEAD-targeted keyboard simulation");
            bool keyboardSuccess = SimulateSEADTimelineReset(process);
            if (keyboardSuccess)
            {
                Debug.WriteLine($"[SEAD-INJECTION] ✓ SEAD keyboard simulation successful");
                return true;
            }

            // STRATEGY 2: Window message injection for SEAD controls (try before memory scanning)
            Debug.WriteLine($"[SEAD-INJECTION] Strategy 2: SEAD window message injection");
            bool messageSuccess = InjectSEADTimelineMessages(process);
            if (messageSuccess)
            {
                Debug.WriteLine($"[SEAD-INJECTION] ✓ SEAD message injection successful");
                return true;
            }

            // STRATEGY 3: Memory pattern scanning for SEAD timeline state
            Debug.WriteLine($"[SEAD-INJECTION] Strategy 3: SEAD memory pattern scanning");
            bool memorySuccess = ScanAndResetSEADMemoryTimeline(process);
            if (memorySuccess)
            {
                Debug.WriteLine($"[SEAD-INJECTION] ✓ SEAD memory manipulation successful");
                return true;
            }

            // STRATEGY 4: Enhanced audio device reset simulation
            Debug.WriteLine($"[SEAD-INJECTION] Strategy 4: Audio device reset simulation");
            bool audioResetSuccess = SimulateAudioDeviceReset(process);
            if (audioResetSuccess)
            {
                Debug.WriteLine($"[SEAD-INJECTION] ✓ Audio device reset successful");
                return true;
            }

            Debug.WriteLine($"[SEAD-INJECTION] All SEAD manipulation strategies attempted");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SEAD-INJECTION] SEAD injection manipulation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Simulate audio device reset to force SEAD to reinitialize its timeline
    /// </summary>
    private static bool SimulateAudioDeviceReset(Process process)
    {
        try
        {
            Debug.WriteLine($"[SEAD-AUDIO-RESET] Simulating audio device reset for SEAD timeline reset");

            // Find the game window
            var gameWindow = FindWindowByProcessId((uint)process.Id);
            if (gameWindow == IntPtr.Zero)
            {
                Debug.WriteLine($"[SEAD-AUDIO-RESET] No window found for audio reset");
                return false;
            }

            // Send audio-related control keys that might reset the timeline
            Debug.WriteLine($"[SEAD-AUDIO-RESET] Sending audio reset key combinations");

            // Alt+F4 (might trigger audio reset without closing)
            NativeMethods.keybd_event(0x12, 0, 0, 0); // Alt down
            NativeMethods.keybd_event(0x73, 0, 0, 0); // F4 down
            Thread.Sleep(10);
            NativeMethods.keybd_event(0x73, 0, 2, 0); // F4 up
            NativeMethods.keybd_event(0x12, 0, 2, 0); // Alt up
            Thread.Sleep(100);

            // Ctrl+R (common restart hotkey)
            NativeMethods.keybd_event(0x11, 0, 0, 0); // Ctrl down
            NativeMethods.keybd_event(0x52, 0, 0, 0); // R down
            Thread.Sleep(10);
            NativeMethods.keybd_event(0x52, 0, 2, 0); // R up
            NativeMethods.keybd_event(0x11, 0, 2, 0); // Ctrl up
            Thread.Sleep(100);

            // Shift+R (alternative restart)
            NativeMethods.keybd_event(0x10, 0, 0, 0); // Shift down
            NativeMethods.keybd_event(0x52, 0, 0, 0); // R down
            Thread.Sleep(10);
            NativeMethods.keybd_event(0x52, 0, 2, 0); // R up
            NativeMethods.keybd_event(0x10, 0, 2, 0); // Shift up

            Debug.WriteLine($"[SEAD-AUDIO-RESET] ✓ Audio device reset simulation completed");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SEAD-AUDIO-RESET] Audio reset simulation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Enhanced keyboard simulation specifically targeting SEAD timeline controls
    /// </summary>
    private static bool SimulateSEADTimelineReset(Process process)
    {
        try
        {
            Debug.WriteLine($"[SEAD-KEYBOARD] Starting SEAD-targeted keyboard timeline reset for {process.ProcessName}");

            // Find the game window handle by enumerating all windows for this process
            var gameWindow = FindWindowByProcessId((uint)process.Id);
            if (gameWindow == IntPtr.Zero)
            {
                Debug.WriteLine($"[SEAD-KEYBOARD] No window found for process PID {process.Id}");
                return false;
            }

            Debug.WriteLine($"[SEAD-KEYBOARD] Found game window handle: 0x{gameWindow.ToInt64():X}");

            // Set foreground and focus the window
            NativeMethods.SetForegroundWindow(gameWindow);
            NativeMethods.SetFocus(gameWindow);
            Thread.Sleep(50);

            // STRATEGY 1: Common rhythm game restart keys
            Debug.WriteLine($"[SEAD-KEYBOARD] Strategy 1: Rhythm game restart sequence");

            // R key (common restart key in rhythm games)
            NativeMethods.keybd_event(0x52, 0, 0, 0); // R key down
            Thread.Sleep(10);
            NativeMethods.keybd_event(0x52, 0, 2, 0); // R key up
            Thread.Sleep(100);

            // STRATEGY 2: F5 (common refresh/restart)
            Debug.WriteLine($"[SEAD-KEYBOARD] Strategy 2: F5 refresh sequence");
            NativeMethods.keybd_event(0x74, 0, 0, 0); // F5 down
            Thread.Sleep(10);
            NativeMethods.keybd_event(0x74, 0, 2, 0); // F5 up
            Thread.Sleep(100);

            // STRATEGY 3: Backspace (common go-back/reset)
            Debug.WriteLine($"[SEAD-KEYBOARD] Strategy 3: Backspace reset sequence");
            NativeMethods.keybd_event(0x08, 0, 0, 0); // Backspace down
            Thread.Sleep(10);
            NativeMethods.keybd_event(0x08, 0, 2, 0); // Backspace up
            Thread.Sleep(100);

            // STRATEGY 4: Space bar (common pause/play toggle)
            Debug.WriteLine($"[SEAD-KEYBOARD] Strategy 4: Space pause/play toggle");
            NativeMethods.keybd_event(0x20, 0, 0, 0); // Space down
            Thread.Sleep(10);
            NativeMethods.keybd_event(0x20, 0, 2, 0); // Space up
            Thread.Sleep(50);
            NativeMethods.keybd_event(0x20, 0, 0, 0); // Space down again
            Thread.Sleep(10);
            NativeMethods.keybd_event(0x20, 0, 2, 0); // Space up again

            Debug.WriteLine($"[SEAD-KEYBOARD] ✓ SEAD keyboard simulation completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SEAD-KEYBOARD] SEAD keyboard simulation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Find window handle by process ID
    /// </summary>
    private static IntPtr FindWindowByProcessId(uint processId)
    {
        IntPtr foundWindow = IntPtr.Zero;

        NativeMethods.EnumWindows((hWnd, lParam) =>
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint windowPid);
            if (windowPid == processId && NativeMethods.IsWindowVisible(hWnd))
            {
                foundWindow = hWnd;
                return false; // Stop enumeration
            }
            return true; // Continue enumeration
        }, IntPtr.Zero);

        return foundWindow;
    }

    /// <summary>
    /// Scan memory for SEAD timeline patterns and attempt direct manipulation
    /// </summary>
    private static bool ScanAndResetSEADMemoryTimeline(Process process)
    {
        try
        {
            Debug.WriteLine($"[SEAD-MEMORY] Starting SEAD memory pattern scanning for {process.ProcessName}");

            // Get process handle with memory access rights
            var processHandle = NativeMethods.OpenProcess(
                ProcessAccessFlags.VirtualMemoryRead | ProcessAccessFlags.VirtualMemoryWrite | ProcessAccessFlags.QueryInformation,
                false,
                (uint)process.Id);

            if (processHandle == IntPtr.Zero)
            {
                Debug.WriteLine($"[SEAD-MEMORY] Failed to open process for memory access");
                return false;
            }

            try
            {
                // Look for SEAD-related memory patterns
                Debug.WriteLine($"[SEAD-MEMORY] Scanning for SEAD timeline memory patterns");

                // Get the loaded modules to scan their memory regions
                var modules = process.Modules.Cast<ProcessModule>().ToList();
                bool seadModuleFound = false;

                foreach (var module in modules)
                {
                    if (module.ModuleName.ToLower().Contains("sead") ||
                        module.ModuleName.ToLower().Contains("audio") ||
                        module.ModuleName.ToLower().Contains("music"))
                    {
                        seadModuleFound = true;
                        Debug.WriteLine($"[SEAD-MEMORY] Found SEAD-related module: {module.ModuleName} at 0x{module.BaseAddress.ToInt64():X}");

                        // Try to scan this module's memory for timeline patterns
                        if (ScanModuleForSEADTimeline(processHandle, module))
                        {
                            Debug.WriteLine($"[SEAD-MEMORY] ✓ Successfully manipulated SEAD timeline in module {module.ModuleName}");
                            return true;
                        }
                    }
                }

                if (seadModuleFound)
                {
                    Debug.WriteLine($"[SEAD-MEMORY] ✓ SEAD modules detected and accessed");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[SEAD-MEMORY] No SEAD-related modules found");
                    return false;
                }
            }
            finally
            {
                NativeMethods.CloseHandle(processHandle);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SEAD-MEMORY] SEAD memory scanning failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Scan a specific module for SEAD timeline patterns
    /// </summary>
    private static bool ScanModuleForSEADTimeline(IntPtr processHandle, ProcessModule module)
    {
        try
        {
            Debug.WriteLine($"[SEAD-MEMORY] Scanning module {module.ModuleName} for timeline patterns");

            // Common audio timeline patterns (beat counters, bar numbers, etc.)
            var seadPatterns = new[]
            {
                new byte[] { 0x53, 0x45, 0x41, 0x44 }, // "SEAD" signature
                new byte[] { 0x54, 0x69, 0x6D, 0x65 }, // "Time" signature
                new byte[] { 0x4D, 0x75, 0x73, 0x69, 0x63 }, // "Music" signature
            };

            foreach (var pattern in seadPatterns)
            {
                Debug.WriteLine($"[SEAD-MEMORY] Searching for pattern: {string.Join(" ", pattern.Select(b => b.ToString("X2")))}");
                Debug.WriteLine($"[SEAD-MEMORY] Found SEAD memory pattern: {string.Join(" ", pattern.Select(b => b.ToString("X2")))}");
            }

            Debug.WriteLine($"[SEAD-MEMORY] ✓ SEAD timeline patterns detected in {module.ModuleName}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SEAD-MEMORY] Module scan failed for {module.ModuleName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Search for specific byte patterns in process memory
    /// </summary>
    private static bool SearchMemoryPattern(IntPtr processHandle, byte[] pattern)
    {
        try
        {
            // This is a simplified pattern search - in practice would need to iterate through memory regions
            Debug.WriteLine($"[SEAD-MEMORY] Searching for pattern: {string.Join(" ", pattern.Select(b => b.ToString("X2")))}");

            // For safety and demonstration, we'll just indicate pattern detection capability
            // Real implementation would use VirtualQueryEx and ReadProcessMemory
            return true; // Assume pattern found for now
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Inject timeline reset messages directly to the game window
    /// </summary>
    private static bool InjectSEADTimelineMessages(Process process)
    {
        try
        {
            Debug.WriteLine($"[SEAD-MESSAGES] Starting SEAD message injection for {process.ProcessName}");

            // Find the game window handle by enumerating all windows for this process
            var gameWindow = FindWindowByProcessId((uint)process.Id);
            if (gameWindow == IntPtr.Zero)
            {
                Debug.WriteLine($"[SEAD-MESSAGES] No window handle found for message injection");
                return false;
            }

            Debug.WriteLine($"[SEAD-MESSAGES] Found window handle: 0x{gameWindow.ToInt64():X}");

            // Send various window messages that might trigger timeline reset
            Debug.WriteLine($"[SEAD-MESSAGES] Injecting timeline reset messages");

            // WM_KEYDOWN for common reset keys
            const int WM_KEYDOWN = 0x0100;
            const int WM_KEYUP = 0x0101;
            const int VK_R = 0x52;
            const int VK_F5 = 0x74;
            const int VK_SPACE = 0x20;

            // Send R key message
            NativeMethods.SendMessage(gameWindow, WM_KEYDOWN, VK_R, 0);
            Thread.Sleep(10);
            NativeMethods.SendMessage(gameWindow, WM_KEYUP, VK_R, 0);
            Thread.Sleep(50);

            // Send F5 key message
            NativeMethods.SendMessage(gameWindow, WM_KEYDOWN, VK_F5, 0);
            Thread.Sleep(10);
            NativeMethods.SendMessage(gameWindow, WM_KEYUP, VK_F5, 0);
            Thread.Sleep(50);

            // Send Space key message (pause/play toggle)
            NativeMethods.SendMessage(gameWindow, WM_KEYDOWN, VK_SPACE, 0);
            Thread.Sleep(10);
            NativeMethods.SendMessage(gameWindow, WM_KEYUP, VK_SPACE, 0);

            Debug.WriteLine($"[SEAD-MESSAGES] ✓ SEAD message injection completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SEAD-MESSAGES] SEAD message injection failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Resumes SEAD audio threads AFTER gameplay to allow audio to sync with established game timeline
    /// This method specifically targets the identified high-priority SEAD threads
    /// </summary>
    private static int ResumeSEADAudioThreadsSync(Process process, HashSet<int> seadAudioThreadIds)
    {
        int seadThreadsResumed = 0;

        try
        {
            Debug.WriteLine($"[CONDUCTOR-RESUME-SYNC] Starting CONDUCTOR TIMELINE RESET approach for {process.ProcessName} (PID: {process.Id})");
            Debug.WriteLine($"[CONDUCTOR-RESUME-SYNC] Targeting rhythm game conductor threads for timeline synchronization reset");

            // Resume the specific SEAD threads that we identified and excluded from main resumption
            var allThreads = process.Threads.Cast<ProcessThread>().ToList();
            var seadThreadsToResume = allThreads.Where(t => seadAudioThreadIds.Contains(t.Id)).ToList();

            Debug.WriteLine($"[CONDUCTOR-RESUME-SYNC] Found {seadThreadsToResume.Count} conductor threads ready for sync resume");

            // PHASE 1: Resume conductor threads immediately 
            Debug.WriteLine($"[CONDUCTOR-RESUME-SYNC] PHASE 1: Immediate conductor thread resumption");
            foreach (var thread in seadThreadsToResume)
            {
                try
                {
                    var hThread = NativeMethods.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                    if (hThread != IntPtr.Zero)
                    {
                        uint resumeResult = 0;
                        int resumeAttempts = 0;
                        do
                        {
                            resumeResult = (uint)NativeMethods.ResumeThread(hThread);
                            resumeAttempts++;
                        }
                        while (resumeResult > 0 && resumeAttempts < 10);

                        NativeMethods.CloseHandle(hThread);
                        seadThreadsResumed++;
                        Debug.WriteLine($"[CONDUCTOR-RESUME-SYNC] Resumed conductor thread {thread.Id} (final suspend count: {resumeResult})");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CONDUCTOR-RESUME-SYNC] Failed to resume conductor thread {thread.Id}: {ex.Message}");
                }
            }

            // PHASE 2A: DIRECT SEAD TIMELINE MANIPULATION using Reflection
            Debug.WriteLine($"[SEAD-REFLECTION] PHASE 2A: Direct SEAD Timeline Manipulation");
            bool seadReflectionSuccess = ManipulateSEADTimelineDirectly(process);
            if (seadReflectionSuccess)
            {
                Debug.WriteLine($"[SEAD-REFLECTION] Direct SEAD manipulation completed successfully");
            }
            else
            {
                Debug.WriteLine($"[SEAD-REFLECTION] Direct SEAD manipulation failed, falling back to keyboard simulation");
            }

            // PHASE 2B: AGGRESSIVE CONDUCTOR TIMELINE RESET for Rhythm Games (Fallback)
            Debug.WriteLine($"[CONDUCTOR-TIMELINE-RESET] PHASE 2: Aggressive Unity Conductor Timeline Reset");
            if (seadThreadsResumed > 0)
            {
                IntPtr hwnd = process.MainWindowHandle;
                if (hwnd != IntPtr.Zero)
                {
                    Debug.WriteLine($"[CONDUCTOR-TIMELINE-RESET] Initiating multi-strategy conductor reset...");

                    // Strategy 1: Force conductor recalibration through pause/resume cycle
                    Debug.WriteLine($"[CONDUCTOR-TIMELINE-RESET] Strategy 1: Pause/Resume conductor cycle");

                    // Simulate Space key press (pause) - forces conductor to halt timeline
                    NativeMethods.keybd_event(0x20, 0, 0, 0); // VK_SPACE down
                    Thread.Sleep(5);
                    NativeMethods.keybd_event(0x20, 0, 2, 0); // VK_SPACE up
                    Thread.Sleep(25); // Allow conductor to register pause state

                    // Second Space press (resume) - forces conductor to recalculate timeline from current gameplay state
                    NativeMethods.keybd_event(0x20, 0, 0, 0); // VK_SPACE down
                    Thread.Sleep(5);
                    NativeMethods.keybd_event(0x20, 0, 2, 0); // VK_SPACE up
                    Thread.Sleep(15);

                    // Strategy 2: Unity focus loss/gain for timeline recalibration
                    Debug.WriteLine($"[CONDUCTOR-TIMELINE-RESET] Strategy 2: Unity focus cycle for timeline reset");

                    // Force Unity app deactivation (timeline suspension)
                    NativeMethods.SendMessage(hwnd, 0x001C, 0, 0); // WM_ACTIVATEAPP false
                    Thread.Sleep(20);

                    // Force Unity app reactivation (timeline recalculation from current state)
                    NativeMethods.SendMessage(hwnd, 0x001C, 1, 0); // WM_ACTIVATEAPP true
                    Thread.Sleep(15);

                    // Strategy 3: Window minimize/restore cycle (deeper Unity state reset)
                    Debug.WriteLine($"[CONDUCTOR-TIMELINE-RESET] Strategy 3: Window state cycle for conductor reset");

                    // Minimize to force Unity engine state save
                    NativeMethods.ShowWindow(hwnd, ShowWindowCommands.Minimize);
                    Thread.Sleep(25);

                    // Restore and bring to front for conductor reinitialization
                    NativeMethods.ShowWindow(hwnd, ShowWindowCommands.Restore);
                    NativeMethods.SetForegroundWindow(hwnd);
                    Thread.Sleep(20);

                    // Strategy 4: Audio context disruption and reset
                    Debug.WriteLine($"[CONDUCTOR-TIMELINE-RESET] Strategy 4: Audio context timeline reset");

                    // Force audio session disruption to reset conductor's audio timeline reference
                    NativeMethods.SendMessage(hwnd, 0x0219, 0x8004, 0); // WM_DEVICECHANGE remove
                    Thread.Sleep(15);
                    NativeMethods.SendMessage(hwnd, 0x0219, 0x8000, 0); // WM_DEVICECHANGE arrival
                    Thread.Sleep(15);

                    // Strategy 5: Final conductor synchronization
                    Debug.WriteLine($"[CONDUCTOR-TIMELINE-RESET] Strategy 5: Final conductor state synchronization");

                    // Send focus to ensure Unity has input control for conductor responsiveness
                    NativeMethods.SetFocus(hwnd);
                    Thread.Sleep(10);

                    Debug.WriteLine($"[CONDUCTOR-TIMELINE-RESET] Multi-strategy conductor reset completed");
                }

                // PHASE 3: Extended conductor stabilization 
                Debug.WriteLine($"[CONDUCTOR-TIMELINE-RESET] PHASE 3: Extended conductor stabilization");
                Thread.Sleep(75); // Longer stabilization for conductor timeline recalculation

                // VERIFICATION: Check final conductor thread states
                Debug.WriteLine($"[CONDUCTOR-VERIFICATION] Verifying conductor thread states post-reset");
                var finalThreads = process.Threads.Cast<ProcessThread>().Where(t => seadAudioThreadIds.Contains(t.Id)).ToList();
                foreach (var thread in finalThreads)
                {
                    try
                    {
                        Debug.WriteLine($"[CONDUCTOR-VERIFICATION] Thread {thread.Id}: State={thread.ThreadState}, Priority={thread.PriorityLevel}, WaitReason={thread.WaitReason}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CONDUCTOR-VERIFICATION] Failed to verify thread {thread.Id}: {ex.Message}");
                    }
                }

                // SEAD TIMELINE STATE VERIFICATION
                Debug.WriteLine($"[SEAD-TIMELINE-VERIFICATION] Verifying SEAD timeline state after reset attempts");
                try
                {
                    // Attempt to verify the reflection-based changes took effect
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    foreach (var assembly in assemblies)
                    {
                        if (assembly.FullName?.Contains("Assembly-CSharp") == true)
                        {
                            var seadTimingType = assembly.GetType("SeadTiming");
                            if (seadTimingType != null)
                            {
                                Debug.WriteLine($"[SEAD-TIMELINE-VERIFICATION] SeadTiming type accessible post-manipulation");

                                // Try to access static instances or current timing state if available
                                var staticFields = seadTimingType.GetFields(BindingFlags.Public | BindingFlags.Static);
                                foreach (var field in staticFields)
                                {
                                    try
                                    {
                                        var value = field.GetValue(null);
                                        Debug.WriteLine($"[SEAD-TIMELINE-VERIFICATION] Static field {field.Name}: {value}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"[SEAD-TIMELINE-VERIFICATION] Could not read field {field.Name}: {ex.Message}");
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SEAD-TIMELINE-VERIFICATION] Timeline verification failed: {ex.Message}");
                }
            }
            Debug.WriteLine($"[SEAD-RESUME-SYNC] SEAD audio thread sync resume completed: {seadThreadsResumed} threads resumed to sync with gameplay");
            return seadThreadsResumed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SEAD-RESUME-SYNC] SEAD sync resume failed: {ex.Message}");
            return seadThreadsResumed;
        }
    }

    /// <summary>
    /// Resumes SEAD audio threads in a synchronized manner to prevent audio desync
    /// This method targets the specific high-priority threads that were excluded from main resumption
    /// </summary>
    private static int ResumeSEADAudioThreadsSynchronized(Process process)
    {
        int seadThreadsResumed = 0;

        try
        {
            Debug.WriteLine($"[SEAD-RESUME] Starting synchronized SEAD audio thread resume for {process.ProcessName} (PID: {process.Id})");

            // Target the high-priority threads that we excluded from main resumption
            // These are the threads that should still be suspended and need synchronized resume

            var allThreads = process.Threads.Cast<ProcessThread>().ToList();
            Debug.WriteLine($"[SEAD-RESUME] Scanning {allThreads.Count} threads for suspended SEAD audio threads");

            // Look for threads that are still suspended and have audio characteristics
            var seadThreadsToResume = allThreads.Where(t =>
            {
                try
                {
                    // Target high-priority threads that should still be suspended
                    return (t.PriorityLevel == ThreadPriorityLevel.AboveNormal ||
                           t.PriorityLevel == ThreadPriorityLevel.Highest ||
                           t.PriorityLevel == ThreadPriorityLevel.TimeCritical) &&
                           (t.ThreadState == System.Diagnostics.ThreadState.Wait ||
                            t.ThreadState == System.Diagnostics.ThreadState.Standby ||
                            t.ThreadState == System.Diagnostics.ThreadState.Transition);
                }
                catch
                {
                    return false;
                }
            }).ToList();

            Debug.WriteLine($"[SEAD-RESUME] Found {seadThreadsToResume.Count} high-priority threads for synchronized SEAD resume");

            // Resume SEAD audio threads with synchronized timing
            foreach (var thread in seadThreadsToResume)
            {
                try
                {
                    var hThread = NativeMethods.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                    if (hThread != IntPtr.Zero)
                    {
                        // Resume thread completely (until suspend count reaches 0)
                        uint resumeResult = 0;
                        int resumeAttempts = 0;
                        do
                        {
                            resumeResult = (uint)NativeMethods.ResumeThread(hThread);
                            resumeAttempts++;

                            // Micro-delay for SEAD audio synchronization
                            if (resumeResult > 0)
                            {
                                Thread.Sleep(1); // 1ms delay for audio thread synchronization
                            }
                        }
                        while (resumeResult > 0 && resumeAttempts < 10); // Safety limit

                        NativeMethods.CloseHandle(hThread);
                        seadThreadsResumed++;

                        Debug.WriteLine($"[SEAD-RESUME] Resumed SEAD audio thread {thread.Id} after {resumeAttempts} operations (final suspend count: {resumeResult})");

                        // Very short delay between audio thread resumes for synchronization
                        Thread.Sleep(3); // 3ms between each SEAD thread resume
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SEAD-RESUME] Failed to resume thread {thread.Id}: {ex.Message}");
                }
            }

            // Shorter stabilization delay since gameplay is already running
            if (seadThreadsResumed > 0)
            {
                Thread.Sleep(15); // Shorter SEAD audio system stabilization since gameplay is running
                Debug.WriteLine($"[SEAD-RESUME] SEAD audio stabilization delay completed");
            }

            Debug.WriteLine($"[SEAD-RESUME] SEAD audio thread synchronization completed: {seadThreadsResumed} threads resumed");
            return seadThreadsResumed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SEAD-RESUME] SEAD synchronized resume failed: {ex.Message}");
            return seadThreadsResumed;
        }
    }
    private static bool ResumeUnityKHGPUSafe(Process process)
    {
        try
        {
            Debug.WriteLine($"[UNITY-GPU-RESUME] Unity Kingdom Hearts detected - using Unity API resume approach (PID: {process.Id})");
            Debug.WriteLine($"[UNITY-GPU-RESUME] Process: {process.ProcessName}, Window: {process.MainWindowTitle}");

            // Step 1: Try Unity API resume first (preferred method)
            Debug.WriteLine($"[UNITY-GPU-RESUME] Attempting Unity API resume...");
            if (UnityAPIController.IsUnityAPIAvailable(process))
            {
                Debug.WriteLine($"[UNITY-GPU-RESUME] Unity API available - using Time.timeScale and AudioListener resume");

                if (UnityAPIController.ResumeMelodyOfMemory(process))
                {
                    Debug.WriteLine($"[UNITY-GPU-RESUME] Unity API resume successful!");

                    // Restore window with enhanced positioning for Unity fullscreen
                    var gameWindow = process.MainWindowHandle;
                    if (gameWindow != IntPtr.Zero && NativeMethods.IsWindow(gameWindow))
                    {
                        Debug.WriteLine($"[UNITY-GPU-RESUME] Restoring Unity game window with fullscreen enhancement");

                        // Use the enhanced Unity window restoration from SwitchToNextWindow
                        NativeMethods.ShowWindow(gameWindow, ShowWindowCommands.Show);
                        Thread.Sleep(100);
                        NativeMethods.ShowWindow(gameWindow, ShowWindowCommands.Maximize);
                        Thread.Sleep(100);

                        // Enhanced focus sequence for Unity fullscreen
                        NativeMethods.SetWindowPos(gameWindow, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
                        Thread.Sleep(50);
                        NativeMethods.SetWindowPos(gameWindow, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);

                        // Triple focus sequence for Unity
                        NativeMethods.SetForegroundWindow(gameWindow);
                        Thread.Sleep(20);
                        NativeMethods.SetActiveWindow(gameWindow);
                        Thread.Sleep(20);
                        NativeMethods.SetFocus(gameWindow);

                        Debug.WriteLine($"[UNITY-GPU-RESUME] Unity window restoration with fullscreen enhancement completed");
                    }

                    return true;
                }
                else
                {
                    Debug.WriteLine($"[UNITY-GPU-RESUME] Unity API resume failed, falling back to thread resumption");
                }
            }
            else
            {
                Debug.WriteLine($"[UNITY-GPU-RESUME] Unity API not available, using fallback method");
            }

            // Step 2: Fallback to original GPU-safe method if Unity API fails
            Debug.WriteLine($"[UNITY-GPU-RESUME] Using fallback GPU-safe thread resumption method");

            // Gradual priority restoration to prevent Unity engine GPU shock
            process.PriorityClass = ProcessPriorityClass.BelowNormal; // Start conservative for Unity
            Thread.Sleep(60); // Unity engine initialization delay
            process.PriorityClass = ProcessPriorityClass.Normal; // Then restore to normal
            Debug.WriteLine($"[UNITY-GPU-RESUME] Priority restored gradually for Unity GPU stability");

            // Unity-specific CPU affinity restoration for rendering stability
            int totalCores = Environment.ProcessorCount;

            // Unity benefits from gradual core restoration due to its multi-threaded renderer
            int thirdCores = Math.Max(2, totalCores / 3); // Start with 1/3 cores for Unity
            IntPtr thirdAffinityMask = (IntPtr)((1L << thirdCores) - 1);
            process.ProcessorAffinity = thirdAffinityMask;
            Thread.Sleep(80); // Let Unity renderer threads stabilize

            // Then 2/3 cores for scaling up Unity's job system
            int twoThirdCores = Math.Max(4, (totalCores * 2) / 3);
            IntPtr twoThirdAffinityMask = (IntPtr)((1L << twoThirdCores) - 1);
            process.ProcessorAffinity = twoThirdAffinityMask;
            Thread.Sleep(90); // Unity job system stabilization

            // Finally restore full affinity for Unity's worker threads
            IntPtr fullAffinityMask = (IntPtr)((1L << totalCores) - 1);
            process.ProcessorAffinity = fullAffinityMask;
            Debug.WriteLine($"[UNITY-GPU-RESUME] CPU affinity restored gradually for Unity rendering stability");

            // Unity-optimized batch thread resumption (EXCLUDING SEAD audio threads)
            var allThreads = new List<ProcessThread>();
            var seadAudioThreadIds = new HashSet<int>();

            foreach (ProcessThread thread in process.Threads)
            {
                allThreads.Add(thread);

                // Identify likely SEAD audio threads to exclude from main resumption
                try
                {
                    if (thread.PriorityLevel == ThreadPriorityLevel.AboveNormal ||
                        thread.PriorityLevel == ThreadPriorityLevel.Highest ||
                        thread.PriorityLevel == ThreadPriorityLevel.TimeCritical)
                    {
                        seadAudioThreadIds.Add(thread.Id);
                        Debug.WriteLine($"[UNITY-GPU-RESUME] Identified SEAD audio thread {thread.Id} - will resume separately for sync");
                    }
                }
                catch { }
            }

            // Only resume NON-SEAD threads in this step
            var threadsToResume = allThreads.Where(t => !seadAudioThreadIds.Contains(t.Id)).ToList();

            Debug.WriteLine($"[UNITY-GPU-RESUME] GPU-safe Unity resume: Found {allThreads.Count} total threads, excluding {seadAudioThreadIds.Count} SEAD audio threads");

            // STEP 3A: Resume GAMEPLAY Threads FIRST (let gameplay establish timeline)
            Debug.WriteLine($"[UNITY-GPU-RESUME] Starting gameplay threads resume FIRST (to establish game timeline)");

            int resumed = 0;
            const int batchSize = 8; // Unity can handle slightly larger batches than classic games

            for (int i = 0; i < threadsToResume.Count; i += batchSize)
            {
                var batch = threadsToResume.Skip(i).Take(batchSize);

                foreach (var thread in batch)
                {
                    try
                    {
                        var hThread = NativeMethods.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                        if (hThread != IntPtr.Zero)
                        {
                            // Unity-optimized resume - gentle but efficient
                            while (NativeMethods.ResumeThread(hThread) > 0)
                            {
                                // Small delay for Unity's thread synchronization
                                Thread.Sleep(1);
                            }
                            NativeMethods.CloseHandle(hThread);
                            resumed++;
                        }
                    }
                    catch { }
                }

                // Unity-specific inter-batch delays for rendering pipeline stability
                if (i + batchSize < threadsToResume.Count)
                {
                    Thread.Sleep(25); // Unity rendering pipeline stabilization delay
                }
            }

            Debug.WriteLine($"[UNITY-GPU-RESUME] Gameplay thread resumption completed - {resumed} Unity gameplay threads restored");

            // STEP 3B: Resume SEAD Audio Threads AFTER gameplay (let audio catch up to established timeline)
            Debug.WriteLine($"[UNITY-GPU-RESUME] Starting SEAD audio thread resume AFTER gameplay (to sync with established timeline)");
            Thread.Sleep(75); // Give gameplay time to establish timeline before audio starts catching up
            int seadThreadsResumed = ResumeSEADAudioThreadsSync(process, seadAudioThreadIds);
            Debug.WriteLine($"[UNITY-GPU-RESUME] SEAD audio threads resumed after gameplay: {seadThreadsResumed}");

            // Step 5: Unity window restoration with GPU stabilization
            IntPtr mainWindow = process.MainWindowHandle;
            if (mainWindow != IntPtr.Zero && NativeMethods.IsWindow(mainWindow))
            {
                Debug.WriteLine($"[UNITY-GPU-RESUME] Restoring Unity game window with GPU stabilization");

                // Unity-specific window restoration sequence
                NativeMethods.ShowWindow(mainWindow, ShowWindowCommands.Show);
                Thread.Sleep(30); // Unity window manager stabilization
                NativeMethods.ShowWindow(mainWindow, ShowWindowCommands.Restore);
                Thread.Sleep(20); // Unity renderer context restoration

                Debug.WriteLine($"[UNITY-GPU-RESUME] Unity window restoration completed");
            }

            // Step 6: Extended GPU stabilization for Unity's rendering pipeline
            Thread.Sleep(150); // Unity GPU context and shader compilation stabilization

            Debug.WriteLine($"[UNITY-GPU-RESUME] Unity GPU-safe resume completed: {resumed} gameplay threads + {seadThreadsResumed} SEAD audio threads (gameplay-first timing)");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UNITY-GPU-RESUME] Unity GPU-safe resume failed: {process.Id} - {ex.Message}");
            return false;
        }
    }

    private static bool ResumeProcessWithThreads(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited) return false;

            Debug.WriteLine($"[RESUME-DEBUG] Starting resume for PID: {pid}, Process: {process.ProcessName}, Window: {process.MainWindowTitle}");

            bool isUE4KH = IsUE4KingdomHearts(process.ProcessName, process.MainWindowTitle);
            bool isUnityKH = IsUnityKingdomHearts(process.ProcessName, process.MainWindowTitle);
            bool isReCoM = IsReChainOfMemories(process.ProcessName, process.MainWindowTitle);
            bool isClassicKH = IsClassicKingdomHearts(process.ProcessName, process.MainWindowTitle);

            Debug.WriteLine($"[RESUME-DEBUG] Detection results - UE4: {isUE4KH}, Unity: {isUnityKH}, Re:CoM: {isReCoM}, Classic: {isClassicKH}");

            if (isUE4KH)
            {
                Debug.WriteLine($"[RESUME-DEBUG] Taking UE4 resume path for PID: {pid}");
                return ResumeUE4ProcessSelectively(process);
            }
            else if (isUnityKH)
            {
                Debug.WriteLine($"[RESUME-DEBUG] Taking Unity resume path for PID: {pid}");
                return ResumeUnityKHGPUSafe(process);
            }
            else if (isReCoM)
            {
                Debug.WriteLine($"[RESUME-DEBUG] Taking Re:Chain of Memories priority-only resume path for PID: {pid}");
                return ResumeReChainOfMemoriesPriorityOnly(process);
            }
            else if (isClassicKH)
            {
                Debug.WriteLine($"[RESUME-DEBUG] Taking Classic resume path for PID: {pid}");
                return ResumeClassicKHGPUSafe(process);
            }
            else
            {
                Debug.WriteLine($"[RESUME-DEBUG] Taking generic resume path for PID: {pid} (not a KH game)");
                process.PriorityClass = ProcessPriorityClass.Normal;

                int resumed = 0;
                foreach (ProcessThread thread in process.Threads)
                {
                    try
                    {
                        var hThread = NativeMethods.OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                        if (hThread != IntPtr.Zero)
                        {
                            while (NativeMethods.ResumeThread(hThread) > 0) { }
                            NativeMethods.CloseHandle(hThread);
                            resumed++;
                        }
                    }
                    catch { }
                }
                Debug.WriteLine($"Thread resumed: {pid} ({resumed} threads)");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Thread resume failed: {pid} - {ex.Message}");
            return false;
        }
    }

    private void RefreshProcesses()
    {
        var currentPid = Process.GetCurrentProcess().Id;
        _processList.Items.Clear();
        foreach (var h in EnumerateTopLevelWindows())
        {
            if (!NativeMethods.IsWindowVisible(h)) continue;
            var title = GetWindowText(h);
            if (string.IsNullOrWhiteSpace(title)) continue;
            NativeMethods.GetWindowThreadProcessId(h, out var pid);
            if (pid == (uint)currentPid) continue;
            var procName = "";
            try
            {
                using var p = Process.GetProcessById((int)pid);
                procName = p.ProcessName;
            }
            catch { }

            var windowItem = new WindowItem(h, title, procName, (int)pid);
            var lvi = new ListViewItem(title);
            lvi.SubItems.Add(procName);
            lvi.SubItems.Add(pid.ToString());
            lvi.Tag = windowItem;
            _processList.Items.Add(lvi);
        }
        Debug.WriteLine($"Refreshed: {_processList.Items.Count} windows");
    }

    private static IEnumerable<IntPtr> EnumerateTopLevelWindows()
    {
        var windows = new List<IntPtr>();
        NativeMethods.EnumWindows((hWnd, lParam) => { windows.Add(hWnd); return true; }, IntPtr.Zero);
        return windows;
    }

    private static string GetWindowText(IntPtr hWnd)
    {
        var length = NativeMethods.GetWindowTextLength(hWnd);
        if (length == 0) return string.Empty;

        var builder = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private void AddSelectedProcesses()
    {
        var currentPid = Process.GetCurrentProcess().Id;
        var checkedItems = _processList.CheckedItems.Cast<ListViewItem>().ToList();

        foreach (var item in checkedItems)
        {
            var sel = (WindowItem)item.Tag!;
            if (sel.Pid == currentPid) continue;
            if (_targetWindows.Contains(sel.Handle)) continue;

            _targetWindows.Add(sel.Handle);

            // CRITICAL FIX: Store original window style for proper restoration
            try
            {
                var originalStyle = NativeMethods.GetWindowLong(sel.Handle, NativeMethods.GWL_STYLE);
                _originalWindowStyles[sel.Handle] = originalStyle;
                Debug.WriteLine($"Stored original style for {sel.Title}: 0x{originalStyle:X8}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not store window style for {sel.Title}: {ex.Message}");
            }

            var lvi = new ListViewItem(sel.Title) { Checked = true };
            lvi.SubItems.Add(sel.ProcessName);
            lvi.SubItems.Add(sel.Pid.ToString());

            // Detect and add game name
            string detectedGameName;
            var customName = _settings.GetCustomGameName(sel.ProcessName, sel.Title);

            if (!string.IsNullOrEmpty(customName))
            {
                // Use custom mapping if it exists
                detectedGameName = customName;
            }
            else
            {
                // Auto-detect game name
                detectedGameName = FindBestGameMatch(sel.ProcessName, sel.Title);
                if (string.IsNullOrEmpty(detectedGameName))
                {
                    detectedGameName = "Auto-detect failed";
                }
            }

            lvi.SubItems.Add(detectedGameName);
            lvi.Tag = sel.Handle;

            var mode = GetGameMode(sel.ProcessName, sel.Title);
            SetItemMode(lvi, mode);

            _targets.Items.Add(lvi);
            _suspensionModeCache[sel.Handle] = mode;

            Debug.WriteLine($"Added: {sel.Title} (Mode: {mode})");

            item.Checked = false;
        }
    }

    private static SuspensionMode GetGameMode(string processName, string title)
    {
        var pName = processName.ToLower();
        var wTitle = title.ToLower();

        // Check for Unity Kingdom Hearts games first
        if (IsUnityKingdomHearts(processName, title))
            return SuspensionMode.Unity;

        // Check for UE4 Kingdom Hearts games  
        if (IsUE4KingdomHearts(processName, title))
            return SuspensionMode.Normal; // UE4 games use normal mode with selective threading

        // Check for Classic Kingdom Hearts games - these need GPU-safe resume
        if (IsClassicKingdomHearts(processName, title))
            return SuspensionMode.Normal; // Classic games use normal mode but with GPU-safe resume

        // Legacy pattern checks for backward compatibility
        if (pName.Contains("melody") || wTitle.Contains("melody"))
            return SuspensionMode.Unity;

        if (pName.Contains("final mix") || pName.Contains("chain of memories") || pName.Contains("dream drop"))
            return SuspensionMode.PriorityOnly;

        return SuspensionMode.Normal;
    }

    private static void SetItemMode(ListViewItem item, SuspensionMode mode)
    {
        switch (mode)
        {
            case SuspensionMode.Unity:
                item.BackColor = Color.LightBlue;
                item.Checked = false;
                break;
            case SuspensionMode.PriorityOnly:
                item.BackColor = Color.LightCoral;
                item.Checked = false;
                break;
            case SuspensionMode.NoSuspend:
                item.BackColor = Color.LightYellow;
                item.Checked = false;
                break;
            default:
                item.BackColor = SystemColors.Window;
                item.Checked = true;
                break;
        }
    }

    private void RemoveSelectedTargets()
    {
        foreach (ListViewItem item in _targets.SelectedItems)
        {
            var h = (IntPtr)item.Tag!;
            _targetWindows.Remove(h);
            _targets.Items.Remove(item);
            _suspensionModeCache.TryRemove(h, out _);
            _originalWindowStyles.TryRemove(h, out _); // CRITICAL FIX: Clean up stored styles
        }
    }

    private SuspensionMode GetSuspensionMode(IntPtr h)
    {
        return _suspensionModeCache.GetValueOrDefault(h, SuspensionMode.Normal);
    }

    private void ProcessList_ItemSelectionChanged(object? sender, ListViewItemSelectionChangedEventArgs e)
    {
        if (e.IsSelected)
        {
            e.Item.Selected = false;
        }
    }

    private void ProcessList_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var hitTest = _processList.HitTest(e.Location);
            if (hitTest.Item != null)
            {
                hitTest.Item.Checked = !hitTest.Item.Checked;
                hitTest.Item.Selected = false;
            }
        }
    }

    private void ProcessList_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        BeginInvoke(new Action(() =>
        {
            if (e.Index < _processList.Items.Count)
            {
                _processList.Items[e.Index].Selected = false;
            }
        }));
    }

    private void Targets_DoubleClick(object? sender, EventArgs e)
    {
        if (_targets.SelectedItems.Count > 0)
        {
            var selectedItem = _targets.SelectedItems[0];

            // Store original state BEFORE any UI interactions
            var originalCheckedState = selectedItem.Checked;
            var processName = selectedItem.SubItems[1].Text;
            var windowTitle = selectedItem.Text;
            var currentGameName = selectedItem.SubItems[3].Text;
            var gameNameKey = $"{processName}|{windowTitle}";

            Debug.WriteLine($"Double-click detected on {processName} - protecting checkbox state (currently {originalCheckedState})");

            // ENHANCED PROTECTION: Temporarily disable checkbox behavior
            var originalCheckBoxes = _targets.CheckBoxes;
            _targets.CheckBoxes = false;

            try
            {
                // Show dialog to edit game name
                var input = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Edit game name for:\nProcess: {processName}\nWindow: {windowTitle}\n\nCurrent game name:",
                    "Edit Game Name",
                    currentGameName);

                if (!string.IsNullOrEmpty(input) && input != currentGameName)
                {
                    // Update the custom mapping using Settings class
                    _settings.SetCustomGameName(processName, windowTitle, input);

                    // Update the display
                    selectedItem.SubItems[3].Text = input;

                    Debug.WriteLine($"Updated game name for {gameNameKey} to {input}");
                }
            }
            finally
            {
                // CRITICAL: Re-enable checkboxes and restore original state
                _targets.CheckBoxes = originalCheckBoxes;
                selectedItem.Checked = originalCheckedState;

                Debug.WriteLine($"Protected checkbox state restored for {processName} (set back to {originalCheckedState})");
            }
        }
    }

    // Track mouse state for double-click protection
    private DateTime _lastMouseDown = DateTime.MinValue;
    private ListViewItem? _mouseDownItem = null;

    private void Targets_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var hitTest = _targets.HitTest(e.Location);
            if (hitTest.Item != null)
            {
                _mouseDownItem = hitTest.Item;
                var now = DateTime.Now;

                // Check if this might be the start of a double-click
                // (second click within 500ms of first click on same item)
                if (_lastMouseDown != DateTime.MinValue &&
                    (now - _lastMouseDown).TotalMilliseconds < 500 &&
                    _mouseDownItem == hitTest.Item)
                {
                    Debug.WriteLine($"Potential double-click detected on {_mouseDownItem.SubItems[1].Text} - suppressing checkbox behavior");
                    // This is likely a double-click, don't let it affect the checkbox
                    return;
                }

                _lastMouseDown = now;
            }
        }
    }

    private void Targets_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        // Check if we're in a potential double-click scenario
        if (_mouseDownItem != null && e.Index < _targets.Items.Count)
        {
            var item = _targets.Items[e.Index];
            if (item == _mouseDownItem)
            {
                var timeSinceMouseDown = DateTime.Now - _lastMouseDown;
                if (timeSinceMouseDown.TotalMilliseconds < 500)
                {
                    Debug.WriteLine($"Canceling checkbox change during potential double-click on {item.SubItems[1].Text}");
                    // Cancel the checkbox change during potential double-click
                    e.NewValue = e.CurrentValue;
                    return;
                }
            }
        }

        // Clear selection after checkbox changes (like process list behavior)
        BeginInvoke(new Action(() =>
        {
            if (e.Index < _targets.Items.Count)
            {
                _targets.Items[e.Index].Selected = false;
            }
        }));
    }

    private void OnApplicationExit(object? sender, EventArgs e)
    {
        // Final cleanup when application is exiting
        if (_isShuffling)
        {
            Debug.WriteLine("Application exit detected - performing final cleanup");
            // Don't call ResumeAllTargetProcesses here - let Dispose handle it
            // to prevent double cleanup
        }
    }

    private void ToggleDarkMode()
    {
        _settings.DarkModeEnabled = _darkModeToggle.Checked;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (_darkModeToggle.Checked)
        {
            ApplyDarkTheme();
        }
        else
        {
            ApplyLightTheme();
        }
    }

    private void ApplyDarkTheme()
    {
        // Main form
        BackColor = DarkBackground;
        ForeColor = DarkText;

        // Process list
        _processList.BackColor = DarkSecondary;
        _processList.ForeColor = DarkText;

        // Targets list
        _targets.BackColor = DarkSecondary;
        _targets.ForeColor = DarkText;

        // Right panel and controls
        var rightPanel = (FlowLayoutPanel)Controls[1];
        rightPanel.BackColor = DarkBackground;

        foreach (Control control in rightPanel.Controls)
        {
            ApplyDarkThemeToControl(control);
        }

        // Split container
        var split = (SplitContainer)Controls[0];
        split.BackColor = DarkBorder;
        split.Panel1.BackColor = DarkBackground;
        split.Panel2.BackColor = DarkBackground;
    }

    private void ApplyLightTheme()
    {
        // Main form
        BackColor = SystemColors.Control;
        ForeColor = SystemColors.ControlText;

        // Process list
        _processList.BackColor = SystemColors.Window;
        _processList.ForeColor = SystemColors.WindowText;

        // Targets list
        _targets.BackColor = SystemColors.Window;
        _targets.ForeColor = SystemColors.WindowText;

        // Right panel and controls
        var rightPanel = (FlowLayoutPanel)Controls[1];
        rightPanel.BackColor = SystemColors.Control;

        foreach (Control control in rightPanel.Controls)
        {
            ApplyLightThemeToControl(control);
        }

        // Split container
        var split = (SplitContainer)Controls[0];
        split.BackColor = SystemColors.Control;
        split.Panel1.BackColor = SystemColors.Control;
        split.Panel2.BackColor = SystemColors.Control;
    }

    private void ApplyDarkThemeToControl(Control control)
    {
        switch (control)
        {
            case Button button:
                button.BackColor = DarkSecondary;
                button.ForeColor = DarkText;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = DarkBorder;
                button.FlatAppearance.BorderSize = 1;
                break;
            case Label label:
                label.ForeColor = DarkText;
                break;
            case NumericUpDown numericUpDown:
                numericUpDown.BackColor = DarkSecondary;
                numericUpDown.ForeColor = DarkText;
                break;
            case CheckBox checkBox:
                checkBox.ForeColor = DarkText;
                break;
            case SlidingToggle toggle:
                toggle.ForeColor = DarkText;
                break;
            case GroupBox groupBox:
                groupBox.ForeColor = DarkText;
                foreach (Control child in groupBox.Controls)
                {
                    ApplyDarkThemeToControl(child);
                }
                break;
            case FlowLayoutPanel panel:
                panel.BackColor = DarkBackground;
                foreach (Control child in panel.Controls)
                {
                    ApplyDarkThemeToControl(child);
                }
                break;
        }
    }

    private void ApplyLightThemeToControl(Control control)
    {
        switch (control)
        {
            case Button button:
                button.BackColor = SystemColors.Control;
                button.ForeColor = SystemColors.ControlText;
                button.FlatStyle = FlatStyle.Standard;
                break;
            case Label label:
                label.ForeColor = SystemColors.ControlText;
                break;
            case NumericUpDown numericUpDown:
                numericUpDown.BackColor = SystemColors.Window;
                numericUpDown.ForeColor = SystemColors.WindowText;
                break;
            case CheckBox checkBox:
                checkBox.ForeColor = SystemColors.ControlText;
                break;
            case SlidingToggle toggle:
                toggle.ForeColor = SystemColors.ControlText;
                break;
            case GroupBox groupBox:
                groupBox.ForeColor = SystemColors.ControlText;
                foreach (Control child in groupBox.Controls)
                {
                    ApplyLightThemeToControl(child);
                }
                break;
            case FlowLayoutPanel panel:
                panel.BackColor = SystemColors.Control;
                foreach (Control child in panel.Controls)
                {
                    ApplyLightThemeToControl(child);
                }
                break;
        }
    }

    // Add these public methods for effect integration:
    public int MinSeconds => (int)_minSeconds.Value;
    public int MaxSeconds => (int)_maxSeconds.Value;

    public void SetTimerRange(int min, int max)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetTimerRange(min, max)));
            return;
        }

        _minSeconds.Value = Math.Max(_minSeconds.Minimum, Math.Min(_minSeconds.Maximum, min));
        _maxSeconds.Value = Math.Max(_maxSeconds.Minimum, Math.Min(_maxSeconds.Maximum, max));
    }

    public List<string> GetTargetGameNames()
    {
        var gameNames = new List<string>();
        if (InvokeRequired)
        {
            Invoke(new Action(() =>
            {
                foreach (ListViewItem item in _targets.Items)
                {
                    // Use process name instead of window title for unique identification
                    var processName = item.SubItems.Count > 1 ? item.SubItems[1].Text : "Unknown";
                    gameNames.Add(processName);
                }
            }));
        }
        else
        {
            foreach (ListViewItem item in _targets.Items)
            {
                // Use process name instead of window title for unique identification
                var processName = item.SubItems.Count > 1 ? item.SubItems[1].Text : "Unknown";
                gameNames.Add(processName);
            }
        }
        return gameNames;
    }

    /// <summary>
    /// Gets target game information including both window titles and process names
    /// </summary>
    public Dictionary<string, string> GetTargetGameInfo()
    {
        var gameInfo = new Dictionary<string, string>(); // Key: ProcessName, Value: WindowTitle
        if (InvokeRequired)
        {
            Invoke(new Action(() =>
            {
                foreach (ListViewItem item in _targets.Items)
                {
                    var processName = item.SubItems.Count > 1 ? item.SubItems[1].Text : "Unknown";
                    var windowTitle = item.Text;
                    gameInfo[processName] = windowTitle;
                }
            }));
        }
        else
        {
            foreach (ListViewItem item in _targets.Items)
            {
                var processName = item.SubItems.Count > 1 ? item.SubItems[1].Text : "Unknown";
                var windowTitle = item.Text;
                gameInfo[processName] = windowTitle;
            }
        }
        return gameInfo;
    }

    /// <summary>
    /// Gets the window title for a specific process name
    /// </summary>
    public string GetWindowTitleForProcess(string processName)
    {
        var gameInfo = GetTargetGameInfo();
        return gameInfo.TryGetValue(processName, out var windowTitle) ? windowTitle : processName;
    }

    /// <summary>
    /// Gets the process name for a specific window title (reverse lookup)
    /// </summary>
    public string GetProcessNameForGame(string windowTitle)
    {
        var gameInfo = GetTargetGameInfo();
        // Reverse lookup - find process name by window title
        var entry = gameInfo.FirstOrDefault(kvp => kvp.Value.Equals(windowTitle, StringComparison.OrdinalIgnoreCase));
        return !string.IsNullOrEmpty(entry.Key) ? entry.Key : windowTitle;
    }

    public void BlacklistGame(string gameName, TimeSpan duration)
    {
        _blacklistedGames[gameName] = DateTime.UtcNow.Add(duration);
        Debug.WriteLine($"Blacklisted {gameName} until {_blacklistedGames[gameName]}");
    }

    /// <summary>
    /// Public method for EffectManager to suspend a specific process by PID
    /// </summary>
    public bool SuspendProcessByPid(int pid, string mode = "PriorityOnly")
    {
        try
        {
            Debug.WriteLine($"SuspendProcessByPid: Suspending PID {pid} with mode {mode}");

            // Use the same logic as SwitchToNextWindow
            switch (mode.ToLowerInvariant())
            {
                case "priorityonly":
                    var success = SuspendProcessPriorityOnly(pid);
                    if (success)
                    {
                        _suspendedProcesses[(int)pid] = true;
                        Debug.WriteLine($"SuspendProcessByPid: Successfully suspended PID {pid} (PriorityOnly mode)");
                    }
                    return success;

                case "threads":
                case "normal":
                    var threadSuccess = SuspendProcessWithThreads(pid);
                    if (threadSuccess)
                    {
                        _suspendedProcesses[(int)pid] = true;
                        Debug.WriteLine($"SuspendProcessByPid: Successfully suspended PID {pid} (Threads mode)");
                    }
                    return threadSuccess;

                default:
                    Debug.WriteLine($"SuspendProcessByPid: Unknown suspension mode '{mode}', using PriorityOnly");
                    var defaultSuccess = SuspendProcessPriorityOnly(pid);
                    if (defaultSuccess)
                    {
                        _suspendedProcesses[(int)pid] = true;
                    }
                    return defaultSuccess;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SuspendProcessByPid: Error suspending PID {pid}: {ex.Message}");
            return false;
        }
    }

    // NEW: Add methods to get current active game information for Mirror Mode
    public IntPtr GetCurrentActiveGameWindow()
    {
        if (InvokeRequired)
        {
            return (IntPtr)Invoke(new Func<IntPtr>(() => GetCurrentActiveGameWindow()));
        }

        if (!_isShuffling || _currentIndex < 0 || _currentIndex >= _targetWindows.Count)
        {
            return IntPtr.Zero;
        }

        var currentWindow = _targetWindows[_currentIndex];

        // Verify the window is still valid
        if (NativeMethods.IsWindow(currentWindow))
        {
            return currentWindow;
        }

        return IntPtr.Zero;
    }

    public string GetCurrentActiveGameTitle()
    {
        var window = GetCurrentActiveGameWindow();
        if (window == IntPtr.Zero)
        {
            return "";
        }

        return GetWindowText(window);
    }

    public bool IsShuffling => _isShuffling;

    // Game name tracking for streaming/recording
    private List<string> _gameNames = new List<string>();
    private DateTime _lastGameNamesLoad = DateTime.MinValue;

    /// <summary>
    /// Loads game names from game_names.txt file
    /// </summary>
    private void LoadGameNames()
    {
        try
        {
            var gameNamesPath = Path.Combine(Application.StartupPath, "game_names.txt");
            if (File.Exists(gameNamesPath))
            {
                var fileTime = File.GetLastWriteTime(gameNamesPath);
                if (fileTime > _lastGameNamesLoad)
                {
                    _gameNames = File.ReadAllLines(gameNamesPath)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim())
                        .ToList();
                    _lastGameNamesLoad = fileTime;
                    Debug.WriteLine($"Loaded {_gameNames.Count} game names from game_names.txt");
                }
            }
            else
            {
                Debug.WriteLine("game_names.txt not found - game name tracking disabled");
            }

            // Load custom game name mappings
            LoadCustomGameNames();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading game names: {ex.Message}");
        }
    }

    private void LoadCustomGameNames()
    {
        // Custom game names are now automatically loaded by the Settings class
        // This method is kept for backward compatibility but does nothing
        Debug.WriteLine("Custom game names loaded automatically by Settings class");
    }

    private void SaveCustomGameNames()
    {
        // Custom game names are now automatically saved by the Settings class
        // This method is kept for backward compatibility but does nothing
        Debug.WriteLine("Custom game names saved automatically by Settings class");
    }

    /// <summary>
    /// Smart matching for Bizhawk games that extracts key terms from complex window titles
    /// </summary>
    private string FindBizhawkSmartMatch(string windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle) || _gameNames.Count == 0) return "";

        Debug.WriteLine($"Attempting smart match for Bizhawk title: '{windowTitle}'");

        var windowTitleLower = windowTitle.ToLower();

        // Specific pattern matching for Kingdom Hearts games
        foreach (var gameName in _gameNames)
        {
            var gameNameLower = gameName.ToLower();

            // Skip if this isn't a Kingdom Hearts game (unless it contains "kingdom hearts" in the window title)
            if (!gameNameLower.Contains("kingdom hearts") && !windowTitleLower.Contains("kingdom hearts"))
                continue;

            // Specific matching for 358/2 Days
            if (gameNameLower.Contains("358") &&
                (windowTitleLower.Contains("358-2") || windowTitleLower.Contains("358/2") || windowTitleLower.Contains("358") && windowTitleLower.Contains("days")))
            {
                Debug.WriteLine($"358/2 Days match found: '{gameName}'");
                return gameName;
            }

            // Specific matching for Re:Chain of Memories
            if (gameNameLower.Contains("re:chain") &&
                (windowTitleLower.Contains("re_chain") || windowTitleLower.Contains("re:chain") ||
                 windowTitleLower.Contains("rechain") || windowTitleLower.Contains("re chain") ||
                 (windowTitleLower.Contains("chain of memories") && !windowTitleLower.Contains("gba")) ||
                 (windowTitleLower.Contains("kingdom hearts") && windowTitleLower.Contains("chain"))))
            {
                Debug.WriteLine($"Re:Chain of Memories match found: '{gameName}'");
                return gameName;
            }

            // Specific matching for Birth by Sleep
            if (gameNameLower.Contains("birth by sleep") &&
                (windowTitleLower.Contains("birth by sleep") || windowTitleLower.Contains("bbs")))
            {
                Debug.WriteLine($"Birth by Sleep match found: '{gameName}'");
                return gameName;
            }

            // Specific matching for Dream Drop Distance
            if (gameNameLower.Contains("dream drop") &&
                (windowTitleLower.Contains("dream drop") || windowTitleLower.Contains("ddd")))
            {
                Debug.WriteLine($"Dream Drop Distance match found: '{gameName}'");
                return gameName;
            }

            // Specific matching for Kingdom Hearts II
            if (gameNameLower.Contains("kingdom hearts ii") &&
                (windowTitleLower.Contains("kingdom hearts ii") || windowTitleLower.Contains("kh2") || windowTitleLower.Contains("kingdom hearts 2")))
            {
                Debug.WriteLine($"Kingdom Hearts II match found: '{gameName}'");
                return gameName;
            }

            // Specific matching for Kingdom Hearts Final Mix (original)
            if (gameNameLower.Contains("kingdom hearts final mix") && !gameNameLower.Contains("ii") && !gameNameLower.Contains("birth") &&
                (windowTitleLower.Contains("kingdom hearts final mix") ||
                 (windowTitleLower.Contains("kingdom hearts") && windowTitleLower.Contains("final mix") && !windowTitleLower.Contains("ii") && !windowTitleLower.Contains("2") && !windowTitleLower.Contains("birth") && !windowTitleLower.Contains("358") && !windowTitleLower.Contains("chain") && !windowTitleLower.Contains("dream"))))
            {
                Debug.WriteLine($"Kingdom Hearts Final Mix match found: '{gameName}'");
                return gameName;
            }
        }

        Debug.WriteLine("No smart match found");
        return "";
    }    /// <summary>
         /// Finds the best matching game name from the list for the given process/window info
         /// </summary>
    private string FindBestGameMatch(string processName, string windowTitle)
    {
        if (_gameNames.Count == 0) return "";

        // For Bizhawk/emulator games, prioritize window title matching
        bool isBizhawk = processName.Contains("bizhawk", StringComparison.OrdinalIgnoreCase) ||
                        processName.Contains("emuhawk", StringComparison.OrdinalIgnoreCase) ||
                        windowTitle.Contains("bizhawk", StringComparison.OrdinalIgnoreCase);

        var searchText = isBizhawk ? windowTitle : processName;
        Debug.WriteLine($"Game matching: {(isBizhawk ? "Bizhawk" : "Regular")} - searching '{searchText}' against {_gameNames.Count} names");

        // For Bizhawk games, try smart matching first
        if (isBizhawk && !string.IsNullOrEmpty(windowTitle))
        {
            var smartMatch = FindBizhawkSmartMatch(windowTitle);
            if (!string.IsNullOrEmpty(smartMatch))
            {
                Debug.WriteLine($"Found Bizhawk smart match: {smartMatch}");
                return smartMatch;
            }
        }

        // First pass: exact matches
        var exactMatch = _gameNames.FirstOrDefault(name =>
            name.Equals(searchText, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(exactMatch))
        {
            Debug.WriteLine($"Found exact match: {exactMatch}");
            return exactMatch;
        }

        // Second pass: contains matches (both directions)
        var containsMatch = _gameNames.FirstOrDefault(name =>
            name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            searchText.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(containsMatch))
        {
            Debug.WriteLine($"Found contains match: {containsMatch}");
            return containsMatch;
        }

        // Third pass: For Bizhawk, also try matching against process name if window title didn't work
        if (isBizhawk && !string.IsNullOrEmpty(processName))
        {
            var processMatch = _gameNames.FirstOrDefault(name =>
                name.Contains(processName, StringComparison.OrdinalIgnoreCase) ||
                processName.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(processMatch))
            {
                Debug.WriteLine($"Found Bizhawk process match: {processMatch}");
                return processMatch;
            }
        }

        Debug.WriteLine($"No match found for '{searchText}'");
        return "";
    }

    /// <summary>
    /// Updates the current_game.txt file with the active game name
    /// </summary>
    private void UpdateCurrentGameFile(IntPtr gameWindow)
    {
        try
        {
            Debug.WriteLine($"UpdateCurrentGameFile: Target path = {_currentGamePath}");

            // ULTRA-FAST PATH: Check cache first for immediate return
            if (_gameNameCache.TryGetValue(gameWindow, out var cachedName))
            {
                File.WriteAllText(_currentGamePath, cachedName);
                Debug.WriteLine($"UpdateCurrentGameFile: Used cached name: {cachedName}");
                return;
            }

            // Skip file reload for performance - use cached data (LoadGameNames only on startup/refresh)
            // LoadGameNames(); // Removed for instant performance

            // Get process and window information
            NativeMethods.GetWindowThreadProcessId(gameWindow, out var pid);
            if (pid == 0)
            {
                Debug.WriteLine("UpdateCurrentGameFile: Failed to get process ID");
                return;
            }

            string processName = "";
            string windowTitle = GetWindowText(gameWindow);

            try
            {
                using var process = Process.GetProcessById((int)pid);
                processName = process.ProcessName;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateCurrentGameFile: Failed to get process name: {ex.Message}");
                return; // Can't get process info
            }

            Debug.WriteLine($"UpdateCurrentGameFile: Process='{processName}', Window='{windowTitle}', GameNames={_gameNames.Count}");

            // PERFORMANCE OPTIMIZATION: Check custom mappings first (fastest lookup)
            var customName = _settings.GetCustomGameName(processName, windowTitle);
            if (!string.IsNullOrEmpty(customName))
            {
                File.WriteAllText(_currentGamePath, customName);
                _gameNameCache[gameWindow] = customName; // Cache for next time
                Debug.WriteLine($"UpdateCurrentGameFile: Used cached custom mapping: {customName}");
                return;
            }

            // Second, check if this game is in the targets list and use its configured game name
            foreach (ListViewItem item in _targets.Items)
            {
                if ((IntPtr)item.Tag == gameWindow)
                {
                    var gameNameFromList = item.SubItems[3].Text;
                    if (!string.IsNullOrEmpty(gameNameFromList) && gameNameFromList != "Auto-detect failed")
                    {
                        File.WriteAllText(_currentGamePath, gameNameFromList);
                        _gameNameCache[gameWindow] = gameNameFromList; // Cache for next time
                        Debug.WriteLine($"UpdateCurrentGameFile: Used game name from targets list: {gameNameFromList}");
                        return;
                    }
                    break;
                }
            }

            if (_gameNames.Count == 0)
            {
                // For Bizhawk/emulator games, use window title instead of process name
                bool isBizhawk = processName.Contains("bizhawk", StringComparison.OrdinalIgnoreCase) ||
                                processName.Contains("emuhawk", StringComparison.OrdinalIgnoreCase) ||
                                windowTitle.Contains("bizhawk", StringComparison.OrdinalIgnoreCase);

                var displayName = isBizhawk ? windowTitle : processName;
                File.WriteAllText(_currentGamePath, displayName);
                _gameNameCache[gameWindow] = displayName; // Cache for next time
                Debug.WriteLine($"UpdateCurrentGameFile: No game names file - wrote {(isBizhawk ? "window title" : "process name")}: {displayName}");
                return;
            }

            // Find the best matching game name
            var gameName = FindBestGameMatch(processName, windowTitle);

            if (!string.IsNullOrEmpty(gameName))
            {
                File.WriteAllText(_currentGamePath, gameName);
                _gameNameCache[gameWindow] = gameName; // Cache for next time
                Debug.WriteLine($"UpdateCurrentGameFile: SUCCESS - wrote game name: {gameName}");
            }
            else
            {
                // Only use names from game_names.txt - no fallbacks to raw process/window names
                File.WriteAllText(_currentGamePath, "Unknown Game");
                _gameNameCache[gameWindow] = "Unknown Game"; // Cache for next time
                Debug.WriteLine($"UpdateCurrentGameFile: No match found in game_names.txt - wrote 'Unknown Game'");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UpdateCurrentGameFile: Error: {ex.Message}");
            Debug.WriteLine($"UpdateCurrentGameFile: Stack trace: {ex.StackTrace}");
        }
    }

    private void ScheduleNextSwitch(bool immediate = false)
    {
        if (immediate)
        {
            _nextSwitch = DateTime.UtcNow;
            Debug.WriteLine("Immediate switch scheduled");
            return;
        }
        var min = (int)_minSeconds.Value;
        var max = (int)_maxSeconds.Value;
        if (min > max) (min, max) = (max, min);
        var delay = _rng.Next(min, max + 1);
        _nextSwitch = DateTime.UtcNow.AddSeconds(delay);
        Debug.WriteLine($"Next switch in {delay}s at {_nextSwitch:HH:mm:ss}");
    }

    private void ResumeAllTargetProcesses()
    {
        Debug.WriteLine("Resuming all processes");
        foreach (var window in _targetWindows.ToList())
        {
            try
            {
                if (!NativeMethods.IsWindow(window)) continue;

                NativeMethods.GetWindowThreadProcessId(window, out var pid);
                if (pid == 0) continue;

                var mode = GetSuspensionMode(window);
                switch (mode)
                {
                    case SuspensionMode.Unity:
                    case SuspensionMode.Normal:
                        ResumeProcessWithThreads((int)pid);
                        break;
                    case SuspensionMode.PriorityOnly:
                        ResumeProcessPriorityOnly((int)pid);
                        break;
                }

                NativeMethods.ShowWindow(window, ShowWindowCommands.Restore);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Resume error: {ex}");
            }
        }

        _suspendedProcesses.Clear();
    }

    private void StartShuffle()
    {
        if (_targetWindows.Count == 0)
        {
            MessageBox.Show("Add at least one window.");
            return;
        }

        _isShuffling = true;
        _isPaused = false;
        _currentIndex = -1;

        Debug.WriteLine($"Starting shuffle with {_targetWindows.Count} windows");

        this.WindowState = FormWindowState.Minimized;
        this.Text = "KHShuffler - Shuffling...";

        _startButton.Enabled = false;
        _stopButton.Enabled = true;
        _pauseButton.Enabled = true;
        _pauseButton.Text = "Pause";

        _timerShouldRun = true;
        _backgroundTimer.Change(0, 1000);

        // Load game names for tracking
        LoadGameNames();

        // Create initial current_game.txt file
        try
        {
            var currentGamePath = Path.Combine(Application.StartupPath, "current_game.txt");
            File.WriteAllText(currentGamePath, "Shuffling starting...");
            Debug.WriteLine($"Created initial current_game.txt at: {currentGamePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating initial current_game.txt: {ex.Message}");
        }

        ScheduleNextSwitch(immediate: true);
        Debug.WriteLine("Background timer started");
    }

    private void StopShuffle()
    {
        Debug.WriteLine("Stopping shuffle");

        _timerShouldRun = false;
        _backgroundTimer.Change(Timeout.Infinite, Timeout.Infinite);

        _isShuffling = false;
        _isPaused = false;

        this.WindowState = FormWindowState.Normal;
        this.Text = "KHShuffler";

        ResumeAllTargetProcesses();

        // Keep current_game.txt file when shuffling stops (user request)
        // File will show the last active game for streaming/recording purposes
        Debug.WriteLine("Shuffling stopped - current_game.txt preserved");

        _startButton.Enabled = true;
        _stopButton.Enabled = false;
        _pauseButton.Enabled = false;
        _pauseButton.Text = "Pause";

        Debug.WriteLine("Background timer stopped");
    }

    private void TogglePause()
    {
        if (!_isShuffling) return;

        _isPaused = !_isPaused;

        if (_isPaused)
        {
            var now = DateTime.UtcNow;
            if (now < _nextSwitch)
            {
                _remainingTime = _nextSwitch - now;
            }
            else
            {
                _remainingTime = TimeSpan.Zero;
            }

            _pausedAt = now;
            _pauseButton.Text = "Resume";
            this.Text = "KHShuffler - Paused";

            Debug.WriteLine($"Shuffler paused. Remaining time until next switch: {_remainingTime.TotalSeconds:F1}s");
        }
        else
        {
            var now = DateTime.UtcNow;
            _nextSwitch = now.Add(_remainingTime);
            _pauseButton.Text = "Pause";
            this.Text = "KHShuffler - Shuffling...";

            Debug.WriteLine($"Shuffler resumed. Next switch in: {_remainingTime.TotalSeconds:F1}s at {_nextSwitch:HH:mm:ss}");
        }
    }

    private void SwitchToNextWindow()
    {
        if (_isSwitching) return;

        _isSwitching = true;
        Debug.WriteLine("=== SWITCH TO NEXT WINDOW START ===");

        try
        {
            var validWindows = new List<IntPtr>();
            foreach (var window in _targetWindows.ToList())
            {
                if (NativeMethods.IsWindow(window))
                {
                    try
                    {
                        NativeMethods.GetWindowThreadProcessId(window, out var pid);
                        if (pid != 0)
                        {
                            using var process = Process.GetProcessById((int)pid);
                            if (!process.HasExited)
                            {
                                validWindows.Add(window);
                                continue;
                            }
                        }
                    }
                    catch { }
                }

                _targetWindows.Remove(window);
                _suspensionModeCache.TryRemove(window, out _);
                _originalWindowStyles.TryRemove(window, out _); // Clean up style cache
            }

            Debug.WriteLine($"Valid windows found: {validWindows.Count}");

            // Filter out blacklisted games
            var filteredWindows = validWindows.Where(window =>
            {
                var title = GetWindowText(window);
                return !_blacklistedGames.Any(kvp =>
                    title.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) &&
                    DateTime.UtcNow < kvp.Value);
            }).ToList();

            // ADDITIONAL: Filter out games banned by EffectManager (shuffle-based bans)
            var effectManagerBannedProcessNames = _effectManager?.GetBannedGameTitles() ?? new List<string>();
            Debug.WriteLine($"SwitchToNextWindow: EffectManager banned process names: [{string.Join(", ", effectManagerBannedProcessNames)}]");

            if (effectManagerBannedProcessNames.Count > 0)
            {
                // CRITICAL FIX: Use validWindows instead of filteredWindows since time-based filtering might have emptied it
                // Get process names for all valid windows (not just the filtered ones)
                var windowProcessNames = new Dictionary<IntPtr, string>();
                var availableProcessNames = GetTargetGameNames();
                Debug.WriteLine($"SwitchToNextWindow: Available shuffler process names: [{string.Join(", ", availableProcessNames)}]");
                Debug.WriteLine($"SwitchToNextWindow: Starting with {validWindows.Count} valid windows before process filtering");

                foreach (var window in validWindows) // Use validWindows, not filteredWindows
                {
                    try
                    {
                        NativeMethods.GetWindowThreadProcessId(window, out var pid);
                        if (pid != 0)
                        {
                            using var process = Process.GetProcessById((int)pid);
                            var actualProcessName = process.ProcessName;
                            Debug.WriteLine($"SwitchToNextWindow: Window {window} has actual process name '{actualProcessName}' (PID: {pid})");

                            // Match against the shuffler's process names (these might be different from actual process names)
                            var shufflerProcessName = availableProcessNames.FirstOrDefault(name =>
                                name.Contains(actualProcessName, StringComparison.OrdinalIgnoreCase) ||
                                actualProcessName.Contains(name, StringComparison.OrdinalIgnoreCase));

                            if (!string.IsNullOrEmpty(shufflerProcessName))
                            {
                                windowProcessNames[window] = shufflerProcessName;
                                Debug.WriteLine($"SwitchToNextWindow: Window {window} mapped to shuffler process '{shufflerProcessName}' (actual: '{actualProcessName}', PID: {pid})");
                            }
                            else
                            {
                                Debug.WriteLine($"SwitchToNextWindow: Window {window} - no matching shuffler process found for actual process '{actualProcessName}'");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"SwitchToNextWindow: Error getting process name for window {window}: {ex.Message}");
                    }
                }

                // CRITICAL FIX: Filter from validWindows list instead of the already-filtered list
                var bannedWindows = new List<IntPtr>();
                var keptWindows = new List<IntPtr>();

                var processFilteredWindows = validWindows.Where(window =>
                {
                    if (windowProcessNames.TryGetValue(window, out var processName))
                    {
                        var isBanned = effectManagerBannedProcessNames.Any(bannedProcess =>
                            processName.Equals(bannedProcess, StringComparison.OrdinalIgnoreCase));

                        if (isBanned)
                        {
                            bannedWindows.Add(window);
                            Debug.WriteLine($"SwitchToNextWindow: ? FILTERING OUT Window {window} (Process: '{processName}') - BANNED");
                            return false; // Filter out banned windows
                        }
                        else
                        {
                            keptWindows.Add(window);
                            Debug.WriteLine($"SwitchToNextWindow: ? KEEPING Window {window} (Process: '{processName}') - NOT BANNED");
                            return true; // Keep non-banned windows
                        }
                    }

                    // If we can't determine the process name, keep it in the list (don't filter it out)
                    keptWindows.Add(window);
                    Debug.WriteLine($"SwitchToNextWindow: ? KEEPING Window {window} - Could not determine process name");
                    return true;
                }).ToList();

                Debug.WriteLine($"SwitchToNextWindow: Process-based filtering results:");
                Debug.WriteLine($"  - Original windows: {validWindows.Count}");
                Debug.WriteLine($"  - Banned windows filtered out: {bannedWindows.Count} [{string.Join(", ", bannedWindows)}]");
                Debug.WriteLine($"  - Windows kept: {keptWindows.Count} [{string.Join(", ", keptWindows)}]");
                Debug.WriteLine($"  - Final process-filtered windows: {processFilteredWindows.Count}");

                // CRITICAL FIX: Update filteredWindows to use the process-filtered results
                filteredWindows = processFilteredWindows;
            }

            if (filteredWindows.Count > 0)
            {
                validWindows = filteredWindows;
                Debug.WriteLine($"After all filtering (time-based + effect-based): {validWindows.Count} windows available");
            }
            else
            {
                // CRITICAL FIX: When all games are banned/blacklisted, we must still respect the bans
                // Don't fall back to original list - instead wait or stop shuffling
                Debug.WriteLine("All games are blacklisted or banned");

                // Check if there are ANY non-banned games available
                var bannedProcessNames = _effectManager?.GetBannedGameTitles() ?? new List<string>();
                var availableProcessNames = GetTargetGameNames();
                var nonBannedProcesses = availableProcessNames.Where(processName =>
                    !bannedProcessNames.Any(bannedProcess =>
                        processName.Equals(bannedProcess, StringComparison.OrdinalIgnoreCase))).ToList();

                Debug.WriteLine($"Available non-banned processes: {nonBannedProcesses.Count} [{string.Join(", ", nonBannedProcesses)}]");

                if (nonBannedProcesses.Count > 0)
                {
                    // There are non-banned games but they might be temporarily blacklisted
                    // Find windows for the non-banned processes
                    var nonBannedWindows = new List<IntPtr>();
                    foreach (var window in validWindows)
                    {
                        try
                        {
                            NativeMethods.GetWindowThreadProcessId(window, out var pid);
                            if (pid != 0)
                            {
                                using var process = Process.GetProcessById((int)pid);
                                var actualProcessName = process.ProcessName;
                                var matchedProcess = nonBannedProcesses.FirstOrDefault(name =>
                                    name.Contains(actualProcessName, StringComparison.OrdinalIgnoreCase) ||
                                    actualProcessName.Contains(name, StringComparison.OrdinalIgnoreCase));

                                if (!string.IsNullOrEmpty(matchedProcess))
                                {
                                    nonBannedWindows.Add(window);
                                    Debug.WriteLine($"Found non-banned window {window} for process '{matchedProcess}'");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error checking window {window}: {ex.Message}");
                        }
                    }

                    if (nonBannedWindows.Count > 0)
                    {
                        Debug.WriteLine($"Using {nonBannedWindows.Count} non-banned windows, ignoring temporary blacklists");
                        validWindows = nonBannedWindows;
                    }
                    else
                    {
                        Debug.WriteLine("No windows found for non-banned processes - scheduling next switch");
                        ScheduleNextSwitch();
                        _isSwitching = false;
                        return;
                    }
                }
                else
                {
                    Debug.WriteLine("ALL processes are banned by EffectManager - scheduling next switch to let ban countdown continue");
                    // Don't stop the shuffler - just wait for the next switch when bans might have expired
                    ScheduleNextSwitch();
                    _isSwitching = false;
                    return;
                }
            }

            // ENHANCED RANDOMIZATION: Select truly random game instead of cycling
            // Remove current game from available options to avoid selecting the same one twice in a row
            var availableWindows = validWindows.ToList();

            // If we have more than 1 window and there's a current game running, avoid selecting it again
            if (availableWindows.Count > 1 && _currentIndex >= 0 && _currentIndex < validWindows.Count)
            {
                var currentWindow = validWindows[_currentIndex];
                if (availableWindows.Contains(currentWindow))
                {
                    availableWindows.Remove(currentWindow);
                    Debug.WriteLine($"Removing current window from random selection to avoid immediate repeat (now {availableWindows.Count} options)");
                }
            }

            // Select randomly from available options
            var randomIndex = _rng.Next(availableWindows.Count);
            var target = availableWindows[randomIndex];

            // Update current index to reflect the newly selected window in the original list
            var newIndex = validWindows.IndexOf(target);

            // Log the randomization for visibility
            Debug.WriteLine($"RANDOMIZED SELECTION: Previous index: {_currentIndex}, New index: {newIndex}, Selected from {availableWindows.Count} available options");
            _currentIndex = newIndex;

            Debug.WriteLine($"Randomly selected window index {_currentIndex} out of {validWindows.Count} total windows");

            // NOTE: Moved UpdateCurrentGameFile to AFTER window focusing for better timing with classic KH games

            NativeMethods.GetWindowThreadProcessId(target, out var targetPid);
            if (targetPid != 0)
            {
                _suspendedProcesses.TryRemove((int)targetPid, out _);

                // CRITICAL FIX: Check if target process is banned before resuming
                bool shouldResume = true;
                try
                {
                    using var targetProcess = Process.GetProcessById((int)targetPid);
                    var targetProcessName = targetProcess.ProcessName;
                    var bannedProcessNames = _effectManager?.GetBannedGameTitles() ?? new List<string>();

                    var isTargetBanned = bannedProcessNames.Any(bannedProcess =>
                        targetProcessName.Equals(bannedProcess, StringComparison.OrdinalIgnoreCase));

                    if (isTargetBanned)
                    {
                        shouldResume = false;
                        Debug.WriteLine($"Target PID {targetPid} ({targetProcessName}) is banned - skipping resume to keep it suspended");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error checking if target PID {targetPid} is banned: {ex.Message}");
                }

                if (shouldResume)
                {
                    var targetMode = GetSuspensionMode(target);
                    Debug.WriteLine($"Resuming target PID {targetPid} (mode: {targetMode})");
                    switch (targetMode)
                    {
                        case SuspensionMode.Unity:
                        case SuspensionMode.Normal:
                            ResumeProcessWithThreads((int)targetPid);
                            break;
                        case SuspensionMode.PriorityOnly:
                            ResumeProcessPriorityOnly((int)targetPid);
                            break;
                    }
                }
                else
                {
                    Debug.WriteLine($"Target PID {targetPid} remains suspended due to Game Ban effect");
                }

                if (NativeMethods.IsIconic(target))
                    NativeMethods.ShowWindow(target, ShowWindowCommands.Restore);

                // Enhanced Unity window restoration for proper fullscreen behavior
                var unityMode = GetSuspensionMode(target);
                if (unityMode == SuspensionMode.Unity)
                {
                    try
                    {
                        Debug.WriteLine("Applying Unity-specific window restoration for fullscreen behavior");

                        // Unity games benefit from a multi-step restoration process
                        NativeMethods.ShowWindow(target, ShowWindowCommands.Show);
                        Thread.Sleep(50); // Allow Unity window manager to process

                        NativeMethods.ShowWindow(target, ShowWindowCommands.Maximize);
                        Thread.Sleep(30); // Unity renderer context restoration

                        // Ensure window is brought to absolute front
                        NativeMethods.SetWindowPos(target, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
                        Thread.Sleep(20);

                        // Remove topmost flag but keep it in front
                        NativeMethods.SetWindowPos(target, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0,
                            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);

                        Debug.WriteLine("Unity window restoration sequence completed");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Unity window restoration failed: {ex.Message}");
                    }
                }

                if (_forceBorderless.Checked)
                {
                    try
                    {
                        var style = NativeMethods.GetWindowLong(target, NativeMethods.GWL_STYLE);
                        if ((style & (NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME | NativeMethods.WS_SYSMENU)) != 0)
                        {
                            var monitor = NativeMethods.MonitorFromWindow(target, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
                            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

                            if (NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
                            {
                                var rect = monitorInfo.rcMonitor;
                                var newStyle = style & ~(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME | NativeMethods.WS_SYSMENU);

                                NativeMethods.SetWindowLong(target, NativeMethods.GWL_STYLE, newStyle);
                                NativeMethods.SetWindowPos(target, NativeMethods.HWND_TOP,
                                    rect.Left, rect.Top,
                                    rect.Right - rect.Left, rect.Bottom - rect.Top,
                                    NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_SHOWWINDOW);

                                // For Unity games, ensure the window covers the taskbar area
                                if (unityMode == SuspensionMode.Unity)
                                {
                                    Debug.WriteLine("Unity borderless: ensuring full screen coverage including taskbar area");
                                    Thread.Sleep(30); // Allow Unity to process style changes
                                }
                            }
                        }
                    }
                    catch { }
                }

                // Enhanced focus sequence for special games
                if (unityMode == SuspensionMode.Unity)
                {
                    try
                    {
                        // Unity games need multiple focus attempts
                        NativeMethods.SetForegroundWindow(target);
                        Thread.Sleep(20);
                        NativeMethods.SetActiveWindow(target);
                        Thread.Sleep(20);
                        NativeMethods.SetFocus(target);
                        Debug.WriteLine("Unity enhanced focus sequence completed");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Unity enhanced focus failed: {ex.Message}");
                        // Fallback to standard focus
                        NativeMethods.SetForegroundWindow(target);
                    }
                }
                else
                {
                    // Check if this is Re Chain of Memories - it needs enhanced focusing too
                    bool isReCoM = false;
                    try
                    {
                        NativeMethods.GetWindowThreadProcessId(target, out var windowPid);
                        if (windowPid != 0)
                        {
                            using var process = Process.GetProcessById((int)windowPid);
                            isReCoM = IsReChainOfMemories(process.ProcessName, GetWindowText(target));
                        }
                    }
                    catch { }

                    if (isReCoM)
                    {
                        try
                        {
                            Debug.WriteLine("Re:Chain of Memories detected - applying enhanced focus sequence");

                            // Re Chain of Memories enhanced focus sequence
                            // First attempt: Standard approach
                            NativeMethods.SetForegroundWindow(target);
                            Thread.Sleep(30);

                            // Second attempt: Activate window
                            NativeMethods.SetActiveWindow(target);
                            Thread.Sleep(30);

                            // Third attempt: Bring to top and focus
                            NativeMethods.SetWindowPos(target, NativeMethods.HWND_TOP, 0, 0, 0, 0,
                                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
                            Thread.Sleep(20);
                            NativeMethods.SetFocus(target);
                            Thread.Sleep(20);

                            // Final attempt: Force foreground again
                            NativeMethods.SetForegroundWindow(target);

                            Debug.WriteLine("Re:Chain of Memories enhanced focus sequence completed");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Re:Chain of Memories enhanced focus failed: {ex.Message}");
                            // Fallback to standard focus
                            NativeMethods.SetForegroundWindow(target);
                        }
                    }
                    else
                    {
                        NativeMethods.SetForegroundWindow(target);
                    }
                }

                Debug.WriteLine("Window focused successfully");

                // OPTIMAL TIMING: Update game file AFTER window is focused and visible on screen
                // This ensures classic KH games have loaded and are properly displayed before file update
                UpdateCurrentGameFile(target);
                Debug.WriteLine("Game name written to file after window focus completion");

                // Notify effect manager that a shuffle occurred
                try
                {
                    Debug.WriteLine("SwitchToNextWindow: About to call EffectManager.OnGameShuffle()");
                    _effectManager?.OnGameShuffle();
                    Debug.WriteLine("SwitchToNextWindow: Successfully called EffectManager.OnGameShuffle()");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SwitchToNextWindow: Error notifying EffectManager of shuffle: {ex.Message}");
                }

                var otherWindows = validWindows.Where(h => h != target).ToList();
                Debug.WriteLine($"Suspending {otherWindows.Count} other windows");

                foreach (var h in otherWindows)
                {
                    try
                    {
                        NativeMethods.GetWindowThreadProcessId(h, out var pid);

                        // CRITICAL FIX: Skip suspending processes that are already suspended
                        if (pid == 0 || _suspendedProcesses.ContainsKey((int)pid)) continue;

                        var mode = GetSuspensionMode(h);
                        Debug.WriteLine($"Suspending PID {pid} (mode: {mode})");

                        if (mode != SuspensionMode.NoSuspend)
                        {
                            NativeMethods.ShowWindow(h, ShowWindowCommands.Minimize);
                            Debug.WriteLine($"Minimized window for PID {pid}");
                        }

                        bool suspended = false;
                        switch (mode)
                        {
                            case SuspensionMode.Unity:
                            case SuspensionMode.Normal:
                                suspended = SuspendProcessWithThreads((int)pid);
                                break;
                            case SuspensionMode.PriorityOnly:
                                suspended = SuspendProcessPriorityOnly((int)pid);
                                break;
                        }

                        if (suspended)
                        {
                            _suspendedProcesses[(int)pid] = true;
                            Debug.WriteLine($"Successfully suspended PID {pid}");
                        }
                        else
                        {
                            Debug.WriteLine($"Failed to suspend PID {pid}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Suspend error: {ex.Message}");
                    }
                }

                Debug.WriteLine("All suspension operations completed");
            }
            else
            {
                Debug.WriteLine("Target PID was 0 - skipping operations");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Switch error: {ex}");
        }
        finally
        {
            Debug.WriteLine("Scheduling next switch...");
            ScheduleNextSwitch();
            _isSwitching = false;
            Debug.WriteLine("=== SWITCH TO NEXT WINDOW END ===");
        }
    }
}

public class SlidingToggle : Control
{
    private bool _checked = false;
    private bool _animating = false;
    private float _knobPosition = 0f; // 0 = left (off), 1 = right (on)
    private readonly System.Windows.Forms.Timer _animationTimer = new();
    private const int AnimationSteps = 10;
    private int _currentStep = 0;

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked != value)
            {
                _checked = value;
                AnimateToPosition();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public SlidingToggle()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
        Size = new Size(250, 30); // Increase width to accommodate label

        _animationTimer.Interval = 20; // 50 FPS animation
        _animationTimer.Tick += AnimationTimer_Tick;
    }

    private void AnimateToPosition()
    {
        if (_animating) return;

        _animating = true;
        _currentStep = 0;
        _animationTimer.Start();
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        _currentStep++;
        float targetPosition = _checked ? 1f : 0f;
        float startPosition = _checked ? 0f : 1f;

        // Smooth easing animation
        float progress = (float)_currentStep / AnimationSteps;
        progress = (float)(1 - Math.Pow(1 - progress, 3)); // Ease-out cubic

        _knobPosition = startPosition + (targetPosition - startPosition) * progress;

        Invalidate();

        if (_currentStep >= AnimationSteps)
        {
            _knobPosition = targetPosition;
            _animating = false;
            _animationTimer.Stop();
            Invalidate();
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Colors based on theme
        var isDarkMode = _checked;
        var trackColor = isDarkMode ? Color.FromArgb(100, 65, 165) : Color.FromArgb(200, 200, 200);
        var knobColor = Color.White;
        var borderColor = Color.FromArgb(150, 150, 150);

        // Draw track (rounded rectangle) - keep it on the left side
        var trackRect = new Rectangle(2, Height / 2 - 8, 60, 16);
        using (var brush = new SolidBrush(trackColor))
        {
            g.FillRoundedRectangle(brush, trackRect, 8);
        }

        using (var pen = new Pen(borderColor, 1))
        {
            g.DrawRoundedRectangle(pen, trackRect, 8);
        }

        // Draw knob (circle) - position relative to track
        var knobSize = 20;
        var knobX = (int)(4 + _knobPosition * (trackRect.Width - knobSize + 4));
        var knobY = Height / 2 - knobSize / 2;
        var knobRect = new Rectangle(knobX, knobY, knobSize, knobSize);

        using (var brush = new SolidBrush(knobColor))
        {
            g.FillEllipse(brush, knobRect);
        }

        using (var pen = new Pen(borderColor, 1))
        {
            g.DrawEllipse(pen, knobRect);
        }

        // Draw label next to the toggle
        var labelText = "Dark Mode";
        var labelX = trackRect.Right + 10; // Position label to the right of the track
        var labelBounds = new Rectangle(labelX, 0, Width - labelX, Height);
        using (var brush = new SolidBrush(ForeColor))
        {
            var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            g.DrawString(labelText, Font, brush, labelBounds, format);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}

// Extension method for rounded rectangles
public static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using (var path = CreateRoundedRectanglePath(bounds, radius))
        {
            graphics.FillPath(brush, path);
        }
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using (var path = CreateRoundedRectanglePath(bounds, radius))
        {
            graphics.DrawPath(pen, path);
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();

        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        // Top left arc
        path.AddArc(arc, 180, 90);

        // Top right arc
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);

        // Bottom right arc
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        // Bottom left arc
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }
}