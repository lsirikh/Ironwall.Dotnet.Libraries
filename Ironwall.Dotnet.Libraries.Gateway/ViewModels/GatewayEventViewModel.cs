using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.GatewayEvents;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Gateway.ViewModels;
/****************************************************************************
   Purpose      : Gateway 이벤트 ViewModel (DataGrid 행 래퍼)
   Created By   : GHLee
   Created On   : 10/29/2025 12:16:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class GatewayEventViewModel : BaseCustomViewModel<IGatewayEventModel>, IGatewayEventViewModel
{
    #region - Ctors -
    public GatewayEventViewModel(IGatewayEventModel model) : base(model)
    {
        _model = model;
    }

    public GatewayEventViewModel(IGatewayEventModel model, IEventAggregator eventAggregator, ILogService log)
        : base(model, eventAggregator, log)
    {
        _model = model;
    }
    #endregion

    #region - Overrides -
    public override void Dispose()
    {
        _model = new GatewayEventModel();
        GC.Collect();
    }
    #endregion

    #region - Properties -
    public int Index
    {
        get => _index;
        set
        {
            _index = value;
            NotifyOfPropertyChange(() => Index);
        }
    }

    public string EventName
    {
        get => _model.EventName;
        set
        {
            _model.EventName = value;
            NotifyOfPropertyChange(() => EventName);
        }
    }

    public List<int> DeviceGroups
    {
        get => _model.DeviceGroups;
        set
        {
            _model.DeviceGroups = value;
            NotifyOfPropertyChange(() => DeviceGroups);
            NotifyOfPropertyChange(() => GroupNames);
        }
    }

    public string GroupNames =>
        _model.DeviceGroups.Count == 0
            ? "미지정"
            : string.Join(", ", _model.DeviceGroups
                .Select(id => AllGroups?.FirstOrDefault(g => g.Id == id)?.Name ?? id.ToString()));

    public ObservableCollection<IDeviceGroupModel>? AllGroups { get; set; }

    public IDeviceGroupModel? SelectedGroup
    {
        get => AllGroups?.FirstOrDefault(g => g.Id == DeviceGroups.FirstOrDefault());
        set
        {
            var newList = new List<int>();
            if (value != null && value.Id > 0)
                newList.Add(value.Id);
            DeviceGroups = newList;
            NotifyOfPropertyChange();
        }
    }

    public bool IsEnable
    {
        get => _model.IsEnable;
        set
        {
            _model.IsEnable = value;
            NotifyOfPropertyChange(() => IsEnable);
        }
    }

    public string? Description
    {
        get => _model.Description;
        set
        {
            _model.Description = value;
            NotifyOfPropertyChange(() => Description);
        }
    }
    #endregion

    #region - Attributes -
    private int _index;
    #endregion
}

public interface IGatewayEventViewModel : IBaseCustomViewModel<IGatewayEventModel>
{
    int Index { get; set; }
    string EventName { get; set; }
    List<int> DeviceGroups { get; set; }
    string GroupNames { get; }
    IDeviceGroupModel? SelectedGroup { get; set; }
    bool IsEnable { get; set; }
    string? Description { get; set; }
}
