using System;
using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Enums;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/21/2025 9:48:20 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 마커 색상 타입 열거형
/// - UI에서 선택 가능한 주요 색상들
/// - Brush 변환을 위한 표준 색상 정의
/// </summary>
public enum EnumColorType
{
    [Description("기본 파란색")]
    Blue,

    [Description("빨간색")]
    Red,

    [Description("초록색")]
    Green,

    [Description("주황색")]
    Orange,

    [Description("보라색")]
    Purple,

    [Description("노란색")]
    Yellow,

    [Description("분홍색")]
    Pink,

    [Description("청록색")]
    Teal,

    [Description("남색")]
    Indigo,

    [Description("라임색")]
    Lime,

    [Description("갈색")]
    Brown,

    [Description("회색")]
    Gray,

    [Description("검은색")]
    Black,

    [Description("흰색")]
    White,

    [Description("진한 파란색")]
    DarkBlue,

    [Description("진한 빨간색")]
    DarkRed,

    [Description("진한 초록색")]
    DarkGreen,

    [Description("진한 주황색")]
    DarkOrange,

    [Description("진한 보라색")]
    DarkPurple,

    [Description("금색")]
    Gold,

    [Description("은색")]
    Silver,

    [Description("마젠타")]
    Magenta,

    [Description("사이언")]
    Cyan,

    [Description("베이지색")]
    Beige,

    [Description("올리브색")]
    Olive,

    [Description("투명")]
    Transparent,
}