using Caliburn.Micro;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;

/// <summary>
/// 센서 모델 창을 열어 달라는 요청(sensor-mount-preset W1 여는 곳) — 호스트 설정 창 레일 "센서 모델" · 그 밖의 입구가 보낸다.
/// 장비 모듈(<see cref="SensorModelsSettingsBlockViewModel"/>)이 받아 창을 연다(호스트는 메시지만 보내면 된다).
/// </summary>
public sealed record OpenSensorModelsMessage;

/// <summary>
/// 설정 창 레일 "센서 모델" 블록(sensor-mount-preset W1 여는 곳) — 저장소 표지 · 요약 · [센서 모델 열기].
/// 설정은 창 안에서 바로 저장되므로(GIS 로컬 DB) 설정 창 저장 막대와는 잇지 않는다(초안 없음).
/// <see cref="OpenSensorModelsMessage"/> 를 받으면(<see cref="AttachMessages"/> 뒤) 같은 창을 연다.
/// </summary>
public sealed class SensorModelsSettingsBlockViewModel : PropertyChangedBase, IHandle<OpenSensorModelsMessage>
{
    private readonly Lazy<ISensorPresetLauncher>? _launcher;
    private readonly Lazy<ISensorPresetRepository>? _repository;
    private string _summaryText = string.Empty;
    private string _statusText = string.Empty;
    private bool _isOpening;

    /// <param name="launcher">창을 여는 쪽(장비 모듈의 결선 입구) — 없으면 블록이 "열 수 없음"을 말한다.</param>
    /// <param name="repository">표지 · 요약을 읽을 저장소 — 없으면 표지만 "저장소 없음".</param>
    public SensorModelsSettingsBlockViewModel(Lazy<ISensorPresetLauncher>? launcher = null, Lazy<ISensorPresetRepository>? repository = null)
    {
        _launcher = launcher;
        _repository = repository;
    }

    #region - Facts -
    /// <summary>블록 제목(레일 항목 이름).</summary>
    public string Title => "센서 모델";

    /// <summary>"저장소: GIS DB" · "저장소: GOP API(읽기 전용)" — 센서 모델 창(W1)과 같은 글자.</summary>
    public string SourceText => Repository()?.Source switch
    {
        null => "저장소: 없음(지도 DB 모듈이 없습니다)",
        SensorPresetSource.GopApi => "저장소: GOP API(읽기 전용)",
        SensorPresetSource.Memory => "저장소: 이 창만(DB 없음)",
        _ => "저장소: GIS DB",
    };

    /// <summary>편집할 수 있나(GOP API 면 잠금 · 창 안 편집 단추도 같이 잠긴다).</summary>
    public bool CanEdit => Repository()?.CanWrite == true;

    /// <summary>"모델 7 · 위치 프리셋 12 · 속성 프리셋 9" — <see cref="LoadAsync"/> 뒤.</summary>
    public string SummaryText
    {
        get => _summaryText;
        private set { _summaryText = value; NotifyOfPropertyChange(); }
    }

    /// <summary>상태 한 줄(열지 못함 · 읽지 못함).</summary>
    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; NotifyOfPropertyChange(); }
    }

    /// <summary>[센서 모델 열기] 를 누를 수 있나.</summary>
    public bool CanOpen => !_isOpening && Launcher() is { IsSensorModelsAvailable: true };
    #endregion

    #region - Actions -
    /// <summary>요약을 읽는다(설정 창이 블록을 보일 때). 실패하면 요약을 비우고 상태에 까닭.</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (Repository() is not { } repo) { SummaryText = string.Empty; RaiseFacts(); return; }
        try
        {
            var models = await repo.GetModelsAsync(ct).ConfigureAwait(true);
            var mounts = 0;
            var attrs = 0;
            foreach (var m in models)
            {
                mounts += (await repo.GetMountPresetsAsync(m.Id, ct).ConfigureAwait(true)).Count;
                attrs += (await repo.GetAttributePresetsAsync(m.Id, ct).ConfigureAwait(true)).Count;
            }
            SummaryText = $"모델 {models.Count(m => !m.IsHidden)} · 위치 프리셋 {mounts} · 속성 프리셋 {attrs}";
            StatusText = string.Empty;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            SummaryText = string.Empty;
            StatusText = $"프리셋을 읽지 못했습니다 — {ex.Message}";
        }
        RaiseFacts();
    }

    /// <summary>[센서 모델 열기] — 창이 닫히면 요약을 다시 읽는다.</summary>
    public async Task OpenAsync()
    {
        if (!CanOpen || Launcher() is not { } launcher) { StatusText = "센서 모델 창을 열 수 없습니다 — 지도 DB 모듈 · 장비 모듈을 확인하세요."; return; }
        _isOpening = true;
        NotifyOfPropertyChange(nameof(CanOpen));
        try
        {
            if (!await launcher.OpenSensorModelsAsync().ConfigureAwait(true)) StatusText = "센서 모델 창을 열지 못했습니다.";
        }
        finally
        {
            _isOpening = false;
            NotifyOfPropertyChange(nameof(CanOpen));
        }
        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary><see cref="OpenSensorModelsMessage"/> 를 받기 시작한다(장비 모듈이 한 번 — 호스트는 메시지만 보낸다).</summary>
    public void AttachMessages(IEventAggregator eventAggregator) => eventAggregator?.SubscribeOnUIThread(this);

    /// <inheritdoc/>
    public Task HandleAsync(OpenSensorModelsMessage message, CancellationToken cancellationToken) => OpenAsync();
    #endregion

    private ISensorPresetLauncher? Launcher()
    {
        try { return _launcher?.Value; }
        catch (Exception) { return null; }
    }

    private ISensorPresetRepository? Repository()
    {
        try { return _repository?.Value; }
        catch (Exception) { return null; }
    }

    private void RaiseFacts()
    {
        NotifyOfPropertyChange(nameof(SourceText));
        NotifyOfPropertyChange(nameof(CanEdit));
        NotifyOfPropertyChange(nameof(CanOpen));
    }
}
