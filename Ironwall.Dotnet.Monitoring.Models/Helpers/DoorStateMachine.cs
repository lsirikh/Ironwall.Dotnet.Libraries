using System;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Helpers
{
    /// <summary>
    /// 개폐 형태 축 상태 머신 (PRD FR-12) — 순수 함수. Monitoring.Models 에 두는 이유: Events.Ui(이벤트 배선)와 GMaps.Ui(렌더)가
    /// 함께 쓰는데 GMaps.Ui → Events.Ui 참조 방향이라 GMaps.Ui 에 두면 순환 참조가 된다(이관, 사본 금지). 색 축(CompositeStatus)과 무관하며, 큐 Dequeue/자동조치보고는 이 함수를 호출하지 않는다.
    /// <list type="bullet">
    /// <item>ContactOn → Open, ContactOff → Closed (<paramref name="openOnContactOn"/>=false 면 현장 배선 반전)</item>
    /// <item>그 외 이벤트(Intrusion/Fault/…)는 현재 상태 유지</item>
    /// <item>부팅: 서버 문자열(OPEN/CLOSED) → 상태, 없으면 Unknown. 표시용 Effective 는 Unknown→Closed</item>
    /// </list>
    /// </summary>
    public static class DoorStateMachine
    {
        public static EnumDoorState Next(EnumDoorState current, EnumEventType evt, bool openOnContactOn = true)
        {
            switch (evt)
            {
                case EnumEventType.ContactOn: return openOnContactOn ? EnumDoorState.Open : EnumDoorState.Closed;
                case EnumEventType.ContactOff: return openOnContactOn ? EnumDoorState.Closed : EnumDoorState.Open;
                default: return current;
            }
        }

        /// <summary>서버 상태 문자열(gates.gate_status / enclosures.door_status: "OPEN"|"CLOSED", 대소문자 무시) → 상태. null/미지값 → Unknown.</summary>
        public static EnumDoorState FromServer(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return EnumDoorState.Unknown;
            if (status.Equals("OPEN", StringComparison.OrdinalIgnoreCase)) return EnumDoorState.Open;
            if (status.Equals("CLOSED", StringComparison.OrdinalIgnoreCase)) return EnumDoorState.Closed;
            return EnumDoorState.Unknown;
        }

        /// <summary>OPERATION_EVENT `reason`(GATE_OPEN/GATE_CLOSED/ENCLOSURE_DOOR_OPEN/ENCLOSURE_DOOR_CLOSED …, 서버 PRD v1.4) → 상태. 임계치 경보 등 개폐와 무관한 값은 null.</summary>
        public static EnumDoorState? FromOperationEvent(string? typeEvent)
        {
            if (string.IsNullOrWhiteSpace(typeEvent)) return null;
            var t = typeEvent.Trim().ToUpperInvariant();
            if (t.EndsWith("_OPEN", StringComparison.Ordinal) || t.EndsWith("_OPENED", StringComparison.Ordinal)) return EnumDoorState.Open;
            if (t.EndsWith("_CLOSED", StringComparison.Ordinal) || t.EndsWith("_CLOSE", StringComparison.Ordinal)) return EnumDoorState.Closed;
            return null;
        }

        /// <summary>표시 형태 — Unknown 은 닫힘으로 그리고 라벨에 '?' 를 붙인다(정직 표기).</summary>
        public static EnumDoorState Effective(EnumDoorState state) => state == EnumDoorState.Unknown ? EnumDoorState.Closed : state;

        /// <summary>애니메이션 목표(0=닫힘, 1=열림). Unknown 은 0.</summary>
        public static double OpenFraction(EnumDoorState state) => state == EnumDoorState.Open ? 1.0 : 0.0;

        /// <summary>접점 이벤트인가 — 큐·카드·사운드에서 제외할 대상 판정에 쓴다.</summary>
        public static bool IsContactEvent(EnumEventType evt) => evt is EnumEventType.ContactOn or EnumEventType.ContactOff;

        /// <summary>개폐 형태를 가진 장비 타입인가(통문·함체).</summary>
        public static bool HasDoor(EnumDeviceType type) => type is EnumDeviceType.Gate or EnumDeviceType.Enclosure;
    }
}
