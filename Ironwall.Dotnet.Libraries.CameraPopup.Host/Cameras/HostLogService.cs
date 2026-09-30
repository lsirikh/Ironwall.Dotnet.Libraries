using System.Text.RegularExpressions;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Cameras;

/// <summary>
/// 제공자(OnvifSolution · PTZ 컨트롤러)가 쓰는 <see cref="ILogService"/> 를 호스트 파일 로그로 잇는다.
/// 줄마다 URL 계정(<c>//user:pass@</c>)을 가린다 — 제공자 로그가 실수로 주소를 통째로 찍어도 계정은 남지 않는다.
/// </summary>
internal sealed class HostLogService : ILogService
{
    private static readonly Regex UserInfo = new(@"(?<=://)[^/?#\s]+@", RegexOptions.Compiled);
    private readonly HostLog _log;

    public HostLogService(HostLog log) => _log = log;

    public event EventHandler<LogEventArgs>? LogEvent { add { } remove { } }

    public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => _log.Error(Mask(msg));

    public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => _log.Info(Mask(msg));

    public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => _log.Warn(Mask(msg));

    /// <summary>URL userinfo 가리기(순수).</summary>
    public static string Mask(string? text) => string.IsNullOrEmpty(text) ? string.Empty : UserInfo.Replace(text, "***@");
}
