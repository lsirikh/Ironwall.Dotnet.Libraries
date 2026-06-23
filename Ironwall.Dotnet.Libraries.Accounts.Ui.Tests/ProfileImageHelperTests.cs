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
}
