using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/23/2025 9:48:13 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class PidsGroupSymbolModel : LineSymbolModel, IPidsGroupSymbolModel
{
    #region - Ctors -
    public PidsGroupSymbolModel()
    {
        Width = 60;
        Height = 60;
        ShowShape = true;
        ShowTitle = false;
        StrokeColor = EnumColorType.Lime;
        FillColor = EnumColorType.Transparent;
        Category = EnumMarkerCategory.AREA_BOUNDARY;
        OperationState = EnumOperationState.NONE;
        LinePattern = EnumLinePattern.Solid;
        TitleSize = 10;
        Title = "New Group";
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
    [JsonProperty("device_group", Order = 20)]
    public int LinkedDeviceGroup { get; set; }

    /// <summary>3D 철망 기둥 간격(m) — NULL 이면 전역 설정(Symbol3D.FencePostSpacingM, 기본 3.0)을 따른다(FR-03).</summary>
    [JsonProperty("post_spacing_m", Order = 40)]
    public double? PostSpacingM { get; set; }

    /// <summary>3D 철망 높이(m) — NULL 이면 전역 설정(Symbol3D.FenceHeightM, 기본 2.4).</summary>
    [JsonProperty("fence_height_m", Order = 41)]
    public double? FenceHeightM { get; set; }

    /// <summary>철망 형태 — 기둥 간격(Posts) / 센서 장착(SensorMount)(FR-04).</summary>
    [JsonProperty("fence_mode", Order = 42)]
    public EnumFenceMode FenceMode { get; set; } = EnumFenceMode.Posts;

    /// <summary>그룹별 3D 렌더 on/off — 전역 Symbol3D 플래그·LOD 와 AND 로 결합한다(FR-06).</summary>
    [JsonProperty("render_3d", Order = 43)]
    public bool Render3D { get; set; } = true;

    /// <summary>센서 장착 모드에서 노드 번호를 역순으로 매긴다(FR-04).</summary>
    [JsonProperty("reverse_sensor_order", Order = 44)]
    public bool ReverseSensorOrder { get; set; }

    /// <summary>탐지 중인 센서 노드의 장비 Id 집합 — 런타임 전용(Phase 2 배선), 직렬화·영속 제외.</summary>
    [JsonIgnore]
    public IReadOnlySet<int>? ActiveSensorDeviceIds { get; set; }

    [JsonProperty("event_status", Order = 21)]
    public EnumEventStatus EventStatus { get; set; } = EnumEventStatus.Normal;

    private EnumCompositeEventStatus _compositeStatus = EnumCompositeEventStatus.Normal;
    public EnumCompositeEventStatus CompositeStatus
    {
        get => _compositeStatus;
        set
        {
            _compositeStatus = value;
            EventStatus = value switch
            {
                EnumCompositeEventStatus.Detecting        => EnumEventStatus.Detecting,
                EnumCompositeEventStatus.Faulted          => EnumEventStatus.Fault,
                EnumCompositeEventStatus.FaultedDetecting => EnumEventStatus.Fault,
                EnumCompositeEventStatus.Connection       => EnumEventStatus.Connection,
                EnumCompositeEventStatus.Blackout         => EnumEventStatus.Blackout,   // 제어기 무통신(검은색)
                _                                         => EnumEventStatus.Normal,
            };
        }
    }

    public event EventHandler Update;

    public void SetUpdate()
    {
        Update?.Invoke(this, EventArgs.Empty);
    }
    #endregion
    #region - Attributes -
    #endregion
}