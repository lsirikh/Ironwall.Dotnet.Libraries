using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 3D 엔진(map-3d) 다시 연결 — 감시 프로그램이 엔진을 다시 띄우면(몇 초 ~ 1~2분) GIS 가 스스로 되살아난다.
   Note         : ★ 끊김(down): map-3d 호출이 Map3DServerUnavailableException · 3D 화면 탐색 실패(CannotConnect 등) ·
                    3D 가 보이는 동안 10 초마다 묻는 엔진 확인이 2번 잇달아 실패 → NoteMap3DEngineDown. 그때부터 5 초마다 IsHealthyAsync.
                  ★ 다시 연결(up): 배너("3D 서버(map-3d)가 꺼져 있습니다")를 내리고 영역 목록을 다시 받고, 3D 를 보던(또는 들어가던) 중이었으면
                    지금 자리에서 3D 를 다시 연다(기존 진입 경로 EnterMap3DDirect — 카메라 처리는 그대로). 확인은 거기서 멈춘다.
                  ★ 3D 설정이 꺼져 있으면(Map3D.IsEnabled false) 아무것도 묻지 않는다. 화면 비활성화 · 닫기에서 모두 멈춘다(CleanupMap3D).
                  ★ 시간은 IMap3DTicker 이음매(제품 = DispatcherTimer) — 시험은 가짜를 꽂아 틱을 직접 부른다.
   Created On   : 2026-10-20 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    /// <summary>끊긴 동안 엔진을 다시 묻는 간격.</summary>
    internal static readonly TimeSpan Map3DReconnectInterval = TimeSpan.FromSeconds(5);
    /// <summary>3D 가 보이는 동안 엔진을 묻는 간격.</summary>
    internal static readonly TimeSpan Map3DWatchInterval = TimeSpan.FromSeconds(10);
    /// <summary>3D 가 보이는 동안 이만큼 잇달아 실패하면 끊긴 것으로 본다.</summary>
    private const int Map3DWatchFailLimit = 2;

    private IMap3DTicker? _map3DReconnectTicker, _map3DWatchTicker;
    private IMap3DTicker Map3DReconnectTicker => _map3DReconnectTicker ??= new DispatcherMap3DTicker();
    private IMap3DTicker Map3DWatchTicker => _map3DWatchTicker ??= new DispatcherMap3DTicker();
    /// <summary>엔진이 끊긴 상태 — 5 초마다 다시 묻는 중.</summary>
    private bool _map3DEngineDown;
    /// <summary>끊길 때 3D 를 보던(또는 들어가던) 중이었다 — 다시 연결되면 3D 를 다시 연다.</summary>
    private bool _map3DReloadOnReconnect;
    private int _map3DWatchFails;
    private bool _map3DHealthBusy;

    /// <summary>
    /// 엔진에 닿지 않았다 — 끊김 상태로 들어가 5 초마다 다시 묻는다(이미 끊김이면 그대로). 지금 3D 중이거나 wasIn3D 면 다시 연결 때 3D 를 다시 연다.
    /// 3D 설정이 꺼져 있거나 창구가 없으면 하지 않는다. showBanner = 배너를 여기서 띄운다(호출자가 이미 띄웠으면 false).
    /// </summary>
    private void NoteMap3DEngineDown(string cause, bool wasIn3D = false, bool showBanner = false)
    {
        if (!_map3DSettings.IsEnabled || _map3DClient == null) return;
        if (wasIn3D || _map3DState != Map3DState.Off) _map3DReloadOnReconnect = true;
        if (showBanner) SetAimStatus(Map3DUnavailableText, autoHide: true);
        if (_map3DEngineDown) return;
        _map3DEngineDown = true;
        _map3DWatchTicker?.Stop();
        _map3DWatchFails = 0;
        _log?.Warning($"[Map3D] 엔진 끊김({cause}) — 5초마다 다시 확인");
        Map3DReconnectTicker.Start(Map3DReconnectInterval, OnMap3DReconnectTickAsync);
    }

    /// <summary>끊긴 동안 5 초마다 — 엔진이 살아 있으면 다시 연결 처리.</summary>
    private async Task OnMap3DReconnectTickAsync()
    {
        if (_map3DHealthBusy) return;
        if (!_map3DEngineDown || !_map3DSettings.IsEnabled || _map3DClient is not { } client) { StopMap3DEngineWatchers(); return; }
        _map3DHealthBusy = true;
        bool healthy;
        try { healthy = await client.IsHealthyAsync(); }
        finally { _map3DHealthBusy = false; }
        if (!healthy || !_map3DEngineDown || !ReferenceEquals(client, _map3DClient)) return;
        try { await OnMap3DEngineBackAsync(); }
        catch (Exception ex) { _log?.Error($"[Map3D] 엔진 다시 연결 처리 실패(격리): {ex.Message}"); }
    }

    /// <summary>엔진이 돌아왔다 — 확인을 멈추고 배너를 내리고 영역 목록을 다시 받은 뒤, 3D 를 보던 중이었으면 다시 연다.</summary>
    private async Task OnMap3DEngineBackAsync()
    {
        _map3DEngineDown = false;
        _map3DReconnectTicker?.Stop();
        bool reload = _map3DReloadOnReconnect;
        _map3DReloadOnReconnect = false;
        _log?.Info($"[Map3D] 엔진 다시 연결됨 — 영역 목록 새로 고침{(reload ? " · 3D 다시 열기" : "")}");
        if (AimStatusMessage == Map3DUnavailableText) IsAimStatusVisible = false;
        await RefreshMap3DAreasAsync();
        if (_map3DEngineDown) { _map3DReloadOnReconnect |= reload; return; }   // 목록을 받다가 다시 끊겼다 — 다음 연결 때 다시 연다
        ReloadMap3DAfterReconnect(reload);
        SyncMap3DEngineWatch();
    }

    /// <summary>
    /// 다시 연결 뒤 3D 화면 — 보던(들어가던) 중이었으면 옛 페이지를 내려놓고 지금 자리의 영역으로 다시 들어간다(EnterMap3DDirect).
    /// 3D 가 보이는 채였으면 3D 마지막 카메라로 2D 에 돌아왔다가 다시 들어간다 — 「나가기」 때 돌아갈 자리는 처음 그대로.
    /// 2D 였으면 미리 불러 둔 옛 페이지만 내려놓는다(다음 진입이 새로 연다).
    /// </summary>
    private void ReloadMap3DAfterReconnect(bool wasIn3D)
    {
        if (MainMap == null) return;
        if (!wasIn3D)
        {
            if (_map3DState == Map3DState.Off && _map3DHost?.Job != null) _map3DHost.Unload();
            return;
        }
        var scene = _map3DScenes.Find(sc => sc.Id == _map3DJob && Map3DMath.SceneContains(sc, MainMap.Position.Lng, MainMap.Position.Lat))
                    ?? FindMap3DSceneAt(MainMap.Position);
        if (_map3DState == Map3DState.Shown)
        {
            var back = _map3DReturn;
            Leave3D(_map3DLastCamera, "reconnect");
            _map3DReturn = back;
        }
        else if (_map3DState == Map3DState.Pending) CancelMap3DPending();
        Map3DHost.Unload();   // 엔진이 다시 떴다 — 옛 페이지를 다시 쓰지 않고 새로 연다
        if (scene == null) { _map3DReturn = null; return; }
        _map3DEntrySuppressed = false;
        EnterMap3DDirect(scene, "reconnect");
    }

    /// <summary>3D 가 보이는 동안만(3D 설정 켜짐 · 끊김 아님) 10 초마다 엔진 확인을 돌린다 — 그 밖에는 멈춘다.</summary>
    private void SyncMap3DEngineWatch()
    {
        bool want = _map3DState == Map3DState.Shown && _map3DSettings.IsEnabled && !_map3DEngineDown && _map3DClient != null;
        if (!want) { _map3DWatchTicker?.Stop(); _map3DWatchFails = 0; return; }
        if (Map3DWatchTicker.IsRunning) return;
        _map3DWatchFails = 0;
        Map3DWatchTicker.Start(Map3DWatchInterval, OnMap3DWatchTickAsync);
    }

    /// <summary>3D 가 보이는 동안 10 초마다 — 2번 잇달아 실패하면 끊김(배너 + 5 초마다 다시 확인).</summary>
    private async Task OnMap3DWatchTickAsync()
    {
        if (_map3DHealthBusy) return;
        if (_map3DState != Map3DState.Shown || !_map3DSettings.IsEnabled || _map3DEngineDown || _map3DClient is not { } client)
        {
            SyncMap3DEngineWatch();
            return;
        }
        _map3DHealthBusy = true;
        bool healthy;
        try { healthy = await client.IsHealthyAsync(); }
        finally { _map3DHealthBusy = false; }
        if (healthy) { _map3DWatchFails = 0; return; }
        if (++_map3DWatchFails < Map3DWatchFailLimit || _map3DState != Map3DState.Shown) return;
        NoteMap3DEngineDown("watch", wasIn3D: true, showBanner: true);
    }

    /// <summary>3D 화면을 열지 못했는데 이유가 분명히 "닿지 않음"이 아니다 — 엔진을 한 번 물어 꺼졌을 때만 끊김으로(다시 연결 때 3D 를 다시 연다).</summary>
    private async Task CheckMap3DEngineAfterLoadFailureAsync()
    {
        if (!_map3DSettings.IsEnabled || _map3DEngineDown || _map3DClient is not { } client) return;
        if (!await client.IsHealthyAsync()) NoteMap3DEngineDown("load-failed", wasIn3D: true);
    }

    /// <summary>다시 연결 감시 · 3D 중 엔진 확인을 모두 멈추고 끊김 상태를 지운다(3D 끄기 · 화면 비활성화).</summary>
    private void StopMap3DEngineWatchers()
    {
        _map3DReconnectTicker?.Stop();
        _map3DWatchTicker?.Stop();
        _map3DEngineDown = false;
        _map3DReloadOnReconnect = false;
        _map3DWatchFails = 0;
    }
}
