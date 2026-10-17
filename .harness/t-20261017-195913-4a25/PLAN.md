# PLAN — 2D → 3D 전환 빠르게 · 확실하게(t-20261017-195913-4a25)

## 목표
실측 5.7 s(① WebView2 기동 4.36 s · ③ 오버레이 스냅샷 0.92 s)를 줄이고, 3D 가 실제로 그려진 뒤에 펼친다.

## 진행
- [x] 코드 파악: MapViewModel.Map3D.cs · Map3DViewHost.cs · Map3DOverlayBridge.cs · Map3DOverlay/Math/Guide.cs
- [x] ① 미리 데우기 — Map3DViewHost.PrewarmAsync(EnsureCoreWebView2 1회 · 공유 Task), VM 이 ApplicationIdle 에 호출(3D 설정 켜짐일 때만)
- [x] ① 미리 불러오기 — Map3DMath.ShouldPreload(줌 ≥ 15 · 가운데 장면 · 호스트가 다른 장면 · 불러오는 중 아님 · 막힘 아님), EvaluateMap3DGate 에서 호출
- [x] ③ 이미지 디스크 캐시 — Map3DImageCache(%LocalAppData%\Ironwall\Gis\map3d-cache, 키=경로+크기+수정시각, JPEG 85 / 투명이면 PNG, 긴 변 2048)
- [x] ③ 아이콘 메모리 캐시 — 시각 서명 → PNG(다리 인스턴스 = 세션)
- [x] ③ 스냅샷 ready 직후 · 접을 때 Pause(기록 유지) · 펼칠 때 Resume(변경분만, rev 이어서)
- [x] ④ rendered 메시지 기다려 펼치기(1.5 s 넘으면 경고 후 펼침), 재사용은 0.4 s
- [x] ⑤ 전환 시간 로그 한 줄(Map3DGuide.TransitionTimingText)
- [x] ⑥ ParseMessage rendered
- [x] 시험: GMaps.Ui.Tests 1038 통과(새 Map3DTransitionTests) · GMaps.Housing.Tests 354 통과(새 Map3DImageCacheTests) · sln 10개 시험 프로젝트 전부 통과(종료 코드 0)

## 결정 · 발견
- DECISIONS.md 참고.
- GMaps.Ui.Tests · GMaps.Housing.Tests 는 sln 밖 — 직접 dotnet test.
- 기존 시험이 MinEnterShow=0.6 · EncodeImagePng 4096 을 고정 → 그대로 두고 새 값(MinReuseShow 0.4 · CachedImageSide 2048)을 더한다.

## 남은 일
- 실기 전환 시간 측정(코디네이터) — 미검증.
- map-3d 페이지가 rendered 를 보내야 1.5 s 대기 없이 펼친다(페이지 쪽 작업, 이 리포 밖).
