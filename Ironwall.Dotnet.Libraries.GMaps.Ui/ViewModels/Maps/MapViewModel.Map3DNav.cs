using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 3D 방향 링 · 이동 단추(Map3DNavControl, 2026-10-19) — 링 끌기 · 구 손잡이 · 정북 · 위에서 보기 · 기본 비스듬히 · 이동 · 확대 축소를
                  3D 카메라 메시지(camera.nudge · camera.preset, Map3DNavMath)로 보낸다.
   Note         : ★ 3D 가 보이는 동안만(IsMap3DShown) — 그 밖에는 보내지 않고, 2D 로 돌아가면 누르고 있던 이동을 멈춘다(SetMap3DState).
                  ★ 링 방위는 3D 가 알려 주는 camera 메시지를 따른다(OnMap3DMessage → SetMap3DNavCamera). 끄는 동안은 보낸 만큼 먼저 돌려
                    링이 손을 따라가게 하고, 3D 가 다 옮긴 뒤 알려 주는 방위로 맞춘다.
                  ★ 누르고 있는 이동은 시간 기반(Map3DNavMover — 초당 카메라 거리 비율) — 타이머 간격이 흔들려도 같은 속도.
                  ★ 3D 가 보이는 동안 2D 나침반 대신 이 링을 보인다(IsCompassShown).
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    /// <summary>누르고 있는 이동을 3D 로 보내는 간격.</summary>
    private static readonly TimeSpan Map3DNavTickInterval = TimeSpan.FromMilliseconds(40);

    private double _map3DNavBearing;
    private Map3DNavMover? _map3DNavMover;
    private DispatcherTimer? _map3DNavTimer;
    /// <summary>시계(시험에서 바꾼다).</summary>
    private Func<DateTime>? _map3DNavClock;

    private Map3DNavMover Map3DNavMover => _map3DNavMover ??= new Map3DNavMover();
    private DateTime Map3DNavNow => (_map3DNavClock ?? (() => DateTime.UtcNow))();

    /// <summary>링이 돌아갈 각도 — 정북(N)이 화면에서 진짜 북쪽을 가리킨다.</summary>
    public double Map3DNavRingAngle => Map3DNavMath.RingAngle(_map3DNavBearing);

    /// <summary>링 아래 지금 방위("방위 123°").</summary>
    public string Map3DNavBearingText => $"방위 {Math.Round(Map3DNavMath.Normalize(_map3DNavBearing)) % 360:0}°";

    /// <summary>2D 나침반 보임 — 보기 메뉴에서 켰고 3D 가 아닐 때(3D 에서는 방향 링이 대신한다).</summary>
    public bool IsCompassShown => IsCompassVisible && !IsMap3DShown;

    private RelayCommand? _map3DNorthCommand, _map3DTopViewCommand, _map3DDefaultViewCommand;
    /// <summary>링의 N — 정북으로.</summary>
    public RelayCommand Map3DNorthCommand => _map3DNorthCommand ??= new RelayCommand(_ => PresetMap3D(Map3DNavMath.PresetNorth));
    /// <summary>「위에서 보기」 — 기울기 0.</summary>
    public RelayCommand Map3DTopViewCommand => _map3DTopViewCommand ??= new RelayCommand(_ => PresetMap3D(Map3DNavMath.PresetTop));
    /// <summary>「기본 비스듬히」 — 처음 진입 기울기.</summary>
    public RelayCommand Map3DDefaultViewCommand => _map3DDefaultViewCommand ??= new RelayCommand(_ => PresetMap3D(Map3DNavMath.PresetDefault));

    /// <summary>링 · 손잡이 끌기 · 키 — 조금 돌리고 · 기울이고 · 옮긴다. 링은 보낸 방위만큼 먼저 돈다.</summary>
    public void NudgeMap3D(Map3DNudge nudge)
    {
        if (!IsMap3DShown || nudge.IsZero) return;
        Map3DHost.PostJson(Map3DNavMath.NudgeJson(nudge));
        if (nudge.BearingDelta != 0) SetMap3DNavBearing(_map3DNavBearing + nudge.BearingDelta);
    }

    /// <summary>정해 둔 시점(north · top · default). 정북은 링을 먼저 돌린다.</summary>
    public void PresetMap3D(string name)
    {
        if (!IsMap3DShown) return;
        Map3DHost.PostJson(Map3DNavMath.PresetJson(name));
        if (name == Map3DNavMath.PresetNorth) SetMap3DNavBearing(0);
    }

    /// <summary>이동 단추 · 키 누름 — 곧바로 한 걸음, 누르고 있는 동안 계속.</summary>
    public void StartMap3DMove(Map3DNavMove move)
    {
        if (!IsMap3DShown) return;
        if (Map3DNavMover.Start(move, Map3DNavNow) is { } first) NudgeMap3D(first);
        if (!Map3DNavMover.IsActive) return;
        if (_map3DNavTimer == null)
        {
            _map3DNavTimer = new DispatcherTimer { Interval = Map3DNavTickInterval };
            _map3DNavTimer.Tick += (_, __) =>
            {
                try { TickMap3DMove(); }
                catch (Exception ex) { StopAllMap3DMoves(); _log?.Error($"[Map3D] 이동 처리 실패(격리): {ex.Message}"); }
            };
        }
        _map3DNavTimer.Start();
    }

    /// <summary>이동 단추 · 키 뗌.</summary>
    public void StopMap3DMove(Map3DNavMove move)
    {
        Map3DNavMover.Stop(move);
        if (!Map3DNavMover.IsActive) _map3DNavTimer?.Stop();
    }

    /// <summary>모두 뗌 — 초점 · 캡처를 잃었거나 3D 를 나갔다.</summary>
    public void StopAllMap3DMoves()
    {
        _map3DNavMover?.StopAll();
        _map3DNavTimer?.Stop();
    }

    /// <summary>누르고 있는 동안 타이머마다 — 흐른 시간만큼 보낸다.</summary>
    private void TickMap3DMove()
    {
        if (!IsMap3DShown) { StopAllMap3DMoves(); return; }
        if (Map3DNavMover.Tick(Map3DNavNow) is { } n) NudgeMap3D(n);
        if (!Map3DNavMover.IsActive) _map3DNavTimer?.Stop();
    }

    /// <summary>3D 가 알려 준 카메라 — 링 방위를 맞춘다.</summary>
    private void SetMap3DNavCamera(Map3DCamera cam)
    {
        if (double.IsFinite(cam.Bearing)) SetMap3DNavBearing(cam.Bearing);
    }

    private void SetMap3DNavBearing(double bearing)
    {
        bearing = Map3DNavMath.Normalize(bearing);
        if (bearing == _map3DNavBearing) return;
        _map3DNavBearing = bearing;
        NotifyOfPropertyChange(nameof(Map3DNavRingAngle));
        NotifyOfPropertyChange(nameof(Map3DNavBearingText));
    }
}
