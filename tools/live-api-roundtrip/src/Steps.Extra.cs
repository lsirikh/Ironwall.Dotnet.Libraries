// 확장 점검 등록부 — 여러 작업자가 Steps.Core.cs 를 동시에 고치지 않고 점검을 더하기 위한 자리.
//
// 쓰는 법:
//   1) 새 파일 Steps.<주제>.cs 에 `public static partial class Steps` 로 메서드를 하나 둔다.
//   2) 시그니처는 `static Task Name(ExtraContext ctx)`, 위에 [ExtraStep("주제키", order)] 를 단다.
//   3) 끝. RunAll 이 정리 스윕 직전에 order → key 순으로 부른다.
//
// 빠르게 자기 것만 돌리기: 환경변수 LRT_ONLY=키1,키2  (예: LRT_ONLY=server-console)
//   → 기본 점검(item 2~11 · 조치 문구)은 건너뛰고 지정한 확장 점검만 돈다.
//   → 로그인 · 판본 프로브 · 부대 범위 준비 · 정리 스윕(item 9)은 언제나 돈다 — 스윕을 건너뛰면
//     잔여물이 "확인 안 됨" 이 아니라 "없음" 으로 보이는 D-17 구멍이 다시 열린다.
//
// 한 점검이 던지면 그 점검만 BLOCKED 로 적고 다음으로 간다(하네스 결함 ≠ 제품 결함).
using System.Reflection;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;

namespace LiveApiRoundTrip;

/// <summary>확장 점검이 받는 공용 준비물 — RunAll 이 이미 만든 것을 그대로 넘긴다.</summary>
public sealed record ExtraContext(
    Bootstrap Boot,
    Recorder Rec,
    Raw Raw,
    DeviceApiService DeviceApi,
    UnitApiService UnitApi,
    DeviceQueryPolicy Policy);

/// <summary>RunAll 이 자동으로 부르는 확장 점검 표시.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExtraStepAttribute : Attribute
{
    public ExtraStepAttribute(string key, int order = 0)
    {
        Key = key;
        Order = order;
    }

    public string Key { get; }
    public int Order { get; }
}

public static partial class Steps
{
    /// <summary>LRT_ONLY 에 적힌 키 집합. 비어 있으면 null(= 전부).</summary>
    static HashSet<string>? OnlyKeys()
    {
        var raw = Environment.GetEnvironmentVariable("LRT_ONLY");
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return new HashSet<string>(
            raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>기본 점검(item 2~11 · 조치 문구)을 돌릴지 — LRT_ONLY 가 없을 때만.</summary>
    static bool RunCoreItems => OnlyKeys() is null;

    static async Task RunExtraSteps(ExtraContext ctx)
    {
        var only = OnlyKeys();
        var steps = typeof(Steps)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(m => (Method: m, Attr: m.GetCustomAttribute<ExtraStepAttribute>()))
            .Where(x => x.Attr is not null)
            .OrderBy(x => x.Attr!.Order)
            .ThenBy(x => x.Attr!.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var (method, attr) in steps)
        {
            if (only is not null && !only.Contains(attr!.Key)) continue;
            Console.WriteLine($"== extra: {attr!.Key} ({method.Name}) ==");
            try
            {
                if (method.Invoke(null, new object[] { ctx }) is Task task)
                    await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var inner = ex is TargetInvocationException tie && tie.InnerException is not null ? tie.InnerException : ex;
                ctx.Rec.Add("x." + attr.Key, attr.Key, "extra step crashed", Verdict.BLOCKED, inner.ToString(),
                    blocked: "harness step threw - NOT a product defect unless proven");
            }
        }
    }
}
