namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>이벤트 창 닫기 조건(FR-15).</summary>
public sealed class WindowClosePolicy
{
    /// <summary>조치보고가 오면 닫는다 — 닫기 명령은 GIS 가 보낸다(호스트는 기록만).</summary>
    public bool CloseOnActionReport { get; init; } = true;

    /// <summary>0 이하이면 타이머 없음. 창을 만지면 다시 센다(T-05).</summary>
    public int TimeoutSeconds { get; init; }

    /// <summary>📌 고정 — 자동으로 닫지 않는다.</summary>
    public bool Pinned { get; init; }
}
