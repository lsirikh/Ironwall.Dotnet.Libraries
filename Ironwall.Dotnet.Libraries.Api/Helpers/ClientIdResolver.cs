using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Ironwall.Dotnet.Libraries.Api.Helpers;

/// <summary>
/// 서버 세션 축(<c>X-Client-Id</c> · 로그인 본문 <c>client_id</c>)의 <b>단일 정본 생성기</b>.
///
/// <para><b>왜 필요한가</b> — 서버는 세션을 <c>(user_id, client_id)</c> 로 가른다.
/// 관리자가 <c>session_self_replace_enabled</c> 를 켜면 <b>같은 client_id</b> 의 옛 세션을 끊어
/// 비정상 종료로 남은 자기 유령 세션을 회수해 준다(우리가 원하는 동작).
/// 그러나 값이 <b>전 PC 공통</b>이면 "자기 옛 세션" 이 아니라 <b>남의 현재 세션</b>을 끊어
/// 관제석끼리 무한 축출한다(서버 <c>auth.py</c> 주석의 ★ 경고와 같은 함정).</para>
///
/// <para><b>정본은 설치 시 만든 고유값이다</b> — 설치기(FR-20)가
/// <c>gis-</c> + GUID 12자를 만들어 <c>%ProgramData%\Ironwall\Gis\client-id</c> 에 적고
/// <c>appsettings.json</c> 의 <c>ClientId</c> 로도 주입한다. 설치 폴더 밖이라 재설치·업그레이드에도
/// 같은 값이 유지된다.</para>
///
/// <para><b>이 클래스가 메우는 구멍</b> — 설치기를 타지 않은 실행본(개발 빌드 · FR-20 이전 옛 설치본)은
/// 설정에 값이 없거나 <b>옛 공용값</b>(<c>central-ui</c> · <c>gis-monitoring</c>)을 쓴다.
/// 그 경우 이 클래스가 같은 파일을 읽고, 파일도 없으면 <b>직접 만들어</b> 쓴다 — 사람이 손댈 일이 없다.</para>
///
/// <para><b>판정 순서</b>:
/// ① 설정값이 유효하고 옛 공용값이 아니면 그대로 쓴다(관리자 지정 존중)
/// ② 아니면 <c>%ProgramData%</c> 파일을 읽는다
/// ③ 파일도 없으면 새로 만들어 적는다
/// ④ 파일 접근이 막히면 프로세스 수명 동안만 쓰는 임시값을 만든다(로그 경고).</para>
///
/// <para><b>알려진 한계</b> — 디스크 이미지 복제로 배포하면 <c>%ProgramData%</c> 의 파일까지 복제되어
/// 여러 관제석이 같은 값을 갖는다. 그 감지는 설치기 몫이며 별도 과제다.</para>
/// </summary>
public static class ClientIdResolver
{
    /// <summary>서버 검증식. 위반값은 서버가 422 가 아니라 <b>무시</b>한다(로그인 가용성 우선).</summary>
    public const string Pattern = "^[A-Za-z0-9._:-]{1,64}$";

    /// <summary>서버 패턴 상한.</summary>
    private const int MaxLength = 64;

    /// <summary>생성값 접두 — 설치기 <c>NewStationId</c> 와 같은 규약.</summary>
    private const string Prefix = "gis-";

    /// <summary>
    /// <b>전 PC 공통이라 세션 축으로 쓸 수 없는 값들.</b>
    /// <c>central-ui</c> 는 라이브러리 하드코딩 폴백, <c>gis-monitoring</c> 은 설치기가
    /// 옛 공용 기본값으로 명시 취급하는 값이다(<c>Pages.iss</c> FR-20).
    /// </summary>
    private static readonly string[] LegacySharedIds = { "central-ui", "gis-monitoring" };

    private static readonly object _gate = new();
    private static string? _cached;

    /// <summary>
    /// 이 설치 인스턴스의 <c>client_id</c>. 프로세스 수명 동안 불변.
    /// </summary>
    /// <param name="configuredId">설정(appsettings)에서 온 값. 비어 있거나 옛 공용값이면 무시된다.</param>
    /// <param name="warn">진단 문구 수신자. 로거가 없으면 <c>null</c>.</param>
    /// <remarks>
    /// <b>호출 순서에 안전하다.</b> 설정값을 모르는 호출부(<c>ClientIdentity.Current</c>)가 먼저 와도
    /// 뒤에 오는 <b>유효한 설정값이 항상 이긴다</b> — 먼저 온 쪽이 캐시를 선점해 관리자 지정값을
    /// 삼키는 일이 없다. 반대로 설정값이 비었거나 옛 공용값이면 이미 정해진 값을 그대로 쓴다.
    /// </remarks>
    public static string Resolve(string? configuredId, Action<string>? warn = null)
    {
        lock (_gate)
        {
            var configured = configuredId?.Trim();
            var configuredIsAuthoritative = IsWellFormed(configured) && !IsLegacyShared(configured);

            if (_cached is null)
                return _cached = Build(configuredId, warn);

            // 캐시가 이미 있는데 뒤늦게 유효한 설정값이 왔고 값이 다르다 →
            // 관리자 지정이 이긴다. 선점한 폴백값을 바로잡고 그 사실을 남긴다.
            if (configuredIsAuthoritative && !string.Equals(_cached, configured, StringComparison.Ordinal))
            {
                warn?.Invoke($"[ClientId] 설정값 '{configured}' 으로 바로잡습니다(앞서 '{_cached}' 로 정해져 있었습니다).");
                _cached = configured!;
            }

            return _cached;
        }
    }

    /// <summary>테스트·진단용 — 캐시를 비운다.</summary>
    internal static void ResetCache()
    {
        lock (_gate) { _cached = null; }
    }

    /// <summary>값이 서버 패턴을 만족하는가.</summary>
    public static bool IsWellFormed(string? value)
        => !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, Pattern);

    /// <summary>값이 전 PC 공통인 옛 값인가 — 세션 축으로 쓰면 상호 축출이 난다.</summary>
    public static bool IsLegacyShared(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        foreach (var legacy in LegacySharedIds)
            if (string.Equals(value.Trim(), legacy, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>설치 고유값 파일 경로 — 설치기 <c>StationIdFilePath</c> 와 같은 자리.</summary>
    public static string InstallIdFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Ironwall", "Gis", "client-id");

    /// <summary>테스트가 단계를 하나씩 검증할 수 있게 분리한 순수 조립부.</summary>
    internal static string Build(string? configuredId, Action<string>? warn = null)
    {
        // ① 관리자가 지정한 유효값은 존중한다 — 설치기가 주입한 값이 여기로 온다.
        var configured = configuredId?.Trim();
        if (IsWellFormed(configured) && !IsLegacyShared(configured))
            return configured!;

        if (IsLegacyShared(configured))
            warn?.Invoke($"[ClientId] 설정값 '{configured}' 은 전 PC 공통이라 세션 축으로 쓸 수 없습니다 — 설치 고유값으로 대체합니다.");
        else if (!string.IsNullOrWhiteSpace(configured))
            warn?.Invoke($"[ClientId] 설정값 '{configured}' 이 서버 패턴({Pattern})에 어긋나 설치 고유값으로 대체합니다.");

        // ②③ 설치 고유값 파일 — 없으면 만든다.
        var fromFile = ReadOrCreateInstallId(warn);
        if (IsWellFormed(fromFile))
            return fromFile!;

        // ④ 파일을 쓸 수도 읽을 수도 없다 — 프로세스 수명 동안만 쓰는 값.
        //    세션 축이 실행마다 달라져 유령 세션 회수가 안 되므로 반드시 경고를 남긴다.
        var volatileId = NewId();
        warn?.Invoke($"[ClientId] 고유값 파일을 쓸 수 없어 임시값 '{volatileId}' 을 씁니다 — " +
                     $"재시작마다 바뀌어 유령 세션이 누적됩니다. '{InstallIdFilePath}' 쓰기 권한을 확인하십시오.");
        return volatileId;
    }

    private static string? ReadOrCreateInstallId(Action<string>? warn)
    {
        var path = InstallIdFilePath;

        try
        {
            if (File.Exists(path))
            {
                var raw = File.ReadAllText(path, Encoding.UTF8);
                var cleaned = Sanitize(raw);
                if (IsWellFormed(cleaned) && !IsLegacyShared(cleaned))
                    return cleaned;

                // 손으로 고쳐 깨진 파일 · 옛 공용값이 적힌 파일은 새 값으로 갈아 준다.
                warn?.Invoke($"[ClientId] '{path}' 의 값이 쓸 수 없어 새로 만듭니다.");
            }
        }
        catch (Exception ex)
        {
            warn?.Invoke($"[ClientId] '{path}' 읽기 실패({ex.GetType().Name}) — 새로 만들기를 시도합니다.");
        }

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var id = NewId();
            File.WriteAllText(path, id, new UTF8Encoding(false));
            warn?.Invoke($"[ClientId] 설치 고유값을 새로 만들었습니다: '{id}' → '{path}'");
            return id;
        }
        catch (Exception ex)
        {
            warn?.Invoke($"[ClientId] '{path}' 쓰기 실패({ex.GetType().Name}).");
            return null;
        }
    }

    /// <summary>설치기 <c>NewStationId</c> 와 같은 모양 — <c>gis-</c> + GUID 앞 12자(소문자 16진).</summary>
    private static string NewId()
        => Prefix + Guid.NewGuid().ToString("N").Substring(0, 12);

    /// <summary>
    /// 파일에서 읽은 문자열을 서버 패턴에 맞게 다듬는다 —
    /// BOM·공백·줄바꿈 등 앞뒤 잡문자를 걷고, 허용 외 문자는 버린다(설치기 판정과 같은 취지).
    /// </summary>
    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            var ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
                     || c == '.' || c == '_' || c == ':' || c == '-';
            if (ok) sb.Append(c);
        }

        var s = sb.ToString().Trim('-');
        return s.Length <= MaxLength ? s : s.Substring(0, MaxLength);
    }
}
