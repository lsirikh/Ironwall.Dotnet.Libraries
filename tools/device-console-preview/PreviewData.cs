using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace DeviceConsolePreview;

/// <summary>미리보기용 가짜 데이터 — 목업(장비 콘솔 와이어프레임)과 같은 규모 · 모양.</summary>
internal static class PreviewData
{
    public static void Fill(DeviceProvider devices, DeviceGroupProvider groups, bool isAxis)
    {
        groups.Add(new DeviceGroupModel { Id = 1, Name = "동측 울타리", Description = "1소초 동측", DeviceCount = 6 });
        groups.Add(new DeviceGroupModel { Id = 2, Name = "서측 울타리", Description = "1소초 서측", DeviceCount = 4 });
        groups.Add(new DeviceGroupModel { Id = 3, Name = "정문", DeviceCount = 3 });

        var controllers = new List<ControllerDeviceModel>();
        for (var i = 1; i <= 3; i++)
        {
            var controller = new ControllerDeviceModel
            {
                Id = i, DeviceNumber = i, DeviceName = $"제어기 {i}", IpAddress = $"10.10.1.{10 + i}", Port = 5000 + i,
                Status = i == 2 ? EnumDeviceStatus.ERROR : EnumDeviceStatus.ACTIVATED, IsEnable = true,
                TypeAxisCode = isAxis ? "pids_controller" : null, DeviceGroups = new List<int> { 1 },
            };
            controllers.Add(controller);
            devices.Add(controller);
        }

        for (var i = 1; i <= 8; i++)
        {
            devices.Add(new SensorDeviceModel
            {
                Id = 100 + i, DeviceNumber = i, DeviceName = $"광망 센서 {i}", Controller = controllers[i % 3],
                Status = i == 5 ? EnumDeviceStatus.ERROR : EnumDeviceStatus.ACTIVATED, IsEnable = i != 7,
                TypeAxisCode = isAxis ? "fence" : null, DeviceType = EnumDeviceType.Fence,
                DeviceGroups = new List<int> { i <= 4 ? 1 : 2 },
            });
        }

        for (var i = 1; i <= 6; i++)
        {
            var camera = new CameraDeviceModel
            {
                Id = 200 + i, DeviceNumber = i, DeviceName = $"동측 PTZ {i}", IpAddress = $"10.10.2.{20 + i}", IpPort = 80,
                UserName = "admin", UserPassword = "secret",
                Status = i == 3 ? EnumDeviceStatus.ERROR : i == 6 ? EnumDeviceStatus.DEACTIVATED : EnumDeviceStatus.ACTIVATED,
                IsEnable = i != 6, TypeAxisCode = isAxis ? (i % 2 == 0 ? "fixed" : "ptz") : null, DeviceType = EnumDeviceType.IpCamera,
                Location = "동측 3번 폴", Latitude = 37.5 + i * 0.001, Longitude = 127.03 + i * 0.001, Heading = 45 * i % 360,
                DeviceGroups = i <= 2 ? new List<int> { 1, 3 } : new List<int>(),
            };

            if (isAxis)
            {
                // 1번은 축을 다 받았고, 2번은 형상 축을 못 받았다(미수신 상자), 나머지는 접속 축만.
                var sections = i == 1 ? new[] { "connection", "hardware_spec", "components", "device_status", "device_config" }
                             : i == 2 ? new[] { "connection", "device_status" }
                             : new[] { "connection" };
                camera.Axes = new DeviceAxesModel
                {
                    Connection = new ConnectionAxisModel { Type = "network", IpAddress = camera.IpAddress, IpPort = 80, Protocol = "onvif" },
                    HardwareSpec = i == 1 ? new HardwareSpecModel { Manufacturer = "Hanwha", Model = "XNP-6400RW", Serial = "ZK1A7R0", Firmware = "2.21.04", MacAddress = "00:09:18:AA:BB:01", MaxDetectionRange = 250, OnvifVersion = "21.12" } : null,
                    Meta = new ResponseMeta("detail", sections),
                };
            }
            devices.Add(camera);
        }

        devices.Add(new SpeakerDeviceModel { Id = 301, DeviceNumber = 1, DeviceName = "정문 스피커", Status = EnumDeviceStatus.ACTIVATED, IsEnable = true, DeviceGroups = new List<int> { 3 } });
        devices.Add(new EnclosureDeviceModel { Id = 401, DeviceNumber = 1, DeviceName = "동측 함체 1", Status = EnumDeviceStatus.ACTIVATED, IsEnable = true });
        devices.Add(new EnclosureDeviceModel { Id = 402, DeviceNumber = 2, DeviceName = "동측 함체 2", Status = EnumDeviceStatus.ERROR, IsEnable = true });
        devices.Add(new LampDeviceModel { Id = 501, DeviceNumber = 1, DeviceName = "정문 경광등", Status = EnumDeviceStatus.ACTIVATED, IsEnable = true });
        devices.Add(new GateDeviceModel { Id = 601, DeviceNumber = 1, DeviceName = "1통문", Status = EnumDeviceStatus.ACTIVATED, IsEnable = true });
    }
}

/// <summary>종류 축 어휘 몇 개만 가진 가짜 카탈로그 — 콤보가 채워진 모습을 보려고.</summary>
internal sealed class PreviewCatalog : ICatalogService
{
    private static readonly Dictionary<EnumDeviceCategory, CatalogTypeAxis> Axes = new()
    {
        [EnumDeviceCategory.Controller] = new("type_controller", true, "unknown", null, new[] { new CatalogOption("pids_controller", "PIDS 제어기"), new CatalogOption("io_controller", "IO 제어기") }),
        [EnumDeviceCategory.Sensor] = new("type_sensor", true, "unknown", null, new[] { new CatalogOption("fence", "광망"), new CatalogOption("pir", "PIR"), new CatalogOption("radar", "레이더") }),
        [EnumDeviceCategory.Camera] = new("type_camera", true, "unknown", null, new[] { new CatalogOption("ptz", "PTZ"), new CatalogOption("fixed", "고정형"), new CatalogOption("thermal", "열상") }),
    };

    public bool IsLoaded => true;
    public event EventHandler? CatalogChanged { add { } remove { } }

    public Task<bool> EnsureLoadedAsync(CancellationToken token = default) => Task.FromResult(true);
    public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    public CatalogTypeAxis? TypeAxis(EnumDeviceCategory category) => Axes.GetValueOrDefault(category);
    public IReadOnlyList<CatalogOption> TypeAxisValues(EnumDeviceCategory category) => TypeAxis(category)?.Values ?? Array.Empty<CatalogOption>();
    public bool IsTypeAxisValue(EnumDeviceCategory category, string? code) => TypeAxisValues(category).Any(o => o.Code == code);
    public string TypeAxisLabel(EnumDeviceCategory category, string? code) => TypeAxisValues(category).FirstOrDefault(o => o.Code == code)?.Label ?? code ?? string.Empty;
    public IReadOnlyList<CatalogExtraAxis> ExtraAxes(EnumDeviceCategory category) => Array.Empty<CatalogExtraAxis>();

    public IReadOnlyList<CatalogOption> Vocabulary(string name, bool includeDeprecated = false, EnumDeviceCategory? appliesTo = null)
        => name == "component_type" ? new[] { new CatalogOption("door", "문"), new CatalogOption("heater", "히터"), new CatalogOption("fan", "팬") } : Array.Empty<CatalogOption>();

    public string LabelOf(string vocabularyName, string? code) => code ?? string.Empty;
}
