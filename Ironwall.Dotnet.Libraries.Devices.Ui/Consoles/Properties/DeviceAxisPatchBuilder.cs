using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
/****************************************************************************
   Purpose      : 상세 폼의 축 값 편집(접속 · 형상 · 부대 · 설정) → 좁은 PATCH 본문 (순수 함수)
   Created By   : GHLee
   Created On   : 9/27/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 손댄 축 값 칸만으로 <c>PATCH /devices/{종류}/{id}</c> 본문을 만든다 — <b>보낸 키만 바뀐다</b>.
/// </summary>
/// <remarks>
/// <para><b>왜 DTO 가 아닌가</b> — 장비 DTO 는 축 모드에서 조건 없이 직렬화되는 키(재조립 <c>connection</c> ·
/// <c>description</c> …)가 있어 빈 DTO 로 PATCH 를 만들면 null 키가 서버 값을 지운다. 여기서는 명세의
/// <see cref="DevicePropertySpec.AxisWritePath"/> 로 중첩 객체를 만들 뿐이다 — 서버는 축 객체를 병합하므로
/// <c>{"connection":{"channel":3}}</c> 는 채널만 바꾸고 나머지 접속 칸 · 부품 배열은 그대로 둔다.</para>
/// <para><b>빈 칸 = 지운다</b>(JSON <c>null</c>, RFC 7396). 서버가 NOT NULL 로 받는 칸(<see cref="DevicePropertySpec.AxisAllowsClear"/>
/// = false)은 보내기 전에 막는다 — 서버 422 를 받기 전에 운영자에게 한국어로 알린다.</para>
/// </remarks>
public static class DeviceAxisPatchBuilder
{
    /// <summary>칸 하나의 입력 글을 JSON 값으로 바꾼다. 못 바꾸면 false + 한국어 까닭.</summary>
    public static bool TryConvert(DevicePropertySpec spec, string? text, out JToken value, out string? error)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        value = JValue.CreateNull();
        error = null;

        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            if (spec.AxisAllowsClear) return true;
            error = $"{spec.Label} 값은 비울 수 없습니다.";
            return false;
        }

        switch (spec.AxisValueKind)
        {
            case DeviceAxisValueKind.Integer:
                if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    error = $"{spec.Label} 값은 정수여야 합니다.";
                    return false;
                }
                value = new JValue(integer);
                return true;

            case DeviceAxisValueKind.Number:
                if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                    || double.IsNaN(number) || double.IsInfinity(number))
                {
                    error = $"{spec.Label} 값은 숫자여야 합니다.";
                    return false;
                }
                value = new JValue(number);
                return true;

            case DeviceAxisValueKind.Boolean:
                if (!bool.TryParse(trimmed, out var flag))
                {
                    error = $"{spec.Label} 값은 켜기 또는 끄기여야 합니다.";
                    return false;
                }
                value = new JValue(flag);
                return true;

            default:
                value = new JValue(trimmed);
                return true;
        }
    }

    /// <summary>
    /// 칸들로 본문 하나를 만든다. 같은 경로가 두 번 오면 뒤의 것이 이긴다. 바꿀 수 없는 입력이 있으면 예외 —
    /// 폼이 <see cref="TryConvert"/> 로 먼저 걸러 이 지점에서는 일어나지 않는다.
    /// </summary>
    public static JObject Build(IEnumerable<(DevicePropertySpec Spec, string Text)> edits)
    {
        if (edits == null) throw new ArgumentNullException(nameof(edits));
        var body = new JObject();
        foreach (var (spec, text) in edits)
        {
            if (spec.AxisWritePath is not { Length: > 0 } path)
                throw new ArgumentException($"{spec.Key} 은(는) 축 값 칸이 아닙니다.", nameof(edits));
            if (!TryConvert(spec, text, out var value, out var error))
                throw new ArgumentException(error, nameof(edits));
            Put(body, path, value);
        }
        return body;
    }

    private static void Put(JObject body, string path, JToken value)
    {
        var segments = path.Split('.');
        var node = body;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (node[segments[i]] is not JObject child)
            {
                child = new JObject();
                node[segments[i]] = child;
            }
            node = child;
        }
        node[segments[^1]] = value;
    }
}
