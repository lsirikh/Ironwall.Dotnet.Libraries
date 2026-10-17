using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 이벤트 시 지도 이동 설정 블록 VM — event-map-flyto PRD §3·§4.
                  주입된 MapEventFlyToModel 싱글톤을 직접 바꾼다(지도가 같은 인스턴스를 읽어 즉시 반영), [저장] 시 appsettings 영속.
   Note         : 슬라이더 · 숫자 칸의 값은 double 로 오지만 세터가 정수 정규화(반올림 + 0~5000 자르기)를 거쳐 int 로만 남긴다
                  — 999.9999 같은 값이 화면 · 저장 파일에 나오지 않게.
   Created On   : 2026-10-13 · Sensorway Co., Ltd.
 ****************************************************************************/
public sealed class MapEventFlyToSetupViewModel : PropertyChangedBase
{
    private readonly MapEventFlyToModel _model;
    private readonly ILogService? _log;

    public MapEventFlyToSetupViewModel(MapEventFlyToModel model, ILogService? log = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _log = log;
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
    }

    /// <summary>이동 시간 상한(ms) — 슬라이더 Maximum.</summary>
    public int MaxDurationMs => MapEventFlyToModel.MaxDurationMs;

    public bool IsDetectionEnabled { get => _model.IsDetectionEnabled; set { _model.IsDetectionEnabled = value; NotifyOfPropertyChange(); } }
    public bool IsFaultEnabled { get => _model.IsFaultEnabled; set { _model.IsFaultEnabled = value; NotifyOfPropertyChange(); } }

    /// <summary>이동 시간(ms) — 값은 언제나 0~5000 정수.</summary>
    public double DurationMs
    {
        get => _model.DurationMs;
        set { _model.DurationMs = MapEventFlyToModel.NormalizeDurationMs(value); NotifyOfPropertyChange(); }
    }

    private int _zoomMinimum = 1;
    /// <summary>지도 최소 줌 — 최대 줌 슬라이더 Minimum. 지도가 뜰 때 <see cref="SetZoomRange"/> 로 맞춘다.</summary>
    public int ZoomMinimum { get => _zoomMinimum; private set { _zoomMinimum = value; NotifyOfPropertyChange(); } }

    private int _zoomMaximum = 22;
    /// <summary>지도 최대 줌 — 최대 줌 슬라이더 Maximum.</summary>
    public int ZoomMaximum { get => _zoomMaximum; private set { _zoomMaximum = value; NotifyOfPropertyChange(); } }

    /// <summary>이벤트 시 최대 줌 — 지도 줌 범위 안 정수.</summary>
    public double MaxZoom
    {
        get => _model.MaxZoom;
        set { _model.MaxZoom = MapEventFlyToModel.NormalizeMaxZoom(value, ZoomMinimum, ZoomMaximum); NotifyOfPropertyChange(); }
    }

    /// <summary>지도 줌 범위를 알려 준다 — 모델의 최대 줌도 그 범위로 자른다.</summary>
    public void SetZoomRange(int minZoom, int maxZoom)
    {
        ZoomMinimum = minZoom;
        ZoomMaximum = Math.Max(minZoom, maxZoom);
        _model.Normalize(ZoomMinimum, ZoomMaximum);
        NotifyOfPropertyChange(nameof(MaxZoom));
        NotifyOfPropertyChange(nameof(DurationMs));
    }

    private string _status = string.Empty;
    public string Status { get => _status; set { _status = value; NotifyOfPropertyChange(); } }

    public ICommand SaveCommand { get; }

    private async Task SaveAsync()
    {
        try
        {
            await MapSettingsHelper.SaveMapEventFlyToAsync(_model, _log).ConfigureAwait(true);
            Status = "저장됨";
            _log?.Info($"[EventFlyTo] 설정 저장: {_model}");
        }
        catch (Exception ex)
        {
            Status = "저장 실패 — 로그 확인";
            _log?.Error($"[EventFlyTo] 저장 실패: {ex.Message}");
        }
    }
}
