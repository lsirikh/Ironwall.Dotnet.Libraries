using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
/****************************************************************************
   Purpose      : ACTION_REPORT 폴백 body — 조치 생성 201 의 data 원문이 없을 때만 쓰는 명세 모양(브로커 명세 v2.0.7 §6.4)
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 서버 원문이 없을 때(구 라이브러리 · 응답 읽기 실패) 발행부가 싣는 ACTION_REPORT body.
/// </summary>
/// <remarks>
/// <para>정본은 201 의 <c>data</c> 그대로다. 이 폴백은 <b>명세 모양만</b> 따른다 —
/// <c>from_event.category_event</c> 판별자 · 장비 <b>참조</b> <c>{id, category_device}</c>(삭제된 장비면 <c>null</c>) ·
/// bool <c>action_reported: true</c>. 종전 폴백(<c>ToActionEventDto</c>)은 장비 블록(status · version · controller_id ·
/// geolocation · device_groups)을 조립하고 <c>category_event</c> 를 빠뜨려, 명세가 금지한 모양(§6.1 발행자 규칙)을 냈다.</para>
/// <para>서버만 아는 값(<c>device_description</c> 스냅샷 · <c>detail</c> · <c>updated_at</c>)은 지어내지 않는다.</para>
/// </remarks>
public static class ActionReportFallbackBody
{
    /// <summary>조치 body 를 만든다. 원본이 없으면 <c>from_event</c> 를 싣지 않는다.</summary>
    public static JObject Build(int actionId, string? content, string? user, DateTime actionTime, IExEventModel? origin)
    {
        var body = new JObject
        {
            ["id"] = actionId,
            ["type_event"] = nameof(EnumEventType.Action),
            ["content"] = content ?? string.Empty,
            ["user"] = user ?? string.Empty,
        };
        if (origin is not null) body["from_event"] = FromEvent(origin);
        body["created_at"] = KoreaTimeHelper.ToServerIso8601(actionTime);
        return body;
    }

    /// <summary>원본 이벤트의 판별자(<c>detection</c> · <c>malfunction</c> · <c>connection</c>). 모르면 <c>null</c>.</summary>
    public static string? CategoryOf(IExEventModel origin) => origin switch
    {
        IDetectionEventModel => "detection",
        IMalfunctionEventModel => "malfunction",
        IConnectionEventModel => "connection",
        _ => null,
    };

    /// <summary>장비 참조 <c>{id, category_device}</c> — 장비가 없으면 JSON <c>null</c>. 카테고리를 모르면 id 만.</summary>
    public static JToken DeviceReference(IBaseDeviceModel? device)
    {
        if (device is null || device.Id <= 0) return JValue.CreateNull();
        var reference = new JObject { ["id"] = device.Id };
        var category = device.CategoryDevice != EnumDeviceCategory.None
            ? device.CategoryDevice
            : DeviceTypeResolver.CategoryOf(device.DeviceType);
        if (category != EnumDeviceCategory.None) reference["category_device"] = category.ToString().ToLowerInvariant();
        return reference;
    }

    private static JObject FromEvent(IExEventModel origin)
    {
        var from = new JObject { ["id"] = origin.Id };
        if (CategoryOf(origin) is { } category) from["category_event"] = category;
        from["type_event"] = origin.MessageType.ToString();
        from["action_reported"] = true;   // 조치가 생기면 서버가 원본을 참으로 다시 계산한다(§6.4)
        switch (origin)
        {
            case IDetectionEventModel detection:
                from["result"] = detection.Result.ToString();
                break;
            case IMalfunctionEventModel malfunction:
                from["reason"] = malfunction.Reason.ToString();
                break;
        }
        from["device"] = DeviceReference(origin.Device);
        from["created_at"] = KoreaTimeHelper.ToServerIso8601(origin.DateTime);
        return from;
    }
}
