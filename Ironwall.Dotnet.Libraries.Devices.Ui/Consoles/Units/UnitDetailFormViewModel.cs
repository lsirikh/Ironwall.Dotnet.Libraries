using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;

/****************************************************************************
   Purpose      : 부대 상세 폼 — 코드 잠금 · 손댄 칸 표지 · 인접 칩 (N-11 FR-13 ~ FR-16)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 상세 칸이 쥐는 값. 손댄 칸은 <see cref="ConsoleDetailPresenter.Tracker"/> 에 그대로 실려
/// 적용 막대가 "변경 N건 미적용" 으로 켜진다.
/// </summary>
/// <remarks>
/// <para><b>부대 코드</b>는 등록 화면에서만 입력 칸이고, 수정 화면에서는 🔒 읽기 전용이다
/// (스토리보드 화면 K L395-400). 그 값이 곧 NATS subject 의 두 번째 토큰이라 바꾸면 구독자가 메시지를 잃는다.</para>
/// </remarks>
public sealed class UnitDetailFormViewModel : PropertyChangedBase
{
    public const string FIELD_NAME = "name";
    public const string FIELD_ECHELON = "echelon";
    public const string FIELD_DESCRIPTION = "description";
    public const string FIELD_ENABLE = "is_enable";
    public const string FIELD_CODE = "code";
    public const string FIELD_PARENT = "parent_id";

    /// <summary>"최상위(루트)" 를 콤보에서 고를 수 있게 하는 자리표. 요청에서는 <c>null</c> 로 바뀐다.</summary>
    public const int ROOT_OPTION = 0;

    private readonly ConsoleDetailPresenter _presenter;

    private UnitDto? _original;
    private string _code = string.Empty;
    private string _name = string.Empty;
    private EnumUnitEchelon? _echelon;
    private string _description = string.Empty;
    private bool _isEnable = true;
    private int? _parentId;
    private string? _errorText;
    private int _deviceCount;
    private int _childCount;
    private bool _isCreating;
    private bool _isLoaded;
    private bool _isSeeding;

    public UnitDetailFormViewModel(ConsoleDetailPresenter presenter)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        Echelons = new BindableCollection<EnumUnitEchelon>(Enum.GetValues<EnumUnitEchelon>());
    }

    #region - Options -
    public BindableCollection<EnumUnitEchelon> Echelons { get; }

    /// <summary>상위 부대 피커 — 드래그의 키보드·버튼 폴백(드래그 규칙 §키보드 폴백 필수).</summary>
    public BindableCollection<UnitOptionViewModel> ParentOptions { get; } = new();

    /// <summary>인접 후보 — 같은 제대만 미리 걸러 둔다(와이어프레임 L361).</summary>
    public BindableCollection<UnitOptionViewModel> AdjacencyCandidates { get; } = new();

    public BindableCollection<UnitAdjacencyChipViewModel> AdjacencyChips { get; } = new();
    #endregion

    #region - Fields -
    /// <summary>부대 코드. 등록 화면에서만 입력이고 그 뒤로는 영원히 읽기 전용이다.</summary>
    public string Code
    {
        get => _code;
        set
        {
            if (_code == value) return;
            _code = value ?? string.Empty;
            NotifyOfPropertyChange();
            if (!_isSeeding && IsCreating) Touch(FIELD_CODE, string.Empty, _code);
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value ?? string.Empty;
            NotifyOfPropertyChange();
            if (!_isSeeding) Touch(FIELD_NAME, _original?.Name ?? string.Empty, _name);
        }
    }

    public EnumUnitEchelon? Echelon
    {
        get => _echelon;
        set
        {
            if (_echelon == value) return;
            _echelon = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(ChildEchelonWarning));
            NotifyOfPropertyChange(nameof(HasChildEchelonWarning));
            if (!_isSeeding) Touch(FIELD_ECHELON, _original?.Echelon, _echelon);
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (_description == value) return;
            _description = value ?? string.Empty;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(DescriptionCounter));
            if (!_isSeeding) Touch(FIELD_DESCRIPTION, _original?.Description ?? string.Empty, _description);
        }
    }

    public bool IsEnable
    {
        get => _isEnable;
        set
        {
            if (_isEnable == value) return;
            _isEnable = value;
            NotifyOfPropertyChange();
            if (!_isSeeding) Touch(FIELD_ENABLE, _original?.IsEnable ?? true, _isEnable);
        }
    }

    /// <summary>등록 화면의 상위 부대 선택. 수정 화면에서는 [옮기기] 가 곧바로 보낸다(적용 막대에 얹지 않는다).</summary>
    public int? ParentId
    {
        get => _parentId;
        set
        {
            if (_parentId == value) return;
            _parentId = value;
            NotifyOfPropertyChange();
            if (!_isSeeding && IsCreating) Touch(FIELD_PARENT, null, _parentId);
        }
    }
    #endregion

    #region - State -
    public bool IsCreating
    {
        get => _isCreating;
        private set { _isCreating = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsCodeLocked)); NotifyOfPropertyChange(nameof(CodeNote)); }
    }

    /// <summary>등록 뒤에는 코드 칸이 잠긴다 — 🔒.</summary>
    public bool IsCodeLocked => !_isCreating;

    /// <summary>부대 하나를 고른 상태인가(빈 화면과 구분).</summary>
    public bool IsLoaded { get => _isLoaded; private set { _isLoaded = value; NotifyOfPropertyChange(); } }

    public int DeviceCount { get => _deviceCount; set { _deviceCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CountsText)); } }
    public int ChildCount { get => _childCount; set { _childCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CountsText)); } }

    public string CountsText => $"소속 장비 {_deviceCount} · 하위 부대 {_childCount}";

    /// <summary>코드 칸 밑에 붙는 한 줄 — 등록에서는 경고, 그 뒤로는 불변 사실.</summary>
    public string CodeNote => IsCreating
        ? "이 값은 나중에 바꿀 수 없습니다 — 바꾸면 그 부대 구독자가 메시지를 잃습니다."
        : "등록 뒤 바꿀 수 없습니다(NATS subject 의 두 번째 토큰).";

    public string AdjacencyCountText => AdjacencyChips.Count == 0 ? string.Empty : AdjacencyChips.Count.ToString();
    public string DescriptionCounter => $"{_description.Length} / {UnitRules.DESCRIPTION_MAX_LENGTH}";

    public string? ErrorText { get => _errorText; set { _errorText = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(_errorText);

    /// <summary>제대를 바꾸면 이미 매달린 자식과 어긋날 수 있다 — 서버가 422 로 막기 전에 먼저 알린다(스토리보드 L354).</summary>
    public string ChildEchelonWarning { get; private set; } = string.Empty;
    public bool HasChildEchelonWarning => !string.IsNullOrEmpty(ChildEchelonWarning);

    public UnitDto? Original => _original;

    /// <summary>손댄 칸 표지 — 색이 아니라 <b>형태</b>(좌측 3px 앰버 줄)로 낸다.</summary>
    public bool IsNameTouched => _presenter.Tracker.IsTouched(FIELD_NAME);
    public bool IsEchelonTouched => _presenter.Tracker.IsTouched(FIELD_ECHELON);
    public bool IsDescriptionTouched => _presenter.Tracker.IsTouched(FIELD_DESCRIPTION);
    public bool IsEnableTouched => _presenter.Tracker.IsTouched(FIELD_ENABLE);
    #endregion

    #region - Seeding -
    /// <summary>수정 화면 — 서버가 준 값으로 채우고 손댄 칸 장부를 비운다.</summary>
    public void Load(UnitDetailDto detail, UnitTreeModel tree, int deviceCount)
    {
        ArgumentNullException.ThrowIfNull(detail);

        _isSeeding = true;
        try
        {
            _original = detail;
            IsCreating = false;
            IsLoaded = true;
            Code = detail.Code;
            Name = detail.Name;
            _echelon = detail.Echelon;
            Description = detail.Description ?? string.Empty;
            _isEnable = detail.IsEnable;
            _parentId = tree.Find(detail.Id)?.ParentId ?? detail.ParentId ?? ROOT_OPTION;
            DeviceCount = deviceCount;
            ChildCount = detail.Children?.Count ?? tree.Find(detail.Id)?.ChildIds.Count ?? 0;
            ErrorText = null;
            ChildEchelonWarning = string.Empty;

            RebuildAdjacency(detail, tree);
            RebuildOptions(tree, detail.Id, detail.Echelon);
            RaiseAdjacency();
        }
        finally
        {
            _isSeeding = false;
            RaiseAll();
        }
    }

    /// <summary>등록 화면 — 빈 칸에서 시작하고 코드 입력을 연다.</summary>
    public void BeginCreate(UnitTreeModel tree, int? preselectedParentId)
    {
        _isSeeding = true;
        try
        {
            _original = null;
            IsCreating = true;
            IsLoaded = true;
            Code = string.Empty;
            Name = string.Empty;
            _echelon = EnumUnitEchelon.Outpost;
            Description = string.Empty;
            _isEnable = true;
            _parentId = preselectedParentId ?? ROOT_OPTION;
            DeviceCount = 0;
            ChildCount = 0;
            ErrorText = null;
            ChildEchelonWarning = string.Empty;
            AdjacencyChips.Clear();
            AdjacencyCandidates.Clear();
            RebuildOptions(tree, unitId: 0, echelon: _echelon);
        }
        finally
        {
            _isSeeding = false;
            RaiseAll();
        }
    }

    public void Clear()
    {
        _isSeeding = true;
        try
        {
            _original = null;
            IsCreating = false;
            IsLoaded = false;
            Code = string.Empty;
            Name = string.Empty;
            _echelon = null;
            Description = string.Empty;
            _isEnable = true;
            _parentId = null;
            DeviceCount = 0;
            ChildCount = 0;
            ErrorText = null;
            ChildEchelonWarning = string.Empty;
            AdjacencyChips.Clear();
            AdjacencyCandidates.Clear();
            ParentOptions.Clear();
        }
        finally
        {
            _isSeeding = false;
            RaiseAll();
        }
    }
    #endregion

    #region - Derived -
    public UnitEditValues EditValues => new(Name, Echelon, Description, IsEnable);

    public UnitCreateValues CreateValues => new(Code, Name, Echelon ?? EnumUnitEchelon.Outpost, SelectedParentId, Description, IsEnable);

    /// <summary>고른 상위 부대 — 최상위(루트)면 <c>null</c>.</summary>
    public int? SelectedParentId => ParentId is int id && id > 0 ? id : null;

    /// <summary>지금 인접 집합(칩에서 읽는다).</summary>
    public IReadOnlyList<int> AdjacentIds => AdjacencyChips.Select(c => c.Id).ToList();

    /// <summary>제대를 바꿨을 때 자식과 어긋나는지 미리 본다.</summary>
    public void RefreshChildEchelonWarning(UnitTreeModel tree)
    {
        ChildEchelonWarning = string.Empty;
        if (_original == null || Echelon is not EnumUnitEchelon echelon) { RaiseWarning(); return; }

        var node = tree.Find(_original.Id);
        if (node == null || node.ChildIds.Count == 0) { RaiseWarning(); return; }

        var conflicting = node.ChildIds
            .Select(tree.Find)
            .Where(child => child?.Echelon is EnumUnitEchelon childEchelon && !UnitRules.IsAllowedParent(echelon, childEchelon))
            .Select(child => child!.Name)
            .ToList();

        if (conflicting.Count > 0)
            ChildEchelonWarning = $"하위 부대 {conflicting.Count}개({string.Join(" · ", conflicting.Take(3))})가 이 제대 아래에 올 수 없습니다 — 서버가 거절합니다.";

        RaiseWarning();
    }

    private void RaiseWarning()
    {
        NotifyOfPropertyChange(nameof(ChildEchelonWarning));
        NotifyOfPropertyChange(nameof(HasChildEchelonWarning));
    }

    private void RaiseAdjacency() => NotifyOfPropertyChange(nameof(AdjacencyCountText));
    #endregion

    #region - Helpers -
    private void RebuildAdjacency(UnitDetailDto detail, UnitTreeModel tree)
    {
        AdjacencyChips.Clear();
        // 순서가 곧 신뢰도다: ① 서버가 펼쳐 준 객체 ② 서버가 준 id 목록 ③ 마지막에야 트리(방금 읽은 것이 아닐 수 있다).
        // ②를 빼먹으면 인접을 바꾼 직후 상세가 <b>옛 트리 값</b>으로 되돌아간다.
        var ids = detail.Adjacent?.Select(a => a.Id).ToList()
               ?? (detail.AdjacentUnitIds is { Count: > 0 } ? detail.AdjacentUnitIds.ToList() : null)
               ?? tree.Find(detail.Id)?.AdjacentIds.ToList()
               ?? new List<int>();

        foreach (var id in ids.Distinct().OrderBy(x => x))
        {
            var node = tree.Find(id);
            var name = node?.Name ?? detail.Adjacent?.FirstOrDefault(a => a.Id == id)?.Name ?? $"#{id}";
            var echelon = node != null ? UnitDropRules.EchelonTextOf(node) : string.Empty;
            AdjacencyChips.Add(new UnitAdjacencyChipViewModel(id, name, echelon));
        }
    }

    /// <summary>상위 후보(엄격히 상위 제대 · 자기 자손 제외) · 인접 후보(같은 제대 · 자기 제외)를 다시 만든다.</summary>
    public void RebuildOptions(UnitTreeModel tree, int unitId, EnumUnitEchelon? echelon)
    {
        ParentOptions.Clear();
        // 루트는 null 이 아니라 0 으로 싣는다 — WPF 콤보는 SelectedValue=null 을 "고른 것 없음" 으로 읽어 칸이 빈 채로 남는다.
        ParentOptions.Add(new UnitOptionViewModel(ROOT_OPTION, "최상위(루트)"));

        foreach (var node in tree.Ordered)
        {
            if (node.Id == unitId) continue;
            if (unitId > 0 && tree.IsDescendantOf(node.Id, unitId)) continue;
            if (echelon is EnumUnitEchelon child && node.Echelon is EnumUnitEchelon parent && !UnitRules.IsAllowedParent(parent, child)) continue;
            if (echelon is not null && node.Echelon is null) continue;
            ParentOptions.Add(new UnitOptionViewModel(node.Id, $"{UnitDropRules.EchelonTextOf(node)} · {node.Name}"));
        }

        AdjacencyCandidates.Clear();
        if (unitId <= 0 || echelon is not EnumUnitEchelon self) return;

        var already = AdjacencyChips.Select(c => c.Id).ToHashSet();
        foreach (var node in tree.Ordered)
        {
            if (node.Id == unitId || already.Contains(node.Id)) continue;
            if (node.Echelon != self) continue;
            AdjacencyCandidates.Add(new UnitOptionViewModel(node.Id, node.Name));
        }
    }

    private void Touch(string key, object? original, object? current)
    {
        _presenter.Tracker.Touch(key, original, current);
        RaiseTouched();
    }

    private void RaiseTouched()
    {
        NotifyOfPropertyChange(nameof(IsNameTouched));
        NotifyOfPropertyChange(nameof(IsEchelonTouched));
        NotifyOfPropertyChange(nameof(IsDescriptionTouched));
        NotifyOfPropertyChange(nameof(IsEnableTouched));
    }

    private void RaiseAll()
    {
        NotifyOfPropertyChange(string.Empty);
    }
    #endregion
}
