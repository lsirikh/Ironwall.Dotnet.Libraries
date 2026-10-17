using Caliburn.Micro;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;

/// <summary>
/// 권한 상세(한 그룹) 행 — 모듈 1개 × 4동작(조회/편집/삭제/제어). 더블클릭한 그룹의 전체 권한 페이지.
/// XxxEnabled=해당 모듈에 그 동작이 적용 가능한지(PermissionCatalog) — false면 비활성(▦).
/// View/Edit/Delete/Control 은 편집가능(TwoWay) — 저장 시 PermissionsDto 로 직렬화. (PRD-GOP-01 IMPL-06)
/// <para>행 집합의 권위는 <b>서버</b>다 — 사전(<c>PermissionCatalog</c>)에 없는 모듈 키도
/// <see cref="IsUnknownModule"/>=true 행으로 노출된다(표시명 = 서버 키 그대로, 4동작 전부 활성).</para>
/// </summary>
public class ModulePermRowViewModel : PropertyChangedBase
{
    public string ModuleKey { get; init; } = string.Empty;
    public string ModuleDisplay { get; init; } = string.Empty;

    /// <summary>
    /// 사전에 표시명·동작 적용성이 <b>없는</b> 서버 모듈인가. true 면 <see cref="ModuleDisplay"/> 는 서버 키 문자열이고
    /// 4동작이 전부 활성이다(적용성 미확인 — 막지 않는다).
    /// </summary>
    public bool IsUnknownModule { get; init; }

    private bool _view, _edit, _delete, _control;
    public bool View    { get => _view;    set { _view = value;    NotifyOfPropertyChange(() => View); } }
    public bool Edit    { get => _edit;    set { _edit = value;    NotifyOfPropertyChange(() => Edit); } }
    public bool Delete  { get => _delete;  set { _delete = value;  NotifyOfPropertyChange(() => Delete); } }
    public bool Control { get => _control; set { _control = value; NotifyOfPropertyChange(() => Control); } }

    public bool ViewEnabled { get; init; } = true;
    public bool EditEnabled { get; init; } = true;
    public bool DeleteEnabled { get; init; } = true;
    public bool ControlEnabled { get; init; }

    /// <summary>
    /// 이 모듈의 <b>제어</b>를 서버가 실제로 집행하는가. 거짓이면 화면 게이팅용일 뿐이다 —
    /// 목업이 "실집행" 꼬리표로 구분하는 그 사실이다(window-layout-system-storyboard.html L1210 · L1214 · L1231).
    /// </summary>
    public bool IsControlServerEnforced { get; init; }
}
