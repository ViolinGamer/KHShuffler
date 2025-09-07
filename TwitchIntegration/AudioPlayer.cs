using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Vorbis;

namespace BetterGameShuffler.TwitchIntegration;

/// <summary>
/// Enhanced audio player that supports WAV, MP3, and OGG Vorbis formats
/// Also provides helpful guidance for OPUS files
/// </summary>
public class AudioPlayer : IDisposable
{
    private IWavePlayer? _wavePlayer;
    private AudioFileReader? _audioFile;
    private VorbisWaveReader? _vorbisReader;
    private bool _disposed = false;
    private static bool _opusWarningShown = false;

    /// <summary>
    /// Plays an audio file asynchronously. Supports WAV, MP3, and OGG Vorbis formats.
    /// </summary>
    /// <param name="filePath">Path to the audio file</param>
    /// <returns>Task that completes when playback starts (not when it finishes)</returns>
    public async Task PlayAsync(string filePath)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AudioPlayer));

        if (!File.Exists(filePath))
        {
            Debug.WriteLine($"Audio file not found: {filePath}");
            return;
        }

        try
        {
            // Stop any currently playing audio
            Stop();

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            Debug.WriteLine($"AudioPlayer: Playing audio file: {Path.GetFileName(filePath)} ({extension})");

            switch (extension)
            {
                case ".wav":
                    await PlayWavFile(filePath);
                    break;
                
                case ".mp3":
                    await PlayMp3File(filePath);
                    break;
                
                case ".ogg":
                    await PlayOggFile(filePath);
                    break;
                
                default:
                    Debug.WriteLine($"AudioPlayer: Unsupported audio format: {extension}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AudioPlayer: Failed to play audio file {filePath}: {ex.Message}");
            Debug.WriteLine($"AudioPlayer: Exception details: {ex}");
        }
    }

    /// <summary>
    /// Plays a WAV file using the built-in SoundPlayer for best compatibility
    /// </summary>
    private async Task PlayWavFile(string filePath)
    {
        await Task.Run(() =>
        {
            try
            {
                using var player = new SoundPlayer(filePath);
                player.Play(); // Non-blocking play
                Debug.WriteLine($"AudioPlayer: Successfully started WAV playback: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioPlayer: WAV playback failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Plays an MP3 file using NAudio
    /// </summary>
    private async Task PlayMp3File(string filePath)
    {
        await Task.Run(() =>
        {
            try
            {
                _wavePlayer = new WaveOutEvent();
                _audioFile = new AudioFileReader(filePath);
                
                _wavePlayer.PlaybackStopped += (sender, e) =>
                {
                    Debug.WriteLine($"AudioPlayer: MP3 playback finished: {Path.GetFileName(filePath)}");
                    if (e.Exception != null)
                    {
                        Debug.WriteLine($"AudioPlayer: MP3 playback error: {e.Exception.Message}");
                    }
                };

                _wavePlayer.Init(_audioFile);
                _wavePlayer.Play();
                
                Debug.WriteLine($"AudioPlayer: Successfully started MP3 playback: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioPlayer: MP3 playback failed: {ex.Message}");
                DisposeCurrentPlayback();
            }
        });
    }

    /// <summary>
    /// Plays an OGG file (supports OGG Vorbis, provides guidance for OPUS)
    /// </summary>
    private async Task PlayOggFile(string filePath)
    {
        await Task.Run(() =>
        {
            try
            {
                Debug.WriteLine($"AudioPlayer: Attempting to load OGG file: {Path.GetFileName(filePath)}");
                Debug.WriteLine($"AudioPlayer: File size: {new FileInfo(filePath).Length} bytes");
                
                _wavePlayer = new WaveOutEvent();
                _vorbisReader = new VorbisWaveReader(filePath);
                
                Debug.WriteLine($"AudioPlayer: OGG Vorbis file loaded successfully:");
                Debug.WriteLine($"  Sample Rate: {_vorbisReader.WaveFormat.SampleRate} Hz");
                Debug.WriteLine($"  Channels: {_vorbisReader.WaveFormat.Channels}");
                Debug.WriteLine($"  Bits Per Sample: {_vorbisReader.WaveFormat.BitsPerSample}");
                Debug.WriteLine($"  Total Length: {_vorbisReader.Length} bytes");
                Debug.WriteLine($"  Duration: {_vorbisReader.TotalTime}");
                
                _wavePlayer.PlaybackStopped += (sender, e) =>
                {
                    Debug.WriteLine($"AudioPlayer: OGG Vorbis playback finished: {Path.GetFileName(filePath)}");
                    if (e.Exception != null)
                    {
                        Debug.WriteLine($"AudioPlayer: OGG Vorbis playback error: {e.Exception.Message}");
                    }
                };

                _wavePlayer.Init(_vorbisReader);
                _wavePlayer.Play();
                
                Debug.WriteLine($"AudioPlayer: Successfully started OGG Vorbis playback: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioPlayer: OGG playback failed: {ex.Message}");
                
                // Check if it's an OPUS file and provide helpful guidance
                if (ex.Message.Contains("OPUS") || ex.Message.Contains("Could not initialize container"))
                {
                    HandleOpusFile(filePath);
                }
                else
                {
                    Debug.WriteLine($"AudioPlayer: OGG error details: {ex}");
                }
                
                DisposeCurrentPlayback();
            }
        });
    }

    /// <summary>
    /// Handles OPUS files by providing helpful information and conversion guidance
    /// </summary>
    private static void HandleOpusFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        Debug.WriteLine($"");
        Debug.WriteLine($"========================================");
        Debug.WriteLine($"OPUS FILE DETECTED: {fileName}");
        Debug.WriteLine($"========================================");
        Debug.WriteLine($"");
        Debug.WriteLine($"? This file uses OPUS codec (not OGG Vorbis)");
        Debug.WriteLine($"? NAudio.Vorbis only supports OGG Vorbis, not OPUS");
        Debug.WriteLine($"");
        Debug.WriteLine($"?? SOLUTION: Convert OPUS files to supported formats");
        Debug.WriteLine($"");
        Debug.WriteLine($"?? RECOMMENDED CONVERSION COMMANDS:");
        Debug.WriteLine($"");
        Debug.WriteLine($"   To OGG Vorbis (best compatibility):");
        Debug.WriteLine($"   ffmpeg -i \"{fileName}\" -c:a libvorbis \"{Path.GetFileNameWithoutExtension(fileName)}_vorbis.ogg\"");
        Debug.WriteLine($"");
        Debug.WriteLine($"   To MP3 (universal support):");
        Debug.WriteLine($"   ffmpeg -i \"{fileName}\" -c:a libmp3lame -b:a 192k \"{Path.GetFileNameWithoutExtension(fileName)}.mp3\"");
        Debug.WriteLine($"");
        Debug.WriteLine($"?? GET FFMPEG:");
        Debug.WriteLine($"   Download from: https://ffmpeg.org/download.html");
        Debug.WriteLine($"   Or use online converters (search 'OPUS to OGG converter')");
        Debug.WriteLine($"");
        Debug.WriteLine($"?? AFTER CONVERSION:");
        Debug.WriteLine($"   1. Place converted files in the sounds folder");
        Debug.WriteLine($"   2. Remove or rename original OPUS files (optional)");
        Debug.WriteLine($"   3. Test sound effects again");
        Debug.WriteLine($"");
        Debug.WriteLine($"========================================");
        Debug.WriteLine($"");
        
        // Show this information only once per session to avoid spam
        if (!_opusWarningShown)
        {
            _opusWarningShown = true;
            Debug.WriteLine($"?? TIP: This guidance is shown once per session");
            Debug.WriteLine($"?? Check the Debug Output for conversion commands for each OPUS file");
        }
    }

    /// <summary>
    /// Stops any currently playing audio
    /// </summary>
    public void Stop()
    {
        try
        {
            _wavePlayer?.Stop();
            DisposeCurrentPlayback();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AudioPlayer: Error stopping audio playback: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the list of supported audio file extensions
    /// </summary>
    public static string[] GetSupportedExtensions()
    {
        return new[] { ".wav", ".mp3", ".ogg" };
    }

    /// <summary>
    /// Checks if a file extension is supported
    /// </summary>
    /// <param name="extension">File extension including the dot (e.g., ".ogg")</param>
    /// <returns>True if the extension is supported</returns>
    public static bool IsSupportedFormat(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return false;
            
        extension = extension.ToLowerInvariant();
        return extension == ".wav" || extension == ".mp3" || extension == ".ogg";
    }

    /// <summary>
    /// Checks if a file path has a supported audio format
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>True if the file has a supported audio format</returns>
    public static bool IsSupportedFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;
            
        var extension = Path.GetExtension(filePath);
        return IsSupportedFormat(extension);
    }

    private void DisposeCurrentPlayback()
    {
        try
        {
            _audioFile?.Dispose();
            _audioFile = null;
            
            _vorbisReader?.Dispose();
            _vorbisReader = null;
            
            _wavePlayer?.Dispose();
            _wavePlayer = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AudioPlayer: Error disposing audio resources: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Debug.WriteLine("AudioPlayer: Disposing...");
        Stop();
        _disposed = true;
        
        Debug.WriteLine("AudioPlayer: Disposed successfully");
    }
}