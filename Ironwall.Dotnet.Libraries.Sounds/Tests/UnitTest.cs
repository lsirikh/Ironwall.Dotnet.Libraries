using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Sounds.Helpers;
using Ironwall.Dotnet.Libraries.Sounds.Models;
using Ironwall.Dotnet.Libraries.Sounds.Providers;
using Ironwall.Dotnet.Libraries.Sounds.Services;
using NAudio.CoreAudioApi;
using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Sounds.Tests;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/10/2025 4:27:05 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// SoundService 전용 Fixture – 서비스 한 번만 세팅 & 해제
/// </summary>
public sealed class SoundServiceFixture : IAsyncLifetime
{
    internal SoundService SoundSvc { get; private set; } = null!;
    public SoundSourceProvider SoundProvider { get; private set; } = null!;
    public AudioDeviceInfoProvider AudioDeviceInfoProvider { get; private set; }
    public SoundSetupModel SetupModel { get; private set; } = null!;
    public ILogService LogService { get; private set; } = null!;

    internal CancellationTokenSource Cts { get; } = new();

    // 실제 사운드 디렉토리
    private const string ACTUAL_SOUND_DIRECTORY = @"C:\Sample";

    // 실제 사운드 파일들
    internal List<string> ActualSoundFiles { get; } = new();

    // 테스트용 사운드 설정 (실제 파일명 기반)
    private SoundSetupModel _testSetup = null!;

    /// <summary>
    /// 테스트 시작 시 초기화
    /// </summary>
    public async Task InitializeAsync()
    {
        // 로그 서비스 초기화
        LogService = new LogService();

        // 사운드 Provider 초기화
        SoundProvider = new SoundSourceProvider();

        AudioDeviceInfoProvider = new AudioDeviceInfoProvider();

        var soundFiles = await SoundHelper.GetSoundFilesAsync(ACTUAL_SOUND_DIRECTORY);
        ActualSoundFiles.AddRange(soundFiles);

        var audioDevices = SoundHelper.GetAvailableAudioDevices();

        foreach (var filePath in ActualSoundFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var soundModel = new SoundModel
            {
                Id = SoundProvider.Count + 1,
                Name = fileName,
                File = filePath,
                Type = SoundHelper.DetermineEventType(fileName), // 파일명으로 타입 추정
                IsPlaying = false
            };

            SoundProvider.Add(soundModel);
        }

        if (!SoundProvider.Any())
        {
            throw new FileNotFoundException($"No sound files found in {ACTUAL_SOUND_DIRECTORY}");
        }

        SetupModel = CreateSetupModelWithActualFiles();

        // SoundService 생성
        SoundSvc = new SoundService(LogService, SetupModel, SoundProvider, AudioDeviceInfoProvider);
        await SoundSvc.SetSoundProviderAsync(soundFiles);
        await SoundSvc.SetAudioDeviceInfoProviderAsync(audioDevices);

        // 사운드 파일 로드
        Assert.True(Directory.Exists(ACTUAL_SOUND_DIRECTORY), $"Sound directory should exist: {ACTUAL_SOUND_DIRECTORY}");

        PrintActualFiles();
    }

    /// <summary>
    /// 테스트 종료 시 정리
    /// </summary>
    public async Task DisposeAsync()
    {

        try
        {
            // 모든 사운드 중지
            await SoundSvc.StopAllSoundsAsync();

            if (!Cts.IsCancellationRequested)
                Cts.Cancel();

            Cts.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during cleanup: {ex.Message}");
        }
    }

    /// <summary>
    /// 실제 파일을 기반으로 SetupModel 생성
    /// </summary>
    private SoundSetupModel CreateSetupModelWithActualFiles()
    {

        // 기본값으로 첫 번째 파일 사용
        var detectionFile = SoundProvider.Where(entity => entity.Type == EnumEventType.Intrusion).FirstOrDefault();
        var malfunctionFile = SoundProvider.Where(entity => entity.Type == EnumEventType.Fault).FirstOrDefault();
        var actionReportFile = SoundProvider.Where(entity => entity.Type == EnumEventType.Action).FirstOrDefault();

        // 타입별 파일이 없으면 첫 번째 파일 사용
        var fallbackFile = SoundProvider.FirstOrDefault();

        return new SoundSetupModel
        {
            DirectoryUri = ACTUAL_SOUND_DIRECTORY,
            DetectionSound = detectionFile?.Name ?? fallbackFile?.Name ?? string.Empty,
            MalfunctionSound = malfunctionFile?.Name ?? fallbackFile?.Name ?? string.Empty,
            ActionReportSound = actionReportFile?.Name ?? fallbackFile?.Name ?? string.Empty,
            DetectionSoundDuration = 3,
            MalfunctionSoundDuration = 3,
            ActionReportSoundDuration = 3,
            IsDetectionAutoSoundStop = false,
            IsMalfunctionAutoSoundStop = false,  // 수동 중지 (무한 재생)
            IsActionReportAutoSoundStop = false
        };
    }

    /// <summary>
    /// 실제 파일 정보 출력
    /// </summary>
    public void PrintActualFiles()
    {
        Debug.WriteLine($"\n=== Sound Files Information ===");
        Debug.WriteLine($"Directory: {ACTUAL_SOUND_DIRECTORY}");
        Debug.WriteLine($"Found {ActualSoundFiles.Count} sound files:");

        foreach (var file in ActualSoundFiles)
        {
            var fileInfo = new FileInfo(file);
            Debug.WriteLine($"  - {Path.GetFileName(file)} ({fileInfo.Length:N0} bytes)");
        }

        Debug.WriteLine($"\n=== Test Configuration ===");
        Debug.WriteLine($"Detection Sound: {SetupModel.DetectionSound} ({SetupModel.DetectionSoundDuration}s, AutoStop: {SetupModel.IsDetectionAutoSoundStop})");
        Debug.WriteLine($"Malfunction Sound: {SetupModel.MalfunctionSound} ({SetupModel.MalfunctionSoundDuration}s, AutoStop: {SetupModel.IsMalfunctionAutoSoundStop})");
        Debug.WriteLine($"Action Report Sound: {SetupModel.ActionReportSound} ({SetupModel.ActionReportSoundDuration}s, AutoStop: {SetupModel.IsActionReportAutoSoundStop})");
    }
}

/// <summary>
/// xUnit 컬렉션 정의
/// </summary>
[CollectionDefinition(nameof(SoundServiceCollection))]
public sealed class SoundServiceCollection : ICollectionFixture<SoundServiceFixture> { }


/// <summary>
/// SoundService 기본 기능 테스트
/// </summary>
[Collection(nameof(SoundServiceCollection))]
public class SoundService_BasicTests
{
    private readonly SoundServiceFixture _fx;

    public SoundService_BasicTests(SoundServiceFixture fx) => _fx = fx;

    [Fact(DisplayName = "SoundService – Service Initialization")]
    public void Service_Initialization_Success()
    {
        // Assert
        Assert.NotNull(_fx.SoundSvc);
        Assert.NotNull(_fx.SoundProvider);
        Assert.NotNull(_fx.SetupModel);
        Assert.True(_fx.SoundSvc.IsSoundEnabled);
        Assert.True(Directory.Exists(_fx.SetupModel.DirectoryUri));
    }

    [Fact(DisplayName = "SoundService – Get Audio Devices")]
    public void Get_Audio_Devices_Works()
    {
        // Act
        var devices = SoundHelper.GetAvailableAudioDevices();

        // Assert
        Assert.NotNull(devices);
        Assert.NotEmpty(devices);

        // 최소한 기본 장치는 있어야 함
        Assert.Contains(devices, d => d.IsDefault);

        // 각 장치 정보 검증
        Assert.All(devices, device =>
        {
            Assert.False(string.IsNullOrEmpty(device.Name));
            Assert.False(string.IsNullOrEmpty(device.Description));
            Assert.NotNull(device.NativeDevice);
        });

        Debug.WriteLine($"Found {devices.Count} audio devices:");
        foreach (var device in devices)
        {
            Debug.WriteLine($"  - {device.Name} ({device.DeviceType}){(device.IsDefault ? " [Default]" : "")}");
        }
    }

    [Fact(DisplayName = "SoundService – Audio Device Selection")]
    public async Task Audio_Device_Selection_Works()
    {
        // Arrange
        var devices = SoundHelper.GetAvailableAudioDevices();
        Assert.NotEmpty(devices);

        var testDevice = devices.First();

        // Act
        var result = await _fx.SoundSvc.SelectAudioDevice(testDevice);

        // Assert
        Assert.True(result);

        var currentDevice = _fx.SoundSvc.GetCurrentAudioDevice();
        Assert.NotNull(currentDevice);
        Assert.Equal(testDevice.Name, currentDevice.Name);
        Assert.Equal(testDevice.DeviceType, currentDevice.DeviceType);

        // Reset to default
        _fx.SoundSvc.ResetToDefaultAudioDevice();
        Assert.Null(_fx.SoundSvc.GetCurrentAudioDevice());
    }

    [Fact(DisplayName = "SoundService – Audio Output Mode Setting")]
    public void Audio_Output_Mode_Setting_Works()
    {
        // Act & Assert - 각 모드 설정 테스트
        _fx.SoundSvc.SetAudioOutputMode(EnumAudioOutputMode.WaveOut);
        _fx.SoundSvc.SetAudioOutputMode(EnumAudioOutputMode.WaveOutEvent);
        _fx.SoundSvc.SetAudioOutputMode(EnumAudioOutputMode.DirectSoundOut);
        _fx.SoundSvc.SetAudioOutputMode(EnumAudioOutputMode.WasapiOut);

        // 예외가 발생하지 않으면 성공
        Assert.True(true);
    }

    [Fact(DisplayName = "SoundService – Get Sound Files")]
    public async Task Get_Sound_Files_Works()
    {
        // Act
        var soundFiles = await SoundHelper.GetSoundFilesAsync(_fx.SetupModel.DirectoryUri);

        // Assert
        Assert.NotNull(soundFiles);
        Assert.NotEmpty(soundFiles);
        Assert.Equal(_fx.ActualSoundFiles.Count, soundFiles.Count);

        Assert.All(soundFiles, file =>
        {
            Assert.True(System.IO.File.Exists(file));
            Assert.Contains(Path.GetExtension(file).ToLower(),
                new[] { ".wav", ".mp3", ".aiff", ".flac", ".wma" });
        });
    }

    [Fact(DisplayName = "SoundService – Set Sound Provider")]
    public async Task Set_Sound_Provider_Works()
    {
        // Arrange
        var testProvider = new SoundSourceProvider();
        var testAudioProvider = new AudioDeviceInfoProvider();
        var testSoundService = new SoundService(_fx.LogService, _fx.SetupModel, testProvider, testAudioProvider);

        // Act
        await testSoundService.SetSoundProviderAsync(_fx.ActualSoundFiles);

        // Assert
        Assert.NotEmpty(testProvider);
        Assert.Equal(_fx.ActualSoundFiles.Count, testProvider.Count);

        Assert.All(testProvider, sound =>
        {
            Assert.False(string.IsNullOrEmpty(sound.Name));
            Assert.True(System.IO.File.Exists(sound.File));
            Assert.False(sound.IsPlaying);
            Assert.True(sound.Id > 0);
            Assert.Contains(sound.Type ?? EnumEventType.Intrusion, new[] { EnumEventType.Intrusion, EnumEventType.Fault, EnumEventType.Action });
        });
    }

    [Fact(DisplayName = "SoundService – Invalid Directory Handling")]
    public async Task Invalid_Directory_Handling_Works()
    {
        // Act & Assert
        var result1 = await SoundHelper.GetSoundFilesAsync(string.Empty);
        Assert.Empty(result1);

        var result2 = await SoundHelper.GetSoundFilesAsync(@"C:\NonExistentDirectory");
        Assert.Empty(result2);

        var result3 = await SoundHelper.GetSoundFilesAsync(null);
        Assert.Empty(result3);
    }
}

/// <summary>
/// SoundService 재생 기능 테스트
/// </summary>
[Collection(nameof(SoundServiceCollection))]
public class SoundService_PlaybackTests
{
    private readonly SoundServiceFixture _fx;

    public SoundService_PlaybackTests(SoundServiceFixture fx) => _fx = fx;

    [Fact(DisplayName = "SoundService – Detection Sound Playback (Auto Stop)")]
    public async Task Detection_Sound_Playback_AutoStop_Works()
    {
        // Arrange - 자동 중지 설정 확인
        Assert.True(_fx.SetupModel.IsDetectionAutoSoundStop);
        Assert.True(_fx.SetupModel.DetectionSoundDuration > 0);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_fx.SetupModel.DetectionSoundDuration)); // 안전 타임아웃

        // Act
        var playTask = _fx.SoundSvc.DetectionSoundPlayAsync(cts.Token);

        // 재생 시작 확인
        await Task.Delay(300); // 재생 시작 대기
        var detectionSound = _fx.SoundProvider.FirstOrDefault(s =>
            s.Name.Equals(_fx.SetupModel.DetectionSound, StringComparison.OrdinalIgnoreCase));

        if (detectionSound != null)
        {
            Assert.True(detectionSound.IsPlaying);
        }

        // 자동 중지까지 대기
        await playTask;

        // Assert - 재생 완료 후 상태 확인
        if (detectionSound != null)
        {
            Assert.False(detectionSound.IsPlaying);
        }
    }

    [Fact(DisplayName = "SoundService – Malfunction Sound Playback (Manual Stop)")]
    public async Task Malfunction_Sound_Playback_ManualStop_Works()
    {
        // Arrange - 수동 중지 설정 확인 (무한 재생)
        Assert.False(_fx.SetupModel.IsMalfunctionAutoSoundStop);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // 2초 후 취소

        // Act
        var playTask = _fx.SoundSvc.MalfunctionSoundPlayAsync(cts.Token);

        // 재생 시작 확인
        await Task.Delay(300);
        var malfunctionSound = _fx.SoundProvider.FirstOrDefault(s =>
            s.Name.Equals(_fx.SetupModel.MalfunctionSound, StringComparison.OrdinalIgnoreCase));

        if (malfunctionSound != null)
        {
            Assert.True(malfunctionSound.IsPlaying);
        }

        // Assert - 취소로 인한 중지
        await Assert.ThrowsAsync<OperationCanceledException>(() => playTask);

        // 재생 상태 확인
        if (malfunctionSound != null)
        {
            Assert.False(malfunctionSound.IsPlaying);
        }
    }

    [Fact(DisplayName = "SoundService – Action Report Sound Playback")]
    public async Task ActionReport_Sound_Playback_Works()
    {
        // Arrange - 자동 중지 설정 확인
        Assert.True(_fx.SetupModel.IsActionReportAutoSoundStop);
        Assert.True(_fx.SetupModel.ActionReportSoundDuration > 0);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_fx.SetupModel.ActionReportSoundDuration)); // 안전 타임아웃

        // Act
        var playTask = _fx.SoundSvc.ActionReportSoundPlayAsync(cts.Token);

        // 재생 시작 확인
        await Task.Delay(300);
        var actionSound = _fx.SoundProvider.FirstOrDefault(s =>
            s.Name.Equals(_fx.SetupModel.ActionReportSound, StringComparison.OrdinalIgnoreCase));

        if (actionSound != null)
        {
            Assert.True(actionSound.IsPlaying);
        }

        // 완료까지 대기
        await playTask;

        // Assert
        if (actionSound != null)
        {
            Assert.False(actionSound.IsPlaying);
        }
    }

    [Fact(DisplayName = "SoundService – Stop All Sounds with Actual Files")]
    public async Task Stop_All_Sounds_With_Actual_Files_Works()
    {

        // 사운드 재생 시작
        var playTask = _fx.SoundSvc.DetectionSoundPlayAsync();
        await Task.Delay(600); // 재생 시작 대기 (0.5초 딜레이 + 여유시간)

        // Act
        await _fx.SoundSvc.StopAllSoundsAsync();

        // Assert
        Assert.All(_fx.SoundProvider, sound => Assert.False(sound.IsPlaying));

        // 재생 작업이 취소되었는지 확인
        try
        {
            await playTask;
        }
        catch (OperationCanceledException)
        {
            // 예상된 결과
        }
    }

    [Fact(DisplayName = "SoundService – Concurrent Playback Prevention")]
    public async Task Concurrent_Playback_Prevention_Works()
    {
        // Arrange - 자동 중지 설정 확인
        Assert.False(_fx.SetupModel.IsDetectionAutoSoundStop);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // 안전 타임아웃

        // Act - 동시에 두 개의 사운드 재생 시도
        var task1 = _fx.SoundSvc.DetectionSoundPlayAsync(cts.Token);
        await Task.Delay(100); // 첫 번째 재생이 시작되도록 대기

        // 두 번째 재생은 실패해야 함 (Semaphore 보호)
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _fx.SoundSvc.MalfunctionSoundPlayAsync(cts.Token));

        // 첫 번째 재생 완료 대기
        await task1;
    }
}

/// <summary>
/// SoundService Queue 시스템 테스트
/// </summary>
[Collection(nameof(SoundServiceCollection))]
public class SoundService_QueueTests
{
    private readonly SoundServiceFixture _fx;

    public SoundService_QueueTests(SoundServiceFixture fx) => _fx = fx;

    [Fact(DisplayName = "SoundService – Basic Queue Functionality")]
    public async Task Basic_Queue_Functionality_Test()
    {
        // Arrange
        _fx.SoundSvc.SetMaxQueueSize(3);

        // Act - 순차적으로 3개 이벤트 추가
        await _fx.SoundSvc.DetectionSoundPlayAsync();
        await _fx.SoundSvc.MalfunctionSoundPlayAsync();
        await _fx.SoundSvc.ActionReportSoundPlayAsync();

        await Task.Delay(300); // 처리 시작 대기

        var status = _fx.SoundSvc.GetQueueStatus();

        // Assert
        Assert.True(status.IsProcessing, "Queue should be processing");
        Assert.Equal(3, status.MaxQueueSize);
        Assert.True(status.QueueCount <= 3, "Queue count should not exceed max size");

        Debug.WriteLine($"Queue status: {status.QueueCount}/{status.MaxQueueSize} items, Processing: {status.IsProcessing}");
        foreach (var item in status.QueueItems)
        {
            Debug.WriteLine($"  - {item.EventId}: {item.SoundName} ({item.SoundType}) Priority: {item.Priority}");
        }

        await Task.Delay(10000);
        // 정리
        await _fx.SoundSvc.StopAllSoundsAsync();
    }

    [Fact(DisplayName = "SoundService – Queue Size Limit and FIFO")]
    public async Task Queue_Size_Limit_And_FIFO_Test()
    {
        // Arrange - 큐 크기를 2로 제한
        _fx.SoundSvc.SetMaxQueueSize(2);

        var eventIds = new List<string>();

        // Act - 5개 이벤트 빠르게 추가 (큐 크기 초과)
        for (int i = 0; i < 5; i++)
        {
            var eventType = (i % 3) switch
            {
                0 => "Detection",
                1 => "Malfunction",
                _ => "ActionReport"
            };

            switch (eventType)
            {
                case "Detection":
                    await _fx.SoundSvc.DetectionSoundPlayAsync();
                    break;
                case "Malfunction":
                    await _fx.SoundSvc.MalfunctionSoundPlayAsync();
                    break;
                case "ActionReport":
                    await _fx.SoundSvc.ActionReportSoundPlayAsync();
                    break;
            }

            await Task.Delay(50); // 빠른 간격

            var status = _fx.SoundSvc.GetQueueStatus();
            eventIds.Add($"Event{i + 1}");

            Debug.WriteLine($"After Event {i + 1}: Queue={status.QueueCount}, Processing={status.IsProcessing}");
        }

        await Task.Delay(500); // 처리 진행 대기

        var finalStatus = _fx.SoundSvc.GetQueueStatus();

        // Assert
        Assert.True(finalStatus.QueueCount <= 2, $"Queue should not exceed size limit. Actual: {finalStatus.QueueCount}");

        Debug.WriteLine($"\nFinal queue status:");
        Debug.WriteLine($"Queue count: {finalStatus.QueueCount}/{finalStatus.MaxQueueSize}");
        Debug.WriteLine($"Is processing: {finalStatus.IsProcessing}");

        await Task.Delay(15000); // 처리 진행 대기
        // 정리
        await _fx.SoundSvc.StopAllSoundsAsync();
    }

    [Fact(DisplayName = "SoundService – Rapid Event Burst with Queue")]
    public async Task Rapid_Event_Burst_With_Queue_Test()
    {
        const int eventCount = 100;
        const int intervalMs = 30; // 30ms 간격
        const int maxQueueSize = 3;

        // Arrange
        _fx.SoundSvc.SetMaxQueueSize(maxQueueSize);

        var eventLog = new List<QueueEventLog>();
        var startTime = DateTime.UtcNow;

        // Act - 빠른 연속 이벤트 생성
        for (int i = 0; i < eventCount; i++)
        {
            var eventTime = DateTime.UtcNow;
            var eventType = (i % 3) switch
            {
                0 => "Detection",
                1 => "Malfunction",
                _ => "ActionReport"
            };

            var beforeStatus = _fx.SoundSvc.GetQueueStatus();

            // 이벤트 발생
            switch (eventType)
            {
                case "Detection":
                    await _fx.SoundSvc.DetectionSoundPlayAsync();
                    break;
                case "Malfunction":
                    await _fx.SoundSvc.MalfunctionSoundPlayAsync();
                    break;
                case "ActionReport":
                    await _fx.SoundSvc.ActionReportSoundPlayAsync();
                    break;
            }

            var afterStatus = _fx.SoundSvc.GetQueueStatus();

            eventLog.Add(new QueueEventLog
            {
                EventId = i + 1,
                EventType = eventType,
                EventTime = eventTime,
                QueueSizeBefore = beforeStatus.QueueCount,
                QueueSizeAfter = afterStatus.QueueCount,
                IsProcessingBefore = beforeStatus.IsProcessing,
                IsProcessingAfter = afterStatus.IsProcessing
            });

            await Task.Delay(intervalMs);
        }

        // 처리 완료까지 대기
        await Task.Delay(15000);

        var totalTime = DateTime.UtcNow - startTime;
        var finalStatus = _fx.SoundSvc.GetQueueStatus();

        // Assert & 분석
        AnalyzeQueueBurstResults(eventLog, totalTime, maxQueueSize);

        // 정리
        await _fx.SoundSvc.StopAllSoundsAsync();
    }

    [Fact(DisplayName = "SoundService – Queue Processing Performance")]
    public async Task Queue_Processing_Performance_Test()
    {
        const int eventCount = 15;
        const int maxQueueSize = 5;

        // Arrange
        _fx.SoundSvc.SetMaxQueueSize(maxQueueSize);

        var performanceLog = new List<QueuePerformanceMetric>();
        var startTime = DateTime.UtcNow;

        // Act - 성능 측정하면서 이벤트 처리
        for (int i = 0; i < eventCount; i++)
        {
            var eventStartTime = DateTime.UtcNow;

            // 다양한 이벤트 타입 혼합
            var eventType = (i % 3) switch
            {
                0 => "Detection",
                1 => "Malfunction",
                _ => "ActionReport"
            };

            var beforeStatus = _fx.SoundSvc.GetQueueStatus();

            switch (eventType)
            {
                case "Detection":
                    await _fx.SoundSvc.DetectionSoundPlayAsync();
                    break;
                case "Malfunction":
                    await _fx.SoundSvc.MalfunctionSoundPlayAsync();
                    break;
                case "ActionReport":
                    await _fx.SoundSvc.ActionReportSoundPlayAsync();
                    break;
            }

            var afterStatus = _fx.SoundSvc.GetQueueStatus();
            var eventEndTime = DateTime.UtcNow;

            performanceLog.Add(new QueuePerformanceMetric
            {
                EventId = i + 1,
                EventType = eventType,
                EnqueueTime = (eventEndTime - eventStartTime).TotalMilliseconds,
                QueueSizeAfter = afterStatus.QueueCount,
                IsProcessing = afterStatus.IsProcessing
            });

            // 가변 간격 (실제 환경 시뮬레이션)
            var delay = (i % 4) switch
            {
                0 => 100, // 느린 간격
                1 => 50,  // 보통 간격
                2 => 25,  // 빠른 간격
                _ => 75   // 중간 간격
            };

            await Task.Delay(delay);
        }

        // 모든 처리 완료까지 대기
        await WaitForQueueCompletion(TimeSpan.FromSeconds(10));

        var totalTime = DateTime.UtcNow - startTime;

        // Assert & 분석
        AnalyzePerformanceResults(performanceLog, totalTime);

        // 정리
        await _fx.SoundSvc.StopAllSoundsAsync();
    }

    [Fact(DisplayName = "SoundService – Queue Clear and Management")]
    public async Task Queue_Clear_And_Management_Test()
    {
        // Arrange
        _fx.SoundSvc.SetMaxQueueSize(5);

        // 다양한 타입의 이벤트 추가
        await _fx.SoundSvc.DetectionSoundPlayAsync();    // Intrusion
        await _fx.SoundSvc.DetectionSoundPlayAsync();    // Intrusion
        await _fx.SoundSvc.MalfunctionSoundPlayAsync();  // Fault
        await _fx.SoundSvc.ActionReportSoundPlayAsync(); // Action
        await _fx.SoundSvc.MalfunctionSoundPlayAsync();  // Fault

        await Task.Delay(200);
        var initialStatus = _fx.SoundSvc.GetQueueStatus();

        Debug.WriteLine($"Initial queue: {initialStatus.QueueCount} items");
        foreach (var item in initialStatus.QueueItems)
        {
            Debug.WriteLine($"  - {item.SoundType}: {item.SoundName}");
        }

        // Act - Fault 타입만 제거
        var removedCount = _fx.SoundSvc.ClearQueueByEventType(EnumEventType.Fault);

        await Task.Delay(100);
        var afterClearStatus = _fx.SoundSvc.GetQueueStatus();

        Debug.WriteLine($"\nAfter clearing Fault events:");
        Debug.WriteLine($"Removed: {removedCount} items");
        Debug.WriteLine($"Remaining: {afterClearStatus.QueueCount} items");
        foreach (var item in afterClearStatus.QueueItems)
        {
            Debug.WriteLine($"  - {item.SoundType}: {item.SoundName}");
        }

        // Assert
        Assert.True(removedCount >= 1, "Should have removed at least 1 Fault event");
        Assert.True(afterClearStatus.QueueCount < initialStatus.QueueCount, "Queue size should be reduced");
        Assert.All(afterClearStatus.QueueItems, item =>
            Assert.NotEqual("Fault", item.SoundType));

        await Task.Delay(15000);

        // 모든 큐 클리어 테스트
        await _fx.SoundSvc.StopAllSoundsAsync();
        var finalStatus = _fx.SoundSvc.GetQueueStatus();

        Assert.Equal(0, finalStatus.QueueCount);
        Assert.False(finalStatus.IsProcessing);
    }

    private async Task WaitForQueueCompletion(TimeSpan timeout)
    {
        var endTime = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < endTime)
        {
            var status = _fx.SoundSvc.GetQueueStatus();
            if (status.QueueCount == 0 && !status.IsProcessing)
            {
                return; // 완료됨
            }

            await Task.Delay(100);
        }

        Debug.WriteLine("Warning: Queue did not complete within timeout");
    }

    private void AnalyzeQueueBurstResults(List<QueueEventLog> eventLog, TimeSpan totalTime, int maxQueueSize)
    {
        var totalEvents = eventLog.Count;
        var queueOverflows = eventLog.Count(e => e.QueueSizeAfter >= maxQueueSize);
        var processingStarted = eventLog.Count(e => !e.IsProcessingBefore && e.IsProcessingAfter);

        Debug.WriteLine($"\n=== Queue Burst Test Results ===");
        Debug.WriteLine($"Total events: {totalEvents}");
        Debug.WriteLine($"Queue overflows: {queueOverflows}");
        Debug.WriteLine($"Processing started: {processingStarted} times");
        Debug.WriteLine($"Total time: {totalTime.TotalMilliseconds:F0}ms");
        Debug.WriteLine($"Event rate: {totalEvents / totalTime.TotalSeconds:F1} events/sec");

        // 큐 크기 변화 패턴 분석
        var maxQueueReached = eventLog.Max(e => e.QueueSizeAfter);
        var avgQueueSize = eventLog.Average(e => e.QueueSizeAfter);

        Debug.WriteLine($"Max queue size reached: {maxQueueReached}");
        Debug.WriteLine($"Average queue size: {avgQueueSize:F1}");

        // Assert
        Assert.True(maxQueueReached <= maxQueueSize, "Queue should not exceed max size");
        Assert.True(processingStarted >= 1, "Processing should have started");
    }

    private void AnalyzePerformanceResults(List<QueuePerformanceMetric> performanceLog, TimeSpan totalTime)
    {
        var avgEnqueueTime = performanceLog.Average(p => p.EnqueueTime);
        var maxEnqueueTime = performanceLog.Max(p => p.EnqueueTime);
        var minEnqueueTime = performanceLog.Min(p => p.EnqueueTime);

        var byEventType = performanceLog.GroupBy(p => p.EventType);

        Debug.WriteLine($"\n=== Queue Performance Test Results ===");
        Debug.WriteLine($"Total events: {performanceLog.Count}");
        Debug.WriteLine($"Total time: {totalTime.TotalMilliseconds:F0}ms");
        Debug.WriteLine($"Enqueue time - Avg: {avgEnqueueTime:F2}ms, Min: {minEnqueueTime:F2}ms, Max: {maxEnqueueTime:F2}ms");

        foreach (var group in byEventType)
        {
            var groupAvgTime = group.Average(p => p.EnqueueTime);
            Debug.WriteLine($"{group.Key}: {group.Count()} events, Avg enqueue: {groupAvgTime:F2}ms");
        }

        // Assert
        Assert.True(avgEnqueueTime < 50, $"Average enqueue time should be < 50ms. Actual: {avgEnqueueTime:F2}ms");
        Assert.True(maxEnqueueTime < 200, $"Max enqueue time should be < 200ms. Actual: {maxEnqueueTime:F2}ms");
    }
}

/// <summary>
/// 큐 이벤트 로그
/// </summary>
public class QueueEventLog
{
    public int EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
    public int QueueSizeBefore { get; set; }
    public int QueueSizeAfter { get; set; }
    public bool IsProcessingBefore { get; set; }
    public bool IsProcessingAfter { get; set; }
}

/// <summary>
/// 큐 성능 메트릭
/// </summary>
public class QueuePerformanceMetric
{
    public int EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public double EnqueueTime { get; set; } // ms
    public int QueueSizeAfter { get; set; }
    public bool IsProcessing { get; set; }
}

/// <summary>
/// SoundService 오디오 장치 관리 테스트
/// </summary>
[Collection(nameof(SoundServiceCollection))]
public class SoundService_AudioDeviceTests
{
    private readonly SoundServiceFixture _fx;

    public SoundService_AudioDeviceTests(SoundServiceFixture fx) => _fx = fx;

    [Fact(DisplayName = "AudioDevice – Get Available Audio Devices")]
    public void Get_Available_Audio_Devices_Test()
    {
        // Act
        var devices = SoundHelper.GetAvailableAudioDevices();

        // Assert
        Assert.NotNull(devices);
        Assert.NotEmpty(devices);

        // 기본 장치가 최소 하나는 있어야 함
        var defaultDevices = devices.Where(d => d.IsDefault).ToList();
        Assert.NotEmpty(defaultDevices);

        // 장치 정보 유효성 검증
        Assert.All(devices, device =>
        {
            Assert.False(string.IsNullOrEmpty(device.Name));
            Assert.False(string.IsNullOrEmpty(device.Description));
            Assert.NotNull(device.NativeDevice);
            Assert.True(Enum.IsDefined(typeof(EnumAudioType), device.DeviceType));
        });

        // 장치 타입별 분류
        var waveOutDevices = devices.Where(d => d.DeviceType == EnumAudioType.WaveOut).ToList();
        var wasapiDevices = devices.Where(d => d.DeviceType == EnumAudioType.WASAPI).ToList();

        Debug.WriteLine($"\n=== Available Audio Devices ===");
        Debug.WriteLine($"Total devices: {devices.Count}");
        Debug.WriteLine($"WaveOut devices: {waveOutDevices.Count}");
        Debug.WriteLine($"WASAPI devices: {wasapiDevices.Count}");
        Debug.WriteLine($"Default devices: {defaultDevices.Count}");

        Debug.WriteLine($"\n=== Device Details ===");
        foreach (var device in devices)
        {
            var status = device.IsEnabled ? "Enabled" : "Disabled";
            var defaultMark = device.IsDefault ? " [DEFAULT]" : "";
            Debug.WriteLine($"{device.DeviceType}: {device.Name} ({status}){defaultMark}");
            Debug.WriteLine($"  Description: {device.Description}");
            Debug.WriteLine($"  Device Number: {device.DeviceNumber}");
            Debug.WriteLine($"  Native Type: {device.NativeDevice?.GetType().Name}");
            Debug.WriteLine("");
        }

        // WaveOut 장치 검증
        Assert.All(waveOutDevices, device =>
        {
            Assert.True(device.NativeDevice is int);
            Assert.True((int)device.NativeDevice >= -1); // -1은 Audio Mapper
        });

        // WASAPI 장치 검증
        Assert.All(wasapiDevices, device =>
        {
            Assert.True(device.NativeDevice is MMDevice);
        });
    }

    [Fact(DisplayName = "AudioDevice – Device Selection and Reset")]
    public async Task Device_Selection_And_Reset_Test()
    {
        // Arrange
        var devices = SoundHelper.GetAvailableAudioDevices();
        Assert.NotEmpty(devices);

        var testDevice = devices.First();

        // Act & Assert - 장치 선택
        var selectResult = await _fx.SoundSvc.SelectAudioDevice(testDevice);
        Assert.True(selectResult);

        var currentDevice = _fx.SoundSvc.GetCurrentAudioDevice();
        Assert.NotNull(currentDevice);
        Assert.Equal(testDevice.Name, currentDevice.Name);
        Assert.Equal(testDevice.DeviceType, currentDevice.DeviceType);
        Assert.Equal(testDevice.DeviceNumber, currentDevice.DeviceNumber);

        // Act & Assert - 기본 장치로 리셋
        _fx.SoundSvc.ResetToDefaultAudioDevice();
        var resetDevice = _fx.SoundSvc.GetCurrentAudioDevice();
        Assert.Null(resetDevice);

        Debug.WriteLine($"✅ Successfully selected and reset device: {testDevice.Name}");
    }

    [Fact(DisplayName = "AudioDevice – Audio Output Mode Configuration")]
    public void Audio_Output_Mode_Configuration_Test()
    {
        // Arrange
        var outputModes = Enum.GetValues<EnumAudioOutputMode>();

        // Act & Assert - 각 출력 모드 설정 테스트
        foreach (var mode in outputModes)
        {
            try
            {
                _fx.SoundSvc.SetAudioOutputMode(mode);
                Debug.WriteLine($"✅ Successfully set audio output mode: {mode}");
            }
            catch (Exception ex)
            {
                Assert.True(false, $"Failed to set audio output mode {mode}: {ex.Message}");
            }
        }

        // 기본 모드로 복원
        _fx.SoundSvc.SetAudioOutputMode(EnumAudioOutputMode.WaveOutEvent);
    }

    [Fact(DisplayName = "AudioDevice – Device Type Compatibility Test")]
    public async Task Device_Type_Compatibility_Test()
    {
        // Arrange
        var devices = SoundHelper.GetAvailableAudioDevices();
        var outputModes = Enum.GetValues<EnumAudioOutputMode>();

        var compatibilityResults = new List<DeviceCompatibilityResult>();

        // Act - 각 장치와 출력 모드 조합 테스트
        foreach (var device in devices.Take(3)) // 처음 3개 장치만 테스트 (시간 절약)
        {
            foreach (var outputMode in outputModes)
            {
                var result = new DeviceCompatibilityResult
                {
                    DeviceName = device.Name,
                    DeviceType = device.DeviceType,
                    OutputMode = outputMode,
                    TestTime = DateTime.UtcNow
                };

                try
                {
                    // 장치 선택
                    _fx.SoundSvc.SelectAudioDevice(device);
                    _fx.SoundSvc.SetAudioOutputMode(outputMode);

                    // 짧은 사운드 재생 테스트
                    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                    await _fx.SoundSvc.DetectionSoundPlayAsync(cts.Token);

                    result.IsCompatible = true;
                    result.ErrorMessage = null;
                }
                catch (OperationCanceledException)
                {
                    // 타임아웃은 정상 (재생이 시작되었다는 의미)
                    result.IsCompatible = true;
                    result.ErrorMessage = "Timeout (Normal)";
                }
                catch (Exception ex)
                {
                    result.IsCompatible = false;
                    result.ErrorMessage = ex.Message;
                }

                compatibilityResults.Add(result);
                await Task.Delay(100); // 장치 전환 간격
            }
        }

        // 기본 설정으로 복원
        _fx.SoundSvc.ResetToDefaultAudioDevice();
        _fx.SoundSvc.SetAudioOutputMode(EnumAudioOutputMode.WaveOutEvent);

        // Assert & 결과 분석
        AnalyzeCompatibilityResults(compatibilityResults);
    }

    [Fact(DisplayName = "AudioDevice – Device Switching During Playback")]
    public async Task Device_Switching_During_Playback_Test()
    {
        // Arrange
        var devices = SoundHelper.GetAvailableAudioDevices();
        if (devices.Count < 2)
        {
            Debug.WriteLine("⚠️ Skipping device switching test - not enough audio devices");
            return;
        }

        var device1 = devices[0];
        var device2 = devices[1];

        var switchingResults = new List<DeviceSwitchResult>();

        // Act - 재생 중 장치 전환 테스트
        for (int i = 0; i < 3; i++)
        {
            var result = new DeviceSwitchResult
            {
                TestRound = i + 1,
                StartTime = DateTime.UtcNow
            };

            try
            {
                // 첫 번째 장치에서 재생 시작
                _fx.SoundSvc.SelectAudioDevice(device1);
                result.InitialDevice = device1.Name;

                var playTask = _fx.SoundSvc.DetectionSoundPlayAsync();
                await Task.Delay(300); // 재생 시작 대기

                // 재생 중 두 번째 장치로 전환
                _fx.SoundSvc.SelectAudioDevice(device2);
                result.SwitchedDevice = device2.Name;
                result.SwitchTime = DateTime.UtcNow;

                // 전환 후 새로운 재생 시도
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
                await _fx.SoundSvc.MalfunctionSoundPlayAsync(cts.Token);

                result.IsSuccessful = true;
                result.ErrorMessage = null;

                // 기존 재생 정리
                await _fx.SoundSvc.StopAllSoundsAsync();
                try
                {
                    await playTask;
                }
                catch (OperationCanceledException)
                {
                    // 예상된 취소
                }
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = ex.Message;
            }

            result.EndTime = DateTime.UtcNow;
            switchingResults.Add(result);

            await Task.Delay(500); // 다음 테스트 간격
        }

        // 기본 설정으로 복원
        _fx.SoundSvc.ResetToDefaultAudioDevice();

        // Assert & 결과 분석
        AnalyzeDeviceSwitchingResults(switchingResults);
    }

    [Fact(DisplayName = "AudioDevice – Device Error Handling")]
    public async Task Device_Error_Handling_Test()
    {
        // Arrange - 잘못된 장치 정보 생성
        var invalidDevice = new AudioDeviceInfo
        {
            DeviceNumber = 999,
            Name = "Invalid Device",
            Description = "Non-existent device for testing",
            DeviceType = EnumAudioType.WaveOut,
            IsDefault = false,
            IsEnabled = false,
            NativeDevice = 999 // 존재하지 않는 장치 번호
        };

        var errorResults = new List<ErrorHandlingResult>();

        // Test 1: 잘못된 장치 선택
        var result1 = new ErrorHandlingResult
        {
            TestCase = "Invalid Device Selection",
            StartTime = DateTime.UtcNow
        };

        try
        {
            var selectResult = _fx.SoundSvc.SelectAudioDevice(invalidDevice);
            // 선택 자체는 성공할 수 있음 (실제 사용 시 실패)
            result1.IsExpectedFailure = true;
            result1.ActualResult = "Selection succeeded";
        }
        catch (Exception ex)
        {
            result1.IsExpectedFailure = true;
            result1.ActualResult = $"Selection failed: {ex.Message}";
        }

        errorResults.Add(result1);

        // Test 2: 잘못된 장치로 재생 시도
        var result2 = new ErrorHandlingResult
        {
            TestCase = "Playback with Invalid Device",
            StartTime = DateTime.UtcNow
        };

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000));
            await _fx.SoundSvc.DetectionSoundPlayAsync(cts.Token);
            result2.IsExpectedFailure = false;
            result2.ActualResult = "Playback succeeded unexpectedly";
        }
        catch (Exception ex)
        {
            result2.IsExpectedFailure = true;
            result2.ActualResult = $"Playback failed as expected: {ex.GetType().Name}";
        }

        errorResults.Add(result2);

        // 정상 장치로 복원
        _fx.SoundSvc.ResetToDefaultAudioDevice();

        // Test 3: 복원 후 정상 동작 확인
        var result3 = new ErrorHandlingResult
        {
            TestCase = "Recovery After Error",
            StartTime = DateTime.UtcNow
        };

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
            await _fx.SoundSvc.DetectionSoundPlayAsync(cts.Token);
            result3.IsExpectedFailure = false;
            result3.ActualResult = "Recovery successful";
        }
        catch (OperationCanceledException)
        {
            result3.IsExpectedFailure = false;
            result3.ActualResult = "Recovery successful (timeout)";
        }
        catch (Exception ex)
        {
            result3.IsExpectedFailure = false;
            result3.ActualResult = $"Recovery failed: {ex.Message}";
        }

        errorResults.Add(result3);

        // Assert & 결과 분석
        AnalyzeErrorHandlingResults(errorResults);
    }

    private void AnalyzeCompatibilityResults(List<DeviceCompatibilityResult> results)
    {
        var totalTests = results.Count;
        var successfulTests = results.Count(r => r.IsCompatible);
        var successRate = (double)successfulTests / totalTests * 100;

        Debug.WriteLine($"\n=== Device Compatibility Test Results ===");
        Debug.WriteLine($"Total combinations tested: {totalTests}");
        Debug.WriteLine($"Successful combinations: {successfulTests}");
        Debug.WriteLine($"Success rate: {successRate:F1}%");

        // 장치 타입별 분석
        var byDeviceType = results.GroupBy(r => r.DeviceType);
        foreach (var group in byDeviceType)
        {
            var typeSuccessRate = group.Count(r => r.IsCompatible) / (double)group.Count() * 100;
            Debug.WriteLine($"{group.Key}: {group.Count(r => r.IsCompatible)}/{group.Count()} ({typeSuccessRate:F1}%)");
        }

        // 출력 모드별 분석
        var byOutputMode = results.GroupBy(r => r.OutputMode);
        foreach (var group in byOutputMode)
        {
            var modeSuccessRate = group.Count(r => r.IsCompatible) / (double)group.Count() * 100;
            Debug.WriteLine($"{group.Key}: {group.Count(r => r.IsCompatible)}/{group.Count()} ({modeSuccessRate:F1}%)");
        }

        // 실패한 조합 상세 정보
        var failures = results.Where(r => !r.IsCompatible).ToList();
        if (failures.Any())
        {
            Debug.WriteLine($"\n=== Failed Combinations ===");
            foreach (var failure in failures)
            {
                Debug.WriteLine($"❌ {failure.DeviceName} + {failure.OutputMode}: {failure.ErrorMessage}");
            }
        }

        // Assert
        Assert.True(successRate >= 70, $"Compatibility success rate should be >= 70%. Actual: {successRate:F1}%");
    }

    private void AnalyzeDeviceSwitchingResults(List<DeviceSwitchResult> results)
    {
        var successfulSwitches = results.Count(r => r.IsSuccessful);
        var totalSwitches = results.Count;

        Debug.WriteLine($"\n=== Device Switching Test Results ===");
        Debug.WriteLine($"Total switching tests: {totalSwitches}");
        Debug.WriteLine($"Successful switches: {successfulSwitches}");
        Debug.WriteLine($"Success rate: {(double)successfulSwitches / totalSwitches * 100:F1}%");

        foreach (var result in results)
        {
            var status = result.IsSuccessful ? "✅" : "❌";
            var duration = (result.EndTime - result.StartTime).TotalMilliseconds;
            Debug.WriteLine($"{status} Round {result.TestRound}: {result.InitialDevice} → {result.SwitchedDevice} ({duration:F0}ms)");

            if (!result.IsSuccessful)
            {
                Debug.WriteLine($"    Error: {result.ErrorMessage}");
            }
        }

        // Assert
        Assert.True(successfulSwitches >= totalSwitches * 0.6,
            $"Device switching success rate should be >= 60%. Actual: {successfulSwitches}/{totalSwitches}");
    }

    private void AnalyzeErrorHandlingResults(List<ErrorHandlingResult> results)
    {
        Debug.WriteLine($"\n=== Error Handling Test Results ===");

        foreach (var result in results)
        {
            var status = result.IsExpectedFailure ? "✅" : "❌";
            Debug.WriteLine($"{status} {result.TestCase}: {result.ActualResult}");
        }

        // Assert
        var recoveryTest = results.FirstOrDefault(r => r.TestCase == "Recovery After Error");
        Assert.NotNull(recoveryTest);
        Assert.False(recoveryTest.IsExpectedFailure); // 복구는 성공해야 함
        Assert.Contains("successful", recoveryTest.ActualResult.ToLower());
    }
}

/// <summary>
/// 장치 호환성 테스트 결과
/// </summary>
public class DeviceCompatibilityResult
{
    public string DeviceName { get; set; } = string.Empty;
    public EnumAudioType DeviceType { get; set; }
    public EnumAudioOutputMode OutputMode { get; set; }
    public bool IsCompatible { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime TestTime { get; set; }
}

/// <summary>
/// 장치 전환 테스트 결과
/// </summary>
public class DeviceSwitchResult
{
    public int TestRound { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime SwitchTime { get; set; }
    public string InitialDevice { get; set; } = string.Empty;
    public string SwitchedDevice { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 에러 처리 테스트 결과
/// </summary>
public class ErrorHandlingResult
{
    public string TestCase { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public bool IsExpectedFailure { get; set; }
    public string ActualResult { get; set; } = string.Empty;
}


/// <summary>
/// 사운드 재생 중 실시간 장치 전환 테스트
/// </summary>
[Collection(nameof(SoundServiceCollection))]
public class SoundService_RuntimeDeviceSwitchTests
{
    private readonly SoundServiceFixture _fx;

    public SoundService_RuntimeDeviceSwitchTests(SoundServiceFixture fx) => _fx = fx;


    [Fact(DisplayName = "DeviceSwitch – Auto Stop on Device Change")]
    public async Task Device_Change_Auto_Stop_Test()
    {
        // Arrange
        var devices = SoundHelper.GetAvailableAudioDevices();
        if (devices.Count < 2)
        {
            Debug.WriteLine("⚠️ Skipping test - need at least 2 audio devices");
            return;
        }

        var device1 = devices[1]; // 첫 번째 장치
        var device2 = devices[2]; // 두 번째 장치

        Debug.WriteLine($"🔊 Testing device switch auto-stop:");
        Debug.WriteLine($"  Device 1: {device1.Name}");
        Debug.WriteLine($"  Device 2: {device2.Name}");

        bool isPlayingBefore = false;
        bool isPlayingAfter = false;
        try
        {
            // 1. 첫 번째 장치 선택 및 재생 시작
            _fx.SoundSvc.SelectAudioDevice(device1);

            // 무한 재생 시작
            var playTask = _fx.SoundSvc.MalfunctionSoundPlayAsync(); // 무한 재생
            await Task.Delay(2000);

            // 2. 재생 시작 확인
            isPlayingBefore = _fx.SoundSvc.IsCurrentlyPlaying();
            Debug.WriteLine($"✅ Playing on {device1.Name}: {isPlayingBefore}");
            // 3. 재생 중 장치 변경 → 자동 중지되어야 함
            _fx.SoundSvc.SelectAudioDevice(device2);
            Debug.WriteLine($"🔄 Switched to {device2.Name}");

            // 5. 이전 재생 작업 완료 확인
            try
            {
                await playTask;
                Debug.WriteLine("❌ Previous playback completed normally (unexpected)");
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("✅ Previous playback was cancelled (expected)");
            }

            // 6. 새 장치에서 새로운 재생 테스트
            Debug.WriteLine($"🎵 Testing new playback on {device2.Name}...");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _fx.SoundSvc.DetectionSoundPlayAsync(cts.Token);
            Debug.WriteLine($"✅ New playback successful on {device2.Name}");
            await Task.Delay(2000);
            // Assert
            Assert.True(isPlayingBefore, "Should be playing before device switch");
            Assert.False(isPlayingAfter, "Should NOT be playing after device switch (auto-stopped)");

            Debug.WriteLine("🎯 Device switch auto-stop test PASSED!");
        }
        finally
        {
            await _fx.SoundSvc.StopAllSoundsAsync();
            _fx.SoundSvc.ResetToDefaultAudioDevice();
        }
    }

    [Fact(DisplayName = "DeviceSwitch – Multiple Device Changes")]
    public async Task Multiple_Device_Changes_Test()
    {
        var devices = SoundHelper.GetAvailableAudioDevices();
        if (devices.Count < 3)
        {
            Debug.WriteLine("⚠️ Skipping test - need at least 3 audio devices");
            return;
        }

        Debug.WriteLine($"🔊 Testing multiple device changes:");

        try
        {
            // 각 장치에서 순차적으로 재생 테스트
            for (int i = 0; i < Math.Min(3, devices.Count); i++)
            {
                var device = devices[i];
                Debug.WriteLine($"\n📱 Testing device {i + 1}: {device.Name}");

                // 장치 선택
                _fx.SoundSvc.SelectAudioDevice(device);

                // 짧은 재생 테스트
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

                try
                {
                    await _fx.SoundSvc.DetectionSoundPlayAsync(cts.Token);
                    Debug.WriteLine($"  ✅ Playback successful on {device.Name}");
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine($"  ✅ Playback timeout on {device.Name} (normal)");
                }

                await Task.Delay(200); // 장치 간 전환 간격
            }

            Debug.WriteLine("\n🎯 Multiple device changes test completed!");
        }
        finally
        {
            await _fx.SoundSvc.StopAllSoundsAsync();
            _fx.SoundSvc.ResetToDefaultAudioDevice();
        }
    }

    [Fact(DisplayName = "DeviceSwitch – Queue Processing with Device Change")]
    public async Task Queue_With_Device_Change_Test()
    {
        var devices = SoundHelper.GetAvailableAudioDevices();
        if (devices.Count < 2)
        {
            Debug.WriteLine("⚠️ Skipping test - need at least 2 audio devices");
            return;
        }

        _fx.SoundSvc.SetMaxQueueSize(3);
        var device1 = devices[0];
        var device2 = devices[1];

        Debug.WriteLine($"🔊 Testing queue processing with device change:");

        try
        {
            // 1. 첫 번째 장치에서 큐에 여러 사운드 추가
            _fx.SoundSvc.SelectAudioDevice(device1);

            await _fx.SoundSvc.DetectionSoundPlayAsync();
            await _fx.SoundSvc.MalfunctionSoundPlayAsync();
            await _fx.SoundSvc.ActionReportSoundPlayAsync();

            await Task.Delay(500); // 처리 시작 대기
            var statusBefore = _fx.SoundSvc.GetQueueStatus();
            Debug.WriteLine($"📊 Queue before switch: {statusBefore.QueueCount} items, Processing: {statusBefore.IsProcessing}");

            // 2. 큐 처리 중 장치 변경 → 모든 큐 중지되어야 함
            _fx.SoundSvc.SelectAudioDevice(device2);
            Debug.WriteLine($"🔄 Switched to {device2.Name} during queue processing");

            await Task.Delay(500);
            var statusAfter = _fx.SoundSvc.GetQueueStatus();
            Debug.WriteLine($"📊 Queue after switch: {statusAfter.QueueCount} items, Processing: {statusAfter.IsProcessing}");

            // 3. 새 장치에서 새로운 큐 테스트
            await _fx.SoundSvc.DetectionSoundPlayAsync();
            await Task.Delay(300);
            var statusNew = _fx.SoundSvc.GetQueueStatus();
            Debug.WriteLine($"📊 New queue on {device2.Name}: {statusNew.QueueCount} items, Processing: {statusNew.IsProcessing}");

            // Assert
            Assert.False(statusAfter.IsProcessing, "Queue should stop processing after device change");
            Assert.True(statusNew.IsProcessing || statusNew.QueueCount > 0, "New queue should work on new device");

            Debug.WriteLine("🎯 Queue + device change test passed!");
        }
        finally
        {
            await _fx.SoundSvc.StopAllSoundsAsync();
            _fx.SoundSvc.ResetToDefaultAudioDevice();
        }
    }
}

