using OpenCvSharp.Dnn;
using OpenCvSharp;
using System.IO;
using System.Diagnostics;
using Ironwall.Dotnet.Libraries.Base.Services;
using Caliburn.Micro;


namespace Dotnet.Streaming.UI.Darknet;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 4/11/2025 12:01:53 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ObjectDetector
{
    public ObjectDetector(String model_dir)
    {
        _log = IoC.Get<ILogService>();

        _log.Info($"OpenCvSharp version: {typeof(Mat).Assembly.GetName().Version}");


        if (Directory.Exists(model_dir))
        {
            string[] fileEntries = Directory.GetFiles(model_dir);
            foreach (string fileName in fileEntries)
            {
                if (Path.GetExtension(fileName) == ".cfg")
                    cfgFile = fileName;
                else if (Path.GetExtension(fileName) == ".weights")
                    modelFile = fileName;
                else if (Path.GetExtension(fileName) == ".txt")
                    classNames = File.ReadAllLines(fileName);
            }
        }


        _log.Info($"Cfg file({cfgFile}) was loaded...");
        _log.Info($"Model file({modelFile}) was loaded...");
        _log.Info($"ClassNames file({classNames}) was loaded...");

        if (String.IsNullOrEmpty(cfgFile) || String.IsNullOrEmpty(modelFile) || classNames.Length <= 0) throw new FileNotFoundException();

        net = Net.ReadNetFromDarknet(cfgFile, modelFile);
        if (net == null) throw new FileNotFoundException();


        net.SetPreferableBackend(Backend.CUDA);
        _log.Info($"Set Preferable Backend : {Backend.CUDA.ToString()}");
        net.SetPreferableTarget(Target.CUDA);
        _log.Info($"Set Preferable Target : {Target.CUDA.ToString()}");
    }


    public int Detect(Mat image)
    {
        lock (_yoloLock)// 동일 인스턴스에 2‑스레드 진입 금지
        {
            try
            {
                // 1) 이전 검출 결과 초기화
                Clear();

                // 2) 입력 Blob 생성 & 네트워크 입력
                using var inputBlob = CvDnn.BlobFromImage(
                    image: image,
                    scaleFactor: 1f / 255f,
                    size: new Size(416, 416),
                    mean: new Scalar(),
                    swapRB: true,
                    crop: false);
                net!.SetInput(inputBlob);

                // 3) 출력 레이어 이름 가져오기
                string?[] outNames = net.GetUnconnectedOutLayersNames() ?? Array.Empty<string>();
                if (outNames == null || outNames.Length == 0)
                    return 0;

                // 4) 네이티브→매니지드 안전 오버로드
                Mat[] outputs = new Mat[outNames.Length];
                for (int i = 0; i < outputs.Length; i++)
                    outputs[i] = new Mat();

                net.Forward(outputs, outNames);   // IList<Mat> + IEnumerable<string>

                // 5) 결과 파싱
                foreach (var prob in outputs)
                {
                    for (int i = 0; i < prob.Rows; i++)
                    {
                        float objectness = prob.At<float>(i, 4);
                        if (objectness <= 0.3f) continue;

                        Cv2.MinMaxLoc(prob.Row(i).ColRange(5, prob.Cols), out _, out _, out _, out Point classPt);

                        int classId = classPt.X;
                        float classScore = prob.At<float>(i, classId + 5);
                        //if (classScore <= 0.5f) continue;

                        if (classNames[classId] == "person" || classNames[classId] == "car")
                        {
                            float cx = prob.At<float>(i, 0) * image.Width;
                            float cy = prob.At<float>(i, 1) * image.Height;
                            float w = prob.At<float>(i, 2) * image.Width;
                            float h = prob.At<float>(i, 3) * image.Height;

                            labels.Add(classNames[classId]);
                            scores.Add(classScore);
                            bboxes.Add(new Rect(
                                (int)(cx - w / 2),
                                (int)(cy - h / 2),
                                (int)w,
                                (int)h));
                        }
                    }

                    prob.Dispose();   // 꼭 해제!
                }

                CvDnn.NMSBoxes(bboxes, scores,
                           scoreThreshold: 0.5f,   // 이미 0.5로 필터링됨
                           nmsThreshold: 0.4f,    // IoU 0.4
                           out int[] keep);

                // keep 인덱스만 남김
                labels = keep.Select(i => labels[i]).ToList();
                scores = keep.Select(i => scores[i]).ToList();
                bboxes = keep.Select(i => bboxes[i]).ToList();

                // 6) 결과 개수 반환
                return bboxes.Count;
            }
            catch (Exception ex)
            {
                _log.Error($"{ex.ToString()}");
                return 0;
            }
        }
    }
    
    public void Clear()
    {
        labels.Clear();
        scores.Clear();
        bboxes.Clear();
    }

    public List<string> Labels => labels;
    public List<float> Scores => scores;
    public List<OpenCvSharp.Rect> Bboxes => bboxes;

    private readonly string[] classNames = [];

    private List<string> labels = [];
    private List<float> scores = [];
    private List<OpenCvSharp.Rect> bboxes = [];

    String cfgFile = "";
    String modelFile = "";
    private Net? net;
    private ILogService _log;
    private readonly object _yoloLock = new();   // 동시 접근 차단용
}