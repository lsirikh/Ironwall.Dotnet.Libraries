using System.Diagnostics;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Watchdogs;

/// <summary>부모(GIS)가 죽으면 호스트도 내려간다 — 고아 호스트가 창 · 디코딩을 붙잡고 남지 않게.</summary>
internal static class ParentProcessWatch
{
    public static void Start(int parentProcessId, HostLog log)
    {
        Process parent;
        try
        {
            parent = Process.GetProcessById(parentProcessId);
        }
        catch (ArgumentException)
        {
            log.Error($"parent {parentProcessId} not running");
            HostExit.Now(HostExitCodes.ParentGone, "parent not running");
            return;
        }

        var thread = new Thread(() =>
        {
            try { parent.WaitForExit(); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                log.Warn($"parent wait failed: {ex.Message}");
            }
            HostExit.Now(HostExitCodes.ParentGone, "parent exited");
        })
        { IsBackground = true, Name = "parent-watch" };
        thread.Start();
    }
}
