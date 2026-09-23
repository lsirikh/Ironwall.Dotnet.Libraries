using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Models;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.ByComponent;
/****************************************************************************
   Purpose      : "부품으로 찾기" 콘솔 탭 뷰모델 (FR-17 — 읽기 전용 조회)
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// "부품으로 찾기" 탭 — <c>GET /api/devices/by-component</c> 하나로 부품 <b>유형</b> + 상태 + 건강을 걸러
/// 장비를 찾는 읽기 전용 조회 화면이다(FR-17). 콘솔 레일의 맨 아래 항목으로 꽂힌다.
/// </summary>
/// <remarks>
/// <para><b>왜 유형으로만 찾는가</b> — 서버 계약상 <c>component_type</c>·<c>component</c>(key) 중
/// <b>정확히 하나</b>만 보낼 수 있다(<see cref="IDeviceApiService.GetDevicesByComponentAsync"/> 규약,
/// 어겨서 보내면 <c>VALIDATION_ERROR</c> 로 즉시 막힌다). 이 화면은 유형 축만 다룬다 — key 검색 UI 는 없다.
/// 그래서 <see cref="SelectedComponentType"/> 이 비어 있으면 <see cref="SearchAsync"/> 는 애초에 서버를 부르지
/// 않는다(어차피 거부될 요청으로 왕복을 만들지 않는다).</para>
///
/// <para><b>상태 칩은 카탈로그가 아니라 직전 결과에서 뽑는다</b> — <see cref="ICatalogService"/> 는
/// <c>component_type</c> 어휘의 <c>{code,label}</c> 만 노출하고, 유형별 상태 어휘
/// (서버 <c>VocabularyEntryDto.States</c> = <c>definition.states</c>)는 이 서비스 계약 밖이다 — 다른 소비자가
/// 없어 넓히지 않았다(개발 보고서에 명시). 그래서 <see cref="StateChips"/> 는 직전 조회 결과에 <b>실제로
/// 나타난 상태값</b> + 맨 앞 "전체" 로 구성한다. 카탈로그가 아는 전체 어휘가 아니라 "이 조건에서 지금
/// 관측된 값"이라는 뜻이다 — 예컨대 상태 필터를 걸어 결과가 한 가지 상태로만 좁혀지면, 다음 칩 목록도
/// 그 한 가지만 보인다(서버 재왕복 없이 카탈로그 전체 어휘를 미리 아는 방법이 없어 생기는 자연스러운 결과).</para>
///
/// <para><b>재조회 배선</b> — <see cref="SelectedComponentType"/> 이 바뀌면 상태 선택을 <b>동기적으로</b>
/// "전체" 로 되돌리고 <see cref="StateChips"/> 도 비운다. 이 세터는 <b>스스로 서버를 부르지 않는다</b> —
/// 실제 재조회는 뷰가 콤보/칩의 <c>SelectionChanged</c> 에서 <see cref="SearchAsync"/> 를 명시적으로 부른다
/// (칩 클릭과 동일한 배선). 세터 안에서 fire-and-forget 로 비동기를 거는 대신 이렇게 한 이유는, 호출 시점이
/// 결정적이어야 테스트·재진입 양쪽에서 안전하기 때문이다(세터가 스스로 비동기를 걸면 "지금 어떤 검색이
/// 진행 중인가"를 외부에서 관찰할 방법이 없어진다).</para>
///
/// <para><b>단일 비행(single-flight)</b> — <see cref="SearchAsync"/> 를 호출할 때마다 이전 호출의
/// <see cref="CancellationTokenSource"/> 를 취소한다. 이전 호출이 취소 이후에도 계속 응답을 기다리고
/// 있었다면(가짜 지연), 응답이 와도 자신의 토큰이 이미 취소됐음을 보고 결과를 버린다 — 취소가 예외로
/// 오든(가짜 서비스 대부분은 안 던진다) <c>IsCancellationRequested</c> 로만 드러나든 양쪽 다 안전하다.</para>
/// </remarks>
public sealed class ByComponentViewModel : Screen
{
    #region - Ctors -
    public ByComponentViewModel(IDeviceApiService api, ICatalogService catalog, ILogService log, DeviceQueryPolicy? policy = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _policy = policy ?? DeviceQueryPolicy.Resolve();

        ComponentTypes = new BindableCollection<CatalogOption>();
        StateChips = new BindableCollection<string> { AllChip };
        Rows = new BindableCollection<ByComponentRowViewModel>();
        // Code 는 서버로 보내는 원문("OK" 등 — DestructiveGuard/서버가 아는 값), Label 은 화면 전용 한글
        // (ByComponentRowViewModel.ToHealth 와 같은 표, DeviceEnumDisplay.ComponentHealthKorean 이 정본).
        // ComponentTypes 콤보와 같은 관용구(CatalogOption + DisplayMemberPath) — 라벨을 바꿔도 보내는 값은 그대로다.
        HealthChips = new[]
        {
            AllHealthChip,
            new CatalogOption("OK", DeviceEnumDisplay.ComponentHealthKorean("OK")),
            new CatalogOption("DEGRADED", DeviceEnumDisplay.ComponentHealthKorean("DEGRADED")),
            new CatalogOption("FAULT", DeviceEnumDisplay.ComponentHealthKorean("FAULT")),
            new CatalogOption("UNKNOWN", DeviceEnumDisplay.ComponentHealthKorean("UNKNOWN")),
        };
        _selectedState = AllChip;
        _selectedHealth = AllHealthChip;

        if (IsAvailable)
        {
            _statusText = "부품 유형을 선택하세요.";
            LoadComponentTypesFromCatalog();
            _catalog.CatalogChanged += OnCatalogChanged;
            _catalogSubscribed = true;
            if (!_catalog.IsLoaded) _ = _catalog.EnsureLoadedAsync();
        }
        else
        {
            // 6.3 서버 — HTTP 왕복 없이 즉시 안내(IDeviceApiService.GetDevicesByComponentAsync 와 같은 게이트).
            _statusText = "이 서버 버전에서는 부품으로 찾기를 사용할 수 없습니다(계약 7.0 이상 필요).";
        }
    }
    #endregion

    #region - Properties -
    /// <summary>6.3 계약이면 <c>false</c> — 통합자가 이 값으로 레일 항목 자체를 숨긴다.</summary>
    public bool IsAvailable => _policy.IsAxisContract;

    /// <summary>부품 유형 콤보 원천 — 카탈로그 <c>component_type</c> 어휘(<see cref="ICatalogService.Vocabulary"/>).</summary>
    public BindableCollection<CatalogOption> ComponentTypes { get; }

    public CatalogOption? SelectedComponentType
    {
        get => _selectedComponentType;
        set
        {
            if (Equals(_selectedComponentType, value)) return;
            _selectedComponentType = value;
            NotifyOfPropertyChange(() => SelectedComponentType);

            // 종류가 바뀌면 이전 종류에서 관측된 상태값은 의미가 없다 — 전체로 되돌리고 칩도 비운다(remarks 참조).
            StateChips.Clear();
            StateChips.Add(AllChip);
            if (!string.Equals(_selectedState, AllChip, StringComparison.Ordinal))
            {
                _selectedState = AllChip;
                NotifyOfPropertyChange(() => SelectedState);
            }
        }
    }

    /// <summary>첫 항목은 항상 "전체" — 나머지는 직전 결과에서 관측된 상태값(remarks 참조).</summary>
    public BindableCollection<string> StateChips { get; }

    public string SelectedState
    {
        get => _selectedState;
        set
        {
            var next = value ?? AllChip;
            if (string.Equals(_selectedState, next, StringComparison.Ordinal)) return;
            _selectedState = next;
            NotifyOfPropertyChange(() => SelectedState);
        }
    }

    /// <summary>건강은 서버가 강한(strict) 4값 어휘라 카탈로그와 무관하게 고정 목록이다. Code=서버로 보내는 값, Display=한글.</summary>
    public IReadOnlyList<CatalogOption> HealthChips { get; }

    public CatalogOption SelectedHealth
    {
        get => _selectedHealth;
        set
        {
            var next = value ?? AllHealthChip;
            if (Equals(_selectedHealth, next)) return;
            _selectedHealth = next;
            NotifyOfPropertyChange(() => SelectedHealth);
        }
    }

    public BindableCollection<ByComponentRowViewModel> Rows { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            NotifyOfPropertyChange(() => IsBusy);
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (string.Equals(_statusText, value, StringComparison.Ordinal)) return;
            _statusText = value;
            NotifyOfPropertyChange(() => StatusText);
        }
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// 현재 선택(유형·상태·건강)으로 <c>GET /api/devices/by-component</c> 를 부른다. <b>절대 던지지 않는다</b> —
    /// 실패·취소는 <see cref="StatusText"/> 로만 드러난다. 더 새 호출이 들어오면 이전 호출은 결과를 버린다(단일 비행).
    /// </summary>
    public async Task SearchAsync(CancellationToken token = default)
    {
        if (!IsAvailable)
        {
            StatusText = "이 서버 버전에서는 부품으로 찾기를 사용할 수 없습니다(계약 7.0 이상 필요).";
            return;
        }

        var componentType = SelectedComponentType?.Code;
        if (string.IsNullOrWhiteSpace(componentType))
        {
            Rows.Clear();
            StatusText = "부품 유형을 선택하세요.";
            return;
        }

        // 단일 비행 — 이전 호출을 취소하고 이번 호출의 토큰만 신뢰한다.
        _searchCts?.Cancel();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _searchCts = cts;
        var myToken = cts.Token;

        IsBusy = true;
        try
        {
            var state = string.Equals(SelectedState, AllChip, StringComparison.Ordinal) ? null : SelectedState;
            var health = string.Equals(SelectedHealth.Code, AllChip, StringComparison.Ordinal) ? null : SelectedHealth.Code;

            ApiListResponse<ComponentStateRowDto> response;
            try
            {
                response = await _api.GetDevicesByComponentAsync(
                    componentType: componentType,
                    state: state,
                    health: health,
                    token: myToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return; // 더 새 검색이 이미 이겼다 — 조용히 버린다
            }
            catch (Exception ex)
            {
                if (myToken.IsCancellationRequested) return;   // 취소 도중의 부수 예외 — 역시 버린다
                _log.Error($"[ByComponent] 조회 예외 — {ex.Message}");
                Rows.Clear();
                StatusText = $"부품 조회 중 오류가 발생했습니다 ({ex.Message}).";
                return;
            }

            if (myToken.IsCancellationRequested) return;   // 더 새 검색이 이미 이겼다 — 이 결과는 버린다

            if (!response.Success || response.Data == null)
            {
                Rows.Clear();
                var code = response.Error?.Code ?? "UNKNOWN";
                _log.Warning($"[ByComponent] 조회 실패 — {code}: {response.Error?.Message ?? response.Message}");
                StatusText = $"부품 조회에 실패했습니다 ({code}).";
                return;
            }

            Rows.Clear();
            foreach (var dto in response.Data)
                Rows.Add(new ByComponentRowViewModel(dto, _catalog));

            RebuildStateChips(response.Data);

            StatusText = Rows.Count > 0 ? $"조건에 맞는 부품 {Rows.Count}건" : "조건에 맞는 부품이 없습니다";
        }
        finally
        {
            // 내가 아직도 "현재" 검색일 때만 끈다 — 이미 다음 검색이 시작됐으면 그쪽이 자기 몫을 끈다.
            if (ReferenceEquals(_searchCts, cts)) IsBusy = false;
        }
    }
    #endregion

    #region - Overrides -
    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        if (close && _catalogSubscribed)
        {
            _catalog.CatalogChanged -= OnCatalogChanged;
            _catalogSubscribed = false;
        }
        return base.OnDeactivateAsync(close, cancellationToken);
    }
    #endregion

    #region - Implementations -
    private void OnCatalogChanged(object? sender, EventArgs e) => LoadComponentTypesFromCatalog();

    private void LoadComponentTypesFromCatalog()
    {
        var options = _catalog.Vocabulary(DeviceSpecCatalogDto.VOCAB_COMPONENT_TYPE);
        var previousCode = SelectedComponentType?.Code;

        ComponentTypes.Clear();
        foreach (var option in options) ComponentTypes.Add(option);

        var next = ComponentTypes.FirstOrDefault(o => string.Equals(o.Code, previousCode, StringComparison.OrdinalIgnoreCase))
                   ?? ComponentTypes.FirstOrDefault();
        SelectedComponentType = next;
    }

    private void RebuildStateChips(IEnumerable<ComponentStateRowDto> data)
    {
        var distinct = data
            .Select(d => d.State)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var previousSelection = SelectedState;
        StateChips.Clear();
        StateChips.Add(AllChip);
        foreach (var s in distinct) StateChips.Add(s);

        // 직전 선택이 새 칩 목록에 없으면(예: 그 상태의 결과가 0건이 됨) 전체로 되돌린다 — 매달린 필터 방지.
        if (!StateChips.Contains(previousSelection, StringComparer.Ordinal))
            SelectedState = AllChip;
    }
    #endregion

    #region - Attributes -
    /// <summary>"필터 없음"을 뜻하는 칩 값 — 상태·건강 공통.</summary>
    public const string AllChip = "전체";

    /// <summary>건강 칩의 "전체" 항목 — Code=Label 이라 <see cref="CatalogOption.Display"/> 가 괄호 없이 "전체"만 보인다.</summary>
    private static readonly CatalogOption AllHealthChip = new(AllChip, AllChip);

    private readonly IDeviceApiService _api;
    private readonly ICatalogService _catalog;
    private readonly ILogService _log;
    private readonly DeviceQueryPolicy _policy;

    private CatalogOption? _selectedComponentType;
    private string _selectedState;
    private CatalogOption _selectedHealth;
    private bool _isBusy;
    private string _statusText;
    private CancellationTokenSource? _searchCts;
    private bool _catalogSubscribed;
    #endregion
}
