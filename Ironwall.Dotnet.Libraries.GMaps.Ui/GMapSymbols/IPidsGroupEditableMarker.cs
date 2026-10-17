using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols
{
    public interface IPidsGroupEditableMarker : ILineEditableMarker
    {
        /// <summary>
        /// 연결된 장비 그룹
        /// </summary>
        int LinkedDeviceGroup { get; set; }
        /// <summary>
        /// 이벤트 상태
        /// </summary>
        EnumEventStatus EventStatus { get; set; }

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
        /// <summary>탐지 중 센서 노드 장비 Id — 런타임 전용</summary>
        IReadOnlySet<int>? ActiveSensorDeviceIds { get; set; }
    }
}
