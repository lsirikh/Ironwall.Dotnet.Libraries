using System.IO;
using System.Text;

namespace LiveApiRoundTrip;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var t = Run(args);
        t.Wait();
        return t.Result;
    }

    static async Task<int> Run(string[] args)
    {
        var rec = new Recorder();
        var boot = new Bootstrap(rec);
        string serverInfo = "(unresolved)";
        try
        {
            await boot.InitAsync();
            Console.WriteLine($"== base url: {Bootstrap.BASE_URL} (loopback asserted) ==");

            if (!await boot.LoginAsync())
            {
                rec.Add("00", "bootstrap", "login", Verdict.BLOCKED, "login failed",
                        blocked: "could not authenticate against the local server");
                Dump(rec, serverInfo);
                return 2;
            }
            Console.WriteLine("== logged in ==");

            serverInfo = await Steps.RunAll(boot, rec);
        }
        catch (Exception ex)
        {
            rec.Add("99", "harness", "unhandled", Verdict.BLOCKED, ex.ToString(),
                    blocked: "harness crashed - NOT a product defect unless proven");
            Console.WriteLine(ex);
        }
        finally
        {
            Dump(rec, serverInfo);
        }
        return rec.Checks.Any(c => c.Verdict == Verdict.FAIL) ? 1 : 0;
    }

    static void Dump(Recorder rec, string serverInfo)
    {
        var dir = AppContext.BaseDirectory;
        var outDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "..", "..", ".."));
        var json = System.IO.Path.Combine(outDir, "results.json");
        rec.WriteJson(json, serverInfo);
        Console.WriteLine($"\n== results.json -> {json} ==");
        Console.WriteLine($"== PASS {rec.Checks.Count(c => c.Verdict == Verdict.PASS)} / " +
                          $"FAIL {rec.Checks.Count(c => c.Verdict == Verdict.FAIL)} / " +
                          $"BLOCKED {rec.Checks.Count(c => c.Verdict == Verdict.BLOCKED)} ==");
        Console.WriteLine($"== created {rec.CreatedIds.Count}, deleted {rec.DeletedIds.Count}, leftovers {rec.Leftovers.Count} ==");
        foreach (var l in rec.Leftovers) Console.WriteLine("   LEFTOVER " + l);
    }
}
