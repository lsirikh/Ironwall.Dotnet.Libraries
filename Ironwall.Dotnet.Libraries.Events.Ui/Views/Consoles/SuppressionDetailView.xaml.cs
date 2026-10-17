using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Consoles;

/// <summary>억제 스케줄 상세 칸(SB L2734-2740). 뷰는 배선만 한다.</summary>
public partial class SuppressionDetailView : UserControl
{
    public SuppressionDetailView()
    {
        InitializeComponent();
    }

    private SuppressionConsoleViewModel? Model => DataContext as SuppressionConsoleViewModel;

    // 동작 버튼은 커널 적용 막대로 옮겼다 — 배선은 EventDashboardView 가 한다.
}
