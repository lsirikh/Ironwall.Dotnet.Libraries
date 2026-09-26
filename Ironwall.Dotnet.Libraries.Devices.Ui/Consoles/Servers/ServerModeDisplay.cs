using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 운용 모드 · 강풍 모드 표시 사전 — 저장 값(코드)과 화면 글(한국어)을 가른다 (U-18)
   Created By   : GHLee
   Created On   : 9/27/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>콤보 한 칸 — 저장은 <see cref="Code"/>, 화면은 <see cref="Display"/>.</summary>
public sealed record ServerModeOption(string Code, string Display)
{
    public override string ToString() => Display;
}

/// <summary>
/// 운용 모드(<c>NORMAL</c> · <c>REGISTER</c>)와 강풍 모드(<c>wind0</c>~<c>wind3</c>)를 한국어로 보인다.
/// 모르는 코드는 원문 대신 "알 수 없음" 이다(원문은 <see cref="UnknownTooltip"/> 로 따로 낸다).
/// </summary>
public static class ServerModeDisplay
{
    public const string Unknown = "알 수 없음";

    public static readonly IReadOnlyList<ServerModeOption> OperationModes = new[]
    {
        new ServerModeOption("NORMAL", "일반"),
        new ServerModeOption("REGISTER", "등록"),
    };

    /// <summary>강풍 단계 이름은 지도 계기(<c>InstrumentMath.WindyModeName</c>)와 같은 말을 쓴다.</summary>
    public static readonly IReadOnlyList<ServerModeOption> WindyModes = new[]
    {
        new ServerModeOption("wind0", "보통 바람"),
        new ServerModeOption("wind1", "약한 바람"),
        new ServerModeOption("wind2", "강한 바람"),
        new ServerModeOption("wind3", "태풍 바람"),
    };

    public static string OperationLabel(string? code) => Label(OperationModes, code);

    public static string WindyLabel(string? code) => Label(WindyModes, code);

    /// <summary>모르는 코드일 때만 원문을 담은 툴팁 글, 아는 코드면 빈 글자.</summary>
    public static string UnknownTooltip(string? code)
        => string.IsNullOrWhiteSpace(code) || OperationModes.Concat(WindyModes).Any(o => Same(o.Code, code))
            ? string.Empty
            : $"받은 값: {code!.Trim()}";

    private static string Label(IReadOnlyList<ServerModeOption> options, string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "—";
        return options.FirstOrDefault(o => Same(o.Code, code))?.Display ?? Unknown;
    }

    private static bool Same(string a, string? b) => string.Equals(a, b?.Trim(), StringComparison.OrdinalIgnoreCase);
}
