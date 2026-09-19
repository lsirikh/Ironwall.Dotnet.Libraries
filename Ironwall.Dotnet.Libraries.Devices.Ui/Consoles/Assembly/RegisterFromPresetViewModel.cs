using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

/// <summary>
/// 프리셋으로 장비 만들기(device-assembly-preset FR-12 ~ FR-14) — 프리셋을 고르고, <b>프리셋이 담지 않는 개체 정보만</b> 넣고,
/// 나갈 본문을 그대로 본 뒤, POST 1건.
/// </summary>
/// <remarks>
/// 왕복 1회가 안전장치다 — 부품마다 PATCH 를 보내는 구조라면 세 번째에서 끊겼을 때 반쯤 조립된 장비가 서버에 남는다.
/// POST 한 건이면 성공 아니면 아무것도 없음이다(AS L351).
/// </remarks>
public sealed class RegisterFromPresetViewModel : Screen
{
    private readonly DevicePresetStore _presets;
    private readonly IComponentCatalog _catalog;
    private readonly PresetRegistrar _registrar;
    private readonly Func<EnumDeviceCategory, IReadOnlyCollection<int>> _usedNumbers;

    private EnumDeviceCategory _category;
    private DevicePreset? _selectedPreset;
    private string _deviceNumber = string.Empty;
    private string _deviceName = string.Empty;
    private string _ipAddress = string.Empty;
    private string _ipPort = string.Empty;
    private string _userName = string.Empty;
    private string _userPassword = string.Empty;
    private IControllerDeviceModel? _controller;
    private string _previewJson = string.Empty;
    private string _message = string.Empty;
    private bool _isBusy;

    /// <param name="fixedPreset">조립기에서 넘어온 이름 없는 프리셋 — 주면 프리셋 목록을 감추고 그것만 쓴다.</param>
    /// <param name="usedNumbers">그 카테고리에서 이미 쓰인 장비번호 — 빈 번호를 제안한다.</param>
    public RegisterFromPresetViewModel(DevicePresetStore presets, IComponentCatalog catalog, PresetRegistrar registrar,
        IEnumerable<IControllerDeviceModel> controllers, Func<EnumDeviceCategory, IReadOnlyCollection<int>> usedNumbers,
        EnumDeviceCategory category, DevicePreset? fixedPreset = null)
    {
        _presets = presets ?? throw new ArgumentNullException(nameof(presets));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));
        _usedNumbers = usedNumbers ?? throw new ArgumentNullException(nameof(usedNumbers));

        FixedPreset = fixedPreset;
        _category = fixedPreset?.Category ?? category;
        Categories = Lists.DeviceRailCounter.RailOrder;
        Presets = new ObservableCollection<DevicePreset>();
        Controllers = (controllers ?? Array.Empty<IControllerDeviceModel>()).Where(c => c.Id > 0).ToList();   // 저장 전 제어기는 고를 수 없다
        Problems = new ObservableCollection<string>();
        DisplayName = "프리셋으로 장비 만들기";

        RefreshPresets();
        SuggestNumber();
        Rebuild();
    }

    /// <summary>
    /// 카탈로그를 먼저 읽는다 — 안 읽힌 채면 "사라진 유형 · 이 카테고리에 못 다는 유형" 검사가 통째로 건너뛰어져,
    /// 그런 프리셋이 그대로 나가 서버에서 422 로 죽는다(장비 창을 열자마자 이 창부터 열면 아직 아무도 카탈로그를 안 읽었다).
    /// </summary>
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await _catalog.EnsureLoadedAsync(cancellationToken);
        Rebuild();
    }

    public DevicePreset? FixedPreset { get; }
    public bool ShowsPresetList => FixedPreset is null;
    public IReadOnlyList<EnumDeviceCategory> Categories { get; }
    public ObservableCollection<DevicePreset> Presets { get; }
    public IReadOnlyList<IControllerDeviceModel> Controllers { get; }
    public ObservableCollection<string> Problems { get; }

    public EnumDeviceCategory Category
    {
        get => _category;
        set
        {
            if (_category == value || FixedPreset is not null) return;
            _category = value;
            NotifyOfPropertyChange();
            RefreshPresets();
            SuggestNumber();
            NotifyFieldVisibility();
            Rebuild();
        }
    }

    public DevicePreset? SelectedPreset
    {
        get => FixedPreset ?? _selectedPreset;
        set { if (FixedPreset is not null || ReferenceEquals(_selectedPreset, value)) return; _selectedPreset = value; NotifyOfPropertyChange(); Rebuild(); }
    }

    public string PresetSummary => SelectedPreset is { } preset
        ? $"{preset.Name} — 부품 {preset.Components.Count} · 재정의 {preset.ComponentOverrides?.Count ?? 0}"
        : "프리셋을 고르세요";

    #region - 개체 정보(프리셋이 담지 않는 것) -
    public string DeviceNumber { get => _deviceNumber; set => Assign(ref _deviceNumber, value, rebuild: true); }
    public string DeviceName { get => _deviceName; set => Assign(ref _deviceName, value, rebuild: true); }
    public string IpAddress { get => _ipAddress; set => Assign(ref _ipAddress, value, rebuild: true); }
    public string IpPort { get => _ipPort; set => Assign(ref _ipPort, value, rebuild: true); }
    public string UserName { get => _userName; set => Assign(ref _userName, value, rebuild: true); }
    public string UserPassword { get => _userPassword; set => Assign(ref _userPassword, value, rebuild: true); }

    public IControllerDeviceModel? Controller
    {
        get => _controller;
        set { if (ReferenceEquals(_controller, value)) return; _controller = value; NotifyOfPropertyChange(); Rebuild(); }
    }

    /// <summary>접속 칸이 있는 카테고리만 보인다(행 뷰모델이 그 속성을 가진 것들).</summary>
    public bool ShowsConnection => _category is EnumDeviceCategory.Controller or EnumDeviceCategory.Camera or EnumDeviceCategory.Lamp;
    public bool ShowsCredentials => _category is EnumDeviceCategory.Camera or EnumDeviceCategory.Lamp;
    public bool ShowsController => _category == EnumDeviceCategory.Sensor;

    private void NotifyFieldVisibility()
    {
        NotifyOfPropertyChange(nameof(ShowsConnection));
        NotifyOfPropertyChange(nameof(ShowsCredentials));
        NotifyOfPropertyChange(nameof(ShowsController));
    }

    private void SuggestNumber()
    {
        var used = _usedNumbers(_category);
        var number = 1;
        while (used.Contains(number)) number++;
        _deviceNumber = number.ToString(CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(_deviceName) || _deviceName.StartsWith("새 ", StringComparison.Ordinal))
            _deviceName = $"새 {DeviceCategoryText.Of(_category)} {number}";
        NotifyOfPropertyChange(nameof(DeviceNumber));
        NotifyOfPropertyChange(nameof(DeviceName));
    }
    #endregion

    #region - 보낼 내용 -
    /// <summary>이대로 나간다 — <c>POST /api/devices/&lt;category&gt;s</c> 1건.</summary>
    public string PreviewJson
    {
        get => _previewJson;
        private set { _previewJson = value; NotifyOfPropertyChange(); }
    }

    public string Message
    {
        get => _message;
        private set { _message = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanRegister)); }
    }

    public bool HasProblems => Problems.Count > 0;
    public bool CanRegister => !IsBusy && _request is not null && Problems.Count == 0;

    /// <summary>등록된 장비의 Id. 등록하지 않고 닫았으면 null.</summary>
    public int? RegisteredDeviceId { get; private set; }

    private PresetRequest? _request;

    private PresetInstanceInfo? ReadInfo(List<string> problems)
    {
        if (!int.TryParse(_deviceNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            problems.Add("장비번호는 숫자여야 한다");
            return null;
        }

        int? port = null;
        if (ShowsConnection && !string.IsNullOrWhiteSpace(_ipPort))
        {
            if (int.TryParse(_ipPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) port = parsed;
            else { problems.Add("포트는 숫자여야 한다"); return null; }
        }

        if (_usedNumbers(_category).Contains(number)) problems.Add($"장비번호 {number} 은(는) 이미 쓰이고 있다");

        return new PresetInstanceInfo
        {
            DeviceNumber = number,
            DeviceName = _deviceName?.Trim() ?? string.Empty,
            IpAddress = ShowsConnection && !string.IsNullOrWhiteSpace(_ipAddress) ? _ipAddress.Trim() : null,
            IpPort = port,
            UserName = ShowsCredentials && !string.IsNullOrWhiteSpace(_userName) ? _userName.Trim() : null,
            UserPassword = ShowsCredentials && !string.IsNullOrEmpty(_userPassword) ? _userPassword : null,
            Controller = ShowsController ? _controller : null,
        };
    }

    /// <summary>입력이 바뀔 때마다 검증하고 본문을 다시 만든다 — 미리보기가 곧 보낼 내용이다.</summary>
    private void Rebuild()
    {
        _request = null;
        Problems.Clear();

        var preset = SelectedPreset;
        if (preset is null)
        {
            PreviewJson = string.Empty;
            Finish();
            return;
        }

        var problems = new List<string>();
        var info = ReadInfo(problems);
        if (info is not null) problems.AddRange(PresetRequestBuilder.Validate(preset, info, _catalog));

        foreach (var problem in problems.Distinct()) Problems.Add(problem);

        if (info is not null)
        {
            try
            {
                var request = PresetRequestBuilder.Build(preset, info);
                PreviewJson = Indent(request.PreviewJson);     // 같은 내용을 읽기 좋게만 — 보내는 것은 request 그대로다
                if (Problems.Count == 0) _request = request;
            }
            catch (ArgumentException ex)
            {
                Problems.Add(ex.Message);
                PreviewJson = string.Empty;
            }
        }

        Finish();

        void Finish()
        {
            NotifyOfPropertyChange(nameof(PresetSummary));
            NotifyOfPropertyChange(nameof(HasProblems));
            NotifyOfPropertyChange(nameof(CanRegister));
        }
    }

    public async Task RegisterAsync(CancellationToken token = default)
    {
        if (!CanRegister || _request is null) return;

        IsBusy = true;
        try
        {
            var result = await _registrar.RegisterAsync(_request, token);
            Message = result.Message;
            if (!result.IsSuccess) return;      // 창과 입력은 그대로 — 고쳐서 다시 보낼 수 있다

            RegisteredDeviceId = result.NewDeviceId;
            await TryCloseAsync(true);
        }
        finally { IsBusy = false; }
    }

    public Task CancelAsync() => TryCloseAsync(false);
    #endregion

    private static string Indent(string json)
    {
        try { return Newtonsoft.Json.Linq.JToken.Parse(json).ToString(Newtonsoft.Json.Formatting.Indented); }
        catch (Newtonsoft.Json.JsonException) { return json; }
    }

    private void RefreshPresets()
    {
        Presets.Clear();
        if (FixedPreset is not null) return;
        foreach (var preset in _presets.ForCategory(_category)) Presets.Add(preset);
        _selectedPreset = Presets.FirstOrDefault();
        NotifyOfPropertyChange(nameof(SelectedPreset));
        NotifyOfPropertyChange(nameof(HasPresets));
    }

    public bool HasPresets => Presets.Count > 0 || FixedPreset is not null;

    private void Assign(ref string field, string? value, bool rebuild, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        var next = value ?? string.Empty;
        if (field == next) return;
        field = next;
        NotifyOfPropertyChange(name);
        if (rebuild) Rebuild();
    }
}
