using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Sounds.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.IO;

namespace Ironwall.Dotnet.Libraries.Sounds.Helpers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/10/2025 8:25:51 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public static class SoundHelper
{
    /// <summary>
    /// 지정된 디렉토리에서 사운드 파일을 모두 가져오는 메서드
    /// </summary>
    public async static Task<List<string>> GetSoundFilesAsync(string directoryUri, ILogService? log = default)
    {
        try
        {
            if (string.IsNullOrEmpty(directoryUri) || !Directory.Exists(directoryUri))
            {
                log?.Warning($"Sound directory not found: {directoryUri}");
                return new List<string>();
            }

            var supportedExtensions = new[] { ".wav", ".mp3", ".aiff", ".flac", ".wma" };

            var soundFiles = await Task.Run(() =>
            {
                return Directory.GetFiles(directoryUri, "*.*", SearchOption.AllDirectories)
                    .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToList();
            });

            log?.Info($"Found {soundFiles.Count} sound files in {directoryUri}");
            return soundFiles;
        }
        catch (Exception ex)
        {
            log?.Error($"Error getting sound files from {directoryUri}: {ex.Message}");
            return new List<string>();
        }
    }

    /// <summary>
    /// 사운드 파일명에서 EventType 추정
    /// </summary>
    public static EnumEventType DetermineEventType(string fileName)
    {
        var lowerFileName = fileName.ToLowerInvariant();

        if (lowerFileName.Contains("intrusion") || lowerFileName.Contains("detection") || lowerFileName.Contains("탐지"))
            return EnumEventType.Intrusion;
        if (lowerFileName.Contains("fault") || lowerFileName.Contains("malfunction") || lowerFileName.Contains("장애"))
            return EnumEventType.Fault;
        if (lowerFileName.Contains("action") || lowerFileName.Contains("report") || lowerFileName.Contains("조치"))
            return EnumEventType.Action;

        return EnumEventType.Intrusion; // 기본값
    }

    /// <summary>
    /// 사용 가능한 모든 오디오 출력 장치 열거
    /// </summary>
    public static List<IAudioDeviceInfo> GetAvailableAudioDevices(ILogService? log = default)
    {
        var devices = new List<IAudioDeviceInfo>();

        try
        {
            // WaveOut 장치들
            devices.AddRange(SoundHelper.GetWaveOutDevices(log));

            // WASAPI 장치들 (더 상세한 정보 제공)
            devices.AddRange(SoundHelper.GetWasapiDevices(log));

            log?.Info($"Found {devices.Count} audio output devices");
        }
        catch (Exception ex)
        {
            log?.Error($"Error enumerating audio devices: {ex.Message}");
        }

        return devices;
    }



    /// <summary>
    /// WaveOut 장치 목록 가져오기
    /// </summary>
    public static List<IAudioDeviceInfo> GetWaveOutDevices(ILogService? log=default)
    {
        var devices = new List<IAudioDeviceInfo>();

        try
        {
            // -1은 Audio Mapper (기본 장치)
            for (int n = -1; n < WaveOut.DeviceCount; n++)
            {
                try
                {
                    var caps = WaveOut.GetCapabilities(n);
                    devices.Add(new AudioDeviceInfo
                    {
                        DeviceNumber = n,
                        Name = caps.ProductName,
                        Description = n == -1 ? "Audio Mapper (Default)" : $"WaveOut Device {n}",
                        DeviceType = EnumAudioType.WaveOut,
                        IsDefault = n == -1,
                        IsEnabled = true,
                        NativeDevice = n
                    });
                }
                catch (Exception ex)
                {
                    log?.Warning($"Could not get capabilities for WaveOut device {n}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            log?.Error($"Error enumerating WaveOut devices: {ex.Message}");
        }

        return devices;
    }

    /// <summary>
    /// WASAPI 장치 목록 가져오기
    /// </summary>
    public static List<IAudioDeviceInfo> GetWasapiDevices(ILogService? log = default)
    {
        var devices = new List<IAudioDeviceInfo>();

        try
        {
            using var enumerator = new MMDeviceEnumerator();

            // 기본 장치 가져오기
            MMDevice? defaultDevice = null;
            try
            {
                defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch (Exception ex)
            {
                log?.Warning($"Could not get default WASAPI device: {ex.Message}");
            }

            // 모든 활성 렌더링 장치 열거
            foreach (var wasapiDevice in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                try
                {
                    var isDefault = defaultDevice?.ID == wasapiDevice.ID;

                    devices.Add(new AudioDeviceInfo
                    {
                        DeviceNumber = devices.Count, // WASAPI는 인덱스 대신 MMDevice 객체 사용
                        Name = wasapiDevice.FriendlyName,
                        Description = $"WASAPI: {wasapiDevice.DeviceFriendlyName}{(isDefault ? " (Default)" : "")}",
                        DeviceType = EnumAudioType.WASAPI,
                        IsDefault = isDefault,
                        IsEnabled = wasapiDevice.State == DeviceState.Active,
                        NativeDevice = wasapiDevice
                    });
                }
                catch (Exception ex)
                {
                    log?.Warning($"Error processing WASAPI device {wasapiDevice.FriendlyName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            log?.Error($"Error enumerating WASAPI devices: {ex.Message}");
        }

        return devices;
    }
}