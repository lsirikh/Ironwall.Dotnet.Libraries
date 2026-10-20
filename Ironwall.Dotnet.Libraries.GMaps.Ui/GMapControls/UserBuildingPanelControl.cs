using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 「건물 만들기」 판(2026-10-19) — 위쪽 형상 도구 줄(사각형 · 원 · 타원 · 다각형)로 지도에 윤곽을 그리고, 아래쪽 건물 설정
/// (이름 · 층수 · 높이 · 층고 · ±1층 · 들어올리기 손잡이 · 지붕 모양 · 외벽 성격)으로 저장 · 지우기.
/// 크롬 · 손잡이 · 판 안 키는 「건물 높이」 판과 같은 공용 바탕(HeightPanelControlBase) — 여기는 도구 단축키(R · C · E · P, 판에 초점 ·
/// 입력칸 밖)만 더한다. 스타일은 Themes/UserBuildingPanelStyle.xaml(x:Name 없이 AutomationId).
/// </summary>
public class UserBuildingPanelControl : HeightPanelControlBase
{
    static UserBuildingPanelControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(UserBuildingPanelControl),
            new FrameworkPropertyMetadata(typeof(UserBuildingPanelControl)));
    }

    public UserBuildingPanelControl() : base("Map.UserBuilding.Panel", "건물 만들기") { }

    protected override string HandleAutomationId => "Map.UserBuilding.Handle";

    /// <summary>도구 고르기 명령(매개변수 = "Rect" · "Circle" · "Ellipse" · "Polygon") — 단축키가 부른다.</summary>
    public ICommand? ToolCommand { get; set; }

    /// <summary>판 단축키 — R 사각형 · C 원 · E 타원 · P 다각형(입력칸 안 · 꾸밈키가 있으면 글자 그대로).</summary>
    public static string? ToolForKey(Key key) => key switch
    {
        Key.R => "Rect",
        Key.C => "Circle",
        Key.E => "Ellipse",
        Key.P => "Polygon",
        _ => null,
    };

    protected override bool HandleExtraKey(KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox or ComboBox or ComboBoxItem || Keyboard.Modifiers != ModifierKeys.None || ToolForKey(e.Key) is not { } tool) return false;
        if (ToolCommand?.CanExecute(tool) != true) return false;
        ToolCommand.Execute(tool);
        return true;
    }
}
