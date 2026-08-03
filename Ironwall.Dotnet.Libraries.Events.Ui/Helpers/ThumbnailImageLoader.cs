using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
/****************************************************************************
   Purpose      : 원격 썸네일(URL)을 1회 로드→OnLoad 디코드→Freeze 후 Uri-키 정적
                  캐시에 보관. 탭 전환/DataGrid 컨테이너 재활용에도 즉시·안정 렌더
                  (재다운로드 없음). 다운로드는 WPF 네이티브 경로(BitmapImage.UriSource)
                  를 쓴다 — 구 <Image Source="{Binding Uri}"> 와 '동일한 인증서 신뢰
                  경로'라, 그 방식으로 뜨던 이미지는 여기서도 뜬다.
   Created By   : GHLee
   Created On   : 7/31/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 원격 썸네일 URL을 프로세스 전역에서 1회만 로드·디코드하여 Freeze된 <see cref="ImageSource"/>로
/// 캐시하는 로더. 행 VM이 재생성돼도(탭 복귀 시 새 VM 생성) 동일 Uri면 캐시 히트로 즉시 반환되어
/// 재다운로드 없이 안정적으로 다시 그려진다.
/// <para>다운로드는 <see cref="BitmapImage.UriSource"/>(WPF 네이티브, WebRequest/SChannel) 로 수행한다.
/// HttpClient 로 받으면 인증서 신뢰 경로가 달라 자체서명 환경에서 실패해 Default만 남을 수 있어,
/// 구 &lt;Image Source&gt; 와 동일 경로로 통일했다.</para>
/// </summary>
public static class ThumbnailImageLoader
{
    // Uri 문자열 → Freeze된 ImageSource (전역 공유, 행 VM 재생성에도 유지)
    private static readonly ConcurrentDictionary<string, ImageSource> _cache = new();
    // 동일 Uri 중복 로드 방지
    private static readonly ConcurrentDictionary<string, byte> _inflight = new();

    // ★ 진행 중 BitmapImage 강참조 — 로컬 변수만 두면 원격 다운로드 도중 GC가 수거해
    //   DownloadCompleted/Failed가 영영 발화하지 않는다(무음으로 빈 칸 고정). 완료·실패 시 제거.
    //   "여러 행 중 일부만 뜨는" 증상의 원인 — 수거 여부가 GC 타이밍에 좌우돼 산발적으로 보인다.
    private static readonly ConcurrentDictionary<string, BitmapImage> _pending = new();

    // Uri별 실패 횟수 — 실패 시 onLoaded로 바인딩을 재평가시켜 재시도하되, 한도를 둬 무한 루프를 막는다.
    private static readonly ConcurrentDictionary<string, int> _failures = new();
    private const int MAX_RETRY = 2;   // 총 3회 시도

    /// <summary>실패 진단용 로거 — 정적 헬퍼라 IoC lazy 조회(미등록/테스트 시 null). 실패 경로에서만 호출.</summary>
    private static ILogService? Log
    {
        // Caliburn.Micro.Action ↔ System.Action 모호성 회피 위해 using 대신 정규화 호출
        get { try { return Caliburn.Micro.IoC.Get<ILogService>(); } catch { return null; } }
    }

    /// <summary>
    /// 캐시에 있으면 즉시 반환(재다운로드 없음). 없으면 null 반환 + 백그라운드 1회 로드 시작 →
    /// 완료 시 <paramref name="onLoaded"/> 를 UI 스레드에서 호출(바인딩 갱신용).
    /// </summary>
    public static ImageSource? GetOrLoad(Uri uri, Action onLoaded)
    {
        var key = uri.AbsoluteUri;
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        if (_inflight.TryAdd(key, 0))           // 동일 Uri는 1회만 로드
        {
            var app = Application.Current;
            if (app != null)
                _ = app.Dispatcher.BeginInvoke(new Action(() => BeginLoad(uri, key, onLoaded)));
            else
                BeginLoad(uri, key, onLoaded);   // 테스트 등 Application 부재 시
        }

        return null;                            // 로드 중 → 뷰는 뒤의 default 노출, 완료 후 onLoaded 로 갱신
    }

    /// <summary>UI 스레드에서 BitmapImage(UriSource, OnLoad) 로 로드. 원격은 비동기 다운로드(DownloadCompleted).</summary>
    private static void BeginLoad(Uri uri, string key, Action onLoaded)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;             // 완료 시 전체 메모리 로드(지연 재디코드 금지)
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.UriSource = uri;
            bmp.EndInit();

            if (bmp.IsDownloading)
            {
                _pending[key] = bmp;   // ★ GC 수거 방지 — 이벤트 훅 전에 강참조 확보(미보유 시 콜백 미발화)
                bmp.DownloadCompleted += (s, e) => Complete(bmp, key, onLoaded);
                bmp.DownloadFailed   += (s, e) => Fail(key, "download", onLoaded);
                bmp.DecodeFailed     += (s, e) => Fail(key, "decode", onLoaded);
            }
            else
            {
                Complete(bmp, key, onLoaded);   // 로컬/즉시 완료
            }
        }
        catch (Exception ex)
        {
            Fail(key, $"exception: {ex.Message}", onLoaded);
        }
    }

    /// <summary>
    /// 로드 실패 처리 — 사유를 로그로 남기고(무음 실패 금지), 한도 내면 <paramref name="onLoaded"/>로
    /// 바인딩을 재평가시켜 재시도한다. 한도 초과 시 더는 알리지 않아 재시도 루프를 끊는다.
    /// </summary>
    private static void Fail(string key, string reason, Action onLoaded)
    {
        _pending.TryRemove(key, out _);
        _inflight.TryRemove(key, out _);

        var attempt = _failures.AddOrUpdate(key, 1, (_, v) => v + 1);
        if (attempt <= MAX_RETRY)
        {
            Log?.Warning($"[Thumbnail] 로드 실패({reason}) {attempt}/{MAX_RETRY + 1} — 재시도: {key}");
            try { onLoaded(); } catch { /* 바인딩 갱신 실패는 무시 */ }
        }
        else
        {
            Log?.Error($"[Thumbnail] 로드 실패({reason}) — 재시도 한도 초과, 기본 이미지 유지: {key}");
        }
    }

    private static void Complete(BitmapImage bmp, string key, Action onLoaded)
    {
        try { if (bmp.CanFreeze) bmp.Freeze(); } catch { /* Freeze 실패해도 캐시·표시는 진행 */ }
        _cache[key] = bmp;
        _pending.TryRemove(key, out _);          // 강참조 해제 — 캐시가 이제 소유
        _failures.TryRemove(key, out _);         // 재시도 카운터 리셋
        _inflight.TryRemove(key, out _);
        onLoaded();                             // UI 스레드(Dispatcher)에서 호출됨 → NotifyOfPropertyChange
    }
}
