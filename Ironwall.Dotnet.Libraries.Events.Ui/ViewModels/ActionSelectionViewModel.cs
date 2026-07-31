using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 8:35:47 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ActionSelectionViewModel : BasePanelViewModel
{
    #region - Ctors -
    public ActionSelectionViewModel(IList<ActionEventViewModel> selection)
    {
        PanelViewModel = IoC.Get<ActionEventPanelViewModel>();
        _selection = selection;
        RefreshAll();
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    public void ApplyButton()
    {
        foreach (var item in _selection)
        {
            item.MessageType = MessageType ?? item.MessageType;
            item.User = User ?? item.User;
            item.Content = Content ?? item.Content;
            item.OriginEvent = OriginEvent ?? item.OriginEvent;
            item.DateTime = DateTime ?? item.DateTime;
        }
    }

    /* 공통값 계산 헬퍼 */
    //int 형 및 Enum 타입의 형식 비교
    private static T? CommonOrNullValue<T>(IEnumerable<ActionEventViewModel> list, Func<IActionEventModel, T> selector) where T : struct
    {
        try
        {
            if (list == null || !list.Any()) return null;

            var firstModel = list.FirstOrDefault()?.Model;
            if (firstModel == null) return null;

            T firstValue = selector(firstModel);

            bool allSame = list
                .Select(vm => vm.Model)
                .Where(m => m != null)
                .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue));

            return allSame ? firstValue : (T?)null;
        }
        catch (Exception)
        {

            throw;
        }
    }

    private static T? CommonOrNullString<T>(IEnumerable<ActionEventViewModel> list, Func<IActionEventModel, T> selector) where T : class?
    {
        try
        {
            if (!list.Any()) return null;

            var models = list.Select(x => x.Model).ToList();
            var firstModel = list.FirstOrDefault()?.Model;
            if (firstModel == null) return null;
            T firstValue = selector(firstModel);

            return models.All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue)) ? firstValue : null;
        }
        catch (Exception)
        {
            throw;
        }
    }

    private static IExEventModel? CommonOrNullReference(IEnumerable<ActionEventViewModel> list, IEnumerable<IExEventModel> events, ILogService? log)
    {
        if (!list.Any()) return null;

        var first = list.First()?.OriginEvent;
        if (first == null) return null;

        var ret = list
            .Where(m => m?.OriginEvent != null)
            .All(m => ReferenceEquals(m!.OriginEvent, first))
        ? first
        : null;

        if (ret == null)
            return null;
        else
            return events
                .Where(entity => entity.Id == ret.Id).FirstOrDefault();
    }

    public void RefreshAll()
    {
        MessageType = CommonOrNullValue(_selection, m => m.MessageType);
        User = CommonOrNullString(_selection, m => m.User);
        Content = CommonOrNullString(_selection, m => m.Content);
        OriginEvent = CommonOrNullReference(_selection, EventProvider, _log);
        DateTime = CommonOrNullValue(_selection, m => m.DateTime);
        NotifyOfPropertyChange(() => EventProvider);
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public EnumEventType? MessageType { get; set; }
    public string? User { get; set; }
    public string? Content { get; set; }

    private IExEventModel? _originEvent;
    /// <summary>선택 항목의 (공통) 원본 이벤트. 변경 시 원본 필수정보·썸네일 파생 표시를 갱신.</summary>
    public IExEventModel? OriginEvent
    {
        get => _originEvent;
        set
        {
            _originEvent = value;
            NotifyOfPropertyChange(nameof(OriginEvent));
            NotifyOfPropertyChange(nameof(HasOrigin));
            NotifyOfPropertyChange(nameof(IsDetectionOrigin));
            NotifyOfPropertyChange(nameof(IsMalfunctionOrigin));
            NotifyOfPropertyChange(nameof(OriginKindText));
            NotifyOfPropertyChange(nameof(OriginZoneText));
            NotifyOfPropertyChange(nameof(OriginDeviceText));
            NotifyOfPropertyChange(nameof(OriginNumberText));
            NotifyOfPropertyChange(nameof(OriginDateTimeText));
            NotifyOfPropertyChange(nameof(OriginResultText));
            NotifyOfPropertyChange(nameof(OriginSignalText));
            NotifyOfPropertyChange(nameof(OriginReasonText));
            NotifyOfPropertyChange(nameof(HasOriginThumbnail));
            NotifyOfPropertyChange(nameof(OriginThumbnail));
        }
    }

    public DateTime? DateTime { get; set; }
    public ActionEventPanelViewModel PanelViewModel { get; }
    public IEnumerable<IExEventModel> EventProvider => PanelViewModel.EventProvider.OfType<IExEventModel>();

    // ── 원본 이벤트(OriginEvent) 필수정보 + 썸네일 — 조치보고 origin 맥락. [Action_Report_Origin_Thumbnail] ──
    // 단일/공통 origin일 때만 OriginEvent 비-null(다중·상이면 CommonOrNullReference가 null → "—"/Default).
    public bool HasOrigin => OriginEvent != null;
    public bool IsDetectionOrigin => OriginEvent is IDetectionEventModel;
    public bool IsMalfunctionOrigin => OriginEvent is IMalfunctionEventModel;

    /// <summary>원본 종류 — 탐지/장애.</summary>
    public string OriginKindText => OriginEvent switch
    {
        IDetectionEventModel => "탐지 이벤트",
        IMalfunctionEventModel => "장애 이벤트",
        null => "—",
        _ => OriginEvent!.MessageType.ToString()
    };

    /// <summary>원본 장비 소속 구역(그룹) 이름 — DeviceGroups(Id)→DeviceGroupProvider 변환(DetectionSelection 패턴 미러).</summary>
    public string OriginZoneText
    {
        get
        {
            var groups = OriginEvent?.Device?.DeviceGroups;
            if (groups == null || groups.Count == 0) return "—";
            try
            {
                var provider = IoC.Get<DeviceGroupProvider>();
                return string.Join(", ", groups.Select(id =>
                    provider.OfType<DeviceGroupModel>().FirstOrDefault(g => g.Id == id)?.Name ?? id.ToString()));
            }
            catch { return string.Join(", ", groups); }
        }
    }

    public string OriginDeviceText => OriginEvent?.Device?.DeviceName is string n && n.Length > 0 ? n : "—";
    public string OriginNumberText => OriginEvent?.Device is { } d ? d.DeviceNumber.ToString() : "—";
    public string OriginDateTimeText => OriginEvent is { } e ? e.DateTime.ToString("yyyy-MM-dd HH:mm:ss") : "—";

    /// <summary>탐지 origin 결과(Result). 장애/부재면 "—".</summary>
    public string OriginResultText => (OriginEvent as IDetectionEventModel)?.Result.ToString() ?? "—";
    /// <summary>탐지 origin 신호(signal). 0/부재/장애면 "—".</summary>
    public string OriginSignalText => (OriginEvent as IDetectionEventModel)?.Signal is int s and > 0 ? s.ToString("N0") : "—";
    /// <summary>장애 origin 사유(Reason). 탐지/부재면 "—".</summary>
    public string OriginReasonText => (OriginEvent as IMalfunctionEventModel)?.Reason.ToString() ?? "—";

    // 썸네일: origin=탐지만. 장애=null → Default. host-rebase 리졸버 + OnLoad 캐시 재사용.
    public Uri? OriginThumbnailUri => ThumbnailUriResolver.Resolve((OriginEvent as IDetectionEventModel)?.Thumbnail);
    public bool HasOriginThumbnail => OriginThumbnailUri != null;
    /// <summary>썸네일 이미지 — 1회 다운로드→OnLoad 캐시(재열기/탭 전환에도 유지). 로드 전/장애/실패면 null→Default.</summary>
    public ImageSource? OriginThumbnail
    {
        get
        {
            var uri = OriginThumbnailUri;
            return uri is null ? null : ThumbnailImageLoader.GetOrLoad(uri, () => NotifyOfPropertyChange(nameof(OriginThumbnail)));
        }
    }
    #endregion
    #region - Attributes -
    private IList<ActionEventViewModel> _selection;
    #endregion
}