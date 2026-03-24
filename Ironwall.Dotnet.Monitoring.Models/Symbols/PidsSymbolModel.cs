using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/26/2025 10:39:19 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class PidsSymbolModel : SymbolModel, IPidsSymbolModel
{
    #region - Ctors -
    public PidsSymbolModel()
    {
        Category = EnumMarkerCategory.PIDS_EQUIPMENT;
    }

    public PidsSymbolModel(string title, double latitude, double longitude, double zoom,
        EnumDeviceType deviceType = EnumDeviceType.Fence) : base(title, latitude, longitude, zoom)
    {
        Category = EnumMarkerCategory.PIDS_EQUIPMENT;
        DeviceType = deviceType;
    }
    
    #endregion
    #region - Properties -
    /// <summary>
    /// 연결된 디바이스 ID (JSON 직렬화용, 하위 호환성 유지)
    /// </summary>
    [JsonProperty("device_id", Order = 20)]
    public int LinkedDeviceId { get; set; }

    /// <summary>
    /// 연결된 디바이스 객체 (런타임 바인딩용)
    /// <para>설정 시 LinkedDeviceId가 자동 동기화됩니다.</para>
    /// </summary>
    private IBaseDeviceModel? _linkedDevice;

    [JsonIgnore]  // JSON 직렬화 제외 - 런타임 전용
    public IBaseDeviceModel? LinkedDevice
    {
        get => _linkedDevice;
        set
        {
            _linkedDevice = value;
            // LinkedDeviceId 자동 동기화 (하위 호환성)
            LinkedDeviceId = value?.Id ?? 0;
        }
    }

    [JsonProperty("device_type", Order = 21)]
    public EnumDeviceType DeviceType { get; set; }

    [JsonProperty("event_status", Order = 22)]
    public EnumEventStatus EventStatus { get; set; } = EnumEventStatus.Normal;

    [JsonProperty("show_fov", Order = 23)]
    public bool ShowFOV { get; set; } = false;

    [JsonProperty("fov_color", Order = 24)]
    public EnumColorType FOVColor { get; set; } = EnumColorType.Red;

    [JsonProperty("fov_opacity", Order = 25)]
    public double FOVOpacity { get; set; } = 0.3;

    // 모든 PIDS 장비에 공통으로 사용되는 FOV 속성
    [JsonProperty("detection_range", Order = 26)]
    public double DetectionRange { get; set; } = 30.0; // 미터

    [JsonProperty("detection_angle", Order = 27)]
    public double DetectionAngle { get; set; } = 80; // 도

    [JsonProperty("detection_bearing", Order = 28)]
    public double DetectionBearing { get; set; } = 0; // 도 (북쪽 기준)

    /// <summary>
    /// 기준 방향 각도 (카메라 물리적 설치 방향)
    /// <para>0.0 ~ 360.0 (정북 기준 시계방향 각도)</para>
    /// </summary>
    [JsonProperty("base_bearing", Order = 29)]
    public double BaseBearing { get; set; } = 0.0; // 도 (북쪽 기준, 고정값)

    public event EventHandler Update;

    public void SetUpdate()
    {
        Update?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// LinkedDeviceId를 기반으로 디바이스 목록에서 LinkedDevice를 바인딩합니다.
    /// <para>Legacy JSON 로드 시 마이그레이션에 사용됩니다.</para>
    /// </summary>
    /// <param name="deviceList">검색할 디바이스 목록</param>
    public void BindToDeviceList(IEnumerable<IBaseDeviceModel> deviceList)
    {
        if (LinkedDeviceId == 0 || deviceList == null)
        {
            // ID가 0이면 바인딩하지 않음 (LinkedDevice는 null 유지)
            return;
        }

        // LinkedDeviceId와 일치하는 디바이스 찾기
        var matchingDevice = deviceList.FirstOrDefault(d => d.Id == LinkedDeviceId);

        // 일치하는 디바이스가 있으면 바인딩 (없으면 null 유지)
        // 주의: LinkedDevice setter가 LinkedDeviceId를 자동 동기화하므로,
        //       _linkedDevice를 직접 설정하여 ID 덮어쓰기 방지
        _linkedDevice = matchingDevice;
    }

    #endregion
}