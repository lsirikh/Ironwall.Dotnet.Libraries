namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface ICameraUrlsModel
{
    string? HomepageUrl { get; set; }
    string? OnvifDeviceService { get; set; }
    string? RtspMain { get; set; }
    string? RtspSub { get; set; }
    string? WebrtcMain { get; set; }
    string? SnapshotCh1 { get; set; }
}
