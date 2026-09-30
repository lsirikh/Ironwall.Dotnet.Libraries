using System.Text.Json.Nodes;

namespace DummyCameras.Seed;

/// <summary>
/// Removes ONLY the rows listed in the seed manifest, children first (reverse creation order). Before deleting a named
/// row it re-reads it and requires the LRT-DUMMY prefix (a recycled id never takes a real device with it).
/// 404 = already gone. DRY-RUN unless apply. The manifest shrinks as rows are removed.
/// </summary>
public sealed class Cleaner
{
    private readonly SeedOptions _o;
    private readonly ISeedApi _api;
    private readonly TextWriter _out;

    public Cleaner(SeedOptions o, ISeedApi api, TextWriter output)
    {
        _o = o;
        _api = api;
        _out = output;
    }

    /// <summary>DELETE path + the field that must carry the prefix (null = no name to check, e.g. mapping rows).</summary>
    public static (string DeletePath, string? GetPath, string? NameField) Route(SeedItem it) => it.Kind switch
    {
        "mapping_camera" => ($"integrations/event-mappings/{it.ParentId}/cameras/{it.Id}", null, null),
        "mapping" => ($"integrations/event-mappings/{it.Id}", $"integrations/event-mappings/{it.Id}", "name_event"),
        "preset" => ($"devices/cameras/{it.ParentId}/presets/{it.Id}", null, null),
        "camera" => ($"devices/cameras/{it.Id}", $"devices/cameras/{it.Id}", "name_device"),
        "sensor" => ($"devices/sensors/{it.Id}", $"devices/sensors/{it.Id}", "name_device"),
        "controller" => ($"devices/controllers/{it.Id}", $"devices/controllers/{it.Id}", "name_device"),
        "group_member" => ($"devices/groups/{it.ParentId}/devices/{it.Id}", null, null),
        "group" => ($"devices/groups/{it.Id}", $"devices/groups/{it.Id}", "name"),
        _ => throw new InvalidOperationException($"unknown manifest kind {it.Kind}"),
    };

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var manifest = SeedManifest.Load(_o.Manifest);
        if (manifest is null || manifest.Items.Count == 0)
        {
            _out.WriteLine($"[cleanup] nothing to do - no rows in {_o.Manifest}");
            return 0;
        }
        _out.WriteLine($"[cleanup] manifest run {manifest.RunId}: {manifest.Items.Count} rows{(_api.IsDryRun ? " (dry-run)" : "")}");

        if (!_api.IsDryRun)
        {
            if (_o.CredentialFile is null) throw new InvalidOperationException("--cred <file> is required for -apply");
            var (id, pw) = InjectorCredential.Read(_o.CredentialFile);
            var login = await _api.SendAsync(HttpMethod.Post, "auth/login", new JsonObject { ["login_id"] = id, ["password"] = pw, ["client_id"] = "dummy-cameras-seed" }, ct).ConfigureAwait(false);
            if (!login.Ok) throw new InvalidOperationException($"login failed: HTTP {login.Status}");
            if (_api is LiveSeedApi live && login.Body?["data"]?["access_token"]?.GetValue<string>() is { } token) live.SetToken(token);
        }

        int failures = 0;
        try
        {
            foreach (var it in manifest.Items.AsEnumerable().Reverse().ToList())
            {
                var (del, get, field) = Route(it);
                if (get is not null && !_api.IsDryRun)
                {
                    var r = await _api.SendAsync(HttpMethod.Get, get, null, ct).ConfigureAwait(false);
                    if (r.Status == 404) { _out.WriteLine($"  gone     {it.Kind} {it.Id}"); Drop(manifest, it); continue; }
                    var name = r.Body?["data"]?[field!]?.ToString();
                    if (!r.Ok || name is null || !name.StartsWith(_o.Prefix, StringComparison.Ordinal))
                    {
                        _out.WriteLine($"  SKIP     {it.Kind} {it.Id} - name '{name}' lacks prefix {_o.Prefix} (HTTP {r.Status}); left untouched");
                        failures++;
                        continue;
                    }
                }
                var d = await _api.SendAsync(HttpMethod.Delete, del, null, ct).ConfigureAwait(false);
                if (d.Ok || d.Status == 404)
                {
                    _out.WriteLine($"  {(d.Status == 404 ? "gone   " : "deleted")}  {it.Kind} {it.Id}{(it.Name is null ? "" : " " + it.Name)}");
                    Drop(manifest, it);
                }
                else
                {
                    _out.WriteLine($"  FAILED   {it.Kind} {it.Id} - HTTP {d.Status}");
                    failures++;
                }
            }
        }
        finally
        {
            if (!_api.IsDryRun)
            {
                try { await _api.SendAsync(HttpMethod.Post, "auth/logout", null, ct).ConfigureAwait(false); }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { }
            }
        }
        _out.WriteLine(failures == 0 ? "[cleanup] ok" : $"[cleanup] {failures} row(s) not removed - see above");
        return failures == 0 ? 0 : 2;
    }

    private void Drop(SeedManifest m, SeedItem it)
    {
        if (_api.IsDryRun) return;
        m.Items.Remove(it);
        m.Save(_o.Manifest);
    }
}
