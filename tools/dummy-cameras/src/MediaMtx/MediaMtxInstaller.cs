using System.IO.Compression;
using System.Security.Cryptography;
using DummyCameras.Infra;

namespace DummyCameras.MediaMtx;

/// <summary>
/// Downloads the official MediaMTX Windows build from GitHub releases into tools/dummy-cameras/bin/mediamtx/&lt;ver&gt;/
/// (gitignored) and verifies it against a PINNED SHA-256 before extracting. The pin was cross-checked (2026-09-30)
/// against two independent sources: the release's checksums.sha256 and the GitHub API asset digest.
/// To upgrade: change <see cref="Version"/> and <see cref="ZipSha256"/> together (never auto-trust a new hash).
/// </summary>
public static class MediaMtxInstaller
{
    public const string Version = "v1.21.1";
    public const string ZipName = "mediamtx_" + Version + "_windows_amd64.zip";
    public const string ZipSha256 = "faa97974861eb75a68b5aa326c78e7e7a6f670b5ef191bace78e715130381f23";
    public static readonly Uri DownloadUrl = new($"https://github.com/bluenviron/mediamtx/releases/download/{Version}/{ZipName}");

    public static string InstallDir => Path.Combine(ToolPaths.BinDir, "mediamtx", Version);
    public static string ExePath => Path.Combine(InstallDir, "mediamtx.exe");

    /// <summary>Returns the exe path, downloading + verifying on first use.</summary>
    public static async Task<string> EnsureAsync(TextWriter log, CancellationToken ct)
    {
        if (File.Exists(ExePath)) return ExePath;
        Directory.CreateDirectory(InstallDir);
        var zipPath = Path.Combine(InstallDir, ZipName);

        if (!File.Exists(zipPath) || !HashMatches(zipPath))
        {
            log.WriteLine($"[mediamtx] downloading {DownloadUrl}");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };   // default TLS validation (github.com)
            var tmp = zipPath + ".part";
            await using (var src = await http.GetStreamAsync(DownloadUrl, ct).ConfigureAwait(false))
            await using (var dst = File.Create(tmp))
                await src.CopyToAsync(dst, ct).ConfigureAwait(false);
            File.Move(tmp, zipPath, overwrite: true);
        }

        var actual = Sha256(zipPath);
        if (!string.Equals(actual, ZipSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(zipPath);
            throw new InvalidOperationException($"MediaMTX checksum mismatch: expected {ZipSha256}, got {actual}. Download deleted; not extracted.");
        }
        log.WriteLine($"[mediamtx] sha256 OK ({actual[..16]}...) - extracting");
        ZipFile.ExtractToDirectory(zipPath, InstallDir, overwriteFiles: true);
        File.Delete(zipPath);
        if (!File.Exists(ExePath)) throw new InvalidOperationException("mediamtx.exe not found in the release zip.");
        return ExePath;
    }

    private static bool HashMatches(string path) => string.Equals(Sha256(path), ZipSha256, StringComparison.OrdinalIgnoreCase);

    public static string Sha256(string path)
    {
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
    }
}
