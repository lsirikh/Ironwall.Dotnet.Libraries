using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/28/2025 9:18:43 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// TIF 파일 정보 클래스
/// </summary>
public class TifFileInfo
{
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public bool FileExists { get; set; }
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; }

    // 이미지 기본 정보
    public int Width { get; set; }
    public int Height { get; set; }
    public int BitsPerSample { get; set; }
    public int SamplesPerPixel { get; set; }
    public int PhotometricInterpretation { get; set; }
    public int Compression { get; set; }
    public int ScanlineSize { get; set; }

    // 해상도 정보
    public double XResolution { get; set; }
    public double YResolution { get; set; }
    public int ResolutionUnit { get; set; }

    // 구조 정보
    public bool IsTiled { get; set; }
    public int TileWidth { get; set; }
    public int TileHeight { get; set; }
    public bool HasColormap { get; set; }
    public bool IsGeoTiff { get; set; }

    // 메모리 정보
    public long TotalPixels { get; set; }
    public long EstimatedRawSize { get; set; }
    public long EstimatedBitmapSize { get; set; }

    // 호환성 정보
    public bool IsBitmapCompatible { get; set; }
    public List<string> CompatibilityIssues { get; set; } = new List<string>();
}