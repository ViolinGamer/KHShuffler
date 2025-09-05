using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfImage = System.Windows.Controls.Image;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using DrawingColor = System.Drawing.Color;

namespace BetterGameShuffler.TwitchIntegration;

public class WpfEffectOverlay : IDisposable
{
    private Window? _overlayWindow;
    private Canvas? _overlayCanvas;
    private readonly List<OverlayElement> _activeElements = new();
    private readonly DispatcherTimer _updateTimer;
    
    public WpfEffectOverlay()
    {
        CreateOverlayWindow();
        
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _updateTimer.Tick += UpdateOverlay;
        _updateTimer.Start();
    }
    
    private void CreateOverlayWindow()
    {
        // Create WPF window with true transparency support
        _overlayWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = WpfBrushes.Transparent, // TRUE transparency - no color keys needed!
            Topmost = true,
            ShowInTaskbar = false,
            WindowState = WindowState.Normal,
            Left = 0,
            Top = 0,
            Width = SystemParameters.PrimaryScreenWidth,
            Height = SystemParameters.PrimaryScreenHeight,
            Visibility = Visibility.Hidden // Start hidden
        };
        
        // Create canvas for content
        _overlayCanvas = new Canvas
        {
            Background = WpfBrushes.Transparent,
            Width = _overlayWindow.Width,
            Height = _overlayWindow.Height
        };
        
        _overlayWindow.Content = _overlayCanvas;
        
        // Make click-through
        MakeWindowClickThrough();
        
        Debug.WriteLine($"WPF Overlay window created: Size={_overlayWindow.Width}x{_overlayWindow.Height}, True transparency enabled");
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
                
                var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
                
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
                MakeWindowClickThrough(); // Reapply click-through after showing
            }
            
            // Load PNG with full transparency support
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            
            // Create Image control - WPF automatically handles PNG transparency!
            var image = new WpfImage
            {
                Source = bitmap,
                Stretch = Stretch.Fill,
                Width = _overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth,
                Height = _overlayCanvas?.Height ?? SystemParameters.PrimaryScreenHeight
            };
            
            // Set position
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            
            // Add to canvas
            _overlayCanvas?.Children.Add(image);
            
            Debug.WriteLine($"Added WPF static image: {Path.GetFileName(imagePath)} with native PNG transparency");
            
            // Remove after duration
            Task.Delay(duration).ContinueWith(_ =>
            {
                _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _overlayCanvas?.Children.Remove(image);
                    Debug.WriteLine($"Removed WPF static image: {Path.GetFileName(imagePath)}");
                    
                    // Hide window if no more content
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
            
            // Load PNG with full transparency support
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            
            // Create Image control
            var image = new WpfImage
            {
                Source = bitmap,
                Stretch = Stretch.None // Keep original size for moving images
            };
            
            // Add to canvas
            _overlayCanvas?.Children.Add(image);
            
            Debug.WriteLine($"Added WPF moving image: {Path.GetFileName(imagePath)} with native PNG transparency");
            
            // Start animation
            AnimateMovingImage(image, duration, imagePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show WPF moving image: {ex.Message}");
        }
    }
    
    private async void AnimateMovingImage(WpfImage image, TimeSpan duration, string imagePath)
    {
        var random = new Random();
        var startTime = DateTime.UtcNow;
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        
        while (DateTime.UtcNow - startTime < duration)
        {
            var x = random.Next(0, (int)Math.Max(1, screenWidth - image.ActualWidth));
            var y = random.Next(0, (int)Math.Max(1, screenHeight - image.ActualHeight));
            
            // Smooth WPF animation
            var moveAnimation = new DoubleAnimation
            {
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new QuadraticEase()
            };
            
            // Animate X position
            moveAnimation.To = x;
            image.BeginAnimation(Canvas.LeftProperty, moveAnimation);
            
            // Animate Y position
            moveAnimation.To = y;
            image.BeginAnimation(Canvas.TopProperty, moveAnimation);
            
            await Task.Delay(random.Next(500, 1500));
        }
        
        // Remove when done
        _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
        {
            _overlayCanvas?.Children.Remove(image);
            Debug.WriteLine($"Removed WPF moving image: {Path.GetFileName(imagePath)}");
            
            // Hide window if no more content
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
            Foreground = WpfBrushes.Yellow,
            Background = new SolidColorBrush(WpfColor.FromArgb(150, 0, 0, 0))
        };
        
        Canvas.SetLeft(textBlock, 20);
        Canvas.SetTop(textBlock, 20);
        
        _overlayCanvas?.Children.Add(textBlock);
        
        // Remove after 3 seconds
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
            Foreground = WpfBrushes.Cyan,
            Background = new SolidColorBrush(WpfColor.FromArgb(150, 0, 0, 0))
        };
        
        Canvas.SetLeft(textBlock, 20);
        Canvas.SetTop(textBlock, SystemParameters.PrimaryScreenHeight - 100);
        
        _overlayCanvas?.Children.Add(textBlock);
        
        // Remove after 5 seconds
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
        
        var colorBrush = new SolidColorBrush(WpfColor.FromArgb(color.A, color.R, color.G, color.B));
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
        
        // Remove after duration
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
        
        var blurBrush = new SolidColorBrush(WpfColor.FromArgb(100, 128, 128, 128));
        var rectangle = new System.Windows.Shapes.Rectangle
        {
            Fill = blurBrush,
            Width = _overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth,
            Height = _overlayCanvas?.Height ?? SystemParameters.PrimaryScreenHeight
        };
        
        Canvas.SetLeft(rectangle, 0);
        Canvas.SetTop(rectangle, 0);
        
        _overlayCanvas?.Children.Add(rectangle);
        
        Debug.WriteLine("Created WPF blur filter");
        
        // Remove after duration
        Task.Delay(duration).ContinueWith(_ =>
        {
            _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
            {
                _overlayCanvas?.Children.Remove(rectangle);
                Debug.WriteLine("Removed WPF blur filter");
            }));
        });
    }
    
    public void AddActiveEffect(string effectName, TimeSpan duration)
    {
        var element = new OverlayElement
        {
            Text = effectName,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.Add(duration),
            Type = OverlayElementType.ActiveEffect
        };
        
        _activeElements.Add(element);
        UpdateActiveEffectsDisplay();
    }
    
    public void RemoveActiveEffect(string effectName)
    {
        _activeElements.RemoveAll(e => e.Text == effectName && e.Type == OverlayElementType.ActiveEffect);
        UpdateActiveEffectsDisplay();
    }
    
    private void UpdateActiveEffectsDisplay()
    {
        // Implementation similar to WinForms version but using WPF controls
        // For brevity, keeping it simple - you can expand this if needed
    }
    
    private void UpdateOverlay(object? sender, EventArgs e)
    {
        // Clean up expired elements
        _activeElements.RemoveAll(e => DateTime.UtcNow > e.EndTime);
        
        // Update active effects display periodically
        if (_activeElements.Any())
        {
            UpdateActiveEffectsDisplay();
        }
    }
    
    public void ClearAllEffects()
    {
        if (_overlayWindow?.Dispatcher.CheckAccess() == false)
        {
            _overlayWindow.Dispatcher.BeginInvoke(new Action(ClearAllEffects));
            return;
        }
        
        _overlayCanvas?.Children.Clear();
        _activeElements.Clear();
        _overlayWindow?.Hide();
        
        Debug.WriteLine("Cleared all WPF effects");
    }
    
    public void Dispose()
    {
        _updateTimer?.Stop();
        _overlayWindow?.Close();
    }
}