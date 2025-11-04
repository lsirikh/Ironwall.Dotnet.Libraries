using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums
{
    /// <summary>
    /// 군대부호 소속 구분 (NATO APP-6D 기반)
    /// 심볼의 기본 형태와 색상을 결정하는 핵심 분류
    /// </summary>
    public enum EnumMilitaryAffiliation
    {
        /// <summary>
        /// 미확인 - 회색 (정보 부족)
        /// </summary>
        [Description("미확인")]
        Unknown = 0,
     
        /// <summary>
        /// 아군 - 파란색 (사각형 프레임)
        /// </summary>
        [Description("아군")]
        Friend = 1,

        /// <summary>
        /// 중립 - 녹색 (정사각형 프레임)
        /// </summary>
        [Description("중립")]
        Neutral = 2,

        /// <summary>
        /// 적군 - 빨간색 (다이아몬드 프레임)
        /// </summary>
        [Description("적군")]
        Hostile = 3,
    }
}