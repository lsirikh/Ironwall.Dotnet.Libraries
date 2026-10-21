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
        /// <remarks>통문 순서(구동부 → 문 센서). 함체는 <see cref="EnclosureOrder"/>. 읽는 순서는 <see cref="OrderFor"/>.</remarks>
        public static readonly string[] DoorPositionTypes = { "DOOR_ACTUATOR", "DOOR_SENSOR" };

        /// <summary>통문의 찾는 순서 — 구동부(<c>DOOR_ACTUATOR</c>, 구동 중까지 안다) → 문 센서. 관례 key 는 <c>actuator</c> → <c>door</c>.</summary>
        public static readonly (string Type, string Key)[] GateOrder = { ("DOOR_ACTUATOR", "actuator"), ("DOOR_SENSOR", "door") };

        /// <summary>
        /// 함체의 찾는 순서 — 문 센서(<c>DOOR_SENSOR</c>) → 구동부. 관례 key 는 <c>door</c> → <c>actuator</c>(GOP API 8.0.15 대조 G34).
        /// 함체 문 위치의 라이브 관례는 센서다(브로커 §3.2 ⚠ "함체 = 센서 · 통문 = 구동부") — 둘 다 달린 함체에서 구동부가 이기면 안 된다.
        /// </summary>
        public static readonly (string Type, string Key)[] EnclosureOrder = { ("DOOR_SENSOR", "door"), ("DOOR_ACTUATOR", "actuator") };

        /// <summary>장비 종류별 찾는 순서 — 함체면 <see cref="EnclosureOrder"/>, 그 밖(통문 등)은 <see cref="GateOrder"/>.</summary>
        public static (string Type, string Key)[] OrderFor(IBaseDeviceModel? device)
            => device is IEnclosureDeviceModel ? EnclosureOrder : GateOrder;

        /// <summary>
        /// 장비의 문 위치 원문 — ① 관측 축의 문 위치 부품 상태(유형으로: 통문은 구동부 → 문 센서, 함체는 문 센서 → 구동부)
        /// → ② 형상 선언이 없을 때 관례 key(같은 순서) → ③ 옛 스칼라(6.3 <c>gate_status</c> · <c>door_status</c>).
        /// 문이 없는 장비 · 모르면 null.
        /// <para>일반 통문은 구동부 없이 문 센서(+ 잠금)만 있는 경우가 많다 — 구동부만 보면 늘 '모름' 이 된다(이슈 #7 Q-4b).
        /// 7.0+ 서버에는 스칼라가 없어 축을 먼저 본다.</para>
        /// </summary>
        public static string? Of(IBaseDeviceModel? device)
        {
            if (device is null) return null;
            var observed = FindStatus(device)?.State;
            if (!string.IsNullOrWhiteSpace(observed)) return observed;
            var legacy = device switch
            {
                IGateDeviceModel gate => gate.GateStatus,
                IEnclosureDeviceModel enclosure => enclosure.DoorStatus,
                _ => null,
            };
            return string.IsNullOrWhiteSpace(legacy) ? null : legacy;
        }

        /// <summary>
        /// 문 위치 부품의 관측(상태가 있는 것만) — 유형 순서대로 찾고, 없으면 관례 key 로 폴백한다.
        /// key 폴백은 그 key 가 <b>다른 유형으로 선언돼 있지 않을 때만</b> 쓴다(함체의 <c>lock</c> 처럼 문과 무관한 부품을 문으로 읽지 않는다).
        /// </summary>
        public static ComponentStatusModel? FindStatus(IBaseDeviceModel? device)
        {
            var axes = device?.Axes;
            if (axes is null) return null;
            var order = OrderFor(device);

            foreach (var (type, _) in order)
            {
                var byType = axes.FindStatusByType(type);
                if (!string.IsNullOrWhiteSpace(byType?.State)) return byType;
            }

            var observed = axes.DeviceStatus?.Components;
            if (observed is null) return null;
            foreach (var (type, key) in order)
            {
                var declared = axes.HardwareSpec?.Components?.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));
                if (declared != null && !string.IsNullOrWhiteSpace(declared.Type) && !string.Equals(declared.Type, type, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (observed.TryGetValue(key, out var byKey) && !string.IsNullOrWhiteSpace(byKey?.State)) return byKey;
            }
            return null;
        }

    }
}
