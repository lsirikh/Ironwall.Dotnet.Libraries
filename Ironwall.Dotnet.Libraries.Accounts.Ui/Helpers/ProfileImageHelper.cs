using System.IO;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Helpers;

/****************************************************************************
   Purpose      : 프로필 이미지 확장자/크기 검증 (M-1: 파일 검증 누락 수정)
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public static class ProfileImageHelper
{
    // 서버 허용(jpeg/png/webp/gif)과 정렬. bmp/tiff 는 서버가 400 으로 거부하므로 클라에서 선제 배제, webp/gif 추가.
    public static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    public const long MaxBytes = 5 * 1024 * 1024;   // 5MB

    /// <summary>
    /// 사진으로 쓸 수 없는 파일을 골랐을 때의 팝업 문장(고정). 검증 원문 · 예외 원문(" (Parameter 'sourcePath')" 등)은 로그로만 남긴다(A-51).
    /// </summary>
    public const string RejectedText = "이 파일은 사진으로 쓸 수 없습니다. JPG · PNG · WebP · GIF 형식의 5MB 이하 파일을 고르세요.";

    /// <summary>경로의 이미지가 허용 확장자/크기를 만족하는지 검증.</summary>
    public static bool IsValid(string path, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path)) { error = "경로가 비어 있습니다."; return false; }
        if (!File.Exists(path)) { error = "파일이 존재하지 않습니다."; return false; }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (Array.IndexOf(AllowedExtensions, ext) < 0)
        {
            error = $"허용되지 않은 확장자입니다: {ext} (허용: {string.Join(", ", AllowedExtensions)})";
            return false;
        }

        var len = new FileInfo(path).Length;
        if (len > MaxBytes)
        {
            error = $"파일 크기가 너무 큽니다: {len / 1024}KB > {MaxBytes / 1024}KB";
            return false;
        }
        return true;
    }
}
