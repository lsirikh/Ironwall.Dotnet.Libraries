using Caliburn.Micro;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;

/// <summary>
/// 개요가 쓰는 계열 — 서버 요약/추이의 집계 키 그대로.
/// </summary>
/// <remarks>
/// 정본 window-layout-system-storyboard.html L2317-2323. <b>색이 아니라 형태로</b> 구분한다 —
/// 카메라 탐지는 같은 탐지 계열의 빗금/파선이다(라이트 테마에서 주색 · 선택색 · 포커스색이 같은 색이라 색으로는 못 가른다).
/// </remarks>
public sealed record EventSeriesSpec(string Key, string Name, string BrushKey, bool IsHatched, string? DashArray, bool InTotal,
                                     EventSwatchShape Swatch = EventSwatchShape.Solid)
{
    public static readonly EventSeriesSpec Sensor = new("sensor", "센서 탐지", "StatusWarningBrush", false, null, true);
    public static readonly EventSeriesSpec Camera = new("camera", "카메라 탐지", "AccentBrush", true, "6 3", true, EventSwatchShape.Hatched);
    public static readonly EventSeriesSpec Malfunction = new("mal", "장애", "StatusCriticalBrush", false, null, true);
    public static readonly EventSeriesSpec Connection = new("con", "연결", "PrimaryBrush", false, null, true);
    public static readonly EventSeriesSpec Action = new("act", "조치", "StatusNormalBrush", false, null, true);

    /// <summary>
    /// 사전 경보(접근, <c>type_event=Alert</c>) — 서버는 침입과 따로 세고 <c>total</c> 에 넣는다(detection-alert FR-04).
    /// 센서 탐지와 <b>색도 형태도</b> 다르다 — 정보색 · 속이 빈 윤곽(칩 · 막대), 추이는 점선.
    /// 예전엔 센서 탐지와 같은 경고색에 빗금만 달라 칩 크기에서 셋(센서 · 카메라 · 사전 경보)이 한 색으로 보였다(감사 E-3 #5).
    /// </summary>
    public static readonly EventSeriesSpec Alert = new("alert", "사전 경보", "StatusInfoBrush", false, "2 3", true, EventSwatchShape.Outline);

    /// <summary>
    /// 운영 이벤트(함체 · 통문 개폐, 환경 경보) — 서버가 <b>총계 밖</b>에서 따로 센다(X12).
    /// 조각 · 추이 계열이 아니라 함체·통문 막대의 한 칸으로만 쓴다(그래서 <see cref="All"/> 에 없다).
    /// 사전 경보가 정보색을 쓰므로 운영은 중립색(TextMuted)으로 비킨다 — 함체·통문 막대에 둘이 함께 실린다.
    /// </summary>
    public static readonly EventSeriesSpec Operation = new("op", "운영", "TextMutedBrush", false, null, false);

    /// <summary>표시 순서대로(총계에 드는 계열만).</summary>
    public static readonly IReadOnlyList<EventSeriesSpec> All = new[] { Sensor, Camera, Alert, Malfunction, Connection, Action };

    /// <summary>견본 · 막대 칸을 속이 빈 윤곽으로 그린다.</summary>
    public bool IsOutline => Swatch == EventSwatchShape.Outline;
}

/// <summary>
/// 칩 견본 · 비중 막대 · 장비 막대 칸의 <b>형태</b> — 색만으로 계열을 가르지 않는다(drag-first-ux.md "형태로 구분").
/// </summary>
public enum EventSwatchShape
{
    /// <summary>꽉 찬 면.</summary>
    Solid,
    /// <summary>사선 빗금.</summary>
    Hatched,
    /// <summary>속이 빈 윤곽.</summary>
    Outline,
}

/// <summary>도넛 한 조각 · 범례 한 줄.</summary>
public sealed class EventSliceViewModel : PropertyChangedBase
{
    private bool _isOn = true;

    public EventSliceViewModel(EventSeriesSpec spec, int count, double percent)
    {
        Spec = spec;
        Count = count;
        Percent = percent;
    }

    public EventSeriesSpec Spec { get; }
    public string Name => Spec.Name;
    public string BrushKey => Spec.BrushKey;
    public bool IsHatched => Spec.IsHatched;
    public bool IsOutline => Spec.IsOutline;
    public int Count { get; }
    public double Percent { get; }
    public string PercentText => Count > 0 ? $"{Percent:0}%" : "—";

    /// <summary>계열 칩으로 켜고 끈다.</summary>
    public bool IsOn { get => _isOn; set { _isOn = value; NotifyOfPropertyChange(); } }

    /// <summary>가로 비중 막대의 폭 비율(0~1).</summary>
    public double Ratio => Percent / 100.0;
}

/// <summary>장비별 막대 한 줄.</summary>
public sealed class EventDeviceBarViewModel
{
    public EventDeviceBarViewModel(string id, string name, int total, IReadOnlyList<EventBarSegment> segments)
    {
        Id = id;
        Name = name;
        Total = total;
        Segments = segments;
    }

    public string Id { get; }
    public string Name { get; }
    public int Total { get; }
    public IReadOnlyList<EventBarSegment> Segments { get; }
}

/// <summary>막대 한 칸 — 계열 하나의 몫.</summary>
public sealed record EventBarSegment(EventSeriesSpec Spec, int Value, double Ratio)
{
    public string BrushKey => Spec.BrushKey;
    public bool IsHatched => Spec.IsHatched;
    public bool IsOutline => Spec.IsOutline;
}

// 추이 선(과거 Polygon/Polyline 손그림 도형)과 x 축 눈금 뷰모델은 LiveChartsCore CartesianChart 로
// 옮기며 사라졌다 — 차트가 제 값(EventOverviewViewModel.Series/XAxes)으로 스스로 배치·그린다(2026-09-23).
