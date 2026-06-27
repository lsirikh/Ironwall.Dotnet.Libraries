using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 감사 로그 뷰어(읽기전용) — GOP /api/audit-logs 조회
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : IAccountApiService 직접 주입(게이트웨이 미노출, GOP 모드 전용). append-only 서버 로그라 read-only.
                  활성화 시 1회 로드 + 갱신 버튼. 권한설정 매트릭스/세션과 동일 패턴(BasePanelViewModel).
                  ⚠ UI 스레드 갱신: OnActivate가 UI 스레드(탭 전환)라 ConfigureAwait 미사용으로 컬렉션 갱신.
****************************************************************************/
public class AuditLogPanelViewModel : BasePanelViewModel
{
    private readonly IAccountApiService _api;

    public AuditLogPanelViewModel(IEventAggregator eventAggregator, ILogService log, IAccountApiService api)
        : base(eventAggregator, log) => _api = api;

    /// <summary>감사 로그 목록(최신 100건). DataGrid ItemsSource.</summary>
    public ObservableCollection<AuditLogDto> Items { get; } = new();

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
            var res = await _api.GetAuditLogsAsync(1, 100, ct);   // ConfigureAwait 미사용 → UI 스레드 복귀
            Items.Clear();
            if (res.Success && res.Data is not null)
                foreach (var d in res.Data) Items.Add(d);
            else
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "감사 로그", Explain = $"불러오기 실패: {res.Error?.Message ?? res.Message}" });
        }
        catch (Exception ex) { _log?.Error($"[AuditLog] 로드 실패: {ex.Message}"); }
    }
}
