using Ironwall.Dotnet.Libraries.Enums;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
public interface IPidsGroupSymbolModel : IPidsEventCapable, ILineSymbolModel
{
    int LinkedDeviceGroup { get; set; }

    /// <summary>3D 철망 기둥 간격(m), NULL=전역 설정(FR-03)</summary>
    double? PostSpacingM { get; set; }
    /// <summary>3D 철망 높이(m), NULL=전역 설정</summary>
    double? FenceHeightM { get; set; }
    /// <summary>철망 형태(Posts/SensorMount)(FR-04)</summary>
    EnumFenceMode FenceMode { get; set; }
    /// <summary>그룹별 3D 렌더 on/off(FR-06)</summary>
    bool Render3D { get; set; }
    /// <summary>센서 노드 역순 번호(FR-04)</summary>
    bool ReverseSensorOrder { get; set; }
    /// <summary>탐지 중 센서 노드 장비 Id — 런타임 전용, 비영속</summary>
    IReadOnlySet<int>? ActiveSensorDeviceIds { get; set; }

    event EventHandler Update;
}