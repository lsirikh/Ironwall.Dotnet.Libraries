using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;

/// <summary>
/// 장비 패널의 <b>종류축</b>(<c>type_&lt;category&gt;</c>) 지원 — 콤보 선택지 · 목록 필터 · 저장 선차단.
/// 7개 패널이 이 하나를 조립해 쓴다(device-console-v8 FR-09 · FR-10).
/// </summary>
/// <remarks>
/// <para>선택지는 코드 상수가 아니라 <see cref="ICatalogService"/> 에서 온다. 6.3 계약에서는 비어 있고
/// <see cref="IsAxisUi"/> 가 <c>false</c> 라 화면은 옛 enum 콤보를 그대로 쓴다 — 그때는 아무것도 막지 않는다.</para>
/// <para><b>승계 불가 값은 고치지 않고 드러낸다.</b> 6.3 의 <c>Cable</c>·<c>THERMAL</c> 같은 값을 대응값으로 몰래 바꾸면
/// 장비 종류가 조용히 틀려진다. 원값을 "미대응: X" 로 보이고, 운용자가 유효한 값을 고를 때까지 <b>저장을 막는다</b>
/// (서버 422 를 받기 전에).</para>
/// </remarks>
public sealed class TypeAxisPanelSupport : PropertyChangedBase
{
    /// <summary>필터의 "전체" 항목 코드.</summary>
    public const string AllFilterCode = "";

    #region - Ctors -
    public TypeAxisPanelSupport(EnumDeviceCategory category, ICatalogService? catalog, DeviceQueryPolicy? policy = null)
    {
        Category = category;
        _catalog = catalog;
        _policy = policy ?? DeviceQueryPolicy.Resolve();
        if (_catalog != null) _catalog.CatalogChanged += (_, _) => Execute.OnUIThread(Rebuild);
        Rebuild();
    }

    /// <summary>
    /// 패널용 생성 — 카탈로그 서비스를 컨테이너에서 얻는다. 컨테이너 미구성(디자인 타임·단위 테스트)이면 카탈로그 없이 동작한다
    /// (선택지 없음 · 아무것도 막지 않음). 패널 생성자 시그니처를 넓히지 않으려고 여기서 해석한다.
    /// </summary>
    public static TypeAxisPanelSupport ForPanel(EnumDeviceCategory category)
    {
        ICatalogService? catalog = null;
        try { catalog = IoC.Get<ICatalogService>(); }
        catch (Exception) { /* 컨테이너 미구성 — 아래에서 카탈로그 없이 만든다 */ }
        var support = new TypeAxisPanelSupport(category, catalog);
        Registry[category] = support;   // panels are singletons: one support per category
        return support;
    }

    /// <summary>
    /// Row-level display text for a device's type axis. Rows cannot hold a reference to their panel, so the panel's support
    /// is looked up by category. Falls back to the raw code when no panel has been created (unit tests, design time).
    /// </summary>
    public static string DescribeFor(Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel model)
    {
        if (model == null) return string.Empty;
        return Registry.TryGetValue(DeviceAxesMapper.CategoryOf(model), out var support)
            ? support.Describe(model.TypeAxisCode)
            : model.TypeAxisCode ?? string.Empty;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<EnumDeviceCategory, TypeAxisPanelSupport> Registry = new();
    #endregion

    #region - Properties -
    public EnumDeviceCategory Category { get; }

    /// <summary>v7.0+ 축 UI 를 보인다(종류축 콤보·필터).</summary>
    public bool IsAxisUi => _policy.IsAxisContract;

    /// <summary>6.3 화면 그대로(옛 enum 콤보).</summary>
    public bool IsLegacyUi => !IsAxisUi;

    /// <summary>편집 콤보의 선택지.</summary>
    public IReadOnlyList<CatalogOption> Options { get; private set; } = Array.Empty<CatalogOption>();

    /// <summary>필터 콤보의 선택지 — 맨 앞이 "전체".</summary>
    public IReadOnlyList<CatalogOption> FilterOptions { get; private set; } = Array.Empty<CatalogOption>();

    /// <summary>스피커의 <c>speaker_role</c> 같은 추가 축. 없으면 빈 목록.</summary>
    public IReadOnlyList<CatalogExtraAxis> ExtraAxes { get; private set; } = Array.Empty<CatalogExtraAxis>();

    /// <summary>생성 시 필수인 축인가(제어기·센서·카메라). 형상 4축은 비워도 서버가 <c>Unknown</c> 을 배정한다.</summary>
    public bool IsRequired => _catalog?.TypeAxis(Category)?.RequiredOnCreate ?? false;

    /// <summary>목록 필터. <see cref="AllFilterCode"/>(또는 null)이면 전부. 바뀌면 <see cref="FilterChanged"/>.</summary>
    public string? SelectedFilterCode
    {
        get => _selectedFilterCode;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? AllFilterCode : value!.Trim();
            if (string.Equals(_selectedFilterCode, normalized, StringComparison.Ordinal)) return;
            _selectedFilterCode = normalized;
            NotifyOfPropertyChange();
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>필터가 바뀌었다 — 패널이 목록을 다시 채운다.</summary>
    public event EventHandler? FilterChanged;
    #endregion

    #region - Processes -
    /// <summary>카탈로그를 (아직 없으면) 읽고 선택지를 만든다. 6.3 이면 아무것도 하지 않는다.</summary>
    public async Task EnsureAsync(CancellationToken token = default)
    {
        if (_catalog == null || !IsAxisUi) return;
        await _catalog.EnsureLoadedAsync(token);
        Rebuild();
    }

    /// <summary>카탈로그를 다시 읽는다.</summary>
    public async Task RefreshAsync(CancellationToken token = default)
    {
        if (_catalog == null || !IsAxisUi) return;
        await _catalog.RefreshAsync(token);
        Rebuild();
    }

    /// <summary>
    /// 이 값으로 저장해도 되는가. 6.3 에서는 항상 참. 축 계약에서는 카탈로그 값이어야 하고,
    /// 비어 있는 것은 필수 축(제어기·센서·카메라)에서만 막는다.
    /// </summary>
    public bool IsValid(string? code)
    {
        if (!IsAxisUi || _catalog == null || !_catalog.IsLoaded) return true;   // 카탈로그를 못 읽었으면 막지 않는다 — 서버가 최종 판정한다
        if (string.IsNullOrWhiteSpace(code)) return !IsRequired;
        return _catalog.IsTypeAxisValue(Category, code);
    }

    /// <summary>표시 문자열 — 라벨(코드) · "미대응: 원값" · "선택 필요" · 형상 축의 서버 기본값.</summary>
    public string Describe(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            if (IsRequired) return "선택 필요";
            var fallback = _catalog?.TypeAxis(Category)?.DefaultCode;
            return string.IsNullOrEmpty(fallback) ? "—" : $"{DeviceEnumDisplay.TypeAxisKorean(fallback)} (기본값)";
        }

        var option = Options.FirstOrDefault(o => string.Equals(o.Code, code!.Trim(), StringComparison.OrdinalIgnoreCase));
        if (option != null) return option.ToString();

        // 카탈로그에 없는 값 — 원문 영문 코드 대신 "알 수 없음"(원문은 상세 칸의 툴팁 · 로그). 카탈로그를 못 읽었으면
        // 판정할 근거가 없으니 아는 사전으로만 옮긴다(모르면 원문).
        return IsAxisUi && _catalog?.IsLoaded == true ? DeviceEnumDisplay.UnknownValue : DeviceEnumDisplay.TypeAxisKorean(code!.Trim());
    }

    /// <summary>현재 필터에 걸리는가.</summary>
    public bool Matches(string? code)
    {
        if (!IsAxisUi || string.IsNullOrEmpty(_selectedFilterCode)) return true;
        return string.Equals(code?.Trim(), _selectedFilterCode, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>저장을 막는 행들(종류축이 비었거나 카탈로그에 없는 값).</summary>
    public IReadOnlyList<TRow> FindBlocked<TRow>(IEnumerable<TRow> rows) where TRow : IDeviceViewModel
        => rows.Where(r => !IsValid(r.Model.TypeAxisCode)).ToList();

    private void Rebuild()
    {
        // 서버 카탈로그 라벨이 코드와 같으면(한국어 라벨 없음) 목업 AX 표의 한국어로 바꿔 보인다 — 저장 값(Code)은 그대로다.
        var values = (IsAxisUi && _catalog != null ? _catalog.TypeAxisValues(Category) : Array.Empty<CatalogOption>())
            .Select(o => o with { Label = DeviceEnumDisplay.TypeAxisKorean(o.Code, o.Label) })
            .ToArray();
        Options = values;
        FilterOptions = values.Length == 0
            ? Array.Empty<CatalogOption>()
            : new[] { new CatalogOption(AllFilterCode, "전체") }.Concat(values).ToArray();
        ExtraAxes = IsAxisUi && _catalog != null ? _catalog.ExtraAxes(Category) : Array.Empty<CatalogExtraAxis>();

        NotifyOfPropertyChange(nameof(Options));
        NotifyOfPropertyChange(nameof(FilterOptions));
        NotifyOfPropertyChange(nameof(ExtraAxes));
        NotifyOfPropertyChange(nameof(IsRequired));
        NotifyOfPropertyChange(nameof(IsAxisUi));
        NotifyOfPropertyChange(nameof(IsLegacyUi));
    }
    #endregion

    #region - Attributes -
    private readonly ICatalogService? _catalog;
    private readonly DeviceQueryPolicy _policy;
    private string? _selectedFilterCode = AllFilterCode;
    #endregion
}
