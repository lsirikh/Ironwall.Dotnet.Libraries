using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;

/// <summary>
/// 계정 쓰기 본문의 와이어 모양 — 라이브 왕복(tools/live-api-roundtrip, accounts-vm)에서 드러난 두 결함의 회귀 방지.
/// <list type="bullet">
/// <item>PUT /settings/session: 읽기 전용 <c>auth_mode</c>·<c>jwt_algorithm</c> 이 null 로 실려 서버 8.0 이 매 저장을 422 UNKNOWN_FIELD 로 거부.</item>
/// <item>PUT /users/{id}: null 을 전부 생략해 "비우기"를 보낼 방법이 없었다(서버는 보낸 키의 null 을 해제로 받는다).</item>
/// </list>
/// ApiService 와 같은 기본 설정(NullValueHandling 미지정 = Include)으로 직렬화해 본다.
/// </summary>
public class AccountWriteDtoTests
{
    private static JObject Wire(object dto) => JObject.Parse(JsonConvert.SerializeObject(dto, new JsonSerializerSettings
    {
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateTimeZoneHandling = DateTimeZoneHandling.Local,
    }));

    [Fact]
    public void should_omit_read_only_keys_when_session_settings_leave_them_null()
    {
        var dto = new SessionSettingsDto { SessionTimeoutHours = 24, SessionConcurrencyPolicy = "allow" };

        var wire = Wire(dto);

        Assert.False(wire.ContainsKey("auth_mode"));
        Assert.False(wire.ContainsKey("jwt_algorithm"));
        Assert.Equal(new[] { "session_timeout_hours", "session_concurrency_policy" }, wire.Properties().Select(p => p.Name));
    }

    [Fact]
    public void should_still_read_read_only_keys_when_server_returns_them()
    {
        var dto = JsonConvert.DeserializeObject<SessionSettingsDto>(@"{""auth_mode"":""token"",""jwt_algorithm"":""HS256"",""session_timeout_hours"":24}")!;

        Assert.Equal("token", dto.AuthMode);
        Assert.Equal("HS256", dto.JwtAlgorithm);
        Assert.Equal(24, dto.SessionTimeoutHours);
    }

    [Fact]
    public void should_omit_null_keys_when_user_update_has_no_clear_fields()
    {
        var wire = Wire(new UserUpdateDto { Department = "경비과" });

        Assert.Equal(new[] { "department" }, wire.Properties().Select(p => p.Name));
    }

    [Fact]
    public void should_write_explicit_null_when_field_is_listed_in_clear_fields()
    {
        var dto = new UserUpdateDto();
        dto.ClearFields.Add("email");
        dto.ClearFields.Add("phone");

        var wire = Wire(dto);

        Assert.Equal(JTokenType.Null, wire["email"]!.Type);
        Assert.Equal(JTokenType.Null, wire["phone"]!.Type);
        Assert.Equal(2, wire.Count);
    }

    [Fact]
    public void should_not_write_null_role_or_is_active_when_they_are_listed_in_clear_fields()
    {
        var dto = new UserUpdateDto();
        dto.ClearFields.Add("role");
        dto.ClearFields.Add("is_active");

        var wire = Wire(dto);

        Assert.Empty(wire.Properties());   // 서버는 두 키의 null 을 무시한다 — 실을 이유가 없다
    }

    [Fact]
    public void should_send_is_active_false_when_set()
    {
        var wire = Wire(new UserUpdateDto { IsActive = false });

        Assert.False((bool)wire["is_active"]!);
    }
}
