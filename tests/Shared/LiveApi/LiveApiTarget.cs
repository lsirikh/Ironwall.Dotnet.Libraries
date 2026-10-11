using System.IO;
using System.Net;
using System.Net.Sockets;
using Ironwall.Dotnet.Libraries.Api.Models;

namespace Ironwall.Dotnet.Libraries.Tests.LiveApi;

/// <summary>
/// 라이브 API 통합 시험의 <b>대상 서버 · 전용 계정 · 안전 문</b> — Devices.Api · Events.Api 가 링크로 같이 쓴다.
/// <para>
/// <b>주소</b>: 환경변수 <c>IRONWALL_LIVE_API_URL</c>(없으면 <c>https://localhost:8000/api</c>).
/// 서버는 HTTPS 다 — 옛 시험이 <c>http://localhost:8000/api</c> 로 고정돼 서버에 닿지 못했다.
/// </para>
/// <para>
/// <b>계정</b>: <c>IRONWALL_E2E_CRED_FILE</c> 이 가리키는 파일의 <c>id=</c> · <c>pw=</c> 두 줄(전용 주입 계정).
/// 값은 어디에도 출력하지 않는다(건너뜀 사유에도 경로만 쓴다).
/// </para>
/// <para>
/// <b>건너뜀</b>: 파일이 없거나 서버가 닿지 않으면 실패가 아니라 건너뜀이다(<see cref="LiveApiFactAttribute"/>).
/// <b>쓰기 시험</b>은 주소가 loopback · 사설망(RFC1918) IP 일 때만 돈다 — 원격 운영 주소는 네트워크에 닿기 전에 건너뛴다
/// (<see cref="LiveApiWriteFactAttribute"/>).
/// </para>
/// </summary>
internal static class LiveApiTarget
{
    public const string UrlVariable = "IRONWALL_LIVE_API_URL";
    public const string CredentialFileVariable = "IRONWALL_E2E_CRED_FILE";
    public const string DefaultUrl = "https://localhost:8000/api";

    /// <summary>TCP 닿음 확인 제한 시간 — 시험 발견 단계에서 1회만 잰다.</summary>
    private static readonly TimeSpan ReachTimeout = TimeSpan.FromSeconds(2);

    /// <summary>대상 API 주소(끝 슬래시는 그대로 둔다 — <c>ApiService</c> 가 정규화한다).</summary>
    public static string Url
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable(UrlVariable);
            return string.IsNullOrWhiteSpace(raw) ? DefaultUrl : raw.Trim();
        }
    }

    /// <summary>읽기 시험의 건너뜀 사유. 돌릴 수 있으면 <c>null</c>. 한 번 재고 프로세스 내내 쓴다.</summary>
    public static string? ReadSkipReason => _readSkip.Value;

    /// <summary>쓰기 시험의 건너뜀 사유. 안전 문(원격 금지)을 <b>먼저</b> 본다 — 원격이면 네트워크에 닿지 않는다.</summary>
    public static string? WriteSkipReason => WriteGuardReason(Url) ?? ReadSkipReason;

    private static readonly Lazy<string?> _readSkip = new(ComputeReadSkipReason, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>시험용 접속 설정 — 계정은 싣지 않는다(로그인은 <see cref="LiveApiSession"/> 이 전용 계정으로 한다).</summary>
    public static ApiSetupModel CreateSetup() => new() { Url = Url, Timeout = 30 };

    /// <summary>전용 계정(id · pw). 파일이 없거나 형식이 틀리면 예외 — 건너뜀 판정을 먼저 거친 뒤에만 부른다.</summary>
    public static (string Id, string Password) ReadCredentials()
    {
        if (!TryReadCredentials(out var id, out var pw, out var problem))
            throw new InvalidOperationException(problem);
        return (id!, pw!);
    }

    /// <summary>
    /// 쓰기를 허용하는 주소인가 — 호스트가 <c>localhost</c> · loopback IP · RFC1918 사설 IP 일 때만 <c>true</c>.
    /// 그 밖의 호스트 이름은 이름 해석 없이 원격으로 본다(보수적).
    /// </summary>
    public static bool IsWriteAllowedHost(Uri uri)
    {
        var host = uri.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;

        if (!IPAddress.TryParse(host.Trim('[', ']'), out var ip)) return false;
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;

        var b = ip.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168);
    }

    /// <summary>쓰기 안전 문 — 허용 주소면 <c>null</c>, 아니면 건너뜀 사유.</summary>
    public static string? WriteGuardReason(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return $"{UrlVariable} 가 절대 주소가 아닙니다 — 라이브 API 시험 건너뜀";
        return IsWriteAllowedHost(uri)
            ? null
            : $"쓰기 시험은 loopback · 사설망(RFC1918) 주소에서만 돈다 — '{uri.Host}' 는 원격(운영)으로 보고 건너뜀";
    }

    private static string? ComputeReadSkipReason()
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri))
            return $"{UrlVariable} 가 절대 주소가 아닙니다 — 라이브 API 시험 건너뜀";

        if (!TryReadCredentials(out _, out _, out var problem))
            return problem;

        return Reachable(uri) ? null : $"서버에 닿지 않습니다({uri.Host}:{uri.Port}) — 라이브 API 시험 건너뜀";
    }

    private static bool TryReadCredentials(out string? id, out string? password, out string problem)
    {
        id = null;
        password = null;
        problem = string.Empty;

        var path = Environment.GetEnvironmentVariable(CredentialFileVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            problem = $"{CredentialFileVariable} 미설정 — 전용 계정이 없어 라이브 API 시험 건너뜀";
            return false;
        }
        if (!File.Exists(path))
        {
            problem = $"{CredentialFileVariable} 가 가리키는 파일이 없습니다 — 라이브 API 시험 건너뜀";
            return false;
        }

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.StartsWith("id=", StringComparison.OrdinalIgnoreCase)) id = line.Substring(3).Trim();
            else if (line.StartsWith("pw=", StringComparison.OrdinalIgnoreCase)) password = line.Substring(3).Trim();
        }

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(password))
        {
            // 값은 싣지 않는다 — 어느 줄이 빠졌는지만 알린다.
            problem = $"계정 파일에 id= · pw= 두 줄이 모두 있어야 합니다({CredentialFileVariable}) — 라이브 API 시험 건너뜀";
            return false;
        }
        return true;
    }

    /// <summary>TCP 연결만 해 본다(HTTP 요청 없음). 제한 시간 안에 붙으면 닿는다.</summary>
    private static bool Reachable(Uri uri)
    {
        try
        {
            using var tcp = new TcpClient();
            var connect = tcp.ConnectAsync(uri.Host, uri.Port);
            return connect.Wait(ReachTimeout) && tcp.Connected;
        }
        catch
        {
            return false;
        }
    }
}
