# 완료 리포트 — 세션 축(`client_id`) 단일화

| 항목 | 값 |
|---|---|
| 일자 | 2026-09-30 |
| Track | B (작은 범위 수정) |
| 롤백 태그 | `before-client-id-unify` (`3b248642`) |
| 상태 | **코드·테스트 완료** · 실기(배포 설치본) 검증 **미실행** |

---

## 1. 무엇이 문제였나

서버는 세션을 **`(user_id, client_id)`** 로 가른다. 관리자가 `session_self_replace_enabled` 를 켜면 **같은 `client_id`** 의 옛 세션을 끊어, 비정상 종료로 남은 **자기 유령 세션을 회수**해 준다 — 우리가 원하는 동작이다.

그런데 그 값이 **전 PC 공통**이면 "자기 옛 세션" 이 아니라 **남의 현재 세션**을 끊는다.

```
A 관제석 로그인  →  같은 client_id 인 B 세션을 끊음
B 재로그인       →  A 를 끊음   →  …  무한 축출
```

서버 코드가 이 함정을 주석으로 경고하고 있다:

```python
# app/routers/auth.py:613-616
# self-replace(옵트인, 기본 off): 같은 (user_id, client_id) 세션만 교체.
#   ★공유 client_id 로 self-replace 를 켜면 상호 evict — 고유 client_id 전제.
if settings_service.get(db, SettingKey.SESSION_SELF_REPLACE_ENABLED) and _client_id:
    _to_evict.extend(s for s in _active_sessions if s.client_id == _client_id)
```

### 실측된 GIS 상태 (수정 전)

| 자리 | 보내던 값 | PC 고유? |
|---|---|---|
| GOP 요청 **헤더** `X-Client-Id` | `ApiSetupModel.ClientId` — 기본값 **`central-ui`** | ❌ |
| 로그인 **본문** `client_id` | `ClientIdentity.Current` = `gis-monitoring:{머신명}` | 부분적 |

그리고 서버는 **헤더를 먼저 보고 본문은 헤더가 없을 때만** 본다:

```python
_client_id = request.headers.get("X-Client-Id") or getattr(login_data, "client_id", None)
```

➡ **머신명이 섞인 본문 값은 조용히 버려지고, 전 PC 공통인 `central-ui` 가 세션 축이 되어 있었다.**

확인된 실례: `C:\PidsMonitoringSystem\appsettings.json` 에 `ClientId` 키 자체가 없어 기본값 `central-ui` 가 쓰였다. 개발 빌드는 `gis-monitoring`(머신 접미어 없음) — 역시 공통.

---

## 2. 정본은 이미 있었다 — 설치기 FR-20

설치기가 **설치 시 고유값을 자동 생성**한다. 이번 작업은 그것을 **런타임에서 쓰게 만든 것**이다.

| 구현 | 위치 |
|---|---|
| `gis-` + GUID 12자 생성 (`Scriptlet.TypeLib`) | `Installer/Pages.iss` `NewStationId` |
| **설치 폴더 밖 보존** → 재설치·업그레이드에도 동일 | `%ProgramData%\Ironwall\Gis\client-id` |
| appsettings 주입 | `Installer/ConfigWriter.iss:88` `__CLIENT_ID__` → `ClientId`·`SystemUuid` |
| 서버 검증식과 동일 판정 | `Pages.iss` `StationIdLooksValid` = `^[A-Za-z0-9._:-]{1,64}$` |
| 옛 공용값 `gis-monitoring` 이면 자동값으로 교체 | `Pages.iss:181,264` |

이 PC 실측: `%ProgramData%\Ironwall\Gis\client-id` = **`gis-fd44d1f63952`**.

> ⚠ 이전 계획(2026-09-30 오전)은 축을 **`MachineName`** 으로 잡았다. **폐기했다** — 설치 고유값이 정본이고, 머신명은 이미지 복제·이름 변경에 약하다.

---

## 3. 무엇을 바꿨나

### 신규 — `Ironwall.Dotnet.Libraries.Api/Helpers/ClientIdResolver.cs`

세션 축의 **단일 정본 생성기**. `Api` 가 `Accounts.Api` 보다 하위라 양쪽이 같은 값을 쓸 수 있는 자리다.

판정 순서:

| 순서 | 조건 | 동작 |
|---|---|---|
| ① | 설정값이 유효하고 옛 공용값이 아니다 | **그대로 쓴다**(설치기 주입값·관리자 지정 존중) |
| ② | 설정값이 비었거나 옛 공용값(`central-ui`·`gis-monitoring`)이거나 패턴 위반 | `%ProgramData%` 파일을 읽는다 |
| ③ | 파일도 없다 | **새로 만들어 적는다** (`gis-` + GUID 12자) |
| ④ | 파일 접근 불가 | 프로세스 수명 임시값 + **경고 로그** |

②③ 덕분에 **설치기를 타지 않은 실행본**(개발 빌드 · FR-20 이전 옛 설치본)도 자동으로 고유값을 갖는다. **사람이 손댈 일이 없다.**

### 수정 — `Api/Services/ApiService.cs`

헤더 값을 `_setupModel.ClientId` 원본이 아니라 `ClientIdResolver.Resolve(...)` 결과로 붙인다. 대체가 일어나면 이유가 로그에 남는다.

### 수정 — `Accounts.Api/Helpers/ClientIdentity.cs`

머신명 축을 **폐기**하고 리졸버에 위임하는 얇은 껍데기로 바꿨다. 본문·헤더가 같은 값이 된다.

---

## 4. 설계 중 잡은 결함 1건 (자체 발견)

초안은 캐시가 `_cached ??= Build(...)` 한 줄이었다. 이러면 **설정값을 모르는 호출부**(`ClientIdentity.Current`)가 먼저 불릴 때 폴백값이 캐시를 선점해 **관리자 지정값을 영구히 삼킨다.**

➡ `Resolve` 를 **호출 순서에 안전**하게 고쳤다: 캐시가 이미 있어도 뒤늦게 온 **유효한 설정값이 이기고**, 바로잡았다는 사실을 로그에 남긴다.
➡ 회귀 시험으로 고정: `should_prefer_configured_value_when_it_arrives_after_a_fallback_took_the_cache`.

---

## 5. 검증

| 항목 | 결과 |
|---|---|
| 신규 시험 `ClientIdResolverTests` | **30 통과 / 0 실패** |
| `Accounts.Api` 전체 회귀 | **207 통과 / 0 실패** |
| 솔루션 전체 빌드 | **오류 0** (경고 477 — 전부 기존 `Streaming` 등) |
| 인코딩 | 신규·수정 `.cs` **UTF-8 BOM** 확인, 한글 mojibake 없음 |

시험이 지키는 것: 옛 공용값이 **세션 축으로 나가지 않음** · 서버 패턴 준수(한글·공백·64자 초과 포함) · 본문 = 헤더 동일값 · 머신명 비포함 · 호출 순서 안전성 · 대체 시 진단 로그.

### 미검증 (통과로 적지 않는다)

| 항목 | 왜 |
|---|---|
| **실기 배포 설치본에서의 동작** | `C:\PidsMonitoringSystem\` 에 exe 가 없다(폴더·설정만 남은 껍데기). 설치 후 재확인 필요 |
| `%ProgramData%` 파일 **생성** 경로 | 환경 의존이라 단위시험에서 단언하지 않았다(이 PC 에는 파일이 이미 있음) |
| 서버에 실제로 기록되는 값 | 로그인 왕복으로 세션 행을 확인해야 한다 |

---

## 6. 남은 일

| # | 내용 | 문서 |
|---|---|---|
| 1 | **이미지 복제로 설치 고유값이 복제되는 구멍** — 설치기가 감지해야 한다 | [client-id-image-clone-risk-analysis.md](../analyses/client-id-image-clone-risk-analysis.md) |
| 2 | **웹/VMS 에 "고정 `client_id` 금지 · 브라우저 프로필별 GUID"** 통지 | [WEB_VMS_client_id_session_axis_NOTIFY_20260930.md](../coordinations/WEB_VMS_client_id_session_axis_NOTIFY_20260930.md) |
| 3 | 서버팀이 `session_self_replace_enabled` 를 켜기 **전에** 위 1·2 가 닫혀야 한다 | 3자 계약 rev.4 R10 |

---

## 7. 바뀐 파일

```
신규  Ironwall.Dotnet.Libraries.Api/Helpers/ClientIdResolver.cs
신규  Ironwall.Dotnet.Libraries.Accounts.Api/Tests/ClientIdResolverTests.cs
수정  Ironwall.Dotnet.Libraries.Api/Services/ApiService.cs          (헤더 값 · using)
수정  Ironwall.Dotnet.Libraries.Accounts.Api/Helpers/ClientIdentity.cs (머신명 축 폐기 → 위임)
```

`ApiSetupModel.ClientId` 의 기본값(`central-ui`)은 **건드리지 않았다** — 리졸버가 옛 공용값으로 걸러내므로 불필요하고, 다른 주체가 이 라이브러리를 쓸 때의 하위호환을 깨지 않는다.
