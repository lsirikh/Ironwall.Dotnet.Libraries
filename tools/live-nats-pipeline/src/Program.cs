using System.IO;
using System.Text;
using System.Windows;

namespace LiveNatsPipeline;

/// <summary>
/// WP-5 headless live NATS pipeline probe.
///
/// Run (library working tree + host working tree):
///   dotnet run --project tools\live-nats-pipeline\LiveNatsPipeline.csproj -- --label wip
/// Run against a frozen snapshot (e.g. `git archive HEAD` extracted to a scratch dir):
///   dotnet run --project tools\live-nats-pipeline\LiveNatsPipeline.csproj -p:LibRoot=X:\libsnap\ -p:HostRoot=X:\hostsnap\ -- --label head
/// --real : device inventory from the loopback test server (GET only, ONE login with the dedicated account in
///          env LNP_CRED_FILE id=/pw= lines — admin is refused; writes refused by ReadOnlyGate) + R-series scenarios.
/// Options: --only S01,S05 (prefix match)  ·  env LNP_NATS_URL (default nats://127.0.0.1:4222, loopback enforced)
///          env LNP_REPORT (markdown path; default &lt;repo&gt;\docs\tests\event-live-pipeline-probe-log.md)
///
/// Isolation: everything is sensorway.unit999.* (Safety.cs). Default mode: no REST traffic at all (FakeGopServer.cs).
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var label = Arg(args, "--label") ?? "run";
        var only = (Arg(args, "--only") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        var toolDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var repoRoot = Path.GetFullPath(Path.Combine(toolDir, "..", ".."));
        var outDir = Path.Combine(toolDir, "out", label);
        Directory.CreateDirectory(outDir);
        var natsUrl = Environment.GetEnvironmentVariable("LNP_NATS_URL") ?? "nats://127.0.0.1:4222";
        var report = Environment.GetEnvironmentVariable("LNP_REPORT")
                     ?? Path.Combine(repoRoot, "docs", "tests", "event-live-pipeline-probe-log.md");

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int exit = 3;
        app.Dispatcher.InvokeAsync(async () =>
        {
            try { exit = await Runner.RunAsync(label, only, outDir, natsUrl, report, args.Contains("--real")); }
            catch (Exception ex) { Console.WriteLine("HARNESS CRASH: " + ex); exit = 2; }
            finally { app.Shutdown(); }
        });
        app.Run();
        return exit;
    }

    static string? Arg(string[] a, string name)
    {
        var i = Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }
}
