using System.Windows;
using System.Windows.Controls;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Panels;

/// <summary>권한그룹 한시부여(Grant Scheduling) 관리 패널 뷰 — T4/FR-GS-06.</summary>
public partial class GrantManagementPanelView : UserControl
{
    public GrantManagementPanelView()
    {
        InitializeComponent();
        // item3: 콘솔이 모든 탭을 한 번만 활성화(eager)하므로, 탭 전환으로 이 패널이 다시 보일 때
        //   계정/그룹/부여목록을 재조회 → 권한설정서 새로 추가한 그룹이 부여 탭 콤보에 자동 반영(갱신버튼 없이).
        IsVisibleChanged += async (_, e) =>
        {
            if (e.NewValue is true && DataContext is GrantManagementPanelViewModel vm)
                await vm.OnClickReloadButton();
        };
    }
}
