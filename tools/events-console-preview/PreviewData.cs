using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EventsConsolePreview;

/// <summary>
/// 미리보기용 가짜 데이터 — 서버 없이 콘솔 모양만 본다. 값은 예시이고 계약 검증에는 쓰지 않는다.
/// </summary>
internal static class PreviewData
{
    public static readonly DateTime End = new(2026, 9, 20, 12, 10, 0);
    public static readonly DateTime Start = End.AddDays(-1);

    private static readonly (int Id, string Name, string Type)[] Sensors =
    {
        (101, "정문 1구간", "PIR"),
        (102, "북측 3구간", "FENCE"),
        (103, "북측 7구간", "FENCE"),
        (104, "탄약고 동측", "PIR"),
        (105, "남측 울타리", "FENCE"),
        (106, "서측 초소", "PIR"),
    };

    private static readonly string[] Results = { "CABLE_CUTTING", "CLIMBING", "TOUCH", "NONE" };
    private static readonly string[] Reasons = { "LINE_FAULT", "POWER_FAULT", "COMMUNICATION_FAULT" };

    private static string Iso(DateTime when) => when.ToString("yyyy-MM-ddTHH:mm:ss");

    private static BaseDeviceDto Device(int index) => new()
    {
        Id = Sensors[index % Sensors.Length].Id,
        NumberDevice = Sensors[index % Sensors.Length].Id,
        NameDevice = Sensors[index % Sensors.Length].Name,
        TypeDevice = Sensors[index % Sensors.Length].Type,
        Status = "ACTIVATED",
        IsEnable = true,
    };

    public static List<DetectionEventDto> Detections(int count = 34)
        => Enumerable.Range(0, count).Select(i => new DetectionEventDto
        {
            Id = 8800 + i,
            TypeEvent = "Intrusion",
            DeviceId = Sensors[i % Sensors.Length].Id,
            Device = Device(i),
            Result = Results[i % Results.Length],
            // 셋에 하나는 조치가 있다 — 레일 배지의 ▲n 을 볼 수 있게.
            ActionReported = i % 3 == 0 ? "True" : "False",
            CreatedAt = Iso(End.AddMinutes(-13 * i)),
            Detail = new DetectionDetailDto
            {
                Signal = 400 + (i * 37) % 900,
                Model = i % 4 == 0 ? "yolov8n-gop" : null,
                InferenceMs = i % 4 == 0 ? 18 + i % 9 : null,
                FrameWidth = i % 4 == 0 ? 1920 : null,
                FrameHeight = i % 4 == 0 ? 1080 : null,
            },
        }).ToList();

    public static List<MalfunctionEventDto> Malfunctions(int count = 12)
        => Enumerable.Range(0, count).Select(i => new MalfunctionEventDto
        {
            Id = 2200 + i,
            TypeEvent = "Fault",
            DeviceId = Sensors[i % Sensors.Length].Id,
            Device = Device(i),
            Reason = Reasons[i % Reasons.Length],
            ActionReported = i % 4 == 0 ? "True" : "False",
            CreatedAt = Iso(End.AddMinutes(-37 * i)),
        }).ToList();

    public static List<ConnectionEventDto> Connections(int count = 9)
        => Enumerable.Range(0, count).Select(i => new ConnectionEventDto
        {
            Id = 4400 + i,
            TypeEvent = i % 2 == 0 ? "Connection" : "Connection",
            DeviceId = Sensors[i % Sensors.Length].Id,
            Device = Device(i),
            CreatedAt = Iso(End.AddMinutes(-64 * i)),
        }).ToList();

    public static List<ActionEventDto> Actions(int count = 11)
        => Enumerable.Range(0, count).Select(i => new ActionEventDto
        {
            Id = 6600 + i,
            TypeEvent = "Action",
            Content = i % 2 == 0 ? "현장 확인 결과 이상 없음" : "순찰 인원 출동",
            User = i % 2 == 0 ? "김상병(21-70001)" : "이하사(19-30014)",
            CreatedAt = Iso(End.AddMinutes(-51 * i)),
        }).ToList();

    public static EventDashboardDto Dashboard()
    {
        var buckets = Enumerable.Range(0, 24).Select(h => new EventTrendItemDto
        {
            TimeBucket = Iso(Start.Date.AddHours(h)),
            SensorDetection = 3 + (h * 7) % 9,
            CameraDetection = 1 + (h * 5) % 5,
            Malfunction = h % 5 == 0 ? 2 : h % 3 == 0 ? 1 : 0,
            Connection = h % 4 == 0 ? 2 : 0,
            Action = 1 + (h * 3) % 4,
        }).ToList();

        return new EventDashboardDto
        {
            Summary = new EventSummaryDto
            {
                StartDate = Iso(Start),
                EndDate = Iso(End),
                DaysInRange = 1,
                SensorDetection = buckets.Sum(b => b.SensorDetection),
                CameraDetection = buckets.Sum(b => b.CameraDetection),
                Malfunction = buckets.Sum(b => b.Malfunction),
                Connection = buckets.Sum(b => b.Connection),
                Action = buckets.Sum(b => b.Action),
                Total = buckets.Sum(b => b.SensorDetection + b.CameraDetection + b.Malfunction + b.Connection + b.Action),
                DailyAverages = new DailyAveragesDto
                {
                    SensorDetection = buckets.Sum(b => b.SensorDetection),
                    CameraDetection = buckets.Sum(b => b.CameraDetection),
                    Malfunction = buckets.Sum(b => b.Malfunction),
                    Connection = buckets.Sum(b => b.Connection),
                    Action = buckets.Sum(b => b.Action),
                },
                ActiveDevices = new ActiveDevicesDto { Sensors = 48, Cameras = 7, Controllers = 6 },
            },
            Trend = new EventTrendDto { Interval = "hour", StartDate = Iso(Start), EndDate = Iso(End), Series = buckets },
            ByDevice = new EventByDeviceDto
            {
                StartDate = Iso(Start),
                EndDate = Iso(End),
                Controllers = new List<ControllerStatsDto>
                {
                    new() { ControllerId = 11, ControllerName = "북측 제어기 B", ControllerNumber = 103, SensorDetection = 38, Malfunction = 5, Connection = 9, Action = 17 },
                    new() { ControllerId = 12, ControllerName = "북측 제어기 A", ControllerNumber = 102, SensorDetection = 22, Malfunction = 1, Connection = 2, Action = 9 },
                    new() { ControllerId = 13, ControllerName = "탄약고 제어기", ControllerNumber = 104, SensorDetection = 19, Malfunction = 1, Connection = 4, Action = 12 },
                    new() { ControllerId = 14, ControllerName = "정문 제어기", ControllerNumber = 101, SensorDetection = 14, Malfunction = 1, Connection = 3, Action = 6 },
                    new() { ControllerId = 15, ControllerName = "남측 제어기", ControllerNumber = 105, SensorDetection = 9, Malfunction = 1, Connection = 2, Action = 4 },
                    new() { ControllerId = 16, ControllerName = "서측 제어기", ControllerNumber = 106, SensorDetection = 6, Malfunction = 0, Connection = 1, Action = 3 },
                },
                Cameras = new List<CameraStatsDto>
                {
                    new() { CameraId = 21, CameraName = "북측 7구간 돔", CameraNumber = 415, CameraDetection = 11 },
                    new() { CameraId = 22, CameraName = "남문 고정", CameraNumber = 417, CameraDetection = 7 },
                    new() { CameraId = 23, CameraName = "정문 PTZ", CameraNumber = 402, CameraDetection = 6 },
                    new() { CameraId = 24, CameraName = "탄약고 동측", CameraNumber = 421, CameraDetection = 4 },
                    new() { CameraId = 25, CameraName = "서측 초소", CameraNumber = 411, CameraDetection = 2 },
                },
            },
        };
    }

    public static PaginationDto Page(int total) => new() { Page = 1, Limit = 100, Total = total, TotalPages = 1 };
}
