using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BetterGameShuffler.TwitchIntegration;

public static class ImageLoader
{
    /// <summary>
    /// Loads an image from file with support for PNG, JPG, GIF, and WebP formats (including animated WebP)
    /// </summary>
    public static Image? LoadImage(string imagePath)
    {
        try
        {
            var extension = Path.GetExtension(imagePath).ToLowerInvariant();
            
            // For WebP files, try to load using enhanced WebP support
            if (extension == ".webp")
            {
                return LoadWebPImage(imagePath);
            }
            
            // For other formats, use standard Image.FromFile
            return Image.FromFile(imagePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load image {imagePath}: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Enhanced WebP loading with animation support for Windows 10+
    /// </summary>
    private static Image? LoadWebPImage(string imagePath)
    {
        try
        {
            // Try to load as standard image first (works for both static and animated WebP on Win10+)
            var image = Image.FromFile(imagePath);
            
            // Check if it's an animated WebP by looking at frame count
            if (IsAnimatedWebP(image))
            {
                System.Diagnostics.Debug.WriteLine($"Animated WebP loaded: {Path.GetFileName(imagePath)} ({image.GetFrameCount(FrameDimension.Time)} frames)");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Static WebP loaded: {Path.GetFileName(imagePath)}");
            }
            
            return image;
        }
        catch (OutOfMemoryException ex)
        {
            // WebP file is too large or format issue
            System.Diagnostics.Debug.WriteLine($"WebP file too large or corrupted: {Path.GetFileName(imagePath)} - {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            // WebP not supported by system codecs or other error
            System.Diagnostics.Debug.WriteLine($"WebP format not supported on this system for file: {Path.GetFileName(imagePath)} - {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Checks if an image is an animated WebP
    /// </summary>
    public static bool IsAnimatedWebP(Image image)
    {
        try
        {
            // Check if image has time-based frames (animation)
            var frameCount = image.GetFrameCount(FrameDimension.Time);
            return frameCount > 1;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Checks if a file is an animated image format
    /// </summary>
    public static bool IsAnimatedFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension == ".gif")
            return true;
            
        if (extension == ".webp")
        {
            try
            {
                using var image = Image.FromFile(filePath);
                return IsAnimatedWebP(image);
            }
            catch
            {
                return false;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Gets all supported image files from a directory
    /// </summary>
    public static string[] GetSupportedImageFiles(string directory, string searchPattern = "*")
    {
        if (!Directory.Exists(directory))
            return Array.Empty<string>();
            
        var supportedExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        var allFiles = new List<string>();
        
        foreach (var extension in supportedExtensions)
        {
            try
            {
                var files = Directory.GetFiles(directory, $"*{extension}", SearchOption.TopDirectoryOnly);
                allFiles.AddRange(files);
            }
            catch
            {
                // Continue with other extensions if one fails
            }
        }
        
        return allFiles.ToArray();
    }
    
    /// <summary>
    /// Checks if a file is a supported image format
    /// </summary>
    public static bool IsSupportedImageFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || 
               extension == ".gif" || extension == ".webp";
    }
    
    /// <summary>
    /// Gets a user-friendly list of supported formats
    /// </summary>
    public static string GetSupportedFormatsString()
    {
        return "PNG, JPG, GIF, WebP (including animated)";
    }
    
    /// <summary>
    /// Gets detailed format information for a specific file
    /// </summary>
    public static string GetImageFormatInfo(string filePath)
    {
        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            if (extension == ".webp")
            {
                using var image = Image.FromFile(filePath);
                var isAnimated = IsAnimatedWebP(image);
                var frameCount = isAnimated ? image.GetFrameCount(FrameDimension.Time) : 1;
                return $"WebP ({(isAnimated ? $"animated, {frameCount} frames" : "static")})";
            }
            else if (extension == ".gif")
            {
                using var image = Image.FromFile(filePath);
                var frameCount = image.GetFrameCount(FrameDimension.Time);
                return $"GIF ({(frameCount > 1 ? $"animated, {frameCount} frames" : "static")})";
            }
            else
            {
                return extension.TrimStart('.').ToUpperInvariant();
            }
        }
        catch
        {
            return "Unknown";
        }
    }
}