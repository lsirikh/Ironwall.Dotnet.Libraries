using System.IO;
using Ironwall.Dotnet.Libraries.Utils.Perf;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests.Perf;

/****************************************************************************
   Purpose      : 이벤트 경로 계측(PerfTrace) — 켜기 판정 · 한 줄 형식 왕복 · 이벤트 문맥 · 배경 파일 기록 · 회전
                  (카탈로그 §7 PERF-001 ~ 006 의 단언 수단 "계측 jsonl")
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection(PerfTraceCollection.Name)]   // 붙잡기(전역 받는 곳)를 쓰는 시험끼리 겹치지 않게
public sealed class PerfTraceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ironwall-perf-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { /* 임시 폴더 — 남아도 무해 */ }
    }

    [Theory]
    [Trait("Scenario", "PERF-001,PERF-002,PERF-005,PERF-006")]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData(" On ", true)]
    [InlineData("YES", true)]
    [InlineData("0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("off", false)]
    public void should_turn_on_only_for_affirmative_values_when_environment_variable_is_read(string? value, bool expected)
        => Assert.Equal(expected, PerfTrace.ReadEnabled(value));

    [Fact]
    [Trait("Scenario", "PERF-001,PERF-002,PERF-005,PERF-006")]
    public void should_use_override_path_or_local_app_data_perf_folder_when_resolving_file()
    {
        Assert.Equal(@"D:\x\gis.jsonl", PerfTrace.ResolveFilePath(@"D:\x\gis.jsonl", @"C:\Users\u\AppData\Local", 42));
        Assert.Equal(Path.Combine(@"C:\Users\u\AppData\Local", "Ironwall", "perf", "gis-perf-42.jsonl"),
                     PerfTrace.ResolveFilePath(null, @"C:\Users\u\AppData\Local", 42));
        Assert.Equal(Path.Combine(@"C:\L", "Ironwall", "perf", "gis-perf-7.jsonl"), PerfTrace.ResolveFilePath("  ", @"C:\L", 7));
    }

    [Fact]
    [Trait("Scenario", "PERF-001,PERF-002,PERF-005,PERF-006")]
    public void should_capture_marks_in_order_and_stop_capturing_when_capture_disposed()
    {
        var id = Guid.NewGuid().ToString("N");
        PerfTraceCapture capture;
        using (capture = PerfTrace.Capture())
        {
            Assert.True(PerfTrace.IsEnabled);
            PerfTrace.Mark(PerfStages.NatsReceived, PerfKinds.Detection, id, PerfTrace.Extra(("cmd", "DETECT")));
            PerfTrace.Mark(PerfStages.Enqueued, PerfKinds.Detection, id);
            PerfTrace.Mark(PerfStages.CardAdded, PerfKinds.Detection, id);
        }
        PerfTrace.Mark(PerfStages.PopupRequested, PerfKinds.Detection, id);   // 붙잡기가 끝난 뒤 — 안 잡혀야 한다

        var mine = capture.For(PerfKinds.Detection, id);
        Assert.Equal(new[] { PerfStages.NatsReceived, PerfStages.Enqueued, PerfStages.CardAdded }, mine.Select(r => r.Stage));
        Assert.True(mine[0].T <= mine[1].T && mine[1].T <= mine[2].T);
        Assert.All(mine, r => Assert.Equal(Environment.ProcessId, r.Pid));
        Assert.Equal("DETECT", mine[0].ExtraString("cmd"));
    }

    [Fact]
    [Trait("Scenario", "PERF-001,PERF-002,PERF-005,PERF-006")]
    public void should_stamp_high_resolution_utc_close_to_wall_clock_when_marking()
    {
        var (utc1, mono1) = PerfTrace.Now();
        var (utc2, mono2) = PerfTrace.Now();

        Assert.Equal(DateTimeKind.Utc, utc1.Kind);
        Assert.True(utc2 >= utc1);
        Assert.True(mono2 >= mono1);
        Assert.InRange((DateTime.UtcNow - utc2).Duration().TotalSeconds, 0, 5);   // 벽시계와 같은 기준(장시간 드리프트는 wall 로 본다)
    }

    [Fact]
    [Trait("Scenario", "PERF-001,PERF-002,PERF-005,PERF-006")]
    public void should_round_trip_every_field_when_line_is_serialized_and_parsed()
    {
        var t = new DateTime(2026, 10, 11, 1, 2, 3, DateTimeKind.Utc).AddTicks(1234567);
        var record = new PerfTraceRecord
        {
            T = t, Wall = t.AddMilliseconds(1), MonoMs = 12.3456, Pid = 77, Tid = 5,
            Stage = PerfStages.SymbolState, Kind = PerfKinds.Malfunction, Id = "7001",
            Extra = PerfTrace.Extra(("target", "group:7"), ("state", "Faulted"), ("count", 3), ("ok", true), ("none", null), ("ratio", 0.5)),
        };

        var line = record.ToJsonLine();
        Assert.DoesNotContain('\n', line);
        Assert.True(PerfTraceRecord.TryParse(line, out var parsed));

        Assert.Equal(t, parsed!.T);
        Assert.Equal(t.AddMilliseconds(1), parsed.Wall);
        Assert.Equal(12.346, parsed.MonoMs, 3);
        Assert.Equal((77, 5), (parsed.Pid, parsed.Tid));
        Assert.Equal((PerfStages.SymbolState, PerfKinds.Malfunction, "7001"), (parsed.Stage, parsed.Kind, parsed.Id));
        Assert.Equal("group:7", parsed.ExtraString("target"));
        Assert.Equal("Faulted", parsed.ExtraString("state"));
        Assert.Equal(3, parsed.ExtraNumber("count"));
        Assert.Equal(0.5, parsed.ExtraNumber("ratio"));
        Assert.Equal(true, parsed.Extra!["ok"]);
        Assert.Null(parsed.Extra["none"]);
    }

    [Theory]
    [Trait("Scenario", "PERF-001,PERF-002,PERF-005,PERF-006")]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"stage\":\"enqueued\"}")]   // t 없음
    public void should_skip_line_without_throwing_when_line_is_broken(string line)
        => Assert.False(PerfTraceRecord.TryParse(line, out _));

    [Fact]
    [Trait("Scenario", "PERF-002,PERF-005,PERF-006")]
    public void should_stamp_context_event_when_marking_inside_context_and_fall_back_outside()
    {
        var id = Guid.NewGuid().ToString("N");
        using var capture = PerfTrace.Capture();

        using (PerfTrace.BeginContext(PerfKinds.Detection, id))
        {
            PerfTrace.MarkInContext(PerfStages.SymbolState, PerfKinds.Device, "group:7", PerfTrace.Extra(("tag", id)));
            using (PerfTrace.BeginContext(PerfKinds.Malfunction, id))
                PerfTrace.MarkInContext(PerfStages.SymbolState, PerfKinds.Device, "group:8", PerfTrace.Extra(("tag", id)));
            Assert.Equal((PerfKinds.Detection, id), PerfTrace.CurrentContext);   // 안쪽 범위가 끝나면 바깥 문맥으로
        }
        PerfTrace.MarkInContext(PerfStages.SymbolState, PerfKinds.Device, "group:9", PerfTrace.Extra(("tag", id)));

        var mine = capture.Records.Where(r => r.ExtraString("tag") == id).ToList();
        Assert.Equal(new[] { (PerfKinds.Detection, id), (PerfKinds.Malfunction, id), (PerfKinds.Device, "group:9") },
                     mine.Select(r => (r.Kind, r.Id!)));
        Assert.Equal((null, null), PerfTrace.CurrentContext);
    }

    [Fact]
    [Trait("Scenario", "PERF-001,PERF-007,PERF-013")]
    public void should_write_parseable_lines_in_order_on_background_thread_when_records_enqueued()
    {
        var path = Path.Combine(_directory, "gis-perf-1.jsonl");
        using (var writer = new PerfTraceFileWriter(path))
        {
            for (int i = 1; i <= 200; i++) Assert.True(writer.Enqueue(Record(i)));
            Assert.True(writer.Flush(TimeSpan.FromSeconds(10)));
            Assert.Equal(200, writer.WrittenCount);
            Assert.Equal(0, writer.DroppedCount);
        }

        var records = PerfTraceAnalyzer.ReadRecords(new[] { path });
        Assert.Equal(Enumerable.Range(1, 200).Select(i => i.ToString()), records.Select(r => r.Id));
    }

    [Fact]
    [Trait("Scenario", "PERF-013")]
    public void should_rotate_to_numbered_files_and_read_back_oldest_first_when_size_limit_exceeded()
    {
        var path = Path.Combine(_directory, "gis-perf-2.jsonl");
        using (var writer = new PerfTraceFileWriter(path, maxFileBytes: 1024, maxFiles: 50))
        {
            for (int i = 1; i <= 40; i++) writer.Enqueue(Record(i));
            Assert.True(writer.Flush(TimeSpan.FromSeconds(10)));
        }

        Assert.True(File.Exists(PerfTraceFileWriter.RotatedPath(path, 1)));
        Assert.All(Directory.GetFiles(_directory), f => Assert.True(new FileInfo(f).Length < 1024 + 512, $"{f} 가 상한을 크게 넘음"));
        var files = PerfTraceAnalyzer.WithRotations(path, 50);
        Assert.True(files.Count > 2);
        var records = PerfTraceAnalyzer.ReadRecords(files);
        Assert.Equal(Enumerable.Range(1, 40).Select(i => i.ToString()), records.Select(r => r.Id));   // 회전본 → 현재 순서 = 쓴 순서
    }

    [Fact]
    [Trait("Scenario", "PERF-013")]
    public void should_delete_oldest_rotation_when_file_count_limit_reached()
    {
        var path = Path.Combine(_directory, "gis-perf-3.jsonl");
        using (var writer = new PerfTraceFileWriter(path, maxFileBytes: 1024, maxFiles: 3))
        {
            for (int i = 1; i <= 60; i++) writer.Enqueue(Record(i));
            Assert.True(writer.Flush(TimeSpan.FromSeconds(10)));
        }

        Assert.True(Directory.GetFiles(_directory).Length <= 3);
        Assert.False(File.Exists(PerfTraceFileWriter.RotatedPath(path, 3)));
        var records = PerfTraceAnalyzer.ReadRecords(PerfTraceAnalyzer.WithRotations(path, 3));
        Assert.Equal("60", records[^1].Id);   // 최신은 남는다
        Assert.True(records.Count < 60);
    }

    [Fact]
    [Trait("Scenario", "PERF-001")]
    public void should_name_rotated_files_before_extension()
    {
        Assert.Equal(@"C:\p\gis-perf-9.1.jsonl", PerfTraceFileWriter.RotatedPath(@"C:\p\gis-perf-9.jsonl", 1));
        Assert.Equal(@"C:\p\gis-perf-9.4.jsonl", PerfTraceFileWriter.RotatedPath(@"C:\p\gis-perf-9.jsonl", 4));
    }

    private static PerfTraceRecord Record(int i)
    {
        var (utc, mono) = PerfTrace.Now();
        return new PerfTraceRecord
        {
            T = utc, Wall = DateTime.UtcNow, MonoMs = mono, Pid = 1, Tid = 1,
            Stage = PerfStages.Enqueued, Kind = PerfKinds.Detection, Id = i.ToString(),
            Extra = PerfTrace.Extra(("entry", "env-" + i), ("device", 100 + i)),
        };
    }
}

[CollectionDefinition(Name)]
public sealed class PerfTraceCollection
{
    public const string Name = "PerfTrace";
}
