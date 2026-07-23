using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;

/****************************************************************************
   Purpose   : 권한그룹 한시부여(Grant Scheduling) 검증용 인메모리 GOP 서버 모사.
   출처(SSOT) : api-test-server/app/routers/grants.py · services/grant_service.py
                · schemas/grant.py · main.py(http_exception_handler, HTTP_ERROR_CODES).
   충실도    : 검증 규칙(until<=from / until<=now → 422 VALIDATION_ERROR),
                404 NOT_FOUND(User/User group/Grant not found), 403 FORBIDDEN(require_admin/users:view),
                파생상태 우선순위(REVOKED>PENDING>EXPIRED>ACTIVE), soft-revoke 멱등,
                GET /grants 응답 total은 top-level(클라 pagination 미매핑=Pagination null) 재현.
   비고      : IAccountApiService 를 직접 구현(VM은 GOP 모드에서 서비스를 직접 주입받음).
                VM이 소비하는 5개 경로만 충실 구현, 나머지 추상멤버는 NotSupported.
****************************************************************************/
internal sealed class FakeGopServer : IAccountApiService
{
    // ── 가짜 시계(파생상태 결정론) ── 서버 _now() 대응. 고정 기준시각.
    public DateTime Now { get; set; } = new DateTime(2026, 7, 20, 12, 0, 0);

    // ── 행위 주체(인가) 모사 ── require_admin_async / require_perm_async("users","view")
    public string ActorRole { get; set; } = "ADMIN";
    public bool ActorHasUsersView { get; set; } = true;   // ADMIN이면 bypass, 아니면 이 플래그가 users:view
    public int ActorId { get; set; } = 1;

    // ── 결함 주입(예외 경로·로드 실패) ──
    public bool ThrowOnCreate { get; set; }
    public bool ThrowOnDelete { get; set; }
    public bool ThrowOnListAll { get; set; }
    public bool FailUsersLoad { get; set; }
    public bool FailGroupsLoad { get; set; }
    /// <summary>GET /grants 가 예외가 아닌 실패 envelope(500) 반환 — 목록 로드 실패 팝업 경로 검증.</summary>
    public bool FailListAll { get; set; }

    /// <summary>보정 서버/파싱 모사 — top-level total을 pagination.total로 노출(클라 절단경고 로직 검증용). 기본 false=실서버 재현.</summary>
    public bool EmitPaginationMeta { get; set; }

    // ── 호출 카운터(부수효과 검증: "POST가 서버에 도달했는가") ──
    public int CreateCallCount { get; private set; }
    public int DeleteCallCount { get; private set; }
    public int ListAllCallCount { get; private set; }
    public int UsersCallCount { get; private set; }
    public int GroupsCallCount { get; private set; }

    // ── 상태 ──
    private int _grantSeq;
    public List<AuthUserDto> Users { get; } = new();
    public List<UserGroupDto> Groups { get; } = new();
    public List<GrantRow> GrantRows { get; } = new();

    // ── 감사 로그 모사(날짜필터+페이지네이션, 서버 audit_logs.py 계약) ──
    public List<AuditLogDto> AuditLogs { get; } = new();
    public int AuditCallCount { get; private set; }
    public string? LastAuditStartDate { get; private set; }
    public string? LastAuditEndDate { get; private set; }
    public int LastAuditPage { get; private set; }

    /// <summary>서버 UserGroupGrant 행 모사.</summary>
    internal sealed class GrantRow
    {
        public int Id;
        public int UserId;
        public int GroupId;
        public DateTime ValidFrom;
        public DateTime? ValidUntil;
        public bool IsActive;
        public DateTime? RevokedAt;
        public int? GrantedBy;
        public DateTime CreatedAt;
    }

    /// <summary>시드 헬퍼 — 부여 행 직접 추가(초기 로드/상태 시나리오용).</summary>
    public GrantRow Seed(int userId, int groupId, DateTime from, DateTime? until, DateTime? createdAt = null, bool revoked = false)
    {
        var row = new GrantRow
        {
            Id = ++_grantSeq,
            UserId = userId,
            GroupId = groupId,
            ValidFrom = from,
            ValidUntil = until,
            IsActive = !revoked,
            RevokedAt = revoked ? (createdAt ?? Now) : null,
            GrantedBy = 1,
            CreatedAt = createdAt ?? Now,
        };
        GrantRows.Add(row);
        return row;
    }

    // ── 파생상태(grant_service.grant_status 대응, 우선순위 REVOKED>PENDING>EXPIRED>ACTIVE) ──
    public string StatusOf(GrantRow g)
    {
        if (g.RevokedAt != null) return "REVOKED";
        if (g.ValidFrom > Now) return "PENDING";
        if (g.ValidUntil != null && g.ValidUntil <= Now) return "EXPIRED";
        return "ACTIVE";
    }

    private GrantDto ToDto(GrantRow g)
    {
        var u = Users.FirstOrDefault(x => x.Id == g.UserId);
        var gr = Groups.FirstOrDefault(x => x.Id == g.GroupId);
        return new GrantDto
        {
            Id = g.Id,
            UserId = g.UserId,
            UserLogin = u?.LoginId,   // v5.4 비정규화
            UserName = u?.Name,
            GroupId = g.GroupId,
            GroupName = gr?.Name,
            ValidFrom = g.ValidFrom,
            ValidUntil = g.ValidUntil,
            IsActive = g.IsActive,
            Status = StatusOf(g),
            GrantedBy = g.GrantedBy,
            RevokedAt = g.RevokedAt,
            CreatedAt = g.CreatedAt,
        };
    }

    // ── 에러 응답(클라 ToApiResponseAsync 파싱 결과 재현: Error.Code/Message 세팅 + StatusCode) ──
    private static ApiResponse<T> Err<T>(int status, string code, string message)
    {
        var r = ApiResponse<T>.CreateError(code, message);
        r.StatusCode = status;
        return r;
    }
    private static ApiListResponse<T> ErrL<T>(int status, string code, string message)
    {
        var r = ApiListResponse<T>.CreateError(code, message);
        r.StatusCode = status;
        return r;
    }

    private ApiResponse<object> Forbidden_Admin()
        => Err<object>(403, "FORBIDDEN", $"Insufficient role: requires one of ['ADMIN'] (current role: {ActorRole})");
    private ApiListResponse<GrantDto> Forbidden_View()
        => ErrL<GrantDto>(403, "FORBIDDEN", $"Insufficient permission: requires users:view (role: {ActorRole})");

    private bool IsAdmin => ActorRole == "ADMIN";

    // ─────────────────────────── VM이 소비하는 5경로(충실 구현) ───────────────────────────

    public Task<ApiListResponse<AuthUserDto>> GetUsersAsync(int page = 1, int limit = 100, CancellationToken ct = default)
    {
        UsersCallCount++;
        if (FailUsersLoad)
            return Task.FromResult(ErrL<AuthUserDto>(500, "INTERNAL_ERROR", "Internal server error"));
        // 서버 Query(100, ge=1, le=100) — RequestValidationError → code VALIDATION_ERROR, message "Validation error"
        if (limit < 1 || limit > 100)
            return Task.FromResult(ErrL<AuthUserDto>(422, "VALIDATION_ERROR", "Validation error"));
        if (page < 1)
            return Task.FromResult(ErrL<AuthUserDto>(422, "VALIDATION_ERROR", "Validation error"));
        var slice = Users.Skip((page - 1) * limit).Take(limit).ToList();
        return Task.FromResult(ApiListResponse<AuthUserDto>.CreateSuccess(slice));
    }

    public Task<ApiListResponse<UserGroupDto>> GetUserGroupsAsync(CancellationToken ct = default)
    {
        GroupsCallCount++;
        if (FailGroupsLoad)
            return Task.FromResult(ErrL<UserGroupDto>(500, "INTERNAL_ERROR", "Internal server error"));
        return Task.FromResult(ApiListResponse<UserGroupDto>.CreateSuccess(Groups.ToList()));
    }

    // POST /users/{userId}/grants — create_grant 대응
    public Task<ApiResponse<GrantDto>> CreateGrantAsync(int userId, GrantCreateDto dto, CancellationToken ct = default)
    {
        CreateCallCount++;
        if (ThrowOnCreate) throw new InvalidOperationException("simulated transport failure (create)");
        if (!IsAdmin) return Task.FromResult(Err<GrantDto>(403, "FORBIDDEN",
            $"Insufficient role: requires one of ['ADMIN'] (current role: {ActorRole})"));

        var user = Users.FirstOrDefault(u => u.Id == userId);
        if (user == null) return Task.FromResult(Err<GrantDto>(404, "NOT_FOUND", "User not found"));
        var group = Groups.FirstOrDefault(g => g.Id == dto.GroupId);
        if (group == null) return Task.FromResult(Err<GrantDto>(404, "NOT_FOUND", "User group not found"));

        var validFrom = dto.ValidFrom;
        var validUntil = dto.ValidUntil;
        if (validUntil != null)
        {
            if (validUntil <= validFrom)
                return Task.FromResult(Err<GrantDto>(422, "VALIDATION_ERROR", "valid_until must be after valid_from"));
            if (validUntil <= Now)
                return Task.FromResult(Err<GrantDto>(422, "VALIDATION_ERROR", "valid_until must be in the future"));
        }

        var row = new GrantRow
        {
            Id = ++_grantSeq,
            UserId = userId,
            GroupId = dto.GroupId,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            IsActive = true,
            GrantedBy = ActorId,
            CreatedAt = Now,
        };
        GrantRows.Add(row);
        var ok = new ApiResponse<GrantDto> { Success = true, Data = ToDto(row), StatusCode = 201 };
        return Task.FromResult(ok);
    }

    // DELETE /grants/{grantId} — revoke_grant 대응(soft, 멱등)
    public Task<ApiResponse<object>> DeleteGrantAsync(int grantId, CancellationToken ct = default)
    {
        DeleteCallCount++;
        if (ThrowOnDelete) throw new InvalidOperationException("simulated transport failure (delete)");
        if (!IsAdmin) return Task.FromResult(Forbidden_Admin());

        var g = GrantRows.FirstOrDefault(x => x.Id == grantId);
        if (g == null) return Task.FromResult(Err<object>(404, "NOT_FOUND", "Grant not found"));
        if (g.RevokedAt == null)
        {
            g.RevokedAt = Now;
            g.IsActive = false;
        }
        var ok = new ApiResponse<object> { Success = true, Message = $"Grant {grantId} revoked", StatusCode = 200 };
        return Task.FromResult(ok);
    }

    // GET /grants — list_all_grants 대응(필터·페이지네이션·created_at desc)
    public Task<ApiListResponse<GrantDto>> GetAllGrantsAsync(int page = 1, int size = 20, int? userId = null,
        int? groupId = null, string? status = null, bool activeOnly = false, CancellationToken ct = default)
    {
        ListAllCallCount++;
        if (ThrowOnListAll) throw new InvalidOperationException("simulated transport failure (list)");
        if (FailListAll) return Task.FromResult(ErrL<GrantDto>(500, "INTERNAL_ERROR", "Internal server error"));
        if (!IsAdmin && !ActorHasUsersView) return Task.FromResult(Forbidden_View());
        if (page < 1) return Task.FromResult(ErrL<GrantDto>(422, "VALIDATION_ERROR", "page must be >= 1"));
        if (size < 1 || size > 100) return Task.FromResult(ErrL<GrantDto>(422, "VALIDATION_ERROR", "size must be between 1 and 100"));

        IEnumerable<GrantRow> q = GrantRows;
        if (userId != null) q = q.Where(g => g.UserId == userId);
        if (groupId != null) q = q.Where(g => g.GroupId == groupId);
        if (activeOnly) q = q.Where(g => g.IsActive);

        if (status != null)
        {
            var valid = new[] { "ACTIVE", "PENDING", "EXPIRED", "REVOKED" };
            if (!valid.Contains(status))
                return Task.FromResult(ErrL<GrantDto>(422, "VALIDATION_ERROR",
                    "status must be one of ['ACTIVE', 'EXPIRED', 'PENDING', 'REVOKED']"));
            var allRows = q.OrderByDescending(g => g.CreatedAt).ThenByDescending(g => g.Id).ToList();
            var filtered = allRows.Where(g => StatusOf(g) == status).ToList();
            var pageItems = filtered.Skip((page - 1) * size).Take(size).Select(ToDto).ToList();
            return Task.FromResult(OkList(pageItems, filtered.Count, page, size));
        }

        var ordered = q.OrderByDescending(g => g.CreatedAt).ThenByDescending(g => g.Id).ToList();
        var total = ordered.Count;
        var slice = ordered.Skip((page - 1) * size).Take(size).Select(ToDto).ToList();
        return Task.FromResult(OkList(slice, total, page, size));
    }

    // GET /users/{userId}/grants — list_grants 대응(per-user, total 없음)
    public Task<ApiListResponse<GrantDto>> GetUserGrantsAsync(int userId, CancellationToken ct = default)
    {
        if (!IsAdmin && !ActorHasUsersView) return Task.FromResult(Forbidden_View());
        var user = Users.FirstOrDefault(u => u.Id == userId);
        if (user == null) return Task.FromResult(ErrL<GrantDto>(404, "NOT_FOUND", "User not found"));
        var list = GrantRows.Where(g => g.UserId == userId)
                            .OrderByDescending(g => g.CreatedAt).ThenByDescending(g => g.Id)
                            .Select(ToDto).ToList();
        return Task.FromResult(ApiListResponse<GrantDto>.CreateSuccess(list));
    }

    private ApiListResponse<GrantDto> OkList(List<GrantDto> data, int total, int page, int size)
    {
        var r = ApiListResponse<GrantDto>.CreateSuccess(data);
        // 실서버는 top-level "total" 반환 → F-1 수정으로 클라 ApiListResponse.Total 이 이를 수신(절단경고 정상화).
        r.Total = total;
        // EmitPaginationMeta=true 면 pagination 객체(events 등 TotalPages 경로)도 함께 노출(대조 경로).
        if (EmitPaginationMeta)
            r.Pagination = new PaginationDto { Page = page, Limit = size, Total = total };
        return r;
    }

    // ─────────────────────────── 미사용 추상멤버(VM 비호출 → 호출 시 즉시 실패) ───────────────────────────
    private static NotSupportedException NS([CallerMemberName] string m = "")
        => new($"FakeGopServer.{m} 은 grant 시뮬레이션에서 사용되지 않습니다(호출됨=시나리오 오류).");

    public Task<ApiResponse<LoginResponseDataDto>> LoginAsync(string loginId, string password, CancellationToken ct = default) => throw NS();
    public Task<ApiResponse<TokenDataDto>> RefreshAsync(string refreshToken, CancellationToken ct = default) => throw NS();
    public Task<ApiResponse<object>> LogoutAsync(CancellationToken ct = default) => throw NS();
    public Task<AuthUserDto?> GetMeAsync(CancellationToken ct = default) => throw NS();
    public Task<ApiResponse<AuthUserDto>> CreateUserAsync(UserCreateDto dto, CancellationToken ct = default) => throw NS();
    public Task<ApiResponse<AuthUserDto>> UpdateUserAsync(int id, UserUpdateDto dto, CancellationToken ct = default) => throw NS();
    public Task<ApiResponse<object>> DeleteUserAsync(int id, CancellationToken ct = default) => throw NS();
    public Task<ApiResponse<object>> ResetUserPasswordAsync(int id, string newPassword, CancellationToken ct = default) => throw NS();
    public Task<ApiResponse<AuthUserDto>> GetMyProfileAsync(CancellationToken ct = default) => throw NS();
    public Task<ApiResponse<AuthUserDto>> UpdateMyProfileAsync(UserSelfUpdateDto dto, CancellationToken ct = default) => throw NS();
    public Task<ApiResponse<AuthUserDto>> UploadMyPhotoAsync(string filePath, CancellationToken ct = default) => throw NS();
    public Task<ApiResponse<object>> ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) => throw NS();
    public Task<ApiListResponse<UserSessionDto>> GetUserSessionsAsync(CancellationToken ct = default) => throw NS();
    // 감사 로그: 날짜필터(created_at >=/<=) + id desc + page/limit 슬라이스 (서버 audit_logs.py 미러)
    public Task<ApiListResponse<AuditLogDto>> GetAuditLogsAsync(int page = 1, int limit = 20, string? startDate = null, string? endDate = null, CancellationToken ct = default)
    {
        AuditCallCount++;
        LastAuditStartDate = startDate;
        LastAuditEndDate = endDate;
        LastAuditPage = page;

        IEnumerable<AuditLogDto> q = AuditLogs;
        if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var sd))
            q = q.Where(a => DateTime.TryParse(a.CreatedAt, out var c) && c >= sd);
        if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var ed))
            q = q.Where(a => DateTime.TryParse(a.CreatedAt, out var c) && c <= ed);

        var filtered = q.OrderByDescending(a => a.Id).ToList();   // 서버 id desc
        var total = filtered.Count;
        var totalPages = total > 0 ? (int)Math.Ceiling(total / (double)limit) : 1;
        var slice = filtered.Skip((page - 1) * limit).Take(limit).ToList();

        var res = ApiListResponse<AuditLogDto>.CreateSuccess(
            slice,
            new PaginationDto { Page = page, Limit = limit, Total = total, TotalPages = totalPages });
        return Task.FromResult(res);
    }
}
