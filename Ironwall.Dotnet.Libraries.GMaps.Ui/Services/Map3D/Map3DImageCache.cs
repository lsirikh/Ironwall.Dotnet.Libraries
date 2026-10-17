using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;

/****************************************************************************
   Purpose      : 지도 3D 오버레이 이미지 인코딩 디스크 캐시 — 3D 진입마다 큰 이미지(예: 6.3 MB PNG)를 다시 인코딩하지 않게.
   Note         : ★ 키 = 원본 파일 경로 + 크기 + 수정 시각(Map3DOverlay.ImageCacheKey) — 파일이 바뀌면 새로 인코딩한다.
                    원본 파일을 모르는 그림(메모리에서 만든 BitmapSource)은 캐시 없이 인코딩만 한다.
                  ★ 값 = 긴 변 2048 px 이하로 줄인 뒤 투명 픽셀이 없으면 JPEG(품질 85), 있으면 PNG.
                  ★ 위치 = %LocalAppData%\Ironwall\Gis\map3d-cache(실행 폴더 아님 — 설치 폴더 쓰기 권한과 무관). 없으면 만든다.
                    쓰기는 임시 파일 → 바꿔 넣기(반쯤 쓴 파일을 읽지 않게). 캐시를 못 쓰면 경고만 남기고 인코딩 결과를 그대로 쓴다.
   Created On   : 2026-10-17 · Sensorway Co., Ltd.
****************************************************************************/
internal static class Map3DImageCache
{
    private const string JpegMime = "image/jpeg";
    private const string PngMime = "image/png";

    /// <summary>기본 캐시 폴더.</summary>
    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ironwall", "Gis", "map3d-cache");

    /// <summary>캐시에 있으면 꺼내고, 없으면 인코딩해 넣는다(원본 파일을 모르면 인코딩만).</summary>
    public static (byte[] Bytes, string Mime) GetOrEncode(BitmapSource source, string directory, ILogService? log = null)
    {
        if (SourceFile(source) is not { } file) return Encode(source);
        string key = Map3DOverlay.ImageCacheKey(file.FullName, file.Length, file.LastWriteTimeUtc);
        foreach (var mime in new[] { JpegMime, PngMime })
        {
            string cached = Path.Combine(directory, key + Extension(mime));
            if (!File.Exists(cached)) continue;
            try { return (File.ReadAllBytes(cached), mime); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log?.Warning($"[Map3D] 이미지 캐시 읽기 실패(다시 인코딩): {ex.Message}"); }
        }
        var encoded = Encode(source);
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, key + Extension(encoded.Mime));
            string temp = $"{path}.{Guid.NewGuid():N}.tmp";
            File.WriteAllBytes(temp, encoded.Bytes);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log?.Warning($"[Map3D] 이미지 캐시 쓰기 실패(건너뜀): {ex.Message}"); }
        return encoded;
    }

    /// <summary>긴 변 2048 px 이하로 줄이고, 투명 픽셀이 없으면 JPEG(품질 85) · 있으면 PNG 로 인코딩한다.</summary>
    public static (byte[] Bytes, string Mime) Encode(BitmapSource source)
    {
        var fitted = Map3DOverlayBridge.Fit(source, Map3DOverlay.CachedImageSide);
        var bgra = new FormatConvertedBitmap(fitted, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
        bgra.CopyPixels(pixels, bgra.PixelWidth * 4, 0);
        string mime = Map3DOverlay.ImageMime(Map3DOverlay.HasTransparentPixel(pixels));
        if (mime == PngMime) return (Map3DOverlayBridge.EncodePng(fitted), mime);

        var encoder = new JpegBitmapEncoder { QualityLevel = Map3DOverlay.JpegQuality };
        encoder.Frames.Add(BitmapFrame.Create(new FormatConvertedBitmap(fitted, PixelFormats.Bgr24, null, 0)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return (stream.ToArray(), mime);
    }

    /// <summary>파일에서 읽은 그림(BitmapImage.UriSource = 로컬 파일)이면 그 파일, 아니면 null.</summary>
    private static FileInfo? SourceFile(BitmapSource source)
    {
        if (source is not BitmapImage { UriSource: { IsAbsoluteUri: true, IsFile: true } uri }) return null;
        var file = new FileInfo(uri.LocalPath);
        return file.Exists ? file : null;
    }

    private static string Extension(string mime) => mime == JpegMime ? ".jpg" : ".png";
}
