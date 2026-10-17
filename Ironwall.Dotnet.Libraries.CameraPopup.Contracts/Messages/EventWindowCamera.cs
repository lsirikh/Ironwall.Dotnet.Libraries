namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>이벤트 창 타일 하나 — 카메라 · 제공자(계정 포함) · PTZ 가능/권한 · 자동 이동 프리셋(FR-13/14).</summary>
public sealed class EventWindowCamera
{
    public string CameraId { get; init; } = string.Empty;
    public string? Name { get; init; }

    /// <summary>영상 · PTZ 제공자. 계정은 같은 사용자 전용 파이프로만 오간다(ToString 은 가린다).</summary>
    public VideoProviderInfo Provider { get; init; } = new();

    /// <summary>장비가 PTZ 카메라인가(고정 카메라면 false).</summary>
    public bool IsPtz { get; init; }

    /// <summary>현재 사용자의 PTZ 권한. false 면 PTZ 항목 비활성 + "PTZ 권한 없음".</summary>
    public bool PtzAllowed { get; init; }

    /// <summary>창이 뜰 때 이동할 대상 프리셋(매핑). null 이면 이동 없음.</summary>
    public string? TargetPresetToken { get; init; }

    /// <summary>대상 프리셋 표시 이름(예 "P2"). 비면 토큰을 보인다.</summary>
    public string? TargetPresetName { get; init; }

    /// <summary>복귀 프리셋. null 이면 제공자의 Home.</summary>
    public string? HomePresetToken { get; init; }

    /// <summary>매핑 delay_time — 프리셋 이동 최소 시간(초). 이 동안 "P2 로 이동 중 · N초".</summary>
    public int DelaySeconds { get; init; }

    public override string ToString() => $"{CameraId}({Name}) ptz={IsPtz}/{PtzAllowed} target={TargetPresetToken ?? "-"} {Provider}";
}
