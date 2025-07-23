using OpenCvSharp.Dnn;
using OpenCvSharp;
using System.IO;
using System.Diagnostics;
using Ironwall.Dotnet.Libraries.Base.Services;
using Caliburn.Micro;
using log4net.Repository.Hierarchy;
using static System.Formats.Asn1.AsnWriter;


namespace Dotnet.Streaming.UI.Darknet;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 4/11/2025 12:01:53 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
//public class ObjectDetector
//{
//    public ObjectDetector(String model_dir)
//    {
//        _log = IoC.Get<ILogService>();

//        _log.Info($"OpenCvSharp version: {typeof(Mat).Assembly.GetName().Version}");


//        if (Directory.Exists(model_dir))
//        {
//            string[] fileEntries = Directory.GetFiles(model_dir);
//            foreach (string fileName in fileEntries)
//            {
//                if (Path.GetExtension(fileName) == ".cfg")
//                    cfgFile = fileName;
//                else if (Path.GetExtension(fileName) == ".weights")
//                    modelFile = fileName;
//                else if (Path.GetExtension(fileName) == ".txt")
//                    classNames = File.ReadAllLines(fileName);
//            }
//        }


//        _log.Info($"Cfg file({cfgFile}) was loaded...");
//        _log.Info($"Model file({modelFile}) was loaded...");
//        _log.Info($"ClassNames file({classNames}) was loaded...");

//        if (String.IsNullOrEmpty(cfgFile) || String.IsNullOrEmpty(modelFile) || classNames.Length <= 0) throw new FileNotFoundException();

//        try
//        {
//            net = CvDnn.ReadNetFromDarknet(cfgFile, modelFile);
//            if (net != null)
//            {
//                net.SetPreferableBackend(Backend.CUDA);
//                _log.Info($"Set Preferable Backend : {Backend.CUDA.ToString()}");
//                net.SetPreferableTarget(Target.CUDA);
//                _log.Info($"Set Preferable Target : {Target.CUDA.ToString()}");
//            }
//        }
//        catch (Exception ex)
//        {
//            _log.Error("Failed to init ObjectDetector: " + ex);
//            throw;   // 여기서 throw 해도 상위 로직이 더 읽기 쉬운 로그 확보
//        }
//    }


//    public int Detect(Mat image)
//    {
//        lock (_yoloLock)// 동일 인스턴스에 2‑스레드 진입 금지
//        {
//            try
//            {
//                // 1) 이전 검출 결과 초기화
//                Clear();

//                // 2) 입력 Blob 생성 & 네트워크 입력
//                using var inputBlob = CvDnn.BlobFromImage(
//                    image: image,
//                    scaleFactor: 1f / 255f,
//                    size: new Size(416, 416),
//                    mean: new Scalar(),
//                    swapRB: true,
//                    crop: false);
//                net!.SetInput(inputBlob);

//                // 3) 출력 레이어 이름 가져오기
//                string?[] outNames = net.GetUnconnectedOutLayersNames() ?? Array.Empty<string>();
//                if (outNames == null || outNames.Length == 0)
//                    return 0;

//                // 4) 네이티브→매니지드 안전 오버로드
//                Mat[] outputs = new Mat[outNames.Length];
//                for (int i = 0; i < outputs.Length; i++)
//                    outputs[i] = new Mat();

//                net.Forward(outputs, outNames);   // IList<Mat> + IEnumerable<string>

//                // 5) 결과 파싱
//                foreach (var m in outputs)
//                {
//                    for (int i = 0; i < m.Rows; i++)
//                    {
//                        float objectness = m.At<float>(i, 4);
//                        if (objectness <= 0.3f) continue;

//                        Cv2.MinMaxLoc(m.Row(i).ColRange(5, m.Cols), out _, out _, out _, out Point classPt);

//                        int classId = classPt.X;
//                        float classScore = m.At<float>(i, classId + 5);
//                        //if (classScore <= 0.5f) continue;

//                        if (classNames[classId] == "person" || classNames[classId] == "car")
//                        {
//                            float cx = m.At<float>(i, 0) * image.Width;
//                            float cy = m.At<float>(i, 1) * image.Height;
//                            float w = m.At<float>(i, 2) * image.Width;
//                            float h = m.At<float>(i, 3) * image.Height;

//                            labels.Add(classNames[classId]);
//                            scores.Add(classScore);
//                            bboxes.Add(new Rect(
//                                (int)(cx - w / 2),
//                                (int)(cy - h / 2),
//                                (int)w,
//                                (int)h));
//                        }
//                    }

//                    m.Dispose();   // 꼭 해제!
//                }

//                CvDnn.NMSBoxes(bboxes, scores,
//                           scoreThreshold: 0.5f,   // 이미 0.5로 필터링됨
//                           nmsThreshold: 0.4f,    // IoU 0.4
//                           out int[] keep);

//                // keep 인덱스만 남김
//                labels = keep.Select(i => labels[i]).ToList();
//                scores = keep.Select(i => scores[i]).ToList();
//                bboxes = keep.Select(i => bboxes[i]).ToList();

//                // 6) 결과 개수 반환
//                return bboxes.Count;
//            }
//            catch (Exception ex)
//            {
//                _log.Error($"{ex.ToString()}");
//                return 0;
//            }
//        }
//    }

//    public void Clear()
//    {
//        labels.Clear();
//        scores.Clear();
//        bboxes.Clear();
//    }

//    public List<string> Labels => labels;
//    public List<float> Scores => scores;
//    public List<OpenCvSharp.Rect> Bboxes => bboxes;

//    private readonly string[] classNames = [];

//    private List<string> labels = [];
//    private List<float> scores = [];
//    private List<OpenCvSharp.Rect> bboxes = [];

//    string cfgFile = "";
//    string modelFile = "";
//    private Net? net;
//    private ILogService _log;
//    private readonly object _yoloLock = new();   // 동시 접근 차단용
//}

public sealed class ObjectDetector
{
    /* ─────────────────────────── 생성자 ─────────────────────────── */
    public ObjectDetector(string modelDir)
    {
        _log = IoC.Get<ILogService>();
        _log.Info($"OpenCvSharp  : {typeof(Mat).Assembly.GetName().Version}");
        _log.Info($"Model folder : {modelDir}");

        /* 1. 모델 파일 스캔 ------------------------------------------------ */
        foreach (var f in Directory.GetFiles(modelDir))
        {
            switch (Path.GetExtension(f).ToLower())
            {
                case ".onnx": _onnx = f; break;
                case ".cfg": _cfg = f; break;
                case ".weights": _weights = f; break;
                case ".txt": _names = File.ReadAllLines(f); break;
            }
        }

        if (_names.Length == 0)
            throw new FileNotFoundException("클래스 이름(txt) 파일을 찾을 수 없습니다.");

        /* 2. 네트워크 로드 ------------------------------------------------ */
        if (_onnx != null)                          // ── Ultralytics YOLO (v8~)
        {
            net = CvDnn.ReadNetFromOnnx(_onnx);
            _inputSize = new Size(640, 640);      // export 시 사용한 크기
            _outputParse = ParseYoloV8;
            _log.Info($"[ONNX] {_onnx}");
            _isV8 = true;
        }
        else if (_cfg != null && _weights != null)  // ── Darknet YOLOv7
        {
            

            net = CvDnn.ReadNetFromDarknet(_cfg, _weights);
            _inputSize = new Size(416, 416);
            _outputParse = ParseYoloV7;
            _log.Info($"[Darknet] {_cfg} + {_weights}");
            _isV8 = false;
        }
        else
            throw new FileNotFoundException("ONNX 파일 또는 cfg+weights 를 찾지 못했습니다.");

        /* 3. CUDA 설정 --------------------------------------------------- */
        net.SetPreferableBackend(Backend.CUDA);
        net.SetPreferableTarget(Target.CUDA);
        _log.Info("Backend=CUDA, Target=CUDA");

        //net.SetPreferableBackend(Backend.OPENCV);
        //net.SetPreferableTarget(Target.CPU);
        //_log.Info("Backend=OPENCV, Target=CPU");
    }

    /* ─────────────────────────── 추론 함수 ────────────────────────── */
    public int Detect(Mat image)
    {
        lock (_lock)
        {
            try
            {
                Clear();

                using var blob = CvDnn.BlobFromImage(image, 1 / 255.0, _inputSize, new Scalar(), swapRB: true, crop: false);
                net.SetInput(blob);


                //if (_isV8)
                //{
                //    /* 1-output 이므로 Forward(Mat) */
                //    using var outMat = net.Forward().Reshape(1, 8400); // or .Reshape(1, rows)
                //    ParseYoloV8(outMat, image.Size());
                //}
                //else
                //{
                //    /* Darknet은 여러 layer */
                //    var outNames = net.GetUnconnectedOutLayersNames();
                //    var outs = outNames.Select(_ => new Mat()).ToArray();
                //    net.Forward(outs, outNames);
                //    foreach (var prob in outs)
                //    {
                //        ParseYoloV7(prob, image.Size());
                //        prob.Dispose();
                //    }
                //}

                var outNames = net.GetUnconnectedOutLayersNames();
                var outs = outNames.Select(_ => new Mat()).ToArray();
                net.Forward(outs, outNames);

                foreach (var m in outs) { _outputParse(m, image.Size()); m.Dispose(); }


                /* NMS 후 최종 결과 정리 */
                CvDnn.NMSBoxes(_bboxes, _scores, 0.0f, 0.6f, out int[] keep);
                _labels = keep.Select(i => _labels[i]).ToList();
                _scores = keep.Select(i => _scores[i]).ToList();
                _bboxes = keep.Select(i => _bboxes[i]).ToList();
                return _bboxes.Count;
            }
            catch (Exception ex)
            {
                _log.Error($"{ex.ToString()}");
                return 0;
            }
        }
    }

    /* ──────────────── [A] YOLOv8/v9/v12  ONNX  파서 ──────────────── */
    private void ParseYoloV8(Mat prob, Size img)
    {
        //_log.Info($"YOLOv8 output shape: {prob.Size()}");

        int numProposals = prob.Size(2);
        int numClasses = prob.Size(1) - 4;

        for (int i = 0; i < numProposals; i++)
        {
            float cx = prob.At<float>(0, 0, i) * img.Width / _inputSize.Width;
            float cy = prob.At<float>(0, 1, i) * img.Height / _inputSize.Height;
            float w = prob.At<float>(0, 2, i) * img.Width / _inputSize.Width;
            float h = prob.At<float>(0, 3, i) * img.Height / _inputSize.Height;

            float obj = prob.At<float>(0, 4, i);
            if (obj < 0.5f) continue;

            float maxConf = 0f;
            int classId = 0;
            for (int j = 0; j < numClasses; j++)
            {
                float conf = prob.At<float>(0, 4 + j, i);
                if (conf > maxConf)
                {
                    maxConf = conf;
                    classId = j;
                }
            }

            float score = obj * maxConf;
            if (score < 0.5f) continue;

            string name = _names[classId];
            if (name != "person" && name != "car") continue;

            int x = (int)(cx - w / 2);
            int y = (int)(cy - h / 2);
            int width = (int)w;
            int height = (int)h;

            if (width <= 0 || height <= 0 || x < 0 || y < 0 || x + width > img.Width || y + height > img.Height)
                continue;

            _labels.Add(name);
            _scores.Add(score);
            _bboxes.Add(new Rect(x, y, width, height));
        }
    }

    /* ──────────────── [B] YOLOv7 Darknet 파서 ──────────────── */
    private void ParseYoloV7(Mat prob, Size img)
    {
        // m: (25200, 85)   (3 scales 병합 기준)
        for (int i = 0; i < prob.Rows; i++)
        {
            float obj = prob.At<float>(i, 4);
            if (obj < 0.30f) continue;

            Cv2.MinMaxLoc(prob.Row(i).ColRange(5, prob.Cols),
                          out _, out _, out _, out Point clsPt);
            int cls = clsPt.X;
            float conf = prob.At<float>(i, cls + 5);
            if (conf < 0.60f) continue;

            string name = _names[cls];
            if (name is not ("person" or "car" or "truck" or "bus")) continue;

            float cx = prob.At<float>(i, 0) * img.Width;
            float cy = prob.At<float>(i, 1) * img.Height;
            float w = prob.At<float>(i, 2) * img.Width;
            float h = prob.At<float>(i, 3) * img.Height;

            _labels.Add(name);
            _scores.Add(conf);
            _bboxes.Add(new Rect((int)(cx - w / 2), (int)(cy - h / 2), (int)w, (int)h));
        }
    }

    /* ─────────────────────── 기타 유틸 / 필드 ────────────────────── */
    public IReadOnlyList<string> Labels => _labels;
    public IReadOnlyList<float> Scores => _scores;
    public IReadOnlyList<Rect> Bboxes => _bboxes;
    public void Clear() { _labels.Clear(); _scores.Clear(); _bboxes.Clear(); }

    readonly ILogService _log;
    readonly object _lock = new();

    Net net;
    Size _inputSize;
    Action<Mat, Size> _outputParse;
    bool _isV8;
    string? _onnx, _cfg, _weights;
    string[] _names = Array.Empty<string>();
    readonly string[] _classNames = [];
    List<string> _labels = new();
    List<float> _scores = new();
    List<Rect> _bboxes = new();
}