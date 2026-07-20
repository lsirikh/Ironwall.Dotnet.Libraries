using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Services;

/// <summary>
/// <see cref="IPermissionService"/> 구현. UI/배경 스레드 공유라 <c>_gate</c> lock 직렬화, 이벤트는 lock 밖 발화(재진입 방지).
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly object _gate = new();
    private readonly IClock _clock;
    private EnumUserRole _role = EnumUserRole.UNDEFINED;
    private HashSet<string> _tokens = new(StringComparer.OrdinalIgnoreCase);
    private List<int> _deviceGroups = new();
    private string? _loginId;
    private string? _name;
    private DateTimeOffset? _validUntil;
    private DateTimeOffset? _serverTime;
    private TimeSpan _clockSkew = TimeSpan.Zero;

    public event Action? PermissionsChanged;

    // Autofac: (IClock) 가 해소 가능하면 주입, 아니면 파라미터리스로 우아하게 폴백(SystemClock). 테스트는 new PermissionService().
    public PermissionService() : this(new SystemClock()) { }
    public PermissionService(IClock clock) => _clock = clock ?? new SystemClock();

    public EnumUserRole Role { get { lock (_gate) return _role; } }
    public bool IsAdmin { get { lock (_gate) return _role == EnumUserRole.ADMIN; } }
    public string? LoginId { get { lock (_gate) return _loginId; } }
    public string? Name    { get { lock (_gate) return _name; } }
    public bool HasRole(EnumUserRole required) { lock (_gate) return _role >= required; }
    // ADR v5.2: 감사 접근 = `audit_logs:view` 매트릭스로 판정(role 라벨 아님). ADMIN은 Has() bypass로 통과.
    //   (이전 role(ADMIN|MAINTAINER) 기반 → v5.2 위반이라 매트릭스 기반으로 교체. ADMIN/MAINTAINER 그룹에 audit_logs:view=true 확인됨.)
    public bool CanAccessAuditLogs() => CanView("audit_logs");

    // flat 토큰: "rw" → r/w, nested → view/edit/control/delete. 긴 형태 또는 짧은 형태 둘 다 수용.
    public bool CanView(string module)    => Has(module, "view", "r");
    public bool CanEdit(string module)    => Has(module, "edit", "w");
    public bool CanControl(string module) => Has(module, "control", "c");
    public bool CanDelete(string module)  => Has(module, "delete", "d");

    public bool HasDeviceGroup(int id) { lock (_gate) return _deviceGroups.Contains(id); }
    public IReadOnlyList<int> GetAccessibleDeviceGroups() { lock (_gate) return _deviceGroups.ToArray(); }

    // ── Grant Scheduling v5.2 (FR-GS-01) ──
    public DateTimeOffset? ValidUntil { get { lock (_gate) return _validUntil; } }
    public DateTimeOffset? ServerTime { get { lock (_gate) return _serverTime; } }
    public TimeSpan ClockSkew { get { lock (_gate) return _clockSkew; } }

    public void Apply(AuthUserDto user)
    {
        lock (_gate)
        {
            _role = RoleMappingHelper.ParseRole(user.Role);
            _tokens = new HashSet<string>(PermissionsFlattener.Flatten(user.Permissions), StringComparer.OrdinalIgnoreCase);
            _deviceGroups = ExtractDeviceGroups(user.Permissions);
            _loginId = user.LoginId;
            _name = user.Name;   // 표시이름(회전요청 등 requested_by 소스). 로그인 응답에만 존재.
            // FR-GS-01: 로그인 응답 permissions 에 valid_until 이 있으면 캐시(server_time 은 login 응답에 없어 clockSkew 는 첫 Refresh 에서 보정).
            _validUntil = ParseOffset(user.Permissions?["valid_until"]?.Value<string>());
        }
        PermissionsChanged?.Invoke();
    }

    // FR-GS-02: 재조회 스냅샷(GET /me/permissions)으로 권한·valid_until·clockSkew 갱신. role/loginId/name 은 유지(스냅샷에 없음).
    public void Refresh(PermissionsSnapshotDto snapshot)
    {
        lock (_gate)
        {
            _tokens = new HashSet<string>(PermissionsFlattener.Flatten(snapshot.Modules), StringComparer.OrdinalIgnoreCase);
            _deviceGroups = snapshot.DeviceGroups is { } dg ? new List<int>(dg) : new List<int>();
            _validUntil = ParseOffset(snapshot.ValidUntil);
            _serverTime = ParseOffset(snapshot.ServerTime);
            // clockSkew = server_time − localNow(UTC instant 비교로 로컬 오프셋 무관). server_time 없으면 보정 안 함(0).
            _clockSkew = _serverTime is { } st ? st.UtcDateTime - _clock.UtcNow : TimeSpan.Zero;
        }
        PermissionsChanged?.Invoke();
    }

    public void Clear()
    {
        lock (_gate)
        {
            _role = EnumUserRole.UNDEFINED;
            _tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _deviceGroups = new List<int>();
            _loginId = null;
            _name = null;
            _validUntil = null;
            _serverTime = null;
            _clockSkew = TimeSpan.Zero;
        }
        PermissionsChanged?.Invoke();
    }

    private bool Has(string module, string longVerb, string shortVerb)
    {
        lock (_gate)
        {
            // ADMIN 무조건 통과 — 서버가 ADMIN permissions 를 빈 객체로 보내므로 토큰만 보면 ADMIN 이 차단됨(OQ-PG-01 Option A).
            if (_role == EnumUserRole.ADMIN) return true;
            return _tokens.Contains($"{module}:{longVerb}") || _tokens.Contains($"{module}:{shortVerb}");
        }
    }

    private static List<int> ExtractDeviceGroups(JObject? permissions)
    {
        var result = new List<int>();
        if (permissions?["device_groups"] is JArray arr)
            foreach (var t in arr)
                if (t.Type == JTokenType.Integer)
                    result.Add((int)t);
        return result;
    }

    /// <summary>ISO8601 offset 문자열(KST +09:00 등)을 <see cref="DateTimeOffset"/> 로 관용 파싱. null/공백/실패=null.</summary>
    private static DateTimeOffset? ParseOffset(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var dto) ? dto : null;
    }
}
