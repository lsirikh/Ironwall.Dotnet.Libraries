using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 방위각 심볼(나침반 컨트롤) 위치+설정 영속 배선 (GMap_Compass_Control FR-08).
                  MapViewModel partial 분리 — 본체 파일 충돌 최소화(다중세션).
   Note         : 복원 중 저장 억제(_suppressCompassSave) — 회전 영속 자가오염(44bc6fc) 교훈:
                  복원 경로에서 change-핸들러가 반적용 상태를 영속화하지 않게 항상 억제 플래그.
   Created On   : 2026-07-30 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private bool _suppressCompassSave;

    // ── 컨트롤 TwoWay 바인딩 소스(값 자체는 컨트롤/메뉴가 갱신) ──
    private double _mapCompassX = double.NaN;
    /// <summary>나침반 Canvas X. NaN=미저장(컨트롤이 우상단 기본 도킹, D-04).</summary>
    public double MapCompassX { get => _mapCompassX; set { _mapCompassX = value; NotifyOfPropertyChange(nameof(MapCompassX)); } }

    private double _mapCompassY = double.NaN;
    public double MapCompassY { get => _mapCompassY; set { _mapCompassY = value; NotifyOfPropertyChange(nameof(MapCompassY)); } }

    private CompassDialSize _compassDialSize = CompassDialSize.M;
    public CompassDialSize CompassDialSize { get => _compassDialSize; set { _compassDialSize = value; NotifyOfPropertyChange(nameof(CompassDialSize)); } }

    private CompassVariant _compassVariant = CompassVariant.Rose;
    public CompassVariant CompassVariant { get => _compassVariant; set { _compassVariant = value; NotifyOfPropertyChange(nameof(CompassVariant)); } }

    private bool _compassShowReadout = true;
    public bool CompassShowReadout { get => _compassShowReadout; set { _compassShowReadout = value; NotifyOfPropertyChange(nameof(CompassShowReadout)); } }

    private RelayCommand? _saveMapCompassCommand;
    /// <summary>드래그 종료/메뉴 설정 변경 시 컨트롤이 호출 — 현재 바인딩 값을 영속.</summary>
    public RelayCommand SaveMapCompassCommand
        => _saveMapCompassCommand ??= new RelayCommand(_ => _ = SaveMapCompassState());

    /// <summary>부팅 복원 — ConfigureCommonMapSettings에서 호출. 값 없으면 기본(NaN=우상단 M/Rose).</summary>
    private void LoadMapCompassFromSettings()
    {
        var saved = _setupModel?.MapCompass;
        if (saved == null) return;
        _suppressCompassSave = true;
        try
        {
            MapCompassX = saved.X;
            MapCompassY = saved.Y;
            CompassDialSize = CompassMath.ParseSize(saved.Size);       // 관대 파싱(NFR-04)
            CompassVariant = CompassMath.ParseVariant(saved.Variant);
            CompassShowReadout = saved.ShowReadout;
        }
        finally { _suppressCompassSave = false; }
        _log?.Info($"[Compass] 상태 복원: {saved}");
    }

    /// <summary>현재 바인딩 값 → AppSettings.MapCompass 저장. 이벤트가 이산적(드래그 종료/메뉴 클릭)이라
    /// debounce 없이 즉시 저장. 반환 Task는 fire-and-forget(호출부 커맨드).</summary>
    private Task SaveMapCompassState()
    {
        if (_suppressCompassSave) return Task.CompletedTask;
        try
        {
            var model = new MapCompassModel
            {
                X = MapCompassX,
                Y = MapCompassY,
                Size = CompassDialSize.ToString(),
                Variant = CompassVariant.ToString(),
                ShowReadout = CompassShowReadout,
            };
            if (_setupModel != null) _setupModel.MapCompass = model;
            var task = MapSettingsHelper.SaveMapCompassAsync(model, _log);
            _log?.Info($"[Compass] 상태 저장: {model}");
            return task;
        }
        catch (Exception ex)
        {
            _log?.Error($"[Compass] 상태 저장 실패: {ex.Message}");
            return Task.CompletedTask;
        }
    }
}
