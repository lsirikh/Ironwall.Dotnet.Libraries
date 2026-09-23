using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
/****************************************************************************
   Purpose      : 구역(PidsGroup) 심볼 더블클릭 → 최선착 이벤트 조치보고 패널 오픈.
                  GMap_PidsGroup_DoubleClick_ActionReport PRD v1.2
   Created By   : GHLee
   Created On   : 2026-08-03
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 구역 심볼 더블클릭 → 조치보고 패널 오픈 오케스트레이터.
/// <para>대상 선정의 SSOT는 <see cref="IEventQueueManager"/>(활성 엔트리)다. 심볼의 표시 상태가 아니라
/// EQM 실조회 결과로 판단하므로, 표시-실제 불일치 시에도 없는 이벤트를 열지 않는다.</para>
/// <para>의존은 전부 IoC lazy 해석 — 헤드리스/테스트/부팅 전에는 안전하게 no-op 한다.</para>
/// </summary>
public class GroupActionReportLauncher : IGroupActionReportLauncher
{
    #region - Ctors -
    public GroupActionReportLauncher(ILogService? log = null)
    {
        _log = log;
    }
    #endregion

    #region - IGroupActionReportLauncher -
    /// <inheritdoc/>
    public bool TryOpenForGroup(IPidsGroupEditableMarker marker)
    {
        try
        {
            var groupId = marker.LinkedDeviceGroup;
            var title = string.IsNullOrWhiteSpace(marker.Title) ? $"그룹 {groupId}" : marker.Title;

            // FR-02 — 장비그룹 미연결 구역(LinkedDeviceGroup=0)은 이벤트를 귀속시킬 수 없다.
            if (groupId <= 0)
            {
                _log?.Info($"[GroupActionReport] 장비그룹 미연결 구역 — 스킵: {title}");
                Notify("조치보고 불가", "이 구역에 연결된 장비그룹이 없습니다. 구역 속성에서 장비그룹을 먼저 연결하세요.");
                return false;
            }

            // FR-07 — 조치보고 권한. 서버 POST /events/actions 는 events:edit 를 요구한다(6.3.2 · 8.0.2 동일) —
            // 이벤트 창과 같은 판정(ActionReportRules)을 쓴다. 서버 RBAC이 권위이고 여기는 보조 게이트.
            if (!CanReportEvents())
            {
                _log?.Info($"[GroupActionReport] 권한 없음 — 스킵: {title}(group={groupId})");
                Notify("권한 없음", "조치보고 권한이 없습니다.");
                return false;
            }

            // FR-08 — 이미 조치보고 창이 떠 있으면 재진입 차단(중복 다이얼로그 금지, 연속 더블클릭 멱등).
            if (IsReportDialogOpen())
            {
                _log?.Info($"[GroupActionReport] 조치보고 창이 이미 열려 있음 — 스킵: {title}");
                return false;
            }

            var eqm = Resolve<IEventQueueManager>();
            if (eqm == null)
            {
                _log?.Warning("[GroupActionReport] EventQueueManager 미해석 — 스킵");
                return false;
            }

            // FR-03 — 그룹 활성 엔트리 스냅샷. 심볼이 깜빡여도 EQM에 없으면 조치 대상이 없다(표시-실제 불일치 방어).
            var entries = eqm.GetEntriesByGroup(groupId);
            if (entries.Count == 0)
            {
                _log?.Info($"[GroupActionReport] 활성 이벤트 없음 — 스킵: {title}(group={groupId})");
                Notify("조치보고 불가", "이 구역에 조치할 이벤트가 없습니다.");
                return false;
            }

            // FR-03/FR-04 — 엔트리별 원본 모델을 해석한 뒤 '가장 먼저 발생한' 순서로 정렬.
            //   1순위 서버 발생시각(model.DateTime)  — 현장에서 실제로 먼저 일어난 것
            //   2순위 EnqueuedAt(클라 수신시각)      — 모델 미해석 엔트리의 대체 키
            //   3순위 EventId                        — 완전 동시각 타이브레이크
            var candidates = entries
                .Select(ResolveCandidate)
                .OrderBy(c => c.OccurredAt)
                .ThenBy(c => c.Entry.EnqueuedAt)
                .ThenBy(c => c.Entry.EventId)
                .ToList();

            var target = candidates.FirstOrDefault(c => c.Card != null || c.Model != null);
            if (target == null)
            {
                _log?.Warning($"[GroupActionReport] 이벤트 모델 미해석 — 스킵: {title}(group={groupId}, 후보={candidates.Count})");
                Notify("조치보고 불가", "이벤트 정보를 찾을 수 없습니다. 이벤트 목록에서 조치보고해 주세요.");
                return false;
            }

            _log?.Info($"[GroupActionReport] 최선착 선정: {title}(group={groupId}) → "
                     + $"Event({target.Entry.EventId}, {target.Entry.EventType}) 발생={target.OccurredAt:yyyy-MM-dd HH:mm:ss} "
                     + $"(후보 {candidates.Count}건, 카드={(target.Card != null ? "재사용" : "임시생성")})");

            return OpenReportDialog(target);
        }
        catch (Exception ex)
        {
            _log?.Error($"[GroupActionReport] 처리 실패: {ex.Message}");
            return false;
        }
    }
    #endregion

    #region - Processes -
    /// <summary>엔트리 → (활성 카드 / 원본 모델 / 정렬 기준시각) 해석. 둘 다 못 찾으면 Card·Model 모두 null.</summary>
    private Candidate ResolveCandidate(EventEntry entry)
    {
        var cardList = Resolve<EventCardListPanelViewModel>();
        var eventProvider = Resolve<Ironwall.Dotnet.Libraries.Events.Providers.EventProvider>();

        if (entry.EventType == EnumEventType.Fault)
        {
            // 타입 우선 필터(OfType) 필수 — 탐지/장애는 독립 id 시퀀스라 숫자 Id가 충돌한다.
            var card = cardList?.ViewModelProvider
                                .OfType<MalfunctionEventCardViewModel>()
                                .FirstOrDefault(c => c.Model?.Id == entry.EventId);
            var model = (card?.Model as IMalfunctionEventModel)
                        ?? eventProvider?.OfType<IMalfunctionEventModel>().FirstOrDefault(e => e.Id == entry.EventId);
            return new Candidate(entry, card, model, model?.DateTime ?? entry.EnqueuedAt);
        }
        else
        {
            var card = cardList?.ViewModelProvider
                                .OfType<DetectionEventCardViewModel>()
                                .FirstOrDefault(c => c.Model?.Id == entry.EventId);
            var model = (card?.Model as IDetectionEventModel)
                        ?? eventProvider?.OfType<IDetectionEventModel>().FirstOrDefault(e => e.Id == entry.EventId);
            return new Candidate(entry, card, model, model?.DateTime ?? entry.EnqueuedAt);
        }
    }

    /// <summary>FR-05 — 타입별 조치보고 다이얼로그에 대상 카드를 실어 열기. 카드가 없으면 모델로 임시 카드 VM 구성.</summary>
    private bool OpenReportDialog(Candidate target)
    {
        var ea = Resolve<IEventAggregator>();
        var account = Resolve<IAccountModel>();
        if (ea == null)
        {
            _log?.Warning("[GroupActionReport] EventAggregator 미해석 — 스킵");
            return false;
        }

        if (target.Entry.EventType == EnumEventType.Fault)
        {
            var card = target.Card as MalfunctionEventCardViewModel;
            if (card == null && target.Model is IMalfunctionEventModel m)
                card = new MalfunctionEventCardViewModel(ea, _log!, m);   // 그리드 우클릭 조치보고와 동일 패턴
            if (card == null) return false;

            Resolve<MalfunctionReportDialogViewModel>()?.UpdateData(card, account!);
            ea.PublishOnCurrentThreadAsync(new OpenEventReportDialogMessageModel { EventType = "MALFUNCTION" });
        }
        else
        {
            var card = target.Card as DetectionEventCardViewModel;
            if (card == null && target.Model is IDetectionEventModel d)
                card = new DetectionEventCardViewModel(ea, _log!, d);
            if (card == null) return false;

            Resolve<DetectionReportDialogViewModel>()?.UpdateData(card, account!);
            ea.PublishOnCurrentThreadAsync(new OpenEventReportDialogMessageModel { EventType = "DETECTION" });
        }

        _log?.Info($"[GroupActionReport] 조치보고 창 오픈: Event({target.Entry.EventId}, {target.Entry.EventType})");
        return true;
    }

    /// <summary>조치보고 다이얼로그(탐지/장애) 중 하나라도 활성이면 true. Caliburn Screen의 IsActive 기준.</summary>
    private bool IsReportDialogOpen()
        => Resolve<DetectionReportDialogViewModel>()?.IsActive == true
        || Resolve<MalfunctionReportDialogViewModel>()?.IsActive == true;

    /// <summary>조치보고 권한(events:edit — ActionReportTemplate 이 아닌 조치 생성). PermissionService 미해석(오프라인/테스트)이면 전체허용 폴백.</summary>
    private bool CanReportEvents()
    {
        try { return Ironwall.Dotnet.Libraries.Events.Ui.Helpers.ActionReportRules.CanReport(Resolve<IPermissionService>()); }
        catch { return true; }
    }

    /// <summary>표준 안내 팝업(raw MessageBox 금지 — EventAggregator 경유).</summary>
    private void Notify(string title, string explain)
    {
        var ea = Resolve<IEventAggregator>();
        if (ea == null) return;
        ea.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel { Title = title, Explain = explain });
    }

    /// <summary>IoC lazy 해석 — 미등록/부팅 전이면 null(안전 실패).</summary>
    private static T? Resolve<T>() where T : class
    {
        try { return IoC.Get<T>(); }
        catch { return null; }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;

    /// <summary>선정 후보 — 엔트리와 그 원본(활성 카드 또는 모델), 정렬 기준시각.</summary>
    private sealed record Candidate(
        EventEntry Entry,
        object? Card,
        IExEventModel? Model,
        DateTime OccurredAt);
    #endregion
}
