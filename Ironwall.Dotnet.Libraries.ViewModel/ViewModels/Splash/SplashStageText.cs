using Ironwall.Dotnet.Libraries.Base.Services.Startup;
using System;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Splash;

/****************************************************************************
   Purpose      : 기동 화면에 보이는 한 줄 — 운영자 말로 (window-design-inventory B8)
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 기동 진행(<see cref="StartupProgress"/>)을 운영자가 읽을 한 줄로 바꾼다. <b>순수 함수</b>라 창 없이 시험한다.
/// </summary>
/// <remarks>
/// 예전에는 <c>"{서비스 클래스 이름} — 시작 중..."</c> 을 그대로 보였다(<c>NatsDomainService — 시작 중...</c> 같은 개발자 글).
/// 클래스 이름은 로그에만 남기고 화면에는 지금 무슨 단계인지만 보인다.
/// </remarks>
public static class SplashStageText
{
    public const string Starting = "시스템을 준비하는 중입니다…";
    public const string StartingServices = "서비스를 시작하는 중입니다…";
    public const string LoadingData = "화면에 쓸 정보를 불러오는 중입니다…";
    public const string Done = "시작 준비를 마쳤습니다.";

    /// <summary>
    /// 이 진행 보고를 무엇이라고 보일지. 앞 줄을 그대로 두는 편이 나은 보고(한 단계의 '완료')면 <c>null</c> —
    /// 서비스마다 '시작 중 → 완료' 가 번갈아 와서 줄이 깜박이지 않게.
    /// </summary>
    public static string? Describe(StartupProgress progress)
    {
        if (progress.TotalFraction >= 1.0) return Done;
        return progress.Stage switch
        {
            "시작 중..." => StartingServices,
            "초기화 중..." => LoadingData,
            "완료" => null,
            _ => Starting,
        };
    }

    /// <summary>진행률(0~100)의 글 — 반올림한 정수 퍼센트.</summary>
    public static string Percent(double value)
    {
        if (double.IsNaN(value) || value < 0) value = 0;
        if (value > 100) value = 100;
        return $"{Math.Round(value, MidpointRounding.AwayFromZero):0}%";
    }
}
