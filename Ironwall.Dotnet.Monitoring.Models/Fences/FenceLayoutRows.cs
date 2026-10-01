using System;
using System.Globalization;

namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>저장 한 번에 낼 SQL 갈래.</summary>
public enum FenceLayoutSaveKind
{
    /// <summary>처음(판 0) — 새 행. 이미 있으면 쓰지 않는다(그사이 다른 GIS 가 만들었다 = 충돌).</summary>
    Insert = 0,
    /// <summary>불러온 판과 같을 때만 고친다.</summary>
    Update = 1,
    /// <summary>판과 상관없이 덮는다(없으면 만든다) — 덮기 전 본문은 보관 칸으로.</summary>
    Overwrite = 2,
}

/// <summary>저장 계획 — 어떤 SQL 을 낼지 · 성공하면 몇 판이 되는지(덮어쓰기는 행을 다시 읽어야 안다).</summary>
public sealed record FenceLayoutSavePlan(FenceLayoutSaveKind Kind, int? NextRevision);

/// <summary>
/// 펜스 구성 저장소의 <b>SQL 밖 규칙</b>(fence-wiring-editor FR-11) — 열쇠 만들기 · 행 → 결과 · 판 계산 · 영향 행 수 → 결과.
/// 저장소 구현(<c>GMaps.Db</c>)은 SQL 만 내고 판정은 여기에 맡긴다(헤드리스 시험 대상).
/// </summary>
public static class FenceLayoutRows
{
    /// <summary>서버 주소를 모를 때의 열쇠 — 모르는 것끼리는 한 칸을 함께 쓴다(알려진 서버와는 섞이지 않는다).</summary>
    public const string UNKNOWN_SERVER = "unknown";

    /// <summary>열쇠 칸의 최대 길이(DB 칸 VARCHAR(190) — utf8mb4 인덱스 한도 안).</summary>
    public const int MAX_SERVER_KEY = 190;

    /// <summary>
    /// API 주소 → 서버 열쇠 <c>host:port</c>(소문자 · 포트는 생략되면 스킴 기본값). 경로 · 질의 · 사용자 정보는 버린다 —
    /// 같은 서버를 <c>/api</c> 유무로 다르게 적어도 한 칸이다. 읽을 수 없으면 다듬은 글자 그대로, 비었으면 <see cref="UNKNOWN_SERVER"/>.
    /// </summary>
    public static string ServerKeyOf(string? apiUrl)
    {
        var text = apiUrl?.Trim();
        if (string.IsNullOrEmpty(text)) return UNKNOWN_SERVER;
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            var host = uri.IdnHost.ToLowerInvariant();
            if (uri.HostNameType == UriHostNameType.IPv6) host = $"[{host.Trim('[', ']')}]";
            return Clip($"{host}:{uri.Port.ToString(CultureInfo.InvariantCulture)}");
        }
        return Clip(apiUrl!.Trim().ToLowerInvariant());

        static string Clip(string s) => s.Length <= MAX_SERVER_KEY ? s : s[..MAX_SERVER_KEY];
    }

    /// <summary>
    /// 읽은 행 → 결과. 행이 없으면 없음, 본문을 읽을 수 없으면 <b>손상</b>(행 판은 안다 — 없는 것으로 여기면 판 0 저장이 그 행에 부딪혀 충돌로 오보된다).
    /// </summary>
    public static FenceLayoutLoadResult Interpret(bool rowExists, int revision, string? body, DateTime? updatedAt)
    {
        if (!rowExists) return FenceLayoutLoadResult.NotFound;
        var document = FenceLayoutJson.Deserialize(body, revision, updatedAt);
        return document is null
            ? new FenceLayoutLoadResult(FenceLayoutLoadStatus.Unreadable, null, revision, "본문을 읽을 수 없습니다")
            : FenceLayoutLoadResult.Loaded(document);
    }

    /// <summary>저장 계획 — 덮어쓰기면 판을 보지 않고, 아니면 판 0 은 새 행 · 그 밖은 판 맞춤 고치기.</summary>
    public static FenceLayoutSavePlan PlanSave(int loadedRevision, FenceLayoutSaveMode mode)
        => mode == FenceLayoutSaveMode.Overwrite ? new FenceLayoutSavePlan(FenceLayoutSaveKind.Overwrite, null)
         : loadedRevision <= 0 ? new FenceLayoutSavePlan(FenceLayoutSaveKind.Insert, 1)
         : new FenceLayoutSavePlan(FenceLayoutSaveKind.Update, loadedRevision + 1);

    /// <summary>
    /// SQL 결과 → 저장 결과. 새 행 · 고치기가 한 줄도 못 쓰면 <b>판이 어긋난 것</b>(그사이 다른 GIS 가 썼다)이라 충돌이다.
    /// 덮어쓰기는 <paramref name="revisionAfter"/>(다시 읽은 판)이 새 판이다.
    /// </summary>
    public static FenceLayoutSaveResult ResultOf(FenceLayoutSavePlan plan, int loadedRevision, int affected, int? revisionAfter = null)
    {
        if (plan.Kind == FenceLayoutSaveKind.Overwrite)
            return revisionAfter is { } after && after > 0
                ? new FenceLayoutSaveResult(FenceLayoutSaveStatus.Saved, after, OVERWRITTEN)
                : new FenceLayoutSaveResult(FenceLayoutSaveStatus.Failed, loadedRevision, FAILED);
        return affected > 0
            ? new FenceLayoutSaveResult(FenceLayoutSaveStatus.Saved, plan.NextRevision ?? loadedRevision + 1, SAVED)
            : new FenceLayoutSaveResult(FenceLayoutSaveStatus.Conflict, loadedRevision, CONFLICT);
    }

    public const string SAVED = "펜스 구성을 이 PC 에 저장했습니다.";
    public const string OVERWRITTEN = "펜스 구성을 이 PC 에 덮어 저장했습니다(읽지 못한 이전 본문은 보관했습니다).";
    public const string CONFLICT = "다른 GIS 가 이 제어기의 펜스 구성을 먼저 저장했습니다 — 창을 다시 열어 확인하세요.";
    public const string FAILED = "펜스 구성을 로컬 DB 에 저장하지 못했습니다.";
}
