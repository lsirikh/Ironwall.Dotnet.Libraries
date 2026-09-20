using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// <b>진짜 전송 경로</b>를 탄다 — 카드 뷰모델 → 멱등 가드 → <c>CreateActionEventAsync</c> 까지.
/// 순수 함수만 덮으면 "적용 N" 이 거짓말하던 R1 같은 결함을 못 잡는다(N-07 적대 검토 R11).
/// </summary>
[Collection("IoC-Dependent")]   // IoC.GetInstance 는 전역 정적 — 기존 규약대로 직렬화한다
public class ActionSendPathTests : IDisposable
{
    private readonly List<ActionEventCreateDto> _sent = new();
    private readonly Mock<IEventApiService> _api = new(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
    private readonly ActionReportGuard _guard = new();
    private bool _serverAccepts = true;

    public ActionSendPathTests()
    {
        _api.Setup(a => a.CreateActionEventAsync(It.IsAny<ActionEventCreateDto>(), It.IsAny<CancellationToken>()))
            .Returns((ActionEventCreateDto dto, CancellationToken _) =>
            {
                _sent.Add(dto);
                return Task.FromResult(_serverAccepts
                    ? new ApiResponse<ActionEventDto> { Success = true, Data = new ActionEventDto { Id = 9000 + _sent.Count } }
                    : new ApiResponse<ActionEventDto> { Success = false, Message = "서버가 거절했습니다" });
            });

        var account = new Mock<IAccountModel>();
        account.SetupGet(a => a.Name).Returns("미리보기");

        var events = new EventAggregator();
        var log = new Mock<ILogService>().Object;

        IoC.GetInstance = (type, _) =>
            type == typeof(IEventAggregator) ? events
            : type == typeof(ILogService) ? log
            : type == typeof(IEventApiService) ? _api.Object
            : type == typeof(IActionReportGuard) ? _guard
            : type == typeof(IAccountModel) ? account.Object
            : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new Caliburn.Micro.DefaultPlatformProvider();
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    private DetectionEventCardViewModel DetectionCard(int id)
        => new(IoC.Get<IEventAggregator>(), IoC.Get<ILogService>(), TestEvents.Detection(id));

    private MalfunctionEventCardViewModel MalfunctionCard(int id)
        => new(IoC.Get<IEventAggregator>(), IoC.Get<ILogService>(), TestEvents.Malfunction(id));

    [Fact]
    public async Task should_post_the_expected_body_when_sending_a_detection_report()
    {
        var result = await DetectionCard(41).SendActionDetailed("현장 확인 결과 이상 없음", "김상병(21-70001)");

        Assert.Equal(ActionSendOutcome.Created, result.Outcome);
        Assert.Single(_sent);
        Assert.Equal(41, _sent[0].FromEventId);
        Assert.Equal("현장 확인 결과 이상 없음", _sent[0].Content);
        Assert.False(string.IsNullOrWhiteSpace(_sent[0].User));
        // ⚠ 와이어에는 원본 종류 식별자가 없다 — 서버가 from_event_id 로 찾는다.
        Assert.Equal("Action", _sent[0].TypeEvent);
    }

    [Fact]
    public async Task should_report_failed_when_the_server_rejects()
    {
        _serverAccepts = false;

        var result = await DetectionCard(42).SendActionDetailed("순찰 인원 출동", "user");

        Assert.Equal(ActionSendOutcome.Failed, result.Outcome);
        Assert.Contains("거절", result.Reason);
    }

    [Fact]
    public async Task should_report_skipped_and_send_nothing_when_the_guard_already_holds_the_event()
    {
        // 다른 경로가 같은 이벤트를 쥐고 있다(자동 조치보고 등).
        Assert.True(_guard.TryEnter(43));

        var result = await DetectionCard(43).SendActionDetailed("오경보", "user");

        Assert.Equal(ActionSendOutcome.GuardSkipped, result.Outcome);
        Assert.Empty(_sent);                                    // 아무것도 만들지 않았다
        Assert.False(result.CanCloseDialog is false);           // 다이얼로그는 닫아도 된다(지금 동작 보존)
    }

    [Fact]
    public async Task should_not_block_a_malfunction_when_a_detection_with_the_same_id_is_in_flight()
    {
        // (R1) 자물쇠가 Id 하나였을 때 탐지 3번이 장애 3번을 막았고, 트레이는 그것을 "적용" 으로 셌다.
        Assert.True(_guard.TryEnter(ActionReportKind.Detection, 3));

        var result = await MalfunctionCard(3).SendActionDetailed("울타리 점검/작업", "user");

        Assert.Equal(ActionSendOutcome.Created, result.Outcome);
        Assert.Single(_sent);
        Assert.Equal(3, _sent[0].FromEventId);
    }

    [Fact]
    public async Task should_block_the_same_kind_and_id_when_it_is_already_in_flight()
    {
        Assert.True(_guard.TryEnter(ActionReportKind.Detection, 7));

        var result = await DetectionCard(7).SendActionDetailed("오경보", "user");

        Assert.Equal(ActionSendOutcome.GuardSkipped, result.Outcome);
        Assert.Empty(_sent);
    }

    [Fact]
    public async Task should_release_the_lock_when_the_send_finishes()
    {
        await DetectionCard(8).SendActionDetailed("오경보", "user");

        // 끝났으면 다음 보고가 들어갈 수 있어야 한다 — finally 에서 풀지 않으면 영원히 막힌다.
        Assert.True(_guard.TryEnter(ActionReportKind.Detection, 8));
    }

    [Fact]
    public async Task should_keep_the_legacy_bool_contract_for_the_dialog()
    {
        // 다이얼로그는 아직 bool 을 본다 — 가드 스킵은 true(닫기 허용), 서버 거절은 false.
        Assert.True(await DetectionCard(9).SendAction("오경보", "user"));

        _serverAccepts = false;
        Assert.False(await DetectionCard(10).SendAction("오경보", "user"));
    }
}

/// <summary>멱등 가드의 키 계약(R1).</summary>
public class ActionReportGuardTests
{
    [Fact]
    public void should_separate_detection_and_malfunction_when_ids_collide()
    {
        var guard = new ActionReportGuard();

        Assert.True(guard.TryEnter(ActionReportKind.Detection, 3));
        Assert.True(guard.TryEnter(ActionReportKind.Malfunction, 3));   // 서로를 막지 않는다
        Assert.False(guard.TryEnter(ActionReportKind.Detection, 3));    // 같은 종류는 막는다
    }

    [Fact]
    public void should_block_every_kind_when_a_legacy_caller_holds_the_id()
    {
        // 자동 · 자동복구 · 배치 경로는 아직 Id 로만 잠근다 — 그 보호를 잃지 않는다.
        var guard = new ActionReportGuard();

        Assert.True(guard.TryEnter(5));
        Assert.False(guard.TryEnter(ActionReportKind.Detection, 5));
        Assert.False(guard.TryEnter(ActionReportKind.Malfunction, 5));

        guard.Exit(5);
        Assert.True(guard.TryEnter(ActionReportKind.Detection, 5));
    }

    [Fact]
    public void should_block_a_legacy_caller_when_a_typed_caller_holds_that_id()
    {
        var guard = new ActionReportGuard();

        Assert.True(guard.TryEnter(ActionReportKind.Detection, 6));
        Assert.False(guard.TryEnter(6));
    }

    [Fact]
    public void should_ignore_non_positive_ids()
    {
        var guard = new ActionReportGuard();

        Assert.True(guard.TryEnter(0));
        Assert.True(guard.TryEnter(0));
        Assert.True(guard.TryEnter(ActionReportKind.Detection, -1));
    }
}
