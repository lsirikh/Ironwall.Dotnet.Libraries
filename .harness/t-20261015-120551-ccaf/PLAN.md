# PLAN — 이슈 #7-20 함체 계측(ENCLOSURE_METRICS) 수신

## 목표
통제DB 가 10초마다 `sensorway.{부대ID}.gis.enclosure-metrics` 로 보내는 `ENCLOSURE_METRICS`(브로커 연동설계 §6.6)를
라이브러리가 스스로 구독해 함체별 최신 계측을 보관하고, 지도 함체 심볼 툴팁과 함체 상세 창에 최신 계측 · 받은 시각을 보인다.
30초 넘게 새 계측이 안 오면 '오래된 값'. 메인 솔루션은 고치지 않는다.

## 진행
- [x] 페이로드 해석 `Services/Enclosure/EnclosureMetricsPayload.cs`(record `EnclosureMetricsReading` + `TryParse`)
- [x] 수신 서비스 `EnclosureMetricsNatsSyncService`(BroadcastStatusNatsSyncService 와 같은 방식) + DI 등록(`GMapUiModule`)
- [x] 표시 글 `Helpers/Components/EnclosureMetricsText.cs`(단위 ℃ · % · V · A · UPS 배터리 %, 받은 시각, 오래된 값)
- [x] 마커 `GMapPidsMarker.EnclosureMetrics`(런타임) → 컨트롤 DP 바인딩 → `SymbolStatusText.ToolTip` 끝에 계측 절
- [x] 상세 창 `SymbolDetailViewModel` 기본 탭 맨 밑에 계측 행(함체만), 계측 바뀌면 열린 창도 다시 그림
- [x] `MapViewModel` 활성/비활성에서 구독 시작/해제 + 5초 타이머(오래된 값 전환 · 나중에 생긴 심볼에도 반영)
- [x] 시험: `tests/GMaps.Ui.Tests/EnclosureMetricsPayloadTests.cs`(순수 9건), `tests/GMaps.Housing.Tests/EnclosureMetricsScenarioTests.cs`(NATS→툴팁·상세 1건)
- [ ] 전체 `dotnet test Ironwall.Dotnet.Libraries.sln` 확인

## 결정
- "함체 속성(상세)" = 심볼 상세 보기 창으로 해석. 상태 탭은 2026-09-09 사용자 결정으로 내려 있으므로 **기본 탭 맨 밑**에 둔다(새 XAML 없음 — 기존 라벨-값 행 스타일 · 토큰 그대로, 오래된 값은 기존 `warn` 톤).
- 툴팁은 기존 문자열 툴팁에 줄을 덧붙인다(새 상자 없음). 화면 설명 문장은 넣지 않았다('?' 추가도 불필요하다고 판단).
- '받은 시각' = GIS 가 메시지를 받은 로컬 시각. 오래된 값 판정도 이 시각 기준(30초 초과, `EnclosureMetricsText.StaleAfter`). `observed_at` · `created_at` · `detail` · `id` 는 표시하지 않는다(요구 범위 밖).
- 진동은 단위 없이 숫자만(규격 0~100 레벨). 숫자가 문자열로 와도 읽고, 못 읽으면 값 없음(행 생략).
- 10초 × 함체 수만큼 오므로 수신 로그는 남기지 않는다(오류만).
- 새 파일 UTF-8 BOM 추가는 셸 권한 거부로 못 했다(컴파일 · 시험엔 영향 없음).

## 남은 일
- 전체 솔루션 시험 결과 확인.
