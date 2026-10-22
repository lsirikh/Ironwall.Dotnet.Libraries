using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Unit;

/// <summary>
/// IR 조명(적외선 LED) 직접 제어 — ONVIF PTZ 보조 명령(GetNode AuxiliaryCommands → SendAuxiliaryCommand). 가짜 SOAP 카메라 상대:
/// 지원 판정 · 보낸 SOAP 본문 · 지원 안 함(아무것도 안 보냄) · 카메라 거부.
/// </summary>
public class IrLampProviderTests
{
    private static CancellationToken Within(int ms) => new CancellationTokenSource(ms).Token;

    private static VideoProviderInfo Info(FakeOnvifCamera camera) => new()
    {
        Kind = VideoProviderKind.Onvif, Host = "127.0.0.1", Port = camera.Port, Username = "u", Password = "p",
    };

    private static List<string> AuxBodies(FakeOnvifCamera camera)
        => camera.Requests.Where(r => r.Body.Contains("SendAuxiliaryCommand")).Select(r => r.Body).ToList();

    [Theory]
    [InlineData("tt:IRLamp|On", "tt:IRLamp|Off", "tt:IRLamp|Auto")]   // ONVIF 표준 글자
    [InlineData("irlamp|on", "irlamp|off", "irlamp|auto")]            // 대소문자 · 접두 없는 변형 — 카메라 글자 그대로 돌려보낸다
    public async Task should_detect_ir_lamp_and_send_the_camera_own_auxiliary_string_when_announced(string on, string off, string auto)
    {
        // Arrange
        await using var camera = new FakeOnvifCamera { AuxiliaryCommands = new[] { "tt:Wiper|On", on, off, auto } };
        var ptz = CameraProviderRegistry.CreateDefault(new TestLog()).GetPtz(VideoProviderKind.Onvif)!;
        Assert.True((await ptz.PrepareAsync("ir-1", Info(camera), Within(10_000))).PtzCapable);

        // Act
        var modes = ptz.GetIrLampModes("ir-1");
        var sentOn = await ptz.SetIrLampAsync("ir-1", IrLampMode.On, Within(5_000));
        var lastAfterOn = ptz.GetLastIrLamp("ir-1");
        var sentAuto = await ptz.SetIrLampAsync("ir-1", "auto", Within(5_000));   // 값 대소문자는 가리지 않는다

        // Assert
        Assert.Equal(new[] { IrLampMode.On, IrLampMode.Off, IrLampMode.Auto }, modes);
        Assert.True(sentOn);
        Assert.Equal(IrLampMode.On, lastAfterOn);
        Assert.True(sentAuto);
        Assert.Equal(IrLampMode.Auto, ptz.GetLastIrLamp("ir-1"));
        var bodies = AuxBodies(camera);
        Assert.Equal(2, bodies.Count);
        Assert.Contains($">{on}<", bodies[0]);
        Assert.Contains(">P0<", bodies[0]);   // 프로파일 토큰과 함께
        Assert.Contains($">{auto}<", bodies[1]);
        ptz.Release("ir-1");
    }

    [Fact]
    public async Task should_report_no_ir_lamp_and_send_nothing_when_camera_announces_no_ir_lamp_command()
    {
        // Arrange — 보조 명령은 있지만 IR 조명은 없다(와이퍼 · 근거 없는 이름)
        await using var camera = new FakeOnvifCamera { AuxiliaryCommands = new[] { "tt:Wiper|On", "IRLight|On" } };
        var ptz = CameraProviderRegistry.CreateDefault(new TestLog()).GetPtz(VideoProviderKind.Onvif)!;
        await ptz.PrepareAsync("ir-2", Info(camera), Within(10_000));

        // Act
        var modes = ptz.GetIrLampModes("ir-2");
        var sent = await ptz.SetIrLampAsync("ir-2", IrLampMode.On, Within(5_000));

        // Assert
        Assert.Empty(modes);
        Assert.False(sent);
        Assert.Empty(AuxBodies(camera));
        Assert.Null(ptz.GetLastIrLamp("ir-2"));
    }

    [Fact]
    public async Task should_fail_quietly_and_keep_last_value_when_camera_refuses_the_auxiliary_command()
    {
        // Arrange
        await using var camera = new FakeOnvifCamera { AuxiliaryCommands = new[] { "tt:IRLamp|On", "tt:IRLamp|Off" } };
        var ptz = CameraProviderRegistry.CreateDefault(new TestLog()).GetPtz(VideoProviderKind.Onvif)!;
        await ptz.PrepareAsync("ir-3", Info(camera), Within(10_000));
        Assert.True(await ptz.SetIrLampAsync("ir-3", IrLampMode.Off, Within(5_000)));
        camera.FailAuxiliary = true;

        // Act
        var refused = await ptz.SetIrLampAsync("ir-3", IrLampMode.On, Within(5_000));
        var unannounced = await ptz.SetIrLampAsync("ir-3", IrLampMode.Auto, Within(5_000));

        // Assert — 실패는 false 뿐(던지지 않는다), 마지막으로 성공한 값은 그대로
        Assert.False(refused);
        Assert.False(unannounced);
        Assert.Equal(IrLampMode.Off, ptz.GetLastIrLamp("ir-3"));
        Assert.Equal(2, AuxBodies(camera).Count);   // 알리지 않은 Auto 는 보내지 않았다
        ptz.Release("ir-3");
    }

    [Theory]
    [InlineData(new string[0], new string[0])]
    [InlineData(new[] { "tt:IRLamp|On" }, new[] { "On" })]
    [InlineData(new[] { " TT:IRLAMP|OFF ", "tt:IRLamp|Off" }, new[] { "Off" })]   // 같은 값이 두 번이면 처음 것
    [InlineData(new[] { "tt:IRLamp|Blink", "tt:IRLampX|On", "IRLight|On", "tt:IRLamp|On|x" }, new string[0])]
    public void should_accept_only_standard_ir_lamp_strings_when_parsing_auxiliary_commands(string[] aux, string[] expected)
    {
        var map = PtzController.ParseIrLampCommands(aux);

        Assert.Equal(expected.OrderBy(x => x), map.Keys.OrderBy(x => x));
    }
}
