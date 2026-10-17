using System;
using System.IO;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : 이미지 파일 복사/삭제/경로 해석 서비스
   Created By   : Claude Code
   Created On   : 2026-05-12
   PRD Reference: docs/prds/PRD_ImageOverlay_FileCopy_On_Register.md
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class ImageFileService : IImageFileService
{
    private readonly ILogService? _log;
    private readonly string _baseDirectory;
    private const string OVERLAY_FOLDER = "Images/Overlays";

    public ImageFileService(ILogService? log = null)
    {
        _log = log;
        _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    }

    public void EnsureImageDirectory()
    {
        var fullPath = Path.Combine(_baseDirectory, OVERLAY_FOLDER);
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
            _log?.Info($"이미지 폴더 생성: {fullPath}");
        }
    }

    public string CopyImageToLocal(string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath))
            throw new ArgumentNullException(nameof(sourcePath));

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"원본 파일을 찾을 수 없습니다: {sourcePath}");

        EnsureImageDirectory();

        var originalName = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var newFileName = $"{originalName}_{timestamp}{extension}";

        var relativePath = Path.Combine(OVERLAY_FOLDER, newFileName).Replace('\\', '/');
        var destinationPath = Path.Combine(_baseDirectory, relativePath);

        File.Copy(sourcePath, destinationPath, overwrite: false);
        _log?.Info($"이미지 복사 완료: {sourcePath} → {relativePath}");

        return relativePath;
    }

    public string GetAbsolutePath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return relativePath;

        if (!IsRelativePath(relativePath))
            return relativePath;

        return Path.Combine(_baseDirectory, relativePath);
    }

    public void DeleteLocalImage(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return;

        try
        {
            var absolutePath = IsRelativePath(relativePath)
                ? Path.Combine(_baseDirectory, relativePath)
                : relativePath;

            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
                _log?.Info($"이미지 파일 삭제: {relativePath}");
            }
        }
        catch (Exception ex)
        {
            _log?.Warning($"이미지 파일 삭제 실패 (무시): {relativePath} — {ex.Message}");
        }
    }

    public bool IsRelativePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return !Path.IsPathRooted(path);
    }
}
