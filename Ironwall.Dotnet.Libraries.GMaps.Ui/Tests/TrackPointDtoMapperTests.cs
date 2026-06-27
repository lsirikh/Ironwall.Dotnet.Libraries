using System;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// TrackPointDtoMapper — 서버 KST(+09:00)→UTC 변환 + 유효성 가드 (데이터소스 토글 API 모드 핵심).
/// </summary>
public class TrackPointDtoMapperTests
{
    private static TrackPointDto Valid(string? observedAt) => new()
    {
        CameraId = 201,
        TrackId = "cam201-1",
        Label = "person",
        ThreatLevel = "THREAT",
        Latitude = 38.1235,
        Longitude = 127.5680,
        DistanceM = 120.5,
        SpeedMps = null,
        ObservedAt = observedAt,
    };

    [Fact]
    public void should_convert_kst_offset_to_utc_when_mapping()
    {
        var m = Valid("2026-06-26T19:30:00+09:00").ToTrackPointModel();

        Assert.NotNull(m);
        Assert.Equal(new DateTime(2026, 6, 26, 10, 30, 0, DateTimeKind.Utc), m!.ObservedAt);
        Assert.Equal(DateTimeKind.Utc, m.ObservedAt.Kind);
        Assert.Equal(201, m.CameraId);
        Assert.Equal("cam201-1", m.TrackId);
        Assert.Equal("THREAT", m.ThreatLevel);
        Assert.Equal(120.5, m.DistanceM);
    }

    [Fact]
    public void should_assume_utc_when_observed_at_is_naive()
    {
        // offset 없는 naive → AssumeUniversal로 UTC 처리(OS 로컬=KST 오해로 인한 9h 오차 방지)
        var m = Valid("2026-06-26T10:30:00").ToTrackPointModel();

        Assert.NotNull(m);
        Assert.Equal(new DateTime(2026, 6, 26, 10, 30, 0, DateTimeKind.Utc), m!.ObservedAt);
    }

    [Fact]
    public void should_return_null_when_track_id_missing()
    {
        var dto = Valid("2026-06-26T10:30:00Z");
        dto.TrackId = "";
        Assert.Null(dto.ToTrackPointModel());
    }

    [Fact]
    public void should_return_null_when_lat_lng_invalid()
    {
        var dto = Valid("2026-06-26T10:30:00Z");
        dto.Latitude = 999;
        dto.Longitude = 999;
        Assert.Null(dto.ToTrackPointModel());
    }

    [Fact]
    public void should_return_null_when_observed_at_unparseable()
    {
        Assert.Null(Valid("not-a-date").ToTrackPointModel());
    }

    [Fact]
    public void should_return_null_when_observed_at_is_null()
    {
        Assert.Null(Valid(null).ToTrackPointModel());
    }

    [Fact]
    public void should_return_null_when_observed_at_future_skew()
    {
        var future = DateTime.UtcNow.AddHours(1).ToString("o");
        Assert.Null(Valid(future).ToTrackPointModel());
    }
}
