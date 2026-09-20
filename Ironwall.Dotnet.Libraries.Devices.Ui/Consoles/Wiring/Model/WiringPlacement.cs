using Newtonsoft.Json.Linq;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>
/// 센서 한 대의 결선 자리 — <b>어느 선(1차·2차)의 몇 번째</b>(WS L466).
/// </summary>
/// <param name="Line">1 = 1차(나감) · 2 = 2차(들어옴).</param>
/// <param name="Order">그 선에서의 순번(1부터). 칸 번호가 아니라 <b>찬 칸만 센 순번</b>이다(WS L681).</param>
public sealed record WiringPlacement(int Line, int Order)
{
    public bool IsPrimary => Line == WiringSpec.LINE_PRIMARY;

    /// <summary>"1차 4번" — 사람이 읽는 한 조각.</summary>
    public string Text => $"{Line}차 {Order}번";
}

/// <summary>
/// 결선을 서버의 어디에 싣는가 — <c>hardware_spec.spec.wiring = {"line":1,"order":4}</c>(WS §5 A안 · L466 · L475-480).
/// </summary>
/// <remarks>
/// <para><b>왜 여기인가</b> — 센서에 있는 것은 <c>controller_id</c> 와 버스 주소뿐이고, 회선·순번을 담는 서버 필드가
/// 문서 전체에 없다(WS L461). <c>hardware_spec.spec</c> 은 "표준 키 밖의 모든 것이 들어가는 유일한 자리"로
/// 서버가 선언한 자유 칸이라, 서버를 고치지 않고 오늘 쓸 수 있는 곳은 여기뿐이다.</para>
/// <para><b>다른 키를 건드리지 않는다</b> — <see cref="Apply"/> 는 받은 <c>spec</c> 을 깊은 복제한 뒤
/// <c>wiring</c> 한 칸만 바꾼다. 벤더가 넣어 둔 해상도·렌즈 같은 값이 우리 저장으로 사라지면 안 된다.</para>
/// <para><b>6.3 에서는 쓸 수 없다</b> — <c>HardwareSpecDto.ShouldSerializeSpec()</c> 이
/// <c>UseAxisWrite</c>(7.0+)일 때만 참이다. 그래서 입구를 감추고 저장 계층에서 한 번 더 막는다
/// (<see cref="Register.WiringWriteGuard"/>).</para>
/// </remarks>
public static class WiringSpec
{
    /// <summary><c>spec</c> 안에서 우리가 쓰는 유일한 키.</summary>
    public const string SPEC_KEY = "wiring";
    public const string LINE_KEY = "line";
    public const string ORDER_KEY = "order";

    public const int LINE_PRIMARY = 1;
    public const int LINE_SECONDARY = 2;

    /// <summary>한 선이 가질 수 있는 순번의 상한 — 칸 상한과 같다.</summary>
    public const int MAX_ORDER = WiringBoard.MAX_SLOTS;

    /// <summary>
    /// <paramref name="spec"/> 에서 결선 자리를 읽는다. 없거나 <b>말이 안 되면</b> <c>null</c> 이고,
    /// 까닭은 <see cref="Validate"/> 가 문장으로 돌려준다.
    /// </summary>
    public static WiringPlacement? Read(JObject? spec)
    {
        if (spec?[SPEC_KEY] is not JObject wiring) return null;

        var line = AsInt(wiring[LINE_KEY]);
        var order = AsInt(wiring[ORDER_KEY]);
        if (line is null || order is null) return null;
        if (line is not (LINE_PRIMARY or LINE_SECONDARY)) return null;
        if (order < 1 || order > MAX_ORDER) return null;

        return new WiringPlacement(line.Value, order.Value);
    }

    /// <summary>
    /// 저장된 값이 <b>있는데 읽을 수 없는</b> 경우의 까닭(사람 말). 읽을 수 있거나 아예 없으면 <c>null</c>.
    /// </summary>
    public static string? Validate(JObject? spec)
    {
        if (spec is null || spec[SPEC_KEY] is null or JValue { Type: JTokenType.Null }) return null;
        if (spec[SPEC_KEY] is not JObject wiring) return "결선 값의 모양이 달라 읽지 못했습니다 — 다시 배치해 주세요.";

        var line = AsInt(wiring[LINE_KEY]);
        var order = AsInt(wiring[ORDER_KEY]);
        if (line is null) return "결선에 선 번호가 없습니다 — 다시 배치해 주세요.";
        if (order is null) return "결선에 순번이 없습니다 — 다시 배치해 주세요.";
        if (line is not (LINE_PRIMARY or LINE_SECONDARY)) return $"선 번호 {line} 은 1차·2차가 아닙니다 — 다시 배치해 주세요.";
        if (order < 1 || order > MAX_ORDER) return $"순번 {order} 이 쓸 수 있는 범위(1~{MAX_ORDER}) 밖입니다 — 다시 배치해 주세요.";
        return null;
    }

    /// <summary>
    /// 받은 <c>spec</c> 에 결선 자리를 얹은 <b>새 객체</b> — <b>만들기(POST)</b> 용이다.
    /// <paramref name="placement"/> 가 <c>null</c> 이면 키를 뺀다. 나머지 키는 그대로 옮긴다.
    /// </summary>
    /// <remarks>원본을 고치지 않는다 — 재조회한 DTO 는 비교의 기준이라 손대면 충돌 판정이 거짓이 된다.</remarks>
    public static JObject Apply(JObject? spec, WiringPlacement? placement)
    {
        var next = spec is null ? new JObject() : (JObject)spec.DeepClone();

        if (placement is null)
        {
            next.Remove(SPEC_KEY);
            return next;
        }

        next[SPEC_KEY] = Node(placement);
        return next;
    }

    /// <summary>
    /// <b>고치기(PATCH)</b> 용 <c>spec</c> 조각 — 우리 키 <b>하나만</b> 담는다.
    /// 자리를 비울 때는 키를 빼는 것이 아니라 <b>명시적 <c>null</c></b> 을 보낸다.
    /// </summary>
    /// <remarks>
    /// <para><b>서버 실측(읽어서 확인 · 2026-09-20)</b> — 장비 PATCH 는 축을 <b>RFC 7396 으로 병합</b>한다:
    /// <c>app/routers/sensors.py:241-261</c>("센서 부분 수정 — 축은 RFC 7396 병합") →
    /// <c>app/services/device_axes_io.py:291-299 apply_on_patch</c>
    /// (<c>json_merge_patch(기존 축, _dump(보낸 값, patch: true))</c>) →
    /// <c>app/services/json_merge.py:25-37</c>: 중첩 dict 는 <b>재귀 병합</b>(L33-34) ·
    /// 값이 <c>null</c> 인 키는 <b>삭제</b>(L31-32) · 그 밖은 덮어쓰기. 그리고
    /// <c>_dump(..., patch: true)</c>(<c>device_axes_io.py:93-102</c>)는 PATCH 에서 명시적 null 을 <b>남긴다</b>.</para>
    /// <para>그러므로 ① <b>안 보낸 키는 그대로 남고</b>(그래서 우리 키 하나만 보내면 벤더 키를 건드리지 않는다)
    /// ② <b>키를 빼는 것으로는 지워지지 않는다</b> — 자리를 비우는 저장이 조용히 아무 일도 하지 않게 된다.</para>
    /// </remarks>
    public static JObject MergePatch(WiringPlacement? placement)
        => new() { [SPEC_KEY] = placement is null ? JValue.CreateNull() : Node(placement) };

    private static JObject Node(WiringPlacement placement) => new()
    {
        [LINE_KEY] = placement.Line,
        [ORDER_KEY] = placement.Order,
    };

    /// <summary>두 결선 자리가 같은가(둘 다 없어도 같다) — 재조회 충돌 판정.</summary>
    public static bool SamePlacement(WiringPlacement? a, WiringPlacement? b)
        => a is null ? b is null : b is not null && a.Line == b.Line && a.Order == b.Order;

    private static int? AsInt(JToken? token) => token?.Type switch
    {
        JTokenType.Integer => (int)token,
        JTokenType.Float => (int)Math.Round((double)token),
        JTokenType.String => int.TryParse((string?)token, out var v) ? v : null,
        _ => null,
    };
}
