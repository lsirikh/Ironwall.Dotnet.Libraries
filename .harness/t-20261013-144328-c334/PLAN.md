# PLAN — 장비 대량 등록(CSV · Excel 내보내기 · 올리기)

## 목표
장비 콘솔(Devices.Ui `DeviceDashboardViewModel/View`) 7 카테고리 레일에서 CSV · .xlsx 내보내기 · 양식 받기 · 올리기(미리보기 판정 → 등록).
PRD: `docs/prds/device-bulk-import-prd.md`(요구 FR-01~18 · 결정 D-1~10 · §8 구현/시험 표 · §9 헤디드 남은 항목).

## 진행
- [x] 구조 조사(저장 경로 · 카탈로그 · 접근기 · DTO 변환 · UnitScopeGate · 도움말 계약 · XAML 계약 시험)
- [x] 베이스라인 빌드(Devices.Ui 단독 빌드 성공)
- [x] PRD 작성
- [x] 코드: `Consoles/BulkImport/*` (BulkColumn · BulkFiles · BulkDeviceRows · BulkImportPlanner · BulkImportWriter · BulkExporter · BulkImportViewModel · BulkImportView)
- [x] 대시보드 배선(툴바 [올리기] · [내보내기 ▾] · [양식 ▾] · 목록 덮개 · 파일 끌어 놓기) + 도움말 키 `Devices.BulkImport`
- [x] 헤드리스 시험 BLK-001~022(31건) — `Tests/BulkImportTests.cs` · `Tests/BulkImportConsoleTests.cs`
- [x] Devices.Ui 전체 4,368 통과 · 솔루션 `dotnet test` 2,714 통과(기준선 실패 2 → 0)
- [x] 새 .cs/.xaml UTF-8 BOM

## 결정(요약 — 자세히는 PRD §6)
- 쓰기는 패널 Save 대신 같은 변환(ToXxxDeviceDto) · UnitScopeGate · IDeviceApiService Create/Update 를 직접 부름(PresetRegistrar 선례). 서버 일괄 엔드포인트 없음(api-test-server routers 확인).
- 대조 = ID → (카테고리, 이름). IP:포트 · 번호는 충돌 검사.
- 빈 칸 = 수정 시 바꾸지 않음. 서버 값과 같은 칸은 검증 · 전송하지 않음(옛 값 포트 0 등이 왕복에서 오류가 되지 않게).
- 비밀번호 내보내기 금지 · 미리보기 가림 · 로그엔 줄 번호/개수만.
- 패키지: CsvHelper 33.0.1 · ClosedXML 0.104.2 를 Devices.Ui 에 참조 추가(저장소 기존 판본, 새 패키지 아님). ExcelDataReader 는 쓰지 않음(ClosedXML 로 읽기도 충분).
- 미리보기는 별 창이 아니라 목록 칸 덮개. 레일을 바꾸면 닫히고, 보내는 중에는 레일 이동을 막는다.
- 수정은 캐시 모델의 사본(복사 생성자 / DTO 왕복)에 쓴다 — 실패해도 캐시가 더럽혀지지 않음.
- Consoles/Wiring 폴더는 건드리지 않음.

## 발견
- Devices.Ui 시험은 같은 csproj 안 `Tests/` (sln 밖) → `dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/...csproj --filter`.
- 행 뷰모델 생성자가 정적 IoC 에서 IEventAggregator 를 찾는다 — 시험은 IoC 범위를 잡아야 함.
- Utils.Tests 가 저장소 전 XAML 을 훑음: 버튼 계열 Style 필수 · 40자 이상 TextBlock 글자 금지 · HelpKey ↔ 카탈로그 양방향.
- 이 세션 셸은 변수 확장 · 반복문이 든 명령이 거부됨 → BOM 은 Edit 로 첫 줄 앞에 U+FEFF 삽입.

## 남은 일(사람 · 헤디드)
- PRD §9: 실창 OLE 끌어 놓기 · 파일 창 · Excel 유효성 목록 확인 · 실서버(6.3.2/8.0.x) 대량 등록 왕복.
- 호스트(Dotnet.Monitoring.Solution) UiTests `DestructiveGuard` 에 `Devices.BulkImport.Register` · `Devices.BulkImport.Retry` 등록 필요.
