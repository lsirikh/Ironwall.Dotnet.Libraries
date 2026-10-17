using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Monitoring.Models.Helpers
{
    /// <summary>
    /// 장비의 문 위치 원문 읽기 — 통문 · 함체 공용(장비 화면 · 지도 부팅 복원 · 심볼 초기화가 같은 규칙을 쓴다).
    /// <para><see cref="DoorStateMachine"/> 와 떼어 둔 이유: 그 파일은 장비 모델 없이 소스로 끌어다 쓰는 시험이 있다.</para>
    /// </summary>
    public static class DoorPosition
    {
        /// <summary>문 위치 부품 유형 — 서버는 유형으로 판정한다(통문 · 함체 모두 둘 다 올 수 있다 · 브로커 §6.7 「문 위치」).</summary>
        public static readonly string[] DoorPositionTypes = { "DOOR_ACTUATOR", "DOOR_SENSOR" };

        /// <summary>
        /// 장비의 문 위치 원문 — ① 관측 축의 문 위치 부품 상태(구동부 → 문 센서) → ② 옛 스칼라(6.3 <c>gate_status</c> · <c>door_status</c>).
        /// 문이 없는 장비 · 모르면 null.
        /// <para>일반 통문은 구동부 없이 문 센서(+ 잠금)만 있는 경우가 많다 — 구동부만 보면 늘 '모름' 이 된다(이슈 #7 Q-4b).
        /// 7.0+ 서버에는 스칼라가 없어 축을 먼저 본다.</para>
        /// </summary>
        public static string? Of(IBaseDeviceModel? device)
        {
            if (device is null) return null;
            foreach (var type in DoorPositionTypes)
            {
                var observed = device.Axes?.FindStatusByType(type)?.State;
                if (!string.IsNullOrWhiteSpace(observed)) return observed;
            }
            var legacy = device switch
            {
                IGateDeviceModel gate => gate.GateStatus,
                IEnclosureDeviceModel enclosure => enclosure.DoorStatus,
                _ => null,
            };
            return string.IsNullOrWhiteSpace(legacy) ? null : legacy;
        }

    }
}
