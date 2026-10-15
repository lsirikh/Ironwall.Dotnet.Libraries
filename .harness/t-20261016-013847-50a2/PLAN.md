# PLAN — 비율 도메인 라벨 오프셋 캡 초과(가는 축) · 기존 데이터 복구 · Images.Zoom 0

## 목표
DbProbe 가 로컬 monitor_DB 에서 찾은 결함(라벨오프셋 도메인잔재(비율) 21건 · Images 줌범위밖 1건)의 원인을 코드로 확정하고 고친다.
비율 도메인 마커(이미지 · 라인/폴리곤/PidsGroup) 의 저장 오프셋은 늘 축별 ±3(IMAGE_LABEL_RADIUS_MULT) 안.

## 원인(확정)
- `LabelAdorner.OnMouseMove` 비율 분기: 상한을 **등방 px**(3·max(hw,hh)) 로만 걸고, 저장은 **축별**(nx/hw, ny/hh) 로 나눈다.
  좁은 축(hh ≪ hw)으로 끌면 ny ≤ 3·hw 인데 ÷hh 라 비율이 3·hw/hh 까지 커진다(W157·H40 → 최대 ±11.8).
  울타리처럼 투영 bbox 가 서브픽셀이면 `Math.Max(…,1d)` 하한 때문에 비율 = px ÷ 1 → 76.8 같은 값(DbProbe ⓑ).
  실측값과 일치: Id 261 W76·H55 oy=-3.975 ≤ 3·38/27.5≈4.15, Id 227 oy=76.8(hh≈1).
- 폭 조절(edge-pinned) 보상도 비율 U/X 에 클램프가 없다(같은 계열).
- DbProbe 의 판정 기준도 축별 |v|>3.

## 결정
1. 순수 함수 `LabelAdorner.NormalizedDragOffset(baseA, baseB, hw, hh, dx, dy)` 로 끌기 계산을 떼어 헤드리스 시험 가능하게 하고,
   기존 등방 px 상한은 그대로 둔 뒤 결과 비율을 축별 `ClampRatio`(±3) 로 묶는다. 폭 조절 보상 값도 같은 `ClampRatio`.
   (좁은 축 정규화 하한 1px 을 바꾸면 기존 라벨 그림 위치가 바뀌므로 건드리지 않는다.)
2. 기존 데이터 복구 = `GMapDbSymbolService.BuildSchemeAsync` 안의 자기제한 UPDATE
   `Symbols SET LabelOffsetX=0, LabelOffsetY=0 WHERE Category IN ('AREA_BOUNDARY','PIDS_GROUP') AND (ABS(X)>3 OR ABS(Y)>3)`.
   - px→비율 환산 근거 없음: 저장 당시 줌의 투영 하프익스텐트(RuntimePoints bbox)를 DB 가 모르고, ⓑ 는 하한 1px 에 걸린 값이라 축 길이를 알 수 없다
     → 0(마커 아래 기본 위치)으로 되돌린다. 한 축만 넘어도 다른 축도 px 시절 값일 수 있어(예: AREA_BOUNDARY (2,-52)) 쌍을 0.
   - 멱등: 이제 끌기 · 폭 조절이 ±3 밖 값을 만들 수 없으므로 WHERE 가 자기제한(2회차부터 0행) — 정상 값(≤3)은 건드리지 않는다.
     과거 같은 UPDATE 가 제거된 이유(정상 끌기가 >3 을 만들어 매 부팅 지워짐)는 1번 수정으로 사라졌다(되살림 함정 없음).
3. Images.Zoom=0: `ZoomLadder.IsVisibleAtEffectiveZoom` 규약상 objectZoom ≤ 0 = '설정 안 됨(항상 표시)', Images.Zoom DDL 기본값도 0.0.
   → 결함 아님, 데이터 변경 없음. DbProbe(Dotnet.Monitoring.Solution, 이 worktree 밖)의 'Images 줌범위밖' 은 Zoom=0 을 빼야 맞다 — 보고에 남김.

## 진행
- [x] 원인 확정
- [x] 순수 함수 추출 + 빨강 시험(LabelNarrowAxisTests) — 클램프 전 실패 확인
- [x] 클램프 추가 → 초록
- [x] DB 복구 UPDATE + 시험(SchemaGuardMigrationTests, monitor_test_db)
- [x] 관련 csproj 시험 · sln 시험

## 검증(2026-10-16)
- `LabelNarrowAxisTests`(GMaps.Ui): 클램프 전 5/6 실패(예: W157·H40 위로 100px → -5, W40·H170 → -7.5) → 클램프 후 6/6 통과.
- `SchemaGuardMigrationTests.should_reset_ratio_label_offset_over_cap_and_keep_valid_ones_when_booting`(GMaps.Db, `-p:IncludeTests=true`, 로컬 monitor_test_db):
  복구 UPDATE 를 잠깐 끈 상태에서 실패(`(2,-52)` 남음) → 켜고 통과. 시험용 행 3개(Title `__label_cap_test__`)는 finally 에서 삭제.
- GMaps.Ui 712/712 · GMaps.MapEdit.Tests 55/55 · GMaps.MapLayer.Tests 63/63 통과. `dotnet test Ironwall.Dotnet.Libraries.sln` 통과(GMaps 프로젝트는 sln 밖).
- GMaps.Db `should_preserve_every_zorder_when_booting_against_converged_schema` 실패는 기존 문제: monitor_test_db 가 운영 덤프 복원본이 아니라
  픽스처 행뿐(ZOrder 10 → 기존 band 시프트로 1010, 고유성 125≠1). 이번 변경(라벨 오프셋 UPDATE)과 무관.

## 남은 일(범위 밖 — 보고만)
- DbProbe(Dotnet.Monitoring.Solution.UiTests/Support/DbProbe.cs) 'Images 줌범위밖' 은 Zoom=0(설정 안 됨) 을 결함으로 센다 → `Zoom<>0` 조건 추가 필요.
- 실제 로컬 monitor_DB 의 21건은 앱이 이 판으로 다음에 부팅할 때 복구된다(이 작업은 운영/로컬 지도 DB 에 직접 쓰지 않았다).
