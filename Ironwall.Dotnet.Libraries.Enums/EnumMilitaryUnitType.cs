using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 부대 종류 (Unit Type)
    /// 심볼 내부 아이콘을 결정하는 핵심 분류
    /// </summary>
    public enum EnumMilitaryUnitType
    {
        #region Land Forces - 육상 부대

        /// <summary>
        /// 보병 - 교차된 소총
        /// </summary>
        [Description("보병")]
        Infantry = 100,

        /// <summary>
        /// 기계화보병 - 소총 + 원
        /// </summary>
        [Description("기계화보병")]
        MechanizedInfantry = 101,

        /// <summary>
        /// 공수부대 - 낙하산
        /// </summary>
        [Description("공수부대")]
        Airborne = 102,

        /// <summary>
        /// 해병대 - 닻 + 소총
        /// </summary>
        [Description("해병대")]
        Marines = 103,

        /// <summary>
        /// 특수부대 - 화살표
        /// </summary>
        [Description("특수부대")]
        SpecialForces = 104,

        /// <summary>
        /// 기갑 - 탱크 궤도
        /// </summary>
        [Description("기갑")]
        Armor = 200,

        /// <summary>
        /// 기계화 - 원 + 점
        /// </summary>
        [Description("기계화")]
        Mechanized = 201,

        /// <summary>
        /// 포병 - 포구
        /// </summary>
        [Description("포병")]
        Artillery = 300,

        /// <summary>
        /// 대공포병 - 포구 + 위쪽 화살표
        /// </summary>
        [Description("대공포병")]
        AirDefense = 301,

        /// <summary>
        /// 로켓포병 - 로켓
        /// </summary>
        [Description("로켓포병")]
        Rocket = 302,

        /// <summary>
        /// 공병 - 성
        /// </summary>
        [Description("공병")]
        Engineer = 400,

        /// <summary>
        /// 통신 - 번개
        /// </summary>
        [Description("통신")]
        Signal = 500,

        /// <summary>
        /// 정보 - 다이아몬드 안 I
        /// </summary>
        [Description("정보")]
        Intelligence = 600,

        /// <summary>
        /// 군수 - 톱니바퀴
        /// </summary>
        [Description("군수")]
        Logistics = 700,

        /// <summary>
        /// 의무 - 십자가
        /// </summary>
        [Description("의무")]
        Medical = 800,

        /// <summary>
        /// 헌병 - 방패
        /// </summary>
        [Description("헌병")]
        MilitaryPolice = 900,

        #endregion

        #region Air Forces - 공군

        /// <summary>
        /// 전투기 - 날개
        /// </summary>
        [Description("전투기")]
        Fighter = 1000,

        /// <summary>
        /// 폭격기 - 큰 날개
        /// </summary>
        [Description("폭격기")]
        Bomber = 1001,

        /// <summary>
        /// 헬리콥터 - 로터
        /// </summary>
        [Description("헬리콥터")]
        Helicopter = 1002,

        /// <summary>
        /// 수송기 - 날개 + 사각형
        /// </summary>
        [Description("수송기")]
        Transport = 1003,

        /// <summary>
        /// 정찰기 - 날개 + 눈
        /// </summary>
        [Description("정찰기")]
        Reconnaissance = 1004,

        #endregion

        #region Naval Forces - 해군

        /// <summary>
        /// 수상함 - 배 모양
        /// </summary>
        [Description("수상함")]
        Surface = 2000,

        /// <summary>
        /// 잠수함 - 어뢰 모양
        /// </summary>
        [Description("잠수함")]
        Submarine = 2001,

        /// <summary>
        /// 항공모함 - 배 + 날개
        /// </summary>
        [Description("항공모함")]
        Carrier = 2002,

        /// <summary>
        /// 구축함 - 작은 배
        /// </summary>
        [Description("구축함")]
        Destroyer = 2003,

        #endregion

        #region Command & Control - 지휘통제

        /// <summary>
        /// 지휘부 - 깃발
        /// </summary>
        [Description("지휘부")]
        Command = 9000,

        /// <summary>
        /// 통제소 - 별
        /// </summary>
        [Description("통제소")]
        Control = 9001,

        /// <summary>
        /// 본부 - 다이아몬드
        /// </summary>
        [Description("본부")]
        Headquarters = 9002

        #endregion
    }

}