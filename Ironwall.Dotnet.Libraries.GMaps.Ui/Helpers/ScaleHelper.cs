using Mysqlx.Datatypes;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/7/2025 2:33:25 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public static class ScaleHelper
{

    public static (double, string) RelativeCreateScalebar(double zoom)
    {
        double scaleX = 0.0;
        var scale = "";

        /* Scale
         * Zoom : 17, Scale : 50m, Length : 1.5 cm
         * Zoom : 16, Scale : 100m, Length : 1.5 cm
         * Zoom : 15, Scale : 300m, Length : 2.5 cm
         * Zoom : 14, Scale : 500m, Length : 2 cm 
         * Zoom : 13, Scale : 1000m, Length : 2 cm
         * 
         * Zoom : 12, Scale : 3000m, Length : 2.2 cm
         * Zoom : 11, Scale : 5000m, Length : 2.2 cm
         * Zoom : 10, Scale : 10Km, Length : 2.2 cm
         * Zoom : 9, Scale : 20Km, Length : 2 cm
         * Zoom : 8, Scale : 30Km, Length : 1.7 cm
         * Zoom : 7, Scale : 50Km, Length : 1.4 cm
         * Zoom : 6, Scale : 100Km, Length : 1.4 cm
         * 
         */

        switch (zoom)
        {
            case 6:
                scaleX = 52.9;
                scale = "100Km";
                break;
            case 7:
                scaleX = 52.9;
                scale = "50Km";
                break;
            case 8:
                scaleX = 64.3;
                scale = "30Km";
                break;
            case 9:
                scaleX = 75.6;
                scale = "20Km";
                break;
            case 10:
                scaleX = 83.1;
                scale = "10Km";
                break;
            case 11:
                scaleX = 83.1;
                scale = "5Km";
                break;
            case 12:
                scaleX = 83.1;
                scale = "3Km";
                break;
            case 13:
                scaleX = 75.59;
                scale = "1Km";
                break;
            case 14:
                scaleX = 75.59;
                scale = "500m";
                break;
            case 15:
                scaleX = 94.5;
                scale = "300m";
                break;
            case 16:
                scaleX = 56.7;
                scale = "100m";
                break;
            case 17:
                scaleX = 56.7;
                scale = "50m";
                break;
            case 18:
                scaleX = 56.7;
                scale = "30m";
                break;
            case 19:
                scaleX = 56.7;
                scale = "15m";
                break;

            default:
                break;
        }

        return (scaleX, scale);

    }
}