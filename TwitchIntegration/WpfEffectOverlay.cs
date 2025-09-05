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
using WpfAnimatedGif;
using WpfImage = System.Windows.Controls.Image;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using DrawingColor = System.Drawing.Color;

namespace BetterGameShuffler.TwitchIntegration;

public class WpfEffectOverlay : IDisposable
{
    private Window? _overlayWindow;
    private Canvas? _overlayCanvas;
    private readonly List<OverlayElement> _activeElements = new();
    
    public WpfEffectOverlay()
    {
        CreateOverlayWindow();
    }
    
    private void CreateOverlayWindow()
    {
        // Create WPF window with true transparency support and NO design-time features
        _overlayWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = WpfBrushes.Transparent,
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
        
        // COMPLETELY disable all design-time and preview features
        _overlayWindow.SetValue(System.ComponentModel.DesignerProperties.IsInDesignModeProperty, false);
        _overlayWindow.SetValue(UIElement.SnapsToDevicePixelsProperty, false);
        
        // Create canvas for content
        _overlayCanvas = new Canvas
        {
            Background = WpfBrushes.Transparent,
            Width = _overlayWindow.Width,
            Height = _overlayWindow.Height
        };
        
        _overlayWindow.Content = _overlayCanvas;
        
        // Ensure click-through is applied after loading
        _overlayWindow.Loaded += (s, e) => 
        {
            MakeWindowClickThrough();
            // Force hide any potential design-time UI
            _overlayWindow.WindowStyle = WindowStyle.None;
        };
        
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
                const int WS_EX_TOOLWINDOW = 0x00000080; // Additional flag to hide from alt-tab and taskbar
                
                var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);
                
                Debug.WriteLine("WPF Overlay window made click-through with enhanced hiding");
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
        // Use WpfAnimatedGif library for proper GIF animation
        var image = new WpfImage();
        
        try
        {
            // Set the animated source using WpfAnimatedGif library
            ImageBehavior.SetAnimatedSource(image, new BitmapImage(new Uri(imagePath, UriKind.Absolute)));
            
            Debug.WriteLine($"WpfAnimatedGif: Successfully set animated source for {Path.GetFileName(imagePath)}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WpfAnimatedGif: Failed to set animated source: {ex.Message}");
            
            // Fallback to regular image if animation fails
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
                // Use WpfAnimatedGif library for proper GIF animation
                image = CreateAnimatedGifImage(imagePath);
                image.Stretch = Stretch.Fill;
                image.Width = _overlayCanvas?.Width ?? SystemParameters.PrimaryScreenWidth;
                image.Height = _overlayCanvas?.Height ?? SystemParameters.PrimaryScreenHeight;
                
                Debug.WriteLine($"Added animated static GIF: {Path.GetFileName(imagePath)}");
            }
            else
            {
                // For other formats, use standard loading
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
            
            // Set position
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            
            // Add to canvas
            _overlayCanvas?.Children.Add(image);
            
            Debug.WriteLine($"Added WPF static image: {Path.GetFileName(imagePath)} with native transparency");
            
            // Remove after duration
            Task.Delay(duration).ContinueWith(_ =>
            {
                _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _overlayCanvas?.Children.Remove(image);
                    
                    // Clean up animated GIF resources
                    if (extension == ".gif")
                    {
                        ImageBehavior.SetAnimatedSource(image, null);
                    }
                    
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
            
            WpfImage image;
            var extension = Path.GetExtension(imagePath).ToLowerInvariant();
            
            if (extension == ".gif")
            {
                // Use WpfAnimatedGif library for proper GIF animation
                image = CreateAnimatedGifImage(imagePath);
                image.Stretch = Stretch.None; // Keep original size for moving images
                
                Debug.WriteLine($"Added animated moving GIF: {Path.GetFileName(imagePath)}");
            }
            else
            {
                // For other formats, use standard loading
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                
                image = new WpfImage
                {
                    Source = bitmap,
                    Stretch = Stretch.None // Keep original size for moving images
                };
                
                Debug.WriteLine($"Added static moving image: {Path.GetFileName(imagePath)}");
            }
            
            // Set initial position to avoid NaN issues
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            
            // Add to canvas
            _overlayCanvas?.Children.Add(image);
            
            Debug.WriteLine($"Added WPF moving image: {Path.GetFileName(imagePath)} with native PNG transparency");
            
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
        
        while (DateTime.UtcNow - startTime < duration)
        {
            // CHAOTIC MOVEMENT: Multiple rapid movements in quick succession
            var movementBurst = random.Next(2, 6); // 2-5 rapid movements per burst
            
            for (int burst = 0; burst < movementBurst; burst++)
            {
                // Get current position for smooth transitions
                var currentX = Canvas.GetLeft(image);
                var currentY = Canvas.GetTop(image);
                
                // Handle NaN values by setting to 0
                if (double.IsNaN(currentX)) currentX = 0;
                if (double.IsNaN(currentY)) currentY = 0;
                
                // CHAOTIC POSITIONING: More extreme and unpredictable movements
                var x = random.Next(-50, (int)Math.Max(1, screenWidth - image.ActualWidth + 100)); // Allow off-screen
                var y = random.Next(-50, (int)Math.Max(1, screenHeight - image.ActualHeight + 100)); // Allow off-screen
                
                // CHAOTIC SPEED: Vary animation speed dramatically
                var animationSpeed = random.Next(100, 800); // 100ms to 800ms per movement
                
                // CHAOTIC EASING: Random easing functions for different movement feels
                EasingFunctionBase easingFunction = random.Next(6) switch
                {
                    0 => new BounceEase { Bounces = random.Next(1, 4), Bounciness = random.NextDouble() * 2 },
                    1 => new ElasticEase { Oscillations = random.Next(1, 5), Springiness = random.NextDouble() * 10 },
                    2 => new BackEase { Amplitude = random.NextDouble() * 2 },
                    3 => new CircleEase(),
                    4 => new CubicEase(),
                    _ => new QuadraticEase()
                };
                
                // Random easing mode for even more chaos
                easingFunction.EasingMode = (EasingMode)random.Next(3); // In, Out, or InOut
                
                // Create chaotic animations with random easing
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
                
                // Animate X and Y positions
                image.BeginAnimation(Canvas.LeftProperty, moveXAnimation);
                image.BeginAnimation(Canvas.TopProperty, moveYAnimation);
                
                // CHAOTIC TIMING: Random short delays between burst movements
                await Task.Delay(random.Next(50, 300)); // Very short delays for rapid-fire movement
                
                // Break out of burst if duration exceeded
                if (DateTime.UtcNow - startTime >= duration) break;
            }
            
            // CHAOTIC PAUSES: Random longer pauses between movement bursts
            var pauseDuration = random.Next(200, 1200); // 0.2s to 1.2s pause
            
            // 25% chance for SUPER CHAOTIC rapid-fire mode (no pause)
            if (random.Next(4) == 0)
            {
                pauseDuration = random.Next(10, 100); // Almost no pause - pure chaos!
            }
            
            // 10% chance for sudden FREEZE (dramatic pause)
            if (random.Next(10) == 0)
            {
                pauseDuration = random.Next(1000, 3000); // 1-3 second dramatic freeze
            }
            
            await Task.Delay(pauseDuration);
            
            // Break out if duration exceeded
            if (DateTime.UtcNow - startTime >= duration) break;
        }
        
        // CHAOTIC EXIT: Random exit animation
        var exitStyle = random.Next(3);
        switch (exitStyle)
        {
            case 0: // Spin out
                var rotateTransform = new RotateTransform();
                image.RenderTransform = rotateTransform;
                image.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
                
                var spinAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 720, // Two full rotations
                    Duration = TimeSpan.FromMilliseconds(1000),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                rotateTransform.BeginAnimation(RotateTransform.AngleProperty, spinAnimation);
                
                var fadeAnimation = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(1000),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                image.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
                
                await Task.Delay(1000);
                break;
                
            case 1: // Zoom out
                var scaleTransform = new ScaleTransform();
                image.RenderTransform = scaleTransform;
                image.RenderTransformOrigin = new WpfPoint(0.5, 0.5);
                
                var scaleAnimation = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(800),
                    EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseIn }
                };
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
                
                await Task.Delay(800);
                break;
                
            case 2: // Slide off screen
                var currentX = Canvas.GetLeft(image);
                var currentY = Canvas.GetTop(image);
                if (double.IsNaN(currentX)) currentX = 0;
                if (double.IsNaN(currentY)) currentY = 0;
                
                var exitX = random.Next(2) == 0 ? -200 : screenWidth + 200; // Slide left or right
                var exitY = random.Next(2) == 0 ? -200 : screenHeight + 200; // Slide up or down
                
                var slideAnimation = new DoubleAnimation
                {
                    From = currentX,
                    To = exitX,
                    Duration = TimeSpan.FromMilliseconds(600),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                image.BeginAnimation(Canvas.LeftProperty, slideAnimation);
                
                var slideYAnimation = new DoubleAnimation
                {
                    From = currentY,
                    To = exitY,
                    Duration = TimeSpan.FromMilliseconds(600),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                image.BeginAnimation(Canvas.TopProperty, slideYAnimation);
                
                await Task.Delay(600);
                break;
        }
        
        // Remove when done
        _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
        {
            _overlayCanvas?.Children.Remove(image);
            
            // Clean up animated GIF resources
            if (extension == ".gif")
            {
                ImageBehavior.SetAnimatedSource(image, null);
            }
            
            Debug.WriteLine($"Removed WPF moving image with CHAOTIC exit: {Path.GetFileName(imagePath)}");
            
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
        // This method is no longer needed since we're using activation notifications instead
        // Keeping as placeholder for compatibility
    }
    
    public void RemoveActiveEffect(string effectName)
    {
        // This method is no longer needed since we're using activation notifications instead
        // Keeping as placeholder for compatibility
    }
    
    private void UpdateActiveEffectsDisplay()
    {
        // This method is no longer needed since we're using activation notifications instead
        // Keeping as placeholder for compatibility
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
        
        _overlayCanvas?.Children.Clear();
        _activeElements.Clear();
        _overlayWindow?.Hide();
        
        Debug.WriteLine("Cleared all WPF effects");
    }
    
    public void Dispose()
    {
        // Clean up all animated GIFs before disposing
        if (_overlayCanvas != null)
        {
            foreach (var child in _overlayCanvas.Children.OfType<WpfImage>())
            {
                ImageBehavior.SetAnimatedSource(child, null);
            }
        }
        
        _overlayWindow?.Close();
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
        
        // Create the notification message
        var message = $"{effectName.ToUpper()} activated by {userName} for {durationSeconds} seconds!";
        
        var notificationPanel = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromArgb(200, 0, 0, 0)), // Semi-transparent black
            BorderBrush = new SolidColorBrush(WpfColor.FromArgb(255, 255, 215, 0)), // Gold border
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15, 10, 15, 10)
        };
        
        var textBlock = new TextBlock
        {
            Text = message,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(WpfColor.FromArgb(255, 255, 215, 0)), // Gold text
            TextAlignment = TextAlignment.Center,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
        };
        
        notificationPanel.Child = textBlock;
        
        // Position in top-left corner
        Canvas.SetLeft(notificationPanel, 20);
        Canvas.SetTop(notificationPanel, 20);
        
        _overlayCanvas?.Children.Add(notificationPanel);
        
        Debug.WriteLine($"Effect activation notification: {message}");
        
        // Animate entrance (slide in from left)
        var slideInAnimation = new DoubleAnimation
        {
            From = -300,
            To = 20,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }
        };
        notificationPanel.BeginAnimation(Canvas.LeftProperty, slideInAnimation);
        
        // Remove after 4 seconds with exit animation
        Task.Delay(4000).ContinueWith(_ =>
        {
            _overlayWindow?.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Animate exit (fade out)
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
                    
                    // Hide window if no more content
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
    
    private void UpdateOverlay(object? sender, EventArgs e)
    {
        // Clean up expired elements if needed
        _activeElements.RemoveAll(e => DateTime.UtcNow > e.EndTime);
    }
}