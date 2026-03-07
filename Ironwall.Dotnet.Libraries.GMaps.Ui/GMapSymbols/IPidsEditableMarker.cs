using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols
{
    public interface IPidsEditableMarker : IEditableMarker
    {
        int LinkedDeviceId { get; set; }

        /// <summary>
        /// 연결된 디바이스 객체 (런타임 바인딩용)
        /// <para>설정 시 LinkedDeviceId가 자동 동기화됩니다.</para>
        /// </summary>
        IBaseDeviceModel? LinkedDevice { get; set; }

        EnumEventStatus EventStatus { get; set; }
        EnumDeviceType DeviceType { get; set; }
        bool ShowFOV { get; set; }
        EnumColorType FOVColor { get; set; }
        double FOVOpacity { get; set; }
        double DetectionRange { get; set; }
        double DetectionAngle { get; set; }
        double DetectionBearing { get; set; }
        double BaseBearing { get; set; }

        /// <summary>
        /// 방송(음원/TTS) 동작 중 여부 — XAML Opacity 펄스 애니메이션 트리거
        /// </summary>
        bool IsBroadcasting { get; set; }
    }
}
