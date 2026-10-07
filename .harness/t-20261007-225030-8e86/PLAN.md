# PLAN — ROI 창 영상 띠 제거(기준 해상도 틀 꽉 채우기) (t-20261007-225030-8e86)

## 목표
ROI 설정 창 그리기 칸에서 영상이 기준 해상도 틀(상자)을 꽉 채우게 한다. 지금은 호스트가 오버레이 상자(960 폭)를
"덮는 가장 낮은 해상도" 규칙으로 서브(cam66 720×480 · 3:2)를 고르고 비율 유지(VideoFit)로 맞춰 넣어 양옆 띠가 생긴다.
점은 틀(띠 포함) 기준 0~1 로 저장되므로 띠가 있으면 메인 영상 위치와 어긋난다.
프리셋 관리 창 · 지도 팝업의 영상 비율 동작은 그대로.

## 결정
- D1 방식 = **서브를 상자(기준 비율)로 늘려 그리기**. `VideoProviderInfo.FillBox`(bool, 기본 false) 를 새로 두고
  호스트 `LibVlcFrameProducer.OnFormat` 에서 FillBox 면 디코딩 크기 = 상자 크기(띠 없음).
  - 이유: 메인 · 서브는 같은 시야라 늘리면 위치가 메인과 일치(720×480 D1 은 원래 비정사각 화소). 어떤 프로파일이 골라져도 성립하고,
    메인 강제(1920×1080 디코딩)보다 가볍다. 프로파일 선택 규칙은 손대지 않는다.
- D2 전달: `WithTarget` 사본 · `ResolvingFrameProducer` 의 재생용 사본에 FillBox 를 이어 싣는다. 직렬화는 System.Text.Json(모르는 칸 무시) →
  프로토콜 판 안 올림. 구판 호스트는 무시(예전처럼 띠) — 무해.
- D3 GIS 쪽: `IMappingCameraVideo.OpenVideo` 에 선택 인자 `fillBox=false` 를 더하고 ROI 편집기만 true 로 연다. 프리셋 관리 창은 그대로.

## 진행
- [x] Contracts: VideoProviderInfo.FillBox + WithTarget/WithFillBox(공용 Copy)
- [x] Host: ResolvingFrameProducer 재생 사본에 FillBox · LibVlcFrameProducer.OnFormat · VideoFit.Fill
- [x] Events.Ui: IMappingCameraVideo.OpenVideo(…, fillBox) 기본 구현 · HostMappingCameraVideo · MappingCameraVideoViewModel.FillBox · ROI VM 이 FillBox=true
- [x] 시험(전부 통과): Events.Ui `MappingRoiVideoFillTests` 5 · Host.Tests `VideoFillBoxTests` 8 ·
      CameraPopup.Tests `Survival/LibVlcFillBoxHostTests` 1(실제 호스트 · LibVLC, 3:2 PNG → 16:9 상자) ·
      tools/dummy-cameras `RoiVideoFillFarmTests` 1(1280×720 + 서브 640×480, 비율유지 띠 100% → 꽉 채우기 0%)
- [x] CHANGELOG
- [x] 전체 `dotnet test Ironwall.Dotnet.Libraries.sln` — 10개 어셈블리 모두 통과(실패 0, exit 0)
- [ ] (사람) 실카메라 192.168.202.66 헤디드 확인: 프리셋 관리 → [ROI 설정…] 영상이 틀 양옆 끝까지 차는지(띠 없음).
      호스트 로그 `libvlc format … source→fit 960x540 in box 960x540`. 프리셋 관리 창은 여전히 띠(비율 유지)가 정상.

## 발견
- 가짜 카메라 팜 시험은 MediaMTX 가 worktree 의 `tools/dummy-cameras/bin/mediamtx/` 에 있어야 돈다(없으면 Skip).
  새 worktree 에선 `dotnet run --project tools/dummy-cameras --no-build -- fetch-mediamtx` 로 받는다(bin 은 git 무시).
- 팜 재현: 960×540 상자는 서브 640×480 을 고른다(늘림 1.125 ≤ 1.25) — cam66 720×480 과 같은 상황.
