using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
/****************************************************************************
   Purpose      : 서버가 준 detail 의 '모양'을 읽어 온 모델 인스턴스에 곁들여 두었다가 PUT 때 되살린다.
   Created By   : GHLee
   Created On   : 2026-09-24
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 탐지 · 장애 <c>detail</c> 을 PUT 으로 되보낼 때 <b>서버에 있던 것을 잃지 않게</b> 한다.
/// </summary>
/// <remarks>
/// <para><b>왜 필요한가</b> — 서버 PUT 은 <c>detail</c> 을 통째로 갈아 끼운다(<c>routers/detections.py</c> ·
/// <c>malfunctions.py</c>: <c>event.detail = event_data.detail</c>). 그런데 모델(<see cref="IDetectionEventModel"/> ·
/// <see cref="IMalfunctionEventModel"/>)은 알려진 칸만 담는다. 그래서 '결과' 나 '사유' 하나만 고쳐도
/// ① 업체 키(<c>vendor_x</c> 등)가 사라지고 ② 알려진 칸이 하나도 없던 detail 은 null 로 지워지고
/// ③ detail 이 null 이던 장애는 <c>first_start:0 …</c> 네 칸으로 채워졌다(실서버 왕복 E5a~d).</para>
/// <para><b>어떻게</b> — 읽을 때(<see cref="DtoToModelHelper"/>) 서버 detail 의 모양(모르는 키 · 있던 칸 · null 이었는지)을
/// <b>그 모델 인스턴스</b>에 약하게 붙여 두고(<see cref="ConditionalWeakTable{TKey,TValue}"/> — 모델이 사라지면 같이 사라진다),
/// PUT 본문을 만들 때 되살린다. 공유 모델(Monitoring.Models)에 칸을 더하지 않는다 — 인터페이스를 넓히면 목 · 페이크가 깨진다.</para>
/// <para>곁들인 것이 없는 모델(테스트가 직접 만든 모델 · NATS 로 온 모델)은 <b>지금까지와 똑같이</b> 모델 칸으로만 만든다.</para>
/// </remarks>
public static class EventDetailCarry
{
    private sealed class Shape
    {
        public bool WasNull;
        public HashSet<string> Present = new(StringComparer.Ordinal);
        public Dictionary<string, JToken> Extras = new(StringComparer.Ordinal);
    }

    private static readonly ConditionalWeakTable<object, Shape> Shapes = new();

    /// <summary>서버에서 읽은 탐지 detail 의 모양을 모델에 곁들인다.</summary>
    public static void Remember(IDetectionEventModel model, DetectionDetailDto? detail)
    {
        if (model is null) return;
        var shape = new Shape { WasNull = detail is null };
        if (detail?.AdditionalData is { Count: > 0 } extra)
            foreach (var kv in extra) shape.Extras[kv.Key] = kv.Value.DeepClone();
        Shapes.AddOrUpdate(model, shape);
    }

    /// <summary>서버에서 읽은 장애 detail 의 모양을 모델에 곁들인다.</summary>
    public static void Remember(IMalfunctionEventModel model, MalfunctionDetailDto? detail)
    {
        if (model is null) return;
        var shape = new Shape { WasNull = detail is null };
        if (detail is not null)
        {
            if (detail.FirstStart is not null) shape.Present.Add("first_start");
            if (detail.FirstEnd is not null) shape.Present.Add("first_end");
            if (detail.SecondStart is not null) shape.Present.Add("second_start");
            if (detail.SecondEnd is not null) shape.Present.Add("second_end");
            if (detail.AdditionalData is { Count: > 0 } extra)
                foreach (var kv in extra) shape.Extras[kv.Key] = kv.Value.DeepClone();
        }
        Shapes.AddOrUpdate(model, shape);
    }

    /// <summary>
    /// 탐지 PUT 의 detail — 모델 칸으로 만든 것(<paramref name="fromModel"/>)에 서버에 있던 모르는 키를 되붙인다.
    /// 모델 칸도 모르는 키도 없으면 <c>null</c>(키 생략 — 서버 detail 은 원래 비어 있었다).
    /// </summary>
    public static DetectionDetailDto? MergeDetection(IDetectionEventModel model, DetectionDetailDto? fromModel)
    {
        if (model is null || !Shapes.TryGetValue(model, out var shape) || shape.Extras.Count == 0) return fromModel;

        var merged = fromModel ?? new DetectionDetailDto();
        merged.AdditionalData ??= new Dictionary<string, JToken>(StringComparer.Ordinal);
        foreach (var kv in shape.Extras)
            if (!merged.AdditionalData.ContainsKey(kv.Key)) merged.AdditionalData[kv.Key] = kv.Value.DeepClone();
        return merged;
    }

    /// <summary>
    /// 장애 PUT 의 detail — 서버에 있던 칸은 모델 값으로, 없던 칸은 <b>0 이면 싣지 않는다</b>(0 은 모델의 '없음' 이다).
    /// 서버 detail 이 null 이었고 모델 구간이 전부 0 이면 <c>null</c>(그대로 둔다). 모르는 키는 되붙인다.
    /// 곁들인 것이 없으면 종전처럼 네 칸을 모두 싣는다.
    /// </summary>
    public static MalfunctionDetailDto MergeMalfunction(IMalfunctionEventModel model, out bool keepNull)
    {
        keepNull = false;
        var all = new MalfunctionDetailDto
        {
            FirstStart = model.FirstStart,
            FirstEnd = model.FirstEnd,
            SecondStart = model.SecondStart,
            SecondEnd = model.SecondEnd,
        };
        if (!Shapes.TryGetValue(model, out var shape)) return all;

        int? Pick(string key, int value) => shape.Present.Contains(key) || value != 0 ? value : null;
        var merged = new MalfunctionDetailDto
        {
            FirstStart = Pick("first_start", model.FirstStart),
            FirstEnd = Pick("first_end", model.FirstEnd),
            SecondStart = Pick("second_start", model.SecondStart),
            SecondEnd = Pick("second_end", model.SecondEnd),
        };
        if (shape.Extras.Count > 0)
        {
            merged.AdditionalData = new Dictionary<string, JToken>(StringComparer.Ordinal);
            foreach (var kv in shape.Extras) merged.AdditionalData[kv.Key] = kv.Value.DeepClone();
        }

        var empty = merged.FirstStart is null && merged.FirstEnd is null && merged.SecondStart is null
                    && merged.SecondEnd is null && merged.AdditionalData is not { Count: > 0 };
        keepNull = empty && shape.WasNull;
        return merged;
    }
}
