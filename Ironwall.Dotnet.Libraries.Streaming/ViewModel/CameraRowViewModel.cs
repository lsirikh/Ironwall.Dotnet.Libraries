using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Streaming.Base.Hub;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.Commands;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Ironwall.Dotnet.Libraries.Streaming.ViewModel{
    /****************************************************************************
       Purpose      :
       Created By   : GHLee
       Created On   : 10/1/2025 1:20:01 PM
       Department   : SW Team
       Company      : Sensorway Co., Ltd.
       Email        : lsirikh@naver.com
    ****************************************************************************/
    /// <summary>
    /// Row 단위 카메라 그룹 ViewModel
    /// RowId를 생성자에서 한 번만 생성하여 고유성 보장
    /// </summary>
    public class CameraRowViewModel : PropertyChangedBase, IDisposable, IAsyncDisposable
    {
        private readonly string _rowId = string.Empty;
        private readonly string _eventId = string.Empty;
        private readonly string? _description;
        private ObservableCollection<CameraViewModel> _cameras = new ObservableCollection<CameraViewModel>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _disposed;

        private readonly ISharedCameraStreamHub? _hub;
        private readonly ConcurrentDictionary<string, ICameraStreamLease> _leases = new();
        private readonly SemaphoreSlim _leaseSemaphore = new(1, 1);
        private string _streamStatus = "Ready";
        private bool _isStreamError;
        private ICommand? _closeRowCommand;

        /// <summary>Row X 버튼 클릭 시 발화. sender=this, args=RowId.</summary>
        public event EventHandler<string>? CloseRequested;

        public CameraRowViewModel()
        {
            _rowId = Guid.NewGuid().ToString();
        }

        public CameraRowViewModel(string eventId, string? description) : this()
        {
            _eventId = eventId;
            _description = description;
        }

        public CameraRowViewModel(string eventId, string? description, ISharedCameraStreamHub hub) : this()
        {
            _eventId = eventId;
            _description = description;
            _hub = hub;
        }

        public string RowId => _rowId;
        public string EventId => _eventId;
        public string? Description => _description;

        public string StreamStatus => _streamStatus;
        public bool IsStreamError => _isStreamError;

        /// <summary>대기 큐에서 제거됐거나 이미 종료된 Row 여부</summary>
        public bool IsCancelled => _cts.IsCancellationRequested;

        /// <summary>연결 취소 토큰 — ConnectAsync에 전달</summary>
        public CancellationToken CancellationToken => _cts.Token;

        public ObservableCollection<CameraViewModel> Cameras
        {
            get => _cameras;
            set => Set(ref _cameras, value);
        }

        /// <summary>Hub Lease의 relay URL (첫 번째 lease 기준). 단일 카메라 Row 또는 fallback에 사용.</summary>
        public string? CurrentLeaseRelayUrl => _leases.Values.FirstOrDefault()?.RelayUrl;

        /// <summary>특정 카메라 ID의 relay URL. 다중 카메라 Row에서 ImprovedRtspPlayer가 사용.</summary>
        public string? GetLeaseRelayUrl(string cameraId) =>
            _leases.TryGetValue(cameraId, out var lease) ? lease.RelayUrl : null;

        /// <summary>
        /// 특정 카메라 ID의 공유 BitmapSource.
        /// VLC-backed Hub Entry에서만 non-null (프로덕션 경로).
        /// </summary>
        public BitmapSource? GetLeaseFrame(string cameraId) =>
            _leases.TryGetValue(cameraId, out var lease) ? lease.Frame : null;

        /// <summary>Hub가 주입됐는지 여부. ImprovedRtspPlayer가 연결 경로 분기에 사용.</summary>
        public bool HasHub => _hub is not null;

        /// <summary>Row 개별 종료 커맨드. X 버튼에 바인딩. 백킹 필드로 단일 인스턴스 보장.</summary>
        public ICommand CloseRowCommand => _closeRowCommand ??= new RelayCommand(() =>
        {
            if (_disposed || _cts.IsCancellationRequested) return;
            CloseRequested?.Invoke(this, RowId);
        });

        /// <summary>Row를 취소 상태로 마킹. 중복 호출 안전.</summary>
        public void Cancel()
        {
            if (_disposed || _cts.IsCancellationRequested) return;
            _cts.Cancel();
        }

        public async Task StartStreamAsync(RtspConnectionInfo info, CancellationToken ct = default)
        {
            if (_hub == null) throw new InvalidOperationException("Hub이 주입되지 않았습니다.");

            string cameraId = info.GetCameraKey();
            if (_leases.ContainsKey(cameraId)) return;

            await _leaseSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_leases.ContainsKey(cameraId)) return;

                var req = new LeaseRequest(cameraId, _eventId, _rowId, nameof(CameraRowViewModel));
                var lease = await _hub.AcquireAsync(req, info, ct).ConfigureAwait(false);
                lease.HealthChanged += OnHealthChanged;
                _leases[cameraId] = lease;
            }
            finally
            {
                _leaseSemaphore.Release();
            }
        }

        private void OnHealthChanged(object? sender, StreamHealthEventArgs e)
        {
            _streamStatus = e.Health.ToString();
            _isStreamError = e.Health == StreamHealth.Failed;
            // UI 스레드에서 PropertyChanged 발화 — 스레드 풀 콜백에서 직접 호출되는 경우 대비
            Execute.OnUIThread(() =>
            {
                NotifyOfPropertyChange(nameof(StreamStatus));
                NotifyOfPropertyChange(nameof(IsStreamError));
            });
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            CloseRequested = null;

            _cts.Cancel();
            _cts.Dispose();
            _leaseSemaphore.Dispose();

            foreach (var (_, lease) in _leases)
            {
                lease.HealthChanged -= OnHealthChanged;
                await lease.DisposeAsync().ConfigureAwait(false);
            }
            _leases.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CloseRequested = null;

            _cts.Cancel();
            _cts.Dispose();
            _leaseSemaphore.Dispose();

            foreach (var (_, lease) in _leases)
            {
                lease.HealthChanged -= OnHealthChanged;
                _ = lease.DisposeAsync().AsTask();
            }
            _leases.Clear();
        }
    }
}