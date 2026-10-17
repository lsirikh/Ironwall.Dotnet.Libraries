using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 권한 그룹 관리 — 그룹 CRUD(생성/이름수정/삭제) + 매트릭스(모듈×동작) 편집 + 구성원(계정) 배정
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : OQ-PG-01 확장(Option A→B, PRD GOP_Permission_Group_Management):
                  임의 권한그룹을 생성/삭제/이름수정하고, 계정을 그룹에 상시 배정(user.group_id)한다.
                  예약 5등급(ADMIN/MAINTAINER/OPERATOR/VIEWER/GUEST)은 삭제·개명 금지(매트릭스 편집만).
                  전 기능 ADMIN(콘솔 탭 CanSeePermission=IsAdmin + 서버 require_admin 최종방어).
                  3화면: 목록(List) / 권한매트릭스(Matrix) / 구성원(Members). 한시부여는 별도 '권한 부여' 탭.
****************************************************************************/
public class PermissionMatrixPanelViewModel : BasePanelViewModel, IHandle<CallDeleteGroupMessageModel>
{
    private readonly IAccountApiService _api;
    /// <summary>서버 계약 세대 프로브(선택 주입 — 미등록이면 <c>null</c>).</summary>
    private readonly IServerContractProbe? _contractProbe;
    /// <summary>
    /// 확보된 서버 계약 세대. 미확보·프로브 없음이면 <see cref="EnumServerContract.V6_3"/>(현 운영 판본 — 틀렸을 때 손해가 가장 작다).
    /// </summary>
    private EnumServerContract _contract = EnumServerContract.V6_3;
    private List<UserGroupDto> _raw = new();
    private string? _catalogWarning;

    // v5.4 Role Simplification(서버 v57): ADMIN/GUEST 등급그룹 DROP + 나머지 'Preset - X'로 rename + 편집 허용(NOTIFY §8.2).
    // → 예약 보호 없음(전 그룹 편집/삭제 가능). 'Preset' 접두는 표시/정렬용으로만 인식.
    private const string PresetPrefix = "Preset";
    private static bool IsPresetGroup(string name) => name.StartsWith(PresetPrefix, StringComparison.OrdinalIgnoreCase);

    // Autofac: (IServerContractProbe) 가 해소되면 4인자 생성자, 아니면 3인자로 우아하게 폴백(PermissionService 선례).
    //   → DI 모듈(AccountUiModule `RegisterType<PermissionMatrixPanelViewModel>()`) 수정 없이 주입된다.
    public PermissionMatrixPanelViewModel(IEventAggregator eventAggregator, ILogService log, IAccountApiService api)
        : this(eventAggregator, log, api, null) { }

    public PermissionMatrixPanelViewModel(IEventAggregator eventAggregator, ILogService log, IAccountApiService api,
                                         IServerContractProbe? contractProbe)
        : base(eventAggregator, log)
    {
        _api = api;
        _contractProbe = contractProbe;
    }

    #region - Properties -
    /// <summary>그룹 목록(요약). DataGrid ItemsSource(목록 화면).</summary>
    public ObservableCollection<PermissionGroupRowViewModel> Groups { get; } = new();
    /// <summary>선택 그룹 상세 — 8모듈×4동작. DataGrid ItemsSource(매트릭스 화면).</summary>
    public ObservableCollection<ModulePermRowViewModel> Modules { get; } = new();
    /// <summary>구성원 화면 — 선택 그룹 소속 계정.</summary>
    public ObservableCollection<AuthUserDto> Members { get; } = new();

    /// <summary>
    /// 서버가 내려준 <b>원본</b> 모듈 권한(GET 결과). 저장 시 **덮어쓰지 않고 합친다.**
    /// <para>⚠ 권한 저장은 <b>전체 교체</b>라 ① 서버 모듈이 하나라도 빠지면 <b>422 MISSING_FIELD</b>(v7.0+)
    /// 또는 <b>조용한 권한 삭제</b>(v6.3.17 이하)이고, ② 그 서버가 모르는 키를 보내면 <b>422</b> 다
    /// (<c>PermissionsSchema.modules.propertyNames = EnumPermissionModule</c> — 운영 6.3.2 스웨거도 동일).</para>
    /// <para>그래서 전송 집합은 <b>원본 ∪ (이 서버 세대가 아는 사전 모듈)</b> 하나뿐이다. 실측 대조:
    /// 개발 8.0.1 = 원본 13 ∪ 사전 16 = <b>16</b>(완전·미지 키 0) / 운영 6.3.2 = 원본 12 ∪ 사전 12종 = <b>12</b>(신규 4종 미전송).
    /// 화면 행 집합도 같은 식이라 저장은 행 집합의 결과로 자동 정합된다.</para>
    /// </summary>
    private IReadOnlyDictionary<string, ModulePermissionDto> _originModules
        = new Dictionary<string, ModulePermissionDto>();
    /// <summary>구성원 화면 — 추가 후보(미소속 계정).</summary>
    public ObservableCollection<AuthUserDto> AddableAccounts { get; } = new();

    private PermissionGroupRowViewModel? _selectedGroup;
    public PermissionGroupRowViewModel? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            _selectedGroup = value;
            NotifyOfPropertyChange(() => SelectedGroup);
            NotifyOfPropertyChange(() => CanModifySelectedGroup);
            NotifyOfPropertyChange(() => HasSelectedGroup);
        }
    }

    /// <summary>선택된 그룹이 있음 — 구성원 관리 버튼 활성(예약 등급 포함).</summary>
    public bool HasSelectedGroup => _selectedGroup is not null;

    // ── 화면 모드 ──
    private enum PanelMode { List, Matrix, Members }
    private PanelMode _mode = PanelMode.List;
    public bool IsListView => _mode == PanelMode.List;
    public bool IsDetailView => _mode == PanelMode.Matrix;
    public bool IsMembersView => _mode == PanelMode.Members;
    private void SetMode(PanelMode m)
    {
        _mode = m;
        NotifyOfPropertyChange(() => IsListView);
        NotifyOfPropertyChange(() => IsDetailView);
        NotifyOfPropertyChange(() => IsMembersView);
        NotifyOfPropertyChange(() => CanSave);
    }

    /// <summary>선택 그룹이 예약 등급이 아님 → 삭제/이름수정 가능(구성원 관리는 예약 그룹도 허용).</summary>
    public bool CanModifySelectedGroup => HasSelectedGroup;   // v5.4: 예약 보호 없음 — 선택 시 편집/삭제 가능

    // ── 매트릭스 상세 ──
    private string _detailGroupName = string.Empty;
    public string DetailGroupName { get => _detailGroupName; set { _detailGroupName = value; NotifyOfPropertyChange(() => DetailGroupName); } }
    private int _detailGroupId;
    private List<int>? _detailDeviceGroups;

    private bool _isSaving;
    public bool IsSaving { get => _isSaving; set { _isSaving = value; NotifyOfPropertyChange(() => IsSaving); NotifyOfPropertyChange(() => CanSave); } }
    /// <summary>[저장] 활성 — 매트릭스 화면 + 저장 중 아님.</summary>
    public bool CanSave => IsDetailView && !_isSaving;

    // ── 그룹 생성/이름수정 폼 ──
    private bool _isGroupFormOpen;
    public bool IsGroupFormOpen
    {
        get => _isGroupFormOpen;
        set
        {
            _isGroupFormOpen = value;
            NotifyOfPropertyChange(() => IsGroupFormOpen);
            NotifyOfPropertyChange(() => FormTitle);
            NotifyOfPropertyChange(() => FormSaveText);
        }
    }
    private int _formGroupId;   // 0 = 신규, >0 = 수정
    /// <summary>폼 머리 — 새로 만드는지 고치는지 구분한다(A-10).</summary>
    public string FormTitle => _formGroupId > 0 ? "그룹 이름/설명 수정" : "새 권한 그룹";
    /// <summary>폼 저장 단추의 글 — 만들기 / 저장.</summary>
    public string FormSaveText => _formGroupId > 0 ? "저장" : "만들기";
    private string _formName = string.Empty;
    public string FormName { get => _formName; set { _formName = value; NotifyOfPropertyChange(() => FormName); NotifyOfPropertyChange(() => CanSaveGroupForm); } }
    private string _formDescription = string.Empty;
    public string FormDescription { get => _formDescription; set { _formDescription = value; NotifyOfPropertyChange(() => FormDescription); } }
    public bool CanSaveGroupForm => !string.IsNullOrWhiteSpace(_formName);

    // ── 구성원 화면 ──
    private int _membersGroupId;
    private string _membersGroupName = string.Empty;
    public string MembersGroupName { get => _membersGroupName; set { _membersGroupName = value; NotifyOfPropertyChange(() => MembersGroupName); } }
    private AuthUserDto? _selectedAddAccount;
    public AuthUserDto? SelectedAddAccount { get => _selectedAddAccount; set { _selectedAddAccount = value; NotifyOfPropertyChange(() => SelectedAddAccount); NotifyOfPropertyChange(() => CanAddMember); } }
    public bool CanAddMember => _selectedAddAccount is not null;
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await ReloadAsync(cancellationToken);
    }
    #endregion

    #region - Console seam (N-06) -
    /// <summary>
    /// 저장 전 경고 — <b>전체 교체</b>라 화면이 싣지 못한 모듈이 있으면 저장 자체가 422 로 막힌다.
    /// 비어 있으면 경고 없음. (설계 정본 window-layout-system-storyboard.html L1225 · L1238)
    /// </summary>
    public string? CatalogWarning
    {
        get => _catalogWarning;
        private set { _catalogWarning = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasCatalogWarning)); }
    }

    public bool HasCatalogWarning => !string.IsNullOrEmpty(_catalogWarning);

    /// <summary>지금 매트릭스가 걸려 있는 그룹(0이면 없음).</summary>
    public int DetailGroupId => _detailGroupId;

    /// <summary>콘솔이 고른 그룹의 매트릭스를 연다(목록 화면의 더블클릭과 같은 경로).</summary>
    public void LoadMatrixFor(PermissionGroupRowViewModel? row)
    {
        SelectedGroup = row;
        if (row is null)
        {
            Modules.Clear();
            CatalogWarning = null;
            return;
        }
        OnClickGroupDetail();
    }

    /// <summary>콘솔이 고른 그룹의 구성원을 읽는다(구성원 칩과 같은 경로).</summary>
    public async Task LoadMembersFor(PermissionGroupRowViewModel? row)
    {
        SelectedGroup = row;
        if (row is null) { Members.Clear(); return; }
        await OnClickManageMembers();
    }

    /// <summary>콘솔의 [갱신].</summary>
    public Task ReloadForConsoleAsync(CancellationToken ct = default) => ReloadAsync(ct);
    #endregion

    #region - Binding: 목록/CRUD -
    public async Task OnClickReloadButton() => await ReloadAsync(CancellationToken.None);

    /// <summary>그룹 더블클릭 → 그 그룹의 전체 권한(모듈×동작) 매트릭스 화면으로 진입.</summary>
    public void OnClickGroupDetail()
    {
        var row = SelectedGroup;
        if (row is null) return;
        var g = _raw.FirstOrDefault(x => x.Id == row.GroupId);
        var mods = g?.Permissions?.Modules ?? new Dictionary<string, ModulePermissionDto>();
        // 저장 때 합치기 위해 서버 원본을 그대로 보관한다(우리 카탈로그에 없는 모듈 포함).
        _originModules = new Dictionary<string, ModulePermissionDto>(mods);
        _detailGroupId = row.GroupId;
        _detailDeviceGroups = g?.Permissions?.DeviceGroups;
        BuildMatrixRows(mods);
        DetailGroupName = row.GroupName;
        SetMode(PanelMode.Matrix);
    }

    /// <summary>
    /// 매트릭스 행 집합을 만든다 — <b>서버가 권위</b>다.
    /// <para>행 = <b>이 그룹의 원본 키</b> ∪ <b>이 서버 세대가 아는 사전 모듈</b>.
    /// <list type="number">
    /// <item>사전 모듈은 세대 어휘(<see cref="PermissionCatalog.ModulesForGeneration"/>)거나 원본에 실재하면 띄운다 —
    ///   그래서 서버가 모르는 모듈을 화면에 띄워 저장에서 422 를 만드는 일이 없다(운영 6.3.2 무회귀).</item>
    /// <item>사전에 <b>없는</b> 서버 키는 버리지 않고 <b>키 문자열 그대로</b> 행으로 띄운다(4동작 전부 활성) —
    ///   서버가 어휘를 17종으로 늘려도 <b>운영자가 켤 수 있다</b>. 종전 코드는 이걸 조용히 버려서
    ///   <c>action_report_templates</c>·<c>files</c>·<c>integrations</c>·<c>units</c> 가 <b>영구 403</b> 이었다.</item>
    /// </list></para>
    /// <para>세대 판정이 폴백(V6_3)으로 틀려도 원본 합집합이 메운다 — 8.0.1 실측 원본 13종이
    /// 사전 12종과 합쳐 16종이 되어 저장이 성립한다. 반대(6.3 서버를 8.0 으로 오판)는 프로브가
    /// 구조적으로 V6_3 쪽으로만 틀리므로 발생하지 않는다.</para>
    /// </summary>
    private void BuildMatrixRows(IReadOnlyDictionary<string, ModulePermissionDto> mods)
    {
        var generation = GenerationOf(_contract);
        Modules.Clear();

        var shown = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in PermissionCatalog.Modules)
        {
            var key = PermissionCatalog.ServerKey(m);
            // 이 서버 세대가 아는 모듈이거나, 이 그룹 저장분에 실재하는 모듈만 띄운다.
            if (PermissionCatalog.MinGeneration(m) > generation && !mods.ContainsKey(key)) continue;

            mods.TryGetValue(key, out var mp);
            Modules.Add(new ModulePermRowViewModel
            {
                ModuleKey = key,
                ModuleDisplay = Consoles.AccountModuleNames.Of(m),
                View = mp?.View ?? false,
                Edit = mp?.Edit ?? false,
                Delete = mp?.Delete ?? false,
                Control = mp?.Control ?? false,
                ViewEnabled = PermissionCatalog.IsVerbAllowed(m, EnumPermissionVerb.View),
                EditEnabled = PermissionCatalog.IsVerbAllowed(m, EnumPermissionVerb.Edit),
                DeleteEnabled = PermissionCatalog.IsVerbAllowed(m, EnumPermissionVerb.Delete),
                ControlEnabled = PermissionCatalog.IsVerbAllowed(m, EnumPermissionVerb.Control),
                IsControlServerEnforced = IsControlEnforced(key),
            });
            shown.Add(key);
        }

        // 사전 밖(= 우리가 표시명·동작 적용성을 모르는) 서버 어휘 — 키 그대로, 4동작 전부 활성.
        //   동작 적용성을 모르니 막지 않는다. 서버 스키마는 어느 모듈에도 4동작을 다 받으므로 전송은 안전하다.
        foreach (var kv in mods.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (shown.Contains(kv.Key)) continue;
            Modules.Add(new ModulePermRowViewModel
            {
                ModuleKey = kv.Key,
                // 사전에 없는 모듈 — 서버 이름(영문 키)을 화면에 찍지 않는다. 키는 칸 툴팁이 보인다.
                ModuleDisplay = UnknownModuleName,
                View = kv.Value?.View ?? false,
                Edit = kv.Value?.Edit ?? false,
                Delete = kv.Value?.Delete ?? false,
                Control = kv.Value?.Control ?? false,
                ViewEnabled = true,
                EditEnabled = true,
                DeleteEnabled = true,
                ControlEnabled = true,
                IsUnknownModule = true,
                IsControlServerEnforced = IsControlEnforced(kv.Key),
            });
            _log?.Warning($"[PermGroup] 사전에 없는 권한 모듈 키 '{kv.Key}' — 키 그대로 노출한다(표시명·동작 적용성 미확인).");
        }

        CatalogWarning = BuildCatalogWarning();
    }

    /// <summary>
    /// 저장이 막힐 수 있는 자리를 미리 말한다. 저장 본문은 <b>원본 ∪ 이 세대 어휘</b>라 여기서 볼 수 있는 위험은 둘뿐이다:
    /// ① 판본을 확정하지 못해 어휘를 좁게 잡았을 수 있다 ② 사전에 없는 서버 키가 섞여 있다(표시명·동작 적용성 미확인).
    /// </summary>
    private string? BuildCatalogWarning()
    {
        var notes = new List<string>();
        if (_contractProbe is null || !_contractProbe.IsResolved)
            // 배너 폭(278px)에 맞춰 짧게 — 길면 마지막 줄에 글자 하나만 남는다(D-17: 셋째 줄 12px 실측).
            notes.Add(UnresolvedServerNote);

        var unknown = Modules.Count(m => m.IsUnknownModule);
        if (unknown > 0)
            notes.Add($"새로 추가된 모듈이 {unknown}개 있습니다. 이름은 확인 중입니다.");

        return notes.Count == 0 ? null : string.Join(" · ", notes);
    }

    /// <summary>
    /// 계약 세대 → <see cref="PermissionCatalog"/> 세대 정수. <b><c>&gt;=</c> 비교만</b> 쓴다(동치 비교 금지 — 새 판본이 나와도 안전).
    /// </summary>
    /// <summary>
    /// 서버가 <b>제어</b>를 실제로 집행하는 모듈 — 나머지의 제어 체크는 화면 게이팅용일 뿐이다(목업 L1231).
    /// </summary>
    public static bool IsControlEnforced(string moduleKey)
        => moduleKey is "devices" or "users";

    private static int GenerationOf(EnumServerContract contract)
        => contract >= EnumServerContract.V8_0 ? PermissionCatalog.GEN_V8_0
         : contract >= EnumServerContract.V7_0 ? PermissionCatalog.GEN_V7_0
         : PermissionCatalog.GEN_V6_3;

    public void ClickBackToList() { IsGroupFormOpen = false; SetMode(PanelMode.List); }

    /// <summary>새 그룹 폼 열기.</summary>
    public void OnClickNewGroup()
    {
        _formGroupId = 0;
        FormName = string.Empty;
        FormDescription = string.Empty;
        NotifyOfPropertyChange(() => FormTitle);
        IsGroupFormOpen = true;
    }

    /// <summary>선택 그룹 이름/설명 수정 폼 열기 (예약 등급 제외).</summary>
    public void OnClickRenameGroup()
    {
        var row = SelectedGroup;
        if (row is null) return;
        var g = _raw.FirstOrDefault(x => x.Id == row.GroupId);
        _formGroupId = row.GroupId;
        FormName = g?.Name ?? row.GroupName;
        FormDescription = g?.Description ?? string.Empty;
        NotifyOfPropertyChange(() => FormTitle);
        IsGroupFormOpen = true;
    }

    public void ClickCancelGroupForm() => IsGroupFormOpen = false;

    /// <summary>그룹 폼 저장 — _formGroupId=0이면 생성(POST), >0이면 메타 수정(PUT).</summary>
    public async Task ClickSaveGroupForm()
    {
        if (!CanSaveGroupForm) return;
        try
        {
            if (_formGroupId > 0)
            {
                var res = await _api.UpdateUserGroupAsync(_formGroupId, new UserGroupUpdateDto { Name = FormName.Trim(), Description = FormDescription });
                if (!res.Success) { await Info(Explain("그룹 수정", res.StatusCode, res.Error?.Code, res.Error?.Message ?? res.Message, DuplicateGroupName, "그룹 정보를 저장하지 못했습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요.")); return; }
            }
            else
            {
                var res = await _api.CreateUserGroupAsync(new UserGroupCreateDto { Name = FormName.Trim(), Description = FormDescription });
                if (!res.Success) { await Info(Explain("그룹 생성", res.StatusCode, res.Error?.Code, res.Error?.Message ?? res.Message, DuplicateGroupName, "새 권한 그룹을 만들지 못했습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요.")); return; }
            }
            IsGroupFormOpen = false;
            await Info(_formGroupId > 0 ? "그룹 정보를 수정했습니다." : "새 권한 그룹을 생성했습니다.");   // 성공 피드백(grant 생성 패턴 정합·진단)
            await ReloadAsync(CancellationToken.None);
        }
        catch (Exception ex) { _log?.Error($"[PermGroup] 그룹 저장 실패: {ex.Message}"); }
    }

    /// <summary>선택 그룹 삭제 → Confirm(소속 해제 경고). Yes 시 HandleAsync 가 실제 DELETE.</summary>
    public async Task OnClickDeleteGroup()
    {
        var row = SelectedGroup;
        if (row is null) return;
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "권한 그룹 삭제",
            Explain = $"'{row.GroupName}' 그룹을 삭제하시겠습니까?\n소속 계정 {row.UserCount}명의 그룹 배정이 해제됩니다(해당 계정 권한 초기화).",
            MessageModel = new CallDeleteGroupMessageModel { GroupId = row.GroupId, GroupName = row.GroupName }
        });
    }

    public async Task HandleAsync(CallDeleteGroupMessageModel message, CancellationToken cancellationToken)
    {
        if (message is null || message.GroupId <= 0) return;
        try
        {
            var res = await _api.DeleteUserGroupAsync(message.GroupId);
            if (res.Success) await ReloadAsync(CancellationToken.None);
            else
            {
                _log?.Warning($"[PermGroup] 그룹 삭제 거부: {res.StatusCode} {res.Error?.Code} {res.Error?.Message ?? res.Message}");
                await Info("권한 그룹을 삭제하지 못했습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요.");
            }
        }
        catch (Exception ex) { _log?.Error($"[PermGroup] 그룹 삭제 실패: {ex.Message}"); }
        finally
        {
            // (MC-PM-4/INV-4) ConfirmPopupDialog.ClickOk은 MessageModel만 발행·self-close 안 함(사용자 실측 버그 ②).
            //   → 예외/성공 무관 finally에서 청산(DeleteUserGroupAsync throw 시에도 확인팝업 잔존 방지).
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
        }
    }
    #endregion

    /// <summary>
    /// 저장 본문의 모듈 사전을 만든다 — <b>서버 원본에 화면 편집분을 덮어쓴다</b>(원본 ∪ 행 집합).
    /// <para>행 집합이 <c>BuildMatrixRows</c> 에서 "원본 ∪ 이 서버 세대 어휘"로 구성되므로,
    /// 결과는 ① 그 서버가 아는 모듈을 <b>빠짐없이</b> 담고(전체 교체 계약 충족) ② <b>모르는 키를 담지 않는다</b>.
    /// 원본에만 있고 사전에 없는 키도 값 그대로 실려 나가 사라지지 않는다.</para>
    /// </summary>
    private Dictionary<string, ModulePermissionDto> BuildMergedModules()
    {
        var merged = new Dictionary<string, ModulePermissionDto>(StringComparer.Ordinal);
        foreach (var kv in _originModules)          // ① 서버가 아는 것 전부 보존
            merged[kv.Key] = kv.Value;
        foreach (var m in Modules)                  // ② 화면에서 편집한 것으로 덮어쓰기
            merged[m.ModuleKey] = new ModulePermissionDto
            { View = m.View, Edit = m.Edit, Delete = m.Delete, Control = m.Control };
        return merged;
    }

    #region - Binding: 매트릭스 저장 -
    /// <summary>현재 매트릭스 그룹의 권한(모듈×동작)을 서버에 저장(ADMIN). POST /user-groups/{id}/permissions.</summary>
    public async Task OnClickSave() => await SaveGroupPermissionsAsync().ConfigureAwait(true);

    /// <summary>
    /// 권한 저장 — <b>성패를 돌려준다</b>. 화면(콘솔)이 실패했을 때 미적용 상태를 그대로 두려면 이 결과가 있어야 한다.
    /// </summary>
    /// <remarks>
    /// 보내기 직전에 그 그룹을 <b>다시 읽어</b> 열었을 때의 원본과 비교한다 — 그 사이 다른 관리자가 바꿨으면
    /// 아무것도 보내지 않고 멈춘다(전체 교체라 그대로 보내면 남의 변경을 조용히 덮는다).
    /// </remarks>
    public async Task<PermissionSaveOutcome> SaveGroupPermissionsAsync(CancellationToken ct = default)
    {
        if (_detailGroupId <= 0) return PermissionSaveOutcome.Fail("고른 그룹이 없습니다.");

        try
        {
            IsSaving = true;

            var drift = await DetectOriginDriftAsync(ct).ConfigureAwait(true);
            if (drift is not null)
            {
                await Info(drift);
                return PermissionSaveOutcome.Fail(drift, isDrift: true);
            }

            var dto = new PermissionsDto
            {
                DeviceGroups = _detailDeviceGroups,
                // 원본 ∪ 화면 편집분 — 우리가 모르는 모듈을 떨어뜨리면 422(전체 교체 계약).
                Modules = BuildMergedModules(),
            };
            var res = await _api.UpdateGroupPermissionsAsync(_detailGroupId, dto, ct).ConfigureAwait(true);
            if (!res.Success)
            {
                _log?.Warning($"[PermGroup] 권한 저장 거부: {res.StatusCode} {res.Error?.Code} {res.Error?.Message ?? res.Message}");
                await Info(SaveRejectedText);
                return PermissionSaveOutcome.Fail(SaveRejectedShort);
            }

            await Info($"'{DetailGroupName}' 그룹의 권한을 저장했습니다.");
            // 저장분이 새 원본이다 — 다음 저장의 드리프트 비교 기준.
            _originModules = new Dictionary<string, ModulePermissionDto>(dto.Modules, StringComparer.Ordinal);
            await ReloadAsync(ct).ConfigureAwait(true);
            return PermissionSaveOutcome.Ok();
        }
        catch (Exception ex)
        {
            _log?.Error($"[PermGroup] 권한 저장 실패: {ex.Message}");
            return PermissionSaveOutcome.Fail("서버에 닿지 못했습니다.");
        }
        finally { IsSaving = false; }
    }

    /// <summary>
    /// 열었을 때의 원본과 지금 서버 값이 다른가. 다르면 사람이 읽을 한 줄, 같거나 확인할 수 없으면 null.
    /// </summary>
    private async Task<string?> DetectOriginDriftAsync(CancellationToken ct)
    {
        try
        {
            var res = await _api.GetAllUserGroupsAsync(ct).ConfigureAwait(true);
            if (!res.Success || res.Data is null) return null;   // 못 읽었으면 막지 않는다(저장은 서버가 판정한다)

            var now = res.Data.FirstOrDefault(g => g.Id == _detailGroupId);
            if (now is null) return $"'{DetailGroupName}' 그룹이 사라졌습니다 — 목록을 갱신하세요.";

            var server = now.Permissions?.Modules ?? new Dictionary<string, ModulePermissionDto>();
            if (SameModules(_originModules, server)) return null;

            return $"‘{DetailGroupName}’ 그룹의 권한이 다른 곳에서 먼저 바뀌어 저장하지 않았습니다. 새로 불러오기(⟳)를 누른 뒤 다시 고치세요.";
        }
        catch (Exception ex)
        {
            _log?.Warning($"[PermGroup] 저장 전 재확인 실패(막지 않는다): {ex.Message}");
            return null;
        }
    }

    private static bool SameModules(IReadOnlyDictionary<string, ModulePermissionDto> a, IReadOnlyDictionary<string, ModulePermissionDto> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var (key, left) in a)
        {
            if (!b.TryGetValue(key, out var right)) return false;
            if (left.View != right.View || left.Edit != right.Edit || left.Delete != right.Delete || left.Control != right.Control) return false;
        }
        return true;
    }
    #endregion

    #region - Binding: 구성원 -
    /// <summary>선택 그룹의 구성원 관리 화면으로 진입.</summary>
    public async Task OnClickManageMembers()
    {
        var row = SelectedGroup;
        if (row is null) return;
        _membersGroupId = row.GroupId;
        MembersGroupName = row.GroupName;
        SetMode(PanelMode.Members);
        await ReloadMembersAsync();
    }

    /// <summary>선택 계정을 이 그룹에 배정(user.group_id = 그룹).</summary>
    public async Task ClickAddMember()
    {
        var acc = SelectedAddAccount;
        if (acc is null || _membersGroupId <= 0) return;
        try
        {
            var res = await _api.AssignUserGroupAsync(acc.Id, _membersGroupId);
            if (res.Success) await ReloadMembersAsync();
            else
            {
                _log?.Warning($"[PermGroup] 구성원 추가 거부: {res.StatusCode} {res.Error?.Code} {res.Error?.Message ?? res.Message}");
                await Info("구성원을 추가하지 못했습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요.");
            }
        }
        catch (Exception ex) { _log?.Error($"[PermGroup] 구성원 추가 실패: {ex.Message}"); }
    }

    /// <summary>구성원 해제(user.group_id = null).
    /// ⚠ 서버 update_user(users.py: `if group_id is not None`)가 null을 무시할 수 있음 → 반환 사용자 group_id로 실제 반영 확인,
    /// 미반영 시 무증상 실패 대신 안내(서버 수정 대기, PRD V-03).</summary>
    public async Task OnClickRemoveMember(AuthUserDto member)
    {
        if (member is null) return;
        try
        {
            var res = await _api.AssignUserGroupAsync(member.Id, null);
            if (!res.Success)
            {
                _log?.Warning($"[PermGroup] 구성원 해제 거부: {res.StatusCode} {res.Error?.Code} {res.Error?.Message ?? res.Message}");
                await Info("구성원을 그룹에서 빼지 못했습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요.");
                return;
            }
            if (res.Data?.GroupId == _membersGroupId)
            {
                _log?.Warning("[PermGroup] 서버가 group_id=null 을 반영하지 않았다(구성원 해제 미지원 판본).");
                await Info(RemoveUnsupportedText);
                return;
            }
            await ReloadMembersAsync();
        }
        catch (Exception ex) { _log?.Error($"[PermGroup] 구성원 해제 실패: {ex.Message}"); }
    }

    public async Task OnClickBackFromMembers() { SetMode(PanelMode.List); await ReloadAsync(CancellationToken.None); }
    #endregion

    #region - Processes -
    private async Task ReloadAsync(CancellationToken ct)
    {
        try
        {
            IsGroupFormOpen = false;
            SetMode(PanelMode.List);
            // 행/전송 집합이 서버 어휘에 따라 달라진다 → 목록 로드마다 세대를 확보한다(멱등 — 확보 후엔 필드 읽기 수준).
            _contract = await _contractProbe.GetContractAsync(ct).ConfigureAwait(true);
            var res = await _api.GetAllUserGroupsAsync(ct);   // 그룹도 limit 상한 100 — page 순회로 전량(100개 초과 무증상 절단 제거)
            if (!res.Success || res.Data is null)   // (MC-PM-1/INV-13) swap-on-success — 실패 시 Groups.Clear 전 return(기존 목록 보존, 화면 공백 방지)
            {
                _log?.Warning($"[PermGroup] 그룹 조회 거부: {res.StatusCode} {res.Error?.Code} {res.Error?.Message ?? res.Message}");
                await Info("권한 그룹을 불러오지 못했습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요.");
                return;
            }
            _raw = res.Data;

            // 그룹별 사용자 수 = group_id 소속 계정 수(상시 배정). 서버 limit 상한=100 이라 전량은 page 순회로만 얻는다
            // (단일 호출이면 101번째 계정부터 집계에서 빠져 인원수가 조용히 작게 표시됐다).
            var usersRes = await _api.GetAllUsersAsync(ct);
            var users = (usersRes.Success && usersRes.Data is not null) ? usersRes.Data : new List<AuthUserDto>();
            var countByGroupId = users.Where(u => u.GroupId.HasValue)
                                      .GroupBy(u => u.GroupId!.Value)
                                      .ToDictionary(x => x.Key, x => x.Count());

            // v5.4: 팀 그룹 먼저 → Preset 그룹, 각 이름 asc. 예약 보호 없음(전 그룹 편집/삭제 가능).
            var ordered = _raw
                .OrderBy(g => IsPresetGroup(g.Name) ? 1 : 0)
                .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            ReconcileGroups(ordered, countByGroupId);
            // (MC-PM-1) 실패 통지는 위 swap-on-success 가드로 이동(여기 도달 = 성공).
        }
        catch (Exception ex) { _log?.Error($"[PermGroup] 로드 실패: {ex.Message}"); }
    }

    /// <summary>
    /// 그룹 목록을 <b>제자리에서</b> 맞춘다 — <c>Clear()+Add()</c> 로 갈아 끼우면 고른 그룹이 풀려
    /// 편집 중이던 매트릭스가 통째로 비어 버린다(저장 직후 재조회가 바로 그 길이다).
    /// </summary>
    private void ReconcileGroups(IReadOnlyList<UserGroupDto> ordered, IReadOnlyDictionary<int, int> countByGroupId)
    {
        // ① 사라진 그룹을 뺀다.
        for (var i = Groups.Count - 1; i >= 0; i--)
            if (ordered.All(g => g.Id != Groups[i].GroupId)) Groups.RemoveAt(i);

        // ② 있는 것은 값만 고치고, 없는 것은 제자리에 끼운다(순서도 서버 순서에 맞춘다).
        for (var index = 0; index < ordered.Count; index++)
        {
            var dto = ordered[index];
            int v = 0, e = 0, d = 0, c = 0;
            if (dto.Permissions?.Modules is { } mods)
                foreach (var kv in mods.Values)
                {
                    if (kv.View) v++;
                    if (kv.Edit) e++;
                    if (kv.Delete) d++;
                    if (kv.Control) c++;
                }

            var row = Groups.FirstOrDefault(r => r.GroupId == dto.Id);
            if (row is null)
            {
                row = new PermissionGroupRowViewModel { GroupId = dto.Id };
                Groups.Insert(Math.Min(index, Groups.Count), row);
            }
            else
            {
                var at = Groups.IndexOf(row);
                if (at != index && index < Groups.Count) Groups.Move(at, index);
            }

            row.GroupName = dto.Name;
            row.UserCount = countByGroupId.TryGetValue(dto.Id, out var uc) ? uc : 0;
            row.Active = dto.IsActive ? "사용" : "미사용";
            row.ViewCount = v;
            row.EditCount = e;
            row.DeleteCount = d;
            row.ControlCount = c;
        }
    }

    private async Task ReloadMembersAsync()
    {
        try
        {
            var res = await _api.GetUserGroupUsersAsync(_membersGroupId);
            if (!res.Success || res.Data is null)   // (MC-PM-1/INV-13) 실패 시 Members.Clear 전 return(기존 구성원 보존)
            {
                _log?.Warning($"[PermGroup] 구성원 조회 거부: {res.StatusCode} {res.Error?.Code} {res.Error?.Message ?? res.Message}");
                await Info("구성원을 불러오지 못했습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요.");
                return;
            }
            var members = res.Data;
            Members.Clear();
            foreach (var u in members) Members.Add(u);

            // 추가 후보 = 전체 계정 − 현재 구성원 (전량 순회 — 단일 페이지면 101번째부터 후보에서 사라진다)
            AddableAccounts.Clear();
            var allRes = await _api.GetAllUsersAsync();
            if (allRes.Success && allRes.Data is not null)
            {
                var memberIds = new HashSet<int>(members.Select(m => m.Id));
                foreach (var u in allRes.Data.Where(u => !memberIds.Contains(u.Id))) AddableAccounts.Add(u);
            }
            SelectedAddAccount = null;
        }
        catch (Exception ex) { _log?.Error($"[PermGroup] 구성원 로드 실패: {ex.Message}"); }
    }

    private Task Info(string msg) =>
        _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = "권한 그룹", Explain = msg });

    /// <summary>그룹 이름 중복 409 안내 — 서버 <c>POST/PUT /api/user-groups</c> 409 "같은 name 의 그룹이 이미 있음".</summary>
    private const string DuplicateGroupName = "같은 이름의 권한 그룹이 이미 있습니다. 다른 이름을 입력해 주세요.";

    /// <summary>
    /// 서버 실패를 사용자 문구로 바꾼다 — 409(<c>CONFLICT</c>)는 영문 원문 대신 <paramref name="conflict"/> 안내를 쓴다.
    /// <para>409 는 배포 8.0.1 에서 계정·그룹·세션 경로에 실제로 선언돼 있다(그룹 이름 중복, 마지막 ADMIN 보호,
    /// 자기 계정 삭제 등). 운영 6.3.2 는 선언이 없으므로 이 분기에 도달하지 않고 종전 문구가 그대로 쓰인다(무회귀).</para>
    /// </summary>
    /// <remarks>
    /// 409 가 아니면 고정 문장 <paramref name="fallback"/> — 서버 원문은 팝업에 붙이지 않고 로그로만 남긴다
    /// (A-38: 영문 원문 · 코드가 운영자 팝업에 떴다).
    /// </remarks>
    private string Explain(string what, int statusCode, string? code, string? serverMessage, string conflict, string fallback)
    {
        _log?.Warning($"[PermGroup] {what} 거부: {statusCode} {code} {serverMessage}");
        return (statusCode == 409 || string.Equals(code, "CONFLICT", StringComparison.OrdinalIgnoreCase)) ? conflict : fallback;
    }

    /// <summary>사전에 없는 모듈의 표시명 — 서버 키는 칸의 툴팁으로만 보인다.</summary>
    public const string UnknownModuleName = "알 수 없는 모듈";

    /// <summary>서버 판본을 확인하지 못했을 때의 배너(상세 칸 폭에 두 줄 안).</summary>
    public const string UnresolvedServerNote = "서버 버전을 확인하지 못했습니다. 저장이 거부되면 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요.";

    /// <summary>권한 저장을 서버가 거부했을 때 — 팝업(긴 글)과 바닥 막대(짧은 글).</summary>
    public const string SaveRejectedText = "권한을 저장하지 못했습니다. 새로 불러오기(⟳)를 누른 뒤 다시 시도하세요.";
    public const string SaveRejectedShort = "저장하지 못했습니다";

    /// <summary>서버가 구성원 해제를 받아들이지 않는 판본일 때(A-17).</summary>
    public const string RemoveUnsupportedText = "이 서버 버전에서는 구성원을 그룹에서 뺄 수 없습니다. 관리자에게 문의하세요.";
    #endregion
}

/// <summary>권한 그룹 삭제 확인 트리거 — Confirm 다이얼로그 Yes 시 발행되어 HandleAsync 가 실제 DELETE.</summary>
public class CallDeleteGroupMessageModel : IMessageModel
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
}
