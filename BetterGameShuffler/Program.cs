using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BetterGameShuffler;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
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
    private readonly ListBox _processList = new() { SelectionMode = SelectionMode.MultiExtended, Dock = DockStyle.Fill };
    private readonly Button _refreshButton = new() { Text = "Refresh" };
    private readonly Button _addButton = new() { Text = "Add Selected" };
    private readonly Button _removeButton = new() { Text = "Remove Selected" };
    private readonly Button _startButton = new() { Text = "Start" };
    private readonly Button _stopButton = new() { Text = "Stop", Enabled = false };
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
    private int _currentIndex = -1;
    private DateTime _nextSwitch = DateTime.MinValue;
    private bool _isSwitching = false;

    private readonly CheckBox _forceBorderless = new() { Text = "Force Borderless", Checked = true };

    public MainForm()
    {
        Text = "KH Shuffler";
        Width = 1200;
        Height = 600;

        _targets.Columns.Add("Title", 500);
        _targets.Columns.Add("Process", 200);
        _targets.Columns.Add("PID", 100);

        var rightPanel = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.TopDown, Width = 300, Padding = new Padding(8) };
        rightPanel.Controls.Add(new Label { Text = "Min seconds" });
        rightPanel.Controls.Add(_minSeconds);
        rightPanel.Controls.Add(new Label { Text = "Max seconds" });
        rightPanel.Controls.Add(_maxSeconds);
        rightPanel.Controls.Add(_refreshButton);
        rightPanel.Controls.Add(_addButton);
        rightPanel.Controls.Add(_removeButton);
        rightPanel.Controls.Add(_startButton);
        rightPanel.Controls.Add(_stopButton);
        rightPanel.Controls.Add(_forceBorderless);
        
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

        _backgroundTimer = new System.Threading.Timer(BackgroundTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

        RefreshProcesses();
    }

    protected override bool ShowWithoutActivation => _isShuffling;

    private void BackgroundTimerCallback(object? state)
    {
        try
        {
            if (!_timerShouldRun || _isSwitching) return;
            
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
            _processList.Items.Add(new WindowItem(h, title, procName, (int)pid));
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
        foreach (var sel in _processList.SelectedItems.Cast<WindowItem>())
        {
            if (sel.Pid == currentPid) continue;
            if (_targetWindows.Contains(sel.Handle)) continue;
            
            _targetWindows.Add(sel.Handle);
            
            var lvi = new ListViewItem(sel.Title) { Checked = true };
            lvi.SubItems.Add(sel.ProcessName);
            lvi.SubItems.Add(sel.Pid.ToString());
            lvi.Tag = sel.Handle;
            
            var mode = GetGameMode(sel.ProcessName, sel.Title);
            SetItemMode(lvi, mode);
            
            _targets.Items.Add(lvi);
            _suspensionModeCache[sel.Handle] = mode;
            
            Debug.WriteLine($"Added: {sel.Title} (Mode: {mode})");
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
        }
    }

    private SuspensionMode GetSuspensionMode(IntPtr h)
    {
        return _suspensionModeCache.GetValueOrDefault(h, SuspensionMode.Normal);
    }

    private void StartShuffle()
    {
        if (_targetWindows.Count == 0)
        {
            MessageBox.Show("Add at least one window.");
            return;
        }
        
        _isShuffling = true;
        _currentIndex = -1;
        
        Debug.WriteLine($"Starting shuffle with {_targetWindows.Count} windows");
        
        this.WindowState = FormWindowState.Minimized;
        this.Text = "KH Shuffler - Shuffling...";
        
        _startButton.Enabled = false;
        _stopButton.Enabled = true;
        
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
        
        this.WindowState = FormWindowState.Normal;
        this.Text = "KH Shuffler";
        
        ResumeAllTargetProcesses();
        
        _startButton.Enabled = true;
        _stopButton.Enabled = false;
        
        Debug.WriteLine("Background timer stopped");
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
                
                Debug.WriteLine("Skipping borderless conversion for debugging");
                // Temporarily disable borderless to test if this is causing the hang
                /*
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
                */
                
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
                        
                        // Minimize BEFORE suspending to avoid hanging on suspended processes
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
            _backgroundTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}