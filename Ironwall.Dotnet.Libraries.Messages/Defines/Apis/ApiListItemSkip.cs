namespace Ironwall.Dotnet.Libraries.Messages.Defines.Apis;

/// <summary>
/// 목록 응답에서 <b>형식 오류로 빼낸 항목 한 건</b> — 쪽 전체를 버리지 않고 그 항목만 뺐다는 기록이다.
/// <para>호출부는 <see cref="ApiListResponse{T}.SkippedItems"/> 가 비어 있지 않으면 경고 로그(<see cref="Describe"/>)를 남긴다.</para>
/// </summary>
/// <param name="Index">응답 <c>data[]</c> 안의 위치(0부터).</param>
/// <param name="Id">항목의 <c>id</c>(읽을 수 있었으면) — 없으면 <c>null</c>.</param>
/// <param name="Path">문제 칸의 JSON 경로(예 <c>data[1].detail.signal</c>).</param>
/// <param name="Reason">역직렬화 예외 메시지 원문.</param>
public sealed record ApiListItemSkip(int Index, string? Id, string Path, string Reason)
{
    /// <summary>경고 로그 한 줄 — 어느 id · 어느 칸 · 왜.</summary>
    public string Describe()
        => $"목록 항목 형식 오류로 제외: id={Id ?? "(없음)"} · 칸={Path} · 사유={Reason}";
}
