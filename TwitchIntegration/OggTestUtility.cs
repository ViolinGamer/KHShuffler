using System;
using System.Diagnostics;
using System.IO;
using NAudio.Vorbis;
using NAudio.Wave;

namespace BetterGameShuffler.TwitchIntegration;

/// <summary>
/// Simple OGG test utility to verify NAudio.Vorbis functionality
/// </summary>
public static class OggTestUtility
{
    public static void TestOggFile(string filePath)
    {
        Console.WriteLine($"=== Testing OGG file: {Path.GetFileName(filePath)} ===");
        
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"? File not found: {filePath}");
            return;
        }
        
        var fileInfo = new FileInfo(filePath);
        Console.WriteLine($"?? File size: {fileInfo.Length:N0} bytes");
        
        try
        {
            // Test 1: Can we create a VorbisWaveReader?
            Console.WriteLine("?? Testing VorbisWaveReader creation...");
            using var vorbisReader = new VorbisWaveReader(filePath);
            
            Console.WriteLine("? VorbisWaveReader created successfully!");
            Console.WriteLine($"   Sample Rate: {vorbisReader.WaveFormat.SampleRate} Hz");
            Console.WriteLine($"   Channels: {vorbisReader.WaveFormat.Channels}");
            Console.WriteLine($"   Bits Per Sample: {vorbisReader.WaveFormat.BitsPerSample}");
            Console.WriteLine($"   Total Length: {vorbisReader.Length:N0} bytes");
            Console.WriteLine($"   Duration: {vorbisReader.TotalTime}");
            
            // Test 2: Can we initialize a WaveOut player?
            Console.WriteLine("?? Testing WaveOut initialization...");
            using var wavePlayer = new WaveOutEvent();
            wavePlayer.Init(vorbisReader);
            
            Console.WriteLine("? WaveOut initialized successfully!");
            
            // Test 3: Can we start playback (but stop immediately)?
            Console.WriteLine("?? Testing playback start/stop...");
            wavePlayer.Play();
            System.Threading.Thread.Sleep(100); // Let it start
            wavePlayer.Stop();
            
            Console.WriteLine("? Playback test successful!");
            Console.WriteLine($"? OGG file '{Path.GetFileName(filePath)}' is fully compatible!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? OGG test failed: {ex.Message}");
            Console.WriteLine($"   Exception type: {ex.GetType().Name}");
            Console.WriteLine($"   Full details: {ex}");
        }
        
        Console.WriteLine();
    }
    
    public static void TestAllOggFiles(string directory = "sounds")
    {
        Console.WriteLine($"=== Testing all OGG files in '{directory}' directory ===");
        Console.WriteLine();
        
        if (!Directory.Exists(directory))
        {
            Console.WriteLine($"? Directory '{directory}' does not exist");
            return;
        }
        
        var oggFiles = Directory.GetFiles(directory, "*.ogg");
        Console.WriteLine($"?? Found {oggFiles.Length} OGG files");
        Console.WriteLine();
        
        if (oggFiles.Length == 0)
        {
            Console.WriteLine("No OGG files found to test");
            return;
        }
        
        int successCount = 0;
        int failCount = 0;
        
        foreach (var oggFile in oggFiles)
        {
            try
            {
                TestOggFile(oggFile);
                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Critical error testing {Path.GetFileName(oggFile)}: {ex.Message}");
                failCount++;
            }
        }
        
        Console.WriteLine($"=== Test Summary ===");
        Console.WriteLine($"? Successful: {successCount}");
        Console.WriteLine($"? Failed: {failCount}");
        Console.WriteLine($"?? Success Rate: {(successCount * 100.0 / (successCount + failCount)):F1}%");
    }
}