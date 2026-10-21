using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 3D 환경(해 · 날씨 · 차 · 사람) 배선 — 3D 위 「환경」 판의 DataContext(Map3DEnvironment)를 내고,
                  3D 가 장면을 그리면(rendered) env.set 을 보내게 · env.state(해 뜸/짐)를 판에 넘긴다(env3d-contract C).
   Note         : VM 은 DI 싱글톤(설정 화면 묶음과 같은 인스턴스) — 없으면(시험 · 미등록) 판을 띄우지 않는다.
                  날씨 위치 = 지금 3D 영역 가운데, 없으면 지도 가운데.
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private Map3DEnvironmentViewModel? _map3DEnv;
    private bool _map3DEnvResolved;

    /// <summary>3D 위 「환경」 판 · 설정 화면 「3D 환경」 묶음의 VM(없으면 null).</summary>
    public Map3DEnvironmentViewModel? Map3DEnvironment
    {
        get
        {
            if (_map3DEnvResolved) return _map3DEnv;
            _map3DEnvResolved = true;
            try
            {
                _map3DEnv = IoC.GetAll<Map3DEnvironmentViewModel>().FirstOrDefault();
                _map3DEnv?.Attach(json => _map3DHost?.PostJson(json), Map3DEnvironmentLocation);
            }
            catch (Exception ex) { _log?.Warning($"[Map3DEnv] 환경 VM 해석 실패 — 3D 환경 판 없이: {ex.Message}"); }
            return _map3DEnv;
        }
    }

    /// <summary>3D → GIS 메시지 중 환경 몫(CreateMap3DHost 에서 구독).</summary>
    private void OnMap3DEnvironmentMessage(Map3DMessage msg)
    {
        try
        {
            if (msg.Type == "rendered") Map3DEnvironment?.OnSceneRendered();
            else if (msg.Type == "env.state" && msg.EnvState is { } state) Map3DEnvironment?.OnEngineState(state);
        }
        catch (Exception ex) { _log?.Error($"[Map3DEnv] 환경 메시지 처리 실패(격리): {ex.Message}"); }
    }

    private (double Lat, double Lon)? Map3DEnvironmentLocation()
    {
        if (_map3DScenes.Find(sc => sc.Id == _map3DJob) is { } scene)
            return scene.Rect is { } r ? (r.Lat, r.Lon) : Map3DGuide.AreaCenter(scene.Bbox);
        return MainMap is { } map ? (map.Position.Lat, map.Position.Lng) : null;
    }
}
