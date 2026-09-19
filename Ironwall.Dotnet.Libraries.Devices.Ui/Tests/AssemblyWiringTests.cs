using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Newtonsoft.Json;
using System;
using System.Reflection;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 빌드와 단위 테스트가 다 초록이어도 <b>배선</b>이 틀리면 기능만 죽는다 — 그 배선을 값으로 고정한다.
/// </summary>
public class AssemblyWiringTests
{
    /// <summary>
    /// 조립기 창들은 Caliburn 의 이름 규약으로 뷰를 찾는다(<c>X.FooViewModel → X.FooView</c>). 이 어셈블리가 <c>Views</c> 폴더 밖에서
    /// 규약에 기대는 첫 사례다 — 이름을 바꾸거나 옮기면 호스트에서 창이 "Cannot find view" 글자만 띄운다.
    /// </summary>
    [Theory]
    [InlineData(typeof(AssemblyViewModel))]
    [InlineData(typeof(RegisterFromPresetViewModel))]
    [InlineData(typeof(PresetManagerViewModel))]
    [InlineData(typeof(RepeatExpandViewModel))]
    [InlineData(typeof(TextPromptViewModel))]
    [InlineData(typeof(ConfirmPromptViewModel))]
    public void should_have_a_public_view_beside_every_window_view_model(Type viewModel)
    {
        var viewName = viewModel.FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);

        var view = viewModel.Assembly.GetType(viewName);

        Assert.True(view is not null, $"{viewName} 이(가) 없다 — 창이 뷰를 못 찾는다");
        Assert.True(view!.IsPublic);
        Assert.NotNull(view.GetConstructor(Type.EmptyTypes));     // 규약 뷰는 매개변수 없는 생성자로 만들어진다
    }

    /// <summary>
    /// 등록 창의 미리보기는 "이대로 나간다"고 말한다. 미리보기는 옮겨 적은 직렬화 설정으로 만들고, 실제 전송은 API 서비스의
    /// 비공개 설정으로 나간다 — 둘이 갈리면 미리보기가 거짓말이 된다.
    /// </summary>
    [Fact]
    public void should_use_the_same_serializer_settings_as_the_api_service_for_the_preview()
    {
        var field = typeof(ApiService).GetField("_jsonSettings", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.True(field is not null, "ApiService._jsonSettings 가 없다 — 미리보기 설정의 출처를 다시 찾아야 한다");
        var wire = (JsonSerializerSettings)field!.GetValue(null)!;
        var preview = PresetRequestBuilder.WireSettings;

        Assert.Equal(wire.DateFormatHandling, preview.DateFormatHandling);
        Assert.Equal(wire.DateTimeZoneHandling, preview.DateTimeZoneHandling);
        Assert.Equal(wire.NullValueHandling, preview.NullValueHandling);
        Assert.Equal(wire.DefaultValueHandling, preview.DefaultValueHandling);
        Assert.Equal(wire.Formatting, preview.Formatting);
        Assert.Equal(wire.ContractResolver?.GetType(), preview.ContractResolver?.GetType());
        Assert.Equal(wire.Converters.Count, preview.Converters.Count);
    }
}
