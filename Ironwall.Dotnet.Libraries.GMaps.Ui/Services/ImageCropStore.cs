using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>자른 그림 옆 기록 — 자르기 전 그림(저장 경로 그대로)과 그 그림 픽셀 크기 · 자른 상자.</summary>
public sealed record ImageCropRecord(string Source, int SourceWidth, int SourceHeight, int X, int Y, int Width, int Height)
{
    public Int32Rect Box => new(X, Y, Width, Height);
}

/****************************************************************************
   Purpose      : 오버레이 이미지 자르기 파일 — 자른 PNG 를 오버레이 저장 폴더(Images/Overlays)에 새 파일로 쓰고, 옆에 기록(.crop.json)을 둔다.
   Note         : ★ 원본은 건드리지 않는다. 자른 그림 = "Images/Overlays/{원래 이름}_crop_{시각}.png"(ImageFileService 와 같은 상대 경로 규칙).
                  ★ 기록 = 자른 그림 경로 + ".crop.json" — 「자르기 되돌리기」가 자르기 전 그림 · 상자를 읽는다(없으면 자른 그림이 아님).
                  ★ toAbsolute = 저장 경로(상대) → 절대 경로(제품 = IImageFileService.GetAbsolutePath, 시험 = 임시 폴더).
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
****************************************************************************/
public static class ImageCropStore
{
    public const string OverlayFolder = "Images/Overlays";
    public const string RecordSuffix = ".crop.json";

    /// <summary>그림 파일을 읽어 둔다(파일을 잠그지 않음).</summary>
    public static BitmapSource Load(string absolutePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>자른 그림을 새 PNG 로 쓰고 기록을 남긴다. 돌려주는 값 = 저장 경로(상대).</summary>
    public static string Save(Func<string, string> toAbsolute, string sourcePath, BitmapSource cropped, Int32Rect box, int sourceWidth, int sourceHeight)
    {
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        var relative = $"{OverlayFolder}/{name}_crop_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        var absolute = toAbsolute(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        File.WriteAllBytes(absolute, ImageCropMath.EncodePng(cropped));
        var record = new ImageCropRecord(sourcePath, sourceWidth, sourceHeight, box.X, box.Y, box.Width, box.Height);
        File.WriteAllText(absolute + RecordSuffix, JsonSerializer.Serialize(record));
        return relative;
    }

    /// <summary>자른 그림이면 그 기록, 아니면(기록 없음 · 읽기 실패) null.</summary>
    public static ImageCropRecord? Read(Func<string, string> toAbsolute, string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var file = toAbsolute(path) + RecordSuffix;
        if (!File.Exists(file)) return null;
        try { return JsonSerializer.Deserialize<ImageCropRecord>(File.ReadAllText(file)); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }
}
