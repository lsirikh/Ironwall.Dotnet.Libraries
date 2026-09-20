using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;

/// <summary>
/// 서버가 준 파일 이름을 <b>그대로 믿지 않는다</b> — 저장 대화상자에 넣기 전에 다듬는 <b>순수</b> 함수.
/// </summary>
/// <remarks>
/// <para>이름은 <c>Content-Disposition</c> 에서 온다. 즉 <b>경계 밖에서 들어온 입력</b>이다.
/// 경로 조각(<c>..\\..\\</c>) · 절대 경로 · 장치 이름(<c>CON</c> 등) · 금지 문자가 섞이면
/// 저장 위치가 사용자가 고른 곳이 아니게 되거나 저장 자체가 이상하게 실패한다.</para>
/// <para>한글 이름은 그대로 살린다 — 막아야 하는 것은 <b>경로와 장치</b>이지 언어가 아니다.</para>
/// </remarks>
public static class SafeFileName
{
    /// <summary>파일 이름 최대 길이(확장자 포함). 경로 길이 한계를 넉넉히 피한다.</summary>
    public const int MaxLength = 120;

    /// <summary>Windows 예약 장치 이름 — 확장자를 붙여도 파일이 되지 않는다.</summary>
    private static readonly string[] Reserved =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// 서버 이름을 다듬는다. 쓸 수 없으면 <paramref name="fallback"/> 을 쓴다.
    /// </summary>
    /// <param name="fromServer">Content-Disposition 이 준 이름(신뢰하지 않는다).</param>
    /// <param name="fallback">우리가 만든 이름 — 이미 안전하다고 가정하지 않고 같은 규칙을 태운다.</param>
    /// <param name="allowedExtension">허용 확장자(<c>.pdf</c> · <c>.csv</c>). 다르면 이것으로 바꾼다.</param>
    public static string Sanitize(string? fromServer, string fallback, string allowedExtension)
    {
        var candidate = Clean(fromServer, allowedExtension);
        if (candidate is not null) return candidate;

        return Clean(fallback, allowedExtension) ?? "report" + allowedExtension;
    }

    private static string? Clean(string? raw, string allowedExtension)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // ① 경로 조각을 버린다 — 구분자를 양쪽 다 본다(서버가 어느 쪽을 줄지 모른다).
        var name = raw.Replace('\\', '/');
        var slash = name.LastIndexOf('/');
        if (slash >= 0) name = name[(slash + 1)..];

        // ② 드라이브 표기(C:) · 대체 데이터 스트림(:) 을 자른다.
        var colon = name.IndexOf(':');
        if (colon >= 0) name = name[..colon];

        // ③ 금지 문자와 제어 문자를 지운다(한글 · 공백은 남긴다).
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsControl(c) || invalid.Contains(c)) continue;
            builder.Append(c);
        }
        name = builder.ToString().Trim().TrimEnd('.', ' ');
        if (name.Length == 0) return null;

        // ④ 확장자는 허용된 것 하나로 고정한다.
        var stem = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrWhiteSpace(stem)) return null;

        // ⑤ 예약 장치 이름은 쓸 수 없다 — 접두를 붙여 피한다.
        if (Reserved.Contains(stem, StringComparer.OrdinalIgnoreCase)) stem = "_" + stem;

        // ⑥ 길이를 자른다(확장자를 남기고).
        var room = MaxLength - allowedExtension.Length;
        if (stem.Length > room) stem = stem[..room].TrimEnd('.', ' ');
        if (stem.Length == 0) return null;

        return stem + allowedExtension;
    }
}
