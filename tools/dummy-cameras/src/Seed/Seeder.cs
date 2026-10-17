using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DummyCameras.Seed;

public sealed class SeedOptions
{
    public Uri Api { get; set; } = new("https://127.0.0.1:8000/api");
    public string? CredentialFile { get; set; }
    public string? LocalCaPem { get; set; } = @"C:\workspace_python\api-test-server\certs\rootCA.pem";
    public string Manifest { get; set; } = Path.Combine(Infra.ToolPaths.ToolRoot, "out", "seed-manifest.json");
    public bool Apply { get; set; }
    public int Count { get; set; } = 8;
    public int PtzCount { get; set; } = 4;
    public int OnvifBasePort { get; set; } = 8180;
    public int RtspPort { get; set; } = 8554;
    public bool Sub { get; set; } = true;
    public string CameraUser { get; set; } = "dummy";
    public string CameraPassword { get; set; } = "dummy1234";
    public int DelayBase { get; set; } = 3;
    public double GotoSeconds { get; set; } = 3;
    public List<int> ExistingSensorIds { get; } = new();
    public string Prefix { get; set; } = "LRT-DUMMY";
    public int NumberBase { get; set; } = 9000;
}

/// <summary>Rows the seed created, persisted after EVERY successful create (a half-finished seed is still cleanable).</summary>
public sealed class SeedManifest
{
    public string RunId { get; set; } = string.Empty;
    public string Api { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public List<SeedItem> Items { get; set; } = new();

    public static SeedManifest? Load(string path)
        => File.Exists(path) ? JsonSerializer.Deserialize<SeedManifest>(File.ReadAllText(path)) : null;

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, path, overwrite: true);
    }
}

/// <param name="Kind">group · controller · sensor · group_member · camera · preset · mapping · mapping_camera</param>
public sealed record SeedItem(string Kind, int Id, int? ParentId, string? Name);

/// <summary>
/// Registers the dummy farm on the loopback test server with the injector account:
/// device group LRT-DUMMY ← (own controller + Fence sensor, or --sensor-id existing sensors as members)
/// · 8 cameras (ONVIF on 127.0.0.1:818N, RTSP main/sub URLs, camera account) · presets P1..P5 on PTZ cameras
/// · event mapping LRT-DUMMY-MAP (SENSOR_WITH_CAMERA, group LRT-DUMMY) → all cameras, PTZ ones with
/// target P2..P5 · home P1 · delay 3..6 s.
/// DRY-RUN unless <see cref="SeedOptions.Apply"/>; dry-run never touches the network (no login - the server evicts
/// an account's other session on login).
/// </summary>
public sealed class Seeder
{
    private readonly SeedOptions _o;
    private readonly ISeedApi _api;
    private readonly TextWriter _out;
    private SeedManifest _manifest = new();

    public Seeder(SeedOptions o, ISeedApi api, TextWriter output)
    {
        _o = o;
        _api = api;
        _out = output;
    }

    public SeedManifest Manifest => _manifest;

    public async Task<int> RunAsync(CancellationToken ct)
    {
        _manifest = new SeedManifest
        {
            RunId = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            Api = _o.Api.ToString(),
            CreatedAt = DateTime.UtcNow.ToString("O"),
        };
        string marker = $"dummy-cameras seed {_manifest.RunId} - loopback dummy; remove with: dummy-cameras cleanup -apply";

        if (!_api.IsDryRun)
        {
            var prior = SeedManifest.Load(_o.Manifest);
            if (prior is { Items.Count: > 0 })
                throw new InvalidOperationException($"a previous seed manifest has {prior.Items.Count} rows ({_o.Manifest}) - run cleanup first");
        }

        await LoginAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_api.IsDryRun)
            {
                var existing = await _api.SendAsync(HttpMethod.Get, $"devices/groups?name={Uri.EscapeDataString(_o.Prefix)}", null, ct).ConfigureAwait(false);
                if (existing.Body?["data"] is JsonArray arr && arr.Any(g => (string?)g?["name"] == _o.Prefix))
                    throw new InvalidOperationException($"device group '{_o.Prefix}' already exists on the server - not ours to reuse; clean it up first");
            }

            // 1) group
            int group = await CreateAsync("group", null, _o.Prefix, "devices/groups",
                new JsonObject { ["name"] = _o.Prefix, ["description"] = marker }, ct).ConfigureAwait(false);

            // 2) sensor side of the mapping
            if (_o.ExistingSensorIds.Count == 0)
            {
                int ctrl = await CreateAsync("controller", null, $"{_o.Prefix}-CTRL", "devices/controllers", new JsonObject
                {
                    ["type_controller"] = "Controller",
                    ["number_device"] = _o.NumberBase + 90,
                    ["name_device"] = $"{_o.Prefix}-CTRL",
                    ["description"] = marker,
                    ["connection"] = new JsonObject { ["type"] = "IP_DIRECT", ["ip_address"] = "127.0.0.1", ["ip_port"] = 9 },
                }, ct).ConfigureAwait(false);
                await CreateAsync("sensor", null, $"{_o.Prefix}-SENSOR", "devices/sensors", new JsonObject
                {
                    ["type_sensor"] = "Fence",
                    ["number_device"] = _o.NumberBase + 91,
                    ["name_device"] = $"{_o.Prefix}-SENSOR",
                    ["description"] = marker,
                    ["controller_id"] = ctrl,
                    ["group_ids"] = new JsonArray(group),
                }, ct).ConfigureAwait(false);
            }
            else
            {
                var ids = new JsonArray(_o.ExistingSensorIds.Select(i => (JsonNode)i).ToArray());
                await Checked(HttpMethod.Post, $"devices/groups/{group}/devices", new JsonObject { ["device_ids"] = ids }, ct).ConfigureAwait(false);
                foreach (var sid in _o.ExistingSensorIds) Record(new SeedItem("group_member", sid, group, null));
            }

            // 3) cameras (+ presets on PTZ)
            var items = new JsonArray();
            for (int i = 1; i <= _o.Count; i++)
            {
                bool ptz = i <= _o.PtzCount;
                int port = _o.OnvifBasePort + i;
                var name = $"{_o.Prefix}-CAM{i}";
                int cam = await CreateAsync("camera", null, name, "devices/cameras", new JsonObject
                {
                    ["type_camera"] = ptz ? "PTZ" : "FIXED",
                    ["number_device"] = _o.NumberBase + i,
                    ["name_device"] = name,
                    ["description"] = marker,
                    ["connection"] = new JsonObject
                    {
                        ["type"] = "IP_DIRECT",
                        ["ip_address"] = "127.0.0.1",
                        ["ip_port"] = port,
                        ["protocol"] = "ONVIF",
                        ["credentials"] = new JsonObject { ["user_name"] = _o.CameraUser, ["user_password"] = _o.CameraPassword },
                        ["urls"] = new JsonObject
                        {
                            ["onvif"] = new JsonObject { ["device_service"] = $"http://127.0.0.1:{port}/onvif/device_service" },
                            ["streams"] = new JsonObject
                            {
                                ["rtsp"] = new JsonObject
                                {
                                    ["main"] = $"rtsp://127.0.0.1:{_o.RtspPort}/cam{i}",
                                    ["sub"] = $"rtsp://127.0.0.1:{_o.RtspPort}/cam{i}{(_o.Sub ? "_sub" : "")}",
                                },
                            },
                        },
                    },
                }, ct).ConfigureAwait(false);

                var item = new JsonObject { ["camera_id"] = cam, ["is_enable"] = true, ["priority"] = i, ["delay_time"] = 0 };
                if (ptz)
                {
                    var presetIds = new Dictionary<int, int>();
                    for (int p = 1; p <= 5; p++)
                    {
                        presetIds[p] = await CreateAsync("preset", cam, $"P{p}", $"devices/cameras/{cam}/presets", new JsonObject
                        {
                            ["preset_index"] = p,
                            ["preset_name"] = $"P{p}",
                            ["touring_time"] = (int)Math.Ceiling(_o.GotoSeconds),
                        }, ct).ConfigureAwait(false);
                    }
                    item["target_preset_id"] = presetIds[2 + (i - 1) % 4];
                    item["home_preset_id"] = presetIds[1];
                    item["delay_time"] = _o.DelayBase + (i - 1);
                }
                items.Add(item);
            }

            // 4) event mapping (sensor group → cameras)
            int mapping = await CreateAsync("mapping", null, $"{_o.Prefix}-MAP", "integrations/event-mappings", new JsonObject
            {
                ["name_event"] = $"{_o.Prefix}-MAP",
                ["device_group_id"] = group,
                ["category_event_mapping"] = "SENSOR_WITH_CAMERA",
                ["description"] = marker,
                ["status"] = true,
            }, ct).ConfigureAwait(false);

            var bulk = await Checked(HttpMethod.Post, $"integrations/event-mappings/{mapping}/cameras/bulk", new JsonObject { ["items"] = items }, ct).ConfigureAwait(false);
            if (bulk.Body?["data"]?["created_ids"] is JsonArray created)
                foreach (var id in created) Record(new SeedItem("mapping_camera", id!.GetValue<int>(), mapping, null));
            if (bulk.Body?["data"]?["failed_items"] is JsonArray { Count: > 0 } failed)
                throw new InvalidOperationException($"mapping bulk: {failed.Count} item(s) failed: {failed.ToJsonString()}");

            _out.WriteLine(_api.IsDryRun
                ? $"[seed] dry-run complete - {_planned} rows would be created. Re-run with -apply to write (injector account)."
                : $"[seed] done - {_manifest.Items.Count} rows recorded in {_o.Manifest}");
            return 0;
        }
        finally
        {
            await LogoutAsync(ct).ConfigureAwait(false);
        }
    }

    private int _planned;

    private async Task LoginAsync(CancellationToken ct)
    {
        string id = "<id from cred file>", pw = "***";
        if (!_api.IsDryRun)
        {
            if (_o.CredentialFile is null) throw new InvalidOperationException("--cred <file> is required for -apply");
            (id, pw) = InjectorCredential.Read(_o.CredentialFile);
        }
        else if (_o.CredentialFile is not null)
        {
            InjectorCredential.Read(_o.CredentialFile);   // validate the file shape only - values are not used or shown
            _out.WriteLine($"  [dry-run] credential file OK (id=/pw= present): {_o.CredentialFile}");
        }
        var r = await _api.SendAsync(HttpMethod.Post, "auth/login", new JsonObject { ["login_id"] = id, ["password"] = pw, ["client_id"] = "dummy-cameras-seed" }, ct).ConfigureAwait(false);
        if (!r.Ok) throw new InvalidOperationException($"login failed: HTTP {r.Status}");
        var token = r.Body?["data"]?["access_token"]?.GetValue<string>();
        if (_api is LiveSeedApi live && token is not null) live.SetToken(token);
    }

    private async Task LogoutAsync(CancellationToken ct)
    {
        try { await _api.SendAsync(HttpMethod.Post, "auth/logout", null, ct).ConfigureAwait(false); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { /* best effort */ }
    }

    private async Task<int> CreateAsync(string kind, int? parent, string name, string path, JsonObject body, CancellationToken ct)
    {
        var r = await Checked(HttpMethod.Post, path, body, ct).ConfigureAwait(false);
        var id = r.DataId ?? throw new InvalidOperationException($"{kind} '{name}': no data.id in response");
        Record(new SeedItem(kind, id, parent, name));
        return id;
    }

    private async Task<ApiResult> Checked(HttpMethod m, string path, JsonNode? body, CancellationToken ct)
    {
        var r = await _api.SendAsync(m, path, body, ct).ConfigureAwait(false);
        if (!r.Ok)
        {
            var msg = r.Body?["error"]?["message"]?.ToString() ?? r.Body?["detail"]?.ToJsonString() ?? string.Empty;
            throw new InvalidOperationException($"{m.Method} {path} -> HTTP {r.Status} {msg}");
        }
        return r;
    }

    private void Record(SeedItem item)
    {
        _planned++;
        if (_api.IsDryRun) return;
        _manifest.Items.Add(item);
        _manifest.Save(_o.Manifest);
    }
}
