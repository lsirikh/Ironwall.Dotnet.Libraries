using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services.Onvif;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;

/****************************************************************************
   Purpose      : 카메라 패널 [ONVIF 검색] — 검색(WS-Discovery) → 조회(ONVIF) → 추가(Draft)
   Created On   : 2026-10-04
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>검색 목록 한 줄의 상태.</summary>
public enum OnvifDiscoveredState
{
    Found,
    LookingUp,
    LookedUp,
    Failed,
    Added,
    AlreadyRegistered,
}

/// <summary>
/// 검색 → 조회 → 추가 흐름. 서버에 직접 쓰지 않는다 — [추가] 는 카메라 목록에 Draft 행을 넣고,
/// 등록은 패널의 [저장] 이 기존 검증(포트 · 종류 · 권한)과 함께 한다.
/// </summary>
public sealed class OnvifDiscoveryViewModel : PropertyChangedBase
{
    /// <param name="isRegistered">같은 IP · 포트 카메라가 목록(저장분 + Draft)에 있는가.</param>
    /// <param name="addDraft">번호를 받아 모델을 만드는 함수를 넘기면 Draft 로 넣는다. 못 넣으면 false(권한 · 저장 중).</param>
    public OnvifDiscoveryViewModel(
        IWsDiscoveryClient discovery,
        IOnvifCameraLookup lookup,
        Func<string, int, bool> isRegistered,
        Func<Func<int, CameraDeviceModel>, bool> addDraft,
        ILogService? log = null)
    {
        _discovery = discovery;
        _lookup = lookup;
        _isRegistered = isRegistered;
        _addDraft = addDraft;
        _log = log;
    }

    #region - Binding Methods -
    /// <summary>[ONVIF 검색] 도구 버튼 — 펼치면 바로 한 번 검색한다.</summary>
    public async void OnClickToggleButton()
    {
        IsOpen = !IsOpen;
        if (!IsOpen) { CancelWork(); return; }
        if (Items.Count == 0) await SearchAsync();
    }

    public async void OnClickSearchButton() => await SearchAsync();

    /// <summary>찾은 카메라 전부를 지금 계정으로 조회(이미 추가 · 등록된 줄은 건너뜀).</summary>
    public async void OnClickLookupAllButton() => await LookupAllAsync();

    public void OnClickCloseButton()
    {
        IsOpen = false;
        CancelWork();
    }
    #endregion

    #region - Processes -
    public async Task SearchAsync()
    {
        if (IsSearching) return;
        var ct = NewWork();
        IsSearching = true;
        StatusText = "검색 중…";
        try
        {
            var found = await _discovery.ProbeAsync(ProbeOptionsFactory(), ct);
            Items.Clear();
            foreach (var d in found.Where(d => !string.IsNullOrEmpty(d.Host)))
            {
                var row = new OnvifDiscoveredCameraViewModel(this, d);
                if (_isRegistered(row.Host, row.Port)) row.State = OnvifDiscoveredState.AlreadyRegistered;
                Items.Add(row);
            }
            StatusText = Items.Count == 0
                ? "찾은 ONVIF 카메라가 없습니다."
                : $"{Items.Count}대 찾음 — 계정을 넣고 [조회] 하세요.";
        }
        catch (OperationCanceledException) { StatusText = "검색을 멈췄습니다."; }
        catch (Exception ex)
        {
            _log?.Error($"[ONVIF 검색] {ex.Message}");
            StatusText = "검색하지 못했습니다: " + ex.Message;
        }
        finally { IsSearching = false; }
    }

    public async Task LookupAllAsync()
    {
        var rows = Items.Where(r => r.CanLookup).ToList();
        if (rows.Count == 0) return;
        // 카메라마다 따로 기다리면 느린 한 대가 전체를 잡는다 — 동시에 묻는다(목록은 수십 대 이하).
        await Task.WhenAll(rows.Select(LookupAsync));
        var ok = rows.Count(r => r.State == OnvifDiscoveredState.LookedUp);
        StatusText = $"조회 {ok}/{rows.Count}대 성공" + (ok < rows.Count ? " — 실패한 줄은 계정을 확인하세요." : string.Empty);
    }

    /// <summary>한 줄 조회. 지금 입력된 계정을 줄에 기억해 [추가] 때 같은 계정으로 저장한다.</summary>
    public async Task LookupAsync(OnvifDiscoveredCameraViewModel row)
    {
        if (!row.CanLookup) return;
        var ct = _work?.Token ?? CancellationToken.None;
        var user = UserName;
        var password = Password;
        row.State = OnvifDiscoveredState.LookingUp;
        row.Error = null;
        try
        {
            var r = await _lookup.LookupAsync(row.Host, row.Port, user, password, ct);
            row.Lookup = r;
            row.LookupUserName = user;
            row.LookupPassword = password;
            row.Error = r.Success ? null : r.Error;
            row.State = r.Success ? OnvifDiscoveredState.LookedUp : OnvifDiscoveredState.Failed;
        }
        catch (OperationCanceledException) { row.State = OnvifDiscoveredState.Found; }
        catch (Exception ex)
        {
            row.Error = ex.Message;
            row.State = OnvifDiscoveredState.Failed;
        }
    }

    /// <summary>조회된 줄을 카메라 목록에 Draft 로 넣는다(저장은 패널 [저장]).</summary>
    public bool Add(OnvifDiscoveredCameraViewModel row)
    {
        if (!row.CanAdd || row.Lookup is not { Success: true } lookup) return false;
        if (_isRegistered(row.Host, row.Port))
        {
            row.State = OnvifDiscoveredState.AlreadyRegistered;
            return false;
        }
        var added = _addDraft(n => OnvifCameraDraftFactory.Create(n, row.Host, row.Port, row.LookupUserName, row.LookupPassword, lookup, row.DiscoveredName));
        if (!added)
        {
            StatusText = "지금은 추가할 수 없습니다(권한 또는 저장 중).";
            return false;
        }
        row.State = OnvifDiscoveredState.Added;
        StatusText = $"{row.Address} 를 목록에 추가했습니다 — [저장] 하면 등록됩니다.";
        return true;
    }

    private CancellationToken NewWork()
    {
        CancelWork();
        _work = new CancellationTokenSource();
        return _work.Token;
    }

    private void CancelWork()
    {
        var w = _work;
        _work = null;
        if (w is null) return;
        try { w.Cancel(); } catch (ObjectDisposedException) { }
        w.Dispose();
    }
    #endregion

    #region - Properties -
    public ObservableCollection<OnvifDiscoveredCameraViewModel> Items { get; } = new();

    public bool IsOpen
    {
        get => _isOpen;
        set { _isOpen = value; NotifyOfPropertyChange(); }
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set { _isSearching = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanSearch)); }
    }

    public bool CanSearch => !IsSearching;

    /// <summary>조회에 쓸 카메라 계정. 기본은 비움 — 계정은 코드 · 설정에 두지 않는다.</summary>
    public string? UserName
    {
        get => _userName;
        set { _userName = value; NotifyOfPropertyChange(); }
    }

    public string? Password
    {
        get => _password;
        set { _password = value; NotifyOfPropertyChange(); }
    }

    public string? StatusText
    {
        get => _statusText;
        set { _statusText = value; NotifyOfPropertyChange(); }
    }

    /// <summary>검색 범위(시험 · 도구가 유니캐스트 대상을 줄 때 바꾼다).</summary>
    public Func<WsDiscoveryProbeOptions> ProbeOptionsFactory { get; set; } = () => new WsDiscoveryProbeOptions();
    #endregion

    #region - Attributes -
    private readonly IWsDiscoveryClient _discovery;
    private readonly IOnvifCameraLookup _lookup;
    private readonly Func<string, int, bool> _isRegistered;
    private readonly Func<Func<int, CameraDeviceModel>, bool> _addDraft;
    private readonly ILogService? _log;
    private CancellationTokenSource? _work;
    private bool _isOpen;
    private bool _isSearching;
    private string? _userName;
    private string? _password;
    private string? _statusText;
    #endregion
}

/// <summary>검색 목록 한 줄.</summary>
public sealed class OnvifDiscoveredCameraViewModel : PropertyChangedBase
{
    public OnvifDiscoveredCameraViewModel(OnvifDiscoveryViewModel owner, DiscoveredOnvifDevice device)
    {
        _owner = owner;
        Device = device;
    }

    public DiscoveredOnvifDevice Device { get; }
    public string Host => Device.Host ?? string.Empty;
    public int Port => Device.Port;
    public string Address => $"{Host}:{Port}";
    public string? DiscoveredName => Device.Name;

    /// <summary>조회 전엔 검색 힌트(scope), 조회 뒤엔 실제 값.</summary>
    public string NameDisplay => Lookup is { Success: true } r
        ? OnvifCameraDraftFactory.DisplayName(r, DiscoveredName, Host, Port)
        : DiscoveredName ?? "-";

    public string ModelDisplay => Lookup is { Success: true } r
        ? string.Join(' ', new[] { r.Manufacturer, r.Model }.Where(s => !string.IsNullOrWhiteSpace(s)))
        : Device.Hardware ?? "-";

    public string TypeDisplay => Lookup is { Success: true } r ? (r.IsPtz ? "PTZ" : "고정") : (Device.PtzHint ? "PTZ?" : "-");

    public string StreamDisplay => Lookup is { Success: true } r
        ? (r.RtspMain is null ? "영상 주소 없음" : r.RtspSub is null ? "메인" : "메인 · 서브")
        : "-";

    public OnvifDiscoveredState State
    {
        get => _state;
        set
        {
            _state = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(StateText));
            NotifyOfPropertyChange(nameof(CanLookup));
            NotifyOfPropertyChange(nameof(CanAdd));
            NotifyOfPropertyChange(nameof(NameDisplay));
            NotifyOfPropertyChange(nameof(ModelDisplay));
            NotifyOfPropertyChange(nameof(TypeDisplay));
            NotifyOfPropertyChange(nameof(StreamDisplay));
        }
    }

    public string StateText => State switch
    {
        OnvifDiscoveredState.Found => "발견",
        OnvifDiscoveredState.LookingUp => "조회 중…",
        OnvifDiscoveredState.LookedUp => "조회됨",
        OnvifDiscoveredState.Failed => "실패" + (string.IsNullOrEmpty(Error) ? string.Empty : $" — {Error}"),
        OnvifDiscoveredState.Added => "추가됨(미저장)",
        OnvifDiscoveredState.AlreadyRegistered => "이미 등록됨",
        _ => string.Empty,
    };

    public bool CanLookup => State is OnvifDiscoveredState.Found or OnvifDiscoveredState.LookedUp or OnvifDiscoveredState.Failed;
    public bool CanAdd => State == OnvifDiscoveredState.LookedUp;

    public OnvifLookupResult? Lookup { get; set; }
    public string? Error { get; set; }
    internal string? LookupUserName { get; set; }
    internal string? LookupPassword { get; set; }

    public async void OnClickLookupButton() => await _owner.LookupAsync(this);
    public void OnClickAddButton() => _owner.Add(this);

    private readonly OnvifDiscoveryViewModel _owner;
    private OnvifDiscoveredState _state = OnvifDiscoveredState.Found;
}
