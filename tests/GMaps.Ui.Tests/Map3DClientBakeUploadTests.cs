using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 굽기 시작(POST /api/jobs) 시간 제한 — 본문에 오버레이 이미지(수십 MB)가 실려 올리기만 10 초를 넘을 수 있다.
/// 고치기 전에는 HttpClient 전체 10 초 제한이 이 요청까지 덮어 "서버 닿지 않음"이 됐는데 서버는 이미 굽기를 시작해, 다시 누르면 같은 영역을 두 번 구웠다.
/// 기본값 그대로(제품과 같은 Map3DClient) 시험한다 — 10.5 초 걸린다.
/// </summary>
public class Map3DClientBakeUploadTests
{
    private sealed class SlowUploadHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(10.5), ct);   // 큰 본문을 올리는 데 걸리는 시간(보통 요청 제한 10 초보다 길다)
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("{\"id\":\"j1\",\"status\":\"queued\",\"progress\":0}", Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    public async Task should_get_job_when_bake_start_upload_takes_longer_than_ten_seconds()
    {
        using var c = new Map3DClient("http://127.0.0.1:8765", new SlowUploadHandler());

        var job = await c.StartBakeAsync(new[] { 127.0, 37.4, 127.01, 37.41 });   // ★ 고치기 전: Map3DServerUnavailableException("응답 시간 초과")

        Assert.Equal("j1", job.Id);
    }
}
