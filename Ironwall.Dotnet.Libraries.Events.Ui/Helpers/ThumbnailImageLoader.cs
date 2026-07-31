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
                bmp.DownloadCompleted += (s, e) => Complete(bmp, key, onLoaded);
                bmp.DownloadFailed   += (s, e) => _inflight.TryRemove(key, out _);   // 실패 → 캐시 안 함, 재시도 허용
                bmp.DecodeFailed     += (s, e) => _inflight.TryRemove(key, out _);
            }
            else
            {
                Complete(bmp, key, onLoaded);   // 로컬/즉시 완료
            }
        }
        catch
        {
            _inflight.TryRemove(key, out _);    // 예외 → 뷰는 default 유지, 다음 바인딩서 재시도
        }
    }

    private static void Complete(BitmapImage bmp, string key, Action onLoaded)
    {
        try { if (bmp.CanFreeze) bmp.Freeze(); } catch { /* Freeze 실패해도 캐시·표시는 진행 */ }
        _cache[key] = bmp;
        _inflight.TryRemove(key, out _);
        onLoaded();                             // UI 스레드(Dispatcher)에서 호출됨 → NotifyOfPropertyChange
    }
}
