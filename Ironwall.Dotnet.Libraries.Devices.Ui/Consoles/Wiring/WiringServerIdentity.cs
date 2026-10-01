using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>
/// 지금 접속한 서버의 식별(fence-wiring-editor FR-11) — 펜스 구성 로컬 저장소 열쇠의 앞쪽(<see cref="FenceLayoutKey.Server"/>).
/// </summary>
/// <remarks>
/// <para>값은 호스트가 넘긴 API 설정(<c>IApiSetupModel.Url</c>)에서 <b>열 때마다</b> 읽는다 — 시험 서버로 바꿔 띄운
/// (<c>IRONWALL_UITEST_SERVER</c> → appsettings 의 Url 교체) 앱은 다른 열쇠를 쓰므로 운영 서버의 구성을 덮지 않는다.</para>
/// <para>정규화는 <see cref="FenceLayoutRows.ServerKeyOf"/>(<c>host:port</c> · 경로 무시). 읽다 실패하면 <see cref="FenceLayoutRows.UNKNOWN_SERVER"/>.</para>
/// </remarks>
public sealed class WiringServerIdentity
{
    private readonly Func<string?> _url;

    public WiringServerIdentity(Func<string?> url)
    {
        _url = url ?? throw new ArgumentNullException(nameof(url));
    }

    /// <summary>"host:port".</summary>
    public string Key
    {
        get
        {
            try { return FenceLayoutRows.ServerKeyOf(_url()); }
            catch (Exception) { return FenceLayoutRows.UNKNOWN_SERVER; }
        }
    }
}
