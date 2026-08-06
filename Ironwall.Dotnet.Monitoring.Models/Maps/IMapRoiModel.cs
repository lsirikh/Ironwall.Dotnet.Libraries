using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;
/****************************************************************************
   Purpose      : 관심지역(ROI) 모델 인터페이스
   Created By   : GHLee
   Created On   : 3/17/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public interface IMapRoiModel : IBaseModel
{
    string Title { get; set; }
    double Latitude { get; set; }
    double Longitude { get; set; }
    double Altitude { get; set; }
    double Zoom { get; set; }   // zoom-float-halfstep FR-14: 하프스텝(17.5) 저장
    int MapId { get; set; }
    DateTime? CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
