using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>
/// 펜스 구성 문서 ↔ JSON 본문 — 저장소 구현 · 시험이 같은 규칙을 쓴다. 열거형은 <b>이름 글자</b>(정수 값이 바뀌어도 뜻이 남는다),
/// 읽을 때는 값을 범위 안으로 맞추고(망 · 자리) 읽을 수 없는 본문은 <c>null</c> 이다.
/// </summary>
public static class FenceLayoutJson
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Converters = { new StringEnumConverter() },
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        Formatting = Formatting.None,
    };

    public static string Serialize(FenceLayoutDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonConvert.SerializeObject(document, Settings);
    }

    /// <summary>
    /// 본문 → 문서. <paramref name="revision"/> · <paramref name="updatedAt"/> 은 저장소 행의 값을 얹는다. 읽을 수 없으면 <c>null</c>.
    /// </summary>
    public static FenceLayoutDocument? Deserialize(string? json, int revision = 0, DateTime? updatedAt = null)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var raw = JsonConvert.DeserializeObject<FenceLayoutDocument>(json, Settings);
            if (raw is null) return null;
            var panels = (raw.Panels ?? Array.Empty<FencePanelSpec>()).Where(p => p is not null).Select(p => p.Normalized()).ToList();
            var mounts = (raw.Mounts ?? new Dictionary<int, SensorMountSpec>())
                .Where(p => p.Value is not null)
                .ToDictionary(p => p.Key, p => FenceLayoutMath.Normalize(p.Value, panels));
            // schema 1 → 2: 줄이 없던 문서 — 자리마다 아래 줄(칸이 없어 기본값으로 읽힌다) · 제어기 왼쪽 · VBus 기본.
            var migrated = raw.Schema < 2;
            return new FenceLayoutDocument
            {
                Schema = FenceLayoutDocument.SCHEMA,
                ControllerId = raw.ControllerId,
                Panels = panels,
                Mounts = migrated ? mounts.ToDictionary(p => p.Key, p => p.Value with { Lane = FenceLane.Lower }) : mounts,
                Bands = raw.Bands is { Bands: not null } bands ? bands : null,
                FenceSpacingM = raw.FenceSpacingM is { } s && double.IsFinite(s) && s > 0 ? s : null,
                ControllerEnd = migrated || !Enum.IsDefined(raw.ControllerEnd) ? FenceControllerEnd.Left : raw.ControllerEnd,
                VbusGap = migrated || raw.VbusGap is not { } g || g < 0 ? null : g,
                Revision = revision,
                UpdatedAt = updatedAt,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
