using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Sounds.Helpers;
using Ironwall.Dotnet.Libraries.Sounds.Models;
using Ironwall.Dotnet.Libraries.Sounds.Providers;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.IO;

namespace Ironwall.Dotnet.Libraries.Sounds.Services;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/10/2025 3:19:52 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
internal class SoundService : TaskService, ISoundService
{

    #region - Ctors -
    public SoundService(ILogService log,
                        SoundSetupModel setupModel
                        , SoundSourceProvider soundProvider
                        , AudioDeviceInfoProvider audioDeviceInfoProvider)
    {
        _log = log;
        _setupModel = setupModel;
        _soundProvider = soundProvider;
        _audioDeviceInfoProvider = audioDeviceInfoProvider;

        // 재생 제어를 위한 Semaphore (동시에 하나만 재생)
        _soundPlaySemaphore = new SemaphoreSlim(1, 1);

        // 현재 재생 제어
        _currentPlayCts = new CancellationTokenSource();

        // 기본 설정
        _audioOutputMode = EnumAudioOutputMode.WaveOutEvent;
        _selectedAudioDevice = null;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override async Task RunTask(CancellationToken token = default)
    {
        var devices = SoundHelper.GetAvailableAudioDevices(_log);
        await SetAudioDeviceInfoProviderAsync(devices);

        var selectedDevice = _audioDeviceInfoProvider.Where(entity => entity.Name == _setupModel.AudioDevice).FirstOrDefault();
        if (selectedDevice != null)
            SelectAudioDevice(selectedDevice);

        var files = await SoundHelper.GetSoundFilesAsync(_setupModel.DirectoryUri ?? "c:/", _log);
        await SetSoundProviderAsync(files);
    }

    protected override Task ExitTask(CancellationToken token = default)
    {

        return Task.CompletedTask;
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #region - Audio Device Management -


    /// <summary>
    /// 사운드 파일들을 SoundModel로 변환하여 Provider에 설정
    /// </summary>
    public async Task SetAudioDeviceInfoProviderAsync(List<IAudioDeviceInfo> devices)
    {
        await _soundPlaySemaphore.WaitAsync();
        try
        {
            _audioDeviceInfoProvider.Clear();

            foreach (var device in devices)
            {
                _audioDeviceInfoProvider.Add(device);
            }

            _log?.Info($"Loaded {_audioDeviceInfoProvider.Count} audio device info into provider");
        }
        catch (Exception ex)
        {
            _log?.Error($"Error setting audio device info provider: {ex.Message}");
        }
        finally
        {
            _soundPlaySemaphore.Release();
        }
    }


    /// <summary>
    /// 오디오 출력 모드 설정
    /// </summary>
    public void SetAudioOutputMode(EnumAudioOutputMode mode)
    {
        _audioOutputMode = mode;
        _log?.Info($"Audio output mode set to: {mode}");
    }


    /// <summary>
    /// 특정 오디오 장치 선택 (재생 중이면 자동 중지)
    /// </summary>
    public async Task<bool> SelectAudioDevice(IAudioDeviceInfo deviceInfo)
    {
        try
        {
            // 현재 재생 중인지 확인
            bool wasPlaying = IsCurrentlyPlaying();
            if (wasPlaying)
            {
                _log?.Info($"Stopping current playback due to device change from {_selectedAudioDevice?.Name} to {deviceInfo.Name}");

                //자연스러운 비동기 호출
                await StopAllSoundsAsync();
            }

            // 새 장치 설정
            _selectedAudioDevice = deviceInfo;
            _log?.Info($"Selected audio device: {deviceInfo.Name} ({deviceInfo.DeviceType})");

            return true;
        }
        catch (Exception ex)
        {
            _log?.Error($"Error selecting audio device: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 기본 오디오 장치로 재설정
    /// </summary>
    public void ResetToDefaultAudioDevice()
    {
        _selectedAudioDevice = null;
        _log?.Info("Reset to default audio device");
    }

    /// <summary>
    /// 현재 선택된 장치 정보
    /// </summary>
    public IAudioDeviceInfo? GetCurrentAudioDevice()
    {
        return _selectedAudioDevice;
    }

    /// <summary>
    /// 오디오 출력 장치 생성 (설정에 따라)
    /// </summary>
    private IWavePlayer CreateAudioOutputDevice()
    {
        try
        {
            return _audioOutputMode switch
            {
                EnumAudioOutputMode.WaveOut => CreateWaveOutDevice(),
                EnumAudioOutputMode.WaveOutEvent => CreateWaveOutEventDevice(),
                EnumAudioOutputMode.DirectSoundOut => CreateDirectSoundOutDevice(),
                EnumAudioOutputMode.WasapiOut => CreateWasapiOutDevice(),
                _ => CreateWaveOutEventDevice() // 기본값
            };
        }
        catch (Exception ex)
        {
            _log?.Warning($"Failed to create {_audioOutputMode} device, falling back to WaveOutEvent: {ex.Message}");
            return CreateWaveOutEventDevice();
        }
    }

    private IWavePlayer CreateWaveOutDevice()
    {
        var device = new WaveOut();

        if (_selectedAudioDevice?.DeviceType == EnumAudioType.WaveOut &&
            _selectedAudioDevice.NativeDevice is int deviceNumber)
        {
            device.DeviceNumber = deviceNumber;
        }

        return device;
    }

    private IWavePlayer CreateWaveOutEventDevice()
    {
        var device = new WaveOutEvent();

        if (_selectedAudioDevice?.DeviceType == EnumAudioType.WaveOut &&
            _selectedAudioDevice.NativeDevice is int deviceNumber)
        {
            device.DeviceNumber = deviceNumber;
        }

        return device;
    }

    private IWavePlayer CreateDirectSoundOutDevice()
    {
        // DirectSoundOut은 기본 장치 사용 (GUID로 특정 장치 선택 가능하지만 복잡함)
        return new DirectSoundOut();
    }

    private IWavePlayer CreateWasapiOutDevice()
    {
        if (_selectedAudioDevice?.DeviceType == EnumAudioType.WASAPI &&
            _selectedAudioDevice.NativeDevice is MMDevice mmDevice)
        {
            return new WasapiOut(mmDevice, AudioClientShareMode.Shared, false, 100);
        }

        // 기본 WASAPI 장치 사용
        return new WasapiOut();
    }

    #endregion



    /// <summary>
    /// 큐 최대 크기 설정
    /// </summary>
    public void SetMaxQueueSize(int maxSize)
    {
        if (maxSize < 1)
            throw new ArgumentException("Max queue size must be at least 1");

        lock (_queueLock)
        {
            _maxQueueSize = maxSize;
            _log?.Info($"Sound queue max size set to: {maxSize}");

            // 현재 큐가 새로운 크기보다 크면 정리
            TrimQueue();
        }
    }


    /// <summary>
    /// 사운드 재생 스케줄링 (큐에 추가)
    /// </summary>
    public async Task PlayScheduleAsync(ISoundModel soundModel, int priority = 0, CancellationToken externalToken = default)
    {
        if (soundModel == null)
            throw new ArgumentNullException(nameof(soundModel));

        var scheduleItem = new SoundScheduleItem
        {
            SoundModel = soundModel,
            ScheduledTime = DateTime.UtcNow,
            Priority = priority,
            EventId = Guid.NewGuid().ToString("N")[..8], // 8자리 ID
            ExternalToken = externalToken
        };

        lock (_queueLock)
        {
            // 큐에 추가
            _soundQueue.Enqueue(scheduleItem);

            _log?.Info($"Scheduled sound: {soundModel.Name} (EventId: {scheduleItem.EventId}, Queue size: {_soundQueue.Count})");

            // 큐 크기 제한 적용 (FIFO 방식으로 오래된 것 제거)
            TrimQueue();
        }

        // 처리 작업이 실행 중이지 않으면 시작
        await EnsureProcessingStarted();
    }




    /// <summary>
    /// 사운드 파일들을 SoundModel로 변환하여 Provider에 설정
    /// </summary>
    public async Task SetSoundProviderAsync(List<string> files)
    {
        await _soundPlaySemaphore.WaitAsync();
        try
        {
            _soundProvider.Clear();

            ////Default 입력(사운드 없음)
            //_soundProvider.Add(new SoundModel
            //{
            //    Id = 0,
            //    Name = "--------",
            //    File = null,
            //    Type = EnumEventType.Intrusion,
            //    IsPlaying = false
            //});

            foreach (var filePath in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var soundModel = new SoundModel
                {
                    Id = _soundProvider.Count + 1,
                    Name = fileName,
                    File = filePath,
                    //Type = SoundHelper.DetermineEventType(fileName), // 파일명으로 타입 추정
                    IsPlaying = false
                };

                if (_setupModel.DetectionSound == fileName)
                    soundModel.Type = EnumEventType.Intrusion;
                else if(_setupModel.MalfunctionSound == fileName)
                    soundModel.Type = EnumEventType.Fault;
                else if(_setupModel.ActionReportSound == fileName)
                    soundModel.Type = EnumEventType.Action;
                else
                    soundModel.Type = EnumEventType.Intrusion;

                _soundProvider.Add(soundModel);
            }

            _log?.Info($"Loaded {_soundProvider.Count} sound files into provider");
        }
        catch (Exception ex)
        {
            _log?.Error($"Error setting sound provider: {ex.Message}");
        }
        finally
        {
            _soundPlaySemaphore.Release();
        }
    }


    /// <summary>
    /// Detection 사운드 스케줄링
    /// </summary>
    public async Task DetectionSoundPlayAsync(CancellationToken externalToken = default)
    {
        var soundFile = GetSoundFile(_setupModel.DetectionSound);
        if (soundFile != null)
        {
            if (soundFile.Type == null || soundFile.Type == EnumEventType.None)
                soundFile.Type = EnumEventType.Intrusion;
            await PlayScheduleAsync(soundFile, priority: 1, externalToken); // 중간 우선순위
        }
    }

    /// <summary>
    /// Malfunction 사운드 스케줄링
    /// </summary>
    public async Task MalfunctionSoundPlayAsync(CancellationToken externalToken = default)
    {
        var soundFile = GetSoundFile(_setupModel.MalfunctionSound);
        if (soundFile != null)
        {
            if (soundFile.Type == null || soundFile.Type == EnumEventType.None)
                soundFile.Type = EnumEventType.Fault;
            await PlayScheduleAsync(soundFile, priority: 2, externalToken); // 낮은 우선순위
        }
    }

    /// <summary>
    /// ActionReport 사운드 스케줄링
    /// </summary>
    public async Task ActionReportSoundPlayAsync(CancellationToken externalToken = default)
    {
        var soundFile = GetSoundFile(_setupModel.ActionReportSound);
        if (soundFile != null)
        {
            if (soundFile.Type == null || soundFile.Type == EnumEventType.None)
                soundFile.Type = EnumEventType.Action;
            await PlayScheduleAsync(soundFile, priority: 0, externalToken); // 높은 우선순위
        }
    }

    /// <summary>
    /// 큐 크기 제한 적용 (FIFO 방식)
    /// </summary>
    private void TrimQueue()
    {
        while (_soundQueue.Count > _maxQueueSize)
        {
            var removedItem = _soundQueue.Dequeue();
            _log?.Warning($"Removed old sound from queue: {removedItem.SoundModel.Name} (EventId: {removedItem.EventId})");
        }
    }

    /// <summary>
    /// 처리 작업 시작 보장
    /// </summary>
    private async Task EnsureProcessingStarted()
    {
        lock (_queueLock)
        {
            if (_isProcessing)
                return; // 이미 처리 중

            _isProcessing = true;
            _processingCts = new CancellationTokenSource();
        }

        _processingTask = ProcessSoundQueueAsync(_processingCts.Token);

        try
        {
            await Task.Yield(); // 처리 작업이 시작되도록 양보
        }
        catch
        {
            // 무시
        }
    }


    /// <summary>
    /// 사운드 큐 처리 메인 루프
    /// </summary>
    private async Task ProcessSoundQueueAsync(CancellationToken cancellationToken)
    {
        _log?.Info("Sound queue processing started");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                SoundScheduleItem? currentItem = null;

                lock (_queueLock)
                {
                    if (_soundQueue.Count == 0)
                    {
                        // 큐가 비어있으면 처리 중단
                        _isProcessing = false;
                        _log?.Info("Sound queue is empty, stopping processing");
                        break;
                    }

                    currentItem = _soundQueue.Dequeue();
                }

                if (currentItem != null)
                {
                    await ProcessSingleSoundItem(currentItem, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _log?.Info("Sound queue processing was cancelled");
        }
        catch (Exception ex)
        {
            _log?.Error($"Error in sound queue processing: {ex.Message}");
        }
        finally
        {
            lock (_queueLock)
            {
                _isProcessing = false;
            }
            _log?.Info("Sound queue processing stopped");
        }
    }

    /// <summary>
    /// 개별 사운드 아이템 처리
    /// </summary>
    private async Task ProcessSingleSoundItem(ISoundScheduleItem item, CancellationToken cancellationToken)
    {
        try
        {
            _log?.Info($"Processing sound: {item.SoundModel.Name} (EventId: {item.EventId})");

            // 외부 토큰과 내부 토큰 결합
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, item.ExternalToken);

            // 사운드 타입에 따른 설정 가져오기
            var (duration, autoStop) = GetSoundSettings(item.SoundModel);

            // 실제 사운드 재생
            await PlaySoundAsync(item.SoundModel, duration, autoStop, combinedCts.Token);

            _log?.Info($"Completed sound: {item.SoundModel.Name} (EventId: {item.EventId})");
        }
        catch (OperationCanceledException)
        {
            _log?.Warning($"Sound cancelled: {item.SoundModel.Name} (EventId: {item.EventId})");
        }
        catch (Exception ex)
        {
            _log?.Error($"Error processing sound {item.SoundModel.Name} (EventId: {item.EventId}): {ex.Message}");
        }
    }


    /// <summary>
    /// 사운드 타입에 따른 설정 반환
    /// </summary>
    private (int duration, bool autoStop) GetSoundSettings(ISoundModel soundModel)
    {
        return soundModel.Type switch
        {
            EnumEventType.Intrusion => (_setupModel.DetectionSoundDuration, _setupModel.IsDetectionAutoSoundStop),
            EnumEventType.Fault => (_setupModel.MalfunctionSoundDuration, _setupModel.IsMalfunctionAutoSoundStop),
            EnumEventType.Action => (_setupModel.ActionReportSoundDuration, _setupModel.IsActionReportAutoSoundStop),
            _ => (3, true) // 기본값
        };
    }

    /// <summary>
    /// 현재 큐 상태 정보
    /// </summary>
    public SoundQueueStatus GetQueueStatus()
    {
        lock (_queueLock)
        {
            return new SoundQueueStatus
            {
                QueueCount = _soundQueue.Count,
                MaxQueueSize = _maxQueueSize,
                IsProcessing = _isProcessing,
                QueueItems = _soundQueue.Select(item => new QueueItemInfo
                {
                    EventId = item.EventId,
                    SoundName = item.SoundModel.Name,
                    Priority = item.Priority,
                    ScheduledTime = item.ScheduledTime,
                    SoundType = item.SoundModel.Type.ToString()
                }).ToList()
            };
        }
    }

    /// <summary>
    /// 모든 큐 및 재생 중지
    /// </summary>
    public async Task StopAllSoundsAsync()
    {
        lock (_queueLock)
        {
            // 큐 모든 아이템 제거
            var removedCount = _soundQueue.Count;
            _soundQueue.Clear();

            if (removedCount > 0)
            {
                _log?.Info($"Cleared {removedCount} items from sound queue");
            }

            // 처리 작업 중지
            _processingCts?.Cancel();
        }

        // 현재 재생 중인 사운드 중지
        StopCurrentSound();

        // 처리 작업 완료 대기
        if (_processingTask != null)
        {
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException)
            {
                // 예상된 취소
            }
        }

        // 모든 SoundModel의 IsPlaying 상태 초기화
        foreach (var sound in _soundProvider.Where(s => s.IsPlaying))
        {
            sound.IsPlaying = false;
        }

        _log?.Info("All sounds and queue stopped");
    }

    /// <summary>
    /// 특정 이벤트 타입의 큐 아이템 제거
    /// </summary>
    public int ClearQueueByEventType(EnumEventType eventType)
    {
        lock (_queueLock)
        {
            var originalCount = _soundQueue.Count;
            var itemsToKeep = new Queue<SoundScheduleItem>();

            while (_soundQueue.Count > 0)
            {
                var item = _soundQueue.Dequeue();
                if (item.SoundModel.Type != eventType)
                {
                    itemsToKeep.Enqueue(item);
                }
            }

            // 유지할 아이템들을 다시 큐에 추가
            while (itemsToKeep.Count > 0)
            {
                _soundQueue.Enqueue(itemsToKeep.Dequeue());
            }

            var removedCount = originalCount - _soundQueue.Count;
            if (removedCount > 0)
            {
                _log?.Info($"Removed {removedCount} {eventType} items from queue");
            }

            return removedCount;
        }
    }

    /// <summary>
    /// 사운드 재생 (내부 메서드) - 장치 선택 기능 적용
    /// </summary>
    private async Task PlaySoundAsync(ISoundModel soundModel, int duration, bool autoStop, CancellationToken token)
    {
        IWavePlayer? outputDevice = null;
        AudioFileReader? audioFile = null;

        try
        {
            soundModel.IsPlaying = true;

            // 선택된 설정에 따라 오디오 출력 장치 생성
            outputDevice = CreateAudioOutputDevice();
            audioFile = new AudioFileReader(soundModel.File);

            // 현재 재생 중인 디바이스 저장 (중지용)
            lock (_currentDeviceLock)
            {
                _currentOutputDevice = outputDevice;
                _currentAudioFile = audioFile;
            }

            outputDevice.Init(audioFile);

            _log?.Info($"Started playing {soundModel.Type} sound: {soundModel.Name}(duration: {duration}) on {_audioOutputMode} device: {_selectedAudioDevice?.Name ?? "Default"}");

            if (autoStop && duration > 0)
            {
                await PlayWithAutoStopAsync(outputDevice, audioFile, duration, token);
            }
            else if (!autoStop)
            {
                await PlayContinuouslyAsync(outputDevice, audioFile, token);
            }
            else
            {
                await PlayOnceAsync(outputDevice, audioFile, token);
            }
        }
        catch (OperationCanceledException)
        {
            _log?.Warning($"{soundModel.Type} sound playback was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _log?.Error($"Error during {soundModel.Type} sound playback: {ex.Message}");
            throw;
        }
        finally
        {
            soundModel.IsPlaying = false;

            // 리소스 정리
            lock (_currentDeviceLock)
            {
                if (outputDevice != null)
                {
                    try
                    {
                        outputDevice.Stop();
                        outputDevice.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _log?.Warning($"Error disposing audio output device: {ex.Message}");
                    }

                    if (_currentOutputDevice == outputDevice)
                        _currentOutputDevice = null;
                }

                if (audioFile != null)
                {
                    try
                    {
                        audioFile.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _log?.Warning($"Error disposing audio file reader: {ex.Message}");
                    }

                    if (_currentAudioFile == audioFile)
                        _currentAudioFile = null;
                }
            }
        }
    }

    /// <summary>
    /// 지정된 시간 동안 반복 재생
    /// </summary>
    private async Task PlayWithAutoStopAsync(IWavePlayer outputDevice, AudioFileReader audioFile, int durationSeconds, CancellationToken token)
    {
        var endTime = DateTime.UtcNow.AddSeconds(durationSeconds);

        while (DateTime.UtcNow < endTime && !token.IsCancellationRequested)
        {
            audioFile.Position = 0;

            var playbackStopped = new TaskCompletionSource<bool>();

            EventHandler<StoppedEventArgs>? stoppedHandler = null;
            stoppedHandler = (sender, e) =>
            {
                outputDevice.PlaybackStopped -= stoppedHandler;
                if (e.Exception != null)
                {
                    playbackStopped.TrySetException(e.Exception);
                }
                else
                {
                    playbackStopped.TrySetResult(true);
                }
            };

            outputDevice.PlaybackStopped += stoppedHandler;
            outputDevice.Play();

            var cancellationTask = Task.Delay(Timeout.Infinite, token);
            var completedTask = await Task.WhenAny(playbackStopped.Task, cancellationTask);

            if (completedTask == cancellationTask)
            {
                outputDevice.Stop();
                break;
            }

            if (DateTime.UtcNow >= endTime)
            {
                _log?.Info($"Auto-stopping sound after {durationSeconds} seconds");
                break;
            }
        }
    }

    /// <summary>
    /// 취소될 때까지 무한 반복 재생
    /// </summary>
    private async Task PlayContinuouslyAsync(IWavePlayer outputDevice, AudioFileReader audioFile, CancellationToken token)
    {
        _log?.Info("Starting continuous playback (will loop until cancelled)");

        while (!token.IsCancellationRequested)
        {
            audioFile.Position = 0;

            var playbackStopped = new TaskCompletionSource<bool>();

            EventHandler<StoppedEventArgs>? stoppedHandler = null;
            stoppedHandler = (sender, e) =>
            {
                outputDevice.PlaybackStopped -= stoppedHandler;
                if (e.Exception != null)
                {
                    playbackStopped.TrySetException(e.Exception);
                }
                else
                {
                    playbackStopped.TrySetResult(true);
                }
            };

            outputDevice.PlaybackStopped += stoppedHandler;
            outputDevice.Play();

            var cancellationTask = Task.Delay(Timeout.Infinite, token);
            var completedTask = await Task.WhenAny(playbackStopped.Task, cancellationTask);

            if (completedTask == cancellationTask)
            {
                outputDevice.Stop();
                _log?.Info("Continuous playback cancelled");
                break;
            }

            _log?.Info("Sound file completed, restarting...");
        }
    }

    /// <summary>
    /// 한 번만 재생
    /// </summary>
    private async Task PlayOnceAsync(IWavePlayer outputDevice, AudioFileReader audioFile, CancellationToken token)
    {
        audioFile.Position = 0;

        var playbackStopped = new TaskCompletionSource<bool>();

        EventHandler<StoppedEventArgs>? stoppedHandler = null;
        stoppedHandler = (sender, e) =>
        {
            outputDevice.PlaybackStopped -= stoppedHandler;
            if (e.Exception != null)
            {
                playbackStopped.TrySetException(e.Exception);
            }
            else
            {
                playbackStopped.TrySetResult(true);
            }
        };

        outputDevice.PlaybackStopped += stoppedHandler;
        outputDevice.Play();

        var cancellationTask = Task.Delay(Timeout.Infinite, token);
        await Task.WhenAny(playbackStopped.Task, cancellationTask);

        if (token.IsCancellationRequested)
        {
            outputDevice.Stop();
        }
    }




    /// <summary>
    /// 현재 재생 중인 사운드 중지
    /// </summary>
    private void StopCurrentSound()
    {
        try
        {
            // 진행 중인 재생 취소
            _currentPlayCts?.Cancel();

            // NAudio 디바이스 중지
            _currentOutputDevice?.Stop();

            // 모든 SoundModel의 IsPlaying 상태 초기화
            foreach (var sound in _soundProvider.Where(s => s.IsPlaying))
            {
                sound.IsPlaying = false;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Error stopping current sound: {ex.Message}");
        }
    }



    /// <summary>
    /// 현재 재생 중인지 확인
    /// </summary>
    public bool IsCurrentlyPlaying()
    {
        lock (_queueLock)
        {
            // 큐가 처리 중이거나, 사운드가 재생 중인 경우
            return _isProcessing || _soundProvider.Any(s => s.IsPlaying);
        }
    }

    /// <summary>
    /// 설정된 사운드 파일명으로 SoundModel 찾기
    /// </summary>
    private ISoundModel? GetSoundFile(string? soundFileName)
    {
        if (string.IsNullOrEmpty(soundFileName))
            return null;

        return _soundProvider.FirstOrDefault(s =>
            s.Name.Equals(soundFileName, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(s.File).Equals(soundFileName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 모든 리소스 해제
    /// </summary>
    private void DisposeResources()
    {
        try
        {
            _currentPlayCts?.Cancel();
            _currentPlayCts?.Dispose();

            _currentOutputDevice?.Stop();
            _currentOutputDevice?.Dispose();

            _currentAudioFile?.Dispose();

            _soundPlaySemaphore?.Dispose();
        }
        catch (Exception ex)
        {
            _log?.Error($"Error disposing sound service resources: {ex.Message}");
        }
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    /// <summary>
    /// 사운드 재생 가능 여부
    /// </summary>
    public bool IsSoundEnabled => !string.IsNullOrEmpty(_setupModel.DirectoryUri) &&
                                  Directory.Exists(_setupModel.DirectoryUri);

    #endregion
    #region - Attributes -
    private ILogService _log;
    private SoundSetupModel _setupModel;
    private SoundSourceProvider _soundProvider;
    private readonly AudioDeviceInfoProvider _audioDeviceInfoProvider;
    private readonly Queue<SoundScheduleItem> _soundQueue = new();
    private readonly object _queueLock = new object();
    private Task? _processingTask;
    private CancellationTokenSource? _processingCts;
    private bool _isProcessing = false;
    // 설정 가능한 큐 최대 크기 (기본값 3)
    private int _maxQueueSize = 3;

    // 동시 실행 방지를 위한 단일 Semaphore
    private readonly SemaphoreSlim _soundPlaySemaphore;
    // 현재 재생 제어를 위한 토큰
    private CancellationTokenSource _currentPlayCts;

    // 현재 재생 중인 NAudio 객체들
    private object _currentDeviceLock = new object();
    private IWavePlayer? _currentOutputDevice;
    private AudioFileReader? _currentAudioFile;

    // 오디오 장치 설정
    private IAudioDeviceInfo? _selectedAudioDevice;
    private EnumAudioOutputMode _audioOutputMode;

    private const int MIN_DELAY_TIME = 300;
    #endregion
}