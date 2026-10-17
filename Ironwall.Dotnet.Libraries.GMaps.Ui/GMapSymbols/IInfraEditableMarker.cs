using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols
{
    /// <summary>
    /// 인프라/건물 마커 편집 인터페이스
    /// </summary>
    public interface IInfraEditableMarker : IEditableMarker
    {
        /// <summary>
        /// 건물 종류
        /// </summary>
        EnumBuildingType BuildingType { get; set; }

        /// <summary>
        /// 건물 용도
        /// </summary>
        EnumBuildingUsage BuildingUsage { get; set; }

        /// <summary>
        /// 지상 층수
        /// </summary>
        int FloorCount { get; set; }

        /// <summary>
        /// 지하 층수
        /// </summary>
        int BasementFloorCount { get; set; }

        /// <summary>
        /// 건물 면적 (㎡)
        /// </summary>
        double BuildingArea { get; set; }
    }
}
