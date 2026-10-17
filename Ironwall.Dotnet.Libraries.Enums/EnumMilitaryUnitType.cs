using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 부대 종류 (Unit Type)
    /// 심볼 내부 아이콘을 결정하는 핵심 분류
    /// </summary>
    public enum EnumMilitaryUnitType
    {
        // === Land Forces - 육상 부대 ===

        [Description("보병")]
        Infantry,

        [Description("방공")]
        AirDefence,

        [Description("탄약")]
        Ammunition,

        [Description("대전차")]
        AntiTank,

        [Description("기갑")]
        Armour,

        [Description("포병")]
        Artillery,

        [Description("교량")]
        Bridging,

        [Description("전투근무지원")]
        CombatServiceSupport,

        [Description("연합기동부대")]
        CombinedManoeuvreArms,

        [Description("공병")]
        Engineer,

        [Description("전자측거")]
        ElectronicRanging,

        [Description("전자전")]
        ElectronicWarfare,

        [Description("폭발물처리")]
        ExplosiveOrdnanceDisposal,

        [Description("연료보급")]
        FuelPOL,

        [Description("병원")]
        Hospital,

        [Description("사령부")]
        HQUnit,

        [Description("정비")]
        Maintenance,

        [Description("의료")]
        Medical,

        [Description("기상")]
        Meteorological,

        [Description("미사일")]
        Missile,

        [Description("박격포")]
        Mortar,

        [Description("헌병")]
        MilitaryPolice,

        [Description("화생방방어")]
        CBRNDefence,

        [Description("병기")]
        Ordnance,

        [Description("심리전")]
        PsychologicalOperations,

        [Description("정찰기병")]
        ReconnaissanceCavalry,

        [Description("통신")]
        Signals,

        [Description("특수부대")]
        SpecialForces,

        [Description("특수작전부대")]
        SpecialOperationsForces,

        [Description("보급")]
        Supply,

        [Description("지형")]
        Topographical,

        [Description("수송")]
        Transportation,

        // === Air Forces - 공군 ===

        [Description("회전익항공")]
        RotaryWingAviation,

        [Description("고정익항공")]
        FixedWingAviation,

        [Description("무인항공기")]
        UnmannedAirVehicle,

        [Description("레이더")]
        Radar,

        // === Naval Forces - 해군 ===

        [Description("해군")]
        Navy
    }
}