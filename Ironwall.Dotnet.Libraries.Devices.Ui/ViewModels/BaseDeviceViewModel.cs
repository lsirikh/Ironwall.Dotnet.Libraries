using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/27/2025 8:05:30 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class BaseDeviceViewModel<T> : BaseCustomViewModel<T>
                                        , IBaseDeviceViewModel<T> where T : IBaseDeviceModel
{
    #region - Ctors -
    protected BaseDeviceViewModel(T model) : base(model)
    {
    }
    protected BaseDeviceViewModel(T model, IEventAggregator ea, ILogService log)
        : base(model, ea, log)
    {
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public int Index
    {
        get { return _index; }
        set { _index = value; NotifyOfPropertyChange(() => Index); }
    }

    public List<int>? DeviceGroups
    {
        get { return _model.DeviceGroups; }
        set
        {
            _model.DeviceGroups = value;
            NotifyOfPropertyChange(() => DeviceGroups);
            NotifyOfPropertyChange(() => DeviceGroupsText);
        }
    }

    public string DeviceGroupsText =>
        DeviceGroups != null && DeviceGroups.Count > 0
            ? string.Join(", ", DeviceGroups)
            : "";

    public int DeviceNumber
    {
        get { return _model.DeviceNumber; }
        set
        {
            _model.DeviceNumber = value;
            NotifyOfPropertyChange(() => DeviceNumber);
        }
    }

    public string? DeviceName
    {
        get { return _model.DeviceName; }
        set
        {
            _model.DeviceName = value;
            NotifyOfPropertyChange(() => DeviceName);
        }
    }

    public EnumDeviceType DeviceType
    {
        get { return _model.DeviceType; }
        set
        {
            _model.DeviceType = value;
            NotifyOfPropertyChange(() => DeviceType);
        }
    }


    public string? Version
    {
        get { return _model.Version; }
        set
        {
            _model.Version = value;
            NotifyOfPropertyChange(() => Version);
        }
    }

    public EnumDeviceStatus Status
    {
        get { return _model.Status; }
        set
        {
            _model.Status = value;
            NotifyOfPropertyChange(() => Status);
        }
    }

    public string? Location
    {
        get { return _model.Location; }
        set
        {
            _model.Location = value;
            NotifyOfPropertyChange(() => Location);
        }
    }

    public double Latitude
    {
        get { return _model.Latitude; }
        set
        {
            _model.Latitude = Math.Clamp(value, -90.0, 90.0);
            NotifyOfPropertyChange(() => Latitude);
        }
    }

    public double Longitude
    {
        get { return _model.Longitude; }
        set
        {
            _model.Longitude = Math.Clamp(value, -180.0, 180.0);
            NotifyOfPropertyChange(() => Longitude);
        }
    }

    public bool IsEnable
    {
        get { return _model.IsEnable; }
        set
        {
            _model.IsEnable = value;
            NotifyOfPropertyChange(() => IsEnable);
        }
    }
    #endregion
    #region - Attributes -
    private int _index;
    #endregion
}
