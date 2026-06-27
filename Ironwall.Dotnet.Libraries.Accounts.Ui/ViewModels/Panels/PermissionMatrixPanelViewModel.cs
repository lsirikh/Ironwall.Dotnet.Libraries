using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 권한설정 매트릭스(읽기전용) — GOP /api/user-groups 그룹×모듈 권한 조회
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : IAccountApiService 직접 주입(GOP 모드 전용). UserGroupDto.Permissions.Modules(Dictionary)를
                  그룹×모듈 평탄화(PermissionRowViewModel)해 표시. ⚠ 조회 전용 — 기존 그룹 권한 편집저장은
                  서버 PUT permissions 차단(422, 권한상승 방어) + 클라 CRUD 미구현이라 v5.0 API 후 활성화.
                  모듈/verb 단일소스는 PermissionCatalog(Enums). 진짜 동적 매트릭스(모듈=열)는 후속.
****************************************************************************/
public class PermissionMatrixPanelViewModel : BasePanelViewModel
{
    private readonly IAccountApiService _api;

    public PermissionMatrixPanelViewModel(IEventAggregator eventAggregator, ILogService log, IAccountApiService api)
        : base(eventAggregator, log) => _api = api;

    /// <summary>그룹×모듈 평탄화 행. DataGrid ItemsSource.</summary>
    public ObservableCollection<PermissionRowViewModel> Rows { get; } = new();

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await ReloadAsync(cancellationToken);
    }

    public async Task OnClickReloadButton() => await ReloadAsync(CancellationToken.None);

    private async Task ReloadAsync(CancellationToken ct)
    {
        try
        {
            var res = await _api.GetUserGroupsAsync(ct);
            Rows.Clear();
            if (res.Success && res.Data is not null)
            {
                foreach (var g in res.Data)
                {
                    var modules = g.Permissions?.Modules;
                    if (modules is null) continue;
                    foreach (var kv in modules)
                        Rows.Add(new PermissionRowViewModel
                        {
                            GroupName = g.Name,
                            Module = kv.Key,
                            View = kv.Value.View,
                            Edit = kv.Value.Edit,
                            Delete = kv.Value.Delete,
                            Control = kv.Value.Control,
                        });
                }
            }
            else
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "권한 설정", Explain = $"불러오기 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (Exception ex) { _log?.Error($"[PermissionMatrix] 로드 실패: {ex.Message}"); }
    }
}
