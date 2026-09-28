using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 장비 콘솔 상세 폼 이름 200자 상한(api schemas/device.py name_device max_length=200) 및
                  조립기 카테고리 콤보 한글/UIA 표기 회귀
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// (1) 이름 칸 — 서버가 200자를 넘기면 422 로 되돌리는 것을 보내기 전에 잡는다: 타이핑 중 즉시 알리고,
/// [등록]/[적용]을 잠근다. (2) 조립기 카테고리 콤보 — 화면 글자는 한글이지만 UIA 이름이 raw enum(예: "Enclosure")
/// 으로 새는지를 XAML 계약으로 잡는다.
/// </summary>
[Collection("CaliburnIoC")]
public class DeviceNameLengthAndCategoryDisplayTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    #region - 이름 200자 상한: 명세 -
    [Fact]
    public void should_cap_name_device_at_200_chars_when_catalog_spec_is_built()
    {
        var spec = DevicePropertyCatalog.For(EnumDeviceCategory.Controller, isAxisContract: true, isUnitEra: false)
            .Single(s => s.Key == "name_device");

        Assert.Equal(200, spec.MaxLength);
    }

    [Fact]
    public void should_reject_201_chars_but_accept_200_chars_when_name_device_is_validated()
    {
        var spec = DevicePropertyCatalog.For(EnumDeviceCategory.Controller, isAxisContract: true, isUnitEra: false)
            .Single(s => s.Key == "name_device");

        Assert.Null(DevicePropertyAccessor.Validate(spec, new string('a', 200), isCreating: false));
        var error = DevicePropertyAccessor.Validate(spec, new string('a', 201), isCreating: false);
        Assert.NotNull(error);
        Assert.Contains("200자", error);
    }
    #endregion

    #region - 이름 200자 상한: 폼 칸(즉시 검증 · Commit 검증) -
    private static ControllerDeviceViewModel Controller(int id, int number, string name)
        => new(new ControllerDeviceModel { Id = id, DeviceNumber = number, DeviceName = name, IpAddress = "10.0.0.1", Port = 80 });

    private static DevicePropertyFormViewModel NewForm(out ConsoleDetailPresenter presenter)
    {
        presenter = new ConsoleDetailPresenter();
        return new DevicePropertyFormViewModel(presenter);
    }

    private static PropertyFieldViewModel NameField(DevicePropertyFormViewModel form) => form.Fields.Single(f => f.Key == "name_device");

    [Fact]
    public void should_show_error_immediately_when_name_device_text_exceeds_200_chars_before_commit()
    {
        var form = NewForm(out _);
        form.Load(new object[] { Controller(1, 1, "정상 이름") }, EnumDeviceCategory.Controller, true, false, false);
        var name = NameField(form);

        name.Text = new string('a', 201);   // Commit 을 부르지 않았다 — 타이핑 중 즉시 잡혀야 한다

        Assert.True(name.HasError);
        Assert.Contains("200자", name.Error);
        Assert.True(form.HasInvalidField);
    }

    [Fact]
    public void should_clear_error_when_name_device_text_is_shortened_back_to_200_chars_or_fewer()
    {
        var form = NewForm(out _);
        form.Load(new object[] { Controller(1, 1, "정상 이름") }, EnumDeviceCategory.Controller, true, false, false);
        var name = NameField(form);

        name.Text = new string('a', 201);
        Assert.True(form.HasInvalidField);

        name.Text = new string('a', 200);
        Assert.False(name.HasError);
        Assert.False(form.HasInvalidField);
    }

    [Fact]
    public void should_write_nothing_when_committed_with_name_device_over_200_chars()
    {
        var row = Controller(1, 1, "정상 이름");
        var form = NewForm(out _);
        form.Load(new object[] { row }, EnumDeviceCategory.Controller, true, false, false);

        NameField(form).Text = new string('a', 201);
        var commit = form.Commit();

        Assert.False(commit.IsWritten);
        Assert.Contains("200자", commit.Message);
        Assert.Equal("정상 이름", row.DeviceName);   // 절반만 고친 행이 저장 경로로 새지 않는다
    }
    #endregion

    #region - 이름 200자 상한: 장비 콘솔의 [적용] 실제 잠김 -
    private static readonly string LampRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Lamp);

    private static async Task<DeviceDashboardViewModel> OpenConsoleAsync()
    {
        var log = new MockLogService();
        var events = new EventAggregator();
        var api = new MockDeviceApiService();
        var providerService = new MockDeviceProviderService();

        var devices = new DeviceProvider();
        var groups = new DeviceGroupProvider(log);
        var controllers = new ControllerDeviceProvider(log, devices);
        var lamps = new LampDevicePanelViewModel(events, log, api, new LampDeviceProvider(log, devices), providerService);

        var console = new DeviceDashboardViewModel(
            events, log, new DeviceTabControlViewModel(events, log),
            new ControllerDevicePanelViewModel(events, log, api, controllers, providerService),
            new SensorDevicePanelViewModel(events, log, api, new SensorDeviceProvider(log, devices), controllers, providerService),
            new CameraDevicePanelViewModel(events, log, api, new CameraDeviceProvider(log, devices), providerService),
            new SpeakerDevicePanelViewModel(events, log, api, new SpeakerDeviceProvider(log, devices), providerService),
            new EnclosureDevicePanelViewModel(events, log, api, new EnclosureDeviceProvider(log, devices), providerService),
            lamps,
            new GateDevicePanelViewModel(events, log, api, new GateDeviceProvider(log, devices), providerService),
            new DeviceGroupPanelViewModel(events, log, api, groups, devices),
            devices, groups, controllers, new ServerProvider(log), api, new StubCatalog(), null, null);

        devices.Add(new LampDeviceModel { Id = 11, DeviceNumber = 1, DeviceName = "경광등 1", Status = EnumDeviceStatus.ACTIVATED });

        await ((IActivate)console).ActivateAsync();
        return console;
    }

    [Fact]
    public async Task should_disable_apply_when_name_device_text_exceeds_200_chars_in_device_console()
    {
        var console = await OpenConsoleAsync();
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(new List<object> { console.Rows!.Cast<object>().Single() });
        Assert.False(console.CanApplyNow);   // 아직 손댄 칸이 없다

        var name = console.Form.Fields.Single(f => f.Key == "name_device");
        name.Text = new string('a', 201);

        Assert.False(console.CanApplyNow);   // 손댄 칸은 있지만 200자를 넘겨 잠긴다

        name.Text = "새 이름";

        Assert.True(console.CanApplyNow);    // 다시 유효한 값 — 잠금 풀림
    }

    private sealed class StubCatalog : Ironwall.Dotnet.Libraries.Devices.Ui.Services.ICatalogService
    {
        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(System.Threading.CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(System.Threading.CancellationToken token = default) => Task.FromResult(true);
        public Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogTypeAxis? TypeAxis(EnumDeviceCategory category) => null;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> TypeAxisValues(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public bool IsTypeAxisValue(EnumDeviceCategory category, string? code) => false;
        public string TypeAxisLabel(EnumDeviceCategory category, string? code) => code ?? string.Empty;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis> ExtraAxes(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis>();
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> Vocabulary(string name, bool includeDeprecated = false, EnumDeviceCategory? appliesTo = null) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public string LabelOf(string vocabularyName, string? code) => code ?? string.Empty;
    }
    #endregion

    #region - 조립기 카테고리 콤보: 화면 글자(한글) -
    [Theory]
    [InlineData(EnumDeviceCategory.Controller, "제어기")]
    [InlineData(EnumDeviceCategory.Sensor, "센서")]
    [InlineData(EnumDeviceCategory.Camera, "카메라")]
    [InlineData(EnumDeviceCategory.Speaker, "스피커")]
    [InlineData(EnumDeviceCategory.Enclosure, "함체")]
    [InlineData(EnumDeviceCategory.Lamp, "경광등")]
    [InlineData(EnumDeviceCategory.Gate, "통문")]
    public void should_show_korean_category_name_when_category_text_converter_runs(EnumDeviceCategory category, string expected)
        => Assert.Equal(expected, DeviceCategoryText.Of(category));
    #endregion

    #region - 조립기 카테고리 콤보: UIA 이름(항목 컨테이너) -
    /// <summary>
    /// 데이터 템플릿으로 그린 ComboBoxItem 은 화면 글자(ItemTemplate)가 한글이어도 UIA 이름이 바인딩 원본
    /// (EnumDeviceCategory) 의 ToString() 으로 새는 WPF 관행이 있다 — 자동화로는 "Enclosure" 처럼 잡힌다.
    /// ItemContainerStyle 이 AutomationProperties.Name 을 같은 변환기로 명시하는지를 XAML 계약으로 본다.
    /// </summary>
    [Theory]
    [InlineData("Consoles/Assembly/PresetManagerView.xaml", "Devices.Assembly.Presets.Category")]
    [InlineData("Consoles/Assembly/RegisterFromPresetView.xaml", "Devices.Assembly.Register.Category")]
    public void should_bind_automation_name_to_korean_category_text_when_category_combo_is_declared(string relativePath, string automationId)
    {
        var xaml = File.ReadAllText(Path.Combine(ProjectDir(), relativePath));

        Assert.Contains(automationId, xaml, StringComparison.Ordinal);
        Assert.Contains("ComboBox.ItemContainerStyle", xaml, StringComparison.Ordinal);
        Assert.Contains(
            "<Setter Property=\"AutomationProperties.Name\" Value=\"{Binding Converter={StaticResource CategoryText}}\" />",
            xaml, StringComparison.Ordinal);
    }

    private static string ProjectDir([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, ".."));
    #endregion
}
