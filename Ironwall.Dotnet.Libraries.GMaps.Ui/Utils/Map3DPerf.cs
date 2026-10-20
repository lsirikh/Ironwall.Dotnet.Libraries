using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 지도 3D 성능 계측 · 호스트 방식 순수 함수 — WPF 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DPerfTests).
                  ① WPF 프레임 간격(CompositionTarget.Rendering) · 2D 따라가기(Apply3DCameraTo2D) 시간 모으기 → 평균/95%/최대
                  ② 3D 가 몇 초마다 보내는 perf.stats(조작 중 move / 멈춤 idle 요약 줄) + GIS 쪽 값 → 로그 한 줄 "[Map3D][perf] …"
                  ③ 3D 호스트 방식 — 원격(RDP)이면 네이티브 창(설정 HwndWhenRemote, 2026-10-18 3D 느림 분석)
   Created On   : 2026-10-18 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>시간 표본(ms) 모음 — 로그 한 줄마다 요약을 꺼내고 비운다.</summary>
public sealed class Map3DPerfSamples
{
    private readonly List<double> _ms = new();
    private TimeSpan? _lastFrame;

    /// <summary>WPF 한 프레임(RenderingEventArgs.RenderingTime) — 같은 프레임에 여러 번 와도 한 번만, 1 s 넘는 틈(멈춤 · 숨김)은 뺀다.</summary>
    public void AddFrame(TimeSpan renderingTime)
    {
        if (_lastFrame is TimeSpan last)
        {
            if (renderingTime <= last) return;
            double ms = (renderingTime - last).TotalMilliseconds;
            if (ms < 1000d) _ms.Add(ms);
        }
        _lastFrame = renderingTime;
    }

    /// <summary>시간 하나(ms).</summary>
    public void Add(double ms)
    {
        if (double.IsFinite(ms) && ms >= 0) _ms.Add(ms);
    }

    /// <summary>"평균/95%/최대×개수"(없으면 "-")를 돌려주고 비운다.</summary>
    public string Take()
    {
        string s = Map3DPerf.Summary(_ms);
        _ms.Clear();
        return s;
    }

    /// <summary>프레임 기준점까지 지운다(다시 펼칠 때 — 접혀 있던 틈을 세지 않게).</summary>
    public void Reset()
    {
        _ms.Clear();
        _lastFrame = null;
    }
}

public static class Map3DPerf
{
    /// <summary>표본 → "평균/95%/최대×개수"(ms, 100 이상은 정수). 없으면 "-".</summary>
    public static string Summary(IReadOnlyCollection<double> ms)
    {
        if (ms.Count == 0) return "-";
        var s = ms.OrderBy(x => x).ToArray();
        double p95 = s[Math.Min(s.Length - 1, (int)Math.Ceiling(s.Length * 0.95) - 1)];
        return $"{Num(s.Average())}/{Num(p95)}/{Num(s[^1])}×{s.Length}";
    }

    /// <summary>
    /// 로그 한 줄 — GIS 쪽(호스트 방식 · 크기 · 원격 · WPF 렌더 계층 · WPF 프레임 간격 · 2D 따라가기 시간) + 3D 요약 줄(move · idle).
    /// 판정(PLAN): 3D 의 draw 간격 ≫ cpu · gpu 이고 wpf 간격도 길면 → WPF 안 그리기(컴포지션) · 원격 전달이 병목.
    /// </summary>
    public static string LogLine(IReadOnlyList<string> viewerLines, string hostMode, double width, double height, bool remote, int renderTier,
                                 string wpfFrames, string sync2D)
        => string.Format(CultureInfo.InvariantCulture, "[Map3D][perf] host={0} {1:0}x{2:0} remote={3} tier={4} wpf={5} sync2d={6} | {7}",
                         hostMode, width, height, remote ? "true" : "false", renderTier, wpfFrames, sync2D, string.Join(" | ", viewerLines));

    /// <summary>3D 화면을 WPF 안에서 그릴까 — 설정이 켜져 있고, 원격이면 HwndWhenRemote 가 꺼져 있을 때만.</summary>
    public static bool UseComposition(bool useCompositionHost, bool hwndWhenRemote, bool remote)
        => useCompositionHost && !(remote && hwndWhenRemote);

    private static string Num(double v) => v.ToString(v >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture);
}
