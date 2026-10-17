using System.IO;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Helpers;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests;

public class ProfileImageHelperTests
{
    [Fact]
    public void should_reject_when_path_is_empty()
    {
        var ok = ProfileImageHelper.IsValid("", out var error);
        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void should_reject_when_file_missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"nope_{Guid.NewGuid():N}.png");
        var ok = ProfileImageHelper.IsValid(missing, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void should_reject_when_extension_not_allowed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"img_{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "x");
        try
        {
            var ok = ProfileImageHelper.IsValid(path, out var error);
            Assert.False(ok);
            Assert.Contains("확장자", error);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void should_accept_when_png_within_size_limit()
    {
        var path = Path.Combine(Path.GetTempPath(), $"img_{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47 });   // 4 bytes
        try
        {
            var ok = ProfileImageHelper.IsValid(path, out var error);
            Assert.True(ok);
            Assert.Null(error);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(".webp")]
    [InlineData(".gif")]
    public void should_accept_when_server_supported_format(string ext)
    {
        // 서버 허용(jpeg/png/webp/gif) 정렬 — webp/gif 도 통과해야 한다.
        var path = Path.Combine(Path.GetTempPath(), $"img_{Guid.NewGuid():N}{ext}");
        File.WriteAllBytes(path, new byte[] { 0x00, 0x01, 0x02, 0x03 });
        try
        {
            var ok = ProfileImageHelper.IsValid(path, out var error);
            Assert.True(ok);
            Assert.Null(error);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void should_reject_when_bmp_because_server_rejects()
    {
        // bmp 는 서버가 400 으로 거부하므로 클라에서도 선제 배제(형식 정렬).
        var path = Path.Combine(Path.GetTempPath(), $"img_{Guid.NewGuid():N}.bmp");
        File.WriteAllBytes(path, new byte[] { 0x42, 0x4D });
        try
        {
            var ok = ProfileImageHelper.IsValid(path, out var error);
            Assert.False(ok);
            Assert.Contains("확장자", error);
        }
        finally { File.Delete(path); }
    }
}
