using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;

/// <summary>
/// 같은 장비에서 짧은 시간(<see cref="Window"/>) 안에 이어진 문 위치 부품 전이를 운영 카드 한 줄 흐름으로 —
/// 「문 구동부 열림 → 도어 센서 열림」.
/// </summary>
/// <remarks>
/// 서버는 부품마다 따로 운영 이벤트를 낸다(구동부 RUNNING→OPEN 과 도어 센서 CLOSED→OPEN 이 두 건 — RUNNING 으로 바뀔 때는 이벤트가 없다).
/// 카드를 합치면 종류 + 번호 중복 · 조치보고 · 500장 상한 규칙이 흔들리므로 카드는 그대로 두고, 나중 카드에 앞선 전이를 <b>보이기만</b> 한다.
/// 카드가 닫혀도 흐름은 남도록 카드 목록이 아니라 받은 전이를 기억한다.
/// </remarks>
public sealed class OperationComponentFlow
{
    /// <summary>한 흐름으로 묶는 시간 — 이번 전이 앞으로 이만큼 안의 같은 장비 전이.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(30);

    /// <summary>카드 한 줄에 넣는 칸 수(최근 것부터) — 카드 폭(최대 224)에 넘치지 않게.</summary>
    public const int MaxSteps = 3;

    private readonly Dictionary<int, List<(int EventId, DateTime At, string Step)>> _byDevice = new();

    /// <summary>
    /// 이번 전이를 기억하고, 같은 장비의 앞선 전이가 <see cref="Window"/> 안에 있으면 시간순 흐름 글자를, 없으면 null.
    /// 같은 번호가 다시 오면(재전송) 한 번만 센다.
    /// </summary>
    public string? Add(int deviceId, int eventId, DateTime at, string step)
    {
        lock (_byDevice)
        {
            if (!_byDevice.TryGetValue(deviceId, out var steps)) _byDevice[deviceId] = steps = new();
            if (!steps.Any(s => s.EventId == eventId)) steps.Add((eventId, at, step));
            steps.RemoveAll(s => at - s.At > Window);   // 오래된 것은 버린다 — 장비마다 몇 칸만 남는다

            // 이번 전이까지만(늦게 온 옛 전이의 카드에 그 뒤 일을 싣지 않는다) — 이번 전이가 흐름의 맨 끝.
            var flow = steps.Where(s => s.At < at || (s.At == at && s.EventId <= eventId)).OrderBy(s => s.At).ThenBy(s => s.EventId).ToList();
            if (flow.Count < 2) return null;
            return string.Join(" → ", flow.Skip(Math.Max(0, flow.Count - MaxSteps)).Select(s => s.Step));
        }
    }
}
