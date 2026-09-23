using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace LiveApiRoundTrip;

public enum Verdict { PASS, FAIL, BLOCKED, INFO }

public sealed class Check
{
    public string Id = "";
    public string Item = "";
    public string Title = "";
    public Verdict Verdict = Verdict.BLOCKED;
    public string Detail = "";
    /// <summary>For FAIL only: file:line hypothesis of the product defect.</summary>
    public string DefectAt = "";
    /// <summary>For BLOCKED only: harness/environment reason (NOT a product defect).</summary>
    public string BlockedReason = "";
    public List<int> WireSeqs = new();
}

public sealed class Recorder
{
    public readonly List<Check> Checks = new();
    public readonly System.Collections.Concurrent.ConcurrentQueue<WireExchange> Wire = new();
    public readonly List<string> CreatedIds = new();
    public readonly List<string> DeletedIds = new();
    public readonly List<string> Leftovers = new();

    public void Created(string kind, object id) { CreatedIds.Add($"{kind}:{id}"); }
    public void Deleted(string kind, object id) { DeletedIds.Add($"{kind}:{id}"); }
    public void Leftover(string kind, object id, string why) { Leftovers.Add($"{kind}:{id} ({why})"); }

    public Check Add(string id, string item, string title, Verdict v, string detail,
                     string defectAt = "", string blocked = "", params int[] seqs)
    {
        var c = new Check { Id = id, Item = item, Title = title, Verdict = v, Detail = detail,
                            DefectAt = defectAt, BlockedReason = blocked, WireSeqs = seqs.ToList() };
        Checks.Add(c);
        var mark = v switch { Verdict.PASS => "[PASS]", Verdict.FAIL => "[FAIL]", Verdict.BLOCKED => "[BLOCKED]", _ => "[INFO]" };
        Console.WriteLine($"{mark} {id} {title} :: {detail}");
        if (defectAt.Length > 0) Console.WriteLine($"         defect@ {defectAt}");
        if (blocked.Length > 0) Console.WriteLine($"         blocked: {blocked}");
        return c;
    }

    public int LastSeq() { return Wire.Count; }

    public IEnumerable<WireExchange> Since(int seqExclusive) => Wire.Where(w => w.Seq > seqExclusive).OrderBy(w => w.Seq);

    public void WriteJson(string path, string serverInfo)
    {
        var payload = new
        {
            generated_at = DateTimeOffset.Now.ToString("o"),
            server = serverInfo,
            checks = Checks.Select(c => new {
                c.Id, c.Item, c.Title, verdict = c.Verdict.ToString(), c.Detail, c.DefectAt, c.BlockedReason, c.WireSeqs }),
            cleanup = new { created = CreatedIds, deleted = DeletedIds, leftovers = Leftovers },
            wire = Wire.OrderBy(w => w.Seq).Select(w => new {
                w.Seq, w.Tag, w.Method, w.Uri, w.Status,
                request = w.RequestBodyRedacted, response = Trunc(w.ResponseBodyRedacted, 2000) }),
        };
        File.WriteAllText(path, JsonConvert.SerializeObject(payload, Formatting.Indented), new UTF8Encoding(true));
    }

    public static string Trunc(string s, int n) => s is null ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…<truncated>");
}
