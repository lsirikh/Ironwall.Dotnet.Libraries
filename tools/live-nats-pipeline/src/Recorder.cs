using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace LiveNatsPipeline;

public enum Verdict { PASS, FAIL, INFO, BLOCKED }

public sealed class Result
{
    public string Id = "";
    public string Scenario = "";
    public string Expected = "";
    public string Actual = "";
    public Verdict Verdict;
    public string Evidence = "";
    public string Owner = "";   // WP-1 (Events.Ui / host) · WP-2 (GMaps.Ui) · spec · harness
    public double Ms;
}

public sealed class Recorder
{
    public readonly List<Result> Results = new();
    public readonly List<string> Notes = new();

    public Result Add(string id, string scenario, string expected, string actual, Verdict v, string evidence = "", string owner = "", double ms = 0)
    {
        var r = new Result { Id = id, Scenario = scenario, Expected = expected, Actual = actual, Verdict = v, Evidence = evidence, Owner = owner, Ms = ms };
        lock (Results) Results.Add(r);
        var mark = v switch { Verdict.PASS => "[PASS]", Verdict.FAIL => "[FAIL]", Verdict.INFO => "[INFO]", _ => "[BLOCKED]" };
        Console.WriteLine($"{mark} {id} {scenario}\n        expected: {expected}\n        actual  : {actual}");
        if (evidence.Length > 0) Console.WriteLine($"        evidence: {Trunc(evidence, 600)}");
        return r;
    }

    public static string Trunc(string s, int n) => s is null ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…");

    static string Cell(string s) => (s ?? "").Replace("|", "\\|").Replace("\r", "").Replace("\n", "<br>");

    public void WriteMarkdown(string path, string header)
    {
        var sb = new StringBuilder();
        sb.AppendLine(header);
        sb.AppendLine();
        sb.AppendLine($"| 판정 | PASS | FAIL | INFO | BLOCKED |");
        sb.AppendLine($"|---|---|---|---|---|");
        sb.AppendLine($"| 건수 | {Results.Count(r => r.Verdict == Verdict.PASS)} | {Results.Count(r => r.Verdict == Verdict.FAIL)} | {Results.Count(r => r.Verdict == Verdict.INFO)} | {Results.Count(r => r.Verdict == Verdict.BLOCKED)} |");
        sb.AppendLine();
        sb.AppendLine("| # | 시나리오 | 기대 | 실제 | 판정 | 근거 | 담당 |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var r in Results)
            sb.AppendLine($"| {r.Id} | {Cell(r.Scenario)} | {Cell(r.Expected)} | {Cell(r.Actual)} | **{r.Verdict}** | {Cell(Trunc(r.Evidence, 900))} | {Cell(r.Owner)} |");
        if (Notes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## 실행 메모");
            foreach (var n in Notes) sb.AppendLine($"- {n}");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    public void WriteJson(string path, object meta)
    {
        var payload = new { meta, results = Results.Select(r => new { r.Id, r.Scenario, r.Expected, r.Actual, verdict = r.Verdict.ToString(), r.Evidence, r.Owner, r.Ms }), notes = Notes };
        File.WriteAllText(path, JsonConvert.SerializeObject(payload, Formatting.Indented), new UTF8Encoding(true));
    }
}
