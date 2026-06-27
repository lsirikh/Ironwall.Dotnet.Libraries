using System.Windows.Input;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 추적 설정 패널 VM — 즉시 적용 + 저장 영속 (P3-04)
   Created By   : GHLee
   Created On   : 2026-06-26
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 추적 표시/트레일/수명/속도·재생 설정을 조정하는 패널 VM.
/// <para>주입된 <see cref="ITrackingSetupModel"/> <b>싱글톤을 직접 변경</b> → 오버레이가 같은 인스턴스를 참조하므로 즉시 반영(live).
/// <b>[저장]</b> 시 appsettings(AppSettings.Tracking) 영속, <b>[기본값]</b> 복원.</para>
/// <para>일부 값은 "다음 수신/렌더부터" 반영(TTL=새 메시지, TrailMax=다음 추가). Status에 안내.</para>
/// </summary>
public sealed class TrackingSetupViewModel : PropertyChangedBase
{
    private readonly ITrackingSetupModel _model;
    private readonly ILogService? _log;

    /// <summary>패널 닫기 요청(MapViewModel이 숨김).</summary>
    public event System.Action? CloseRequested;

    public TrackingSetupViewModel(ITrackingSetupModel model, ILogService? log = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _log = log;

        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        ResetCommand = new RelayCommand(_ => ResetToDefault());
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());
    }

    #region - Bindable (싱글톤 직접 변경 = 즉시 적용) -
    public bool IsTrackingEnabled { get => _model.IsTrackingEnabled; set { _model.IsTrackingEnabled = value; NotifyOfPropertyChange(); } }
    public double MinVisibleZoom { get => _model.MinVisibleZoom; set { _model.MinVisibleZoom = value; NotifyOfPropertyChange(); } }
    public int TrailMaxPoints { get => _model.TrailMaxPoints; set { _model.TrailMaxPoints = value; NotifyOfPropertyChange(); } }
    public int GapThresholdSec { get => _model.GapThresholdSec; set { _model.GapThresholdSec = value; NotifyOfPropertyChange(); } }
    public int DefaultTtlSec { get => _model.DefaultTtlSec; set { _model.DefaultTtlSec = value; NotifyOfPropertyChange(); } }
    public int LostTtlSec { get => _model.LostTtlSec; set { _model.LostTtlSec = value; NotifyOfPropertyChange(); } }
    public double MaxSpeedMs { get => _model.MaxSpeedMs; set { _model.MaxSpeedMs = value; NotifyOfPropertyChange(); } }
    public int MaxPlaybackHours { get => _model.MaxPlaybackHours; set { _model.MaxPlaybackHours = value; NotifyOfPropertyChange(); } }

    /// <summary>Playback 데이터 소스(로컬 DB / 서버 API). 변경 시 다음 Load부터 반영(라이브, 무재시작).</summary>
    public EnumTrackDataSource DataSource { get => _model.DataSource; set { _model.DataSource = value; NotifyOfPropertyChange(); } }
    /// <summary>콤보 ItemsSource — enum 값 목록(Local/Api).</summary>
    public IReadOnlyList<EnumTrackDataSource> DataSourceOptions { get; } = Enum.GetValues<EnumTrackDataSource>().ToList();

    private string _status = "조정 즉시 반영 · [저장]으로 영속";
    public string Status { get => _status; set { _status = value; NotifyOfPropertyChange(); } }
    #endregion

    #region - Commands -
    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand CloseCommand { get; }
    #endregion

    private async Task SaveAsync()
    {
        try
        {
            await MapSettingsHelper.SaveTrackingSettingsAsync(_model, _log).ConfigureAwait(true);
            Status = "저장됨 — 재시작 후에도 유지";
            _log?.Info("[TrackingSetup] 설정 저장(appsettings.Tracking)");
        }
        catch (Exception ex)
        {
            Status = "저장 실패 — 로그 확인";
            _log?.Error($"[TrackingSetup] 저장 실패: {ex.Message}");
        }
    }

    private void ResetToDefault()
    {
        var d = new TrackingSetupModel();   // 기본값 원천
        _model.IsTrackingEnabled = d.IsTrackingEnabled;
        _model.MinVisibleZoom = d.MinVisibleZoom;
        _model.TrailMaxPoints = d.TrailMaxPoints;
        _model.GapThresholdSec = d.GapThresholdSec;
        _model.DefaultTtlSec = d.DefaultTtlSec;
        _model.LostTtlSec = d.LostTtlSec;
        _model.MaxSpeedMs = d.MaxSpeedMs;
        _model.MaxPlaybackHours = d.MaxPlaybackHours;
        _model.DataSource = d.DataSource;
        Refresh();   // 모든 바인딩 갱신
        Status = "기본값 복원됨 — [저장] 필요";
    }
}
