using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfAnimatedGif;
using WpfImage = System.Windows.Controls.Image;
using WfColor = System.Windows.Media.Color;
using WfBrushes = System.Windows.Media.Brushes;
using WfPoint = System.Windows.Point;
using DrawingColor = System.Drawing.Color;
using BetterGameShuffler; // Added namespace import for Settings
using System.Drawing; // Added for screen capture and bitmap manipulation
using System.Windows.Forms; // Added for screen capture
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D11;

namespace BetterGameShuffler.TwitchIntegration;

public class OverlayElement
{
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public object? UIElement { get; set; }
    public OverlayElementType Type { get; set; }
}

public enum OverlayElementType
{
    ActiveEffect,
    Notification,
    Image,
    Sound
}

public class WpfEffectOverlay : IDisposable
{
    private Window? _overlayWindow;
    private Canvas? _overlayCanvas;
    private readonly List<OverlayElement> _activeElements = new();
    private readonly MainForm? _mainForm; // Add reference to main form

    // Desktop Duplication API components
    private SharpDX.Direct3D11.Device? _d3dDevice;
    private OutputDuplication? _outputDuplication;
    private Texture2D? _screenTexture;
    private bool _isCapturing = false;

    // Mirror mode tracking
    private IntPtr _activeGameWindow = IntPtr.Zero;
    private bool _isGameMirrorActive = false;
    private bool _isSystemMirrorActive = false;
    private XFORM _originalTransform;

    // Mirror mode extension support
    private System.Threading.CancellationTokenSource? _mirrorCancellationTokenSource = null;
    private DateTime? _mirrorEndTime = null;

    // ULTRA PERFORMANCE: Pre-allocated resources for window capture
    private IntPtr _captureMemoryDC = IntPtr.Zero;
    private IntPtr _captureBitmap = IntPtr.Zero;
    private IntPtr _screenDC = IntPtr.Zero;
    private int _lastCaptureWidth = 0;
    private int _lastCaptureHeight = 0;

    // ULTRA PERFORMANCE: Frame pooling with larger pool for smoother FPS
    private readonly Queue<BitmapSource> _framePool = new();
    private readonly object _framePoolLock = new();
    private const int MAX_POOLED_FRAMES = 6; // Increased pool size

    // ULTRA PERFORMANCE: High-precision timing
    private readonly System.Diagnostics.Stopwatch _performanceStopwatch = new();
    private double _lastFrameTime = 0;
    private const double TARGET_FRAME_TIME_MS = 16.66667; // 60 FPS = 16.67ms
    private const double AGGRESSIVE_FRAME_TIME_MS = 14.0; // Target slightly faster for consistency

    // ULTRA PERFORMANCE: Thread priority and CPU affinity
    private System.Threading.Thread? _captureThread;
    private readonly object _captureLock = new();

    // Windows API for desktop mirroring
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int SetGraphicsMode(IntPtr hdc, int iMode);

    [DllImport("gdi32.dll")]
    private static extern bool SetWorldTransform(IntPtr hdc, ref XFORM lpXform);

    [DllImport("gdi32.dll")]
    private static extern bool GetWorldTransform(IntPtr hdc, out XFORM lpXform);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    // ULTRA PERFORMANCE: Thread and process priority APIs
    [DllImport("kernel32.dll")]
    private static extern bool SetThreadPriority(IntPtr hThread, int nPriority);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    private static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint uPeriod);

    [StructLayout(LayoutKind.Sequential)]
    private struct XFORM
    {
        public float eM11;
        public float eM12;
        public float eM21;
        public float eM22;
        public float eDx;
        public float eDy;
    }

    private const int GM_ADVANCED = 2;
    private const int THREAD_PRIORITY_TIME_CRITICAL = 15;
    private const uint HIGH_PRIORITY_CLASS = 0x00000080;

    // Windows API for window capture
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    // ULTRA PERFORMANCE: Windows DWM API for hardware-accelerated capture
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    private const uint PW_CLIENTONLY = 0x1;
    private const uint PW_RENDERFULLCONTENT = 0x2;
    private const uint SRCCOPY = 0x00CC0020;
    private const uint CAPTUREBLT = 0x40000000;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    public WpfEffectOverlay(MainForm? mainForm = null)
    {
        _mainForm = mainForm;

        CreateOverlayWindow();
        InitializeDesktopDuplication();
        InitializeUltraPerformanceCapture();
    }

    private void InitializeUltraPerformanceCapture()
    {
        try
        {
            Debug.WriteLine("?? ULTRA PERFORMANCE MODE: Initializing maximum performance capture...");

            // ULTRA PERFORMANCE: Set high-precision timer resolution
            timeBeginPeriod(1); // 1ms timer resolution

            // ULTRA PERFORMANCE: Boost process priority
            SetPriorityClass(GetCurrentProcess(), HIGH_PRIORITY_CLASS);

            // Pre-allocate screen DC for better performance
            _screenDC = GetDC(IntPtr.Zero);
            if (_screenDC != IntPtr.Zero)
            {
                Debug.WriteLine("? Screen DC allocated successfully");
            }

            // Start performance stopwatch
            _performanceStopwatch.Start();

            Debug.WriteLine("?? ULTRA PERFORMANCE MODE: Initialization complete!");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"?? Failed to initialize ultra performance capture: {ex.Message}");
        }
    }

    private void CreateOverlayWindow()
    {
        // Create WPF window with true transparency support
        _overlayWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = WfBrushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            WindowState = WindowState.Normal,
            ResizeMode = ResizeMode.NoResize,
            ShowActivated = false,
            Left = 0,
            Top = 0,
            Width = SystemParameters.PrimaryScreenWidth,
            Height = SystemParameters.PrimaryScreenHeight,
            Visibility = Visibility.Hidden
        };

        // Create canvas for content
        _overlayCanvas = new Canvas
        {
            Background = WfBrushes.Transparent,
            Width = _overlayWindow.Width,
            Height = _overlayWindow.Height,
            // ULTRA PERFORMANCE: Enable hardware rendering
            CacheMode = new BitmapCache { EnableClearType = false, RenderAtScale = 1.0 }
        };

        // ULTRA PERFORMANCE: Enable hardware acceleration for the canvas
        RenderOptions.SetBitmapScalingMode(_overlayCanvas, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetEdgeMode(_overlayCanvas, EdgeMode.Aliased);

        _overlayWindow.Content = _overlayCanvas;

        // Ensure click-through is applied after loading
        _overlayWindow.Loaded += (s, e) =>
        {
            MakeWindowClickThrough();
            _overlayWindow.WindowStyle = WindowStyle.None;
        };

        Debug.WriteLine($"WPF Overlay window created: Size={_overlayWindow.Width}x{_overlayWindow.Height}");
    }

    private void MakeWindowClickThrough()
    {
        if (_overlayWindow == null) return;

        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(_overlayWindow).Handle;
            if (hwnd != IntPtr.Zero)
            {
                const int GWL_EXSTYLE = -20;
                const int WS_EX_TRANSPARENT = 0x00000020;
                const int WS_EX_LAYERED = 0x00080000;
                const int WS_EX_TOOLWINDOW = 0x00000080;

                var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);

                Debug.WriteLine("WPF Overlay window made click-through");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to make WPF overlay click-through: {ex.Message}");
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private WpfImage CreateAnimatedGifImage(string imagePath)
    {
        var image = new WpfImage();

        try
        {
            ImageBehavior.SetAnimatedSource(image, new BitmapImage(new Uri(imagePath, UriKind.Absolute)));
            Debug.WriteLine($"WpfAnimatedGif: Successfully set animated source for {Path.GetFileName(imagePath)}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WpfAnimatedGif: Failed to set animated source: {ex.Message}");

            // Fallback to regular image
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            image.Source = bitmap;
        }

        return image;
    }

    public void ShowStaticImage(string imagePath, TimeSpan duration)
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(() => ShowStaticImage(imagePath, duration)));
            return;
        }

        try
        {
            // Show window if hidden
            if (_overlayWindow?.Visibility != Visibility.Visible)
            {
                _overlayWindow?.Show();
                MakeWindowClickThrough();
            }

            WpfImage image;
            var extension = Path.GetExtension(imagePath).ToLowerInvariant();

            if (extension == ".gif")
            {
                image = CreateAnimatedGifImage(imagePath);
                image.Stretch = Stretch.Fill;
                image.Width = _overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth;
                image.Height = _overlayCanvas?.Height ?? SystemParameters.PrimaryScreenHeight;

                Debug.WriteLine($"Added animated static GIF: {Path.GetFileName(imagePath)}");
            }
            else
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                image = new WpfImage
                {
                    Source = bitmap,
                    Stretch = Stretch.Fill,
                    Width = _overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth,
                    Height = _overlayCanvas?.Height ?? SystemParameters.PrimaryScreenHeight
                };

                Debug.WriteLine($"Added static image: {Path.GetFileName(imagePath)}");
            }

            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            _overlayCanvas?.Children.Add(image);

            Debug.WriteLine($"Added WPF static image: {Path.GetFileName(imagePath)}");

            // Remove after duration
            Task.Delay(duration).ContinueWith(_ =>
            {
                _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _overlayCanvas?.Children.Remove(image);

                    if (extension == ".gif")
                    {
                        ImageBehavior.SetAnimatedSource(image, null);
                    }

                    Debug.WriteLine($"Removed WPF static image: {Path.GetFileName(imagePath)}");

                    if (_overlayCanvas?.Children.Count == 0)
                    {
                        _overlayWindow?.Hide();
                        Debug.WriteLine("Hidden WPF overlay window - no more content");
                    }
                }));
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show WPF static image: {ex.Message}");
        }
    }

    public void ShowMovingImage(string imagePath, TimeSpan duration)
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(() => ShowMovingImage(imagePath, duration)));
            return;
        }

        try
        {
            // Show window if hidden
            if (_overlayWindow?.Visibility != Visibility.Visible)
            {
                _overlayWindow?.Show();
                MakeWindowClickThrough();
            }

            WpfImage image;
            var extension = Path.GetExtension(imagePath).ToLowerInvariant();

            if (extension == ".gif")
            {
                image = CreateAnimatedGifImage(imagePath);
                image.Stretch = Stretch.None;

                Debug.WriteLine($"Added animated moving GIF: {Path.GetFileName(imagePath)}");
            }
            else
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                image = new WpfImage
                {
                    Source = bitmap,
                    Stretch = Stretch.None
                };

                Debug.WriteLine($"Added static moving image: {Path.GetFileName(imagePath)}");
            }

            // Set initial position within screen bounds
            var initialX = new Random().Next(0, Math.Max(1, (int)(SystemParameters.PrimaryScreenWidth - 100))); // 100 = default image size
            var initialY = new Random().Next(0, Math.Max(1, (int)(SystemParameters.PrimaryScreenHeight - 100)));

            Canvas.SetLeft(image, initialX);
            Canvas.SetTop(image, initialY);
            _overlayCanvas?.Children.Add(image);

            Debug.WriteLine($"Added WPF moving image: {Path.GetFileName(imagePath)} at position ({initialX}, {initialY})");

            // Start animation
            AnimateMovingImage(image, duration, imagePath, extension);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show WPF moving image: {ex.Message}");
        }
    }

    private async void AnimateMovingImage(WpfImage image, TimeSpan duration, string imagePath, string extension)
    {
        var random = new Random();
        var startTime = DateTime.UtcNow;
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        // Store original size for scaling
        var originalWidth = image.Width;
        var originalHeight = image.Height;

        // If size is NaN, use a default size
        if (double.IsNaN(originalWidth) || originalWidth <= 0) originalWidth = 100;
        if (double.IsNaN(originalHeight) || originalHeight <= 0) originalHeight = 100;

        Debug.WriteLine($"WPF Animation - Original size: {originalWidth}x{originalHeight}");

        // Initialize the ScaleTransform for the image
        var scaleTransform = new ScaleTransform(1.0, 1.0);
        _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
        {
            image.RenderTransform = scaleTransform;
        }));

        // Start concurrent pulsing animation
        var pulsingTask = Task.Run(async () =>
        {
            while (DateTime.UtcNow - startTime < duration)
            {
                // Random scale factor between 0.1 (10%) and 10.0 (1000%)
                var scaleFactor = 0.1 + random.NextDouble() * 9.9;
                var newWidth = originalWidth * scaleFactor;
                var newHeight = originalHeight * scaleFactor;

                Debug.WriteLine($"WPF Pulsing - Scale: {scaleFactor:F2}, Original: {originalWidth}x{originalHeight}, New: {newWidth:F0}x{newHeight:F0}");

                _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Use ScaleTransform instead of setting Width/Height directly
                    scaleTransform.ScaleX = scaleFactor;
                    scaleTransform.ScaleY = scaleFactor;
                }));

                // Wait before next size change
                await Task.Delay(random.Next(300, 800));

                if (DateTime.UtcNow - startTime >= duration) break;
            }
        });

        while (DateTime.UtcNow - startTime < duration)
        {
            var movementBurst = random.Next(2, 6);

            for (int burst = 0; burst < movementBurst; burst++)
            {
                var currentX = Canvas.GetLeft(image);
                var currentY = Canvas.GetTop(image);

                if (double.IsNaN(currentX)) currentX = 0;
                if (double.IsNaN(currentY)) currentY = 0;

                // Calculate the current scaled size of the image
                var currentScaleX = scaleTransform.ScaleX;
                var currentScaleY = scaleTransform.ScaleY;
                var scaledWidth = originalWidth * currentScaleX;
                var scaledHeight = originalHeight * currentScaleY;

                // Keep the image within screen bounds considering its scaled size
                var minX = 0;
                var maxX = Math.Max(minX, screenWidth - scaledWidth);
                var minY = 0;
                var maxY = Math.Max(minY, screenHeight - scaledHeight);

                var x = random.Next((int)minX, (int)Math.Max(minX + 1, maxX));
                var y = random.Next((int)minY, (int)Math.Max(minY + 1, maxY));

                var animationSpeed = random.Next(100, 800);

                EasingFunctionBase easingFunction = random.Next(6) switch
                {
                    0 => new BounceEase { Bounces = random.Next(1, 4), Bounciness = random.NextDouble() * 2 },
                    1 => new ElasticEase { Oscillations = random.Next(1, 5), Springiness = random.NextDouble() * 10 },
                    2 => new BackEase { Amplitude = random.NextDouble() * 2 },
                    3 => new CircleEase(),
                    4 => new CubicEase(),
                    _ => new QuadraticEase()
                };

                easingFunction.EasingMode = (EasingMode)random.Next(3);

                var moveXAnimation = new DoubleAnimation
                {
                    From = currentX,
                    To = x,
                    Duration = TimeSpan.FromMilliseconds(animationSpeed),
                    EasingFunction = easingFunction
                };

                var moveYAnimation = new DoubleAnimation
                {
                    From = currentY,
                    To = y,
                    Duration = TimeSpan.FromMilliseconds(animationSpeed),
                    EasingFunction = easingFunction
                };

                image.BeginAnimation(Canvas.LeftProperty, moveXAnimation);
                image.BeginAnimation(Canvas.TopProperty, moveYAnimation);

                await Task.Delay(random.Next(50, 300));

                if (DateTime.UtcNow - startTime >= duration) break;
            }

            var pauseDuration = random.Next(200, 1200);

            if (random.Next(4) == 0)
            {
                pauseDuration = random.Next(10, 100);
            }

            if (random.Next(10) == 0)
            {
                pauseDuration = random.Next(1000, 3000);
            }

            await Task.Delay(pauseDuration);

            if (DateTime.UtcNow - startTime >= duration) break;
        }

        // Wait for pulsing animation to complete
        try
        {
            await pulsingTask;
            Debug.WriteLine("WPF Pulsing animation completed");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WPF Pulsing animation error: {ex.Message}");
        }

        // Remove when done
        _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
        {
            _overlayCanvas?.Children.Remove(image);

            if (extension == ".gif")
            {
                ImageBehavior.SetAnimatedSource(image, null);
            }

            Debug.WriteLine($"Removed WPF moving image: {Path.GetFileName(imagePath)}");

            if (_overlayCanvas?.Children.Count == 0)
            {
                _overlayWindow?.Hide();
                Debug.WriteLine("Hidden WPF overlay window - no more content");
            }
        }));
    }

    public void ShowEffectNotification(string message)
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(() => ShowEffectNotification(message)));
            return;
        }

        // Show window if hidden
        if (_overlayWindow?.Visibility != Visibility.Visible)
        {
            _overlayWindow?.Show();
            MakeWindowClickThrough();
        }

        var textBlock = new TextBlock
        {
            Text = message,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = WfBrushes.Yellow,
            Background = new SolidColorBrush(WfColor.FromArgb(150, 0, 0, 0))
        };

        Canvas.SetLeft(textBlock, 20);
        Canvas.SetTop(textBlock, 20);

        _overlayCanvas?.Children.Add(textBlock);

        Task.Delay(3000).ContinueWith(_ =>
        {
            _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
            {
                _overlayCanvas?.Children.Remove(textBlock);
            }));
        });
    }

    public void ShowSoundNotification(string soundName)
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(() => ShowSoundNotification(soundName)));
            return;
        }

        // Show window if hidden
        if (_overlayWindow?.Visibility != Visibility.Visible)
        {
            _overlayWindow?.Show();
            MakeWindowClickThrough();
        }

        var textBlock = new TextBlock
        {
            Text = $"? Now Playing: {soundName}",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = WfBrushes.Cyan,
            Background = new SolidColorBrush(WfColor.FromArgb(150, 0, 0, 0))
        };

        Canvas.SetLeft(textBlock, 20);
        Canvas.SetTop(textBlock, SystemParameters.PrimaryScreenHeight - 100);

        _overlayCanvas?.Children.Add(textBlock);

        Task.Delay(5000).ContinueWith(_ =>
        {
            _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
            {
                _overlayCanvas?.Children.Remove(textBlock);
            }));
        });
    }

    public void ShowColorFilter(DrawingColor color, TimeSpan duration)
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(() => ShowColorFilter(color, duration)));
            return;
        }

        // Show window if hidden
        if (_overlayWindow?.Visibility != Visibility.Visible)
        {
            _overlayWindow?.Show();
            MakeWindowClickThrough();
        }

        var colorBrush = new SolidColorBrush(WfColor.FromArgb(color.A, color.R, color.G, color.B));
        var rectangle = new System.Windows.Shapes.Rectangle
        {
            Fill = colorBrush,
            Width = _overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth,
            Height = _overlayCanvas?.Height ?? SystemParameters.PrimaryScreenHeight
        };

        Canvas.SetLeft(rectangle, 0);
        Canvas.SetTop(rectangle, 0);

        _overlayCanvas?.Children.Add(rectangle);

        Debug.WriteLine($"Created WPF color filter: ARGB({color.A},{color.R},{color.G},{color.B})");

        Task.Delay(duration).ContinueWith(_ =>
        {
            _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
            {
                _overlayCanvas?.Children.Remove(rectangle);
                Debug.WriteLine("Removed WPF color filter");
            }));
        });
    }

    public void ShowBlurFilter(TimeSpan duration)
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(() => ShowBlurFilter(duration)));
            return;
        }

        // Show window if hidden
        if (_overlayWindow?.Visibility != Visibility.Visible)
        {
            _overlayWindow?.Show();
            MakeWindowClickThrough();
        }

        Debug.WriteLine("Creating blur overlay from image...");

        try
        {
            // Look for blur images in the configurable blur directory
            var blurFolder = Path.IsPathRooted(Settings.BlurDirectory)
                ? Settings.BlurDirectory
                : Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", Settings.BlurDirectory);

            Debug.WriteLine($"Looking for blur images in: {blurFolder}");

            if (!Directory.Exists(blurFolder))
            {
                Debug.WriteLine($"Blur folder not found at: {blurFolder}");
                CreateFallbackBlurOverlay(duration);
                return;
            }

            var blurFiles = Directory.GetFiles(blurFolder, "*.*")
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (blurFiles.Length == 0)
            {
                Debug.WriteLine("No blur images found in blur folder");
                CreateFallbackBlurOverlay(duration);
                return;
            }

            // Pick a random blur image
            var random = new Random();
            var selectedBlurFile = blurFiles[random.Next(blurFiles.Length)];

            Debug.WriteLine($"Using blur image: {Path.GetFileName(selectedBlurFile)}");

            // Create image element with the blur overlay
            WpfImage blurImage;
            var extension = Path.GetExtension(selectedBlurFile).ToLowerInvariant();

            if (extension == ".gif")
            {
                blurImage = CreateAnimatedGifImage(selectedBlurFile);
            }
            else
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(selectedBlurFile, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                blurImage = new WpfImage { Source = bitmap };
            }

            // Set up the blur overlay to cover the entire screen
            blurImage.Stretch = Stretch.Fill;
            blurImage.Width = _overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth;
            blurImage.Height = _overlayCanvas?.Height ?? SystemParameters.PrimaryScreenHeight;
            blurImage.Opacity = 0.8; // Slightly transparent so some content can show through

            Canvas.SetLeft(blurImage, 0);
            Canvas.SetTop(blurImage, 0);

            _overlayCanvas?.Children.Add(blurImage);

            Debug.WriteLine("Applied blur image overlay successfully!");

            // Remove after duration
            Task.Delay(duration).ContinueWith(_ =>
            {
                _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _overlayCanvas?.Children.Remove(blurImage);

                        if (extension == ".gif")
                        {
                            ImageBehavior.SetAnimatedSource(blurImage, null);
                        }

                        Debug.WriteLine("Removed blur image overlay");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error removing blur overlay: {ex.Message}");
                    }
                }));
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Blur image overlay failed: {ex.Message}, using fallback");
            CreateFallbackBlurOverlay(duration);
        }
    }

    private void CreateFallbackBlurOverlay(TimeSpan duration)
    {
        Debug.WriteLine("Creating fallback blur overlay...");

        // Create a simple semi-transparent overlay with some texture
        var fallbackBlur = new System.Windows.Shapes.Rectangle
        {
            Width = _overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth,
            Height = _overlayCanvas?.Height ?? SystemParameters.PrimaryScreenHeight,
            Fill = new SolidColorBrush(WfColor.FromArgb(160, 200, 200, 200)), // 63% opacity light gray
            Effect = new BlurEffect
            {
                Radius = 20,
                KernelType = KernelType.Gaussian
            }
        };

        Canvas.SetLeft(fallbackBlur, 0);
        Canvas.SetTop(fallbackBlur, 0);

        _overlayCanvas?.Children.Add(fallbackBlur);

        Debug.WriteLine("Applied fallback blur overlay");

        // Remove after duration
        Task.Delay(duration).ContinueWith(_ =>
        {
            _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _overlayCanvas?.Children.Remove(fallbackBlur);
                    Debug.WriteLine("Removed fallback blur overlay");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error removing fallback blur: {ex.Message}");
                }
            }));
        });
    }

    public void ShowEffectActivationNotification(string effectName, string userName, int durationSeconds)
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(() => ShowEffectActivationNotification(effectName, userName, durationSeconds)));
            return;
        }

        // Show window if hidden
        if (_overlayWindow?.Visibility != Visibility.Visible)
        {
            _overlayWindow?.Show();
            MakeWindowClickThrough();
        }

        var message = $"{effectName.ToUpper()} activated by {userName} for {durationSeconds} seconds!";

        var notificationPanel = new Border
        {
            Background = new SolidColorBrush(WfColor.FromArgb(200, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(WfColor.FromArgb(255, 255, 215, 0)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15, 10, 15, 10)
        };

        var textBlock = new TextBlock
        {
            Text = message,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(WfColor.FromArgb(255, 255, 215, 0)),
            TextAlignment = TextAlignment.Center,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
        };

        notificationPanel.Child = textBlock;

        Canvas.SetLeft(notificationPanel, 20);
        Canvas.SetTop(notificationPanel, 20);

        _overlayCanvas?.Children.Add(notificationPanel);

        Debug.WriteLine($"Effect activation notification: {message}");

        // Animate entrance
        var slideInAnimation = new DoubleAnimation
        {
            From = -300,
            To = 20,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }
        };
        notificationPanel.BeginAnimation(Canvas.LeftProperty, slideInAnimation);

        // Remove after 4 seconds
        Task.Delay(4000).ContinueWith(_ =>
        {
            _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
            {
                var fadeOutAnimation = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(800),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };

                fadeOutAnimation.Completed += (s, e) =>
                {
                    _overlayCanvas?.Children.Remove(notificationPanel);
                    Debug.WriteLine("Removed effect activation notification");

                    if (_overlayCanvas?.Children.Count == 0)
                    {
                        _overlayWindow?.Hide();
                        Debug.WriteLine("Hidden WPF overlay window - no more content");
                    }
                };

                notificationPanel.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
            }));
        });
    }

    /// <summary>
    /// Shows a countdown for Mirror Mode in the top right corner
    /// </summary>
    public void ShowMirrorCountdown(DateTime endTime)
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(() => ShowMirrorCountdown(endTime)));
            return;
        }

        // Show window if hidden
        if (_overlayWindow?.Visibility != Visibility.Visible)
        {
            _overlayWindow?.Show();
            MakeWindowClickThrough();
        }

        var countdownPanel = new Border
        {
            Background = new SolidColorBrush(WfColor.FromArgb(180, 25, 25, 112)),
            BorderBrush = new SolidColorBrush(WfColor.FromArgb(255, 70, 130, 180)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 5, 10, 5),
            Tag = "MirrorCountdown" // Tag to identify this element
        };

        var countdownText = new TextBlock
        {
            Text = "MIRROR MODE",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = WfBrushes.White,
            TextAlignment = TextAlignment.Center,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
        };

        countdownPanel.Child = countdownText;

        // Position in top right corner
        var screenWidth = _overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth;
        Canvas.SetLeft(countdownPanel, screenWidth - 180);
        Canvas.SetTop(countdownPanel, 20);

        // Remove any existing mirror countdown
        var existingCountdown = _overlayCanvas?.Children.OfType<Border>()
            .FirstOrDefault(b => "MirrorCountdown".Equals(b.Tag));
        if (existingCountdown != null)
        {
            _overlayCanvas?.Children.Remove(existingCountdown);
        }

        _overlayCanvas?.Children.Add(countdownPanel);

        Debug.WriteLine($"Mirror countdown started until {endTime}");

        // Start countdown timer
        var countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500) // Update twice per second
        };

        countdownTimer.Tick += (s, e) =>
        {
            var remainingTime = endTime - DateTime.UtcNow;
            if (remainingTime.TotalSeconds <= 0)
            {
                countdownTimer.Stop();
                _overlayCanvas?.Children.Remove(countdownPanel);
                Debug.WriteLine("Mirror countdown ended");
                return;
            }

            countdownText.Text = $"MIRROR MODE\n{remainingTime.TotalSeconds:F0}s";
        };

        countdownTimer.Start();
    }

    public void ShowGameBanNotification(string processName, int shuffleCount, string username)
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(() => ShowGameBanNotification(processName, shuffleCount, username)));
            return;
        }

        // Show window if hidden
        if (_overlayWindow?.Visibility != Visibility.Visible)
        {
            _overlayWindow?.Show();
            MakeWindowClickThrough();
        }

        var message = $"?? {processName} BANNED for {shuffleCount} shuffles by {username}";

        var banPanel = new Border
        {
            Background = new SolidColorBrush(WfColor.FromArgb(200, 139, 0, 0)),
            BorderBrush = new SolidColorBrush(WfColor.FromArgb(255, 255, 69, 0)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15, 10, 15, 10)
        };

        var banStack = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };

        var banIcon = new TextBlock
        {
            Text = "?? ",
            FontSize = 24,
            FontWeight = FontWeights.Bold
        };

        var banText = new TextBlock
        {
            Text = $"{processName} BANNED\nfor {shuffleCount} shuffle{(shuffleCount == 1 ? "" : "s")} by {username}",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = WfBrushes.White,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            TextAlignment = TextAlignment.Center
        };

        banStack.Children.Add(banIcon);
        banStack.Children.Add(banText);
        banPanel.Child = banStack;

        // Position in center of screen
        var screenWidth = _overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth;
        var screenHeight = _overlayCanvas?.Height ?? SystemParameters.PrimaryScreenHeight;
        Canvas.SetLeft(banPanel, screenWidth / 2 - 250);
        Canvas.SetTop(banPanel, screenHeight / 2 - 50);

        _overlayCanvas?.Children.Add(banPanel);

        Debug.WriteLine($"Game ban notification: {message}");

        // Animate entrance with shake effect
        var shakeAnimation = new DoubleAnimationUsingKeyFrames();
        shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(screenWidth / 2 - 250, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
        shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(screenWidth / 2 - 245, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50))));
        shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(screenWidth / 2 - 255, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
        shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(screenWidth / 2 - 250, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));

        banPanel.BeginAnimation(Canvas.LeftProperty, shakeAnimation);

        // Remove after 5 seconds
        Task.Delay(5000).ContinueWith(_ =>
        {
            _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
            {
                _overlayCanvas?.Children.Remove(banPanel);
                Debug.WriteLine("Removed game ban notification");
            }));
        });
    }

    /// <summary>
    /// Shows the list of currently banned games in the bottom right corner with shuffle countdown
    /// </summary>
    public void ShowBannedGamesList(Dictionary<string, int> bannedGames)
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(() => ShowBannedGamesList(bannedGames)));
            return;
        }

        // Remove existing banned games list
        var existingList = _overlayCanvas?.Children.OfType<Border>()
            .FirstOrDefault(b => "BannedGamesList".Equals(b.Tag));
        if (existingList != null)
        {
            _overlayCanvas?.Children.Remove(existingList);
        }

        if (bannedGames.Count == 0)
        {
            Debug.WriteLine("No banned games to display");
            return;
        }

        // Show window if hidden
        if (_overlayWindow?.Visibility != Visibility.Visible)
        {
            _overlayWindow?.Show();
            MakeWindowClickThrough();
        }

        // Create main container for all banned games (supports up to 10)
        var mainContainer = new Border
        {
            Background = new SolidColorBrush(WfColor.FromArgb(200, 25, 25, 25)),
            BorderBrush = new SolidColorBrush(WfColor.FromArgb(255, 255, 69, 0)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 5, 8, 5),
            Tag = "BannedGamesList" // Tag to identify this element
        };

        var listStack = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Vertical
        };

        // Header
        var headerText = new TextBlock
        {
            Text = "?? BANNED GAMES",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(WfColor.FromArgb(255, 255, 69, 0)),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 5)
        };
        listStack.Children.Add(headerText);

        // Game list (limit to 10, sort by remaining shuffles descending)
        var sortedGames = bannedGames.OrderByDescending(g => g.Value).Take(10).ToList();

        // Calculate the maximum width needed for all game names
        var maxGameNameLength = 0;
        foreach (var game in sortedGames)
        {
            var gameDisplayName = game.Key.Length > 35 ? game.Key.Substring(0, 32) + "..." : game.Key;
            maxGameNameLength = Math.Max(maxGameNameLength, gameDisplayName.Length);
        }

        // Calculate dynamic width based on content (minimum 220, maximum 400)
        var baseWidth = 220;
        var dynamicWidth = Math.Max(baseWidth, Math.Min(400, maxGameNameLength * 8 + 60)); // 8px per character + padding

        foreach (var game in sortedGames)
        {
            var gameContainer = new Border
            {
                Background = new SolidColorBrush(WfColor.FromArgb(150, 139, 0, 0)),
                BorderBrush = new SolidColorBrush(WfColor.FromArgb(100, 255, 69, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(0, 1, 0, 1),
                Width = dynamicWidth - 16 // Account for container padding
            };

            var gameText = new TextBlock
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(WfColor.FromArgb(255, 255, 255, 255)),
                TextAlignment = TextAlignment.Left,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            // Format: "Game Name: X shuffles" - Use full name with ellipsis if needed
            var gameName = game.Key.Length > 35 ? game.Key.Substring(0, 32) + "..." : game.Key;
            gameText.Text = $"{gameName}: {game.Value} shuffle{(game.Value == 1 ? "" : "s")}";

            // Set tooltip to show full game name if truncated
            if (game.Key.Length > 35)
            {
                gameText.ToolTip = $"{game.Key}: {game.Value} shuffle{(game.Value == 1 ? "" : "s")}";
            }

            // Color code based on remaining shuffles
            if (game.Value <= 1)
            {
                gameText.Foreground = new SolidColorBrush(WfColor.FromArgb(255, 255, 165, 0)); // Orange - about to expire
            }
            else if (game.Value <= 3)
            {
                gameText.Foreground = new SolidColorBrush(WfColor.FromArgb(255, 255, 255, 0)); // Yellow - low count
            }
            else
            {
                gameText.Foreground = new SolidColorBrush(WfColor.FromArgb(255, 255, 255, 255)); // White - normal
            }

            gameContainer.Child = gameText;
            listStack.Children.Add(gameContainer);
        }

        // If there are more than 10 banned games, show a "... and X more" indicator
        if (bannedGames.Count > 10)
        {
            var moreText = new TextBlock
            {
                Text = $"... and {bannedGames.Count - 10} more",
                FontSize = 9,
                Foreground = new SolidColorBrush(WfColor.FromArgb(150, 255, 255, 255)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                FontStyle = FontStyles.Italic
            };
            listStack.Children.Add(moreText);
        }

        mainContainer.Child = listStack;

        // Position in bottom right corner
        var screenWidth = _overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth;
        var screenHeight = _overlayCanvas?.Height ?? SystemParameters.PrimaryScreenHeight;

        // Calculate dynamic height based on number of games (max 10)
        var containerHeight = Math.Min(300, 40 + (sortedGames.Count * 25) + (bannedGames.Count > 10 ? 20 : 0));
        mainContainer.Height = containerHeight;
        mainContainer.Width = dynamicWidth;

        Canvas.SetLeft(mainContainer, screenWidth - dynamicWidth - 10); // 10px margin from right edge
        Canvas.SetTop(mainContainer, screenHeight - containerHeight - 10); // 10px margin from bottom

        _overlayCanvas?.Children.Add(mainContainer);

        Debug.WriteLine($"Updated banned games countdown list with {bannedGames.Count} games (showing {sortedGames.Count}) - Dynamic width: {dynamicWidth}px");
    }

    public void ShowMirrorEffect(TimeSpan duration)
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(() => ShowMirrorEffect(duration)));
            return;
        }

        // Check if mirror mode is already active and extend it instead
        if (_isGameMirrorActive && _mirrorEndTime.HasValue)
        {
            ExtendMirrorMode(duration);
            return;
        }

        // Show window if hidden
        if (_overlayWindow?.Visibility != Visibility.Visible)
        {
            _overlayWindow?.Show();
            MakeWindowClickThrough();
        }

        Debug.WriteLine("?? MIRROR MODE: Starting mirror effect system...");
        Debug.WriteLine($"?? Duration requested: {duration.TotalSeconds}s");
        Debug.WriteLine($"?? MainForm available: {_mainForm != null}");
        if (_mainForm != null)
        {
            Debug.WriteLine($"?? MainForm.IsShuffling: {_mainForm.IsShuffling}");
        }

        try
        {
            // First try to find and mirror the active game window
            Debug.WriteLine("?? Attempting GAME WINDOW mirror capture...");
            if (TryGameWindowMirror(duration))
            {
                Debug.WriteLine("? MIRROR MODE: Game window mirror successful!");
                return;
            }

            // Fall back to Desktop Duplication if no game window found
            Debug.WriteLine("? Game window mirror failed, trying Desktop Duplication API...");

            // Check if Desktop Duplication API is available
            if (_d3dDevice == null || _outputDuplication == null)
            {
                Debug.WriteLine("? Desktop Duplication not available - using fallback methods");
                ShowFallbackMirrorEffect(duration);
                return;
            }

            // Try to test Desktop Duplication before proceeding
            try
            {
                var testResult = _outputDuplication.TryAcquireNextFrame(0, out var testFrameInfo, out var testDesktopResource);
                if (!testResult.Success && testResult.Code != (int)SharpDX.DXGI.ResultCode.WaitTimeout)
                {
                    Debug.WriteLine($"? Desktop Duplication test failed: {testResult} - falling back to other methods");
                    testDesktopResource?.Dispose();
                    ShowFallbackMirrorEffect(duration);
                    return;
                }

                // Release test frame if we got one
                if (testResult.Success)
                {
                    testDesktopResource?.Dispose();
                    _outputDuplication.ReleaseFrame();
                }
            }
            catch (Exception testEx)
            {
                Debug.WriteLine($"? Desktop Duplication test exception: {testEx.Message} - falling back to other methods");
                ShowFallbackMirrorEffect(duration);
                return;
            }

            // Start Desktop Duplication capture as fallback
            Debug.WriteLine("?? Starting Desktop Duplication mirror as fallback...");
            ShowDesktopDuplicationMirror(duration);

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? Mirror effect completely failed: {ex.Message}");
            Debug.WriteLine($"? Exception details: {ex}");
            ShowFallbackMirrorEffect(duration);
        }
    }

    /// <summary>
    /// Extends the duration of an already active mirror mode
    /// </summary>
    public void ExtendMirrorMode(TimeSpan additionalDuration)
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(() => ExtendMirrorMode(additionalDuration)));
            return;
        }

        if (!_isGameMirrorActive || !_mirrorEndTime.HasValue)
        {
            Debug.WriteLine("ExtendMirrorMode called but no active mirror mode found");
            return;
        }

        // Extend the end time
        var oldEndTime = _mirrorEndTime.Value;
        _mirrorEndTime = _mirrorEndTime.Value.Add(additionalDuration);

        Debug.WriteLine($"Extended mirror mode by {additionalDuration.TotalSeconds}s (from {oldEndTime} to {_mirrorEndTime})");

        // Update countdown display
        ShowMirrorCountdown(_mirrorEndTime.Value);
    }

    private void InitializeDesktopDuplication()
    {
        try
        {
            Debug.WriteLine("Initializing Desktop Duplication API for GPU-accelerated screen capture...");

            // Create Direct3D 11 device
            _d3dDevice = new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Hardware,
                DeviceCreationFlags.None);

            // Get DXGI adapter and output - specifically target primary monitor
            using var adapter = _d3dDevice.QueryInterface<SharpDX.DXGI.Device>().Adapter;

            // Find the primary output (monitor)
            Output? primaryOutput = null;
            for (int i = 0; i < adapter.GetOutputCount(); i++)
            {
                try
                {
                    var output = adapter.GetOutput(i);
                    var outputBounds = output.Description.DesktopBounds;

                    Debug.WriteLine($"Found output {i}: {output.Description.DeviceName} at {outputBounds.Left},{outputBounds.Top} size {outputBounds.Right - outputBounds.Left}x{outputBounds.Bottom - outputBounds.Top}");

                    // Primary monitor is typically at 0,0
                    if (outputBounds.Left == 0 && outputBounds.Top == 0)
                    {
                        primaryOutput = output;
                        Debug.WriteLine($"Selected primary output: {output.Description.DeviceName}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error checking output {i}: {ex.Message}");
                }
            }

            // If we couldn't find primary at 0,0, use the first available output
            if (primaryOutput == null)
            {
                Debug.WriteLine("Primary output not found at 0,0, using first available output");
                primaryOutput = adapter.GetOutput(0);
            }

            if (primaryOutput == null)
            {
                Debug.WriteLine("No outputs found!");
                return;
            }

            using var output1 = primaryOutput.QueryInterface<Output1>();

            // Create the desktop duplication interface
            _outputDuplication = output1.DuplicateOutput(_d3dDevice);

            Debug.WriteLine($"Desktop Duplication API initialized successfully!");
            Debug.WriteLine($"Output description: {primaryOutput.Description.DeviceName}");

            var bounds = primaryOutput.Description.DesktopBounds;
            Debug.WriteLine($"Desktop bounds: {bounds.Right - bounds.Left}x{bounds.Bottom - bounds.Top}");

            primaryOutput.Dispose();
        }
        catch (SharpDXException ex) when (ex.ResultCode == SharpDX.DXGI.ResultCode.InvalidCall)
        {
            Debug.WriteLine($"Desktop Duplication already in use by another process: {ex.Message}");
            Debug.WriteLine("This is normal if multiple instances are running. Using fallback method.");
        }
        catch (SharpDXException ex) when (ex.ResultCode == SharpDX.DXGI.ResultCode.AccessDenied)
        {
            Debug.WriteLine($"Desktop Duplication access denied: {ex.Message}");
            Debug.WriteLine("This can happen with certain security software or elevated processes. Using fallback method.");
        }
        catch (SharpDXException ex) when (ex.ResultCode.Code == unchecked((int)0x80070057)) // E_INVALIDARG
        {
            Debug.WriteLine($"Desktop Duplication invalid arguments: {ex.Message}");
            Debug.WriteLine("This typically happens when another instance is already capturing. Using fallback method.");
        }
        catch (ArgumentNullException ex)
        {
            Debug.WriteLine($"Desktop Duplication null parameter error: {ex.Message}");
            Debug.WriteLine("This can happen with display driver issues or incompatible hardware. Using fallback method.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize Desktop Duplication API: {ex.Message}");
            Debug.WriteLine("Falling back to system-level display transformation...");
        }
    }

    private void CleanupDesktopDuplication()
    {
        try
        {
            _isCapturing = false;

            // Clean up in proper order
            if (_outputDuplication != null)
            {
                try
                {
                    // Try to release any active frame before disposing
                    _outputDuplication.ReleaseFrame();
                }
                catch (SharpDXException)
                {
                    // Ignore errors - frame might not be acquired
                }

                _outputDuplication.Dispose();
                _outputDuplication = null;
            }

            _screenTexture?.Dispose();
            _screenTexture = null;

            _d3dDevice?.Dispose();
            _d3dDevice = null;

            Debug.WriteLine("Desktop Duplication resources cleaned up successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error cleaning up Desktop Duplication: {ex.Message}");
        }
    }

    private void CleanupCaptureResources()
    {
        try
        {
            if (_captureBitmap != IntPtr.Zero)
            {
                DeleteObject(_captureBitmap);
                _captureBitmap = IntPtr.Zero;
            }

            if (_captureMemoryDC != IntPtr.Zero)
            {
                DeleteDC(_captureMemoryDC);
                _captureMemoryDC = IntPtr.Zero;
            }

            if (_screenDC != IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, _screenDC);
                _screenDC = IntPtr.Zero;
            }

            Debug.WriteLine("?? Cleanup capture resources completed");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"?? Error cleaning up capture resources: {ex.Message}");
        }
    }

    private void ClearFramePool()
    {
        lock (_framePoolLock)
        {
            _framePool.Clear();
            Debug.WriteLine("?? Cleared frame pool for GC optimization");
        }
    }

    private string GetWindowTitle(IntPtr window)
    {
        try
        {
            var length = GetWindowTextLength(window);
            if (length == 0) return "";

            var title = new StringBuilder(length + 1);
            GetWindowText(window, title, title.Capacity);
            return title.ToString();
        }
        catch
        {
            return "";
        }
    }

    private IntPtr FindActiveGameWindow()
    {
        try
        {
            Debug.WriteLine("?? FindActiveGameWindow: Starting search...");

            // PRIORITY 1: Get current active game from shuffler
            if (_mainForm != null && _mainForm.IsShuffling)
            {
                Debug.WriteLine("?? Priority 1: Checking shuffler integration...");
                var shufflerActiveWindow = _mainForm.GetCurrentActiveGameWindow();
                var activeTitle = _mainForm.GetCurrentActiveGameTitle();

                Debug.WriteLine($"?? Shuffler provided window: {shufflerActiveWindow} ('{activeTitle}')");

                if (shufflerActiveWindow != IntPtr.Zero && IsValidGameWindow(shufflerActiveWindow))
                {
                    Debug.WriteLine($"? SHUFFLER INTEGRATION SUCCESS: Found active game from shuffler: '{activeTitle}'");
                    return shufflerActiveWindow;
                }
                else if (shufflerActiveWindow != IntPtr.Zero)
                {
                    Debug.WriteLine($"? Shuffler window invalid: {shufflerActiveWindow} ('{activeTitle}') - validation failed");
                }
                else
                {
                    Debug.WriteLine("? Shuffler returned null/zero window handle");
                }
            }
            else if (_mainForm != null)
            {
                Debug.WriteLine($"?? MainForm present but not shuffling (IsShuffling: {_mainForm.IsShuffling})");
            }
            else
            {
                Debug.WriteLine("?? MainForm is null (test mode)");
            }

            // PRIORITY 2: Try to get the foreground window as fallback (but filter out our own windows)
            Debug.WriteLine("?? Priority 2: Checking foreground window...");
            var foregroundWindow = GetForegroundWindow();
            var foregroundTitle = GetWindowTitle(foregroundWindow);

            Debug.WriteLine($"?? Foreground window: {foregroundWindow} ('{foregroundTitle}')");

            if (IsValidGameWindow(foregroundWindow))
            {
                Debug.WriteLine($"? Found valid foreground game window: '{foregroundTitle}'");
                return foregroundWindow;
            }
            else
            {
                Debug.WriteLine($"? Foreground window invalid: {foregroundWindow} ('{foregroundTitle}') - validation failed");
            }

            // PRIORITY 3: Scan all visible windows for games (last resort) - but filter out our own windows
            Debug.WriteLine("?? Priority 3: Scanning all visible windows...");
            var gameWindows = new List<(IntPtr Handle, string Title)>();

            // Use Windows API to enumerate all top-level windows
            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    var title = GetWindowTitle(hWnd);
                    if (IsValidGameWindow(hWnd))
                    {
                        gameWindows.Add((hWnd, title));
                        Debug.WriteLine($"?? Found potential game window: '{title}'");
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (gameWindows.Count > 0)
            {
                // Prefer the first valid game window
                var selectedGame = gameWindows[0];
                Debug.WriteLine($"? Selected first available game window: '{selectedGame.Title}'");
                return selectedGame.Handle;
            }

            Debug.WriteLine("? No valid game windows found through any method");
            return IntPtr.Zero;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? Error finding game window: {ex.Message}");
            Debug.WriteLine($"?? Exception details: {ex}");
            return IntPtr.Zero;
        }
    }

    private bool IsValidGameWindow(IntPtr window)
    {
        if (window == IntPtr.Zero) return false;

        try
        {
            // Check if window is visible
            if (!IsWindowVisible(window)) return false;

            // Get window dimensions
            if (!GetWindowRect(window, out var rect)) return false;

            // Check minimum size (games are usually substantial windows)
            if (rect.Width < 400 || rect.Height < 300) return false;

            // Get window title
            var title = GetWindowTitle(window);
            if (string.IsNullOrWhiteSpace(title)) return false;

            // ENHANCED FILTERING: Exclude our own application windows and test windows
            var excludedTitles = new[]
            {
                "WPF", "Overlay", "Effect Test Mode", "Twitch Settings", "BetterGameShuffler",
                "KHShuffler", "Game Shuffler", "Effect Test Mode & Settings",
                "Twitch Integration Settings", "Debug", "Test", "Settings"
            };

            foreach (var excluded in excludedTitles)
            {
                if (title.Contains(excluded, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"?? Excluded window: '{title}' (contains '{excluded}')");
                    return false;
                }
            }

            // Additional filtering for common non-game windows
            var excludedProcesses = new[]
            {
                "explorer", "dwm", "winlogon", "csrss", "lsass", "services", "svchost",
                "notepad", "calculator", "cmd", "powershell", "taskmgr", "regedit",
                "msedge", "chrome", "firefox", "discord", "spotify", "steam", // Common apps
                "devenv", "code", "notepad++", "visual studio" // Development tools
            };

            foreach (var excludedProcess in excludedProcesses)
            {
                if (title.Contains(excludedProcess, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"?? Excluded system/app window: '{title}' (contains '{excludedProcess}')");
                    return false;
                }
            }

            Debug.WriteLine($"? Valid game window candidate: '{title}' ({rect.Width}x{rect.Height})");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error validating window: {ex.Message}");
            return false;
        }
    }

    private unsafe BitmapSource? FastWindowCapture(IntPtr windowHandle)
    {
        try
        {
            // Get window dimensions using DWM for accuracy
            RECT dwmRect;
            if (DwmGetWindowAttribute(windowHandle, DWMWA_EXTENDED_FRAME_BOUNDS, out dwmRect, Marshal.SizeOf<RECT>()) != 0)
            {
                // Fallback to GetWindowRect
                if (!GetWindowRect(windowHandle, out dwmRect)) return null;
            }

            var width = dwmRect.Width;
            var height = dwmRect.Height;

            if (width <= 0 || height <= 0) return null;

            // ULTRA PERFORMANCE: Use pre-allocated resources
            if (_captureMemoryDC == IntPtr.Zero || _captureBitmap == IntPtr.Zero ||
                _lastCaptureWidth != width || _lastCaptureHeight != height)
            {
                PreallocateCaptureResources(width, height);
            }

            if (_captureMemoryDC == IntPtr.Zero || _captureBitmap == IntPtr.Zero)
            {
                return null; // Fast window capture not available
            }

            // ULTRA PERFORMANCE: Hardware-accelerated capture with multiple methods
            bool captured = false;

            // Method 1: PrintWindow with full rendering (best quality for games)
            captured = PrintWindow(windowHandle, _captureMemoryDC, PW_RENDERFULLCONTENT);

            if (!captured)
            {
                // Method 2: BitBlt from screen coordinates (most compatible)
                captured = BitBlt(_captureMemoryDC, 0, 0, width, height, _screenDC,
                    dwmRect.Left, dwmRect.Top, SRCCOPY | CAPTUREBLT);
            }

            if (captured)
            {
                // ULTRA PERFORMANCE: Create mirrored bitmap with optimized pipeline
                return CreateUltraOptimizedMirroredBitmap(width, height);
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Fast window capture error: {ex.Message}");
            return null;
        }
    }

    private unsafe BitmapSource? UltraFastWindowCapture(IntPtr windowHandle)
    {
        try
        {
            // Get window dimensions using DWM for accuracy
            RECT dwmRect;
            if (DwmGetWindowAttribute(windowHandle, DWMWA_EXTENDED_FRAME_BOUNDS, out dwmRect, Marshal.SizeOf<RECT>()) == 0)
            {
                var width = dwmRect.Width;
                var height = dwmRect.Height;

                if (width <= 0 || height <= 0) return null;

                // ULTRA PERFORMANCE: Use pre-allocated resources
                if (_captureMemoryDC == IntPtr.Zero || _captureBitmap == IntPtr.Zero ||
                    _lastCaptureWidth != width || _lastCaptureHeight != height)
                {
                    PreallocateCaptureResources(width, height);
                }

                if (_captureMemoryDC == IntPtr.Zero || _captureBitmap == IntPtr.Zero)
                {
                    return FastWindowCapture(windowHandle); // Fallback
                }

                // ULTRA PERFORMANCE: Hardware-accelerated capture with multiple methods
                bool captured = false;

                // Method 1: PrintWindow with full rendering (best quality for games)
                captured = PrintWindow(windowHandle, _captureMemoryDC, PW_RENDERFULLCONTENT);

                if (!captured)
                {
                    // Method 2: BitBlt from screen coordinates (most compatible)
                    captured = BitBlt(_captureMemoryDC, 0, 0, width, height, _screenDC,
                        dwmRect.Left, dwmRect.Top, SRCCOPY | CAPTUREBLT);
                }

                if (captured)
                {
                    // ULTRA PERFORMANCE: Create mirrored bitmap with optimized pipeline
                    return CreateUltraOptimizedMirroredBitmap(width, height);
                }
            }

            // Fallback to regular capture
            return FastWindowCapture(windowHandle);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ultra fast capture error: {ex.Message}");
            return FastWindowCapture(windowHandle);
        }
    }

    private bool TryGameWindowMirror(TimeSpan duration)
    {
        try
        {
            Debug.WriteLine("?? Attempting ULTRA-OPTIMIZED GAME WINDOW mirror capture...");

            var gameWindow = FindActiveGameWindow();
            if (gameWindow == IntPtr.Zero)
            {
                Debug.WriteLine("No active game window found for ultra-optimized capture");
                return false;
            }

            var gameTitle = GetWindowTitle(gameWindow);
            GetWindowRect(gameWindow, out var rect);

            Debug.WriteLine($"? Found game window: '{gameTitle}' at {rect.Width}x{rect.Height}");

            // Create mirrored image display
            var mirrorImage = new WpfImage
            {
                Width = _overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth,
                Height = _overlayCanvas?.Height ?? SystemParameters.PrimaryScreenHeight,
                Stretch = Stretch.Fill,
                // ULTRA PERFORMANCE: Maximum rendering optimizations
                CacheMode = new BitmapCache { EnableClearType = false, RenderAtScale = 1.0, SnapsToDevicePixels = true }
            };

            // ULTRA PERFORMANCE: Optimize rendering
            RenderOptions.SetBitmapScalingMode(mirrorImage, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetEdgeMode(mirrorImage, EdgeMode.Aliased);
            RenderOptions.SetCachingHint(mirrorImage, CachingHint.Cache);

            Canvas.SetLeft(mirrorImage, 0);
            Canvas.SetTop(mirrorImage, 0);
            _overlayCanvas?.Children.Add(mirrorImage);

            _activeGameWindow = gameWindow;
            _isGameMirrorActive = true;
            _mirrorEndTime = DateTime.UtcNow.Add(duration);

            // Create cancellation token for this mirror session
            _mirrorCancellationTokenSource?.Cancel(); // Cancel any existing session
            _mirrorCancellationTokenSource = new System.Threading.CancellationTokenSource();

            // Start ultra-optimized capture
            Task.Run(() => StartUltraOptimizedGameWindowCapture(mirrorImage, gameWindow, _mirrorCancellationTokenSource.Token));

            // Start cleanup monitoring task that checks the end time
            Task.Run(async () =>
            {
                try
                {
                    while (_isGameMirrorActive && _mirrorEndTime.HasValue && DateTime.UtcNow < _mirrorEndTime.Value)
                    {
                        if (_mirrorCancellationTokenSource.Token.IsCancellationRequested)
                            break;

                        await Task.Delay(500, _mirrorCancellationTokenSource.Token); // Check every 500ms
                    }

                    // Time's up or cancelled, clean up
                    if (_isGameMirrorActive)
                    {
                        _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            CleanupMirrorMode(mirrorImage);
                        }));
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Mirror mode cleanup task cancelled");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in mirror mode cleanup task: {ex.Message}");
                }
            }, _mirrorCancellationTokenSource.Token);

            Debug.WriteLine("?? ULTRA-OPTIMIZED GAME WINDOW mirroring activated successfully!");

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ultra-optimized game window mirror failed: {ex.Message}");
            return false;
        }
    }

    private void CleanupMirrorMode(WpfImage mirrorImage)
    {
        try
        {
            _isGameMirrorActive = false;
            _mirrorCancellationTokenSource?.Cancel();
            _mirrorEndTime = null;

            _overlayCanvas?.Children.Remove(mirrorImage);
            _activeGameWindow = IntPtr.Zero;

            Debug.WriteLine("Cleaned up ultra-optimized game window mirror");

            if (_overlayCanvas?.Children.Count == 0)
            {
                _overlayWindow?.Hide();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error cleaning up ultra-optimized mirror: {ex.Message}");
        }
    }

    private void ShowFallbackMirrorEffect(TimeSpan duration)
    {
        Debug.WriteLine("Starting fallback mirror effect...");

        // Create a simple fallback message
        var fallbackText = new TextBlock
        {
            Text = "Mirror Mode Active\n(Fallback Mode)",
            FontSize = 48,
            FontWeight = FontWeights.Bold,
            Foreground = WfBrushes.White,
            Background = new SolidColorBrush(WfColor.FromArgb(150, 0, 0, 0)),
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };

        Canvas.SetLeft(fallbackText, (_overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth) / 2 - 200);
        Canvas.SetTop(fallbackText, (_overlayCanvas?.Height ?? SystemParameters.PrimaryScreenHeight) / 2 - 50);

        _overlayCanvas?.Children.Add(fallbackText);

        Debug.WriteLine("Applied fallback mirror effect");

        // Remove after duration
        Task.Delay(duration).ContinueWith(_ =>
        {
            _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _overlayCanvas?.Children.Remove(fallbackText);
                    Debug.WriteLine("Removed fallback mirror effect");

                    if (_overlayCanvas?.Children.Count == 0)
                    {
                        _overlayWindow?.Hide();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error removing fallback mirror: {ex.Message}");
                }
            }));
        });
    }

    private void ShowDesktopDuplicationMirror(TimeSpan duration)
    {
        Debug.WriteLine("Starting Desktop Duplication mirror as fallback...");

        // Create mirrored image display
        var mirrorImage = new WpfImage
        {
            Width = _overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth,
            Height = _overlayCanvas?.Height ?? SystemParameters.PrimaryScreenHeight,
            Stretch = Stretch.Fill
        };

        Canvas.SetLeft(mirrorImage, 0);
        Canvas.SetTop(mirrorImage, 0);
        _overlayCanvas?.Children.Add(mirrorImage);

        _isCapturing = true;

        // Start Desktop Duplication capture
        Task.Run(async () =>
        {
            var frameCount = 0;
            var lastSecond = DateTime.UtcNow.Second;

            while (_isCapturing)
            {
                try
                {
                    if (_outputDuplication == null) break;

                    var result = _outputDuplication.TryAcquireNextFrame(1, out var frameInfo, out var desktopResource);

                    if (result.Success)
                    {
                        frameCount++;

                        // Process frame here (simplified)
                        // This would need full implementation for actual desktop duplication

                        desktopResource?.Dispose();
                        _outputDuplication.ReleaseFrame();
                    }

                    // Frame rate monitoring
                    if (DateTime.UtcNow.Second != lastSecond)
                    {
                        Debug.WriteLine($"Desktop Duplication FPS: {frameCount}");
                        frameCount = 0;
                        lastSecond = DateTime.UtcNow.Second;
                    }

                    await Task.Delay(16); // ~60 FPS
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Desktop Duplication error: {ex.Message}");
                    await Task.Delay(100);
                }
            }
        });

        // Schedule cleanup
        Task.Delay(duration).ContinueWith(_ =>
        {
            _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _isCapturing = false;
                    _overlayCanvas?.Children.Remove(mirrorImage);

                    Debug.WriteLine("Cleaned up Desktop Duplication mirror");

                    if (_overlayCanvas?.Children.Count == 0)
                    {
                        _overlayWindow?.Hide();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error cleaning up Desktop Duplication mirror: {ex.Message}");
                }
            }));
        });
    }

    private BitmapSource? CreateHardwareMirroredBitmap(int width, int height)
    {
        try
        {
            // Similar to CreateUltraFastMirror but simpler fallback
            if (_captureBitmap == IntPtr.Zero)
            {
                Debug.WriteLine("Capture bitmap handle is null for hardware mirror");
                return null;
            }

            using var capturedBitmap = System.Drawing.Image.FromHbitmap(_captureBitmap);
            return CreateUltraFastMirror(capturedBitmap, width, height);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Hardware mirrored bitmap creation failed: {ex.Message}");
            return null;
        }
    }

    private async Task StartUltraOptimizedGameWindowCapture(WpfImage targetImage, IntPtr gameWindow, System.Threading.CancellationToken cancellationToken)
    {
        try
        {
            Debug.WriteLine("?? ULTRA-OPTIMIZED 2.0: Starting maximum performance game window capture at 60+ FPS...");

            // ULTRA PERFORMANCE: Create dedicated capture thread with highest priority
            _captureThread = new System.Threading.Thread(() =>
            {
                UltraPerformanceCaptureLoop(targetImage, gameWindow, cancellationToken);
            })
            {
                IsBackground = false, // Foreground thread for maximum priority
                Priority = System.Threading.ThreadPriority.Highest,
                Name = "UltraGameCapture"
            };

            // ULTRA PERFORMANCE: Set thread to real-time priority and CPU affinity
            _captureThread.Start();

            // ULTRA PERFORMANCE: Boost thread priority to maximum
            SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_TIME_CRITICAL);

            // ULTRA PERFORMANCE: Pin to first CPU core for consistent performance
            try
            {
                SetThreadAffinityMask(GetCurrentThread(), new IntPtr(1)); // CPU core 0
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not set CPU affinity: {ex.Message}");
            }

            Debug.WriteLine("?? ULTRA-OPTIMIZED 2.0: Capture thread started with maximum priority");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ULTRA-OPTIMIZED capture failed to start: {ex.Message}");
        }
    }

    private void RestoreSystemDesktopMirror()
    {
        try
        {
            // Restore original system transform if we had one
            if (_isSystemMirrorActive)
            {
                var displayDC = CreateDC("DISPLAY", null, null, IntPtr.Zero);
                if (displayDC != IntPtr.Zero)
                {
                    SetGraphicsMode(displayDC, GM_ADVANCED);
                    SetWorldTransform(displayDC, ref _originalTransform);
                    DeleteDC(displayDC);

                    Debug.WriteLine("Restored original system desktop transform");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error restoring system desktop mirror: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // Cleanup resources
        CleanupDesktopDuplication();
        CleanupCaptureResources();

        try
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Dispatcher.Invoke(() =>
                {
                    if (_overlayCanvas != null)
                    {
                        // Remove all children
                        _overlayCanvas.Children.Clear();
                    }

                    // Close and remove the window
                    _overlayWindow.Close();
                });

                _overlayWindow = null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during WPF overlay disposal: {ex.Message}");
        }

        Debug.WriteLine("WPF overlay resources disposed");
    }

    private BitmapSource? CreateUltraOptimizedMirroredBitmap(int width, int height)
    {
        try
        {
            // ULTRA PERFORMANCE: Skip frame pooling checks for maximum speed
            // Convert directly to mirrored bitmap
            using var capturedBitmap = System.Drawing.Image.FromHbitmap(_captureBitmap);

            // ULTRA PERFORMANCE: Use the fastest mirroring method available
            return CreateUltraFastMirror(capturedBitmap, width, height);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ultra optimized bitmap creation error: {ex.Message}");
            return null;
        }
    }

    private BitmapSource? CreateTestPattern(int width, int height)
    {
        try
        {
            // Create a simple test pattern bitmap
            using var testBitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var graphics = System.Drawing.Graphics.FromImage(testBitmap);

            graphics.Clear(System.Drawing.Color.DarkGray);
            graphics.DrawString("Game Window Capture",
                new System.Drawing.Font("Arial", 24),
                System.Drawing.Brushes.White,
                width / 2 - 100, height / 2 - 20);

            var handle = testBitmap.GetHbitmap();
            try
            {
                var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    handle, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                bitmapSource.Freeze();
                return bitmapSource;
            }
            finally
            {
                DeleteObject(handle);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Test pattern creation failed: {ex.Message}");
            return null;
        }
    }

    private void PreallocateCaptureResources(int width, int height)
    {
        try
        {
            // Only reallocate if size changed
            if (_lastCaptureWidth == width && _lastCaptureHeight == height &&
                _captureMemoryDC != IntPtr.Zero && _captureBitmap != IntPtr.Zero)
            {
                return; // Resources already allocated for this size
            }

            Debug.WriteLine($"?? Pre-allocating capture resources for {width}x{height}...");

            // Clean up old resources
            if (_captureBitmap != IntPtr.Zero)
            {
                DeleteObject(_captureBitmap);
                _captureBitmap = IntPtr.Zero;
            }

            if (_captureMemoryDC != IntPtr.Zero)
            {
                DeleteDC(_captureMemoryDC);
                _captureMemoryDC = IntPtr.Zero;
            }

            // Create new resources
            if (_screenDC != IntPtr.Zero)
            {
                _captureMemoryDC = CreateCompatibleDC(_screenDC);
                if (_captureMemoryDC != IntPtr.Zero)
                {
                    _captureBitmap = CreateCompatibleBitmap(_screenDC, width, height);
                    if (_captureBitmap != IntPtr.Zero)
                    {
                        SelectObject(_captureMemoryDC, _captureBitmap);
                        _lastCaptureWidth = width;
                        _lastCaptureHeight = height;
                        Debug.WriteLine($"? Pre-allocated capture resources successfully");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"? Failed to pre-allocate capture resources: {ex.Message}");
        }
    }

    public void ClearAllEffects()
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(ClearAllEffects));
            return;
        }

        // Clean up all animated GIFs before clearing
        foreach (var child in _overlayCanvas?.Children.OfType<WpfImage>() ?? Enumerable.Empty<WpfImage>())
        {
            ImageBehavior.SetAnimatedSource(child, null);
        }

        // Stop any active Desktop Duplication capture
        _isCapturing = false;

        // Stop any active game window capture
        if (_isGameMirrorActive)
        {
            _isGameMirrorActive = false;
            _mirrorCancellationTokenSource?.Cancel();
            _mirrorEndTime = null;
            _activeGameWindow = IntPtr.Zero;
            Debug.WriteLine("Stopped game window mirror capture on cleanup");
        }

        // Restore system mirror if active
        if (_isSystemMirrorActive)
        {
            RestoreSystemDesktopMirror();
            _isSystemMirrorActive = false;
            Debug.WriteLine("Restored desktop mirror on cleanup");
        }

        // ULTRA PERFORMANCE: Clear frame pool
        ClearFramePool();

        _overlayCanvas?.Children.Clear();
        _activeElements.Clear();
        _overlayWindow?.Hide();

        Debug.WriteLine("Cleared all WPF effects including game capture, desktop duplication, and system mirror");
    }

    private BitmapSource? CreateUltraFastMirror(System.Drawing.Image sourceImage, int width, int height)
    {
        try
        {
            // ULTRA PERFORMANCE: Use System.Drawing only for maximum speed
            using var mirroredBitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var mirrorGraphics = System.Drawing.Graphics.FromImage(mirroredBitmap);

            // ULTRA PERFORMANCE: Fastest possible graphics settings
            mirrorGraphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            mirrorGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            mirrorGraphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            mirrorGraphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
            mirrorGraphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;

            // ULTRA PERFORMANCE: Horizontal flip for mirror effect
            mirrorGraphics.ScaleTransform(-1, 1);
            mirrorGraphics.TranslateTransform(-width, 0);
            mirrorGraphics.DrawImage(sourceImage, 0, 0, width, height);

            // ULTRA PERFORMANCE: Fastest WPF conversion method
            try
            {
                var handle = mirroredBitmap.GetHbitmap();
                try
                {
                    var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        handle, IntPtr.Zero, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    bitmapSource.Freeze(); // CRITICAL: Freeze for thread safety and performance
                    return bitmapSource;
                }
                finally
                {
                    DeleteObject(handle);
                }
            }
            catch (Exception)
            {
                // Fast fallback - return null instead of crashing
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ultra fast mirror creation failed: {ex.Message}");
            return null;
        }
    }

    private void UltraPerformanceCaptureLoop(WpfImage targetImage, IntPtr gameWindow, System.Threading.CancellationToken cancellationToken)
    {
        try
        {
            var frameCount = 0;
            var lastSecond = DateTime.UtcNow.Second;
            var currentGameWindow = gameWindow;

            // ULTRA PERFORMANCE: Pre-allocate all variables to avoid GC pressure
            var consecutiveFailures = 0;
            var maxConsecutiveFailures = 3; // Better tolerance
            var gameValidationCounter = 0;
            var gameWindowValidationInterval = 600; // Every 10 seconds at 60fps (reduced validation)

            // ULTRA PERFORMANCE: Pre-allocate capture resources
            GetWindowRect(currentGameWindow, out var initialRect);
            PreallocateCaptureResources(initialRect.Width, initialRect.Height);

            // ULTRA PERFORMANCE: Frame consistency tracking with better buffering
            BitmapSource? lastValidFrame = null;
            var lastCaptureTime = _performanceStopwatch.Elapsed.TotalMilliseconds;

            // ULTRA PERFORMANCE: Frame timing optimization
            var targetFrameTime = 16.66667; // 60 FPS
            var frameTimeBuffer = new double[10]; // Rolling average for stability
            var frameTimeIndex = 0;

            while (!cancellationToken.IsCancellationRequested && _isGameMirrorActive)
            {
                var frameStartTime = _performanceStopwatch.Elapsed.TotalMilliseconds;

                try
                {
                    // ULTRA PERFORMANCE: Reduced game switching checks for maximum FPS (every 2 seconds)
                    if (frameCount % 120 == 0 && _mainForm != null && _mainForm.IsShuffling)
                    {
                        var shufflerActiveWindow = _mainForm.GetCurrentActiveGameWindow();
                        if (shufflerActiveWindow != IntPtr.Zero && shufflerActiveWindow != currentGameWindow)
                        {
                            var newGameTitle = GetWindowTitle(shufflerActiveWindow);
                            var oldGameTitle = GetWindowTitle(currentGameWindow);

                            Debug.WriteLine($"?? GAME SWITCH: '{oldGameTitle}' ? '{newGameTitle}'");
                            currentGameWindow = shufflerActiveWindow;
                            _activeGameWindow = currentGameWindow;

                            // Re-allocate capture resources for new window size
                            GetWindowRect(currentGameWindow, out var newRect);
                            PreallocateCaptureResources(newRect.Width, newRect.Height);

                            lastValidFrame = null;
                            consecutiveFailures = 0;
                        }
                    }

                    // ULTRA PERFORMANCE: Much less frequent window validation
                    gameValidationCounter++;
                    if (gameValidationCounter >= gameWindowValidationInterval)
                    {
                        if (!IsWindowVisible(currentGameWindow))
                        {
                            var newWindow = FindActiveGameWindow();
                            if (newWindow != IntPtr.Zero)
                            {
                                Debug.WriteLine($"?? Window validation: Switching to '{GetWindowTitle(newWindow)}'");
                                currentGameWindow = newWindow;
                                _activeGameWindow = currentGameWindow;

                                GetWindowRect(currentGameWindow, out var newRect);
                                PreallocateCaptureResources(newRect.Width, newRect.Height);

                                lastValidFrame = null;
                                consecutiveFailures = 0;
                            }
                        }
                        gameValidationCounter = 0;
                    }

                    // ULTRA PERFORMANCE: Optimized capture timing with frame skipping if needed
                    var timeSinceLastCapture = frameStartTime - lastCaptureTime;
                    if (timeSinceLastCapture >= 15.0) // Slightly faster than 60fps for headroom
                    {
                        BitmapSource? mirroredBitmap = null;

                        // ULTRA PERFORMANCE: Try capture with reduced lock time
                        try
                        {
                            mirroredBitmap = UltraFastWindowCapture(currentGameWindow);
                        }
                        catch (Exception captureEx)
                        {
                            Debug.WriteLine($"Capture error: {captureEx.Message}");
                        }

                        if (mirroredBitmap != null)
                        {
                            lastValidFrame = mirroredBitmap;
                            consecutiveFailures = 0;
                            lastCaptureTime = frameStartTime;

                            // ULTRA PERFORMANCE: Queue UI update asynchronously to avoid blocking
                            _ = _overlayWindow.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    if (!cancellationToken.IsCancellationRequested && _isGameMirrorActive)
                                    {
                                        targetImage.Source = mirroredBitmap;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"Error updating ultra-optimized image: {ex.Message}");
                                }
                            }), DispatcherPriority.Render); // Use Render priority instead of Send
                        }
                        else
                        {
                            consecutiveFailures++;

                            // Use last valid frame for better consistency
                            if (lastValidFrame != null && consecutiveFailures <= maxConsecutiveFailures)
                            {
                                _ = _overlayWindow.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    try
                                    {
                                        if (!cancellationToken.IsCancellationRequested && _isGameMirrorActive)
                                        {
                                            targetImage.Source = lastValidFrame;
                                        }
                                    }
                                    catch { }
                                }), DispatcherPriority.Render);
                            }
                            else if (consecutiveFailures > maxConsecutiveFailures)
                            {
                                GetWindowRect(currentGameWindow, out var rect);
                                mirroredBitmap = CreateTestPattern(rect.Width, rect.Height);
                                if (mirroredBitmap != null)
                                {
                                    lastValidFrame = mirroredBitmap;
                                    _ = _overlayWindow.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        try
                                        {
                                            if (!cancellationToken.IsCancellationRequested && _isGameMirrorActive)
                                            {
                                                targetImage.Source = mirroredBitmap;
                                            }
                                        }
                                        catch { }
                                    }), DispatcherPriority.Render);
                                }
                                consecutiveFailures = 0;
                            }
                        }
                    }

                    // ULTRA PERFORMANCE: Simplified frame rate monitoring (less frequent)
                    frameCount++;
                    if (frameCount % 60 == 0) // Only check every 60 frames
                    {
                        var currentSecond = DateTime.UtcNow.Second;
                        if (currentSecond != lastSecond)
                        {
                            var actualFps = frameCount;
                            frameCount = 0;
                            lastSecond = currentSecond;

                            Debug.WriteLine($"???? ULTRA 2.0 Game Capture FPS: {actualFps} (Target: 60) ????");
                        }
                    }

                    // ULTRA PERFORMANCE: Adaptive timing with rolling average
                    var frameEndTime = _performanceStopwatch.Elapsed.TotalMilliseconds;
                    var actualFrameTime = frameEndTime - frameStartTime;

                    // Update rolling average
                    frameTimeBuffer[frameTimeIndex] = actualFrameTime;
                    frameTimeIndex = (frameTimeIndex + 1) % frameTimeBuffer.Length;

                    // Calculate optimal sleep time
                    var avgFrameTime = frameTimeBuffer.Average();
                    var remainingTime = targetFrameTime - avgFrameTime;

                    if (remainingTime > 3.0) // Only sleep if we have significant time
                    {
                        System.Threading.Thread.Sleep(2); // Short sleep
                    }
                    else if (remainingTime > 1.0)
                    {
                        System.Threading.Thread.Sleep(1); // Minimal sleep
                    }
                    // Otherwise, don't sleep at all for maximum performance
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ULTRA-OPTIMIZED 2.0 frame error: {ex.Message}");
                    consecutiveFailures++;

                    // Minimal delay on errors
                    System.Threading.Thread.Sleep(1);
                }
            }

            Debug.WriteLine($"?? ULTRA-OPTIMIZED 2.0 capture stopped");
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("ULTRA-OPTIMIZED 2.0 capture cancelled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ULTRA-OPTIMIZED 2.0 capture failed: {ex.Message}");
        }
    }
}