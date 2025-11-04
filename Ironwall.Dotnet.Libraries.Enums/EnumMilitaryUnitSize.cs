using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 부대 규모 (Unit Size/Echelon)
    /// 심볼 상단의 계급장 표시를 결정
    /// </summary>
    public enum EnumMilitaryUnitSize
    {
        /// <summary>
        /// 개인 - 표시 없음
        /// </summary>
        [Description("개인")]
        Individual = 0,

        /// <summary>
        /// 분대 - 점 1개 (8-12명)
        /// </summary>
        [Description("분대")]
        Squad = 1,

        /// <summary>
        /// 소대 - 점 2개 (20-50명)
        /// </summary>
        [Description("소대")]
        Platoon = 2,

        /// <summary>
        /// 중대 - 세로선 1개 (100-200명)
        /// </summary>
        [Description("중대")]
        Company = 3,

        /// <summary>
        /// 대대 - 세로선 2개 (300-1000명)
        /// </summary>
        [Description("대대")]
        Battalion = 4,

        /// <summary>
        /// 연대 - 세로선 3개 (1000-5000명)
        /// </summary>
        [Description("연대")]
        Regiment = 5,

        /// <summary>
        /// 여단 - X표시 (3000-8000명)
        /// </summary>
        [Description("여단")]
        Brigade = 6,

        /// <summary>
        /// 사단 - X표시 2개 (10000-20000명)
        /// </summary>
        [Description("사단")]
        Division = 7,

        /// <summary>
        /// 군단 - X표시 3개 (30000-80000명)
        /// </summary>
        [Description("군단")]
        Corps = 8,

        /// <summary>
        /// 야전군 - X표시 4개 (80000명 이상)
        /// </summary>
        [Description("야전군")]
        Army = 9,

        /// <summary>
        /// 군집단 - X표시 5개
        /// </summary>
        [Description("군집단")]
        ArmyGroup = 10
    }
}