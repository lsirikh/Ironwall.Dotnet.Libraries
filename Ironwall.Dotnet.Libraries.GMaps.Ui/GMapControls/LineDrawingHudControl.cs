using System;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/****************************************************************************
   Purpose      : 라인/구역/PIDS 그룹 드로잉 중 지도 위에 뜨는 플로팅 HUD 컨트롤.
                  표준 오버레이 패널 패턴(시안 헤더 + 우상단 X 닫기 + 다크/라이트 카드)을
                  재사용한다. 스타일은 Themes/LineDrawingHudStyle.xaml(Generic.xaml 병합).
   Created By   : GHLee
   Created On   : 7/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
 ****************************************************************************/

/// <summary>
/// 드로잉 HUD. <see cref="LineDrawingAdorner"/>가 인스턴스를 만들어 지도 위에 호스팅한다.
/// 헤더 드래그(이동)와 위치 지속 로직은 어도너가 소유하며, 이 컨트롤은
/// 순수 표시(타이틀/상태/버튼) + 버튼 클릭 이벤트 노출만 담당한다.
/// </summary>
public class LineDrawingHudControl : Control
{
    #region Static Constructor
    static LineDrawingHudControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LineDrawingHudControl),
            new FrameworkPropertyMetadata(typeof(LineDrawingHudControl)));
    }
    #endregion

    #region Dependency Properties

    /// <summary>헤더 타이틀 (예: "라인 그리기" / "구역 그리기" / "PIDS 그룹 그리기").</summary>
    public string HudTitle
    {
        get => (string)GetValue(HudTitleProperty);
        set => SetValue(HudTitleProperty, value);
    }
    public static readonly DependencyProperty HudTitleProperty =
        DependencyProperty.Register(nameof(HudTitle), typeof(string),
            typeof(LineDrawingHudControl), new PropertyMetadata("그리기"));

    /// <summary>점 개수 칩 텍스트 (예: "3 점").</summary>
    public string PointText
    {
        get => (string)GetValue(PointTextProperty);
        set => SetValue(PointTextProperty, value);
    }
    public static readonly DependencyProperty PointTextProperty =
        DependencyProperty.Register(nameof(PointText), typeof(string),
            typeof(LineDrawingHudControl), new PropertyMetadata(string.Empty));

    /// <summary>총 거리 텍스트 (예: "151.9 m", 점 2개 미만이면 "—").</summary>
    public string DistanceText
    {
        get => (string)GetValue(DistanceTextProperty);
        set => SetValue(DistanceTextProperty, value);
    }
    public static readonly DependencyProperty DistanceTextProperty =
        DependencyProperty.Register(nameof(DistanceText), typeof(string),
            typeof(LineDrawingHudControl), new PropertyMetadata("—"));

    /// <summary>완료 버튼 활성 여부 (점 2개 이상).</summary>
    public bool CanComplete
    {
        get => (bool)GetValue(CanCompleteProperty);
        set => SetValue(CanCompleteProperty, value);
    }
    public static readonly DependencyProperty CanCompleteProperty =
        DependencyProperty.Register(nameof(CanComplete), typeof(bool),
            typeof(LineDrawingHudControl), new PropertyMetadata(false));

    #endregion

    #region Events

    /// <summary>완료 버튼(✓) 클릭 — 그리기 확정.</summary>
    public event EventHandler? CompleteRequested;
    /// <summary>되돌리기 버튼(↶) 클릭 — 마지막 점 취소.</summary>
    public event EventHandler? UndoRequested;
    /// <summary>헤더 우상단 X 클릭 — 그리기 종료(취소).</summary>
    public event EventHandler? CloseRequested;

    #endregion

    #region Template

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("PART_CompleteButton") is Button complete)
            complete.Click += (_, e) => { e.Handled = true; CompleteRequested?.Invoke(this, EventArgs.Empty); };

        if (GetTemplateChild("PART_UndoButton") is Button undo)
            undo.Click += (_, e) => { e.Handled = true; UndoRequested?.Invoke(this, EventArgs.Empty); };

        if (GetTemplateChild("PART_CloseButton") is Button close)
            close.Click += (_, e) => { e.Handled = true; CloseRequested?.Invoke(this, EventArgs.Empty); };
    }

    #endregion
}
