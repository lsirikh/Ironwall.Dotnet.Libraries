using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/28/2025 9:06:46 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// TIF 이미지 정보 클래스
/// </summary>
public class TifImageInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int BitsPerSample { get; set; }
    public int SamplesPerPixel { get; set; }
    public int PhotometricInterpretation { get; set; }
    public int Compression { get; set; }
    public int ScanlineSize { get; set; }

    public override string ToString()
    {
        return $"{Width}x{Height}, {BitsPerSample}bit/{SamplesPerPixel}samples, " +
               $"Photometric:{PhotometricInterpretation}, Compression:{Compression}";
    }
}