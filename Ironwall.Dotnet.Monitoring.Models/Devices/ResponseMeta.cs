using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <summary>
/// 장비 응답 봉투의 표현 프로필 — <c>meta.view</c>(<c>basic</c>·<c>full</c>)와 <c>meta.sections</c>.
/// "이 응답이 무엇을 실었는가"의 <b>유일한 기준</b>이다(서버 D15, v7.0 신규 — 6.3 에는 없다).
/// </summary>
public sealed class ResponseMeta
{
    public ResponseMeta(string? view, IEnumerable<string>? sections)
    {
        View = view;
        Sections = sections?.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>();
    }

    public string? View { get; }

    /// <summary>실려 온 절 이름들. 6.3 응답이면 빈 목록.</summary>
    public IReadOnlyList<string> Sections { get; }
}
