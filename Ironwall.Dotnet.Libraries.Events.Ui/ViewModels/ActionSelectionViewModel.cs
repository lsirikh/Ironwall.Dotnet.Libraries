using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

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
                .Where(entity => entity.Id == ret.Id)
                .Where(entity => entity.EventGroup == ret.EventGroup).FirstOrDefault();
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
    public IExEventModel? OriginEvent { get; set; }
    public DateTime? DateTime { get; set; }
    public ActionEventPanelViewModel PanelViewModel { get; }
    public IEnumerable<IExEventModel> EventProvider => PanelViewModel.EventProvider.OfType<IExEventModel>();
    #endregion
    #region - Attributes -
    private IList<ActionEventViewModel> _selection;
    #endregion
}