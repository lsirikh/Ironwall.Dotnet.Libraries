using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace AccountsConsolePreview;

/// <summary>미리보기용 로그 — 아무 데도 쓰지 않는다.</summary>
public sealed class PreviewLog : ILogService
{
    public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
    public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
    public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
    public void Debug(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
    public event EventHandler<LogEventArgs>? LogEvent;
}

/// <summary>미리보기용 권한 — 기본은 ADMIN(레일 6개를 다 보려면 필요하다).</summary>
public sealed class PreviewPermission : IPermissionService
{
    public EnumUserRole Role { get; set; } = EnumUserRole.ADMIN;
    public bool IsAdmin => Role == EnumUserRole.ADMIN;
    public string? LoginId => "admin";
    public string? Name => "관리자";
    public bool HasRole(EnumUserRole required) => Role >= required;
    public bool CanView(string module) => true;
    public bool CanEdit(string module) => IsAdmin;
    public bool CanControl(string module) => IsAdmin;
    public bool CanDelete(string module) => IsAdmin;
    public bool HasDeviceGroup(int id) => true;
    public IReadOnlyList<int> GetAccessibleDeviceGroups() => Array.Empty<int>();
    public bool CanAccessAuditLogs() => true;
    public DateTimeOffset? ValidUntil => null;
    public DateTimeOffset? ServerTime => null;
    public TimeSpan ClockSkew => TimeSpan.Zero;
    public void Apply(AuthUserDto user) { }
    public void Refresh(PermissionsSnapshotDto snapshot) { }
    public void Clear() { }
    public event System.Action? PermissionsChanged;
    public void RaiseChanged() => PermissionsChanged?.Invoke();
}

public sealed class PreviewTokenStore : ITokenStorageService
{
    public string? AccessToken => null;
    public string? RefreshToken => null;
    public DateTime? AccessExpiresAtUtc => null;
    public DateTime? RefreshExpiresAtUtc => null;
    public bool IsAuthenticated => true;
    public string? Jti => null;
    public string? UserId => "1";
    public string? SessionId => "9000";
    public int Generation => 0;
    public void SetTokens(string accessToken, string? refreshToken = null, string? sessionId = null) { }
    public bool SetTokensIfGeneration(int expectedGeneration, string accessToken, string? refreshToken = null, string? sessionId = null) => true;
    public bool IsAccessTokenExpiring(TimeSpan threshold) => false;
    public void Clear() { }
    public event System.Action? TokensRenewed;
}

public sealed class PreviewSessionConfig : ISessionConfigService
{
    public bool IsSession => true;
    public int SessionExpiration => 60;
    public string AdminResetPassword => "12345678";
    public void ApplySession(bool isSession, int expirationMinutes) { }
}

public sealed class PreviewProfileImages : IProfileImageService
{
    public Task<string> SaveAsync(string sourcePath, string accountKey, CancellationToken ct = default) => Task.FromResult(sourcePath);
}

public sealed class PreviewProfileGateway : IProfileGateway
{
    public Task<IAccountModel?> GetProfileAsync(int accountId, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(null);
    public Task<IAccountModel?> UpdateProfileAsync(IAccountModel acc, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(acc);
    public Task<IAccountModel?> ChangePasswordAsync(IAccountModel acc, string currentPassword, string newPassword, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(acc);
    public Task<string?> UploadPhotoAsync(string filePath, CancellationToken ct = default) => Task.FromResult<string?>(null);
    public Task<bool> DeletePhotoAsync(CancellationToken ct = default) => Task.FromResult(false);
}

/// <summary>미리보기용 계정 디렉터리 — 메모리에만 산다.</summary>
public sealed class PreviewDirectory : IUserDirectoryGateway
{
    public List<IAccountModel> Accounts { get; } = new();

    public Task<List<IAccountModel>?> GetAllAccountsAsync(CancellationToken ct = default) => Task.FromResult<List<IAccountModel>?>(Accounts.ToList());
    public Task<IAccountModel?> CreateAccountAsync(IAccountModel acc, CancellationToken ct = default) { Accounts.Add(acc); return Task.FromResult<IAccountModel?>(acc); }
    public Task<IAccountModel?> UpdateAccountAsync(IAccountModel acc, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(acc);
    public Task<bool> RemoveAccountAsync(IAccountModel acc, string currentPassword, CancellationToken ct = default) { Accounts.RemoveAll(a => a.Id == acc.Id); return Task.FromResult(true); }
    public Task<bool> IsUsernameTakenAsync(string username, CancellationToken ct = default) => Task.FromResult(false);
    public Task<IAccountModel?> ResetAccountPasswordAsync(IAccountModel acc, string newPassword, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(acc);
    public Task<bool> UnlockAccountAsync(int id, CancellationToken ct = default) => Task.FromResult(true);
}

/// <summary>미리보기용 계정 API — 콘솔이 쓰는 경로만 답한다.</summary>
public sealed class PreviewApi : IAccountApiService
{
    public List<AuthUserDto> Users { get; } = new();
    public List<UserGroupDto> Groups { get; } = new();
    public List<UserSessionDto> Sessions { get; } = new();
    public List<AuditLogDto> AuditLogs { get; } = new();
    public List<GrantDto> Grants { get; } = new();

    public Task<ApiResponse<AuthUserDto>> AssignUserGroupAsync(int userId, int? groupId, CancellationToken ct = default)
    {
        var user = Users.FirstOrDefault(u => u.Id == userId);
        if (user is not null) user.GroupId = groupId;
        return Task.FromResult(ApiResponse<AuthUserDto>.CreateSuccess(user ?? new AuthUserDto { Id = userId, GroupId = groupId }));
    }

    public Task<ApiResponse<UserGroupDto>> UpdateGroupPermissionsAsync(int groupId, PermissionsDto permissions, CancellationToken ct = default)
    {
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
        => Task.FromResult(new ApiListResponse<UserSessionDto> { Success = true, Data = Sessions.Where(s => isActive is null || s.IsActive == isActive).ToList() });
    public Task<ApiListResponse<AuditLogDto>> GetAuditLogsAsync(int page = 1, int limit = 20, string? startDate = null, string? endDate = null, CancellationToken ct = default)
        => Task.FromResult(new ApiListResponse<AuditLogDto> { Success = true, Data = AuditLogs.ToList() });
    public Task<ApiListResponse<GrantDto>> GetAllGrantsAsync(int page = 1, int size = 20, int? userId = null, int? groupId = null, string? status = null, bool activeOnly = false, CancellationToken ct = default)
        => Task.FromResult(new ApiListResponse<GrantDto> { Success = true, Data = Grants.ToList() });
    public Task<ApiResponse<SessionSettingsDto>> GetSessionSettingsAsync(CancellationToken ct = default)
        => Task.FromResult(ApiResponse<SessionSettingsDto>.CreateSuccess(new SessionSettingsDto()));

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
