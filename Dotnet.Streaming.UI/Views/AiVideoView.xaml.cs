using Dotnet.Streaming.UI.Darknet;
using Dotnet.Streaming.UI.RawFramesDecoding.DecodedFrames;
using OpenCvSharp.Dnn;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using PixelFormat = Dotnet.Streaming.UI.RawFramesDecoding.PixelFormat;
using Dotnet.Streaming.UI.RawFramesDecoding;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Ironwall.Dotnet.Libraries.Base.Services;
using Caliburn.Micro;


namespace Dotnet.Streaming.UI.Views;
/// <summary>
/// Interaction logic for AiVideoView.xaml
/// </summary>
public partial class AiVideoView : UserControl
{
    private static readonly System.Windows.Media.Color DefaultFillColor = Colors.Black;
    private static readonly TimeSpan ResizeHandleTimeout = TimeSpan.FromMilliseconds(1000);

    private System.Windows.Media.Color _fillColor = DefaultFillColor;
    private WriteableBitmap _writeableBitmap;

    private int _width;
    private int _height;
    private Int32Rect _dirtyRect;
    private TransformParameters _transformParameters;
    private readonly Action<IDecodedVideoFrame> _invalidateAction;
    private readonly ILogService _log;
    private Task _handleSizeChangedTask = Task.CompletedTask;
    private CancellationTokenSource _resizeCancellationTokenSource = new CancellationTokenSource();

    public static readonly DependencyProperty VideoSourceProperty =
        DependencyProperty.Register(nameof(VideoSource),
            typeof(IVideoSource),
            typeof(AiVideoView),
            new PropertyMetadata(OnVideoSourceChanged));


    public static readonly DependencyProperty FillColorProperty = DependencyProperty.Register(nameof(FillColor),
            typeof(System.Windows.Media.Color),
            typeof(AiVideoView),
            new FrameworkPropertyMetadata(DefaultFillColor, OnFillColorPropertyChanged));

    public IVideoSource VideoSource
    {
        get => (IVideoSource)GetValue(VideoSourceProperty);
        set => SetValue(VideoSourceProperty, value);
    }

    public System.Windows.Media.Color FillColor
    {
        get => (System.Windows.Media.Color)GetValue(FillColorProperty);
        set => SetValue(FillColorProperty, value);
    }

    private readonly ObjectDetector yoloDetector;

    // 1) 마지막 YOLO 디텍션 시점 기록
    private DateTime _lastDetection = DateTime.MinValue;
    private DateTime _initialDelayTime = DateTime.Now;

    // 2) 1초에 1번만 디텍션
    private static readonly TimeSpan DetectionInterval = TimeSpan.FromMilliseconds(100);

    // 바운딩 박스 결과를 저장해두는 리스트
    // -> 1초에 한 번 갱신, 그 외에는 이전 값을 그대로 사용
    private readonly List<OpenCvSharp.Rect> _lastBboxes = new();
    private readonly List<string> _lastLabels = new();

    public AiVideoView()
    {
        InitializeComponent();
        _invalidateAction = Invalidate;
        _log = IoC.Get<ILogService>();
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var darknet_dir = System.IO.Path.Combine(baseDir, "darknet_model");
        if (!System.IO.Directory.Exists(darknet_dir))
        {
#if DEBUG
            darknet_dir = darknet_dir.Replace("\\Debug\\", "\\Release\\");
#else
			darknet_dir = darknet_dir.Replace("\\Release\\", "\\Debug\\");
#endif
        }
        yoloDetector = new ObjectDetector(darknet_dir);
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size constraint)
    {
        int newWidth = (int)constraint.Width;
        int newHeight = (int)constraint.Height;

        if (_width != newWidth || _height != newHeight)
        {
            _resizeCancellationTokenSource.Cancel();
            _resizeCancellationTokenSource = new CancellationTokenSource();

            _handleSizeChangedTask = _handleSizeChangedTask.ContinueWith(prev =>
                HandleSizeChangedAsync(newWidth, newHeight, _resizeCancellationTokenSource.Token));
        }

        return base.MeasureOverride(constraint);
    }

    private async Task HandleSizeChangedAsync(int width, int height, CancellationToken token)
    {
        try
        {
            await Task.Delay(ResizeHandleTimeout, token).ConfigureAwait(false);

            Application.Current.Dispatcher.Invoke(() =>
            {
                ReinitializeBitmap(width, height);
            }, DispatcherPriority.Send, token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ReinitializeBitmap(int width, int height)
    {
        _width = width;
        _height = height;
        _dirtyRect = new Int32Rect(0, 0, width, height);

        _transformParameters = new TransformParameters(RectangleF.Empty,
                new System.Drawing.Size(_width, _height),
                ScalingPolicy.Stretch, PixelFormat.Bgra32, ScalingQuality.FastBilinear);

        _writeableBitmap = new WriteableBitmap(
            width,
            height,
            ScreenInfo.DpiX,
            ScreenInfo.DpiY,
            PixelFormats.Pbgra32,
            null);

        RenderOptions.SetBitmapScalingMode(_writeableBitmap, BitmapScalingMode.NearestNeighbor);

        _writeableBitmap.Lock();

        try
        {
            UpdateBackgroundColor(_writeableBitmap.BackBuffer, _writeableBitmap.BackBufferStride);
            _writeableBitmap.AddDirtyRect(_dirtyRect);
        }
        finally
        {
            _writeableBitmap.Unlock();
        }

        AiVideoImage.Source = _writeableBitmap;
    }

    private static void OnVideoSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (AiVideoView)d;
        if (e.OldValue is IVideoSource oldVideoSource)
            oldVideoSource.FrameReceived -= view.OnFrameReceived;
        if (e.NewValue is IVideoSource newVideoSource)
            newVideoSource.FrameReceived += view.OnFrameReceived;
    }

    private void OnFrameReceived(object? sender, IDecodedVideoFrame decodedFrame)
    {
        Application.Current?.Dispatcher.Invoke(_invalidateAction, DispatcherPriority.Send, decodedFrame);
    }

    private void Invalidate(IDecodedVideoFrame decodedVideoFrame)
    {
        try
        {
            int videoWidth = decodedVideoFrame.CurrentFrameParameters.Width;
            int videoHeight = decodedVideoFrame.CurrentFrameParameters.Height;

            if (videoWidth == 0 || videoHeight == 0 || _writeableBitmap == null)
                return;

            _writeableBitmap.Lock();
            decodedVideoFrame.TransformTo(_writeableBitmap.BackBuffer, _writeableBitmap.BackBufferStride, _transformParameters);
            _writeableBitmap.AddDirtyRect(_dirtyRect);
        }
        catch (Exception ex)
        {
            _log.Error($"[AiVideoView] Invalidate Error: {ex}");
        }
        finally
        {
            _writeableBitmap?.Unlock();
        }

        if(DateTime.Now - _initialDelayTime > TimeSpan.FromMilliseconds(3000))
        {
            // YOLO는 별도 Task에서 처리 (렌더와 분리)
            _ = RunYoloDetectionAsync();
        }
    }

    private async Task RunYoloDetectionAsync()
    {
        var now = DateTime.Now;
        if (now - _lastDetection < DetectionInterval)
            return;

        _lastDetection = now;

        byte[]? buffer = null;
        int stride = 0;
        int width = 0;
        int height = 0;

        // 1. UI Thread에서 백버퍼 복사
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (_writeableBitmap == null) return;

            try
            {
                _writeableBitmap.Lock();

                stride = _writeableBitmap.BackBufferStride;
                width = _writeableBitmap.PixelWidth;
                height = _writeableBitmap.PixelHeight;
                int bytes = stride * height;

                buffer = new byte[bytes];
                Marshal.Copy(_writeableBitmap.BackBuffer, buffer, 0, bytes);
            }
            finally
            {
                _writeableBitmap.Unlock();
            }
        });

        // 유효성 검사
        if (buffer == null || buffer.Length == 0 || width == 0 || height == 0)
            return;

        List<OpenCvSharp.Rect> detectedBoxes = new();
        List<string> detectedLabels = new();
        List<float> detectedScores = new();

        // 2. 백그라운드에서 YOLO 추론
        await Task.Run(() =>
        {
            try
            {
                using var matBGRA = Mat.FromPixelData(height, width, MatType.CV_8UC4, buffer!, stride);
                using var matBGR = new Mat();
                Cv2.CvtColor(matBGRA, matBGR, ColorConversionCodes.BGRA2BGR);

                yoloDetector.Clear();
                yoloDetector.Detect(matBGR);

                detectedBoxes.AddRange(yoloDetector.Bboxes);
                detectedLabels.AddRange(yoloDetector.Labels);
                detectedScores.AddRange(yoloDetector.Scores);
            }
            catch (Exception ex)
            {
                _log.Error($"[YOLO] Detection Failed: {ex}");
            }
        });

        // 3. UI Thread에서 바운딩 박스 렌더링
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            OverlayCanvas.Children.Clear();

            for (int i = 0; i < detectedBoxes.Count; i++)
            {
                var bbox = detectedBoxes[i];
                var label = (i < detectedLabels.Count) ? detectedLabels[i] : "";
                var score = (i < detectedScores.Count) ? detectedScores[i] : 0f;

                // 필터링: 유효하지 않은 바운딩 박스는 제외
                if (bbox.Width <= 0 || bbox.Height <= 0)
                    continue;

                if (bbox.X < 0 || bbox.Y < 0 || bbox.X + bbox.Width > _width || bbox.Y + bbox.Height > _height)
                    continue;

                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = bbox.Width,
                    Height = bbox.Height,
                    Stroke = System.Windows.Media.Brushes.Red,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(rect, bbox.X);
                Canvas.SetTop(rect, bbox.Y);
                OverlayCanvas.Children.Add(rect);

                var truncatedScore = score * 100; 
                string display = truncatedScore.ToString("0.0") + "%"; 
                var text = new TextBlock
                {
                    Text = $"{label}({display})",
                    Foreground = System.Windows.Media.Brushes.Yellow,
                    FontWeight = FontWeights.Bold,
                    Background = System.Windows.Media.Brushes.Gray,
                    Opacity = 0.7
                };
                Canvas.SetLeft(text, bbox.X);
                Canvas.SetTop(text, bbox.Y - 20);
                OverlayCanvas.Children.Add(text);
            }
        });
    }

    private static void OnFillColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (AiVideoView)d;
        view._fillColor = (System.Windows.Media.Color)e.NewValue;
    }

    private unsafe void UpdateBackgroundColor(IntPtr backBufferPtr, int backBufferStride)
    {
        byte* pixels = (byte*)backBufferPtr;
        int color = _fillColor.A << 24 | _fillColor.R << 16 | _fillColor.G << 8 | _fillColor.B;

        Debug.Assert(pixels != null, nameof(pixels) + " != null");

        for (int i = 0; i < _height; i++)
        {
            for (int j = 0; j < _width; j++)
                ((int*)pixels)[j] = color;

            pixels += backBufferStride;
        }
    }
}
