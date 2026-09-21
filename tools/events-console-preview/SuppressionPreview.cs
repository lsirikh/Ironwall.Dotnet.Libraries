using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace EventsConsolePreview;

/****************************************************************************
   Purpose      : 억제 스케줄 레일(N-08) 미리보기 — 진짜 뷰 + 진짜 뷰모델 + 가짜 서버.
                  앱도 서버도 띄우지 않는다. 운영 쓰기 0.
   Created By   : GHLee
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>미리보기용 고정 시계 — 찍을 때마다 '억제중' 이 달라지면 그림이 흔들린다.</summary>
internal sealed class PreviewClock : IClock
{
    public DateTime Now { get; } = new(2026, 9, 20, 14, 30, 0);
    public DateTime UtcNow => Now.ToUniversalTime();
}

/// <summary>억제 미리보기가 쓰는 장비 · 그룹 · 스케줄.</summary>
internal static class SuppressionPreviewData
{
    public static DeviceProvider Devices()
    {
        var provider = new DeviceProvider();
        var rows = new (int Id, string Name, string Where)[]
        {
            (301, "SEN-1301 북측 1구간", "북측 7구간"),
            (302, "SEN-1302 북측 2구간", "북측 7구간"),
            (303, "SEN-1303 북측 3구간", "북측 7구간"),
            (311, "CAM-0415 북측 돔", "북측 7구간"),
            (312, "CAM-0417 남문 고정", "남문 게이트"),
            (321, "CTL-0101 정문 제어기", "정문"),
            (322, "CTL-0104 탄약고 제어기", "탄약고 동측"),
            (331, "ENC-0301 북측 1함체", "북측 7구간"),
            (332, "GAT-0201 남문 통문", "남문 게이트"),
            (341, "SEN-3301 정문 1구간", "정문"),
            (342, "SEN-3302 정문 2구간", "정문"),
            (351, "LMP-0701 남문 경광등", "남문 게이트"),
        };
        foreach (var row in rows)
            provider.CollectionEntity.Add(new BaseDeviceModel { Id = row.Id, DeviceName = row.Name, Location = row.Where });
        return provider;
    }

    public static DeviceGroupProvider Groups(ILogService log)
    {
        var provider = new DeviceGroupProvider(log);
        var rows = new (int Id, string Name, int Count)[]
        {
            (11, "북측 7구간", 18),
            (12, "남문 게이트", 9),
            (13, "탄약고", 14),
            (14, "정문", 11),
        };
        foreach (var row in rows)
            provider.CollectionEntity.Add(new DeviceGroupModel { Id = row.Id, Name = row.Name, DeviceCount = row.Count });
        return provider;
    }

    /// <summary>정본 SB L2295-2299 의 네 줄을 서버 DTO 로 옮긴 것.</summary>
    public static List<EventSuppressionScheduleDto> Schedules() => new()
    {
        new EventSuppressionScheduleDto
        {
            Id = 31, Name = "GOP 3구역 펜스 보수", Status = "active", IsSuppressingNow = true,
            TargetType = "device", TargetDeviceIds = new List<int> { 301, 302 },
            EventScope = "detection", TargetSide = "both",
            WindowStart = "2026-09-20T09:00:00.000+09:00", WindowEnd = "2026-09-20T18:00:00.000+09:00",
            RecurrenceType = "none", OccurrenceEnd = "2026-09-20T18:00:00.000+09:00",
        },
        new EventSuppressionScheduleDto
        {
            Id = 28, Name = "탄약고 야간 점검", Status = "active", IsSuppressingNow = false,
            TargetType = "group", TargetGroupIds = new List<int> { 13 },
            EventScope = "all", TargetSide = "both",
            WindowStart = "2026-09-01T22:00:00.000+09:00", WindowEnd = "2026-09-30T06:00:00.000+09:00",
            RecurrenceType = "weekly", DaysOfWeek = 127, DailyStart = "22:00:00", DailyEnd = "06:00:00",
            NextOccurrenceStart = "2026-09-20T22:00:00.000+09:00",
        },
        new EventSuppressionScheduleDto
        {
            Id = 25, Name = "정문 주간 출입 통제", Status = "active", IsSuppressingNow = false,
            TargetType = "device", TargetDeviceIds = new List<int> { 341 },
            EventScope = "detection", TargetSide = "detection",
            WindowStart = "2026-08-09T08:00:00.000+09:00", WindowEnd = null,
            RecurrenceType = "weekly", DaysOfWeek = 31, DailyStart = "08:00:00", DailyEnd = "21:00:00",
            NextOccurrenceStart = "2026-09-21T08:00:00.000+09:00",
        },
        new EventSuppressionScheduleDto
        {
            Id = 19, Name = "남문 게이트 교체", Status = "pending", IsSuppressingNow = false,
            TargetType = "device", TargetDeviceIds = new List<int> { 312, 351, 332 },
            EventScope = "malfunction", TargetSide = "both",
            WindowStart = "2026-09-22T10:00:00.000+09:00", WindowEnd = "2026-09-22T16:00:00.000+09:00",
            RecurrenceType = "none", NextOccurrenceStart = "2026-09-22T10:00:00.000+09:00",
        },
        new EventSuppressionScheduleDto
        {
            Id = 14, Name = "서측 초소 케이블 교체", Status = "expired", IsSuppressingNow = false,
            TargetType = "device", TargetDeviceIds = new List<int> { 303 },
            EventScope = "all", TargetSide = "both",
            WindowStart = "2026-09-10T09:00:00.000+09:00", WindowEnd = "2026-09-10T17:00:00.000+09:00",
            RecurrenceType = "none",
        },
        new EventSuppressionScheduleDto
        {
            Id = 9, Name = "취소된 전체 억제", Status = "cancelled", IsSuppressingNow = false,
            TargetType = "all", TargetSide = "surveillance", EventScope = "all",
            WindowStart = "2026-09-05T00:00:00.000+09:00", WindowEnd = "2026-09-06T00:00:00.000+09:00",
            RevokedAt = "2026-09-05T02:00:00.000+09:00", RecurrenceType = "none",
        },
    };
}

/// <summary>가짜 억제 서버 — 목록만 답한다. 쓰기는 <b>실제로 보내지 않고</b> 그 자리에서 성공으로 답한다.</summary>
internal sealed class SuppressionPreviewApi : IEventSuppressionApiService
{
    private readonly List<EventSuppressionScheduleDto> _rows = SuppressionPreviewData.Schedules();

    /// <summary>비어 있는 화면을 찍을 때 켠다.</summary>
    public bool IsEmpty { get; set; }

    private List<EventSuppressionScheduleDto> Rows => IsEmpty ? new List<EventSuppressionScheduleDto>() : _rows;

    public Task<ApiListResponse<EventSuppressionScheduleDto>> GetSuppressionSchedulesAsync(
        int page = 1, int limit = 20, string? status = null, string? targetType = null,
        int? deviceId = null, int? groupId = null, CancellationToken token = default)
    {
        var data = status is null ? Rows : Rows.Where(r => r.Status == status).ToList();
        return Task.FromResult(new ApiListResponse<EventSuppressionScheduleDto>
        {
            Success = true,
            Data = data.ToList(),
            Pagination = new PaginationDto { Page = 1, Limit = limit, Total = data.Count },
        });
    }

    public Task<ApiListResponse<EventSuppressionScheduleDto>> GetActiveSuppressionSchedulesAsync(CancellationToken token = default)
        => Task.FromResult(new ApiListResponse<EventSuppressionScheduleDto>
        {
            Success = true,
            Data = Rows.Where(r => r.Status == "active").ToList(),
        });

    public Task<ApiResponse<EventSuppressionScheduleDto>> GetSuppressionScheduleByIdAsync(int id, CancellationToken token = default)
        => Task.FromResult(new ApiResponse<EventSuppressionScheduleDto> { Success = true, Data = Rows.FirstOrDefault(r => r.Id == id) });

    public Task<ApiResponse<EventSuppressionScheduleDto>> CreateSuppressionScheduleAsync(
        EventSuppressionScheduleCreateDto dto, CancellationToken token = default)
        => Task.FromResult(new ApiResponse<EventSuppressionScheduleDto> { Success = true, Data = new EventSuppressionScheduleDto { Id = 900, Name = dto.Name } });

    public Task<ApiResponse<EventSuppressionScheduleDto>> PatchSuppressionScheduleAsync(
        int id, EventSuppressionScheduleUpdateDto dto, CancellationToken token = default)
        => Task.FromResult(new ApiResponse<EventSuppressionScheduleDto> { Success = true, Data = Rows.FirstOrDefault(r => r.Id == id) });

    public Task<ApiResponse<EventSuppressionScheduleDto>> CancelSuppressionScheduleAsync(int id, CancellationToken token = default)
        => Task.FromResult(new ApiResponse<EventSuppressionScheduleDto> { Success = true, Data = Rows.FirstOrDefault(r => r.Id == id) });

    public Task<ApiResponse<EventSuppressionBulkDeleteResultDto>> BulkDeleteSuppressionSchedulesAsync(
        IEnumerable<int> ids, CancellationToken token = default)
        => Task.FromResult(new ApiResponse<EventSuppressionBulkDeleteResultDto>
        {
            Success = true,
            Data = new EventSuppressionBulkDeleteResultDto { DeletedIds = ids.ToList() },
        });

    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
}

/// <summary>억제 레일의 상태들을 차례로 세우고 PNG 로 떠 놓는다.</summary>
internal static class SuppressionShots
{
    public static SuppressionPreviewApi Api { get; } = new();

    /// <summary>
    /// 찍을 상태 — 목록(빈 것 · 채운 것 · 고른 것) · 서랍(새것 · 수정 · 미적용 · 오류) · 칩 트레이 · 드롭 불가 · 좁은 폭.
    /// </summary>
    public static async Task RunAsync(EventDashboardViewModel model,
                                      Window window,
                                      Func<string, Task> save,
                                      Func<int, Task> settle)
    {
        var console = model.Suppression!;
        var drawer = console.Drawer;

        // 1) 목록 — 비어 있음
        Api.IsEmpty = true;
        await model.SelectRailAsync(EventDashboardViewModel.SuppressionRailKey);
        await console.LoadAsync();
        await settle(320);
        await save("01-list-empty");

        // 2) 목록 — 채워짐(억제중 · 진행중 · 예정 · 종료 · 취소 다섯 형태가 한 화면에)
        Api.IsEmpty = false;
        await console.LoadAsync();
        await settle(320);
        await save("02-list-loaded");

        // 3) 한 건 선택 — 상세(반복 요약 · 대상 칩 · 진행중 주의)
        console.Selected = console.Schedules.FirstOrDefault(s => s.Id == 28);
        await settle(320);
        await save("03-list-selected");

        // 4) 서랍 — 새 스케줄(빈 트레이 · 픽커)
        drawer.OpenNew();
        await settle(320);
        await save("04-drawer-new");

        // 5) 대상 칩 트레이 — 끌어 담은 뒤(폴백 경로는 드롭과 같은 함수다)
        drawer.Name = "북측 펜스 보수";
        drawer.AddSelected(drawer.PickerItems.Take(5).Cast<object>().ToList());
        await settle(320);
        await save("05-drawer-tray-chips");

        // 6) 미적용 변경 — 저장이 켜져 있고 되돌리기가 살아 있다
        await save("06-drawer-dirty");

        // 7) 드롭존이 실제로 '가능 / 불가' 로 보이는 모습 — 커널이 끌기 중에 매기는 상태를 그대로 세운다.
        //    (State 는 붙임 속성이라 SetValue 로 세울 수 있다 — 끌지 않고도 그 그림을 찍을 수 있다.)
        var zone = FindByAutomationId(window, "Console.Suppression.Tray.DropZone");
        if (zone is not null)
        {
            zone.SetValue(DropZone.StateProperty, DropZoneState.Available);
            await settle(260);
            await save("07a-drop-available");

            zone.SetValue(DropZone.StateProperty, DropZoneState.Hover);
            await settle(260);
            await save("07b-drop-hover");

            zone.SetValue(DropZone.StateProperty, DropZoneState.Blocked);
            await settle(260);
            await save("07c-drop-blocked");

            zone.SetValue(DropZone.StateProperty, DropZoneState.None);
            await settle(160);
        }

        // 7d) '전체 대상' — 트레이 · 픽커가 통째로 사라지고 까닭이 뜬다
        drawer.TargetType = SuppressionTargetDrop.ModeAll;
        drawer.AddSelected(new object[] { new SuppressionTargetChip(SuppressionTargetKind.Device, 301, "SEN-1301") });
        await settle(320);
        await save("07d-all-target");

        // 8) 오류 — 대상이 없는 장비 스케줄
        drawer.TargetType = SuppressionTargetDrop.ModeDevice;
        await settle(320);
        await save("08-drawer-invalid");

        // 9) 닫기 차단 — 미적용 변경이 있으면 닫히지 않고 바닥이 흔들린다
        drawer.TryClose();
        await settle(400);
        await save("09-drawer-close-blocked");

        // 9b) 읽을 수 없는 시각 — 글자를 버리지 않고 그대로 두고 저장만 막는다
        drawer.WindowEndText = "내일 아침";
        await settle(300);
        await save("09b-drawer-bad-time");
        drawer.WindowEndText = SuppressionTimeText.Format(drawer.Draft.WindowStart.AddHours(6));
        await settle(200);

        // 10) 주간 반복 — 요일 칩 · 일일 시각 · 요약 한 줄
        drawer.AddSelected(drawer.PickerItems.Take(2).Cast<object>().ToList());
        drawer.IsWeekly = true;
        await settle(320);
        await save("10-drawer-weekly");

        // 11) 수정 — 반복 칸이 잠긴다(서버 수정 스키마에 반복 칸이 없다)
        drawer.Revert();
        drawer.Close();
        console.Selected = console.Schedules.FirstOrDefault(s => s.Id == 25);
        await console.EditSelectedAsync();
        await settle(320);
        await save("11-drawer-edit-locked");

        // 12) 좁은 폭 — 서랍이 창을 따라 줄어든다(min(780, 창폭 − 24))
        window.Width = 900;
        await settle(420);
        await save("12-drawer-narrow-900");

        window.Width = 1150;
        await settle(420);
        await save("13-drawer-narrow-1150");

        drawer.Revert();
        drawer.Close();
        window.Width = 1360;
        await settle(320);

        // 14) 취소 · 종료 행을 고른 상태 — [선택 삭제] · [모두 정리] 가 켜진다
        foreach (var row in console.Schedules.Where(s => s.IsDeletable)) row.IsSelected = true;
        console.Selected = console.Schedules.FirstOrDefault(s => s.Id == 9);
        await settle(320);
        await save("14-list-delete-armed");
    }

    /// <summary>자동화 식별자로 요소를 찾는다(시각 트리).</summary>
    private static FrameworkElement? FindByAutomationId(DependencyObject root, string id)
    {
        if (root is FrameworkElement fe && (string?)fe.GetValue(AutomationProperties.AutomationIdProperty) == id) return fe;
        var n = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < n; i++)
            if (FindByAutomationId(VisualTreeHelper.GetChild(root, i), id) is { } hit) return hit;
        return null;
    }

    /// <summary>레일이 실제로 섰는지 — 미리보기가 조용히 빈 화면을 찍는 것을 막는다.</summary>
    public static void AssertRailExists(EventDashboardViewModel model)
    {
        if (model.Suppression is null)
            throw new InvalidOperationException("억제 콘솔이 주입되지 않았습니다 — 레일이 서지 않습니다.");
    }
}
