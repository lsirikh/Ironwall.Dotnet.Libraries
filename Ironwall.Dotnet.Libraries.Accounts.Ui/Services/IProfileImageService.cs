namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Services;

/****************************************************************************
   Purpose      : 프로필 이미지 파일 IO 역전 (VM에서 OpenFileDialog/파일복사 결합 제거)
   Notes        : 검증→복사를 서비스로 위임. 저장 경로는 주입(앱 하드코딩 경로 제거).
****************************************************************************/
public interface IProfileImageService
{
    /// <summary>검증된 이미지를 프로필 폴더로 복사하고 저장 경로를 반환. 검증 실패 시 ArgumentException.</summary>
    Task<string> SaveAsync(string sourcePath, string accountKey, CancellationToken ct = default);
}
