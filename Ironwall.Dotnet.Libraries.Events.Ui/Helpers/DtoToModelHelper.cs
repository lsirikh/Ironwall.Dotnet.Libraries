using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;

/// <summary>
/// Event DTO ↔ Model 변환 Helper
/// Nested Device 구조 기반 (Phase 8B)
/// </summary>
public static class DtoToModelHelper
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // 서버 어휘 관용 해석 (API 계약 동기화 FR-01 · FR-02)
    // ═══════════════════════════════════════════════════════════════════════════════
    //
    // ⚠ 서버는 어휘를 **늘린다**. 브로커 명세가 직접 요구한다 —
    //   "강타입 enum 역직렬화에는 **미지 값 폴백을 두십시오** — 확장 어휘는 카탈로그가 늘립니다".
    //   종전 `Enum.Parse` 는 미지 값에 ArgumentException 을 던졌고, `Alert` 하나가
    //   탐지 목록 로딩 전체를 죽였다(`Alert` 는 detection 카테고리로 실려 온다).
    //   레포 관용구와 동일하다(`Devices.Ui/Helpers/DtoToModelHelper.cs:95`).

    /// <summary>서버 문자열을 enum 으로 관용 해석한다. 미지·공백이면 <c>default</c>.</summary>
    private static T ParseOrDefault<T>(string? value) where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : default;

    /// <summary>
    /// 조치보고 여부를 관용 판정한다.
    /// <para>⚠ 서버 버전마다 타입이 다르다 — API 6.3 은 <c>string("True"/"False")</c>,
    /// 7.0 은 <c>boolean</c>. Newtonsoft 는 JSON <c>false</c> 를 <b>소문자 "false"</b> 로 넘겨
    /// <c>== "True"</c> 비교가 어느 쪽과도 같지 않았고, 예외·로그 없이
    /// <b>전 이벤트가 '미조치'로 굳었다.</b></para>
    /// </summary>
    private static EnumTrueFalse ToActionReported(string? value)
        => bool.TryParse(value, out var b) && b ? EnumTrueFalse.True : EnumTrueFalse.False;

    // ═══════════════════════════════════════════════════════════════════════════════
    // type_event 카테고리 화이트리스트 (API 계약 동기화 F-08)
    // ═══════════════════════════════════════════════════════════════════════════════
    //
    // ⚠ 서버는 **카테고리별 type_event 어휘를 422 로 강제**한다
    //   (`ALLOWED_TYPE_EVENT_BY_CATEGORY` — detection 5값 / malfunction·connection 각 1값).
    //   v6.3.17 까지는 로그만 남겼지만 지금은 거부다.
    //   우리는 `model.MessageType.ToString()` 을 검증 없이 실어 보냈고,
    //   `MessageType` 이 초기화되지 않은 모델(기본값 `None`)을 POST 하면 422 `VALUE_NOT_ALLOWED` 로 끊긴다.
    //   경계에서 정규화해 **본문이 서버 어휘를 벗어나지 못하게** 만든다.

    /// <summary>탐지 경로가 받는 type_event 5값(서버 <c>ALLOWED_TYPE_EVENT_BY_CATEGORY["detection"]</c>).</summary>
    private static readonly HashSet<string> _detectionTypeEvents =
        new(StringComparer.Ordinal) { "Intrusion", "Alert", "ContactOn", "ContactOff", "WindyMode" };

    /// <summary>탐지 쓰기용 type_event. 어휘 밖(특히 <c>None</c>)이면 카테고리 대표값 <c>Intrusion</c> 으로 정규화.</summary>
    private static string ToDetectionTypeEvent(EnumEventType type)
    {
        var text = type.ToString();
        return _detectionTypeEvents.Contains(text) ? text : "Intrusion";
    }

    /// <summary>
    /// 장애 쓰기용 type_event. 서버는 이 경로에서 <c>Fault</c> <b>한 값만</b> 받는다
    /// (8.0.1 설명 원문: "이 경로는 <c>Fault</c> 만 받는다 — 다른 값은 422 <c>VALUE_NOT_ALLOWED</c>").
    /// 모델 값이 무엇이든 경로 어휘로 고정한다.
    /// </summary>
    private const string MALFUNCTION_TYPE_EVENT = "Fault";

    /// <summary>연결 쓰기용 type_event. 서버는 이 경로에서 <c>Connection</c> <b>한 값만</b> 받는다.</summary>
    private const string CONNECTION_TYPE_EVENT = "Connection";

    // ═══════════════════════════════════════════════════════════════════════════════
    // DTO → Model 변환 (기본)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// DetectionEventDto → IDetectionEventModel 변환
    /// </summary>
    public static IDetectionEventModel ToDetectionEventModel(this DetectionEventDto dto)
    {
        return new DetectionEventModel
        {
            Id = dto.Id,
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = ParseOrDefault<EnumEventType>(dto.TypeEvent),
            Status = ToActionReported(dto.ActionReported),
            Result = ParseOrDefault<EnumDetectionType>(dto.Result),
            Signal = dto.Detail?.Signal,
            AiModel = dto.Detail?.Model,
            InferenceMs = dto.Detail?.InferenceMs,
            Thumbnail = dto.Detail?.Thumbnail,
            FrameWidth = dto.Detail?.FrameWidth,
            FrameHeight = dto.Detail?.FrameHeight,
            Objects = ConvertObjectsFromDto(dto.Detail?.Objects),
            Device = ConvertDeviceFromDto(dto.Device, null)
        };
    }

    /// <summary>
    /// MalfunctionEventDto → IMalfunctionEventModel 변환
    /// </summary>
    public static IMalfunctionEventModel ToMalfunctionEventModel(this MalfunctionEventDto dto)
    {
        return new MalfunctionEventModel
        {
            Id = dto.Id,
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = ParseOrDefault<EnumEventType>(dto.TypeEvent),
            Status = ToActionReported(dto.ActionReported),
            Reason = ParseOrDefault<EnumFaultType>(dto.Reason),
            FirstStart = dto.Detail?.FirstStart ?? 0,
            FirstEnd = dto.Detail?.FirstEnd ?? 0,
            SecondStart = dto.Detail?.SecondStart ?? 0,
            SecondEnd = dto.Detail?.SecondEnd ?? 0,
            Device = ConvertDeviceFromDto(dto.Device, null)
        };
    }

    /// <summary>
    /// ConnectionEventDto → IConnectionEventModel 변환
    /// </summary>
    public static IConnectionEventModel ToConnectionEventModel(this ConnectionEventDto dto)
    {
        return new ConnectionEventModel
        {
            Id = dto.Id,
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = ParseOrDefault<EnumEventType>(dto.TypeEvent),
            Status = EnumTrueFalse.False,
            Device = ConvertDeviceFromDto(dto.Device, null)
        };
    }

    /// <summary>
    /// ActionEventDto → IActionEventModel 변환
    /// </summary>
    public static IActionEventModel ToActionEventModel(this ActionEventDto dto)
    {
        return new ActionEventModel
        {
            Id = dto.Id,
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = EnumEventType.Action,
            Content = dto.Content,
            User = dto.User,
            OriginEvent = null
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Model → DTO 변환 (역방향)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// IDetectionEventModel → DetectionEventDto 변환
    /// </summary>
    public static DetectionEventDto ToDetectionEventDto(this IDetectionEventModel model)
    {
        return new DetectionEventDto
        {
            Id = model.Id,
            CreatedAt = KoreaTimeHelper.ToServerIso8601(model.DateTime),   // aware(+09:00) — 리터럴 'Z'(거짓 UTC) 금지
            TypeEvent = ToDetectionTypeEvent(model.MessageType),   // F-08: 카테고리 어휘 강제(None → 422 방지)
            ActionReported = model.Status == EnumTrueFalse.True ? "True" : "False",
            Result = model.Result.ToString(),
            DeviceId = model.Device?.Id ?? 0,   // 서버 Create는 flat device_id(FK) 필수
            Device = ConvertDeviceToDto(model.Device),
            DeviceDescription = model.Device?.DeviceName,
            Detail = BuildDetectionDetail(model)
        };
    }

    /// <summary>
    /// IMalfunctionEventModel → MalfunctionEventDto 변환
    /// </summary>
    public static MalfunctionEventDto ToMalfunctionEventDto(this IMalfunctionEventModel model)
    {
        return new MalfunctionEventDto
        {
            Id = model.Id,
            CreatedAt = KoreaTimeHelper.ToServerIso8601(model.DateTime),   // aware(+09:00) — 리터럴 'Z'(거짓 UTC) 금지
            TypeEvent = MALFUNCTION_TYPE_EVENT,                     // F-08: 서버가 Fault 만 받는다
            ActionReported = model.Status == EnumTrueFalse.True ? "True" : "False",
            Reason = model.Reason.ToString(),
            DeviceId = model.Device?.Id ?? 0,   // 서버 Create는 flat device_id(FK) 필수
            Device = ConvertDeviceToDto(model.Device),
            DeviceDescription = model.Device?.DeviceName,
            Detail = new MalfunctionDetailDto
            {
                FirstStart = model.FirstStart,
                FirstEnd = model.FirstEnd,
                SecondStart = model.SecondStart,
                SecondEnd = model.SecondEnd
            }
        };
    }

    /// <summary>
    /// IConnectionEventModel → ConnectionEventDto 변환
    /// </summary>
    public static ConnectionEventDto ToConnectionEventDto(this IConnectionEventModel model)
    {
        return new ConnectionEventDto
        {
            Id = model.Id,
            CreatedAt = KoreaTimeHelper.ToServerIso8601(model.DateTime),   // aware(+09:00) — 리터럴 'Z'(거짓 UTC) 금지
            TypeEvent = CONNECTION_TYPE_EVENT,                      // F-08: 서버가 Connection 만 받는다
            DeviceId = model.Device?.Id ?? 0,   // 서버 Create는 flat device_id(FK) 필수
            Device = ConvertDeviceToDto(model.Device),
            DeviceDescription = model.Device?.DeviceName
        };
    }

    /// <summary>
    /// IActionEventModel → ActionEventDto 변환
    /// </summary>
    public static ActionEventDto ToActionEventDto(this IActionEventModel model)
    {
        return new ActionEventDto
        {
            Id = model.Id,
            CreatedAt = KoreaTimeHelper.ToServerIso8601(model.DateTime),   // aware(+09:00) — 리터럴 'Z'(거짓 UTC) 금지
            TypeEvent = model.MessageType.ToString(),
            Content = model.Content ?? string.Empty,
            User = model.User ?? string.Empty,
            FromEvent = ConvertOriginEventToDto(model.OriginEvent)
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Model → Replace DTO 변환 (수정 PUT 전용, 서버 *EventReplace 계약: 허용 필드만)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>IDetectionEventModel → DetectionEventReplaceDto (PUT 전용, type_event/result/detail만)</summary>
    /// <remarks>서버 PUT은 detail "전체 교체" — 미전송 시 기존 detail(signal 포함)이 소실되므로 반드시 재구성해 보존한다.</remarks>
    public static DetectionEventReplaceDto ToDetectionEventReplaceDto(this IDetectionEventModel model)
        => new()
        {
            TypeEvent = ToDetectionTypeEvent(model.MessageType),   // F-08
            Result = model.Result.ToString(),
            Detail = BuildDetectionDetail(model)
        };

    /// <summary>detail 필드가 하나라도 있으면 전체 재구성(없으면 필드 자체 생략 — 기존 페이로드와 동일).</summary>
    private static DetectionDetailDto? BuildDetectionDetail(IDetectionEventModel model)
    {
        bool hasDetail = model.Signal is not null
                         || !string.IsNullOrEmpty(model.AiModel)
                         || model.InferenceMs is not null
                         || !string.IsNullOrEmpty(model.Thumbnail)
                         || model.FrameWidth is not null
                         || model.FrameHeight is not null
                         || model.Objects is { Count: > 0 };
        if (!hasDetail) return null;

        return new DetectionDetailDto
        {
            Signal = model.Signal,
            Model = model.AiModel,
            InferenceMs = model.InferenceMs,
            Thumbnail = model.Thumbnail,
            FrameWidth = model.FrameWidth,
            FrameHeight = model.FrameHeight,
            Objects = model.Objects?.Select(o => new DetectedObjectDto
            {
                Label = o.Label,
                Confidence = o.Confidence,
                Bbox = o.Bbox?.ToList(),
                Thumbnail = o.Thumbnail
            }).ToList()
        };
    }

    /// <summary>detail.objects[] DTO → 모델 변환.</summary>
    private static List<DetectionObjectModel>? ConvertObjectsFromDto(List<DetectedObjectDto>? objects)
        => objects?.Select(o => new DetectionObjectModel
        {
            Label = o.Label,
            Confidence = o.Confidence,
            Bbox = o.Bbox?.ToList(),
            Thumbnail = o.Thumbnail
        }).ToList();

    /// <summary>IMalfunctionEventModel → MalfunctionEventReplaceDto (PUT 전용, type_event/reason/detail만)</summary>
    public static MalfunctionEventReplaceDto ToMalfunctionEventReplaceDto(this IMalfunctionEventModel model)
        => new()
        {
            TypeEvent = MALFUNCTION_TYPE_EVENT,                    // F-08
            Reason = model.Reason.ToString(),
            Detail = new MalfunctionDetailDto
            {
                FirstStart = model.FirstStart,
                FirstEnd = model.FirstEnd,
                SecondStart = model.SecondStart,
                SecondEnd = model.SecondEnd
            }
        };

    /// <summary>IConnectionEventModel → ConnectionEventReplaceDto (PUT 전용, type_event만)</summary>
    public static ConnectionEventReplaceDto ToConnectionEventReplaceDto(this IConnectionEventModel model)
        => new()
        {
            TypeEvent = CONNECTION_TYPE_EVENT                      // F-08
        };

    /// <summary>IActionEventModel → ActionEventReplaceDto (PUT 전용, type_event/content/user만 — from_event_id 제외, 원본 연결 불변)</summary>
    public static ActionEventReplaceDto ToActionEventReplaceDto(this IActionEventModel model)
        => new()
        {
            TypeEvent = model.MessageType.ToString(),
            Content = model.Content ?? string.Empty,
            User = model.User ?? string.Empty
        };

    // ═══════════════════════════════════════════════════════════════════════════════
    // DeviceProvider 통합 오버로드
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// DetectionEventDto → IDetectionEventModel 변환 (DeviceProvider 활용)
    /// </summary>
    public static IDetectionEventModel ToDetectionEventModel(
        this DetectionEventDto dto,
        DeviceProvider? deviceProvider)
    {
        return new DetectionEventModel
        {
            Id = dto.Id,
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = ParseOrDefault<EnumEventType>(dto.TypeEvent),
            Status = ToActionReported(dto.ActionReported),
            Result = ParseOrDefault<EnumDetectionType>(dto.Result),
            Signal = dto.Detail?.Signal,
            AiModel = dto.Detail?.Model,
            InferenceMs = dto.Detail?.InferenceMs,
            Thumbnail = dto.Detail?.Thumbnail,
            FrameWidth = dto.Detail?.FrameWidth,
            FrameHeight = dto.Detail?.FrameHeight,
            Objects = ConvertObjectsFromDto(dto.Detail?.Objects),
            Device = ConvertDeviceFromDto(dto.Device, deviceProvider)
        };
    }

    /// <summary>
    /// MalfunctionEventDto → IMalfunctionEventModel 변환 (DeviceProvider 활용)
    /// </summary>
    public static IMalfunctionEventModel ToMalfunctionEventModel(
        this MalfunctionEventDto dto,
        DeviceProvider? deviceProvider)
    {
        return new MalfunctionEventModel
        {
            Id = dto.Id,
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = ParseOrDefault<EnumEventType>(dto.TypeEvent),
            Status = ToActionReported(dto.ActionReported),
            Reason = ParseOrDefault<EnumFaultType>(dto.Reason),
            FirstStart = dto.Detail?.FirstStart ?? 0,
            FirstEnd = dto.Detail?.FirstEnd ?? 0,
            SecondStart = dto.Detail?.SecondStart ?? 0,
            SecondEnd = dto.Detail?.SecondEnd ?? 0,
            Device = ConvertDeviceFromDto(dto.Device, deviceProvider)
        };
    }

    /// <summary>
    /// ConnectionEventDto → IConnectionEventModel 변환 (DeviceProvider 활용)
    /// </summary>
    public static IConnectionEventModel ToConnectionEventModel(
        this ConnectionEventDto dto,
        DeviceProvider? deviceProvider)
    {
        return new ConnectionEventModel
        {
            Id = dto.Id,
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = ParseOrDefault<EnumEventType>(dto.TypeEvent),
            Status = EnumTrueFalse.False,
            Device = ConvertDeviceFromDto(dto.Device, deviceProvider)
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // EventProvider 통합 오버로드
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ActionEventDto → IActionEventModel 변환 (EventProvider 및 DeviceProvider 활용)
    /// </summary>
    public static IActionEventModel ToActionEventModel(
        this ActionEventDto dto,
        Ironwall.Dotnet.Libraries.Events.Providers.EventProvider? eventProvider,
        DeviceProvider? deviceProvider = null)
    {
        return new ActionEventModel
        {
            Id = dto.Id,
            DateTime = ParseDateTime(dto.CreatedAt),
            MessageType = EnumEventType.Action,
            Content = dto.Content,
            User = dto.User,
            OriginEvent = ResolveOriginEvent(dto.FromEvent, eventProvider, deviceProvider)
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Device 변환 헬퍼 (Nested DTO ↔ Model)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// BaseDeviceDto → IBaseDeviceModel 변환
    /// <para>DeviceProvider가 있으면 ID로 실제 Device 조회, 없으면 DTO 기반 최소 모델 생성</para>
    /// </summary>
    private static IBaseDeviceModel? ConvertDeviceFromDto(
        BaseDeviceDto? deviceDto,
        DeviceProvider? deviceProvider)
    {
        if (deviceDto == null)
            return null;

        // 종류축 복원 — 판본 관용(F-03). null 이면 "종류를 모른다"는 뜻이다.
        var resolvedType = DeviceTypeResolver.Resolve(deviceDto);
        var category = DeviceTypeResolver.ParseCategory(deviceDto.CategoryDevice);

        // DeviceProvider가 있으면 ID + 종류로 실제 Device 조회
        if (deviceProvider != null)
        {
            // 종류가 복원됐으면 ID + 종류 우선 매칭
            if (resolvedType is EnumDeviceType filterType)
            {
                var matched = deviceProvider.FirstOrDefault(d => d.Id == deviceDto.Id && d.DeviceType == filterType);
                if (matched != null)
                    return matched;
            }

            // 종류 미복원이거나 매칭 실패 시 ID만으로 조회
            var existing = deviceProvider.FirstOrDefault(d => d.Id == deviceDto.Id);
            if (existing != null)
                return existing;
        }

        // Fallback: DTO에서 가용 속성을 모두 매핑하여 모델 생성
        var model = CreateDeviceShell(resolvedType, category);
        model.Id = deviceDto.Id;
        if (resolvedType is EnumDeviceType t)
            model.DeviceType = t;   // 미복원이면 NONE 유지 — 틀린 종류를 단정하지 않는다
        PopulateDeviceFromDto(model, deviceDto);
        LinkControllerFromDto(model, deviceDto, deviceProvider);
        return model;
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // 장비 종류축 복원 (F-03) — 판본별 `device` 모양 관용 수용
    // ═══════════════════════════════════════════════════════════════════════════════
    //
    // 배포 스웨거 실측(2026-09-18) — 같은 `device` 키가 판본마다 **다른 모양**이다:
    //
    //   운영 6.3.2 : 전문(full nested) — `{id, number_device, name_device, type_device,
    //                status, is_enable, controller_id, geolocation, device_groups, …}`
    //                → `type_device` 는 **필수 키**(6개 NestedResponse 스키마 전부 required,
    //                  값은 문자열 이름 "SmartSensor2"·"Controller" — 실측 확인)
    //   개발 8.0.1 : 참조 프로필(`DeviceReference`) — `{id, category_device}` **두 키뿐**
    //                → 종류축이 **아예 없다**. 카테고리(7값 소문자)만 온다.
    //
    // 종전 코드는 `type_device` 만 봤다. 8.0.1 에서는 그 키가 없어 매 이벤트가
    // 마지막 폴백 `new SensorDeviceModel`(DeviceType=NONE)로 떨어졌고,
    // 표시단 switch 의 `not null => "센서"` 가 이를 삼켜 **카메라·통문·함체 이벤트까지
    // 전부 '센서'로 보였다**. 판본 분기 대신 관용 수용으로 양쪽을 함께 견딘다(읽기 경로).

    // 종류축 복원 규칙(① type_device → ② category_device, sensor 는 의도적 미매핑)의 정본은
    // Messages/Helpers/DeviceTypeResolver 다 — Devices.Ui 와 같은 함수를 쓴다(device-console-v8 FR-02, 이관).

    /// <summary>
    /// 폴백 모델 껍데기를 고른다 — 종류가 있으면 종류로, 없으면 <b>카테고리로</b>.
    /// </summary>
    /// <remarks>
    /// 둘 다 모르면 <c>SensorDeviceModel</c> 이 아니라 <see cref="BaseDeviceModel"/> 을 세운다.
    /// 표시단은 <c>ISensorDeviceModel</c> 여부로 '센서'와 '알 수 없음'을 가르므로,
    /// 여기서 센서 껍데기를 세우면 <b>모르는 장비가 다시 센서로 보인다</b>.
    /// </remarks>
    private static BaseDeviceModel CreateDeviceShell(EnumDeviceType? resolvedType, EnumDeviceCategory category)
    {
        if (resolvedType is EnumDeviceType t)
        {
            return t switch
            {
                EnumDeviceType.Controller => new ControllerDeviceModel(),
                EnumDeviceType.IpCamera   => new CameraDeviceModel(),
                EnumDeviceType.IpSpeaker  => new SpeakerDeviceModel(),
                EnumDeviceType.Enclosure  => new EnclosureDeviceModel(),
                EnumDeviceType.Lamp       => new LampDeviceModel(),
                EnumDeviceType.Gate       => new GateDeviceModel(),
                _ => new SensorDeviceModel()  // Sensor 계열 (Fence, Multi, PIR, etc.)
            };
        }

        // 종류 미복원 — 카테고리가 '센서'라고만 말해주면 센서 껍데기(종류=NONE)까지는 정직하다.
        if (category == EnumDeviceCategory.Sensor)
            return new SensorDeviceModel();

        // 카테고리도 모른다 → 아무것도 단정하지 않는 기반 모델(종류=NONE).
        return new BaseDeviceModel();
    }

    /// <summary>
    /// 폴백 센서 모델에 controller_id(FK)로 컨트롤러를 연결한다.
    /// 장애/연결 이벤트 전문(BaseDeviceDto)은 컨트롤러 표시번호를 싣지 않고 controller_id만 준다 —
    /// Provider에 정식 컨트롤러가 있으면 그 인스턴스로 연결(DeviceNumber 포함), 없으면 최소 FK id만 보존해
    /// 뷰모델(ControllerDeviceNumber)이 나중에 Provider에서 번호를 재해석할 수 있게 한다.
    /// </summary>
    private static void LinkControllerFromDto(BaseDeviceModel model, BaseDeviceDto dto, DeviceProvider? provider)
    {
        if (model is not ISensorDeviceModel sensor) return;
        if (dto.ControllerId is not int cid || cid <= 0) return;

        var ctrl = provider?.OfType<ControllerDeviceModel>().FirstOrDefault(c => c.Id == cid);
        if (ctrl != null) { sensor.Controller = ctrl; return; }

        if (sensor.Controller == null)
            sensor.Controller = new ControllerDeviceModel { Id = cid };
        else
            sensor.Controller.Id = cid;
    }

    /// <summary>
    /// DTO의 가용 속성을 BaseDeviceModel에 매핑
    /// </summary>
    private static void PopulateDeviceFromDto(BaseDeviceModel model, BaseDeviceDto dto)
    {
        model.DeviceName = dto.NameDevice;
        model.DeviceNumber = dto.NumberDevice;
        model.Version = dto.Version;
        if (!string.IsNullOrEmpty(dto.Status) &&
            Enum.TryParse<EnumDeviceStatus>(dto.Status, out var status))
        {
            model.Status = status;
        }
        model.DeviceGroups = dto.DeviceGroups?.Select(g => g.Id).ToList();
    }

    /// <summary>
    /// IBaseDeviceModel → BaseDeviceDto 변환
    /// </summary>
    private static BaseDeviceDto? ConvertDeviceToDto(IBaseDeviceModel? device)
    {
        if (device == null)
            return null;

        return new BaseDeviceDto
        {
            Id = device.Id,
            TypeDevice = device.DeviceType.ToString(),
            // ⚠ 이 `""` 가 F-01(이벤트 쓰기 422 `EMPTY_STRING`)의 직접 원인이었다.
            //    수리는 값이 아니라 **키 자체를 빼는 것**으로 했다 —
            //    `device` 는 세 판본 모두 응답 전용 키라 `DetectionEventDto.ShouldSerializeDevice()` 가
            //    요청 직렬화를 막는다. 여기서 `""` 를 만들어도 더는 전송되지 않는다.
            //    ⇒ 그 보증을 되돌릴 거면 이 두 줄도 **null 유지 + NullValueHandling.Ignore** 로 함께 바꿔야 한다.
            NameDevice = device.DeviceName ?? string.Empty,
            NumberDevice = device.DeviceNumber,
            // 설계 GIS.md v1.5 §2.1/§6.4 device 필드 준수(status/version/geolocation/controller_id/device_groups).
            Status = device.Status.ToString(),
            Version = device.Version ?? string.Empty,   // 위와 동일(F-01)
            ControllerId = (device as ISensorDeviceModel)?.Controller?.Id,
            // device_groups: 수신자 N:N EventMapping 라우팅 키(GIS.md v1.5 §2.1).
            // 그룹 id + DeviceGroupProvider(IoC)로 name/description/device_count 보강(Stage 1).
            DeviceGroups = BuildDeviceGroupDtos(device.DeviceGroups),
            Geolocation = BuildGeolocationDto(device)
        };
    }

    /// <summary>
    /// IBaseDeviceModel → GeolocationDto (설계 GIS.md v1.5: location/latitude/longitude/altitude/heading).
    /// 좌표/고도/방위/설명 중 하나라도 유의미할 때만 생성, 전부 비면 null(직렬화 생략).
    /// </summary>
    private static GeolocationDto? BuildGeolocationDto(IBaseDeviceModel device)
    {
        bool hasCoord = device.Latitude != 0 || device.Longitude != 0;
        if (!hasCoord && device.Altitude is null && device.Heading is null
            && string.IsNullOrEmpty(device.Location))
            return null;

        return new GeolocationDto
        {
            Location = device.Location,
            Latitude = device.Latitude,
            Longitude = device.Longitude,
            Altitude = device.Altitude,
            Heading = device.Heading
        };
    }

    /// <summary>
    /// 그룹 id 리스트 → device_groups(DeviceGroupDto). DeviceGroupProvider(IoC)로
    /// name/description/device_count 보강(EventCardViewModel.DeviceGroupsText 패턴 재사용).
    /// provider 미가용(테스트/부팅 전) 시 id만 채워 fallback — 라우팅 키는 항상 보존.
    /// </summary>
    private static List<DeviceGroupDto>? BuildDeviceGroupDtos(List<int>? groupIds)
    {
        if (groupIds is not { Count: > 0 }) return null;

        DeviceGroupProvider? provider = null;
        try { provider = IoC.Get<DeviceGroupProvider>(); }
        catch { /* IoC 미구성 → id-only fallback */ }

        return groupIds.Select(gid =>
        {
            var g = provider?.FirstOrDefault(x => x.Id == gid);
            return g is null
                ? new DeviceGroupDto { Id = gid }
                : new DeviceGroupDto
                {
                    Id = gid,
                    Name = g.Name,
                    Description = g.Description,
                    DeviceCount = g.DeviceCount
                };
        }).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // OriginEvent 역변환 헬퍼 (Model → DTO)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// IExEventModel → IEventDto 변환 (ActionEvent의 FromEvent 역변환용)
    /// </summary>
    private static IEventDto? ConvertOriginEventToDto(IExEventModel? originEvent)
    {
        if (originEvent == null)
            return null;

        return originEvent switch
        {
            IDetectionEventModel detection => detection.ToDetectionEventDto(),
            IMalfunctionEventModel malfunction => malfunction.ToMalfunctionEventDto(),
            IConnectionEventModel connection => connection.ToConnectionEventDto(),
            _ => null
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // OriginEvent 변환 헬퍼 (DTO → Model)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// FromEvent DTO를 EventProvider에서 조회하거나 변환
    /// </summary>
    private static IExEventModel? ResolveOriginEvent(
        object? fromEvent,
        Ironwall.Dotnet.Libraries.Events.Providers.EventProvider? eventProvider,
        DeviceProvider? deviceProvider)
    {
        if (fromEvent == null)
            return null;

        switch (fromEvent)
        {
            case DetectionEventDto detectionDto:
                if (eventProvider != null)
                {
                    var existing = eventProvider
                        .OfType<IDetectionEventModel>()
                        .FirstOrDefault(e => e.Id == detectionDto.Id);
                    if (existing != null)
                        return existing;
                }
                return detectionDto.ToDetectionEventModel(deviceProvider);

            case MalfunctionEventDto malfunctionDto:
                if (eventProvider != null)
                {
                    var existing = eventProvider
                        .OfType<IMalfunctionEventModel>()
                        .FirstOrDefault(e => e.Id == malfunctionDto.Id);
                    if (existing != null)
                        return existing;
                }
                return malfunctionDto.ToMalfunctionEventModel(deviceProvider);

            default:
                return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // DateTime 파싱 헬퍼
    // ═══════════════════════════════════════════════════════════════════════════════

    private static DateTime ParseDateTime(string? dateTimeString)
    {
        if (string.IsNullOrEmpty(dateTimeString))
            return DateTime.Now;

        // 서버 aware ISO(+09:00/Z)는 offset을 보존해 KST 벽시계로 정규화 — offset 소실/호스트 TZ 시프트 방지
        if (DateTimeOffset.TryParse(dateTimeString, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dto))
            return dto.ToOffset(KoreaTimeHelper.KoreaUtcOffset).DateTime;

        if (DateTime.TryParse(dateTimeString, out var dateTime))
            return dateTime;

        return DateTime.Now;
    }
}
