using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.Runtime.CompilerServices;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
/****************************************************************************
   Purpose      : 장비가 지워진 이벤트에 서버의 장비 스냅샷(device_description)을 곁들여 보인다.
   Created By   : GHLee
   Created On   : 2026-09-24
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 이벤트 → 장비 표시 이름. 장비가 살아 있으면 그 이름, 지워졌으면 서버 스냅샷.
/// </summary>
/// <remarks>
/// <para>서버는 장비를 지워도 이벤트를 남기고(<c>device_id</c> SET NULL) <c>device_description</c> 에 그때의 장비를
/// 문자열로 적어 둔다 — "장비가 삭제된 뒤 남는 <b>유일한</b> 단서"(<c>services/device_nested.py device_description_snapshot</c>).
/// 클라는 이 값을 모델로 옮기지 않아 화면이 "(장비 없음)" 을 보였다(실서버 왕복 E8).</para>
/// <para>서버 규범: 소비자는 이 문자열을 <b>파싱하지 않는다</b> — 사람이 읽는 흔적이라 그대로 보인다.</para>
/// <para>공유 모델(Monitoring.Models)에 칸을 더하지 않고 읽어 온 모델 인스턴스에 약하게 곁들인다(<see cref="EventDetailCarry"/> 와 같은 방식).</para>
/// </remarks>
public static class EventDeviceSnapshot
{
    /// <summary>지워진 장비임을 앞에 밝힌다.</summary>
    public const string DeletedPrefix = "삭제된 장비 · ";

    private sealed class Box { public string Text = string.Empty; }

    private static readonly ConditionalWeakTable<object, Box> Snapshots = new();

    /// <summary>서버가 준 스냅샷을 모델에 곁들인다(비어 있으면 아무것도 하지 않는다).</summary>
    public static void Remember(object? model, string? deviceDescription)
    {
        if (model is null || string.IsNullOrWhiteSpace(deviceDescription)) return;
        Snapshots.AddOrUpdate(model, new Box { Text = deviceDescription.Trim() });
    }

    /// <summary>그 모델에 곁들인 스냅샷(없으면 <c>null</c>).</summary>
    public static string? Of(object? model)
        => model is not null && Snapshots.TryGetValue(model, out var box) ? box.Text : null;

    /// <summary>
    /// 표시 이름 — 장비 이름이 있으면 그것, 없으면 "삭제된 장비 · {스냅샷}", 둘 다 없으면 <c>null</c>(호출부의 기존 대체 문구).
    /// </summary>
    public static string? Label(IBaseDeviceModel? device, object? model)
    {
        if (!string.IsNullOrWhiteSpace(device?.DeviceName)) return device!.DeviceName;
        var snapshot = Of(model);
        return snapshot is null ? null : DeletedPrefix + snapshot;
    }

    /// <summary>
    /// 목록 · 상세 제목에 쓰는 짧은 이름 — 장비가 살아 있으면 그 이름, 지워졌으면 "삭제된 장비 (이름)".
    /// </summary>
    /// <remarks>
    /// 스냅샷 원문("[sensor:PIR] 북측 1 (number: 3, id: 9)")을 그대로 목록에 찍으면 운영자가 모르는 종류 코드 · 번호가 보이고
    /// 열이 넘친다(완성도 감사 E-4 #3). 앞의 [종류] 꼬리표와 뒤의 (…) 괄호만 걷어 사람이 읽는 이름을 남긴다 —
    /// 값을 해석해 쓰지는 않는다(서버 규범: 파싱해 쓰지 않는다). 원문은 <see cref="Label"/> 로 툴팁에 남는다.
    /// 걷고 나서 비면 원문 전체를 쓴다.
    /// </remarks>
    public static string? ShortLabel(IBaseDeviceModel? device, object? model)
    {
        if (!string.IsNullOrWhiteSpace(device?.DeviceName)) return device!.DeviceName;
        var snapshot = Of(model);
        return snapshot is null ? null : $"삭제된 장비 ({ShortName(snapshot)})";
    }

    /// <summary>스냅샷에서 사람이 읽는 이름만 — 앞의 [..] 과 뒤의 (..) 를 걷는다.</summary>
    public static string ShortName(string snapshot)
    {
        var text = snapshot?.Trim() ?? string.Empty;
        if (text.StartsWith('['))
        {
            var close = text.IndexOf(']');
            if (close > 0) text = text[(close + 1)..].TrimStart();
        }
        if (text.EndsWith(')'))
        {
            var open = text.LastIndexOf('(');
            if (open > 0) text = text[..open].TrimEnd();
        }
        return text.Length > 0 ? text : snapshot?.Trim() ?? string.Empty;
    }
}
