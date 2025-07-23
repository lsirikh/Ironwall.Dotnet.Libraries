using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 8:34:47 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ConnectionSelectionViewModel : BasePanelViewModel
{
    #region - Ctors -
    public ConnectionSelectionViewModel(IList<ConnectionEventViewModel> selection)
    {
        PanelViewModel = IoC.Get<ConnectionEventPanelViewModel>();
        DeviceProvider = IoC.Get<DeviceProvider>();
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
            item.EventGroup = EventGroup ?? item.EventGroup;
            item.Device = Device ?? item.Device;
            item.Status = Status ?? item.Status;
            item.DateTime = DateTime ?? item.DateTime;
        }
    }

    /* 공통값 계산 헬퍼 */
    //int 형 및 Enum 타입의 형식 비교
    private static T? CommonOrNullValue<T>(IEnumerable<ConnectionEventViewModel> list, Func<IConnectionEventModel, T> selector) where T : struct
    {
        try
        {
            if (list == null || !list.Any()) return null;

            var firstModel = list.FirstOrDefault()?.Model as IConnectionEventModel;
            if (firstModel == null) return null;

            T firstValue = selector(firstModel);

            bool allSame = list
                .Select(vm => vm.Model as IConnectionEventModel)
                .Where(m => m != null)
                .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue));

            return allSame ? firstValue : (T?)null;
        }
        catch (Exception)
        {

            throw;
        }

    }

    private static T? CommonOrNullString<T>(IEnumerable<ConnectionEventViewModel> list, Func<IConnectionEventModel, T> selector) where T : class?
    {
        try
        {
            if (!list.Any()) return null;

            var models = list.Select(x => x.Model as IConnectionEventModel).ToList();
            var firstModel = list.FirstOrDefault()?.Model as IConnectionEventModel;
            if (firstModel == null) return null;
            T firstValue = selector(firstModel);

            return models.All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue)) ? firstValue : null;
        }
        catch (Exception)
        {
            throw;
        }

    }

    private static IBaseDeviceModel? CommonOrNullReference(IEnumerable<ConnectionEventViewModel> list, DeviceProvider devices, ILogService? log)
    {
        if (!list.Any()) return null;

        var first = list.First()?.Device;
        if (first == null) return null;

        var ret = list
            .Where(m => m?.Device != null)
            .All(m => ReferenceEquals(m!.Device, first))
        ? first
        : null;

        if (ret == null)
            return null;
        else
            return devices.Where(entity => entity.Id == ret.Id)
                .Where(entity => entity.DeviceName == ret.DeviceName).FirstOrDefault();
    }

    public void RefreshAll()
    {
        MessageType = CommonOrNullValue(_selection, m => m.MessageType);
        EventGroup = CommonOrNullString(_selection, m => m.EventGroup);
        Device = CommonOrNullReference(_selection, DeviceProvider, _log);
        Status = CommonOrNullValue(_selection, m => m.Status);
        DateTime = CommonOrNullValue(_selection, m => m.DateTime);
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public EnumEventType? MessageType { get; set; }
    public string? EventGroup { get; set; }
    public IBaseDeviceModel? Device { get; set; }
    public EnumTrueFalse? Status { get; set; }
    public DateTime? DateTime { get; set; }
    public ConnectionEventPanelViewModel PanelViewModel { get; }
    public DeviceProvider DeviceProvider { get; }
    #endregion
    #region - Attributes -
    private readonly IList<ConnectionEventViewModel> _selection;
    #endregion
}