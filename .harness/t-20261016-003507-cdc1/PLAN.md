# PLAN — RTSP 스트리밍 단계별 계측 · 최적화 (t-20261016-003507-cdc1)

## 목표
(사용자 2026-10-16) RTSP → RTP → H264 NAL → Decode → Dispatcher → 렌더 단계마다 로그 플래그를 세워
가장 부하 · 지연이 큰 단계를 찾고 거기서부터 최적화한다. 전/후 수치를 표로 남긴다.

## 1. 파이프라인 확정 (완료)
- GIS(Dotnet.Monitoring.Solution)는 옛 `StreamingModule`(`ImprovedRtspStreamingService` · `ImprovedRtspPlayer`)을 **등록하지 않는다**
  (`Bootstrapper.cs:614`, `Services/CameraPopupRegistration.cs:19`). 옛 자체 경로는 GIS 에서 쓰이지 않는다 → 계측 대상 아님.
- 실제 영상은 전부 별도 프로세스 `Ironwall.CameraPopupHost.exe` 의 LibVLC 경로:
  - 이벤트 창 타일: `EventWindowManager.StartStream` → `BitmapFrameSink`(UI 디스패처로 넘겨 `WriteableBitmap.WritePixels`) ← `GatedFrameSink` ← `LibVlcFrameProducer`
  - 지도 오버레이(카메라 팝업): `OverlayStreamManager` → `SharedMemoryFrameSink`(공유 메모리, GIS 가 읽어 그림) ← `LibVlcFrameProducer`
- LibVLC 내부(RTSP · RTP · NAL · 디코드)는 우리 코드가 아님 → 다음으로 나눈다:
  - RTSP: `Play()` → LibVLC `Playing` 이벤트(연결 + DESCRIBE/SETUP/PLAY 왕복) · 줄 대기(StreamOpenGate) 시간
  - RTP: `media.Statistics` 의 입력 바이트/비트레이트(ReadBytes · InputBitrate)
  - NAL(디먹스 · 프레임 조립): DemuxReadBytes · DemuxCorrupted · DemuxDiscontinuity
  - Decode: DecodedVideo(fps) · lock→display 간격(디코드+변환+표시 대기)
  - 표시: DisplayedPictures · LostPictures(버린 그림) · display 콜백 안 복사 시간
  - Dispatcher: `BitmapFrameSink.Write`→`Flush` 대기(ms) · 덮어쓴(합쳐진) 프레임 수
  - 렌더: `WritePixels` 시간 · 공유 메모리 쓰기 시간
  - CPU: 호스트 프로세스 CPU %(전 코어 대비)

## 2. 측정 플래그 (결정)
- 켜는 법 ① 환경변수 `IRONWALL_STREAM_TRACE=1`(PerfTrace 관례: 1/true/yes/on) — 감시자가 띄우는 호스트는 GIS 환경변수를 물려받는다.
  ② appsettings.json(호스트 폴더 또는 그 위 GIS 폴더)의 `"CameraHostStreamTrace": true`(루트 또는 `"AppSettings"` 안).
  키를 `CameraPopup` 덩어리 안에 두지 않은 이유: GIS 설정 저장이 그 덩어리를 레코드로 다시 써 모르는 키를 지운다.
- 기록: 전용 파일 `{호스트 로그 폴더}\camera-stream-trace-yyyyMMdd.log`(줄마다 즉시 flush). 5초마다 스트림별 한 줄 + 합계 한 줄.
- 기본 꺼짐: 꺼져 있으면 정적 bool 읽기 한 번으로 끝난다.

## 진행
- [x] 파이프라인 확정
- [x] 계측 코드(호스트 `Diagnostics/StreamTrace.cs` + `LibVlcFrameProducer` · `BitmapFrameSink` 훅, `Program.cs` 에서 Start)
- [x] 단위 시험 `CameraPopup.Host.Tests/StreamTraceSettingsTests.cs`(플래그 해석 · 기본 꺼짐)
- [x] 측정 시험 `tools/dummy-cameras/tests/StreamTraceLoadTests.cs`(opt-in `IRONWALL_STREAM_TRACE_LOAD=1`) — 1·4·8·16·60 스트림, 960x540 · 12fps, 헤드리스
  - 실행: `dotnet test tools/dummy-cameras/tests/DummyCameras.Tests.csproj --settings .harness/t-20261016-003507-cdc1/trace-before.runsettings --filter "FullyQualifiedName~StreamTraceLoadTests"`
    (셸 환경변수 설정이 거부돼 runsettings 의 EnvironmentVariables 로 넘긴다. 호스트는 Release 빌드 exe 를 IRONWALL_CAMHOST_EXE 로 지정)
- [ ] 측정 결과 표 · 병목 판정
- [ ] 최적화 + 전/후 표
- [ ] 시험: sln + CameraPopup.Tests · CameraPopup.Host.Tests (RealCamera 제외)

## 발견 · 메모
- LibVLC `Playing` 이벤트는 Play 직후 곧바로 온다(시범 17ms) → RTSP 협상 지점으로 못 씀. 첫 `ESAdded`(SDP 받고 SETUP 해 트랙 생김)로 잰다.
- RTSP(TCP 인터리브) 입력에서 `Statistics.ReadBytes`(입력 바이트)가 0 으로 나옴 → RTP 단계 처리량은 디먹스 바이트(`DemuxReadBytes`)로 본다.
- 헤드리스 호스트는 LibVLC 미리 올리기(prewarm)를 건너뛴다 → 첫 스트림의 첫 프레임 시간에 코어 초기화가 섞인다.
- 헤드리스에는 WPF 합성(화면 그리기)이 없다 — "렌더"는 `WritePixels`(비트맵 갱신)까지만 잰다.
- 하네스 셸 제약: ffmpeg · Get-Command · bash 변수 확장 명령은 자동 거부됨. dotnet 은 허용.
- MediaMTX 는 원본 체크아웃 `tools/dummy-cameras/bin/mediamtx/v1.21.1` 에만 있음(워크트리 bin 없음, gitignore).
