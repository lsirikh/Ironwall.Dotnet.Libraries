using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Linq;

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

    public string DeviceGroupsText
    {
        get
        {
            if (DeviceGroups == null || DeviceGroups.Count == 0) return "";
            try
            {
                var provider = IoC.Get<DeviceGroupProvider>();
                return string.Join(", ", DeviceGroups.Select(id =>
                    provider.OfType<DeviceGroupModel>().FirstOrDefault(g => g.Id == id)?.Name ?? id.ToString()));
            }
            catch { return string.Join(", ", DeviceGroups); }
        }
    }

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

    /// <summary>판별자(<c>category_device</c>) — 읽기 전용. 경로가 정하고 바뀌지 않는다.</summary>
    public EnumDeviceCategory CategoryDevice => _model.CategoryDevice;

    /// <summary>
    /// 종류축(<c>type_&lt;category&gt;</c>)의 서버 코드 — v7.0+ 축 화면의 콤보가 이 값을 바인딩한다(device-console-v8 FR-09).
    /// </summary>
    /// <remarks>
    /// <para><b>빈 값은 받지 않는다.</b> WPF <c>ComboBox</c> 는 <c>ItemsSource</c> 가 바뀌어 현재 값이 목록에 없으면
    /// <c>SelectedValue=null</c> 을 소스에 되쓴다 — 카탈로그 재조회 한 번에 모든 행의 종류축이 지워진다(ISSUE-25).
    /// 콤보로는 어차피 "비우기"를 할 수 없으므로 빈 값 대입을 무시해도 잃는 기능이 없다.</para>
    /// <para><b>클라 enum 을 함께 맞춘다.</b> 쓰기 매핑은 enum 이 표현할 수 있는 값이면 enum 을 쓴다
    /// (<c>DeviceAxesMapper.TypeAxisForWrite</c>). 여기서 enum 을 옛 값으로 두면 콤보 편집이 저장에서 사라진다.
    /// enum 이 표현하지 못하는 값(<c>SPEED_DOME</c>)은 enum 을 건드리지 않고 원값만 보존한다.</para>
    /// </remarks>
    public string? TypeAxisCode
    {
        get { return _model.TypeAxisCode; }
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var code = value.Trim();
            if (string.Equals(_model.TypeAxisCode, code, StringComparison.Ordinal)) return;

            _model.TypeAxisCode = code;
            SyncClientEnum(code);
            NotifyOfPropertyChange(() => TypeAxisCode);
            NotifyOfPropertyChange(nameof(TypeAxisDisplay));
            NotifyOfPropertyChange(() => DeviceType);
        }
    }

    /// <summary>Grid text for the type axis: "Label (Code)", "미대응: X" for values the server catalog does not know, "선택 필요" when required and empty.</summary>
    public string TypeAxisDisplay => Ironwall.Dotnet.Libraries.Devices.Ui.Helpers.TypeAxisPanelSupport.DescribeFor(_model);

    private void SyncClientEnum(string code)
    {
        if (_model is ICameraDeviceModel camera)
        {
            if (Enum.TryParse<EnumCameraType>(code, ignoreCase: true, out var cameraType) && Enum.IsDefined(cameraType))
            {
                camera.Category = cameraType;
                NotifyOfPropertyChange("Category");
            }
            return;
        }

        // 제어기·센서는 옛 DeviceType 이 곧 종류축이다. 형상 4축(스피커·함체·경광등·통문)은 DeviceType 과 무관 — 건드리지 않는다.
        if (_model is IControllerDeviceModel or ISensorDeviceModel
            && Enum.TryParse<EnumDeviceType>(code, ignoreCase: true, out var deviceType)
            && Enum.IsDefined(deviceType) && deviceType != EnumDeviceType.NONE)
        {
            _model.DeviceType = deviceType;
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

    /// <summary>설치 방위각 0~360° (model.Heading, v4.4). 미설정 시 null. set 시 mod360 정규화(서버 0~360 검증 대응).</summary>
    public double? Bearing
    {
        get { return _model.Heading; }
        set
        {
            _model.Heading = value.HasValue ? ((value.Value % 360) + 360) % 360 : (double?)null;
            NotifyOfPropertyChange(() => Bearing);
        }
    }

    /// <summary>설치 고도(m, model.Altitude). optional, clamp 없음.</summary>
    public double? Altitude
    {
        get { return _model.Altitude; }
        set
        {
            _model.Altitude = value;
            NotifyOfPropertyChange(() => Altitude);
        }
    }
    #endregion
    #region - Attributes -
    private int _index;
    #endregion
}
