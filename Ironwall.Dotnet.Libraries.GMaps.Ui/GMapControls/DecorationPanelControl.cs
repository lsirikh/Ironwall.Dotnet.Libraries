using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 「꾸미기」 판(2026-10-21) — 위쪽 도구 줄(나무 한 그루 · 나무 줄 · 나무 영역 · 바닥 · 소품 · 담장) · 값 칸 · 목록 · [적용] · [다시 놓기] · [지우기] ·
/// [되돌리기] · [다시 하기] · [모두 지우기…]. 크롬 · 머리 띠 끌기 · 판 안 키(↑/↓ = 소품 방향, Ctrl+Z, Esc)는 「건물 만들기」 판과 같은 공용 바탕
/// (HeightPanelControlBase — 높이 손잡이는 없다). 스타일은 Themes/DecorationPanelStyle.xaml(x:Name 없이 AutomationId).
/// </summary>
public class DecorationPanelControl : HeightPanelControlBase
{
    static DecorationPanelControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DecorationPanelControl), new FrameworkPropertyMetadata(typeof(DecorationPanelControl)));
    }

    public DecorationPanelControl() : base("Map.Decoration.Panel", "꾸미기") { }

    protected override string HandleAutomationId => "Map.Decoration.Handle";
}
