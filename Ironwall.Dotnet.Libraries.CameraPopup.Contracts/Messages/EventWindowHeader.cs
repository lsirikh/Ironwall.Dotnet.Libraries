namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>이벤트 창 머리 정보(FR-16). 문구는 GIS 가 만든 그대로 표시한다 — 호스트는 번역 · 조회를 하지 않는다.</summary>
public sealed class EventWindowHeader
{
    /// <summary>배지 글자(예 "탐지" · "장애"). 비면 호스트가 종류로 채운다.</summary>
    public string? KindLabel { get; init; }

    /// <summary>구역 · 그룹 이름(예 "구역-07").</summary>
    public string? ZoneName { get; init; }

    /// <summary>장비 이름(예 "펜스 센서 #104").</summary>
    public string? DeviceName { get; init; }

    /// <summary>이벤트 종류 문구(예 "침입").</summary>
    public string? EventTypeText { get; init; }

    /// <summary>발생 시각(현지 오프셋 포함).</summary>
    public DateTimeOffset? OccurredAt { get; init; }
}
