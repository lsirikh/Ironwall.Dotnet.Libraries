namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Dappers
{
    public interface ICameraDeviceDapper
    {
        string DeviceModel { get; set; }
        string DeviceName { get; set; }
        string DummyOption { get; set; }
        string FirmwareVersion { get; set; }
        string HardwareId { get; set; }
        int Id { get; set; }
        string IpAddress { get; set; }
        bool IsDummy { get; set; }
        string Manufacturer { get; set; }
        string Password { get; set; }
        int Port { get; set; }
        int PortOnvif { get; set; }
        int PortRtsp { get; set; }
        bool RtspAuthRequired { get; set; }
        string SerialNumber { get; set; }
        string UserName { get; set; }
        int Zone { get; set; }
    }
}