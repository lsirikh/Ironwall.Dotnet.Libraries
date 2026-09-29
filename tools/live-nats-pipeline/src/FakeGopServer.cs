using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LiveNatsPipeline;

public sealed record DevSpec(int Id, string Category, string TypeValue, int Number, string Name, int[] Groups, int? ControllerId = null);

public sealed record FakeRequest(DateTime At, string Method, string Path, int Status, bool BlockedWrite);

/// <summary>
/// In-process stand-in for the GOP REST server. It is installed as the OUTERMOST handler of the library's own
/// ApiService and NEVER calls base.SendAsync - no byte leaves the process. Device bodies are the test server's
/// own v7 contract fixtures (fixtures/v7_examples_e.json) with id/type/groups substituted, so the library's
/// real DTO mapping (DeviceApiService -> ToSensorDeviceModel ...) is what runs.
/// Every write is refused (403) and recorded - a write attempt is itself a finding.
/// </summary>
public sealed class FakeGopServer : DelegatingHandler
{
    public const string BaseUrl = "https://127.0.0.1:9/api";   // discard port - nothing listens there

    readonly JObject _fixture;
    readonly ConcurrentDictionary<int, JObject> _devices = new();
    readonly ConcurrentDictionary<int, JObject> _detections = new();
    public readonly ConcurrentQueue<FakeRequest> Requests = new();

    public FakeGopServer(string fixturePath)
    {
        _fixture = JObject.Parse(File.ReadAllText(fixturePath, Encoding.UTF8));
    }

    static string TypeKey(string category) => category switch
    {
        "controller" => "type_controller",
        "sensor" => "type_sensor",
        "camera" => "type_camera",
        "speaker" => "type_speaker",
        "enclosure" => "type_enclosure",
        "lamp" => "type_lamp",
        "gate" => "type_gate",
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    public void Put(DevSpec d, string status = "ACTIVATED")
    {
        var template = (JObject)_fixture["full"]![d.Category + "s"]!["data"]!.DeepClone();
        template["id"] = d.Id;
        template["number_device"] = d.Number;
        template["name_device"] = d.Name;
        template[TypeKey(d.Category)] = d.TypeValue;
        template["status"] = status;
        template["group_ids"] = new JArray(d.Groups);
        if (d.Category == "sensor" && d.ControllerId is int c) template["controller_id"] = c;
        if (d.Category == "gate" && d.ControllerId is int gc && template["connection"] is JObject conn) conn["parent_device_id"] = gc;
        _devices[d.Id] = template;
    }

    public void SetStatus(int id, string status) { if (_devices.TryGetValue(id, out var d)) d["status"] = status; }
    public void SetGroups(int id, params int[] groups) { if (_devices.TryGetValue(id, out var d)) d["group_ids"] = new JArray(groups); }
    public bool Remove(int id) => _devices.TryRemove(id, out _);
    public void PutDetection(JObject body) => _detections[body.Value<int>("id")] = body;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.TrimEnd('/');
        Safety.AssertLoopback(request.RequestUri.ToString());
        if (request.Method != HttpMethod.Get)
        {
            Requests.Enqueue(new FakeRequest(DateTime.Now, request.Method.Method, path, 403, true));
            return Task.FromResult(Json(HttpStatusCode.Forbidden, new JObject
            {
                ["success"] = false,
                ["message"] = "live-nats-pipeline: writes are refused (read-only probe)",
                ["error"] = new JObject { ["code"] = "PROBE_WRITE_BLOCKED", ["message"] = "write refused by probe" },
            }));
        }

        var (status, body) = Route(path);
        Requests.Enqueue(new FakeRequest(DateTime.Now, "GET", path + request.RequestUri.Query, (int)status, false));
        return Task.FromResult(Json(status, body));
    }

    (HttpStatusCode, JObject) Route(string path)
    {
        var seg = path.Split('/', StringSplitOptions.RemoveEmptyEntries);   // api, devices, sensors, 123
        if (seg.Length >= 3 && seg[0] == "api" && seg[1] == "devices" && seg[2] == "groups")
        {
            var groups = _devices.Values.SelectMany(d => d["group_ids"]!.Values<int>()).Distinct().OrderBy(g => g).ToList();
            if (seg.Length == 4 && int.TryParse(seg[3], out var gid))
                return groups.Contains(gid) ? (HttpStatusCode.OK, Ok(GroupJson(gid))) : NotFound();
            return (HttpStatusCode.OK, Ok(new JArray(groups.Select(GroupJson)), groups.Count));
        }
        if (seg.Length >= 3 && seg[0] == "api" && seg[1] == "devices")
        {
            var category = seg[2].EndsWith("s") ? seg[2][..^1] : seg[2];
            if (seg.Length == 4 && int.TryParse(seg[3], out var id))
                return _devices.TryGetValue(id, out var d) && d.Value<string>("category_device") == category
                    ? (HttpStatusCode.OK, Ok(d.DeepClone()))
                    : NotFound();
            var list = _devices.Values.Where(d => d.Value<string>("category_device") == category).Select(d => d.DeepClone()).ToList();
            return (HttpStatusCode.OK, Ok(new JArray(list), list.Count));
        }
        if (seg.Length == 4 && seg[0] == "api" && seg[1] == "events" && seg[2] == "detections" && int.TryParse(seg[3], out var eid))
            return _detections.TryGetValue(eid, out var det) ? (HttpStatusCode.OK, Ok(det.DeepClone())) : NotFound();
        if (seg.Length >= 2 && seg[0] == "api" && seg[1] == "servers")
            return (HttpStatusCode.OK, Ok(new JArray(), 0));
        return NotFound();
    }

    static JObject GroupJson(int gid) => new()
    {
        ["id"] = gid, ["name"] = $"PROBE-GROUP-{gid}", ["description"] = "live-nats-pipeline", ["device_count"] = 0,
        ["created_at"] = "2026-09-30T00:00:00.000000+09:00", ["updated_at"] = "2026-09-30T00:00:00.000000+09:00",
    };

    static JObject Ok(JToken data, int? total = null)
    {
        var o = new JObject { ["success"] = true, ["message"] = "ok (probe)", ["data"] = data };
        o["meta"] = total is int t
            ? new JObject { ["total"] = t, ["page"] = 1, ["limit"] = 100, ["total_pages"] = 1 }
            : new JObject { ["view"] = "full" };
        return o;
    }

    static (HttpStatusCode, JObject) NotFound() => (HttpStatusCode.NotFound, new JObject
    {
        ["success"] = false, ["message"] = "Not found (probe)",
        ["error"] = new JObject { ["code"] = "NOT_FOUND", ["message"] = "Not found (probe)" },
    });

    static HttpResponseMessage Json(HttpStatusCode status, JObject body) => new(status)
    {
        Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json"),
    };
}
