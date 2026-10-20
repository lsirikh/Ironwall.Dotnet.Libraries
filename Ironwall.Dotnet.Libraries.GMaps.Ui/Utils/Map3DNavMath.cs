using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 3D 방향 링 · 이동 단추(Map3DNavControl) 순수 함수 — WPF 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DNavMathTests).
                  ① 링 각도(위 = 0, 시계 +) · 360° 감기 ② 링 끌기 · 구 손잡이(기울기) 끌기 판정(데드존 · 캡처 잃음 취소)
                  ③ 누르고 있는 동안 계속 이동(시간 기반 — 초당 카메라 거리 비율 · 줌 단계) ④ 키보드 폴백 ⑤ 3D 로 보낼 메시지.
   Note         : 메시지 — {type:"camera.nudge", bearingDelta, pitchDelta, forward, right, zoom} · {type:"camera.preset", name}.
                  forward · right 는 카메라 거리 비율이라 멀리서 보면 멀리 간다(map-3d camsync.nudgeView). 한계(기울기 0~85 · 거리 ·
                  장면 영역)는 3D 가 지킨다. 데드존은 호출부가 DragMath.DeadZone(8 DIU)을 넘긴다 — 시험 프로젝트가 WPF 없이 링크하므로.
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>3D 카메라 조금 움직이기 — 방위 · 기울기(도), 전진 · 오른쪽(카메라 거리 비율), 줌(단계, + 확대).</summary>
public readonly record struct Map3DNudge(double BearingDelta, double PitchDelta, double Forward, double Right, double Zoom)
{
    public bool IsZero => BearingDelta == 0 && PitchDelta == 0 && Forward == 0 && Right == 0 && Zoom == 0;
}

/// <summary>이동 단추 · 키 — 바라보는 방향 기준 지면 이동과 확대 · 축소.</summary>
public enum Map3DNavMove { Forward, Back, Left, Right, ZoomIn, ZoomOut }

public static class Map3DNavMath
{
    /// <summary>정해 둔 시점 이름(map-3d camsync.presetView).</summary>
    public const string PresetNorth = "north", PresetTop = "top", PresetDefault = "default";
    /// <summary>링 키보드 ←/→ 한 번에 돌릴 방위(도).</summary>
    public const double KeyBearingStep = 15.0;
    /// <summary>링 키보드 ↑/↓ 한 번에 바꿀 기울기(도).</summary>
    public const double KeyPitchStep = 5.0;
    /// <summary>구 손잡이를 1 DIU 끌 때 바뀌는 기울기(도) — 위로 끌면 눕고(수평 쪽) 아래로 끌면 위에서 보기 쪽.</summary>
    public const double TiltDegPerDiu = 0.5;
    /// <summary>계속 이동 속도 — 초당 카메라 거리 비율.</summary>
    public const double MoveSpeedPerSecond = 0.5;
    /// <summary>계속 확대 · 축소 속도 — 초당 줌 단계.</summary>
    public const double ZoomSpeedPerSecond = 2.0;
    /// <summary>누르자마자 한 번 움직이는 양(초) — 짧게 눌렀다 떼도 움직인다.</summary>
    public const double FirstStepSeconds = 0.15;
    /// <summary>한 번에 셈하는 시간 상한(초) — 멈췄던 타이머가 한꺼번에 멀리 보내지 않게.</summary>
    public const double MaxStepSeconds = 0.2;
    /// <summary>링 가운데에서 이보다 가까운 점은 각도가 흔들려 무시한다(DIU).</summary>
    public const double MinRingRadius = 4.0;

    /// <summary>0 이상 360 미만으로.</summary>
    public static double Normalize(double deg) => ((deg % 360.0) + 360.0) % 360.0;

    /// <summary>from → to 의 짧은 쪽 각도 차(-180 초과 180 이하) — 360° 를 넘겨 돌려도 끊기지 않는다.</summary>
    public static double WrapDelta(double from, double to)
    {
        var d = Normalize(to - from);
        return d > 180.0 ? d - 360.0 : d;
    }

    /// <summary>가운데(cx, cy)에서 점(x, y)의 각도 — 화면 위 = 0, 시계 방향 +(화면 y 는 아래로 +).</summary>
    public static double AngleDeg(double cx, double cy, double x, double y)
        => Normalize(Math.Atan2(x - cx, cy - y) * 180.0 / Math.PI);

    /// <summary>링이 돌아갈 각도 — 3D 방위(화면 위가 가리키는 방위)가 b 면 정북은 화면 위에서 반시계 b.</summary>
    public static double RingAngle(double map3DBearing) => -Normalize(map3DBearing);

    /// <summary>링 끌기 각도 차 → 방위 변화. 링(N)이 손가락을 따라 돌므로 시계로 끌면 정북이 시계로 가고 방위는 줄어든다.</summary>
    public static double BearingDeltaForRing(double ringDeltaDeg) => -ringDeltaDeg;

    /// <summary>구 손잡이 세로 끌기(이번 이동 dy, 아래 +) → 기울기 변화.</summary>
    public static double PitchDeltaForTilt(double dy) => -dy * TiltDegPerDiu;

    /// <summary>
    /// 키보드 폴백(WPF Key 이름) — 링 ←/→ 방위 · ↑/↓ 기울기. 방향 키는 "고개를 돌리는" 쪽(→ = 오른쪽으로 돌기 = 방위 +, ↑ = 눕히기).
    /// 아니면 null.
    /// </summary>
    public static Map3DNudge? KeyNudge(string key) => key switch
    {
        "Left" => new Map3DNudge(-KeyBearingStep, 0, 0, 0, 0),
        "Right" => new Map3DNudge(KeyBearingStep, 0, 0, 0, 0),
        "Up" => new Map3DNudge(0, KeyPitchStep, 0, 0, 0),
        "Down" => new Map3DNudge(0, -KeyPitchStep, 0, 0, 0),
        _ => null,
    };

    /// <summary>키보드 폴백 — W/A/S/D 이동, +/- 확대 · 축소(숫자판 포함). 아니면 null.</summary>
    public static Map3DNavMove? KeyMove(string key) => key switch
    {
        "W" => Map3DNavMove.Forward,
        "S" => Map3DNavMove.Back,
        "A" => Map3DNavMove.Left,
        "D" => Map3DNavMove.Right,
        "Add" or "OemPlus" => Map3DNavMove.ZoomIn,
        "Subtract" or "OemMinus" => Map3DNavMove.ZoomOut,
        _ => null,
    };

    /// <summary>키보드 폴백 — N = 정북. 아니면 null.</summary>
    public static string? KeyPreset(string key) => key == "N" ? PresetNorth : null;

    /// <summary>이동 방향들을 seconds 초 동안 누르고 있었을 때 움직일 양.</summary>
    public static Map3DNudge MoveFor(IEnumerable<Map3DNavMove> moves, double seconds)
    {
        double f = 0, r = 0, z = 0, m = MoveSpeedPerSecond * seconds, zs = ZoomSpeedPerSecond * seconds;
        foreach (var mv in moves)
        {
            switch (mv)
            {
                case Map3DNavMove.Forward: f += m; break;
                case Map3DNavMove.Back: f -= m; break;
                case Map3DNavMove.Right: r += m; break;
                case Map3DNavMove.Left: r -= m; break;
                case Map3DNavMove.ZoomIn: z += zs; break;
                case Map3DNavMove.ZoomOut: z -= zs; break;
            }
        }
        return new Map3DNudge(0, 0, f, r, z);
    }

    /// <summary>{type:"camera.nudge", …} — 3D 가 짧게 옮긴다.</summary>
    public static string NudgeJson(Map3DNudge n)
        => new JObject
        {
            ["type"] = "camera.nudge",
            ["bearingDelta"] = n.BearingDelta,
            ["pitchDelta"] = n.PitchDelta,
            ["forward"] = n.Forward,
            ["right"] = n.Right,
            ["zoom"] = n.Zoom,
        }.ToString(Formatting.None);

    /// <summary>{type:"camera.preset", name} — north(정북) · top(위에서 보기) · default(처음 진입 기울기).</summary>
    public static string PresetJson(string name)
        => new JObject { ["type"] = "camera.preset", ["name"] = name }.ToString(Formatting.None);
}

/// <summary>
/// 링 · 구 손잡이 끌기 판정(캡처 드래그 — 눌림 · 데드존 통과 · 끝 분리). 이동마다 3D 로 보낼 변화량을 돌려준다.
/// 링은 각도를 이어 붙여(<see cref="Map3DNavMath.WrapDelta"/>) 몇 바퀴를 돌려도 끊기지 않는다.
/// </summary>
public sealed class Map3DNavDrag
{
    private readonly bool _ring;
    private readonly double _deadZone;
    private double _cx, _cy, _px, _py, _lastY, _lastAngle;

    /// <param name="ring">true = 링(방위), false = 구 손잡이(기울기).</param>
    /// <param name="deadZone">데드존(DIU) — 호출부가 DragMath.DeadZone 을 넘긴다.</param>
    public Map3DNavDrag(bool ring, double deadZone)
    {
        _ring = ring;
        _deadZone = deadZone;
    }

    public bool IsPressed { get; private set; }
    public bool IsDragging { get; private set; }
    /// <summary>이번 끌기에서 보낸 방위(링) 또는 기울기(손잡이) 변화 합.</summary>
    public double Total { get; private set; }

    /// <summary>눌렀다 — 가운데(cx, cy)는 링 중심(손잡이는 쓰지 않는다).</summary>
    public void Press(double cx, double cy, double x, double y)
    {
        _cx = cx; _cy = cy; _px = x; _py = _lastY = y;
        _lastAngle = Map3DNavMath.AngleDeg(cx, cy, x, y);
        IsPressed = true;
        IsDragging = false;
        Total = 0;
    }

    /// <summary>움직였다 — 데드존을 넘은 뒤부터 3D 로 보낼 변화(없으면 null).</summary>
    public Map3DNudge? Move(double x, double y)
    {
        if (!IsPressed) return null;
        if (!IsDragging)
        {
            double dx = x - _px, dy = y - _py;
            if (dx * dx + dy * dy <= _deadZone * _deadZone) return null;
            IsDragging = true;   // 데드존 안에서 움직인 만큼은 버리지 않는다 — 누른 자리부터 이어서 센다
        }
        Map3DNudge n;
        if (_ring)
        {
            if (Math.Sqrt((x - _cx) * (x - _cx) + (y - _cy) * (y - _cy)) < Map3DNavMath.MinRingRadius) return null;
            var a = Map3DNavMath.AngleDeg(_cx, _cy, x, y);
            var d = Map3DNavMath.BearingDeltaForRing(Map3DNavMath.WrapDelta(_lastAngle, a));
            _lastAngle = a;
            n = new Map3DNudge(d, 0, 0, 0, 0);
        }
        else
        {
            n = new Map3DNudge(0, Map3DNavMath.PitchDeltaForTilt(y - _lastY), 0, 0, 0);
            _lastY = y;
        }
        if (n.IsZero) return null;
        Total += _ring ? n.BearingDelta : n.PitchDelta;
        return n;
    }

    /// <summary>
    /// 끝(뗌 · 캡처 잃음 — 한 경로). 끌었으면 true. 캡처를 잃어도(commit = false) 이미 보낸 회전은 그대로 둔다 —
    /// 3D 카메라는 보낸 만큼 이미 움직였고 링은 3D 가 알려 주는 방위를 따라간다. 이후 움직임은 보내지 않는다.
    /// </summary>
    public bool Finish(bool commit)
    {
        bool dragged = IsDragging;
        IsPressed = false;
        IsDragging = false;
        return dragged && commit;
    }
}

/// <summary>
/// 이동 단추 · 키를 누르고 있는 동안 계속 이동 — 시간 기반(누른 시간 × 속도, 프레임 수와 무관). 시계는 호출부가 넘긴다(가짜 시계 시험).
/// </summary>
public sealed class Map3DNavMover
{
    private readonly HashSet<Map3DNavMove> _held = new();
    private DateTime _last;

    public bool IsActive => _held.Count > 0;
    public IReadOnlyCollection<Map3DNavMove> Held => _held;

    /// <summary>누름 — 처음 누른 방향이면 곧바로 한 걸음(<see cref="Map3DNavMath.FirstStepSeconds"/>)을 돌려준다(이미 누르고 있으면 null).</summary>
    public Map3DNudge? Start(Map3DNavMove move, DateTime now)
    {
        if (!_held.Add(move)) return null;
        if (_held.Count == 1) _last = now;
        return Map3DNavMath.MoveFor(new[] { move }, Map3DNavMath.FirstStepSeconds);
    }

    /// <summary>뗌 — 다 떼면 멈춘다.</summary>
    public void Stop(Map3DNavMove move) => _held.Remove(move);

    /// <summary>모두 떼기(캡처 잃음 · 초점 잃음 · 3D 나가기).</summary>
    public void StopAll() => _held.Clear();

    /// <summary>타이머 한 번 — 지난번부터 흐른 시간(상한 <see cref="Map3DNavMath.MaxStepSeconds"/>)만큼 움직일 양. 누른 것이 없으면 null.</summary>
    public Map3DNudge? Tick(DateTime now)
    {
        if (_held.Count == 0) return null;
        double s = Math.Min(Map3DNavMath.MaxStepSeconds, Math.Max(0, (now - _last).TotalSeconds));
        _last = now;
        if (s <= 0) return null;
        var n = Map3DNavMath.MoveFor(_held, s);
        return n.IsZero ? null : n;
    }
}

/// <summary>
/// 조절기 판 옮기기(2026-10-20) — 손잡이 끌기 판정(눌림 · 데드존 통과 · 끝 분리, 취소면 처음 자리)과 화면 안 붙잡기.
/// 좌표는 부모(지도 칸) 기준 — 판이 움직여도 기준이 흔들리지 않는다. 데드존은 호출부가 DragMath.DeadZone(8 DIU)을 넘긴다.
/// </summary>
public sealed class Map3DNavPanelMove
{
    private readonly double _deadZone;
    private double _startX, _startY, _px, _py;

    public Map3DNavPanelMove(double deadZone) => _deadZone = deadZone;

    public bool IsPressed { get; private set; }
    public bool IsDragging { get; private set; }
    /// <summary>옮긴 양(원래 자리 기준) — 붙잡기 전 값.</summary>
    public double X { get; private set; }
    public double Y { get; private set; }

    /// <summary>눌렀다 — 지금 옮긴 양(offX, offY)과 누른 자리(부모 기준).</summary>
    public void Press(double offX, double offY, double px, double py)
    {
        _startX = X = offX; _startY = Y = offY; _px = px; _py = py;
        IsPressed = true;
        IsDragging = false;
    }

    /// <summary>움직였다 — 데드존을 넘은 뒤부터 true(X · Y 갱신). 데드존 안에서 움직인 만큼도 버리지 않는다.</summary>
    public bool Move(double px, double py)
    {
        if (!IsPressed) return false;
        double dx = px - _px, dy = py - _py;
        if (!IsDragging)
        {
            if (dx * dx + dy * dy <= _deadZone * _deadZone) return false;
            IsDragging = true;
        }
        X = _startX + dx; Y = _startY + dy;
        return true;
    }

    /// <summary>끝(뗌 · 캡처 잃음 · Esc — 한 경로). 취소(commit = false)면 처음 자리로. 끌었고 확정이면 true.</summary>
    public bool Finish(bool commit)
    {
        bool dragged = IsDragging && commit;
        if (!commit) { X = _startX; Y = _startY; }
        IsPressed = false;
        IsDragging = false;
        return dragged;
    }

    /// <summary>
    /// 판이 지도 칸(left · top · right · bottom) 밖으로 나가지 않게 옮긴 양을 붙잡는다. baseX · baseY = 옮기기 전 판 왼쪽 위, w · h = 판 크기.
    /// 판이 칸보다 크면 왼쪽 · 위에 맞춘다.
    /// </summary>
    public static (double X, double Y) Clamp(double offX, double offY, double baseX, double baseY, double w, double h,
        double left, double top, double right, double bottom)
    {
        static double One(double off, double b, double size, double lo, double hi)
        {
            double min = lo - b, max = hi - size - b;
            return max < min ? min : Math.Min(max, Math.Max(min, off));
        }
        return (One(offX, baseX, w, left, right), One(offY, baseY, h, top, bottom));
    }
}
