namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : 이미지 파일 복사/삭제/경로 해석 서비스 인터페이스
   Created By   : Claude Code
   Created On   : 2026-05-12
   PRD Reference: docs/prds/PRD_ImageOverlay_FileCopy_On_Register.md
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public interface IImageFileService
{
    /// <summary>
    /// Images/Overlays/ 폴더 자동 생성 (없으면)
    /// </summary>
    void EnsureImageDirectory();

    /// <summary>
    /// 원본 이미지를 Images/Overlays/에 복사하고 상대 경로 반환
    /// </summary>
    /// <param name="sourcePath">원본 파일 절대 경로</param>
    /// <returns>복사된 파일의 상대 경로 (예: Images/Overlays/map_20260512_143000.png)</returns>
    string CopyImageToLocal(string sourcePath);

    /// <summary>
    /// 상대 경로를 절대 경로로 변환
    /// </summary>
    string GetAbsolutePath(string relativePath);

    /// <summary>
    /// 복사된 이미지 파일 삭제 (실패 시 로그만 출력)
    /// </summary>
    void DeleteLocalImage(string relativePath);

    /// <summary>
    /// 경로가 상대 경로인지 판별
    /// </summary>
    bool IsRelativePath(string path);
}
