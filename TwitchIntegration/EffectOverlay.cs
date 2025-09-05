using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BetterGameShuffler.TwitchIntegration;

public class EffectOverlay : IDisposable
{
    private Form? _overlayForm;
    private readonly List<OverlayElement> _activeElements = new();
    private readonly System.Windows.Forms.Timer _updateTimer;
    
    public EffectOverlay()
    {
        CreateOverlayForm();
        
        _updateTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _updateTimer.Tick += UpdateOverlay;
        _updateTimer.Start();
    }
    
    private void CreateOverlayForm()
    {
        _overlayForm = new Form();
        
        // Configure basic form properties first
        _overlayForm.FormBorderStyle = FormBorderStyle.None;
        _overlayForm.TopMost = true;
        _overlayForm.ShowInTaskbar = false;
        _overlayForm.StartPosition = FormStartPosition.Manual;
        _overlayForm.WindowState = FormWindowState.Normal;
        
        // COMPLETELY INVISIBLE OVERLAY - NO COLORS, NO TRANSPARENCY KEYS
        _overlayForm.AllowTransparency = true;
        // NO TransparencyKey at all
        // NO BackColor at all
        
        // Size and position the form
        _overlayForm.Location = new Point(0, 0);
        _overlayForm.Size = Screen.PrimaryScreen.Bounds.Size;
        _overlayForm.Bounds = Screen.PrimaryScreen.Bounds;
        
        // Hide the form initially - only show it when we need to display something
        _overlayForm.Visible = false;
        
        Debug.WriteLine($"Overlay form created: Size={_overlayForm.Size}, AllowTransparency=true, Initially hidden");
    }
    
    private void MakeOverlayClickThrough()
    {
        if (_overlayForm?.Handle == IntPtr.Zero) return;
        
        try
        {
            // Get current window style
            int exStyle = GetWindowLong(_overlayForm.Handle, GWL_EXSTYLE);
            
            // Add WS_EX_TRANSPARENT to make it click-through
            SetWindowLong(_overlayForm.Handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            
            Debug.WriteLine("Overlay form made click-through");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to make overlay click-through: {ex.Message}");
        }
    }
    
    // Windows API constants and functions for click-through functionality
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    
    public void ShowEffectNotification(string message)
    {
        if (_overlayForm != null && _overlayForm.InvokeRequired)
        {
            _overlayForm.BeginInvoke(new Action(() => ShowEffectNotification(message)));
            return;
        }
        
        var notification = new Label
        {
            Text = message,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.Yellow,
            BackColor = Color.FromArgb(150, 0, 0, 0),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        
        _overlayForm?.Controls.Add(notification);
        
        // Remove after 3 seconds
        Task.Delay(3000).ContinueWith(_ =>
        {
            _overlayForm?.BeginInvoke(new Action(() => _overlayForm.Controls.Remove(notification)));
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
        if (_overlayForm != null && _overlayForm.InvokeRequired)
        {
            _overlayForm.BeginInvoke(new Action(UpdateActiveEffectsDisplay));
            return;
        }
        
        // Remove existing active effects display
        var existingDisplay = _overlayForm?.Controls.OfType<Panel>()
            .FirstOrDefault(p => p.Name == "ActiveEffectsPanel");
        if (existingDisplay != null)
        {
            _overlayForm?.Controls.Remove(existingDisplay);
        }
        
        if (_activeElements.Count == 0) return;
        
        var panel = new Panel
        {
            Name = "ActiveEffectsPanel",
            Location = new Point(20, 80),
            Size = new Size(300, _activeElements.Count * 30 + 10),
            BackColor = Color.FromArgb(150, 0, 0, 0)
        };
        
        for (int i = 0; i < _activeElements.Count; i++)
        {
            var element = _activeElements[i];
            var remaining = element.EndTime - DateTime.UtcNow;
            
            if (remaining.TotalSeconds > 0)
            {
                var label = new Label
                {
                    Text = $"{element.Text}: {remaining.TotalSeconds:F0}s",
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(5, 5 + i * 25),
                    AutoSize = true
                };
                
                panel.Controls.Add(label);
            }
        }
        
        _overlayForm?.Controls.Add(panel);
    }
    
    public void ShowMovingImage(string imagePath, TimeSpan duration)
    {
        if (_overlayForm != null && _overlayForm.InvokeRequired)
        {
            _overlayForm.BeginInvoke(new Action(() => ShowMovingImage(imagePath, duration)));
            return;
        }
        
        try
        {
            var image = ImageLoader.LoadImage(imagePath);
            if (image == null)
            {
                Debug.WriteLine($"Failed to load image: {Path.GetFileName(imagePath)}");
                return;
            }
            
            var pictureBox = new PictureBox
            {
                Image = image,
                SizeMode = PictureBoxSizeMode.AutoSize
                // No BackColor - let the image transparency work naturally
            };
            
            var extension = Path.GetExtension(imagePath).ToLowerInvariant();
            
            // Enable animation for GIFs and animated WebPs
            if (extension == ".gif" && image.GetFrameCount(FrameDimension.Time) > 1)
            {
                ImageAnimator.Animate(image, null);
                Debug.WriteLine($"Animated GIF loaded: {Path.GetFileName(imagePath)} ({image.GetFrameCount(FrameDimension.Time)} frames)");
            }
            else if (extension == ".webp" && ImageLoader.IsAnimatedWebP(image))
            {
                ImageAnimator.Animate(image, null);
                Debug.WriteLine($"Animated WebP loaded: {Path.GetFileName(imagePath)} ({image.GetFrameCount(FrameDimension.Time)} frames)");
            }
            else if (extension == ".webp")
            {
                Debug.WriteLine($"Static WebP image loaded: {Path.GetFileName(imagePath)}");
            }
            else
            {
                Debug.WriteLine($"Static image loaded: {Path.GetFileName(imagePath)} ({extension})");
            }
            
            _overlayForm?.Controls.Add(pictureBox);
            
            // Start animation
            _ = Task.Run(() => AnimateMovingImage(pictureBox, duration, imagePath));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show moving image: {ex.Message}");
        }
    }
    
    private async Task AnimateMovingImage(PictureBox pictureBox, TimeSpan duration, string imagePath)
    {
        var random = new Random();
        var startTime = DateTime.UtcNow;
        var screen = Screen.PrimaryScreen.Bounds;
        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        
        while (DateTime.UtcNow - startTime < duration)
        {
            var x = random.Next(0, Math.Max(1, screen.Width - pictureBox.Width));
            var y = random.Next(0, Math.Max(1, screen.Height - pictureBox.Height));
            
            // Smooth movement
            await AnimateToPosition(pictureBox, x, y);
            await Task.Delay(random.Next(500, 1500));
        }
        
        _overlayForm?.BeginInvoke(new Action(() => 
        {
            _overlayForm.Controls.Remove(pictureBox);
            
            // Stop animation for both GIFs and animated WebPs
            if (pictureBox.Image != null)
            {
                if ((extension == ".gif" && pictureBox.Image.GetFrameCount(FrameDimension.Time) > 1) ||
                    (extension == ".webp" && ImageLoader.IsAnimatedWebP(pictureBox.Image)))
                {
                    ImageAnimator.StopAnimate(pictureBox.Image, null);
                    Debug.WriteLine($"Stopped animation for: {Path.GetFileName(imagePath)}");
                }
            }
            
            pictureBox.Image?.Dispose();
            pictureBox.Dispose();
        }));
    }
    
    private async Task AnimateToPosition(PictureBox control, int targetX, int targetY)
    {
        const int steps = 20;
        var startX = control.Location.X;
        var startY = control.Location.Y;
        
        for (int i = 0; i <= steps; i++)
        {
            var progress = (float)i / steps;
            var currentX = (int)(startX + (targetX - startX) * progress);
            var currentY = (int)(startY + (targetY - startY) * progress);
            
            _overlayForm?.BeginInvoke(new Action(() => control.Location = new Point(currentX, currentY)));
            await Task.Delay(25);
        }
    }
    
    public void ShowStaticImage(string imagePath, TimeSpan duration)
    {
        if (_overlayForm != null && _overlayForm.InvokeRequired)
        {
            _overlayForm.BeginInvoke(new Action(() => ShowStaticImage(imagePath, duration)));
            return;
        }
        
        try
        {
            var image = ImageLoader.LoadImage(imagePath);
            if (image == null)
            {
                Debug.WriteLine($"Failed to load image: {Path.GetFileName(imagePath)}");
                return;
            }
            
            // Show the overlay form only when we have content to display
            if (_overlayForm != null && !_overlayForm.Visible)
            {
                _overlayForm.Show();
                // Make it click-through after showing
                MakeOverlayClickThrough();
            }
            
            // Create a PictureBox directly on the overlay form - ABSOLUTELY NO background colors
            var pictureBox = new PictureBox
            {
                Name = $"StaticImage_{DateTime.UtcNow.Ticks}",
                Image = image,
                Size = _overlayForm?.Size ?? Screen.PrimaryScreen.Bounds.Size,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Location = new Point(0, 0),
                // ABSOLUTELY NO BackColor - pure transparency
                BorderStyle = BorderStyle.None
            };
            
            var extension = Path.GetExtension(imagePath).ToLowerInvariant();
            
            // Enable animation for GIFs and animated WebPs
            if (extension == ".gif" && image.GetFrameCount(FrameDimension.Time) > 1)
            {
                ImageAnimator.Animate(image, null);
                Debug.WriteLine($"Animated static GIF loaded: {Path.GetFileName(imagePath)} ({image.GetFrameCount(FrameDimension.Time)} frames)");
            }
            else if (extension == ".webp" && ImageLoader.IsAnimatedWebP(image))
            {
                ImageAnimator.Animate(image, null);
                Debug.WriteLine($"Animated static WebP loaded: {Path.GetFileName(imagePath)} ({image.GetFrameCount(FrameDimension.Time)} frames)");
            }
            else if (extension == ".webp")
            {
                Debug.WriteLine($"Static WebP overlay loaded: {Path.GetFileName(imagePath)}");
            }
            else
            {
                Debug.WriteLine($"Static overlay loaded: {Path.GetFileName(imagePath)} ({extension})");
            }
            
            // Add the PictureBox directly to the overlay form
            _overlayForm?.Controls.Add(pictureBox);
            Debug.WriteLine($"Added static image: {Path.GetFileName(imagePath)}");
            
            Task.Delay(duration).ContinueWith(_ =>
            {
                _overlayForm?.BeginInvoke(new Action(() => 
                {
                    var imageToRemove = _overlayForm.Controls.OfType<PictureBox>().FirstOrDefault(p => p.Name == pictureBox.Name);
                    if (imageToRemove != null)
                    {
                        _overlayForm.Controls.Remove(imageToRemove);
                        
                        // Stop animation for both GIFs and animated WebPs
                        if ((extension == ".gif" && image.GetFrameCount(FrameDimension.Time) > 1) ||
                            (extension == ".webp" && ImageLoader.IsAnimatedWebP(image)))
                        {
                            ImageAnimator.StopAnimate(image, null);
                            Debug.WriteLine($"Stopped static animation for: {Path.GetFileName(imagePath)}");
                        }
                        
                        image.Dispose();
                        imageToRemove.Dispose();
                        Debug.WriteLine($"Removed static image: {Path.GetFileName(imagePath)}");
                        
                        // Hide overlay if no more controls
                        if (_overlayForm != null && _overlayForm.Controls.Count == 0)
                        {
                            _overlayForm.Hide();
                            Debug.WriteLine("Hidden overlay form - no more content");
                        }
                    }
                }));
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show static image: {ex.Message}");
        }
    }
    
    public void ShowColorFilter(Color color, TimeSpan duration)
    {
        if (_overlayForm != null && _overlayForm.InvokeRequired)
        {
            _overlayForm.BeginInvoke(new Action(() => ShowColorFilter(color, duration)));
            return;
        }
        
        // Create unique name for stacking
        var panelName = $"ColorFilter_{DateTime.UtcNow.Ticks}";
        
        var colorPanel = new Panel
        {
            Name = panelName,
            Size = _overlayForm?.Size ?? Screen.PrimaryScreen.Bounds.Size,
            Location = new Point(0, 0),
            BackColor = color
        };
        
        Debug.WriteLine($"Created color filter: {panelName}, Color=ARGB({color.A},{color.R},{color.G},{color.B})");
        
        _overlayForm?.Controls.Add(colorPanel);
        
        Task.Delay(duration).ContinueWith(_ =>
        {
            _overlayForm?.BeginInvoke(new Action(() => 
            {
                var panelToRemove = _overlayForm.Controls.OfType<Panel>().FirstOrDefault(p => p.Name == panelName);
                if (panelToRemove != null)
                {
                    _overlayForm.Controls.Remove(panelToRemove);
                    Debug.WriteLine($"Removed color filter: {panelName}");
                }
            }));
        });
    }
    
    public void ShowBlurFilter(TimeSpan duration)
    {
        if (_overlayForm != null && _overlayForm.InvokeRequired)
        {
            _overlayForm.BeginInvoke(new Action(() => ShowBlurFilter(duration)));
            return;
        }
        
        // Create unique name for stacking
        var panelName = $"BlurFilter_{DateTime.UtcNow.Ticks}";
        
        // Simple blur effect using semi-transparent gray overlay
        var blurPanel = new Panel
        {
            Name = panelName,
            Size = _overlayForm?.Size ?? Screen.PrimaryScreen.Bounds.Size,
            Location = new Point(0, 0),
            BackColor = Color.FromArgb(100, 128, 128, 128) // Semi-transparent gray for blur effect
        };
        
        Debug.WriteLine($"Created blur filter: {panelName}");
        
        _overlayForm?.Controls.Add(blurPanel);
        
        Task.Delay(duration).ContinueWith(_ =>
        {
            _overlayForm?.BeginInvoke(new Action(() => 
            {
                var panelToRemove = _overlayForm.Controls.OfType<Panel>().FirstOrDefault(p => p.Name == panelName);
                if (panelToRemove != null)
                {
                    _overlayForm.Controls.Remove(panelToRemove);
                    Debug.WriteLine($"Removed blur filter: {panelName}");
                }
            }));
        });
    }
    
    public void ShowSoundNotification(string soundName)
    {
        if (_overlayForm != null && _overlayForm.InvokeRequired)
        {
            _overlayForm.BeginInvoke(new Action(() => ShowSoundNotification(soundName)));
            return;
        }
        
        var notification = new Label
        {
            Text = $"? Now Playing: {soundName}",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.Cyan,
            BackColor = Color.FromArgb(150, 0, 0, 0),
            AutoSize = true,
            Location = new Point(20, Screen.PrimaryScreen.Bounds.Height - 100)
        };
        
        _overlayForm?.Controls.Add(notification);
        
        Task.Delay(5000).ContinueWith(_ =>
        {
            _overlayForm?.BeginInvoke(new Action(() => _overlayForm.Controls.Remove(notification)));
        });
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
        if (_overlayForm != null && _overlayForm.InvokeRequired)
        {
            _overlayForm.BeginInvoke(new Action(ClearAllEffects));
            return;
        }
        
        if (_overlayForm == null) return;
        
        var controlsToRemove = new List<Control>();
        
        // Find all effect controls (but keep notifications and active effects display)
        foreach (Control control in _overlayForm.Controls)
        {
            if (control is Panel panel)
            {
                // Remove color filters, blur filters, and static image containers
                // but keep ActiveEffectsPanel
                if (panel.Name != "ActiveEffectsPanel")
                {
                    controlsToRemove.Add(panel);
                }
            }
            else if (control is PictureBox pictureBox)
            {
                // Remove all moving image effects (static images are now in containers)
                controlsToRemove.Add(pictureBox);
            }
            // Keep Labels (notifications) as they expire automatically
        }
        
        // Remove the controls
        foreach (var control in controlsToRemove)
        {
            _overlayForm.Controls.Remove(control);
            
            // Handle cleanup for different control types
            if (control is Panel panel)
            {
                // If it's a static image container, clean up nested PictureBox
                var nestedPictureBox = panel.Controls.OfType<PictureBox>().FirstOrDefault();
                if (nestedPictureBox != null && nestedPictureBox.Image != null)
                {
                    try
                    {
                        ImageAnimator.StopAnimate(nestedPictureBox.Image, null);
                        nestedPictureBox.Image.Dispose();
                    }
                    catch { } // Ignore disposal errors
                }
            }
            else if (control is PictureBox pb && pb.Image != null)
            {
                // Handle moving images
                try
                {
                    ImageAnimator.StopAnimate(pb.Image, null);
                    pb.Image.Dispose();
                }
                catch { } // Ignore disposal errors
            }
            
            control.Dispose();
        }
        
        // Clear active elements list (except notifications)
        _activeElements.RemoveAll(e => e.Type == OverlayElementType.ActiveEffect);
        
        Debug.WriteLine($"Cleared {controlsToRemove.Count} effect controls");
    }
    
    public void Dispose()
    {
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        _overlayForm?.Close();
        _overlayForm?.Dispose();
    }
}

public class OverlayElement
{
    public string Text { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public OverlayElementType Type { get; set; }
}

public enum OverlayElementType
{
    ActiveEffect,
    Notification,
    Image,
    Sound
}