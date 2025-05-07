using Ironwall.Dotnet.Framework.Models.Maps;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 5/26/2023 2:37:31 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public interface IMapFileLoadResponseModel : IResponseModel
    {
        List<MapModel>? Maps { get; set; }
    }
}
