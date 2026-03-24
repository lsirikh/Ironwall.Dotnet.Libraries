using Ironwall.Dotnet.Monitoring.Models.Maps;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
/****************************************************************************
   Purpose      : 관심지역(ROI) 이벤트 인자
   Created By   : GHLee
   Created On   : 3/17/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class MapRoiEventArgs : EventArgs
{
    public IMapRoiModel Roi { get; }

    public MapRoiEventArgs(IMapRoiModel roi)
    {
        Roi = roi;
    }
}

public class MapRoiTitleEditedEventArgs : EventArgs
{
    public IMapRoiModel Roi { get; }
    public string NewTitle { get; }

    public MapRoiTitleEditedEventArgs(IMapRoiModel roi, string newTitle)
    {
        Roi = roi;
        NewTitle = newTitle;
    }
}
