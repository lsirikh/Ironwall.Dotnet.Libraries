namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// "?" 말풍선 하나의 내용 — 제목 + 글머리 항목 + (선택) 소제목 묶음. 키는 <c>도메인.창.섹션</c>(help-callout PRD FR-03).
/// </summary>
/// <remarks>
/// <para>문구는 각 UI 프로젝트의 C# 설명 목록에 한 번만 적는다(XAML · ViewModel 에 흩뜨리지 않는다). 화면은 키만 건다.</para>
/// <para>항목 글의 작은 표기 — <c>{Ctrl}</c> 은 단축키 칩, <c>**손댄 칸만**</c> 은 굵게(<see cref="HelpText"/>).</para>
/// </remarks>
public sealed record HelpEntry
{
    public HelpEntry(string key, string title, IReadOnlyList<HelpSection> sections)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("도움말 키가 비었습니다.", nameof(key));
        Key = key;
        Title = title ?? string.Empty;
        Sections = sections ?? throw new ArgumentNullException(nameof(sections));
    }

    /// <summary>찾는 키 — 예: <c>Devices.Wiring.Fence</c>. 버튼 AutomationId 는 <c>Help.{Key}</c>.</summary>
    public string Key { get; }

    /// <summary>말풍선 제목(굵게).</summary>
    public string Title { get; }

    /// <summary>묶음들. 첫 묶음은 보통 소제목이 없다(제목 바로 아래 글머리).</summary>
    public IReadOnlyList<HelpSection> Sections { get; }

    /// <summary>제목 + 글머리 항목으로 만든다(소제목 없는 첫 묶음).</summary>
    public static HelpEntry Create(string key, string title, params string[] items)
        => new(key, title, items is { Length: > 0 } ? new[] { new HelpSection(null, items) } : Array.Empty<HelpSection>());

    /// <summary>소제목 묶음을 하나 덧붙인 새 항목을 돌려준다(예: "키보드로").</summary>
    public HelpEntry With(string heading, params string[] items)
        => new(Key, Title, Sections.Append(new HelpSection(heading, items ?? Array.Empty<string>())).ToList());

    /// <summary>
    /// 소제목 <paramref name="heading"/> 묶음에 항목을 더한다 — 그 묶음이 없으면 끝에 새로 만든다. 이미 있는 글(표기를 걷은 글 기준)은 다시 넣지 않는다.
    /// </summary>
    public HelpEntry Merge(string heading, IEnumerable<string> items)
    {
        var extra = (items ?? Enumerable.Empty<string>()).Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
        if (extra.Count == 0) return this;

        var sections = Sections.ToList();
        var index = sections.FindIndex(s => string.Equals(s.Heading, heading, StringComparison.Ordinal));
        var existing = index >= 0 ? sections[index].Items : Array.Empty<string>();
        var seen = new HashSet<string>(existing.Select(HelpText.Plain), StringComparer.Ordinal);
        var merged = existing.ToList();
        foreach (var item in extra)
            if (seen.Add(HelpText.Plain(item))) merged.Add(item);

        if (merged.Count == existing.Count) return this;
        var section = new HelpSection(heading, merged);
        if (index >= 0) sections[index] = section;
        else sections.Add(section);
        return new HelpEntry(Key, Title, sections);
    }

    /// <summary>자동화 · 낭독용 평문 — 제목 · 소제목 · 항목을 줄바꿈으로 잇는다(표기는 걷는다).</summary>
    public string ToPlainText()
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(Title)) lines.Add(HelpText.Plain(Title));
        foreach (var section in Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Heading)) lines.Add(HelpText.Plain(section.Heading));
            lines.AddRange(section.Items.Select(HelpText.Plain));
        }
        return string.Join("\n", lines);
    }
}

/// <summary>말풍선 안의 한 묶음 — (선택) 소제목 + 글머리 항목.</summary>
public sealed record HelpSection(string? Heading, IReadOnlyList<string> Items);
