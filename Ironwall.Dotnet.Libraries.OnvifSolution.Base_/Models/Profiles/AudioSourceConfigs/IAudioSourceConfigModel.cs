using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioSourceConfigs.AudioSource;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioSourceConfigs
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 3:28:21 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public interface IAudioSourceConfigModel
    {
        string Name { get; set; }
        string Token { get; set; }
        int UseCount { get; set; }
        AudioSourceModel AudioSource { get; set; }
    }
}
