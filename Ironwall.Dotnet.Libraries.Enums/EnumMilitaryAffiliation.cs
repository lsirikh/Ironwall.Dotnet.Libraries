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
        /// 가정된 아군 - 연한 파란색
        /// </summary>
        [Description("가정 아군")]
        AssumedFriend = 1,

        /// <summary>
        /// 아군 - 파란색 (사각형 프레임)
        /// </summary>
        [Description("아군")]
        Friend = 2,

        /// <summary>
        /// 중립 - 녹색 (정사각형 프레임)
        /// </summary>
        [Description("중립")]
        Neutral = 3,

        /// <summary>
        /// 적군 - 빨간색 (다이아몬드 프레임)
        /// </summary>
        [Description("적군")]
        Hostile = 4,

        /// <summary>
        /// 가정된 적군 - 연한 빨간색
        /// </summary>
        [Description("가정 적군")]
        AssumedHostile = 5,

        /// <summary>
        /// 의심스러운 - 노란색
        /// </summary>
        [Description("의심")]
        Suspect = 6,

        /// <summary>
        /// 민간인 - 보라색
        /// </summary>
        [Description("민간")]
        Civilian = 7
    }
}