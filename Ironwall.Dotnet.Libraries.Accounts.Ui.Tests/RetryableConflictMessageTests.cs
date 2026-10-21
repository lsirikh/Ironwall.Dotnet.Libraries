using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;   // RecordingEventAggregator / RecordingLog
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Sessions; // TestUserSessionPanel / FakeTokenStore
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests;

/****************************************************************************
   Purpose      : G01 — 409 중 동시 쓰기 경합(v8.0.15 details.retryable=true)은 고정 문구(중복 이름 · 마지막 관리자)
                  보다 먼저 "잠시 뒤 다시" 안내를 보인다. details 가 null 인 옛 409(6.3 · 8.0.11)는 종전 문구 그대로.
   Created By   : Claude
   Created On   : 2026-10-21
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class RetryableConflictMessageTests
{
    private static ApiResponse<T> Conflict<T>(bool retryable)
    {
        var res = ApiResponse<T>.CreateError(ApiErrorCodes.Conflict, "conflict");
        res.StatusCode = 409;
        res.Error!.DetailsToken = retryable ? JObject.Parse(@"{""retryable"":true}") : null;
        return res;
    }

    // ── 세션 강제 종료(UserSessionPanelViewModel) ──

    [Trait("Scenario", "GOP815-G01")]
    [Theory]
    [InlineData(true, ApiStatusHelper.RetryableConflictMessage)]
    [InlineData(false, "마지막으로 남은 관리자 세션은 종료할 수 없습니다. 다른 관리자가 로그인한 뒤 다시 시도하세요.")]
    public async Task should_pick_retry_text_only_when_session_conflict_is_retryable(bool retryable, string expected)
    {
        var api = new Mock<IAccountApiService>();
        api.Setup(a => a.ForceLogoutSessionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Conflict<object>(retryable));
        var events = new RecordingEventAggregator();
        var vm = new TestUserSessionPanel(events, new RecordingLog(), api.Object, new FakeTokenStore());

        await vm.HandleAsync(new CallForceLogoutSessionMessageModel { Session = new UserSessionDto { Id = 3, IsActive = true } }, CancellationToken.None);

        Assert.Equal(expected, events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
    }

    // ── 권한 그룹 이름 저장(PermissionMatrixPanelViewModel) ──

    [Trait("Scenario", "GOP815-G01")]
    [Theory]
    [InlineData(true, ApiStatusHelper.RetryableConflictMessage)]
    [InlineData(false, "같은 이름의 권한 그룹이 이미 있습니다. 다른 이름을 입력해 주세요.")]
    public async Task should_pick_retry_text_only_when_group_conflict_is_retryable(bool retryable, string expected)
    {
        var api = new Mock<IAccountApiService>();
        api.Setup(a => a.CreateUserGroupAsync(It.IsAny<UserGroupCreateDto>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(Conflict<UserGroupDto>(retryable));
        var events = new RecordingEventAggregator();
        var vm = new PermissionMatrixPanelViewModel(events, new RecordingLog(), api.Object);

        vm.OnClickNewGroup();
        vm.FormName = "야간조";
        await vm.ClickSaveGroupForm();

        Assert.Equal(expected, events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
        Assert.True(vm.IsGroupFormOpen);   // 실패면 폼을 둔다
    }
}
