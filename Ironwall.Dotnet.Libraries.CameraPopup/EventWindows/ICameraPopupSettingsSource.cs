using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

namespace Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;

/****************************************************************************
   Purpose      : 지금 저장된 카메라 팝업 설정을 읽는 창구 (PRD camera-popup-modes §3 설정 모델)
   Created By   : Claude (T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 창 관리자 · 탐지 트리거가 <b>매번</b> 읽는 설정(설정 콘솔에서 저장하면 다음 이벤트부터 반영).
/// 이 라이브러리는 LibVLC 를 품은 Streaming 프로젝트를 참조하지 않으므로 <c>IStreamingSetupModel</c> 을 모른다 —
/// 호스트가 <c>new CameraPopupModule(options, () =&gt; ((IStreamingSetupModel)setup).CameraPopupSettings)</c> 로 이어 준다.
/// 이어 주지 않으면 <see cref="Disabled"/>(사용 안 함) — 이벤트 창이 뜨지 않는다(안전한 쪽).
/// </summary>
public interface ICameraPopupSettingsSource
{
    CameraPopupSettings Current { get; }
}

/// <summary>대리자로 읽는 설정 창구. 읽기 실패는 부르는 쪽(관리자)이 잡는다.</summary>
public sealed class DelegateCameraPopupSettingsSource : ICameraPopupSettingsSource
{
    private readonly Func<CameraPopupSettings> _read;

    public DelegateCameraPopupSettingsSource(Func<CameraPopupSettings> read)
        => _read = read ?? throw new ArgumentNullException(nameof(read));

    public CameraPopupSettings Current => _read();

    /// <summary>사용 안 함 — 설정을 이어 주지 않은 조립에서 쓴다.</summary>
    public static ICameraPopupSettingsSource Disabled { get; } =
        new DelegateCameraPopupSettingsSource(() => new CameraPopupSettings { Mode = CameraPopupMode.None });
}
