using Caliburn.Micro;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;

/// <summary>
/// 권한 상세(한 그룹) 행 — 모듈 1개 × 4동작(조회/편집/삭제/제어). 더블클릭한 그룹의 전체 권한 페이지.
/// XxxEnabled=해당 모듈에 그 동작이 적용 가능한지(PermissionCatalog) — false면 비활성(▦).
/// View/Edit/Delete/Control 은 편집가능(TwoWay) — 저장 시 PermissionsDto 로 직렬화. (PRD-GOP-01 IMPL-06)
/// </summary>
public class ModulePermRowViewModel : PropertyChangedBase
{
    public string ModuleKey { get; init; } = string.Empty;
    public string ModuleDisplay { get; init; } = string.Empty;

    private bool _view, _edit, _delete, _control;
    public bool View    { get => _view;    set { _view = value;    NotifyOfPropertyChange(() => View); } }
    public bool Edit    { get => _edit;    set { _edit = value;    NotifyOfPropertyChange(() => Edit); } }
    public bool Delete  { get => _delete;  set { _delete = value;  NotifyOfPropertyChange(() => Delete); } }
    public bool Control { get => _control; set { _control = value; NotifyOfPropertyChange(() => Control); } }

    public bool ViewEnabled { get; init; } = true;
    public bool EditEnabled { get; init; } = true;
    public bool DeleteEnabled { get; init; } = true;
    public bool ControlEnabled { get; init; }
}
