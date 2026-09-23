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
            d.DeviceLabel,
            ZoneTextOf(d.Device),
            d.Model?.Id.ToString(),
            d.IsActionReported,
            TypeEventKey: TypeEventKeyOf(d.MessageType),
            Extra: EnumKoreanMap.To(d.Result)),

        MalfunctionEventViewModel m => new EventRowFacts(
            m.DeviceLabel,
            ZoneTextOf(m.Device),
            m.Model?.Id.ToString(),
            m.IsActionReported,
            Extra: EnumKoreanMap.To(m.Reason)),

        // 연결: 서버는 type_event=Connection 하나만 주고 상태 칸이 없다(실서버 왕복 E6a) — 상태 키를 지어내지 않는다.
        ConnectionEventViewModel c => new EventRowFacts(
            c.DeviceLabel,
            ZoneTextOf(c.Device),
            c.Model?.Id.ToString(),
            false,
            Extra: EnumKoreanMap.To(c.MessageType)),

        ActionEventViewModel a => new EventRowFacts(
            a.OriginEvent?.Device?.DeviceName,
            ZoneTextOf(a.OriginEvent?.Device),
            a.Model?.Id.ToString(),
            true,
            Extra: $"{a.User} {a.Content} {a.OriginEvent?.Id}"),

        _ => new EventRowFacts(null, null, null, false),
    };

    /// <summary>
    /// 탐지 유형 → 칩 키. 서버 탐지 type_event 는 Intrusion · Alert · ContactOn · ContactOff · WindyMode 다섯이다
    /// (api-test-server <c>utils/enums.py:124</c>). 접점 · 강풍은 침입이 아니다 — 자기 칩으로 간다(E6b).
    /// 모르는 값(파싱 실패 None 포함)은 침입으로 두지 않고 접점·강풍 쪽에 둔다 — 침입 수를 부풀리지 않는다.
    /// </summary>
    public static string TypeEventKeyOf(EnumEventType type) => type switch
    {
        EnumEventType.Intrusion => EventListFilter.ChipIntrusion,
        EnumEventType.Alert => EventListFilter.ChipAlert,
        _ => EventListFilter.ChipContact,
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
