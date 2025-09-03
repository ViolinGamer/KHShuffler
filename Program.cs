using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BetterGameShuffler;

internal static class Program
{
    public const string Version = "v1.0.0";

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

// Settings manager for persistent user preferences
public static class Settings
{
    private const string RegistryKeyPath = @"HKEY_CURRENT_USER\Software\BetterGameShuffler";

    public static bool DarkMode
    {
        get
        {
            try
            {
                var value = Registry.GetValue(RegistryKeyPath, "DarkMode", 0);
                // Handle both boolean and integer values for compatibility
                return value switch
                {
                    bool b => b,
                    int i => i != 0,
                    _ => false
                };
            }
            catch
            {
                return false; // Default to light mode if registry read fails
            }
        }
        set
        {
            try
            {
                // Store as integer for better registry compatibility
                Registry.SetValue(RegistryKeyPath, "DarkMode", value ? 1 : 0, RegistryValueKind.DWord);
            }
            catch
            {
                // Silently fail if registry write fails
            }
        }
    }
}

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

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
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

    private readonly CheckBox _forceBorderless = new() { Text = "Force borderless fullscreen", Checked = true, AutoSize = true };
    private readonly SlidingToggle _darkModeToggle = new();

    // Dark mode color schemes
    private static readonly Color DarkBackground = Color.FromArgb(32, 32, 32);
    private static readonly Color DarkSecondary = Color.FromArgb(45, 45, 48);
    private static readonly Color DarkText = Color.FromArgb(220, 220, 220);
    private static readonly Color DarkBorder = Color.FromArgb(63, 63, 70);

    // CRITICAL FIX: Store original window styles to prevent resizing issues
    private readonly ConcurrentDictionary<IntPtr, int> _originalWindowStyles = new();

    public MainForm()
    {
        Text = $"KHShuffler {Program.Version}";
        Width = 1200;
        Height = 600;

        // Set up process list columns
        _processList.Columns.Add("Title", 400);
        _processList.Columns.Add("Process", 150);
        _processList.Columns.Add("PID", 80);

        // Set up targets list columns
        _targets.Columns.Add("Title", 500);
        _targets.Columns.Add("Process", 200);
        _targets.Columns.Add("PID", 100);

        // Configure process list behavior
        _processList.ItemCheck += ProcessList_ItemCheck;
        _processList.MouseClick += ProcessList_MouseClick;
        _processList.HideSelection = true; // Hide blue selection highlighting
        _processList.MultiSelect = false; // Disable multi-selection
        _processList.ItemSelectionChanged += ProcessList_ItemSelectionChanged;

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

        _backgroundTimer = new System.Threading.Timer(BackgroundTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

        // Set up application exit handler for cleanup
        Application.ApplicationExit += OnApplicationExit;

        // Load and apply saved dark mode preference
        _darkModeToggle.Checked = Settings.DarkMode;
        ApplyTheme();

        RefreshProcesses();
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

        return (pName.Contains("kh3") || pName.Contains("khiii") ||
                wTitle.Contains("kingdom hearts iii") || wTitle.Contains("kingdom hearts 3") ||
                wTitle.Contains("kh3") || wTitle.Contains("khiii")) ||
               (pName.Contains("0.2") || pName.Contains("02") ||
                pName.Contains("fragmentary") ||
                wTitle.Contains("0.2") || wTitle.Contains("fragmentary passage") ||
                wTitle.Contains("birth by sleep") && wTitle.Contains("0.2") ||
                wTitle.Contains("kingdom hearts 0.2") || wTitle.Contains("kh 0.2") || wTitle.Contains("kh0.2"));
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
            Debug.WriteLine($"Resuming UE4 Kingdom Hearts process {process.Id}");

            process.PriorityClass = ProcessPriorityClass.Normal;

            int totalCores = Environment.ProcessorCount;
            IntPtr fullAffinityMask = (IntPtr)((1L << totalCores) - 1);
            process.ProcessorAffinity = fullAffinityMask;

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

            Debug.WriteLine($"UE4 resume completed: {resumed} threads resumed, priority + CPU affinity restored");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UE4 resume failed: {process.Id} - {ex.Message}");
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

    private static bool SuspendProcessWithThreads(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited) return false;

            bool isUE4KH = IsUE4KingdomHearts(process.ProcessName, process.MainWindowTitle);

            if (isUE4KH)
            {
                return SuspendUE4ProcessSelectively(process);
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

    private static bool ResumeProcessWithThreads(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited) return false;

            bool isUE4KH = IsUE4KingdomHearts(process.ProcessName, process.MainWindowTitle);

            if (isUE4KH)
            {
                return ResumeUE4ProcessSelectively(process);
            }
            else
            {
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
        Settings.DarkMode = _darkModeToggle.Checked;
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

            if (validWindows.Count == 0)
            {
                Debug.WriteLine("No valid windows - stopping shuffle");
                StopShuffle();
                return;
            }

            if (validWindows.Count == 1)
            {
                Debug.WriteLine("Only one valid window - stopping shuffle");
                var lastWindow = validWindows[0];
                NativeMethods.GetWindowThreadProcessId(lastWindow, out var pid);
                if (pid != 0)
                {
                    var mode = GetSuspensionMode(lastWindow);
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
                    
                    // CRITICAL FIX: Restore window style before showing
                    if (_originalWindowStyles.TryGetValue(lastWindow, out var originalStyle))
                    {
                        try
                        {
                            var currentStyle = NativeMethods.GetWindowLong(lastWindow, NativeMethods.GWL_STYLE);
                            if (currentStyle != originalStyle)
                            {
                                NativeMethods.SetWindowLong(lastWindow, NativeMethods.GWL_STYLE, originalStyle);
                                NativeMethods.SetWindowPos(lastWindow, IntPtr.Zero, 0, 0, 0, 0, 
                                    NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_NOMOVE | 
                                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | 
                                    NativeMethods.SWP_NOACTIVATE);
                            }
                        }
                        catch { }
                    }
                    
                    NativeMethods.ShowWindow(lastWindow, ShowWindowCommands.Restore);
                    NativeMethods.SetForegroundWindow(lastWindow);
                }
                StopShuffle();
                return;
            }

            _targetWindows.Clear();
            _targetWindows.AddRange(validWindows);

            if (_currentIndex >= validWindows.Count)
                _currentIndex = -1;

            _currentIndex = (_currentIndex + 1) % validWindows.Count;
            var target = validWindows[_currentIndex];

            Debug.WriteLine($"Switching to window index {_currentIndex}");

            NativeMethods.GetWindowThreadProcessId(target, out var targetPid);
            if (targetPid != 0)
            {
                _suspendedProcesses.TryRemove((int)targetPid, out _);

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

                if (NativeMethods.IsIconic(target))
                    NativeMethods.ShowWindow(target, ShowWindowCommands.Restore);

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
                            }
                        }
                    }
                    catch { }
                }

                NativeMethods.SetForegroundWindow(target);
                Debug.WriteLine("Window focused successfully");

                var otherWindows = validWindows.Where(h => h != target).ToList();
                Debug.WriteLine($"Suspending {otherWindows.Count} other windows");

                foreach (var h in otherWindows)
                {
                    try
                    {
                        NativeMethods.GetWindowThreadProcessId(h, out var pid);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_isShuffling)
            {
                Debug.WriteLine("Application disposing - performing cleanup");
                ResumeAllTargetProcesses();
            }

            _backgroundTimer?.Dispose();
        }
        base.Dispose(disposing);
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
