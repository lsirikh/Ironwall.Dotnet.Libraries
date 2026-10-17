using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;

// ⚠ 네임스페이스는 옛 자리(Streaming.Models) 그대로다 — 폴더와 다르다.
//   이 계약은 LibVLC 를 품은 Streaming 어셈블리에 살았고, GIS 호스트는 이 인터페이스 하나 때문에 Streaming 을
//   참조해 LibVLC 네이티브를 출력에 끌어들였다(camera-popup-modes T-02a 가 남긴 숙제 → T-07).
//   형식만 WPF · LibVLC 없는 Streaming.Base 로 옮기고 이름공간을 지켜, 쓰는 쪽(호스트 · Streaming · 시험)의
//   using 을 한 줄도 바꾸지 않는다. 구현(StreamingSetupModel)은 Streaming 에 남는다.
namespace Ironwall.Dotnet.Libraries.Streaming.Models;
/****************************************************************************
   Purpose      : 스트리밍 · 카메라 팝업 라이브 설정 계약
   Created By   : GHLee
   Created On   : 9/24/2025 3:09:32 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 스트리밍 설정 모델 인터페이스 (전체 서비스 설정)
/// </summary>
public interface IStreamingSetupModel
{
    int MaxConnections { get; set; }
    int MaxRetryAttempts { get; set; }
    long MaxMemoryUsageBytes { get; set; }
    bool EnableDebugLogging { get; set; }
    string LogPath { get; set; }
    string SnapshotPath { get; set; }
    bool UseHardwareAcceleration { get; set; }
    int DefaultNetworkCaching { get; set; }
    int ContextPoolSize { get; set; }
    bool EnableFrameSkipping { get; set; }
    int MaxFrameSkip { get; set; }
    bool IsAutoDiscard { get; set; }
    int TimeoutSeconds { get; set; }
    int ClockJitterMs { get; set; }
    bool EnableClockSync { get; set; }
    int PopupStartupDelayMs { get; set; }
    int QueueMinDisplayMs { get; set; }

    /// <summary>맵 카메라 팝업 연동 on/off — 옛 키. 새 설정은 <see cref="CameraPopupSettings"/>.Mode(사용 안 함 = false).</summary>
    bool IsCameraPopupUsed { get; set; }

    /// <summary>맵 카메라 팝업 RTSP 소스(CameraPopup_RtspSource_Priority FR-01):
    /// <see cref="EnumCameraPopupRtspSource.Url"/>=수동 URL(현행 기본) / <see cref="EnumCameraPopupRtspSource.Onvif"/>=ONVIF GetStreamUri 조회.
    /// default 구현 제공 — 미반영 소비자(메인 SetupModel)도 컴파일되고 Url 모드로 동작(라이브러리 자립, NFR-03).</summary>
    EnumCameraPopupRtspSource CameraPopupRtspSource { get => EnumCameraPopupRtspSource.Url; set { } }

    /// <summary>
    /// 카메라 팝업 설정 한 벌(camera-popup-modes PRD FR-01 · §3) — 모드 · 제공자 · 이벤트 창 · 브로커.
    /// 읽기 전용 보기다. default 구현은 새 키 없이 옛 키에서 이관한 값(<c>IsCameraPopupUsed</c> → 모드,
    /// <c>CameraPopupRtspSource</c> → 제공자) — 새 키를 모르는 소비자도 컴파일되고 지금과 같은 동작을 본다.
    /// 쓰기는 설정 화면(호스트)이 한다.
    /// </summary>
    CameraPopupSettings CameraPopupSettings
        => CameraPopupSettingsCodec.Resolve(null, IsCameraPopupUsed, CameraPopupRtspSource, IsAutoDiscard, TimeoutSeconds);
}
