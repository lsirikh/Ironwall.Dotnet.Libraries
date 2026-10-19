# PLAN — 머지 전 마지막 점검 · 수정(A 1~13 · B 3건)

## 목표
병렬 점검이 남긴 후보 항목을 재현 시험으로 확인 → 실제 결함만 최소 수정 → 결과 표 REPORT.md.
map-3d 는 복사본에서 고치고 `.harness/t-20261020-010420-f207/map3d.patch` 로 남긴다(원본 직접 커밋 금지).

## 작업 방식(병렬 에이전트)
- 빌드가 겹치지 않게 저장소 복사본을 나눠 씀(모두 git 무시되는 `artifacts/` 아래):
  - `artifacts/obj/g1` — A1(이미지 자르기 Undo 실패 숨김) · A2+A9(묶음 회전 미확정 중 Ctrl+Z/Y, 예외 누출)
  - `artifacts/obj/g2` — A3(직접 그린 건물 Undo = 영역 키) · A4(건물 그리기 중 전역 키 / 확인 팝업 Enter) · B-박공 GIS 안내
  - `artifacts/obj/g3` — A5(SwitchToHwnd 경쟁) · A6(3D 구독 해제) · A7(영역 목록 늦은 응답) · A8(높이 판 저장 중 취소) · A13(굽기 업로드 타임아웃 · 중복 굽기)
  - `artifacts/map3d` — 원본 C:\workspace_python\map-3d(6102407) 의 git clone, vendor 는 원본에서 복사 — A10 · A11 · A12 · B(LaMa/SAM 취소 · 지붕 기준 · 박공 불가 윤곽)
- 복사본 경로에 `\obj\` 를 넣은 이유: 원본 소스를 훑는 계약 시험(Utils.Tests)이 `\bin\` · `\obj\` 만 건너뛰어, `artifacts/gN` 에 두면 복사본까지 읽어 실패한다.
- 각 에이전트가 끝나면 바뀐 파일을 이 작업 트리로 옮기고(겹치는 파일은 손으로 합침) 여기서 전체 재검증.
- 에이전트가 세션 끊김으로 멈추면 SendMessage 로 재개(경로 안내).
- 시험 명령: `dotnet test Ironwall.Dotnet.Libraries.sln` + `dotnet test tests/GMaps.{Ui,MapEdit,MapNav,PropertyPanel,MapLayer}.Tests`
  map-3d: `cd artifacts/map3d; & C:\workspace_python\map-3d\.venv\Scripts\python.exe -m pytest -q -p no:cacheprovider` · `node tests/*.mjs`

## 진행
- [x] 복사본 준비(g1~g3 · map3d), map-3d 기준선 300 passed
- [x] 솔루션 기준선(logs/r0-sln.txt) — PerfSamplerTests 경쟁으로 테스트 호스트 중단 → 시험 gate 폐기 제거(DECISIONS 2), Utils.Tests 3회 연속 886 통과
- [x] 복사본을 `artifacts/gN` → `artifacts/obj/gN` 로 이동(DECISIONS 1)
- [x] 세션 끊김으로 멈춘 에이전트 4개 재개
- [x] map3d 완료(313 passed · node 15 ok · map3d.patch apply --check 통과 — 조율자가 직접 재확인) — 보고 전문 artifacts/map3d-DONE.txt
- [x] g1 완료(A1 · A2 · A9 재현 · 수정) → patches/g1.patch 로 이 작업 트리에 적용(파일 동일 확인)
- [x] g3 완료(A5 · A6 · A7 · A8 · A13 재현 · 수정, 새 시험 3파일) → 겹치지 않는 파일은 복사, MapViewModel.cs 는 patches/g3-MapViewModel.patch(HEAD 를 CRLF 로 바꾼 기준)로 g1 위에 얹음
- [x] 직전 검사 7건 실패 원인 = 복사본이 `artifacts/gN` 에 있어 Utils.Tests 소스 스캔이 복사본을 읽음 → `artifacts/obj/` 로 옮겨 해소(제품 · 시험 수정 없음)
- [x] g2 완료(A3 · A4 · B-박공 재현 · 수정) → 겹치지 않는 8파일 복사, MapViewModel.cs 는 patches/g2-MapViewModel.patch 로 얹음
- [x] r1(g1+g3): sln EXIT 0 · Ui 1684 · MapNav 67 · PropertyPanel 436 · MapLayer 99 통과, MapEdit 755/759 — 실패 4건은 Map3DNavComboScenarioTests 키 시험(0.7 초 안에 몰림, 처리기가 실제 Keyboard.Modifiers 를 읽음 → 그 순간 실제 Ctrl/Alt 눌림 추정), 단독 재실행 2회 8/8 통과 — 최종 r2 에서 재확인
- [x] GIS 복사본(artifacts/obj) · artifacts/base 삭제(패치는 patches/ 에 보관)
- [x] r2 최종 재검증: GMaps 5개 전부 통과(Ui 1691 · MapEdit 766 · MapNav 67 · PropertyPanel 436 · MapLayer 99), map-3d 313 · node 15 · apply --check 0. sln 은 CameraPopup 시간 기준 7건(동시 pytest 부하) → r3 단독 재실행 EXIT 0 전부 통과
- [x] REPORT.md · DECISIONS.md(1~15) 완료, artifacts 전부 삭제

## 남은 일
- 없음(하네스 accept 뒤 map3d.patch 를 원본 map-3d 에 적용 · 커밋하는 것은 머지 단계 일)
- [ ] 이 작업 트리로 합치기 · 전체 재검증
- [ ] map3d.patch · REPORT.md · DECISIONS.md
- [ ] 정리(artifacts 복사본 삭제)

## 발견 · 메모
- 기준선 r0-sln: 결함 항목과 무관하게 `Utils.Tests/Perf/PerfSamplerTests` 의 시험 경쟁(gate 를 Set 한 뒤 바로 Dispose → 늦게 돈 디스패처 콜백이 폐기된 gate.Wait → 테스트 호스트 중단). 부하 중 2회 재현 → 수정.
- PowerShell 5.1 `Get-Content -Raw` 는 BOM 없는 UTF-8 을 ANSI 로 읽는다 — md 수정은 Edit/Write 도구로.
