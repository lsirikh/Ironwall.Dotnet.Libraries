using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Enums;
public enum EnumEventCategory
{
    // 미정의
    NONE,
    // 펜스센서 단독
    FENCE_SENSOR_ONLY,
    // 펜스센서와 멀티센서 And 조건
    FENCE_SENSOR_WITH_MULTI_SENSOR,
    // 멀티센서 단독
    MULTI_SENSOR_ONLY,
    // 센서와 카메라 적용
    SENSOR_WITH_CAMERA,
    // 센서와 AI 카메라 판단 적용
    SENSOR_WITH_AI_CAMERA,
    // AI 카메라 판단 단독
    AI_CAMERA_ONLY,
    // 카메라 단독
    CAMERA_ONLY,
}
