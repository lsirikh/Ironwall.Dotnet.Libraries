namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>
/// 실패한 타일의 자동 재시도 간격(순수). 2 → 4 → 8 → 16 → 30초(상한)로 늘린다 — 죽은 카메라를 두드려 대지 않으면서,
/// 잠깐 끊긴 카메라는 곧 돌아온다. 재생이 시작되면 횟수를 0 으로 되돌린다(호출자).
/// </summary>
internal static class StreamRetryBackoff
{
    public static readonly TimeSpan First = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan Max = TimeSpan.FromSeconds(30);

    /// <param name="attempt">이번이 몇 번째 실패인가(1부터).</param>
    public static TimeSpan DelayFor(int attempt)
    {
        if (attempt <= 1) return First;
        double seconds = First.TotalSeconds * Math.Pow(2, Math.Min(attempt - 1, 10));
        return seconds >= Max.TotalSeconds ? Max : TimeSpan.FromSeconds(seconds);
    }

    /// <summary>다시 해도 소용없는 실패(제공자가 지원하지 않음 · 요청이 틀림)는 재시도하지 않는다.</summary>
    public static bool ShouldRetry(string? detail)
        => detail is not (Contracts.Protocol.CameraErrorCodes.NotSupported or Contracts.Protocol.CameraErrorCodes.BadRequest or "empty-uri");
}
