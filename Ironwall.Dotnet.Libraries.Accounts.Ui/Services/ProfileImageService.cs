using System.IO;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Helpers;   // ProfileImageHelper
using Ironwall.Dotnet.Libraries.Base.Services;         // ILogService

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Services;

/****************************************************************************
   Purpose      : 프로필 이미지 저장 서비스 (검증 + 폴더 자동생성 + 복사)
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public sealed class ProfileImageService : IProfileImageService
{
    #region - Ctors -
    /// <param name="baseDir">저장 폴더(미지정 시 실행파일 기준 Profile 폴더).</param>
    public ProfileImageService(ILogService? log = default, string? baseDir = default)
    {
        _log = log;
        _baseDir = string.IsNullOrWhiteSpace(baseDir)
            ? Path.Combine(AppContext.BaseDirectory, "Profile")
            : baseDir!;
    }
    #endregion

    #region - Processes -
    public async Task<string> SaveAsync(string sourcePath, string accountKey, CancellationToken ct = default)
    {
        if (!ProfileImageHelper.IsValid(sourcePath, out var error))
            throw new ArgumentException(error, nameof(sourcePath));

        Directory.CreateDirectory(_baseDir);
        var ext = Path.GetExtension(sourcePath);
        var dst = Path.Combine(_baseDir, $"{accountKey}{ext}");

        await using var src = File.OpenRead(sourcePath);
        await using var output = File.Create(dst);
        await src.CopyToAsync(output, ct).ConfigureAwait(false);

        _log?.Info($"프로필 이미지 저장: {dst}");
        return dst;
    }
    #endregion

    #region - Attributes -
    private readonly string _baseDir;
    private readonly ILogService? _log;
    #endregion
}
