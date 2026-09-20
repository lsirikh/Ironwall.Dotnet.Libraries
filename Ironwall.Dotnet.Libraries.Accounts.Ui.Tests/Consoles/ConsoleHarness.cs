using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;
using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/****************************************************************************
   Purpose   : 계정 콘솔(N-06) 헤드리스 하네스 — 진짜 패널 뷰모델 위에 가짜 서버/게이트웨이를 붙인다.
   Notes     : Application.Current == null 이라 DispatcherService.Invoke 가 제자리에서 돈다(결정론).
                기존 FakeGopServer 는 그룹 배정 경로를 구현하지 않아(NotImplemented) 여기 별도 가짜를 둔다.
****************************************************************************/

/// <summary>권한 서비스 가짜 — 역할과 verb 허용을 시험이 정한다.</summary>
internal sealed class FakePermissionService : IPermissionService
{
    private readonly HashSet<string> _allowed = new(StringComparer.Ordinal);

    public EnumUserRole Role { get; set; } = EnumUserRole.ADMIN;
    public bool IsAdmin => Role == EnumUserRole.ADMIN;
    public string? LoginId { get; set; } = "admin";
    public string? Name { get; set; } = "관리자";
    public bool AuditAllowed { get; set; } = true;

    public void Allow(params string[] tokens)
    {
        foreach (var token in tokens) _allowed.Add(token);
    }

    public bool HasRole(EnumUserRole required) => Role >= required;
    public bool CanView(string module) => IsAdmin || _allowed.Contains($"{module}:view");
    public bool CanEdit(string module) => IsAdmin || _allowed.Contains($"{module}:edit");
    public bool CanControl(string module) => IsAdmin || _allowed.Contains($"{module}:control");
    public bool CanDelete(string module) => IsAdmin || _allowed.Contains($"{module}:delete");
    public bool HasDeviceGroup(int id) => true;
    public IReadOnlyList<int> GetAccessibleDeviceGroups() => Array.Empty<int>();
    public bool CanAccessAuditLogs() => AuditAllowed;
    public DateTimeOffset? ValidUntil => null;
    public DateTimeOffset? ServerTime => null;
    public TimeSpan ClockSkew => TimeSpan.Zero;
    public void Apply(AuthUserDto user) { }
    public void Refresh(PermissionsSnapshotDto snapshot) { }
    public void Clear() { }
    public event System.Action? PermissionsChanged;
    public void RaiseChanged() => PermissionsChanged?.Invoke();
}

/// <summary>토큰 저장소 가짜.</summary>
internal sealed class FakeTokenStore2 : ITokenStorageService
{
    public string? AccessToken => null;
    public string? RefreshToken => null;
    public DateTime? AccessExpiresAtUtc => null;
    public DateTime? RefreshExpiresAtUtc => null;
    public bool IsAuthenticated => true;
    public string? Jti => null;
    public string? UserId { get; set; } = "1";
    public string? SessionId { get; set; } = "1";
    public int Generation => 0;
    public void SetTokens(string accessToken, string? refreshToken = null, string? sessionId = null) { }
    public bool SetTokensIfGeneration(int expectedGeneration, string accessToken, string? refreshToken = null, string? sessionId = null) => true;
    public bool IsAccessTokenExpiring(TimeSpan threshold) => false;
    public void Clear() { }
    public event System.Action? TokensRenewed;
    public void RaiseRenewed() => TokensRenewed?.Invoke();
}

/// <summary>계정 디렉터리 게이트웨이 가짜 — 콘솔의 [적용] · 삭제 · 잠금 해제가 도달하는지 센다.</summary>
internal sealed class FakeDirectoryGateway : IUserDirectoryGateway
{
    public List<IAccountModel> Accounts { get; } = new();
    public int UpdateCallCount { get; private set; }
    public List<IAccountModel> Updated { get; } = new();
    public bool FailUpdate { get; set; }
    public int UnlockCallCount { get; private set; }

    public Task<List<IAccountModel>?> GetAllAccountsAsync(CancellationToken ct = default)
        => Task.FromResult<List<IAccountModel>?>(Accounts.ToList());

    public Task<IAccountModel?> CreateAccountAsync(IAccountModel acc, CancellationToken ct = default)
    {
        Accounts.Add(acc);
        return Task.FromResult<IAccountModel?>(acc);
    }

    public Task<IAccountModel?> UpdateAccountAsync(IAccountModel acc, CancellationToken ct = default)
    {
        UpdateCallCount++;
        if (FailUpdate) return Task.FromResult<IAccountModel?>(null);
        Updated.Add(acc);
        return Task.FromResult<IAccountModel?>(acc);
    }

    public Task<bool> RemoveAccountAsync(IAccountModel acc, string currentPassword, CancellationToken ct = default)
    {
        Accounts.RemoveAll(a => a.Id == acc.Id);
        return Task.FromResult(true);
    }

    public Task<bool> IsUsernameTakenAsync(string username, CancellationToken ct = default)
        => Task.FromResult(Accounts.Any(a => a.Username == username));

    public Task<IAccountModel?> ResetAccountPasswordAsync(IAccountModel acc, string newPassword, CancellationToken ct = default)
        => Task.FromResult<IAccountModel?>(acc);

    public Task<bool> UnlockAccountAsync(int id, CancellationToken ct = default)
    {
        UnlockCallCount++;
        return Task.FromResult(true);
    }
}

/// <summary>프로필 게이트웨이 · 이미지 · 세션 설정 가짜(다이얼로그 뷰모델을 세우기 위한 최소 구현).</summary>
internal sealed class FakeProfileGateway : IProfileGateway
{
    public Task<IAccountModel?> GetProfileAsync(int accountId, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(null);
    public Task<IAccountModel?> UpdateProfileAsync(IAccountModel acc, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(acc);
    public Task<IAccountModel?> ChangePasswordAsync(IAccountModel acc, string currentPassword, string newPassword, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(acc);
    public Task<string?> UploadPhotoAsync(string filePath, CancellationToken ct = default) => Task.FromResult<string?>(null);
    public Task<bool> DeletePhotoAsync(CancellationToken ct = default) => Task.FromResult(false);
}

internal sealed class FakeProfileImageService : IProfileImageService
{
    public Task<string> SaveAsync(string sourcePath, string accountKey, CancellationToken ct = default) => Task.FromResult(sourcePath);
}

internal sealed class FakeSessionConfig : ISessionConfigService
{
    public bool IsSession => true;
    public int SessionExpiration => 60;
    public string AdminResetPassword => "12345678";
    public void ApplySession(bool isSession, int expirationMinutes) { }
}

/// <summary>
/// 계정 API 가짜 — 콘솔이 실제로 쓰는 경로만 충실하게. 나머지는 빈 성공으로 답한다(팝업을 유발하지 않게).
/// </summary>
internal sealed class FakeAccountApi : IAccountApiService
{
    public List<AuthUserDto> Users { get; } = new();
    public List<UserGroupDto> Groups { get; } = new();

    /// <summary>사용자 → 그룹 배정 호출 기록(사용자 1명당 1회라는 사실을 시험이 센다).</summary>
    public List<(int UserId, int? GroupId)> AssignCalls { get; } = new();

    /// <summary>권한 저장 호출 기록(전체 교체 1회).</summary>
    public List<(int GroupId, PermissionsDto Dto)> PermissionSaves { get; } = new();

    public HashSet<int> FailAssignForUsers { get; } = new();
    public bool IgnoreAssign { get; set; }

    /// <summary>권한 저장을 거부한다(사유). null 이면 성공.</summary>
    public string? FailPermissionSave { get; set; }

    public Task<ApiResponse<AuthUserDto>> AssignUserGroupAsync(int userId, int? groupId, CancellationToken ct = default)
    {
        AssignCalls.Add((userId, groupId));
        if (FailAssignForUsers.Contains(userId))
            return Task.FromResult(ApiResponse<AuthUserDto>.CreateError("SERVER", "거부"));

        var user = Users.FirstOrDefault(u => u.Id == userId);
        if (user is not null && !IgnoreAssign) user.GroupId = groupId;
        return Task.FromResult(ApiResponse<AuthUserDto>.CreateSuccess(user ?? new AuthUserDto { Id = userId, GroupId = groupId }));
    }

    public Task<ApiResponse<UserGroupDto>> UpdateGroupPermissionsAsync(int groupId, PermissionsDto permissions, CancellationToken ct = default)
    {
        if (FailPermissionSave is not null)
            return Task.FromResult(ApiResponse<UserGroupDto>.CreateError("FORBIDDEN", FailPermissionSave));

        PermissionSaves.Add((groupId, permissions));
        var group = Groups.FirstOrDefault(g => g.Id == groupId) ?? new UserGroupDto { Id = groupId };
        group.Permissions = permissions;
        return Task.FromResult(ApiResponse<UserGroupDto>.CreateSuccess(group));
    }

    public Task<ApiListResponse<UserGroupDto>> GetUserGroupsAsync(CancellationToken ct = default)
        => Task.FromResult(new ApiListResponse<UserGroupDto> { Success = true, Data = Groups.ToList() });

    public Task<ApiListResponse<AuthUserDto>> GetUsersAsync(int page = 1, int limit = 100, CancellationToken ct = default)
        => Task.FromResult(new ApiListResponse<AuthUserDto> { Success = true, Data = Users.ToList() });

    public Task<ApiListResponse<AuthUserDto>> GetUserGroupUsersAsync(int groupId, CancellationToken ct = default)
        => Task.FromResult(new ApiListResponse<AuthUserDto> { Success = true, Data = Users.Where(u => u.GroupId == groupId).ToList() });

    public Task<ApiListResponse<UserSessionDto>> GetUserSessionsAsync(int page = 1, int limit = 100, bool? isActive = null, CancellationToken ct = default)
        => Task.FromResult(new ApiListResponse<UserSessionDto> { Success = true, Data = new List<UserSessionDto>() });

    public Task<ApiListResponse<AuditLogDto>> GetAuditLogsAsync(int page = 1, int limit = 20, string? startDate = null, string? endDate = null, CancellationToken ct = default)
        => Task.FromResult(new ApiListResponse<AuditLogDto> { Success = true, Data = new List<AuditLogDto>() });

    public Task<ApiListResponse<GrantDto>> GetAllGrantsAsync(int page = 1, int size = 20, int? userId = null, int? groupId = null, string? status = null, bool activeOnly = false, CancellationToken ct = default)
        => Task.FromResult(new ApiListResponse<GrantDto> { Success = true, Data = new List<GrantDto>() });

    // ── 콘솔이 쓰지 않는 경로 ────────────────────────────────────────────
    public Task<ApiResponse<LoginResponseDataDto>> LoginAsync(string loginId, string password, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResponse<TokenDataDto>> RefreshAsync(string refreshToken, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResponse<object>> LogoutAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<AuthUserDto?> GetMeAsync(CancellationToken ct = default) => Task.FromResult<AuthUserDto?>(null);
    public Task<ApiResponse<AuthUserDto>> CreateUserAsync(UserCreateDto dto, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResponse<AuthUserDto>> UpdateUserAsync(int id, UserUpdateDto dto, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResponse<object>> DeleteUserAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResponse<object>> ResetUserPasswordAsync(int id, string newPassword, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResponse<AuthUserDto>> GetMyProfileAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResponse<AuthUserDto>> UpdateMyProfileAsync(UserSelfUpdateDto dto, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResponse<AuthUserDto>> UploadMyPhotoAsync(string filePath, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<ApiResponse<object>> ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) => throw new NotSupportedException();
}

/// <summary>발행된 메시지를 모으는 이벤트 애그리게이터(팝업 · 재조회 통지 확인용).</summary>
internal sealed class CapturingEventAggregator : IEventAggregator
{
    public List<object> Published { get; } = new();
    public bool HandlerExistsFor(Type messageType) => false;
    public void Subscribe(object subscriber, Func<Func<Task>, Task> marshal) { }
    public void Unsubscribe(object subscriber) { }
    public Task PublishAsync(object message, Func<Func<Task>, Task> marshal, CancellationToken cancellationToken = default)
    {
        Published.Add(message);
        return Task.CompletedTask;
    }
    public IEnumerable<T> OfType<T>() => Published.OfType<T>();
}

internal sealed class SilentLog : ILogService
{
    public List<string> Errors { get; } = new();
    public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
    public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
    public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Errors.Add(msg);
    public void Debug(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
    public event EventHandler<LogEventArgs>? LogEvent;
    public void RaiseLog(LogEventArgs args) => LogEvent?.Invoke(this, args);
}

/// <summary>활성화를 시험이 직접 부를 수 있게 연 콘솔.</summary>
internal sealed class TestAccountConsole : AccountConsolePanelViewModel
{
    public TestAccountConsole(IEventAggregator ea, ILogService log, IPermissionService permission, IAccountApiService api,
                              IUserDirectoryGateway gateway, AccountManagerPanelViewModel manager, PermissionMatrixPanelViewModel matrix,
                              UserSessionPanelViewModel session, AuditLogPanelViewModel audit, AccountSetupPanelViewModel setup,
                              GrantManagementPanelViewModel grants)
        : base(ea, log, permission, api, gateway, manager, matrix, session, audit, setup, grants) { }

    public Task ActivateForTestAsync() => OnActivateAsync(CancellationToken.None);
    public Task DeactivateForTestAsync() => OnDeactivateAsync(true, CancellationToken.None);
}

/// <summary>콘솔 한 벌을 세운다.</summary>
internal static class ConsoleFixtures
{
    public static AccountModel User(int id, string username, string name, EnumUserRole role = EnumUserRole.USER,
                                    string? department = null, string? position = null, bool locked = false)
        => new()
        {
            Id = id,
            Username = username,
            Name = name,
            Role = role,
            Level = RoleMappingHelper.ToLevel(role),
            Used = EnumUsedType.USED,
            Department = department,
            Position = position,
            IsLocked = locked,
            LockReason = locked ? "로그인 5회 실패" : null,
        };

    public static (TestAccountConsole Console, FakeAccountApi Api, FakeDirectoryGateway Gateway,
                   FakePermissionService Permission, CapturingEventAggregator Events) Build(
        IEnumerable<AccountModel>? users = null, IEnumerable<UserGroupDto>? groups = null)
    {
        var events = new CapturingEventAggregator();
        var log = new SilentLog();
        var permission = new FakePermissionService();
        var api = new FakeAccountApi();
        var gateway = new FakeDirectoryGateway();
        var provider = new AccountProvider();

        foreach (var user in users ?? Array.Empty<AccountModel>())
        {
            gateway.Accounts.Add(user);
            api.Users.Add(new AuthUserDto { Id = user.Id, LoginId = user.Username, Name = user.Name });
        }
        foreach (var group in groups ?? Array.Empty<UserGroupDto>()) api.Groups.Add(group);

        var login = new LoginViewModel(events, log, new AccountModel());
        var editor = new EditorDialogViewModel(events, log, new AccountViewModel(events, log, new AccountModel()),
                                               gateway, new FakeSessionConfig(), new FakeProfileImageService(), new FakeProfileGateway());

        var manager = new AccountManagerPanelViewModel(events, log, editor, provider, login, gateway);
        var matrix = new PermissionMatrixPanelViewModel(events, log, api);
        var session = new UserSessionPanelViewModel(events, log, api, new FakeTokenStore2());
        var audit = new AuditLogPanelViewModel(events, log, api);
        var setup = new AccountSetupPanelViewModel(events, log);
        var grants = new GrantManagementPanelViewModel(events, log, api);

        var console = new TestAccountConsole(events, log, permission, api, gateway, manager, matrix, session, audit, setup, grants);
        return (console, api, gateway, permission, events);
    }
}
