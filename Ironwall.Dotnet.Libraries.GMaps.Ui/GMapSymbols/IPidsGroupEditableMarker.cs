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
    }
}
