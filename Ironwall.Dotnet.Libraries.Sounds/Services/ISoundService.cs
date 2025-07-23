using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Sounds.Models;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Sounds.Services;
/// <summary>
/// 오디오 사운드 재생 및 장치 관리 서비스의 공개 API.
/// <br/>
/// ◼ 다중 오디오 장치 지원 (WaveOut, WASAPI 등)<br/>
/// ◼ 큐 기반 사운드 스케줄링 및 우선순위 관리<br/>
/// ◼ 이벤트별 사운드 재생 (탐지, 장애, 조치보고)<br/>
/// ◼ 실시간 장치 전환 및 재생 중 자동 중지<br/>
/// 모든 비동기 메서드는 <see cref="CancellationToken"/>을 인자로 받아
/// 호출 측에서 재생 취소를 제어할 수 있어야 한다.
/// </summary>
public interface ISoundService
{
    /*──────────────────────── 상태 ────────────────────────*/

    /// <summary>
    /// 사운드 재생이 가능한 상태인지 여부.
    /// 사운드 디렉토리 존재 및 유효한 사운드 파일이 있을 때 <c>true</c>.
    /// </summary>
    bool IsSoundEnabled { get; }

    /// <summary>
    /// 현재 사운드가 재생 중이거나 큐가 처리 중인지 여부.
    /// </summary>
    bool IsCurrentlyPlaying();

    /*──────────────────────── 장치 관리 ────────────────────────*/

    /// <summary>
    /// 현재 선택된 오디오 출력 장치 정보를 반환한다.
    /// 기본 장치 사용 중이면 <c>null</c>.
    /// </summary>
    IAudioDeviceInfo? GetCurrentAudioDevice();

    /// <summary>
    /// 특정 오디오 장치를 선택한다. 
    /// 재생 중이면 현재 재생을 자동으로 중지한 후 장치를 변경한다.
    /// </summary>
    /// <param name="deviceInfo">선택할 오디오 장치 정보</param>
    /// <returns>장치 선택 성공 여부</returns>
    Task<bool> SelectAudioDevice(IAudioDeviceInfo deviceInfo);

    /// <summary>
    /// 시스템 기본 오디오 장치로 재설정한다.
    /// </summary>
    void ResetToDefaultAudioDevice();

    /// <summary>
    /// 오디오 출력 모드를 설정한다 (WaveOut, WASAPI 등).
    /// 다음 재생부터 적용된다.
    /// </summary>
    /// <param name="mode">사용할 오디오 출력 모드</param>
    void SetAudioOutputMode(EnumAudioOutputMode mode);

    /// <summary>
    /// 사용 가능한 오디오 장치 목록을 서비스에 설정한다.
    /// </summary>
    /// <param name="devices">오디오 장치 목록</param>
    Task SetAudioDeviceInfoProviderAsync(List<IAudioDeviceInfo> devices);

    /*──────────────────────── 사운드 파일 관리 ─────────────────────*/

    /// <summary>
    /// 사운드 파일 목록을 로드하여 내부 Provider에 설정한다.
    /// 파일명을 기반으로 이벤트 타입을 자동 추론한다.
    /// </summary>
    /// <param name="files">사운드 파일 경로 목록</param>
    Task SetSoundProviderAsync(List<string> files);

    /*──────────────────────── 큐 관리 ────────────────────────*/

    /// <summary>
    /// 사운드 재생 큐의 현재 상태를 반환한다.
    /// 큐 크기, 처리 상태, 대기 중인 아이템 목록을 포함한다.
    /// </summary>
    SoundQueueStatus GetQueueStatus();

    /// <summary>
    /// 사운드 재생 큐의 최대 크기를 설정한다.
    /// 큐가 가득 차면 오래된 아이템부터 FIFO 방식으로 제거된다.
    /// </summary>
    /// <param name="maxSize">큐 최대 크기 (최소 1)</param>
    void SetMaxQueueSize(int maxSize);

    /// <summary>
    /// 특정 이벤트 타입의 큐 아이템들을 모두 제거한다.
    /// </summary>
    /// <param name="eventType">제거할 이벤트 타입</param>
    /// <returns>제거된 아이템 개수</returns>
    int ClearQueueByEventType(EnumEventType eventType);

    /*──────────────────────── 사운드 재생 ────────────────────────*/

    /// <summary>
    /// 탐지 이벤트 사운드를 큐에 추가하여 재생한다.
    /// 설정에 따라 자동 중지 또는 무한 재생된다.
    /// </summary>
    /// <param name="externalToken">외부 취소 토큰</param>
    Task DetectionSoundPlayAsync(CancellationToken externalToken = default);

    /// <summary>
    /// 장애 이벤트 사운드를 큐에 추가하여 재생한다.
    /// 설정에 따라 자동 중지 또는 무한 재생된다.
    /// </summary>
    /// <param name="externalToken">외부 취소 토큰</param>
    Task MalfunctionSoundPlayAsync(CancellationToken externalToken = default);

    /// <summary>
    /// 조치보고 이벤트 사운드를 큐에 추가하여 재생한다.
    /// 기존 재생을 중지한 후 우선 재생된다.
    /// </summary>
    /// <param name="externalToken">외부 취소 토큰</param>
    Task ActionReportSoundPlayAsync(CancellationToken externalToken = default);

    /// <summary>
    /// 지정된 사운드 모델을 우선순위와 함께 큐에 스케줄링한다.
    /// 큐가 가득 차면 오래된 아이템이 자동으로 제거된다.
    /// </summary>
    /// <param name="soundModel">재생할 사운드 모델</param>
    /// <param name="priority">우선순위 (낮을수록 높은 우선순위, 기본값 0)</param>
    /// <param name="externalToken">외부 취소 토큰</param>
    Task PlayScheduleAsync(ISoundModel soundModel, int priority = 0, CancellationToken externalToken = default);

    /// <summary>
    /// 현재 재생 중인 모든 사운드를 중지하고 큐를 모두 비운다.
    /// 진행 중인 재생 작업들이 안전하게 취소될 때까지 대기한다.
    /// </summary>
    Task StopAllSoundsAsync();
}