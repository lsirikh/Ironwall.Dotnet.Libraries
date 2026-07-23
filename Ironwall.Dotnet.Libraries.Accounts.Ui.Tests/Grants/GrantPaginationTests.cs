using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;

/****************************************************************************
   Purpose   : GrantManagementPanelViewModel 전체 부여 무한스크롤 페이지네이션 검증.
   대상      : Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels.GrantManagementPanelViewModel
   서버계약  : FakeGopServer.GetAllGrantsAsync(created_at desc·page/size 슬라이스·top-level total).
   비고      : 헤드리스(Application.Current=null → DispatcherService.Invoke 인라인) 결정론. AuditLogPanelTests 미러.
                기본 EmitPaginationMeta=false(실서버 재현: total만 top-level) → VM이 total_pages 보정 계산.
****************************************************************************/
public class GrantPaginationTests
{
    /// <summary>표준 계정/그룹 시드 + N개의 ACTIVE 부여를 seed(상시). id/CreatedAt 은 순증(created_at desc 결정론).</summary>
    private static FakeGopServer NewServerWithGrants(int count)
    {
        var s = GrantFixtures.NewServer();   // 계정 3(id 1~3), 그룹 3(id 10~12)
        for (int i = 0; i < count; i++)
        {
            // CreatedAt 을 i 분 간격으로 증가시켜 서버 created_at desc 결정론 확보(그룹은 순환 배정).
            var groupId = 10 + (i % 3);
            s.Seed(userId: 2, groupId: groupId,
                   from: GrantFixtures.T0.AddHours(-1), until: null,
                   createdAt: GrantFixtures.T0.AddMinutes(-count + i));   // ACTIVE(상시)
        }
        return s;
    }

    [Fact]
    public async Task should_append_next_page_when_load_more_invoked()
    {
        // Arrange — 250건(3페이지: 100/100/50)
        var s = NewServerWithGrants(250);
        var (vm, _, _) = GrantFixtures.NewVm(s);
        await vm.ActivateForTestAsync();   // page 1(100)
        var afterFirst = vm.Grants.Count;

        // Act
        await vm.LoadNextGrantsPageAsync();   // page 2 append

        // Assert
        Assert.Equal(100, afterFirst);
        Assert.Equal(200, vm.Grants.Count);
        Assert.True(vm.HasMorePages);
    }

    [Fact]
    public async Task should_view_beyond_100_when_paginating()
    {
        // Arrange — 250건 (구 동작은 100건에서 절단 + 경고 팝업)
        var s = NewServerWithGrants(250);
        var (vm, ea, _) = GrantFixtures.NewVm(s);
        await vm.ActivateForTestAsync();

        // Act — 끝까지 스크롤(3페이지)
        await vm.LoadNextGrantsPageAsync();   // 200
        await vm.LoadNextGrantsPageAsync();   // 250

        // Assert — 100건 초과 표시 + 절단 경고 팝업 없음(무한 스크롤이 대체)
        Assert.Equal(250, vm.Grants.Count);
        Assert.False(vm.HasMorePages);
        Assert.DoesNotContain(ea.OfType<Ironwall.Dotnet.Libraries.ViewModel.Models.OpenInfoPopupMessageModel>(),
            m => (m.Explain ?? string.Empty).Contains("페이지네이션 필요"));
    }

    [Fact]
    public async Task should_reset_to_page_one_on_reload()
    {
        // Arrange — 250건, 2페이지까지 로드(200)
        var s = NewServerWithGrants(250);
        var (vm, _, _) = GrantFixtures.NewVm(s);
        await vm.ActivateForTestAsync();
        await vm.LoadNextGrantsPageAsync();
        Assert.Equal(200, vm.Grants.Count);

        // Act — 갱신 → 첫 페이지로 리셋
        await vm.OnClickReloadButton();

        // Assert
        Assert.Equal(100, vm.Grants.Count);
        Assert.True(vm.HasMorePages);
    }

    [Fact]
    public async Task should_stop_loading_when_last_page_reached()
    {
        // Arrange — 150건(2페이지: 100/50)
        var s = NewServerWithGrants(150);
        var (vm, _, _) = GrantFixtures.NewVm(s);
        await vm.ActivateForTestAsync();   // page 1(100)

        // Act
        await vm.LoadNextGrantsPageAsync();   // page 2(50) → 150

        // Assert
        Assert.Equal(150, vm.Grants.Count);
        Assert.False(vm.HasMorePages);
    }
}
