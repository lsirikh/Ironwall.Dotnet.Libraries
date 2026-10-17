using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 프리셋으로 장비 만들기 — "보낼 내용" 미리보기에 비밀번호가 평문으로 찍히던 결함(완성도 점검 R1)의 회귀망.
/// 입력칸은 PasswordBox 로 가려 놓고 미리보기 JSON 에는 connection.credentials.user_password 가 그대로 보였다.
/// </summary>
public class RegisterPreviewSecretMaskTests
{
    private const string Wire = "{\"name_device\":\"CAM-1\",\"connection\":{\"ip_address\":\"10.0.0.5\",\"ip_port\":80," +
                                "\"credentials\":{\"user_name\":\"admin\",\"user_password\":\"s3cret!\"}}}";

    [Fact]
    public void should_hide_the_password_when_the_preview_is_shown()
    {
        var shown = RegisterFromPresetViewModel.Indent(Wire);

        Assert.DoesNotContain("s3cret!", shown);
        Assert.Equal(RegisterFromPresetViewModel.MaskedSecret,
            (string?)JToken.Parse(shown).SelectToken("connection.credentials.user_password"));
    }

    [Fact]
    public void should_keep_every_other_value_when_the_password_is_masked()
    {
        var shown = JToken.Parse(RegisterFromPresetViewModel.Indent(Wire));

        Assert.Equal("CAM-1", (string?)shown["name_device"]);
        Assert.Equal("10.0.0.5", (string?)shown.SelectToken("connection.ip_address"));
        Assert.Equal("admin", (string?)shown.SelectToken("connection.credentials.user_name"));
    }

    [Fact]
    public void should_leave_an_empty_password_as_is_when_nothing_was_typed()
    {
        var shown = JToken.Parse(RegisterFromPresetViewModel.Indent(
            "{\"connection\":{\"credentials\":{\"user_password\":\"\"}}}"));

        Assert.Equal("", (string?)shown.SelectToken("connection.credentials.user_password"));
    }

    [Fact]
    public void should_show_nothing_when_the_body_cannot_be_parsed()
    {
        Assert.Equal(string.Empty, RegisterFromPresetViewModel.Indent("{not json \"user_password\":\"s3cret!\""));
    }
}
