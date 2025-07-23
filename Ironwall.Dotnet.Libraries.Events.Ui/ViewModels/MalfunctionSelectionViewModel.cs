using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 8:35:22 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class MalfunctionSelectionViewModel : BasePanelViewModel
{
    #region - Ctors -
    public MalfunctionSelectionViewModel(IList<MalfunctionEventViewModel> selection)
    {
        PanelViewModel = IoC.Get<MalfunctionEventPanelViewModel>();
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
            item.Reason = Reason ?? item.Reason;
            item.FirstStart = FirstStart ?? item.FirstStart;
            item.FirstEnd = FirstEnd ?? item.FirstEnd;
            item.SecondStart = SecondStart ?? item.SecondStart;
            item.SecondEnd = SecondEnd ?? item.SecondEnd;
            item.Status = Status ?? item.Status;
            item.DateTime = DateTime ?? item.DateTime;
        }
    }

    /* 공통값 계산 헬퍼 */
    //int 형 및 Enum 타입의 형식 비교
    private static T? CommonOrNullValue<T>(IEnumerable<MalfunctionEventViewModel> list, Func<IMalfunctionEventModel, T> selector) where T : struct
    {
        try
        {
            if (list == null || !list.Any()) return null;

            var firstModel = list.FirstOrDefault()?.Model as IMalfunctionEventModel;
            if (firstModel == null) return null;

            T firstValue = selector(firstModel);

            bool allSame = list
                .Select(vm => vm.Model as IMalfunctionEventModel)
                .Where(m => m != null)
                .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue));

            return allSame ? firstValue : (T?)null;
        }
        catch (Exception)
        {

            throw;
        }
    }

    private static T? CommonOrNullString<T>(IEnumerable<MalfunctionEventViewModel> list, Func<IMalfunctionEventModel, T> selector) where T : class?
    {
        try
        {
            if (!list.Any()) return null;

            var models = list.Select(x => x.Model as IMalfunctionEventModel).ToList();
            var firstModel = list.FirstOrDefault()?.Model as IMalfunctionEventModel;
            if (firstModel == null) return null;
            T firstValue = selector(firstModel);

            return models.All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue)) ? firstValue : null;
        }
        catch (Exception)
        {
            throw;
        }
    }

    private static IBaseDeviceModel? CommonOrNullReference(IEnumerable<MalfunctionEventViewModel> list, DeviceProvider devices, ILogService? log)
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
        Reason = CommonOrNullValue(_selection, m => m.Reason);
        FirstStart = CommonOrNullValue(_selection, m => m.FirstStart);
        FirstEnd = CommonOrNullValue(_selection, m => m.FirstEnd);
        SecondStart = CommonOrNullValue(_selection, m => m.SecondStart);
        SecondEnd = CommonOrNullValue(_selection, m => m.SecondEnd);
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
    public EnumFaultType? Reason { get; set; }
    public int? FirstStart { get; set; }
    public int? FirstEnd { get; set; }
    public int? SecondStart { get; set; }
    public int? SecondEnd { get; set; }
    public DateTime? DateTime { get; set; }
    public MalfunctionEventPanelViewModel PanelViewModel { get; }
    public DeviceProvider DeviceProvider { get; }
    #endregion
    #region - Attributes -
    private IList<MalfunctionEventViewModel> _selection;
    #endregion
}