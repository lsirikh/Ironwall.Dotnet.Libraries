using Ironwall.Dotnet.Libraries.Accounts.Api.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// 계정 콘솔 [적용](부분 수정)과 사진 주소 경계 — 라이브 왕복(tools/live-api-roundtrip, accounts-vm A2 · A3)의 헤드리스 짝.
/// </summary>
public class AccountPartialUpdateAndPhotoTests
{
    private static AccountModel Model() => new()
    {
        Id = 7,
        Username = "op7",
        Name = "김운영",
        EMail = "op7@example.com",
        Phone = "010-0000-0007",
        Department = "경비과",
        Role = EnumUserRole.USER,
        Used = EnumUsedType.USED,
        Image = "https://h:8000/api/users/photo/7_ab.png",
    };

    private static JObject Wire(UserUpdateDto dto) => JObject.Parse(JsonConvert.SerializeObject(dto));

    // ── 부분 수정 본문 ───────────────────────────────────────────────

    [Fact]
    public void should_send_only_the_touched_field_when_mapping_a_partial_update()
    {
        var wire = Wire(AccountDtoMapper.ToUserUpdateDto(Model(), new[] { "department" }));

        Assert.Equal(new[] { "department" }, wire.Properties().Select(p => p.Name));
        Assert.Equal("경비과", (string?)wire["department"]);
    }

    [Fact]
    public void should_send_is_active_false_when_the_status_field_is_set_to_not_used()
    {
        var m = Model();
        m.Used = EnumUsedType.NOT_USED;

        var wire = Wire(AccountDtoMapper.ToUserUpdateDto(m, new[] { "is_active" }));

        Assert.Equal(new[] { "is_active" }, wire.Properties().Select(p => p.Name));
        Assert.False((bool)wire["is_active"]!);
    }

    [Fact]
    public void should_send_explicit_null_when_a_clearable_field_is_emptied()
    {
        var m = Model();
        m.EMail = null;
        m.Phone = "   ";

        var wire = Wire(AccountDtoMapper.ToUserUpdateDto(m, new[] { "email", "phone" }));

        Assert.Equal(JTokenType.Null, wire["email"]!.Type);
        Assert.Equal(JTokenType.Null, wire["phone"]!.Type);
        Assert.Equal(2, wire.Count);
    }

    [Fact]
    public void should_not_send_role_when_role_was_not_touched()
    {
        var wire = Wire(AccountDtoMapper.ToUserUpdateDto(Model(), new[] { "department", "email" }));

        Assert.False(wire.ContainsKey("role"));
        Assert.False(wire.ContainsKey("photo_url"));
        Assert.False(wire.ContainsKey("name"));
    }

    [Fact]
    public void should_send_only_admin_or_user_when_role_is_touched()
    {
        var m = Model();
        m.Role = EnumUserRole.ADMIN;

        var wire = Wire(AccountDtoMapper.ToUserUpdateDto(m, new[] { "role" }));

        Assert.Equal("ADMIN", (string?)wire["role"]);
    }

    [Fact]
    public void should_not_clear_the_name_when_it_is_blank()
    {
        var m = Model();
        m.Name = "";

        var wire = Wire(AccountDtoMapper.ToUserUpdateDto(m, new[] { "name" }));

        Assert.Empty(wire.Properties());   // 이름은 필수 — 비우기 대상이 아니다
    }

    [Fact]
    public void should_ignore_unknown_field_names_when_mapping_a_partial_update()
    {
        var wire = Wire(AccountDtoMapper.ToUserUpdateDto(Model(), new[] { "group_id", "username", "nope" }));

        Assert.Empty(wire.Properties());
    }

    // ── 사진 주소 경계 ───────────────────────────────────────────────

    [Theory]
    [InlineData("/api/users/photo/7_ab.png", "https://127.0.0.1:8000/api", "https://127.0.0.1:8000/api/users/photo/7_ab.png")]
    [InlineData("/api/users/photo/7_ab.png", "https://127.0.0.1:8000/api/", "https://127.0.0.1:8000/api/users/photo/7_ab.png")]
    [InlineData("/api/users/photo/7_ab.png", "https://gw.example/gop/api", "https://gw.example/gop/api/users/photo/7_ab.png")]
    [InlineData("/api/users/photo/default.png", "http://10.0.0.5:8000", "http://10.0.0.5:8000/api/users/photo/default.png")]
    public void should_resolve_a_relative_server_photo_against_the_api_base_when_displaying(string photo, string baseUrl, string expected)
        => Assert.Equal(expected, ServerPhotoUrl.ToDisplay(photo, baseUrl));

    [Theory]
    [InlineData("https://cdn.example/p.png", "https://h/api")]    // 외부 URL — 그대로
    [InlineData("20260924010203.png", "https://h/api")]          // 로컬 파일 이름(DB 모드) — 그대로
    [InlineData("/api/users/photo/7_ab.png", null)]              // 주소 미설정 — 호스트를 지어내지 않는다
    [InlineData("/api/users/photo/7_ab.png", "not a url")]
    [InlineData(null, "https://h/api")]
    public void should_leave_the_value_unchanged_when_it_cannot_or_need_not_be_resolved(string? photo, string? baseUrl)
        => Assert.Equal(photo, ServerPhotoUrl.ToDisplay(photo, baseUrl));

    [Theory]
    [InlineData("https://127.0.0.1:8000/api/users/photo/7_ab.png", "/api/users/photo/7_ab.png")]
    [InlineData("https://gw.example/gop/api/users/photo/7_ab.png", "/api/users/photo/7_ab.png")]
    [InlineData("/api/users/photo/default.png", "/api/users/photo/default.png")]
    [InlineData("https://cdn.example/p.png", "https://cdn.example/p.png")]
    [InlineData("C:\\temp\\p.png", null)]
    [InlineData("", null)]
    public void should_strip_the_host_when_sending_a_server_photo_back(string image, string? expected)
        => Assert.Equal(expected, ServerPhotoUrl.ToServer(image));

    [Fact]
    public void should_keep_photo_url_relative_when_the_editor_saves_a_displayed_absolute_url()
    {
        var wire = Wire(AccountDtoMapper.ToUserUpdateDto(Model()));

        Assert.Equal("/api/users/photo/7_ab.png", (string?)wire["photo_url"]);
    }

    [Fact]
    public async Task should_give_the_screen_absolute_photo_urls_when_the_api_knows_its_base_url()
    {
        var api = new PhotoStubApi { ServerBaseUrl = "https://127.0.0.1:8000/api" };
        var gw = new ApiAccountGateway(api, new TokenStorageService(), new PermissionService());

        var list = await gw.GetAllAccountsAsync();
        var uploaded = await gw.UploadPhotoAsync(7, "ignored.png");

        Assert.Equal("https://127.0.0.1:8000/api/users/photo/7_ab.png", list![0].Image);
        Assert.Equal("https://127.0.0.1:8000/api/users/photo/7_new.png", uploaded);
    }

    [Fact]
    public async Task should_send_only_the_touched_fields_when_the_gateway_updates_fields()
    {
        var api = new PhotoStubApi();
        var gw = new ApiAccountGateway(api, new TokenStorageService(), new PermissionService());

        var result = await gw.UpdateAccountFieldsAsync(Model(), new[] { "department" });

        Assert.NotNull(result);
        Assert.Equal(new[] { "department" }, Wire(api.LastUpdate!).Properties().Select(p => p.Name));
    }

    private sealed class PhotoStubApi : IAccountApiService
    {
        public string? ServerBaseUrl { get; set; }
        public UserUpdateDto? LastUpdate { get; private set; }

        public Task<ApiListResponse<AuthUserDto>> GetUsersAsync(int page = 1, int limit = 100, CancellationToken ct = default)
            => Task.FromResult(ApiListResponse<AuthUserDto>.CreateSuccess(new List<AuthUserDto>
            {
                new() { Id = 7, LoginId = "op7", Role = "USER", IsActive = true, PhotoUrl = "/api/users/photo/7_ab.png" },
            }));

        public Task<ApiResponse<AuthUserDto>> UploadUserPhotoAsync(int userId, string filePath, CancellationToken ct = default)
            => Task.FromResult(ApiResponse<AuthUserDto>.CreateSuccess(
                new AuthUserDto { Id = userId, LoginId = "op7", Role = "USER", IsActive = true, PhotoUrl = "/api/users/photo/7_new.png" }));

        public Task<ApiResponse<AuthUserDto>> UpdateUserAsync(int id, UserUpdateDto dto, CancellationToken ct = default)
        {
            LastUpdate = dto;
            return Task.FromResult(ApiResponse<AuthUserDto>.CreateSuccess(new AuthUserDto { Id = id, LoginId = "op7", Role = "USER", IsActive = true }));
        }

        public Task<ApiResponse<AuthUserDto>> CreateUserAsync(UserCreateDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> DeleteUserAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> ResetUserPasswordAsync(int id, string newPassword, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<LoginResponseDataDto>> LoginAsync(string loginId, string password, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<TokenDataDto>> RefreshAsync(string refreshToken, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> LogoutAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<UserGroupDto>> GetUserGroupsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<UserSessionDto>> GetUserSessionsAsync(int page = 1, int limit = 100, bool? isActive = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<AuditLogDto>> GetAuditLogsAsync(int page = 1, int limit = 20, string? startDate = null, string? endDate = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AuthUserDto?> GetMeAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> GetMyProfileAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> UpdateMyProfileAsync(UserSelfUpdateDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> UploadMyPhotoAsync(string filePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
