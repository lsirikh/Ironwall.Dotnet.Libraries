using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Services;

/// <summary>
/// <see cref="IPermissionService"/> 구현. UI/배경 스레드 공유라 <c>_gate</c> lock 직렬화, 이벤트는 lock 밖 발화(재진입 방지).
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly object _gate = new();
    private EnumUserRole _role = EnumUserRole.UNDEFINED;
    private HashSet<string> _tokens = new(StringComparer.OrdinalIgnoreCase);
    private List<int> _deviceGroups = new();

    public event Action? PermissionsChanged;

    public EnumUserRole Role { get { lock (_gate) return _role; } }
    public bool IsAdmin { get { lock (_gate) return _role == EnumUserRole.ADMIN; } }
    public bool HasRole(EnumUserRole required) { lock (_gate) return _role >= required; }
    public bool CanAccessAuditLogs() { lock (_gate) return _role is EnumUserRole.ADMIN or EnumUserRole.MAINTAINER; }

    // flat 토큰: "rw" → r/w, nested → view/edit/control/delete. 긴 형태 또는 짧은 형태 둘 다 수용.
    public bool CanView(string module)    => Has(module, "view", "r");
    public bool CanEdit(string module)    => Has(module, "edit", "w");
    public bool CanControl(string module) => Has(module, "control", "c");
    public bool CanDelete(string module)  => Has(module, "delete", "d");

    public bool HasDeviceGroup(int id) { lock (_gate) return _deviceGroups.Contains(id); }
    public IReadOnlyList<int> GetAccessibleDeviceGroups() { lock (_gate) return _deviceGroups.ToArray(); }

    public void Apply(AuthUserDto user)
    {
        lock (_gate)
        {
            _role = RoleMappingHelper.ParseRole(user.Role);
            _tokens = new HashSet<string>(PermissionsFlattener.Flatten(user.Permissions), StringComparer.OrdinalIgnoreCase);
            _deviceGroups = ExtractDeviceGroups(user.Permissions);
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
}
