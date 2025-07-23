using Caliburn.Micro;
using Dotnet.Streaming.UI.Darknet;
using Dotnet.Streaming.UI.RawFramesDecoding.DecodedFrames;
using Dotnet.Streaming.UI.RawFramesDecoding;
using Ironwall.Dotnet.Libraries.Base.Services;
using log4net;
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
using OpenCvSharp;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Diagnostics;
using System.Drawing;

namespace Dotnet.Streaming.UI.Views;
/// <summary>
/// Follow steps 1a or 1b and then 2 to use this custom control in a XAML file.
///
/// Step 1a) Using this custom control in a XAML file that exists in the current project.
/// Add this XmlNamespace attribute to the root element of the markup file where it is 
/// to be used:
///
///     xmlns:MyNamespace="clr-namespace:Dotnet.Streaming.UI.Views"
///
///
/// Step 1b) Using this custom control in a XAML file that exists in a different project.
/// Add this XmlNamespace attribute to the root element of the markup file where it is 
/// to be used:
///
///     xmlns:MyNamespace="clr-namespace:Dotnet.Streaming.UI.Views;assembly=Dotnet.Streaming.UI.Views"
///
/// You will also need to add a project reference from the project where the XAML file lives
/// to this project and Rebuild to avoid compilation errors:
///
///     Right click on the target project in the Solution Explorer and
///     "Add Reference"->"Projects"->[Browse to and select this project]
///
///
/// Step 2)
/// Go ahead and use your control in the XAML file.
///
///     <MyNamespace:AiScreenView/>
///
/// </summary>
[TemplatePart(Name = PART_Image,   Type = typeof(Image))]
[TemplatePart(Name = PART_Overlay, Type = typeof(Canvas))]
public class AiScreenView : Control
{
    private const string PART_Image = "PART_VideoImage";
    private const string PART_Overlay = "PART_Overlay";

    private const int PREPARE_DELAY_MS = 3000;
    private static readonly System.Windows.Media.Color DefaultFillColor = Colors.Black;
    private static readonly TimeSpan DetectionInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ResizeHandleTimeout = TimeSpan.FromMilliseconds(1000);

    static AiScreenView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(AiScreenView), 
            new FrameworkPropertyMetadata(typeof(AiScreenView)));
    }

    public AiScreenView()
    {
        _invalidateAction = Invalidate;
        _log = IoC.Get<ILogService>();

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var darknet_dir = System.IO.Path.Combine(baseDir, "darknet_model");

        // 모델 디렉터리 초기화
        if (!System.IO.Directory.Exists(darknet_dir))
        {
#if DEBUG
            darknet_dir = darknet_dir.Replace("\\Debug\\", "\\Release\\");
#else
            darknet_dir = darknet_dir.Replace("\\Release\\", "\\Debug\\");
#endif
        }
        yoloDetector = new ObjectDetector(darknet_dir);

        _fillColor = DefaultFillColor;
        _initialDelayTime = DateTime.Now;
    }

    #region DependencyProperties
    public static readonly DependencyProperty VideoSourceProperty =
        DependencyProperty.Register(nameof(VideoSource),
            typeof(IVideoSource), typeof(AiScreenView),
            new PropertyMetadata(OnVideoSourceChanged));

    public IVideoSource? VideoSource
    {
        get => (IVideoSource?)GetValue(VideoSourceProperty);
        set => SetValue(VideoSourceProperty, value);
    }

    public static readonly DependencyProperty FillColorProperty =
        DependencyProperty.Register(nameof(FillColor),
            typeof(System.Windows.Media.Color), typeof(AiScreenView),
            new PropertyMetadata(Colors.Black));

    public System.Windows.Media.Color FillColor
    {
        get => (System.Windows.Media.Color)GetValue(FillColorProperty);
        set => SetValue(FillColorProperty, value);
    }
    #endregion

    // 템플릿 파트
    private Image? _image;
    private Canvas? _overlay;

    #region Private state
    private WriteableBitmap? _writeableBitmap;
    private Int32Rect _dirtyRect;
    private TransformParameters _transformParameters;
    private int _width, _height;

    private readonly ObjectDetector yoloDetector;
    private readonly Action<IDecodedVideoFrame> _invalidateAction;
    private readonly ILogService _log;

    private DateTime _lastDetection = DateTime.MinValue;
    private DateTime _initialDelayTime = DateTime.Now;
    private readonly List<OpenCvSharp.Rect> _lastBboxes = new();
    private readonly List<string> _lastLabels = new();
    private System.Windows.Media.Color _fillColor;

    private Task _handleSizeChangedTask = Task.CompletedTask;
    private CancellationTokenSource _resizeCancellationTokenSource = new CancellationTokenSource();
    #endregion





    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _image = GetTemplateChild("PART_VideoImage") as Image;
        _overlay = GetTemplateChild("PART_Overlay") as Canvas;

        if (_writeableBitmap != null && _image != null)
            _image.Source = _writeableBitmap;
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
                ScalingPolicy.Stretch, RawFramesDecoding.PixelFormat.Bgra32, ScalingQuality.FastBilinear);

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

        if (_image != null) _image.Source = _writeableBitmap;
    }

    private static void OnVideoSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (AiScreenView)d;
        if (e.OldValue is IVideoSource oldVideoSource) oldVideoSource.FrameReceived -= view.OnFrameReceived;
        if (e.NewValue is IVideoSource newVideoSource) newVideoSource.FrameReceived += view.OnFrameReceived;
    }

    private void OnFrameReceived(object? sender, IDecodedVideoFrame decodedFrame)
    {
        Application.Current?.Dispatcher.Invoke(_invalidateAction, DispatcherPriority.Send, decodedFrame);
    }

    private void Invalidate(IDecodedVideoFrame frame)
    {
        try
        {
            if (_writeableBitmap is null) return;

            int videoWidth = frame.CurrentFrameParameters.Width;
            int videoHeight = frame.CurrentFrameParameters.Height;

            if (videoWidth == 0 || videoHeight == 0 || _writeableBitmap == null)
                return;

            _writeableBitmap.Lock();
            frame.TransformTo(_writeableBitmap.BackBuffer, _writeableBitmap.BackBufferStride, _transformParameters);
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

        if (DateTime.Now - _initialDelayTime > TimeSpan.FromMilliseconds(PREPARE_DELAY_MS))
        {
            // YOLO는 별도 Task에서 처리 (렌더와 분리)
            _ = DetectAync();
        }
    }

    private async Task DetectAync()
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
            _overlay?.Children.Clear();

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
                _overlay?.Children.Add(rect);

                var truncatedScore = score * 100;
                string display = truncatedScore.ToString("0.0") + "%";
                _log?.Info(display);
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
                _overlay?.Children.Add(text);
            }
        });
    }

    private static void OnFillColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (AiScreenView)d;
        view._fillColor = (System.Windows.Media.Color)e.NewValue;
    }

    private unsafe void UpdateBackgroundColor(IntPtr backBufferPtr, int backBufferStride)
    {
        byte* pixels = (byte*)backBufferPtr;
        int color = _fillColor.A << 24 |
                    _fillColor.R << 16 |
                    _fillColor.G << 8 |
                    _fillColor.B;

        Debug.Assert(pixels != null, nameof(pixels) + " != null");

        for (int i = 0; i < _height; i++)
        {
            for (int j = 0; j < _width; j++)
                ((int*)pixels)[j] = color;

            pixels += backBufferStride;
        }
    }


}
