namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 2/7/2024 1:19:31 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public enum EnumCameraStatus
    {
        NONE,//미설정
        NOT_AVAILABLE,//CameraDeviceModel의 Instance는 존재하나, Onvif로 데이터셋이 온전하지 않은 경우
        AVAILABLE, //Onvif로 데이터 셋이 온전한 경우
    }
}
