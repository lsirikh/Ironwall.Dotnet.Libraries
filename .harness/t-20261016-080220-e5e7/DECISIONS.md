# DECISIONS

## 1. 연결 줄(StreamOpenGate) 자리를 언제 돌려줄까
- options: ① 지금처럼 첫 프레임까지 쥔다 ② 첫 트랙(ES — RTSP DESCRIBE · SETUP 끝)에서 돌려준다 ③ 동시 열기 수(8)를 늘린다
- chosen: ② 첫 ES 에서 반납(동시 수 8 · 우선 줄 · 실패 · 닫기 반납은 그대로)
- why: 줄은 "동시 RTSP 연결 폭주"를 막으려고 만든 것인데, ES 뒤 첫 프레임까지(키프레임 · 버퍼 대기 0.6~1.8초)는 연결과 무관한 대기였다. 실측 60 스트림 줄 대기 5540 → 47 ms, 첫 프레임 1840 → 593 ms, 깨짐 · 끊김 · 버린 그림 0, 60 타일 동시 열기 60/60 · 재시작 0. ③ 은 WaitOnAddress 스핀 대책을 건드려 피했다.
- effect: ES 는 생기지만 프레임이 안 오는 카메라는 열기 제한 시간(기본 10초)까지 자리를 쥐지 않는다 — 실패 정리는 기존 open-timeout 이 한다. 되돌리려면 `LibVlcFrameProducer` 의 `_onEsAdded` 에서 `ReleaseLease()` 한 줄을 빼면 된다.

## 2. 수신 버퍼(network-caching) 기본값과 설정 위치
- options: ① 100ms 등으로 낮춘다 ② 300 유지 + 설정으로 바꿀 수 있게 ③ clock-jitter/clock-synchro=0 추가
- chosen: ② 300 유지, `CameraPopupHostOptions.StreamNetworkCachingMs` → 실행 인자 `--network-caching-ms` 로 넘김(기존 `MaxConcurrentStreamOpens` · `AllowRtspUdpFallback` 와 같은 길)
- why: 100 은 첫 프레임 이득 없이 60 스트림 표시 fps 1374 → 858, clock 옵션은 1~16 스트림 표시 fps 절반. 첫 프레임은 키프레임 대기가 지배한다(카메라 GOP).
- effect: 공개 형식은 속성 · 상수 · 인자를 더하기만 했다. GIS 쪽에서 이 속성을 설정 파일에 잇는 배선은 하지 않았다(필요하면 GIS 의 CameraPopupHostOptions 생성부에서 값을 넣으면 된다). 기본값을 바꾸려면 `HostLaunchArguments.DefaultNetworkCachingMs`.
