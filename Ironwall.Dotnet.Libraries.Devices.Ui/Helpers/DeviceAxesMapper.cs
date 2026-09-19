using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;

/// <summary>
/// 장비 DTO → 모델의 <b>v7.0+ 표현 축</b> 읽기 매핑(device-console-v8 FR-03). 7 카테고리 공용.
/// </summary>
/// <remarks>
/// <para><b>방향이 비대칭이다.</b> 읽기는 네 축 전부를 받은 그대로 싣고, 쓰기는 넓히지 않는다 —
/// 서버 <c>PATCH</c> 가 <c>components</c> 배열을 <b>통째 교체</b>하므로(실측 2026-09-19: 부분 전송 → 무경고 삭제)
/// 형상·관측·의도 축은 1차에서 모델 → DTO 로 되돌리지 않는다(AD-4 · AD-9).</para>
/// <para>접속·의도 축은 DTO 의 재조립 getter 가 아니라 <b>수신 원본</b>
/// (<see cref="BaseDeviceDto.ReceivedConnection"/> · <see cref="BaseDeviceDto.ReceivedDeviceConfig"/>)에서 읽는다.</para>
/// </remarks>
public static class DeviceAxesMapper
{
    /// <summary>
    /// 판별자·종류축 원값·부대·축 묶음을 모델에 싣는다.
    /// </summary>
    /// <param name="pathCategory">이 DTO 가 온 <b>경로의 카테고리</b> — 6.3 응답에는 <c>category_device</c> 가 없으므로 이것이 정본이다.</param>
    /// <param name="typeAxisCode">종류축 서버 원값(카메라 <c>category</c>/<c>type_camera</c>, 제어기·센서 <c>type_device</c>/<c>type_*</c>, 형상 4축 <c>type_*</c>).</param>
    /// <param name="hardwareSpec">파생 DTO 의 <c>HardwareSpec</c>(기반 클래스에는 공개 접근자가 없다).</param>
    public static void MapToModel(
        BaseDeviceDto dto,
        BaseDeviceModel model,
        EnumDeviceCategory pathCategory,
        string? typeAxisCode,
        HardwareSpecDto? hardwareSpec)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(model);

        // 판별자가 읽히면 그것, 아니면(6.3·미지 어휘) 경로. 둘은 서버 계약상 항상 같다 — 경로가 판별자를 정한다.
        var declared = DeviceTypeResolver.ParseCategory(dto.CategoryDevice);
        model.CategoryDevice = declared != EnumDeviceCategory.None ? declared : pathCategory;

        model.TypeAxisCode = NullIfEmpty(typeAxisCode);
        model.UnitId = dto.UnitId;
        model.Axes = BuildAxes(dto, hardwareSpec);
    }

    /// <summary>
    /// 응답 봉투의 <c>meta.view</c>·<c>meta.sections</c> 를 모델에 싣는다.
    /// 6.3 봉투(둘 다 없음)면 아무것도 하지 않는다 — 없는 축 묶음을 지어내지 않는다.
    /// </summary>
    public static void ApplyResponseMeta(this IBaseDeviceModel model, MetaDto? meta)
    {
        if (model == null || meta == null) return;
        if (string.IsNullOrWhiteSpace(meta.View) && (meta.Sections == null || meta.Sections.Count == 0)) return;

        // 축 계약 응답인데 축 객체가 하나도 안 실린 경우(view=basic 의 센서 등)도 "무엇이 안 실렸는가"는 기록해야 한다.
        model.Axes ??= new DeviceAxesModel();
        model.Axes.Meta = new ResponseMeta(meta.View, meta.Sections);
    }

    /// <summary>
    /// <see cref="ApplyResponseMeta"/> 의 흐름식 — <c>dto.ToXxxDeviceModel().WithResponseMeta(response.Meta)</c>.
    /// DTO → 모델 변환 지점(서비스 14곳 · 패널 6곳)마다 봉투의 meta 를 빠뜨리지 않게 한 줄로 붙인다.
    /// </summary>
    public static TModel WithResponseMeta<TModel>(this TModel model, MetaDto? meta) where TModel : IBaseDeviceModel
    {
        model.ApplyResponseMeta(meta);
        return model;
    }

    /// <summary>
    /// 쓰기 본문에 실을 종류축 값. 클라 enum 이 <b>표현하지 못하는</b> 서버 원값(<c>SPEED_DOME</c>·<c>SmartController</c>)이면
    /// 원값을 그대로 돌려보낸다 — enum 을 거치면 <c>NONE</c>/대표값으로 굳어 저장 한 번에 종류가 바뀐다(ISSUE-4·8).
    /// enum 이 표현할 수 있는 값이면 enum 이 이긴다(그리드에서 enum 으로 편집했을 수 있다).
    /// </summary>
    public static string TypeAxisForWrite<TEnum>(string? typeAxisCode, TEnum enumValue) where TEnum : struct, Enum
    {
        var code = NullIfEmpty(typeAxisCode);
        if (code != null && !(Enum.TryParse<TEnum>(code, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)))
            return code;

        return enumValue.ToString();
    }

    private static IDeviceAxesModel? BuildAxes(BaseDeviceDto dto, HardwareSpecDto? hardwareSpec)
    {
        var connection = ToConnection(dto.ReceivedConnection);
        var spec = ToHardwareSpec(hardwareSpec);
        var status = ToDeviceStatus(dto.DeviceStatusAxis);
        var config = ToDeviceConfig(dto.ReceivedDeviceConfig);

        if (connection == null && spec == null && status == null && config == null)
            return null;   // 축을 하나도 받지 못했다(6.3)

        return new DeviceAxesModel
        {
            Connection = connection,
            HardwareSpec = spec,
            DeviceStatus = status,
            DeviceConfig = config,
        };
    }

    private static IConnectionAxisModel? ToConnection(ConnectionAxisDto? dto)
    {
        if (dto == null) return null;

        var model = new ConnectionAxisModel
        {
            Type = NullIfEmpty(dto.Type),
            IpAddress = NullIfEmpty(dto.IpAddress),
            IpPort = dto.IpPort,
            UserName = NullIfEmpty(dto.Credentials?.UserName),
            UserPassword = NullIfEmpty(dto.Credentials?.UserPassword),
            ParentDeviceId = dto.ParentDeviceId,
            Channel = dto.Channel,
            Protocol = NullIfEmpty(dto.Protocol),
        };

        // urls 는 카테고리마다 키가 달라 DTO 가 object 로 받는다 — 역직렬화 직후엔 JObject 다.
        var urls = dto.Urls as JObject ?? (dto.Urls != null ? JObject.FromObject(dto.Urls) : null);
        if (urls != null)
        {
            foreach (var property in urls.Properties())
            {
                model.Urls[property.Name] = property.Value.Type switch
                {
                    JTokenType.Null => null,
                    JTokenType.String => (string?)property.Value,
                    _ => property.Value.ToString(Newtonsoft.Json.Formatting.None),
                };
            }
        }

        return model;
    }

    private static IHardwareSpecModel? ToHardwareSpec(HardwareSpecDto? dto)
    {
        if (dto == null) return null;

        var model = new HardwareSpecModel
        {
            Schema = dto.Schema,
            Manufacturer = NullIfEmpty(dto.Manufacturer),
            Model = NullIfEmpty(dto.Model),
            Serial = NullIfEmpty(dto.SerialAxis),
            Firmware = NullIfEmpty(dto.Firmware),
            HardwareRev = NullIfEmpty(dto.HardwareRevAxis),
            MacAddress = NullIfEmpty(dto.MacAddress),
            MaxDetectionRange = dto.MaxDetectionRange,
            OnvifVersion = NullIfEmpty(dto.OnvifVersion),
            Spec = dto.Spec,
        };

        foreach (var component in dto.Components ?? Enumerable.Empty<ComponentDefinitionDto>())
        {
            model.Components.Add(new ComponentDefinitionModel
            {
                Key = component.Key,
                Type = component.Type,
                Label = component.Label,
                InService = component.InService,
                Channel = component.Channel,
                Position = component.Position,
                Manufacturer = component.Manufacturer,
                Model = component.Model,
                Serial = component.Serial,
                Firmware = component.Firmware,
                HardwareRev = component.HardwareRev,
                InstalledAt = component.InstalledAt,
                ReplacedAt = component.ReplacedAt,
                Spec = component.Spec,
            });
        }

        return model;
    }

    private static IDeviceStatusModel? ToDeviceStatus(DeviceStatusAxisDto? dto)
    {
        if (dto == null) return null;

        var model = new DeviceStatusModel { Schema = dto.Schema };
        if (dto.Components == null) return model;

        foreach (var (key, status) in dto.Components)
        {
            if (status == null) continue;
            model.Components[key] = new ComponentStatusModel
            {
                ObservedAt = status.ObservedAt,
                State = status.State,
                Health = status.Health,
                FaultReason = status.FaultReason,
            };
        }

        return model;
    }

    private static IDeviceConfigModel? ToDeviceConfig(DeviceConfigAxisDto? dto)
    {
        if (dto == null) return null;

        return new DeviceConfigModel
        {
            Schema = dto.Schema,
            Thresholds = dto.Thresholds,
            Modes = dto.Modes,
            ComponentOverrides = dto.ComponentOverrides,
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
