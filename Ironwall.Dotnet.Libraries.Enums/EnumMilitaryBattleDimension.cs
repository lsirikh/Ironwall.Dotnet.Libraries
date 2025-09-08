using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 전투 차원 (Battle Dimension)
    /// 심볼의 기본 프레임 모양을 결정
    /// </summary>
    public enum EnumMilitaryBattleDimension
    {
        /// <summary>
        /// 육상 - 사각형 기반
        /// </summary>
        [Description("육상")]
        Land = 0,

        /// <summary>
        /// 해상 (수상) - 원형 상단
        /// </summary>
        [Description("해상")]
        Sea = 1,

        /// <summary>
        /// 해상 (수중) - 원형 하단
        /// </summary>
        [Description("잠수함")]
        Subsurface = 2,

        /// <summary>
        /// 공중 - 원형
        /// </summary>
        [Description("공중")]
        Air = 3,

        /// <summary>
        /// 우주 - 별 모양
        /// </summary>
        [Description("우주")]
        Space = 4,

        /// <summary>
        /// 전자전 - 육각형
        /// </summary>
        [Description("전자전")]
        ElectronicWarfare = 5,

        /// <summary>
        /// 사이버 - 다이아몬드
        /// </summary>
        [Description("사이버")]
        Cyber = 6
    }
}