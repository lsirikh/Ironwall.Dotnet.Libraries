using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Models;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Modules;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests;

/****************************************************************************
   Purpose      : G08 (GOP API 8.0.15 대조 · 브로커 §9.19 · §9.20) — NATS SYNC_USER · SYNC_USER_GROUP 루프백 봉투 주입.
                  콘솔이 열려 있고 조회 권한이 있을 때만 500ms 창으로 모아 한 번 다시 읽는다 · 본인이면 권한 재평가 · DI 배선.
   Created By   : Claude
   Created On   : 2026-10-21
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class AccountSyncNatsServiceTests
{
    private sealed class Target : IAccountSyncTarget
    {
        public bool IsSyncTargetOpen { get; set; } = true;
        public readonly List<(bool Users, bool Groups)> Reloads = new();
        public Task ReloadFromServerAsync(bool users, bool groups) { Reloads.Add((users, groups)); return Task.CompletedTask; }
    }

    private sealed class Rig
    {
        public readonly Mock<INatsService> Nats = new();
        public readonly Mock<IAccountApiService> Api = new();
        public readonly Mock<IPermissionService> Permission = new();
        public readonly Mock<ITokenStorageService> Token = new();
        public readonly Target Target = new();
        public readonly AccountSyncNatsService Service;

        public Rig(bool canViewUsers = true, bool canViewGroups = true, string myUserId = "7")
        {
            Permission.Setup(p => p.CanView("users")).Returns(canViewUsers);
            Permission.Setup(p => p.CanView("user_groups")).Returns(canViewGroups);
            Token.SetupGet(t => t.IsAuthenticated).Returns(true);
            Token.SetupGet(t => t.UserId).Returns(myUserId);
            Service = new AccountSyncNatsService(null, Nats.Object, Api.Object, Permission.Object, Token.Object, () => Target,
                TimeSpan.FromMilliseconds(50));
            Service.StartService();
        }

        /// <summary>호스트 NATS 가 받은 것처럼 이벤트를 울린다(루프백 주입).</summary>
        public void Inject(string json)
            => Nats.Raise(n => n.NatsSubscribeEventAsync += null,
               new object[] { new MessageArgsModel("sensorway.global.all.sync.user", "sensorway.global.all.sync.user", json) });   // EventArgs 오버로드(sender, e)를 피한다

        public Task Settled() => Service.LastFlush ?? Task.CompletedTask;
    }

    private static string Envelope(string cmd, int id, string action = "UPDATED")
        => "{\"id\":\"x\",\"m_type\":\"PUB\",\"cmd\":\"" + cmd + "\",\"from\":\"DBApi\",\"body\":{\"action\":\"" + action + "\",\"resource_id\":" + id + "}}";

    [Trait("Scenario", "GOP815-G08")]
    [Fact]
    public async Task should_reload_once_for_a_burst_of_changes_when_the_console_is_open()
    {
        var rig = new Rig();

        rig.Inject(Envelope("SYNC_USER", 12));
        rig.Inject(Envelope("SYNC_USER", 13));
        rig.Inject(Envelope("SYNC_USER_GROUP", 3));
        await rig.Settled();

        var reload = Assert.Single(rig.Target.Reloads);
        Assert.True(reload.Users);
        Assert.True(reload.Groups);
    }

    [Trait("Scenario", "GOP815-G08")]
    [Fact]
    public async Task should_read_an_array_envelope_item_by_item()
    {
        var rig = new Rig();

        rig.Inject("[" + Envelope("SYNC_USER_GROUP", 3) + "," + Envelope("DETECT", 1) + "]");
        await rig.Settled();

        Assert.Equal((false, true), Assert.Single(rig.Target.Reloads));
    }

    [Trait("Scenario", "GOP815-G08")]
    [Fact]
    public async Task should_not_reload_when_the_console_is_closed()
    {
        var rig = new Rig();
        rig.Target.IsSyncTargetOpen = false;

        rig.Inject(Envelope("SYNC_USER", 12));
        await rig.Settled();

        Assert.Empty(rig.Target.Reloads);
    }

    [Trait("Scenario", "GOP815-G08")]
    [Fact]
    public async Task should_skip_what_the_operator_may_not_view()
    {
        var rig = new Rig(canViewUsers: false, canViewGroups: true);

        rig.Inject(Envelope("SYNC_USER", 12));
        await rig.Settled();
        Assert.Empty(rig.Target.Reloads);

        rig.Inject(Envelope("SYNC_USER", 12));
        rig.Inject(Envelope("SYNC_USER_GROUP", 3));
        await rig.Settled();
        Assert.Equal((false, true), Assert.Single(rig.Target.Reloads));
    }

    [Trait("Scenario", "GOP815-G08")]
    [Theory]
    [InlineData("SYNC_DEVICE")]
    [InlineData("sync_user")]            // 대문자 토큰 그대로만(브로커 §2.4)
    public async Task should_ignore_other_commands(string cmd)
    {
        var rig = new Rig();

        rig.Inject(Envelope(cmd, 12));
        await Task.Delay(120);

        Assert.Null(rig.Service.LastFlush);
        Assert.Empty(rig.Target.Reloads);
    }

    [Trait("Scenario", "GOP815-G08")]
    [Fact]
    public async Task should_ignore_everything_before_login()
    {
        var rig = new Rig();
        rig.Token.SetupGet(t => t.IsAuthenticated).Returns(false);

        rig.Inject(Envelope("SYNC_USER", 12));
        await Task.Delay(120);

        Assert.Null(rig.Service.LastFlush);
    }

    [Trait("Scenario", "GOP815-G08")]
    [Fact]
    public async Task should_reevaluate_my_permissions_when_my_own_account_changed()
    {
        var rig = new Rig(myUserId: "12");
        var snapshot = new PermissionsSnapshotDto();
        rig.Api.Setup(a => a.GetMyPermissionsAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiResponse<PermissionsSnapshotDto>.CreateSuccess(snapshot));

        rig.Inject(Envelope("SYNC_USER", 12));
        await rig.Settled();

        rig.Permission.Verify(p => p.Refresh(snapshot), Times.Once);
    }

    [Trait("Scenario", "GOP815-G08")]
    [Fact]
    public async Task should_not_touch_my_permissions_for_someone_else_or_on_403()
    {
        var rig = new Rig(myUserId: "12");
        var forbidden = ApiResponse<PermissionsSnapshotDto>.CreateError(ApiErrorCodes.Forbidden, "Insufficient permission");
        forbidden.StatusCode = 403;
        rig.Api.Setup(a => a.GetMyPermissionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(forbidden);

        rig.Inject(Envelope("SYNC_USER", 99));        // 남의 계정
        await rig.Settled();
        rig.Api.Verify(a => a.GetMyPermissionsAsync(It.IsAny<CancellationToken>()), Times.Never);

        rig.Inject(Envelope("SYNC_USER", 12));        // 내 계정인데 403 — 조용히 넘긴다
        await rig.Settled();
        rig.Permission.Verify(p => p.Refresh(It.IsAny<PermissionsSnapshotDto>()), Times.Never);
        Assert.Equal(2, rig.Target.Reloads.Count);           // 목록 재조회는 그대로(창마다 한 번)
    }

    // ── DI 배선 — 등록 · IService 노출 · 컨테이너가 서면 구독까지 ──

    private sealed class StubSetup : IAccountSetupModel, IMariaDbSetupModel
    {
        public bool IsSession { get; set; }
        public int SessionExpiration { get; set; } = 60;
        public string IpDbServer { get; set; } = "127.0.0.1";
        public int PortDbServer { get; set; } = 3306;
        public string DbDatabase { get; set; } = "test";
        public string UidDbServer { get; set; } = "test";
        public string PasswordDbServer { get; set; } = "test";
    }

    private sealed class StubApiSetup : Ironwall.Dotnet.Libraries.Api.Models.IApiSetupModel
    {
        public string Url { get; set; } = "https://127.0.0.1:8000/api";
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int Timeout { get; set; } = 10;
    }

    [Trait("Scenario", "GOP815-G08")]
    [Fact]
    public void should_register_the_sync_service_as_an_iservice_and_subscribe_when_the_container_is_built()
    {
        var nats = new Mock<INatsService>();
        var builder = new ContainerBuilder();
        builder.RegisterInstance<IEventAggregator>(new EventAggregator());
        builder.RegisterInstance(new Mock<ILogService>().Object).As<ILogService>();
        builder.RegisterInstance(nats.Object).As<INatsService>();
        builder.RegisterType<AccountModel>().AsImplementedInterfaces().SingleInstance();
        var setup = new StubSetup();
        builder.RegisterModule(new AccountUiModule(setup, setup, null, 10, useDbAuth: false, apiSetup: new StubApiSetup(), isSsoEnabled: false));

        using var c = builder.Build();

        Assert.Contains(c.Resolve<IEnumerable<Autofac.Features.Metadata.Meta<IService>>>(),
            s => s.Metadata.ContainsKey("Order") && s.Value is AccountSyncNatsService);
        nats.VerifyAdd(n => n.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>(), Times.AtLeastOnce());
    }
}
