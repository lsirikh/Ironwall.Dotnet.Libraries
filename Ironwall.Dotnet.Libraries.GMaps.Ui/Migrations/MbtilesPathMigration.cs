using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Migrations;

/// <summary>
/// 일회성 마이그레이션: C:\ProgramData\Sensorway\PIDS\maps\ → {exe}\maps\
/// 실행 방법: MbtilesPathMigration.RunAsync(gMapDbService, log) 를 명시적으로 한 번 호출
/// </summary>
public static class MbtilesPathMigration
{
    private static readonly string LegacyMapsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Sensorway", "PIDS", "maps");

    private static readonly string LegacyBaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Sensorway", "PIDS");

    private static readonly string NewMapsDir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "maps");

    public static async Task RunAsync(IGMapDbService dbService, ILogService? log = null)
    {
        log?.Info($"[MbtilesPathMigration] 시작");

        MigrateFiles(log);
        await MigrateDbPathsAsync(dbService, log);

        log?.Info($"[MbtilesPathMigration] 완료");
    }

    private static void MigrateFiles(ILogService? log)
    {
        if (!Directory.Exists(LegacyMapsDir))
        {
            log?.Info($"[MbtilesPathMigration] 구 폴더 없음, 파일 이전 생략: {LegacyMapsDir}");
            return;
        }

        Directory.CreateDirectory(NewMapsDir);

        var moved = 0;
        foreach (var src in Directory.GetFiles(LegacyMapsDir, "*.mbtiles"))
        {
            var dest = Path.Combine(NewMapsDir, Path.GetFileName(src));
            if (!File.Exists(dest))
            {
                File.Move(src, dest);
                moved++;
                log?.Info($"[MbtilesPathMigration] 이동: {Path.GetFileName(src)}");
            }
            else
            {
                log?.Info($"[MbtilesPathMigration] 이미 존재, 건너뜀: {Path.GetFileName(src)}");
            }
        }

        log?.Info($"[MbtilesPathMigration] 파일 이전 완료: {moved}개");

        try
        {
            Directory.Delete(LegacyBaseDir, recursive: true);
            log?.Info($"[MbtilesPathMigration] 구 폴더 삭제: {LegacyBaseDir}");
        }
        catch (Exception ex)
        {
            log?.Warning($"[MbtilesPathMigration] 구 폴더 삭제 실패 (수동 삭제 필요): {ex.Message}");
        }
    }

    private static async Task MigrateDbPathsAsync(IGMapDbService dbService, ILogService? log)
    {
        try
        {
            await using var conn = await dbService.OpenConnectionAsync();
            var affected = await conn.ExecuteAsync(
                "UPDATE CustomMaps SET MbtilesPath = REPLACE(MbtilesPath, @old, @new) WHERE MbtilesPath LIKE CONCAT(@old, '%')",
                new { old = LegacyMapsDir, @new = NewMapsDir });

            log?.Info($"[MbtilesPathMigration] DB 경로 업데이트: {affected}건");
        }
        catch (Exception ex)
        {
            log?.Error($"[MbtilesPathMigration] DB 경로 업데이트 실패: {ex.Message}");
            throw;
        }
    }
}
