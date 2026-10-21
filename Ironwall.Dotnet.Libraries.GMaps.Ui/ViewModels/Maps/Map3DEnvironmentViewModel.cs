using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Weather;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 3D 환경(해 · 날씨 · 차 · 사람) VM — 설정 화면 「3D 환경」 묶음과 3D 위 「환경」 빠른 조절 판이 같이 쓴다(env3d-contract A · C).
                  주입된 Map3DEnvironmentModel 싱글톤(같은 원본)을 바꾸면 ① 바로 3D 에 env.set ② 잠시 뒤 appsettings 에 저장.
                  자동 날씨면 3D 를 처음 연 뒤부터 갱신 간격마다 기상청에서 받는다(IWeatherProvider).
   Note         : 날씨를 못 받으면 마지막 성공 값을 유지하고, 한 번도 못 받았으면 수동 값을 보낸다 — 실패는 기록하고 상태 글로만(GIS 는 멈추지 않는다).
                  3D 로 보내기 · 지도 가운데는 MapViewModel 이 Attach 로 넘긴다(MapViewModel.Map3DEnvironment). 보내기는 붙인 스레드(UI)에서.
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
 ****************************************************************************/
public sealed class Map3DEnvironmentViewModel : PropertyChangedBase
{
    private readonly Map3DEnvironmentModel _m;
    private readonly IWeatherProvider? _provider;
    private readonly ILogService? _log;
    private readonly Func<Map3DEnvironmentModel, Task> _save;
    private readonly Func<DateTimeOffset> _now;
    private System.Action<string>? _post;
    private Func<(double Lat, double Lon)?>? _location;
    private SynchronizationContext? _ui;
    private WeatherSnapshot? _fetched;
    private string? _failure;
    private bool _sceneSeen;   // 3D 가 장면을 한 번이라도 그렸다 — 그 전에는 날씨를 받으러 나가지 않는다
    private CancellationTokenSource? _loop, _pendingSave;

    /// <param name="save">저장 경로(없으면 appsettings — 시험은 가짜).</param>
    public Map3DEnvironmentViewModel(Map3DEnvironmentModel model, IWeatherProvider? provider = null, ILogService? log = null,
                                     Func<Map3DEnvironmentModel, Task>? save = null, Func<DateTimeOffset>? now = null)
    {
        _m = model ?? throw new ArgumentNullException(nameof(model));
        _m.Normalize();
        _provider = provider;
        _log = log;
        _save = save ?? (m => MapSettingsHelper.SaveMap3DEnvironmentAsync(m, log));
        _now = now ?? (() => DateTimeOffset.Now);
        TestConnectionCommand = new AsyncRelayCommand(_ => TestConnectionAsync());
        ToggleQuickPanelCommand = new RelayCommand(() => IsQuickPanelOpen = !IsQuickPanelOpen);
    }

    /// <summary>바꾼 뒤 저장까지 기다리는 시간(끌기 · 타자 중 매번 쓰지 않게). 시험은 0.</summary>
    internal TimeSpan SaveDelay { get; set; } = TimeSpan.FromMilliseconds(800);

    public IReadOnlyList<KeyValuePair<string, string>> WeatherModes => Map3DEnvironment.WeatherModes;
    public IReadOnlyList<KeyValuePair<string, string>> TimeModes => Map3DEnvironment.TimeModes;
    public IReadOnlyList<KeyValuePair<string, string>> Skies => Map3DEnvironment.Skies;
    public IReadOnlyList<KeyValuePair<string, string>> Precips => Map3DEnvironment.Precips;
    public IReadOnlyList<int> RefreshChoices { get; } = new[] { 10, 15, 30, 60, 120, 180 };

    // ── 날씨 ──
    public string WeatherMode { get => _m.WeatherMode; set => Change(() => _m.WeatherMode = value, restartWeather: true); }
    public bool IsWeatherAuto => _m.WeatherMode == Map3DEnvironmentModel.ModeAuto;
    public bool IsWeatherManual => !IsWeatherAuto;
    public string KmaBaseUrl { get => _m.KmaBaseUrl; set => Change(() => _m.KmaBaseUrl = value); }
    /// <summary>인증키 — 화면은 가림 입력(PasswordBox)이 코드 뒤에서 넣는다.</summary>
    public string KmaServiceKey { get => _m.KmaServiceKey; set => Change(() => _m.KmaServiceKey = value); }
    public int WeatherRefreshMinutes { get => _m.WeatherRefreshMinutes; set => Change(() => _m.WeatherRefreshMinutes = value, restartWeather: true); }
    public string ManualSky { get => _m.ManualSky; set => Change(() => _m.ManualSky = value); }
    public string ManualPrecip { get => _m.ManualPrecip; set => Change(() => _m.ManualPrecip = value); }
    public double ManualIntensity { get => _m.ManualIntensity; set => Change(() => _m.ManualIntensity = value); }
    public double ManualFog { get => _m.ManualFog; set => Change(() => _m.ManualFog = value); }

    // ── 시간 ──
    public string TimeMode { get => _m.TimeMode; set => Change(() => _m.TimeMode = value); }
    /// <summary>고른 시각을 쓰는가(고른 시각 · 빨리 감기).</summary>
    public bool IsTimePicked => _m.TimeMode != Map3DEnvironmentModel.TimeRealtime;
    public bool IsTimeCycle => _m.TimeMode == Map3DEnvironmentModel.TimeCycle;
    /// <summary>고른 시각 — 하루 안 분(슬라이더 0~1439).</summary>
    public double ManualTimeMinutes
    {
        get => Map3DEnvironmentModel.ParseTime(_m.ManualTime) ?? 720;
        set => Change(() => _m.ManualTime = Map3DEnvironmentModel.FormatTime((int)Math.Round(Math.Clamp(value, 0, 1439))));
    }
    public string ManualTime { get => _m.ManualTime; set => Change(() => _m.ManualTime = value); }
    public int CycleMinutesPerSecond { get => _m.CycleMinutesPerSecond; set => Change(() => _m.CycleMinutesPerSecond = value); }

    // ── 차 · 사람 ──
    public bool CarsEnabled { get => _m.CarsEnabled; set => Change(() => _m.CarsEnabled = value); }
    public double CarDensity { get => _m.CarDensity; set => Change(() => _m.CarDensity = value); }
    public bool PeopleEnabled { get => _m.PeopleEnabled; set => Change(() => _m.PeopleEnabled = value); }
    public double PeopleDensity { get => _m.PeopleDensity; set => Change(() => _m.PeopleDensity = value); }

    // ── 상태 ──
    /// <summary>자동 날씨 상태 한 줄 — 받은 값 · 실패 까닭(수동이면 빈 글).</summary>
    public string WeatherStatus
    {
        get
        {
            if (!IsWeatherAuto) return "";
            string observed = _fetched?.Observed is { } o ? $" ({o.ToLocalTime():HH:mm} 관측)" : "";
            if (_failure == null) return _fetched is { } w ? $"기상청 · {Map3DEnvironment.Summary(w)}{observed}" : "기상청 날씨는 3D 를 열면 받습니다";
            return _fetched != null ? $"{_failure}\n마지막으로 받은 날씨{observed}를 그대로 보입니다" : $"{_failure}\n지금은 직접 고른 날씨로 보입니다";
        }
    }
    public bool HasWeatherStatus => WeatherStatus.Length > 0;

    private string _connectionTestText = "";
    /// <summary>[연결 시험] 결과 — "연결됨 — 맑음 · 12℃" · "인증키가 맞지 않습니다" …</summary>
    public string ConnectionTestText { get => _connectionTestText; private set { _connectionTestText = value; NotifyOfPropertyChange(); } }

    private string _saveStatus = "";
    public string SaveStatus { get => _saveStatus; private set { _saveStatus = value; NotifyOfPropertyChange(); } }

    private string _sunText = "";
    /// <summary>3D 가 알려 준 "해 뜸 06:41 · 해 짐 17:52"(옛 엔진이면 빈 글).</summary>
    public string SunText { get => _sunText; private set { _sunText = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasSunText)); } }
    public bool HasSunText => SunText.Length > 0;

    private bool _isQuickPanelOpen;
    /// <summary>3D 위 「환경」 판이 펼쳐져 있는가.</summary>
    public bool IsQuickPanelOpen { get => _isQuickPanelOpen; set { _isQuickPanelOpen = value; NotifyOfPropertyChange(); } }

    public ICommand TestConnectionCommand { get; }
    public ICommand ToggleQuickPanelCommand { get; }

    // ── 3D 와 잇기 ──

    /// <summary>3D 로 보내기 · 지도 위치(3D 영역 가운데, 없으면 지도 가운데)를 잇는다 — 부른 스레드(UI)에서 보낸다.</summary>
    public void Attach(System.Action<string> post, Func<(double Lat, double Lon)?> location)
    {
        _post = post;
        _location = location;
        _ui = SynchronizationContext.Current;
    }

    /// <summary>3D 가 장면을 그렸다(rendered) — 지금 환경을 보내고, 자동 날씨면 받기 시작한다.</summary>
    public void OnSceneRendered()
    {
        _sceneSeen = true;
        Push();
        if (IsWeatherAuto && _loop == null) RestartWeather();
    }

    /// <summary>3D 가 알려 준 해 · 차 · 사람 상태(env.state).</summary>
    public void OnEngineState(Map3DEnvState state) => SunText = state.SunText;

    /// <summary>지금 보낼 env.set(시험 · 기록용).</summary>
    public string BuildMessage() => Map3DEnvironment.SetMessageJson(_m, _fetched, _now());

    private void Change(System.Action apply, bool restartWeather = false)
    {
        apply();
        _m.Normalize();
        Refresh();   // 파생 칸(모드에 따른 보이기 · 상태 글)까지 한 번에
        Push();
        ScheduleSave();
        if (restartWeather) RestartWeather();
    }

    private void Push()
    {
        if (_post is not { } post) return;
        string json = BuildMessage();
        try
        {
            if (_ui == null || _ui == SynchronizationContext.Current) post(json);
            else _ui.Post(_ => { try { post(json); } catch (Exception ex) { _log?.Error($"[Map3DEnv] env.set 보내기 실패: {ex.Message}"); } }, null);
        }
        catch (Exception ex) { _log?.Error($"[Map3DEnv] env.set 보내기 실패: {ex.Message}"); }
    }

    private void ScheduleSave()
    {
        _pendingSave?.Cancel();
        var cts = _pendingSave = new CancellationTokenSource();
        _ = SaveLaterAsync(cts.Token);
    }

    private async Task SaveLaterAsync(CancellationToken ct)
    {
        try
        {
            if (SaveDelay > TimeSpan.Zero) await Task.Delay(SaveDelay, ct);
            if (ct.IsCancellationRequested) return;
            await _save(_m);
            SaveStatus = "저장됨";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SaveStatus = "저장 실패 — 로그 확인";
            _log?.Error($"[Map3DEnv] 저장 실패: {ex.Message}");
        }
    }

    // ── 자동 날씨 ──

    private void RestartWeather()
    {
        _loop?.Cancel();
        _loop = null;
        if (!IsWeatherAuto || !_sceneSeen || _provider == null) return;   // 3D 를 열기 전에는 바깥으로 부르지 않는다
        var cts = _loop = new CancellationTokenSource();
        _ = WeatherLoopAsync(cts.Token);
    }

    private async Task WeatherLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RefreshWeatherAsync(ct);
            try { await Task.Delay(TimeSpan.FromMinutes(_m.WeatherRefreshMinutes), ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>한 번 받아 반영 — 실패는 마지막 값 유지 · 상태 글(던지지 않는다).</summary>
    internal async Task RefreshWeatherAsync(CancellationToken ct = default)
    {
        var (snapshot, failure) = await FetchOnceAsync(ct);
        if (ct.IsCancellationRequested) return;
        if (snapshot != null) _fetched = snapshot;
        _failure = failure;
        NotifyOfPropertyChange(nameof(WeatherStatus));
        NotifyOfPropertyChange(nameof(HasWeatherStatus));
        Push();
    }

    private async Task<(WeatherSnapshot? Snapshot, string? Failure)> FetchOnceAsync(CancellationToken ct)
    {
        if (_provider == null) return (null, "날씨 제공자가 없습니다");
        if (_location?.Invoke() is not { } p) return (null, "지도 위치를 아직 모릅니다");
        try
        {
            var w = await _provider.FetchAsync(p.Lat, p.Lon, ct);
            _log?.Info($"[Map3DEnv] 날씨 받음({_provider.Source}): {w.Sky}/{w.Precip}/{w.Intensity:0.##} 기온={w.TempC?.ToString() ?? "-"} 관측={w.Observed:yyyy-MM-dd HH:mm}");
            return (w, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return (null, null); }
        catch (WeatherFetchException ex)
        {
            _log?.Warning($"[Map3DEnv] 날씨 못 받음({ex.Kind}): {ex.Message}");
            return (null, ex.UserText);
        }
        catch (Exception ex)
        {
            _log?.Error($"[Map3DEnv] 날씨 받기 오류: {ex}");
            return (null, "날씨를 받지 못했습니다 — 로그 확인");
        }
    }

    /// <summary>[연결 시험] — 한 번 불러 결과를 쉬운 말로. 자동 모드에서 받았으면 그 값을 바로 쓴다.</summary>
    internal async Task TestConnectionAsync()
    {
        ConnectionTestText = "연결 시험 중…";
        var (w, failure) = await FetchOnceAsync(CancellationToken.None);
        ConnectionTestText = w != null ? $"연결됨 — {Map3DEnvironment.Summary(w)}" : failure ?? "";
        if (w != null && IsWeatherAuto) { _fetched = w; _failure = null; Refresh(); Push(); }
    }
}
