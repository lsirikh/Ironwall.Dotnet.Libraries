using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;
/****************************************************************************
   Purpose      : GroupActionReportLauncher 단위 테스트 — 구역 심볼 더블클릭 조치보고.
                  GMap_PidsGroup_DoubleClick_ActionReport plan TEST-02(게이트)/
                  TEST-03(최선착 선정)/TEST-04(모델 해석 폴백) + SETUP-01(GetEntriesByGroup).
                  · CompositeStatus 게이트(FR-02 앞단)는 WPF 컨트롤(GMapMarkerPidsGroupControl.
                    IsEventActive) 소관이라 헤드리스 단위 테스트 범위 밖 — EXT-02 런타임 검증.
                  · IoC는 Caliburn 정적 델리게이트 주입(DevicePanelCrudCompletionTests 패턴).
                    xunit은 동일 클래스 내 테스트를 순차 실행 → 정적 IoC 충돌 없음.
   Created By   : GHLee
   Created On   : 2026-08-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

public class GroupActionReportLauncherTests : IDisposable
{
    private const int GROUP = 5;
    private const int OTHER_GROUP = 6;

    private readonly List<object> _published = new();
    private readonly Mock<IEventAggregator> _ea = new();
    private readonly EventQueueManager _eqm = new();
    private readonly EventProvider _eventProvider = new();
    // ⚠ 다이얼로그는 IoC 배선 '이후' 생성해야 한다 — BasePanelViewModel 기본 생성자가 IoC.Get을 호출하므로
    //   필드 이니셜라이저(생성자 본문보다 선행)로 만들면 "IoC is not initialized"로 전 테스트가 죽는다.
    private readonly CapturingDetectionDialog _detectionDialog;
    private readonly CapturingMalfunctionDialog _malfunctionDialog;
    private IPermissionService? _permission;   // null = 미등록(전체허용 폴백)

    public GroupActionReportLauncherTests()
    {
        _ea.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
           .Callback<object, Func<Func<Task>, Task>, CancellationToken>((m, _, _) => _published.Add(m))
           .Returns(Task.CompletedTask);

        IoC.GetInstance = (type, _) =>
        {
            if (type == typeof(IEventQueueManager)) return _eqm;
            if (type == typeof(EventProvider)) return _eventProvider;
            if (type == typeof(IEventAggregator)) return _ea.Object;
            if (type == typeof(IAccountModel)) return Mock.Of<IAccountModel>();
            if (type == typeof(DetectionReportDialogViewModel)) return _detectionDialog;
            if (type == typeof(MalfunctionReportDialogViewModel)) return _malfunctionDialog;
            if (type == typeof(IPermissionService)) return _permission!;
            return null!;   // EventCardListPanelViewModel 등 미등록 → 카드 폴백 없이 EventProvider 경로 (S-19)
        };
        IoC.GetAllInstances = _ => Enumerable.Empty<object>();
        IoC.BuildUp = _ => { };

        _detectionDialog = new CapturingDetectionDialog();     // IoC 배선 후 생성(위 주석)
        _malfunctionDialog = new CapturingMalfunctionDialog();
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
        _eqm.Dispose();
    }

    #region - 헬퍼 -

    private static GroupActionReportLauncher NewLauncher()
        => new(Mock.Of<ILogService>());   // 임시 카드 VM 생성 경로가 log를 전달하므로 non-null

    private static IPidsGroupEditableMarker NewMarker(int groupId = GROUP)
    {
        var m = new Mock<IPidsGroupEditableMarker>();
        m.SetupGet(x => x.LinkedDeviceGroup).Returns(groupId);
        m.SetupGet(x => x.Title).Returns($"구역-{groupId}");
        return m.Object;
    }

    /// <summary>EQM 활성 엔트리 등록. 반환된 인스턴스는 EQM이 공유 보관 → EnqueuedAt 재설정으로 타이브레이크 결정화.</summary>
    private EventEntry Enqueue(int eventId, EnumEventType type, int groupId = GROUP, DateTime? enqueuedAt = null)
    {
        var entry = new EventEntry
        {
            DeviceId = 1000 + eventId,
            DeviceType = EnumDeviceType.Fence,
            GroupIds = new List<int> { groupId },
            EventType = type,
            EventId = eventId,
            TimeoutSeconds = 9999,
            IsAutoReportEnabled = false,   // 테스트 중 자동 조치보고 tick 개입 차단
        };
        _eqm.Enqueue(entry);
        if (enqueuedAt.HasValue) entry.EnqueuedAt = enqueuedAt.Value;   // Enqueue가 Now로 덮으므로 사후 고정
        return entry;
    }

    private void AddDetectionModel(int id, DateTime occurredAt)
        => _eventProvider.Add(new DetectionEventModel { Id = id, DateTime = occurredAt, MessageType = EnumEventType.Intrusion });

    private void AddMalfunctionModel(int id, DateTime occurredAt)
        => _eventProvider.Add(new MalfunctionEventModel { Id = id, DateTime = occurredAt, MessageType = EnumEventType.Fault });

    private T? Published<T>() where T : class => _published.OfType<T>().FirstOrDefault();

    #endregion

    #region - SETUP-01: EventQueueManager.GetEntriesByGroup -

    [Fact]
    public void should_return_empty_when_group_has_no_entries()
    {
        Enqueue(1, EnumEventType.Intrusion, groupId: OTHER_GROUP);

        var result = _eqm.GetEntriesByGroup(GROUP);

        Assert.Empty(result);
    }

    [Fact]
    public void should_return_only_entries_of_requested_group()
    {
        Enqueue(1, EnumEventType.Intrusion, groupId: GROUP);
        Enqueue(2, EnumEventType.Fault, groupId: GROUP);
        Enqueue(3, EnumEventType.Intrusion, groupId: OTHER_GROUP);

        var result = _eqm.GetEntriesByGroup(GROUP);

        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Contains(GROUP, e.GroupIds!));
    }

    #endregion

    #region - TEST-02: 런처 게이트 (FR-02 미연결 / FR-07 권한 / FR-08 중복창 / 빈 그룹) -

    [Fact]
    public void should_notify_and_skip_when_linked_device_group_is_zero()
    {
        var result = NewLauncher().TryOpenForGroup(NewMarker(groupId: 0));

        Assert.False(result);
        Assert.NotNull(Published<OpenInfoPopupMessageModel>());          // 미연결 안내 (S-30)
        Assert.Null(Published<OpenEventReportDialogMessageModel>());     // 창 안 열림
    }

    [Fact]
    public void should_skip_when_events_control_permission_denied()
    {
        var perm = new Mock<IPermissionService>();
        perm.Setup(p => p.CanControl("events")).Returns(false);
        _permission = perm.Object;
        Enqueue(10, EnumEventType.Intrusion);
        AddDetectionModel(10, DateTime.Now);

        var result = NewLauncher().TryOpenForGroup(NewMarker());

        Assert.False(result);
        Assert.NotNull(Published<OpenInfoPopupMessageModel>());          // 권한 없음 안내 (S-38)
        Assert.Null(Published<OpenEventReportDialogMessageModel>());
    }

    [Fact]
    public async Task should_skip_when_report_dialog_already_active()
    {
        await ((IActivate)_detectionDialog).ActivateAsync(CancellationToken.None);   // IsActive=true (VER-08)
        Enqueue(10, EnumEventType.Intrusion);
        AddDetectionModel(10, DateTime.Now);

        var result = NewLauncher().TryOpenForGroup(NewMarker());

        Assert.False(result);                                            // 재진입 차단 (S-39/S-40)
        Assert.Null(Published<OpenEventReportDialogMessageModel>());
    }

    [Fact]
    public void should_notify_and_skip_when_group_has_no_active_entries()
    {
        var result = NewLauncher().TryOpenForGroup(NewMarker());         // EQM 비어있음 (S-05/S-07/S-14)

        Assert.False(result);
        Assert.NotNull(Published<OpenInfoPopupMessageModel>());
        Assert.Null(Published<OpenEventReportDialogMessageModel>());
    }

    #endregion

    #region - TEST-03: 최선착 선정 (FR-03) -

    [Fact]
    public void should_open_earliest_detection_by_occurred_at_when_arrival_order_reversed()
    {
        // S-08/S-12: 늦게 발생한 11이 먼저 도착(enqueue) — 서버 발생시각 기준이면 10이 이겨야 한다.
        // 카드리스트 미등록 → EventProvider 폴백 경로 = S-19 동시 검증.
        var t0 = new DateTime(2026, 8, 4, 10, 0, 0);
        Enqueue(11, EnumEventType.Intrusion);
        Enqueue(10, EnumEventType.Intrusion);
        AddDetectionModel(11, t0.AddSeconds(5));
        AddDetectionModel(10, t0);

        var result = NewLauncher().TryOpenForGroup(NewMarker());

        Assert.True(result);
        Assert.Equal("DETECTION", Published<OpenEventReportDialogMessageModel>()?.EventType);
        Assert.Equal(10, _detectionDialog.Captured?.Model?.Id);
    }

    [Fact]
    public void should_open_malfunction_when_fault_occurred_before_detection()
    {
        // S-10/S-03: 타입 무관 최선착 — 장애가 4초 먼저면 장애 조치보고가 뜬다.
        var t0 = new DateTime(2026, 8, 4, 10, 0, 0);
        Enqueue(20, EnumEventType.Intrusion);
        Enqueue(7, EnumEventType.Fault);
        AddDetectionModel(20, t0.AddSeconds(4));
        AddMalfunctionModel(7, t0);

        var result = NewLauncher().TryOpenForGroup(NewMarker());

        Assert.True(result);
        Assert.Equal("MALFUNCTION", Published<OpenEventReportDialogMessageModel>()?.EventType);
        Assert.Equal(7, _malfunctionDialog.Captured?.Model?.Id);
        Assert.Null(_detectionDialog.Captured);
    }

    [Fact]
    public void should_only_consider_entries_of_clicked_group()
    {
        // S-15: 타 그룹(6)에 더 이른 이벤트가 있어도 클릭한 그룹(5)의 것만 후보.
        var t0 = new DateTime(2026, 8, 4, 10, 0, 0);
        Enqueue(30, EnumEventType.Intrusion, groupId: OTHER_GROUP);
        Enqueue(31, EnumEventType.Intrusion, groupId: GROUP);
        AddDetectionModel(30, t0);                    // 더 이르지만 다른 그룹
        AddDetectionModel(31, t0.AddMinutes(1));

        var result = NewLauncher().TryOpenForGroup(NewMarker(GROUP));

        Assert.True(result);
        Assert.Equal(31, _detectionDialog.Captured?.Model?.Id);
    }

    [Fact]
    public void should_tiebreak_by_enqueued_at_when_occurred_at_equal()
    {
        // S-11: 서버 발생시각 완전 동일 → 클라 수신시각(EnqueuedAt)이 이른 쪽.
        // Enqueue가 Now로 덮으므로 공유 인스턴스의 EnqueuedAt을 사후 고정해 결정화(sleep 금지).
        var t0 = new DateTime(2026, 8, 4, 10, 0, 0);
        Enqueue(41, EnumEventType.Intrusion, enqueuedAt: t0);
        Enqueue(40, EnumEventType.Intrusion, enqueuedAt: t0.AddSeconds(1));
        AddDetectionModel(40, t0);
        AddDetectionModel(41, t0);                    // 발생시각 동률

        var result = NewLauncher().TryOpenForGroup(NewMarker());

        Assert.True(result);
        Assert.Equal(41, _detectionDialog.Captured?.Model?.Id);
    }

    #endregion

    #region - TEST-04: 모델 해석 폴백 (FR-04) -

    [Fact]
    public void should_notify_when_event_model_not_resolvable()
    {
        // S-20: EQM 엔트리는 있으나 카드도 EventProvider 모델도 없음 → 안내 후 무동작(네트워크 조회 금지).
        Enqueue(50, EnumEventType.Intrusion);

        var result = NewLauncher().TryOpenForGroup(NewMarker());

        Assert.False(result);
        Assert.NotNull(Published<OpenInfoPopupMessageModel>());
        Assert.Null(Published<OpenEventReportDialogMessageModel>());
    }

    [Fact]
    public void should_not_resolve_detection_from_malfunction_model_when_id_collides()
    {
        // S-21: 탐지/장애는 독립 id 시퀀스 — 탐지 엔트리(42)에 같은 Id의 장애 모델만 있으면
        // 타입 필터(OfType)가 막아 미해석으로 끝나야 한다(장애 창 오픈은 오동작).
        Enqueue(42, EnumEventType.Intrusion);
        AddMalfunctionModel(42, DateTime.Now);

        var result = NewLauncher().TryOpenForGroup(NewMarker());

        Assert.False(result);
        Assert.Null(Published<OpenEventReportDialogMessageModel>());
        Assert.Null(_malfunctionDialog.Captured);
    }

    [Fact]
    public void should_resolve_correct_type_when_both_models_share_id()
    {
        // S-21 역방향: 같은 Id(43)의 탐지·장애 모델 공존 + 엔트리는 Fault → 장애 모델이 정확히 선택.
        Enqueue(43, EnumEventType.Fault);
        AddDetectionModel(43, DateTime.Now);
        AddMalfunctionModel(43, DateTime.Now);

        var result = NewLauncher().TryOpenForGroup(NewMarker());

        Assert.True(result);
        Assert.Equal("MALFUNCTION", Published<OpenEventReportDialogMessageModel>()?.EventType);
        Assert.Equal(43, _malfunctionDialog.Captured?.Model?.Id);
        Assert.Null(_detectionDialog.Captured);
    }

    #endregion

    #region - 테스트 더블 (UpdateData 가상 → 대상 카드 캡처) -

    private sealed class CapturingDetectionDialog : DetectionReportDialogViewModel
    {
        public EventCardBaseViewModel? Captured;
        public override void UpdateData(EventCardBaseViewModel eventModel, IAccountModel user) => Captured = eventModel;
    }

    private sealed class CapturingMalfunctionDialog : MalfunctionReportDialogViewModel
    {
        public EventCardBaseViewModel? Captured;
        public override void UpdateData(EventCardBaseViewModel eventModel, IAccountModel user) => Captured = eventModel;
    }

    #endregion
}
