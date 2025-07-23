namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Dappers
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 2/25/2024 3:45:29 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraDeviceDapper : ICameraDeviceDapper
    {
        public CameraDeviceDapper()
        {

        }

        public CameraDeviceDapper(ICameraDeviceModel model)
        {
            Id = model.Id;
            DeviceName = model.DeviceName;
            Zone = model.Zone.Id;
            UserName = model.UserName;
            Password = model.Password;
            IpAddress = model.IpAddress;
            Port = model.Port;
            PortOnvif = model.PortOnvif;
            PortRtsp = model.PortRtsp;
            RtspAuthRequired = model.RtspAuthRequired;
            IsDummy = model.IsDummy;
            DummyOption = model.DummyOption;

            Manufacturer = model.Manufacturer;
            DeviceModel = model.DeviceModel;
            FirmwareVersion = model.FirmwareVersion;
            SerialNumber = model.SerialNumber;
            HardwareId = model.HardwareId;
        }

        public int Id { get; set; }

        public string DeviceName { get; set; }

        public int Zone { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string IpAddress { get; set; }

        public int Port { get; set; }

        public int PortOnvif { get; set; }

        public int PortRtsp { get; set; }

        public bool RtspAuthRequired { get; set; }

        public bool IsDummy { get; set; }

        public string DummyOption { get; set; }

        public string Manufacturer { get; set; }

        public string DeviceModel { get; set; }

        public string FirmwareVersion { get; set; }

        public string SerialNumber { get; set; }

        public string HardwareId { get; set; }

    }
}
