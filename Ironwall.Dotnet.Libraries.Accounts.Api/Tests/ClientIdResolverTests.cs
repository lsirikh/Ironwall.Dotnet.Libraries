using System;
using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
using Ironwall.Dotnet.Libraries.Api.Helpers;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// 세션 축(<c>client_id</c>) 단일화 회귀 — 2026-09-30.
///
/// <para><b>무엇을 지키는 시험인가</b>: 서버는 세션을 <c>(user_id, client_id)</c> 로 가른다.
/// 이 값이 <b>전 PC 공통</b>이면 관리자가 <c>session_self_replace_enabled</c> 를 켜는 순간
/// 관제석끼리 서로 축출한다. 그래서 옛 공용값(<c>central-ui</c> · <c>gis-monitoring</c>)이
/// 세션 축으로 절대 나가지 않아야 한다.</para>
///
/// <para>파일 시스템을 건드리지 않는 <c>Build</c>/판정 경로만 본다.
/// <c>%ProgramData%</c> 파일 생성은 환경 의존이라 여기서 단언하지 않는다(실기 확인 항목).</para>
/// </summary>
public class ClientIdResolverTests
{
    private const string Legacy1 = "central-ui";
    private const string Legacy2 = "gis-monitoring";

    // ── 옛 공용값 판정 ─────────────────────────────────────────────

    [Theory]
    [InlineData("central-ui")]
    [InlineData("gis-monitoring")]
    [InlineData("CENTRAL-UI")]      // 대소문자 무관
    [InlineData("  central-ui  ")]  // 공백 무관
    public void should_flag_as_legacy_shared_when_value_is_an_old_common_id(string value)
    {
        Assert.True(ClientIdResolver.IsLegacyShared(value));
    }

    [Theory]
    [InlineData("gis-fd44d1f63952")]
    [InlineData("gis-monitoring:PC01")]   // 옛 머신명 축도 '공용값' 은 아니다
    [InlineData("sso-server")]
    [InlineData(null)]
    [InlineData("")]
    public void should_not_flag_as_legacy_shared_when_value_is_not_an_old_common_id(string? value)
    {
        Assert.False(ClientIdResolver.IsLegacyShared(value));
    }

    // ── 서버 패턴 판정 ─────────────────────────────────────────────

    [Theory]
    [InlineData("gis-fd44d1f63952")]
    [InlineData("gis-monitoring:PC01")]
    [InlineData("a")]
    [InlineData("A.b_c:d-e")]
    public void should_accept_when_value_matches_server_pattern(string value)
    {
        Assert.True(ClientIdResolver.IsWellFormed(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("한글이름")]          // 비ASCII
    [InlineData("has space")]
    [InlineData("bad/slash")]
    public void should_reject_when_value_violates_server_pattern(string? value)
    {
        Assert.False(ClientIdResolver.IsWellFormed(value));
    }

    [Fact]
    public void should_reject_when_value_exceeds_64_chars()
    {
        Assert.False(ClientIdResolver.IsWellFormed(new string('a', 65)));
        Assert.True(ClientIdResolver.IsWellFormed(new string('a', 64)));
    }

    // ── 설정값 존중 / 대체 ────────────────────────────────────────

    [Fact]
    public void should_keep_configured_value_when_it_is_valid_and_not_shared()
    {
        var warnings = new List<string>();
        var result = ClientIdResolver.Build("gis-fd44d1f63952", warnings.Add);

        Assert.Equal("gis-fd44d1f63952", result);
        Assert.Empty(warnings);   // 존중했으므로 경고가 없어야 한다
    }

    [Fact]
    public void should_trim_configured_value_when_it_has_surrounding_space()
    {
        Assert.Equal("gis-abc123", ClientIdResolver.Build("  gis-abc123  "));
    }

    [Theory]
    [InlineData(Legacy1)]
    [InlineData(Legacy2)]
    public void should_replace_configured_value_when_it_is_an_old_common_id(string legacy)
    {
        var warnings = new List<string>();
        var result = ClientIdResolver.Build(legacy, warnings.Add);

        // 핵심 단언: 공용값이 그대로 나가면 안 된다.
        Assert.NotEqual(legacy, result);
        Assert.False(ClientIdResolver.IsLegacyShared(result));
        Assert.True(ClientIdResolver.IsWellFormed(result));

        // 왜 바뀌었는지 진단이 남아야 한다 — 조용한 대체는 다음 세션을 헤매게 만든다.
        Assert.Contains(warnings, w => w.Contains(legacy));
    }

    [Fact]
    public void should_replace_configured_value_when_it_violates_server_pattern()
    {
        var warnings = new List<string>();
        var result = ClientIdResolver.Build("한글PC이름", warnings.Add);

        Assert.True(ClientIdResolver.IsWellFormed(result));
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public void should_produce_well_formed_value_when_configured_value_is_absent()
    {
        foreach (var absent in new string?[] { null, "", "   " })
        {
            var result = ClientIdResolver.Build(absent);
            Assert.True(ClientIdResolver.IsWellFormed(result),
                $"빈 설정값('{absent}')에서도 서버 패턴을 만족해야 한다 — 실제: '{result}'");
            Assert.False(ClientIdResolver.IsLegacyShared(result));
        }
    }

    // ── 호출 순서 안전성 (캐시 선점 회귀) ─────────────────────────

    /// <summary>
    /// 설정값을 모르는 호출부(<c>ClientIdentity.Current</c>)가 먼저 와서 캐시를 선점하더라도,
    /// 뒤에 오는 <b>유효한 설정값이 이겨야</b> 한다. 초안 구현은 <c>??=</c> 한 줄이라
    /// 먼저 온 폴백값이 관리자 지정값을 영구히 삼켰다.
    /// </summary>
    [Fact]
    public void should_prefer_configured_value_when_it_arrives_after_a_fallback_took_the_cache()
    {
        ClientIdResolver.ResetCache();
        try
        {
            var first = ClientIdResolver.Resolve(configuredId: null);   // 폴백이 캐시 선점
            Assert.True(ClientIdResolver.IsWellFormed(first));

            var warnings = new List<string>();
            var second = ClientIdResolver.Resolve("gis-configured01", warnings.Add);

            Assert.Equal("gis-configured01", second);
            Assert.NotEmpty(warnings);   // 바로잡았다는 사실이 남아야 한다
        }
        finally
        {
            ClientIdResolver.ResetCache();
        }
    }

    [Fact]
    public void should_stay_stable_when_resolve_is_called_repeatedly()
    {
        ClientIdResolver.ResetCache();
        try
        {
            var a = ClientIdResolver.Resolve("gis-stable01");
            var b = ClientIdResolver.Resolve("gis-stable01");
            var c = ClientIdResolver.Resolve(configuredId: null);   // 설정값 없음 → 이미 정해진 값 유지

            Assert.Equal("gis-stable01", a);
            Assert.Equal(a, b);
            Assert.Equal(a, c);
        }
        finally
        {
            ClientIdResolver.ResetCache();
        }
    }

    // ── 본문·헤더 단일화 ──────────────────────────────────────────

    /// <summary>
    /// 로그인 본문(<c>ClientIdentity.Current</c>)과 헤더(<c>ClientIdResolver.Resolve</c>)가
    /// <b>같은 값</b>이어야 한다. 서버는 헤더를 먼저 보고 본문은 헤더가 없을 때만 보므로
    /// (<c>auth.py</c>), 두 값이 다르면 본문이 조용히 버려진다.
    /// </summary>
    [Fact]
    public void should_send_the_same_client_id_in_body_and_header()
    {
        ClientIdResolver.ResetCache();
        try
        {
            var header = ClientIdResolver.Resolve("gis-unified01");
            var body = ClientIdentity.Current;

            Assert.Equal(header, body);
        }
        finally
        {
            ClientIdResolver.ResetCache();
        }
    }

    /// <summary>
    /// 옛 구현은 본문에 <c>gis-monitoring:{머신명}</c> 을 실었다. 그 축은 폐기됐다 —
    /// 머신명이 값에 섞여 나오면 회귀다(이미지 복제·이름 변경에 약하고 설치 고유값과 어긋난다).
    /// </summary>
    [Fact]
    public void should_not_embed_machine_name_when_building_client_id()
    {
        ClientIdResolver.ResetCache();
        try
        {
            var machine = Environment.MachineName;
            var value = ClientIdentity.Current;

            Assert.DoesNotContain(machine, value, StringComparison.OrdinalIgnoreCase);
            Assert.False(ClientIdResolver.IsLegacyShared(value));
        }
        finally
        {
            ClientIdResolver.ResetCache();
        }
    }
}
