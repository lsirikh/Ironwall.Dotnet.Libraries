using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;   // FakeGopServer · RecordingEventAggregator · RecordingLog (동일 어셈블리 internal)
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.AuditLog;

/****************************************************************************
   Purpose   : AuditLogPanelViewModel 날짜필터 + 무한스크롤 페이지네이션 검증.
   대상      : Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels.AuditLogPanelViewModel
   서버계약  : FakeGopServer.GetAuditLogsAsync(날짜필터·id desc·page/limit 슬라이스).
   비고      : 헤드리스(Application.Current=null → DispatcherService.Invoke 인라인) 결정론.
****************************************************************************/

/// <summary>protected OnActivateAsync 를 테스트에 노출(초기 로드 경로 검증).</summary>
internal sealed class TestAuditLogPanel : AuditLogPanelViewModel
{
    public TestAuditLogPanel(IEventAggregator ea, ILogService log, IAccountApiService api) : base(ea, log, api) { }
    public Task ActivateForTestAsync() => OnActivateAsync(CancellationToken.None);
}

public class AuditLogPanelTests
{
    private static FakeGopServer NewServerWithLogs(int count, DateTime baseDate)
    {
        var s = new FakeGopServer();
        for (int i = 1; i <= count; i++)
            s.AuditLogs.Add(new AuditLogDto
            {
                Id = i,
                ActionType = "USER_CREATED",
                ActionStatus = "SUCCESS",
                ResourceType = "USER",
                ActorLoginId = "admin",
                CreatedAt = baseDate.AddMinutes(i).ToString("yyyy-MM-ddTHH:mm:ss")
            });
        return s;
    }

    private static TestAuditLogPanel NewVm(FakeGopServer s)
        => new(new RecordingEventAggregator(), new RecordingLog(), s);

    [Fact]
    public async Task should_send_start_and_end_date_when_search_clicked()
    {
        // Arrange
        var s = NewServerWithLogs(5, new DateTime(2026, 7, 15));
        var vm = NewVm(s);
        vm.StartDate = new DateTime(2026, 7, 13);
        vm.EndDate = new DateTime(2026, 7, 20);

        // Act
        await vm.OnClickSearch();

        // Assert — ISO 8601, 시작=당일 00:00
        Assert.Equal("2026-07-13T00:00:00", s.LastAuditStartDate);
        Assert.Equal("2026-07-20T23:59:59", s.LastAuditEndDate);
    }

    [Fact]
    public async Task should_send_inclusive_end_of_day_when_end_date_selected()
    {
        // Arrange
        var s = NewServerWithLogs(1, new DateTime(2026, 7, 15));
        var vm = NewVm(s);
        vm.EndDate = new DateTime(2026, 7, 20, 9, 30, 0);   // 시각 성분은 당일 끝(23:59:59)으로 상향

        // Act
        await vm.OnClickSearch();

        // Assert
        Assert.StartsWith("2026-07-20", s.LastAuditEndDate);
        Assert.EndsWith("T23:59:59", s.LastAuditEndDate);
    }

    [Fact]
    public async Task should_append_next_page_when_load_more_invoked()
    {
        // Arrange — 250건(3페이지: 100/100/50)
        var s = NewServerWithLogs(250, new DateTime(2026, 7, 1));
        var vm = NewVm(s);
        vm.StartDate = new DateTime(2026, 6, 1);
        vm.EndDate = new DateTime(2026, 8, 1);
        await vm.ActivateForTestAsync();   // 초기 로드 = page 1(100건)
        var afterFirst = vm.Items.Count;

        // Act
        await vm.LoadNextPageAsync();       // page 2 append

        // Assert
        Assert.Equal(100, afterFirst);
        Assert.Equal(200, vm.Items.Count);
        Assert.True(vm.HasMorePages);       // 아직 3페이지 남음
    }

    [Fact]
    public async Task should_stop_loading_when_last_page_reached()
    {
        // Arrange — 150건(2페이지: 100/50)
        var s = NewServerWithLogs(150, new DateTime(2026, 7, 1));
        var vm = NewVm(s);
        vm.StartDate = new DateTime(2026, 6, 1);
        vm.EndDate = new DateTime(2026, 8, 1);
        await vm.ActivateForTestAsync();    // page 1(100)

        // Act
        await vm.LoadNextPageAsync();       // page 2(50) → 150

        // Assert
        Assert.Equal(150, vm.Items.Count);
        Assert.False(vm.HasMorePages);
    }

    [Fact]
    public async Task should_not_load_next_page_when_no_more_pages()
    {
        // Arrange — 30건(1페이지)
        var s = NewServerWithLogs(30, new DateTime(2026, 7, 1));
        var vm = NewVm(s);
        vm.StartDate = new DateTime(2026, 6, 1);
        vm.EndDate = new DateTime(2026, 8, 1);
        await vm.ActivateForTestAsync();
        var callsAfterLoad = s.AuditCallCount;

        // Act — 더 없음 → 가드로 서버 미호출
        await vm.LoadNextPageAsync();

        // Assert
        Assert.False(vm.HasMorePages);
        Assert.Equal(callsAfterLoad, s.AuditCallCount);
    }

    [Fact]
    public async Task should_reset_to_first_page_when_reload()
    {
        // Arrange — 250건, 2페이지까지 로드(200)
        var s = NewServerWithLogs(250, new DateTime(2026, 7, 1));
        var vm = NewVm(s);
        vm.StartDate = new DateTime(2026, 6, 1);
        vm.EndDate = new DateTime(2026, 8, 1);
        await vm.ActivateForTestAsync();
        await vm.LoadNextPageAsync();
        Assert.Equal(200, vm.Items.Count);

        // Act — 갱신 → 첫 페이지로 리셋
        await vm.OnClickReloadButton();

        // Assert
        Assert.Equal(100, vm.Items.Count);
        Assert.True(vm.HasMorePages);
    }

    [Fact]
    public async Task should_not_reload_when_already_loading()
    {
        // Arrange — 250건(다중 페이지), 초기 로드 후 in-flight 상태 강제
        var s = NewServerWithLogs(250, new DateTime(2026, 7, 1));
        var vm = NewVm(s);
        vm.StartDate = new DateTime(2026, 6, 1);
        vm.EndDate = new DateTime(2026, 8, 1);
        await vm.ActivateForTestAsync();    // page 1(100)
        vm.IsLoadingMore = true;            // in-flight 강제 (setter가 가드 필드 _isLoadingMore 기록)
        var before = s.AuditCallCount;

        // Act — 재진입 시도
        await vm.LoadNextPageAsync();

        // Assert — 가드로 단락, 서버 미호출
        Assert.Equal(before, s.AuditCallCount);
    }
}
