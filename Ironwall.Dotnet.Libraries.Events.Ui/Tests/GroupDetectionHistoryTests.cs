using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using ApiListResponse = Ironwall.Dotnet.Libraries.Messages.Defines.Apis.ApiListResponse<Ironwall.Dotnet.Libraries.Messages.Dto.Events.DetectionEventDto>;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 그룹 탐지 이력 팬아웃 엔진 단위 테스트 (pidsgroup-rightclick TEST-01/02/03/05)
                  병합·최신순·총합 500 절단(G-1=a) · N=0 스킵 · 부분 실패 · 취소 전파 ·
                  모드 전환 리셋 · 센서 칩 3면 일관 · 최다 발생 센서 타이브레이크
   Created By   : GHLee
   Created On   : 2026-08-06
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class GroupDetectionHistoryTests
{
    private const int GROUP_ID = 3;

    #region - Helpers -
    private static DetectionEventDto BuildDto(int id, int? signal, string createdAt, bool actioned = true,
        string result = "PIR_SENSOR")
        => new()
        {
            Id = id,
            CreatedAt = createdAt,
            TypeEvent = "Intrusion",
            ActionReported = actioned ? "True" : "False",
            Result = result,
            Detail = signal is int s ? new DetectionDetailDto { Signal = s } : null
        };

    private static ApiListResponse Ok(params DetectionEventDto[] dtos)
        => new() { Success = true, Data = dtos.ToList() };

    private static SensorDeviceModel Sensor(int id, string name, int number) => new()
    {
        Id = id,
        DeviceName = name,
        DeviceNumber = number,
        DeviceType = EnumDeviceType.SmartSensor,
        DeviceGroups = new List<int> { GROUP_ID },
    };

    private static DetectionHistoryDialogViewModel CreateVm(Mock<IEventApiService> apiMock, DeviceProvider provider)
    {
        var eaMock = new Mock<IEventAggregator>();
        eaMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        var logMock = new Mock<ILogService>();
        return new DetectionHistoryDialogViewModel(eaMock.Object, logMock.Object, apiMock.Object, provider);
    }

    private static async Task ActivateAsync(DetectionHistoryDialogViewModel vm)
        => await ((IActivate)vm).ActivateAsync(CancellationToken.None);

    private static DeviceProvider ProviderWith(params SensorDeviceModel[] sensors)
    {
        var provider = new DeviceProvider();
        foreach (var s in sensors) provider.Add(s);
        return provider;
    }

    private static void SetupSensor(Mock<IEventApiService> apiMock, int sensorId, params DetectionEventDto[] dtos)
    {
        // 클라 후필터 계약 반영 — 실서버 응답 형태로 스탬프: 최상위 device_id 스칼라는 서버가 보내지 않으므로(PRD v1.3 제거)
        // 중첩 device.id만 채운다(검증 NEW-1 — 후필터 2절(실경로) 검증, 1절(DeviceId)은 사문)
        foreach (var dto in dtos)
            dto.Device ??= new BaseDeviceDto { Id = sensorId };
        apiMock.Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), null, sensorId, null,
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok(dtos));
    }

    /// <summary>fire-and-forget LoadAsync 완료 대기 — sleep 동기화 금지 규약의 waitFor 대체.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000, Func<string>? detail = null)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"WaitUntil 타임아웃{(detail != null ? " — " + detail() : string.Empty)}");
            await Task.Delay(10);
        }
    }

    private static OpenGroupDetectionHistoryDialogMessageModel GroupMsg(string name = "3구역")
        => new() { GroupId = GROUP_ID, GroupName = name };
    #endregion

    [Fact]
    public async Task should_merge_and_sort_desc_when_multi_sensor_fanout()
    {
        var apiMock = new Mock<IEventApiService>();
        SetupSensor(apiMock, 11, BuildDto(1, 1000, "2026-08-06T10:00:00"), BuildDto(2, 2000, "2026-08-06T12:00:00"));
        SetupSensor(apiMock, 12, BuildDto(3, 1500, "2026-08-06T11:00:00"));
        var vm = CreateVm(apiMock, ProviderWith(Sensor(11, "FN-0311", 31), Sensor(12, "FN-0312", 32)));

        vm.Initialize(GroupMsg());
        await ActivateAsync(vm);

        Assert.True(vm.IsGroupMode);
        Assert.Equal("그룹 탐지 이력 — 3구역 (센서 2)", vm.HeaderText);
        Assert.Equal(3, vm.FilteredItems.Count);
        // 병합 후 최신순 (12:00 → 11:00 → 10:00)
        Assert.Equal(new[] { 2, 3, 1 }, vm.FilteredItems.Select(i => i.EventId).ToArray());
        // 시리즈 인덱스 = 멤버 순서(DeviceNumber 오름차순) 고정 + 센서명 귀속
        Assert.All(vm.FilteredItems.Where(i => i.SensorId == 11), i => Assert.Equal(0, i.SeriesIndex));
        Assert.All(vm.FilteredItems.Where(i => i.SensorId == 12), i => Assert.Equal(1, i.SeriesIndex));
        Assert.Contains(vm.ChartPoints, p => p.SeriesName == "FN-0312" && p.SeriesIndex == 1);
        // 센서 칩 = 멤버 전원 기본 on
        Assert.Equal(2, vm.SensorChips.Count);
        Assert.All(vm.SensorChips, c => Assert.True(c.IsOn));
        Assert.Equal(Visibility.Visible, vm.SensorColumnVisibility);
    }

    [Fact]
    public async Task should_truncate_to_500_total_when_over_limit()
    {
        // 센서 2개 × 300건(100건 × 3페이지) = 600건 → 병합 후 최신 500건 절단(G-1=(a)) + IsTruncated
        var apiMock = new Mock<IEventApiService>();
        var baseTime = new DateTime(2026, 8, 6, 0, 0, 0);
        foreach (var (sensorId, offsetSec) in new[] { (11, 0), (12, 1) })
        {
            apiMock.Setup(x => x.GetDetectionEventsAsync(
                    It.IsAny<string?>(), It.IsAny<string?>(), null, sensorId, null,
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string? s, string? e, int? c, int? sen, string? st, int page, int limit, CancellationToken ct)
                    => page <= 3
                        ? Ok(Enumerable.Range((page - 1) * 100, 100)
                            .Select(n =>
                            {
                                var dto = BuildDto(sensorId * 10000 + n, 100 + n,
                                    baseTime.AddMinutes(n).AddSeconds(offsetSec).ToString("yyyy-MM-ddTHH:mm:ss"));
                                dto.Device = new BaseDeviceDto { Id = sensorId };   // 후필터 계약 — 실서버 형태(중첩 device)
                                return dto;
                            })
                            .ToArray())
                        : Ok());
        }
        var vm = CreateVm(apiMock, ProviderWith(Sensor(11, "FN-0311", 31), Sensor(12, "FN-0312", 32)));

        vm.Initialize(GroupMsg());
        await ActivateAsync(vm);

        Assert.True(vm.IsTruncated);
        Assert.Equal(500, vm.FilteredItems.Count);
        // 최신순 절단 — 남은 500건의 최소 시각이 잘려나간 100건의 최대 시각보다 뒤
        var oldestKept = vm.FilteredItems.Min(i => i.DateTime);
        Assert.True(oldestKept > baseTime.AddMinutes(49));
        Assert.Contains("상한 500", vm.RangeText);
    }

    [Fact]
    public async Task should_skip_query_when_member_count_zero()
    {
        var apiMock = new Mock<IEventApiService>();
        var vm = CreateVm(apiMock, ProviderWith());   // 멤버 없는 빈 그룹

        vm.Initialize(GroupMsg());
        await ActivateAsync(vm);

        apiMock.Verify(x => x.GetDetectionEventsAsync(
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(vm.FilteredItems);
        Assert.False(vm.HasLoadError);
    }

    [Fact]
    public async Task should_show_partial_results_when_one_sensor_fails()
    {
        var apiMock = new Mock<IEventApiService>();
        SetupSensor(apiMock, 11, BuildDto(1, 1000, "2026-08-06T10:00:00"), BuildDto(2, 2000, "2026-08-06T11:00:00"));
        apiMock.Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), null, 12, null,
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiListResponse { Success = false, Message = "server error" });
        var vm = CreateVm(apiMock, ProviderWith(Sensor(11, "FN-0311", 31), Sensor(12, "FN-0312", 32)));

        vm.Initialize(GroupMsg());
        await ActivateAsync(vm);

        // 부분 실패 = 확보분 표시 + 실패 센서 경고 표기, 에러 상태 아님
        Assert.False(vm.HasLoadError);
        Assert.Equal(2, vm.FilteredItems.Count);
        Assert.Contains("조회 실패", vm.RangeText);
        Assert.Contains("FN-0312", vm.RangeText);
    }

    [Fact]
    public async Task should_raise_error_when_all_sensors_fail()
    {
        var apiMock = new Mock<IEventApiService>();
        apiMock.Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiListResponse { Success = false, Message = "server down" });
        var vm = CreateVm(apiMock, ProviderWith(Sensor(11, "FN-0311", 31), Sensor(12, "FN-0312", 32)));

        vm.Initialize(GroupMsg());
        await ActivateAsync(vm);

        Assert.True(vm.HasLoadError);
        Assert.Empty(vm.FilteredItems);
    }

    [Fact]
    public async Task should_cancel_all_fanout_when_reload()
    {
        // 첫 로드(그룹)는 CT 취소까지 영구 대기 → 재-Initialize(단일)가 공용 CTS 취소를 전파해야 한다(NFR-02 핵심 계약)
        var apiMock = new Mock<IEventApiService>();
        var capturedTokens = new List<CancellationToken>();
        apiMock.Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), null, It.Is<int?>(s => s == 11 || s == 12), null,
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (string? s, string? e, int? c, int? sen, string? st, int page, int limit, CancellationToken ct)
                => { lock (capturedTokens) capturedTokens.Add(ct); await Task.Delay(-1, ct); return Ok(); });
        SetupSensor(apiMock, 99, BuildDto(9, 900, "2026-08-06T09:00:00"));
        var vm = CreateVm(apiMock, ProviderWith(Sensor(11, "FN-0311", 31), Sensor(12, "FN-0312", 32)));

        // 프로덕션 흐름 재현: 다이얼로그가 이미 활성(빠른 단일 조회 완료) → 그룹 재오픈(행) → 단일 재오픈(취소 전파).
        // 주의: CM 4.x는 OnActivateAsync 완료 후에야 IsActive=true — 활성화 중 Initialize는 재조회를 걸지 않는다(설계 동작).
        vm.Initialize(new OpenDetectionHistoryDialogMessageModel { DeviceId = 99, DeviceName = "FN-0399" });
        await ActivateAsync(vm);
        Assert.Single(vm.FilteredItems);

        vm.Initialize(GroupMsg());   // 그룹 팬아웃 — 영구 대기 진입 (fire-and-forget)
        await WaitUntilAsync(() => { lock (capturedTokens) return capturedTokens.Count >= 2; },
            detail: () => $"captured={capturedTokens.Count}");   // 두 센서 모두 대기 진입 확인

        vm.Initialize(new OpenDetectionHistoryDialogMessageModel { DeviceId = 99, DeviceName = "FN-0399" });

        // 계약: 재조회가 이전 팬아웃의 공용 CT를 취소한다 — 잔여 요청 유령화 금지(NFR-02)
        await WaitUntilAsync(() => { lock (capturedTokens) return capturedTokens.All(t => t.IsCancellationRequested); },
            detail: () => $"canceled={capturedTokens.Count(t => t.IsCancellationRequested)}/{capturedTokens.Count}");

        await WaitUntilAsync(() => !vm.IsBusy && !vm.IsGroupMode && vm.FilteredItems.Count == 1,
            detail: () => $"busy={vm.IsBusy} group={vm.IsGroupMode} err={vm.HasLoadError} items={vm.FilteredItems.Count}");
        Assert.False(vm.HasLoadError);   // 취소는 에러가 아니다
        Assert.Equal(9, vm.FilteredItems[0].EventId);
    }

    [Fact]
    public async Task should_reset_context_when_switching_sensor_group_sensor()
    {
        var apiMock = new Mock<IEventApiService>();
        SetupSensor(apiMock, 11, BuildDto(1, 1000, "2026-08-06T10:00:00"));
        SetupSensor(apiMock, 12, BuildDto(2, 1200, "2026-08-06T10:30:00"));
        SetupSensor(apiMock, 99, BuildDto(9, 900, "2026-08-06T09:00:00"));
        var vm = CreateVm(apiMock, ProviderWith(Sensor(11, "FN-0311", 31), Sensor(12, "FN-0312", 32)));

        // 센서 → 그룹 → 센서 순차 전환 (PRD 5-B 시나리오)
        vm.Initialize(new OpenDetectionHistoryDialogMessageModel { DeviceId = 99, DeviceName = "FN-0399" });
        await ActivateAsync(vm);
        Assert.False(vm.IsGroupMode);
        Assert.Single(vm.FilteredItems);

        vm.Initialize(GroupMsg());
        await WaitUntilAsync(() => vm.IsGroupMode && vm.FilteredItems.Count == 2 && vm.SensorChips.Count == 2);
        Assert.Equal("최다 발생 센서", vm.TopStatLabel);

        vm.Initialize(new OpenDetectionHistoryDialogMessageModel { DeviceId = 99, DeviceName = "FN-0399" });
        await WaitUntilAsync(() => !vm.IsGroupMode && vm.FilteredItems.Count == 1);
        // 그룹 잔존물 0 — 칩/컬럼/라벨 전부 단일 모드로 복귀
        Assert.Empty(vm.SensorChips);
        Assert.Equal(Visibility.Collapsed, vm.SensorColumnVisibility);
        Assert.Equal("최다 결과", vm.TopStatLabel);
        Assert.All(vm.ChartPoints, p => Assert.Null(p.SeriesName));
    }

    [Fact]
    public async Task should_apply_sensor_chip_to_chart_grid_stats()
    {
        var apiMock = new Mock<IEventApiService>();
        SetupSensor(apiMock, 11, BuildDto(1, 1000, "2026-08-06T10:00:00"), BuildDto(2, 2000, "2026-08-06T11:00:00"));
        SetupSensor(apiMock, 12, BuildDto(3, 3000, "2026-08-06T12:00:00"));
        var vm = CreateVm(apiMock, ProviderWith(Sensor(11, "FN-0311", 31), Sensor(12, "FN-0312", 32)));

        vm.Initialize(GroupMsg());
        await ActivateAsync(vm);
        Assert.Equal(3, vm.TotalCount);
        Assert.Equal(3000, vm.MaxSignal);

        // FN-0312 칩 off → 그리드/차트/통계 3면 동시 반영(FR-12)
        vm.SensorChips.First(c => c.DeviceId == 12).IsOn = false;

        Assert.Equal(2, vm.FilteredItems.Count);
        Assert.All(vm.FilteredItems, i => Assert.Equal(11, i.SensorId));
        Assert.All(vm.ChartPoints, p => Assert.Equal(0, p.SeriesIndex));
        Assert.Equal(2, vm.TotalCount);
        Assert.Equal(2000, vm.MaxSignal);
        Assert.StartsWith("FN-0311", vm.TopResultText);
    }

    [Fact]
    public async Task should_separate_and_dedup_when_server_ignores_sensor_filter()
    {
        // 실서버 버그(B2/B3) 재현: 어떤 sensor 값으로 호출해도 "전체 데이터"가 그대로 반환되는 서버.
        // 클라 방어(요청 센서 후필터 + EventId dedup)로 센서별 데이터가 분리되고 N중복이 사라져야 한다.
        var e1 = BuildDto(1, 1000, "2026-08-06T10:00:00"); e1.Device = new BaseDeviceDto { Id = 11 };
        var e2 = BuildDto(2, 2000, "2026-08-06T11:00:00"); e2.Device = new BaseDeviceDto { Id = 12 };
        var e3 = BuildDto(3, 1500, "2026-08-06T12:00:00"); e3.Device = new BaseDeviceDto { Id = 11 };
        var apiMock = new Mock<IEventApiService>();
        apiMock.Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ok(e1, e2, e3));   // 필터 무시 — 항상 동일 전체 집합
        var vm = CreateVm(apiMock, ProviderWith(Sensor(11, "FN-0311", 31), Sensor(12, "FN-0312", 32)));

        vm.Initialize(GroupMsg());
        await ActivateAsync(vm);

        // N중복(2센서×3건=6행) 아님 — 후필터+dedup으로 고유 3행
        Assert.Equal(3, vm.FilteredItems.Count);
        Assert.Equal(2, vm.FilteredItems.Count(i => i.SensorId == 11));
        Assert.Equal(1, vm.FilteredItems.Count(i => i.SensorId == 12));
        // 칩 off 시 데이터 패턴이 실제로 달라진다(B3 해소 단언)
        vm.SensorChips.First(c => c.DeviceId == 12).IsOn = false;
        Assert.Equal(2, vm.FilteredItems.Count);
        Assert.DoesNotContain(vm.FilteredItems, i => i.EventId == 2);
    }

    [Fact]
    public async Task should_dedup_page_boundary_duplicates_when_single_sensor()
    {
        // offset 페이지네이션 레이스: 페이지 사이 신규 이벤트 삽입으로 같은 이벤트가 두 페이지에 걸쳐 중복 수신 → 1건으로 dedup
        var baseTime = new DateTime(2026, 8, 6, 0, 0, 0);
        DetectionEventDto Row(int id) { var d = BuildDto(id, 100 + id, baseTime.AddMinutes(id).ToString("yyyy-MM-ddTHH:mm:ss")); d.Device = new BaseDeviceDto { Id = 99 }; return d; }
        var apiMock = new Mock<IEventApiService>();
        apiMock.Setup(x => x.GetDetectionEventsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), null, 99, null,
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? s, string? e, int? c, int? sen, string? st, int page, int limit, CancellationToken ct)
                => page == 1
                    ? Ok(Enumerable.Range(1, 100).Select(Row).ToArray())
                    : Ok(Enumerable.Range(100, 50).Select(Row).ToArray()));   // id=100이 페이지 경계에서 중복
        var vm = CreateVm(apiMock, ProviderWith());

        vm.Initialize(new OpenDetectionHistoryDialogMessageModel { DeviceId = 99, DeviceName = "FN-0399" });
        await ActivateAsync(vm);

        Assert.Equal(149, vm.FilteredItems.Count);   // 150 수신 - 중복 1
        Assert.Equal(vm.FilteredItems.Count, vm.FilteredItems.Select(i => i.EventId).Distinct().Count());
    }

    [Fact]
    public async Task should_toggle_all_sensor_chips_when_master_toggled()
    {
        var apiMock = new Mock<IEventApiService>();
        SetupSensor(apiMock, 11, BuildDto(1, 1000, "2026-08-06T10:00:00"));
        SetupSensor(apiMock, 12, BuildDto(2, 1200, "2026-08-06T11:00:00"));
        var vm = CreateVm(apiMock, ProviderWith(Sensor(11, "FN-0311", 31), Sensor(12, "FN-0312", 32)));

        vm.Initialize(GroupMsg());
        await ActivateAsync(vm);
        Assert.True(vm.AllSensorsOn);   // 초기 전부 on

        vm.AllSensorsOn = false;        // 마스터 off → 전 칩 off + 결과 0
        Assert.All(vm.SensorChips, c => Assert.False(c.IsOn));
        Assert.Empty(vm.FilteredItems);

        vm.AllSensorsOn = true;         // 마스터 on → 전 칩 on + 전체 복원
        Assert.All(vm.SensorChips, c => Assert.True(c.IsOn));
        Assert.Equal(2, vm.FilteredItems.Count);

        vm.SensorChips.First(c => c.DeviceId == 12).IsOn = false;   // 개별 off → 마스터 표시 동기(false)
        Assert.False(vm.AllSensorsOn);
        vm.SensorChips.First(c => c.DeviceId == 12).IsOn = true;    // 다시 전부 on → 마스터 true
        Assert.True(vm.AllSensorsOn);
    }

    [Fact]
    public async Task should_pick_top_sensor_with_tiebreak()
    {
        // 동률(2:2) — 최근 발생이 더 늦은 FN-0312가 선정돼야 한다(FR-13 타이브레이크)
        var apiMock = new Mock<IEventApiService>();
        SetupSensor(apiMock, 11, BuildDto(1, 1000, "2026-08-06T10:00:00"), BuildDto(2, 1100, "2026-08-06T10:30:00"));
        SetupSensor(apiMock, 12, BuildDto(3, 1200, "2026-08-06T11:00:00"), BuildDto(4, 1300, "2026-08-06T12:00:00"));
        var vm = CreateVm(apiMock, ProviderWith(Sensor(11, "FN-0311", 31), Sensor(12, "FN-0312", 32)));

        vm.Initialize(GroupMsg());
        await ActivateAsync(vm);

        Assert.StartsWith("FN-0312", vm.TopResultText);
        Assert.Contains("(2건)", vm.TopResultText);
    }
}
