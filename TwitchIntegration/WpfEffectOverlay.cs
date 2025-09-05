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
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfAnimatedGif;
using WpfImage = System.Windows.Controls.Image;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using DrawingColor = System.Drawing.Color;
using BetterGameShuffler; // Added namespace import for Settings

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
        // Create WPF window with true transparency support
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
            
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            _overlayCanvas?.Children.Add(image);
            
            Debug.WriteLine($"Added WPF moving image: {Path.GetFileName(imagePath)}");
            
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
            var movementBurst = random.Next(2, 6);
            
            for (int burst = 0; burst < movementBurst; burst++)
            {
                var currentX = Canvas.GetLeft(image);
                var currentY = Canvas.GetTop(image);
                
                if (double.IsNaN(currentX)) currentX = 0;
                if (double.IsNaN(currentY)) currentY = 0;
                
                var x = random.Next(-50, (int)Math.Max(1, screenWidth - image.ActualWidth + 100));
                var y = random.Next(-50, (int)Math.Max(1, screenHeight - image.ActualHeight + 100));
                
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
            Foreground = WpfBrushes.Yellow,
            Background = new SolidColorBrush(WpfColor.FromArgb(150, 0, 0, 0))
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
            Text = $"?? Now Playing: {soundName}",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = WpfBrushes.Cyan,
            Background = new SolidColorBrush(WpfColor.FromArgb(150, 0, 0, 0))
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
            Fill = new SolidColorBrush(WpfColor.FromArgb(160, 200, 200, 200)), // 63% opacity light gray
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
            Background = new SolidColorBrush(WpfColor.FromArgb(200, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(WpfColor.FromArgb(255, 255, 215, 0)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15, 10, 15, 10)
        };
        
        var textBlock = new TextBlock
        {
            Text = message,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(WpfColor.FromArgb(255, 255, 215, 0)),
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
}