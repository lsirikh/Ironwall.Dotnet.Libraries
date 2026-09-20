using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Converters;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists;

/// <summary>
/// 행 뷰모델 → <see cref="EventRowFacts"/>. 순수 판정(<see cref="EventListFilter"/>)이 WPF · 뷰모델 타입을
/// 모르도록 변환을 여기 한 곳에 모은다.
/// </summary>
public static class EventRowFactsFactory
{
    public static EventRowFacts From(object? row) => row switch
    {
        DetectionEventViewModel d => new EventRowFacts(
            d.Device?.DeviceName,
            ZoneTextOf(d.Device),
            d.Model?.Id.ToString(),
            d.IsActionReported,
            TypeEventKey: d.MessageType == EnumEventType.Alert ? EventListFilter.ChipAlert : EventListFilter.ChipIntrusion,
            Extra: EnumKoreanMap.To(d.Result)),

        MalfunctionEventViewModel m => new EventRowFacts(
            m.Device?.DeviceName,
            ZoneTextOf(m.Device),
            m.Model?.Id.ToString(),
            m.IsActionReported,
            Extra: EnumKoreanMap.To(m.Reason)),

        // 연결의 '끊김 / 연결' 은 접점 ON · OFF 로 가른다.
        // ⚠ 서버가 어느 필드로 이 상태를 싣는지는 실기 미확인 — MessageType 만이 클라에서 증명 가능한 축이다.
        ConnectionEventViewModel c => new EventRowFacts(
            c.Device?.DeviceName,
            ZoneTextOf(c.Device),
            c.Model?.Id.ToString(),
            false,
            StateKey: c.MessageType == EnumEventType.ContactOff ? EventListFilter.ChipDisconnected : EventListFilter.ChipConnected,
            Extra: EnumKoreanMap.To(c.MessageType)),

        ActionEventViewModel a => new EventRowFacts(
            a.OriginEvent?.Device?.DeviceName,
            ZoneTextOf(a.OriginEvent?.Device),
            a.Model?.Id.ToString(),
            true,
            Extra: $"{a.User} {a.Content} {a.OriginEvent?.Id}"),

        _ => new EventRowFacts(null, null, null, false),
    };

    /// <summary>장비 소속 구역(그룹) 이름 — 기존 선택 편집기의 <c>DeviceZoneText</c> 와 같은 규칙.</summary>
    public static string? ZoneTextOf(IBaseDeviceModel? device)
    {
        var groups = device?.DeviceGroups;
        if (groups == null || groups.Count == 0) return null;
        try
        {
            var provider = IoC.Get<DeviceGroupProvider>();
            return string.Join(", ", groups.Select(id =>
                provider.OfType<DeviceGroupModel>().FirstOrDefault(g => g.Id == id)?.Name ?? id.ToString()));
        }
        catch (Exception)
        {
            // 프로바이더가 없는 자리(단위 테스트 · 미리보기)에서는 Id 를 그대로 — 화면을 비우지 않는다.
            return string.Join(", ", groups);
        }
    }
}
