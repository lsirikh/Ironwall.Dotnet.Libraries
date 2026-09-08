using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums
{
    public enum EnumDeviceType : int
    {
        NONE = 0, //0
        Controller = 1, //1
        Multi = 2, //2
        Fence = 3, //3
        Underground = 4, //4
        Contact = 5, //5
        PIR = 6, //6
        IoController = 7, //7
        Laser = 8, //8

        Cable = 9, //9
        IpCamera = 10, //10

        SmartSensor = 11, //11
        SmartSensor2 = 12, //12

        SmartCompound = 13, //13

        IpSpeaker = 14, //14
        Radar = 15, //15
        OpticalCable = 16, //16

        Fence_Group = 17,
        Lamp = 18,
        Enclosure = 19,
        SmartMultisensor2 = 20,
        /// <summary>통문(Gate) — 사용자 결정 D2(2026-09-07): 접점 센서 변형이 아닌 독립 장비 타입 + 전용 3D 모델 "fencegate". 서버 계약은 이름 문자열 "Gate"(operation-event PRD v1.4).</summary>
        Gate = 21,
    }
    
}
