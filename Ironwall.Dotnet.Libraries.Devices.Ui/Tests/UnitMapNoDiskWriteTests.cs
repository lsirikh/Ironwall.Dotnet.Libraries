using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : TEST-45 — 숨은 저장 없음(NFR-14 · FR-51): 관계도 코드는 디스크를 쓰지 않고, 세션 전용 한 바퀴는 파일 시스템을 바꾸지 않는다
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 시나리오 SIM-P042 · P043 · P047 · M025.
                  ① 소스 검사 — Consoles/Units/Map/** 전부(레인 A · C 파일 포함)에 파일 쓰기 API 0.
                     저장소를 못 찾으면 Skip 이 아니라 실패다(검사가 조용히 공허해지지 않게).
                  ② 세션 전용 한 바퀴(끌기 · M 확정 · 초기화 · 되돌리기 · 상위 변경 정리)를 레인 B 부품으로 돌린 뒤
                     %TEMP% · 시험 폴더 · 관계도 폴더의 파일 목록이 그대로인지. 개인 표시 설정(console-prefs)은 이 시험 밖이다(뷰 키만 허용 — 레인 A).
                     ⚠ 관계도 뷰모델(레인 A)이 녹색이 되면 같은 한 바퀴를 뷰모델 경로로 한 번 더 돌린다(Phase 4 통합).
****************************************************************************/
public class UnitMapNoDiskWriteTests
{
    /// <summary>파일 시스템에 쓰는 API — 캐시 · 임시 · 대체 파일 모두 금지(NFR-14). 주석과 문자열 안은 보지 않는다.</summary>
    private static readonly Regex DiskApi = new(
        @"\bFile\s*\.|\bFileStream\b|\bIsolatedStorage|\bStreamWriter\b|\bFileInfo\b|\bDirectory\s*\.|\bBinaryWriter\b|\bMemoryMappedFile\b",
        RegexOptions.Compiled);

    [Fact]
    public void should_find_no_file_system_write_api_in_any_unit_map_source()
    {
        var mapDir = MapSourceDirectory();
        var sources = Directory.EnumerateFiles(mapDir, "*.cs", SearchOption.AllDirectories).ToList();
        Assert.True(sources.Count >= 5, $"관계도 소스를 찾지 못했다(검사가 공허해진다): {mapDir}");

        var offenders = new List<string>();
        foreach (var path in sources)
        {
            var code = StripCommentsAndStrings(File.ReadAllText(path));
            foreach (Match m in DiskApi.Matches(code))
                offenders.Add($"{Path.GetFileName(path)}: {m.Value}");
        }

        Assert.True(offenders.Count == 0, "관계도 코드가 디스크에 쓴다(NFR-14 위반):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void should_leave_the_file_system_unchanged_after_a_full_session_only_round()
    {
        // Arrange — 볼 폴더들의 파일 목록(이름 · 크기 · 수정 시각)
        var watched = new[] { Path.GetTempPath(), AppContext.BaseDirectory, MapSourceDirectory() };
        var before = watched.ToDictionary(d => d, d => Listing(d));
        var org = UnitMapTestData.Standard200();
        var unit = org.IdOf("4중대");
        var store = new SessionOnlyUnitLayoutStore();
        var undo = new UnitMapUndoStack();

        // Act — 세션 전용 한 바퀴: 끌기(위치) · M 확정 · 되돌리기 · 전체 초기화 · 초기화 되돌리기 · 상위 변경 뒤 정리
        void Move(int id, Vector delta)
        {
            var previous = store.Snapshot.DeltaOf(id);
            store.Apply(UnitMapLayoutSync.MoveChange(id, delta));
            undo.Push(new UnitMapPositionUndo(id, previous, delta, null, UnitMapLayoutSync.TouchedUnits(org.Tree, id), SessionOnly: true));
        }
        Move(unit, new Vector(40, -20));                                        // 끌기
        Move(unit, new Vector(60, -20));                                        // M 모드 → 화살표 → Enter
        var top = (UnitMapPositionUndo)undo.Peek()!;
        store.Apply(UnitMapLayoutSync.ReverseChange(top.UnitId, top.Before));   // Ctrl+Z
        undo.CompleteUndo(top, succeeded: true);

        var beforeReset = store.Snapshot.Deltas.ToDictionary(kv => kv.Key, kv => kv.Value);
        store.Apply(UnitLayoutChange.ClearEverything());                        // [배치 초기화]
        var resetEntry = new UnitMapLayoutResetUndo(beforeReset, All: true, Saved: null, SessionOnly: true);
        undo.Push(resetEntry);
        var plan = UnitMapLayoutSync.PlanResetUndo(resetEntry, store.Snapshot, org.Tree);
        if (plan.Change is { } restore) store.Apply(restore);                   // 초기화 되돌리기

        if (UnitMapLayoutSync.PlanReparentCleanup(UnitMapLayoutState.SessionOnly, null, store.Snapshot, unit) is UnitMapReparentCleanup.ClearInMemory)
            store.Apply(UnitLayoutChange.ClearOne(unit));                       // 상위 변경 뒤 정리

        // Assert — 한 바퀴가 실제로 돌았고(공허 방지), 파일 시스템은 그대로다
        Assert.Equal(new Vector(40, -20), plan.Change!.Set[unit]);
        Assert.Null(store.Snapshot.DeltaOf(unit));
        Assert.Equal(0, store.Snapshot.Version);
        foreach (var dir in watched)
            Assert.Equal(before[dir], Listing(dir));
    }

    #region - Helpers -
    /// <summary>
    /// 시험 소스 위치에서 올라가 관계도 폴더를 찾는다. 못 찾으면 <b>실패</b>(Skip 아님).
    /// </summary>
    private static string MapSourceDirectory([CallerFilePath] string here = "")
    {
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(here)!); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Consoles", "Units", "Map");
            if (Directory.Exists(candidate)) return candidate;
            candidate = Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Units", "Map");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new Xunit.Sdk.XunitException($"관계도 소스 폴더를 찾지 못했다 — 시작점 {here}");
    }

    /// <summary>폴더 바로 아래 파일 목록(이름 · 크기 · 수정 시각). 하위 폴더는 보지 않는다.</summary>
    private static string Listing(string dir)
    {
        var isTemp = string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir)),
                                   Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                                   StringComparison.OrdinalIgnoreCase);
        var entries = new DirectoryInfo(dir).EnumerateFiles()
            .Where(f => !isTemp || LooksLikeOurs(f.Name))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => $"{f.Name}|{f.Length}|{f.LastWriteTimeUtc.Ticks}");
        return string.Join("\n", entries);
    }

    /// <summary>
    /// %TEMP% 최상위에는 이 시험과 무관한 프로세스(다른 시험 실행기 · IDE · 헤드 UI 시험)가 수시로 쓴다 — 그 폴더에서는
    /// 관계도 · 배치 · 부대 이름이 들어간 파일만 본다(흔들리는 시험을 만들지 않는다). 시험 폴더 · 관계도 폴더는 전부 본다.
    /// </summary>
    private static bool LooksLikeOurs(string name)
        => Regex.IsMatch(name, "unit|layout|umap|관계도|배치", RegexOptions.IgnoreCase);

    /// <summary>주석 · 문자열 리터럴을 지운다 — 문구 안의 "File." 같은 낱말에 걸리지 않게.</summary>
    private static string StripCommentsAndStrings(string code)
    {
        code = Regex.Replace(code, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        code = Regex.Replace(code, @"//[^\n]*", string.Empty);
        code = Regex.Replace(code, @"@""(?:[^""]|"""")*""|""(?:\\.|[^""\\])*""", "\"\"");
        return code;
    }
    #endregion
}
