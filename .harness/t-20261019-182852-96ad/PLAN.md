# 3D 만들기 — 굽는 중 취소 — PLAN

## 목표
굽기(map-3d 작업) 진행 중 「만들기 취소」 → 서버가 1~2초 안에 멈추고 상태 "cancelled", 만들다 만 폴더 정리, 이전 장면 보존.
GIS 는 진행 판 · 영역 배지 우클릭 · 3D 영역 목록(다시 만드는 영역 항목)에서 취소, 옛 서버면 안내.

## 정한 것(자세히는 DECISIONS.md)
- map-3d 작업 복사본 = `artifacts/map3d-work`(git clone, git 무시 폴더). 결과는 `.harness/t-20261019-182852-96ad/map3d.patch`.
- 취소 = 작업 스레드마다 threading.Event(map3d/cancel.py), 긴 반복 · 진행 콜백에서 check() → Cancelled(BaseException — 넓은 except 에 안 먹힘).
  네트워크 대기(OSM · DEM)는 별도 스레드로 돌리고 0.2 s 마다 확인.
- GLB 쓰기(0.85) 뒤에는 취소 확인 안 함(파일 반쯤 쓴 장면 방지).
- 다시 만들기 = 늘 새 작업 폴더 → 옛 장면은 성공 전까지(그리고 그 뒤에도) 그대로. 취소하면 새 폴더만 지운다.
- POST /api/jobs/{id}/cancel: 없는 작업도 200 {"status":"missing"} → GIS 는 404/405 를 "옛 서버(취소 모름)"로 판정.

## 진행
- [x] map-3d: cancel.py · jobs(cancel · 상태 cancelled · 정리 · too_late) · server(POST /cancel · 목록 cancelled) · 확인 지점(진행 콜백 · OSM/DEM/LLM 대기 · SAM 타일 · SAM 점 격자 · LaMa 창 · 정사영상 줄 · 건물 메시 · 사진 텍스처 면) · README
- [x] map-3d 시험 tests/test_cancel.py 18개 → 전체 pytest 228 통과(기준 210 + 18, node 시험 포함)
- [x] map3d.patch(원본에 `git apply --check` 통과)
- [x] GIS: Map3DClient(CancelJobAsync · Map3DCancelResult · Map3DJob.IsCancelled) · VM(CancelMap3DBakeCommand · IsMap3DCancelBakeVisible · EndMap3DBake · 굽기별 CTS) · Map3DAreaItem(cancelBake 생성자 · IsBaking) · Map3DAreaAction.AutomationId · XAML(진행 판 단추 GMaps.Map3D.CancelBake + "?") · MapsHelp(Map.Map3D.CancelBake) · Map3DGuide(문구 · 보임 판정)
- [x] GIS 시험: MapEdit.Tests/Map3DCancelBakeScenarioTests(12) · Ui.Tests/Map3DCancelBakeTests · PropertyPanel MapsHelpTests 2행. 수정 전 실패 증명 proof-before.txt
- [x] 완료 판정: sln 통과(sln.txt) · GMaps.Ui.Tests 1567 · GMaps.MapEdit.Tests 463 통과(after-gmaps.txt), 새 시험 반복 3회 흔들림 없음

## 기준선(작업 전)
GMaps.Ui.Tests 1524 · GMaps.MapEdit.Tests 451 통과. map-3d pytest 210 통과.

## 발견
- 옛 GIS 는 작업 상태 "cancelled" 를 몰라(done · error 만 끝) 진행 조회를 끝없이 되풀이했다 — 시험이 잡음(proof-before.txt).
- 원본 map-3d 는 POST /api/jobs/{id}/cancel → 404 {"detail":"Not Found"} — GIS 옛 서버 판정 근거.
- VM 시나리오에서 다 만든 뒤 줌 17 이면 게이트가 3D 화면(WebView2)을 미리 연다 → 시험 지도 줌 12.
- GMaps.Ui.Tests 는 소스 링크 방식이라 Map3DAreaItem(RelayCommand) 은 없다 → 항목 시험은 MapEdit.Tests.

## 남은 일
- 실기 미확인
