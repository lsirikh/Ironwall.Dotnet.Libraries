using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 표준 정체성 (Standard Identity)
    /// 심볼의 선 스타일과 투명도를 결정
    /// </summary>
    public enum EnumMilitaryStandardIdentity
    {
        /// <summary>
        /// 현재 - 실선
        /// </summary>
        [Description("현재")]
        Present = 0,

        /// <summary>
        /// 계획 - 점선
        /// </summary>
        [Description("계획")]
        Planned = 1,

        /// <summary>
        /// 가정 - 일점쇄선
        /// </summary>
        [Description("가정")]
        Anticipated = 2,

        /// <summary>
        /// 과거 - 실선 + 투명도
        /// </summary>
        [Description("과거")]
        Past = 3
    }
}