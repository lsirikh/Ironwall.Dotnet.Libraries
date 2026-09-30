# 분석 — 설치 고유값(`client_id`)이 디스크 이미지 복제로 복제되는 구멍

| 항목 | 값 |
|---|---|
| 일자 | 2026-09-30 |
| 발견 경로 | 세션 축 단일화 작업 중 **적대적 검증** |
| 심각도 | 🔴 높음 — 재현되면 관제석 전체가 상호 축출 |
| 소유 | **설치기**(`Dotnet.Monitoring.Solution/Installer`) — 런타임 코드로는 못 막는다 |
| 상태 | **미해결 · 미착수** |

---

## 1. 무엇이 문제인가

설치 고유값이 **`%ProgramData%\Ironwall\Gis\client-id`** 에 보존된다. 설치 폴더 밖에 두는 것은 **의도된 설계**다 — 재설치·업그레이드에도 값이 유지되어, 비정상 종료로 남은 자기 유령 세션을 `self_replace` 로 회수할 수 있다.

**그런데 그 경로는 디스크 이미지에 포함된다.**

```
PC 한 대를 구성 → 디스크 이미지 생성 → 관제석 10대에 복제 배포
   → 10대 모두 %ProgramData%\Ironwall\Gis\client-id 가 같은 값
   → session_self_replace_enabled 를 켜는 순간 10대가 상호 축출
```

이는 이번 작업으로 고친 `central-ui` 공통값 문제와 **증상이 완전히 같다.** 축을 설치 고유값으로 옮겨도, 그 고유값이 복제되면 무력화된다.

### 왜 런타임 코드로 못 막는가

현재 판정 로직은 **"파일에 유효한 값이 있으면 그대로 쓴다"** 이다(`Pages.iss` `InitStationId`, 그리고 `ClientIdResolver.ReadOrCreateInstallId`). 복제된 값도 형식상 완벽히 유효하므로 **구분할 근거가 없다.**

`sysprep` 도 `%ProgramData%` 를 지우지 않는다 — 머신 SID·컴퓨터명만 갱신한다.

### 관제 현장에서 흔한 배포 방식인가

그렇다. 동일 하드웨어 관제석을 다수 납품하는 구성에서 이미징은 표준 관행이다. **가정이 아니라 대비해야 하는 경로다.**

---

## 2. 보조 위험 — GUID 생성 폴백

`NewStationId`(`Pages.iss`)는 `Scriptlet.TypeLib` COM 으로 GUID 를 만들고, 실패하면 이렇게 떨어진다.

```pascal
G := GetSHA1OfString(GetDateTimeString('yyyymmddhhnnss','-',':') + '|' +
                     {computername} + '|' + {username} + '|' + IntToStr(Length(G)));
```

입력이 **시각(초 단위) + 컴퓨터명 + 사용자명**뿐이다. 이미지 복제 환경에서 컴퓨터명·사용자명이 같고 **같은 초에 설치**되면 값이 충돌한다. 확률은 낮지만 무작위성이 사실상 시각 한 축에 걸려 있다.

`ClientIdResolver`(런타임)는 `Guid.NewGuid()` 를 쓰므로 이 폴백 문제가 없다.

---

## 3. 대응안

### 안 1 (권장) — 값에 **기기 지문**을 묶고 불일치하면 재생성

파일에 값만 적지 말고 **그 값이 어느 기기에서 만들어졌는지**를 함께 적는다.

```
gis-fd44d1f63952
machine=<머신 SID 또는 MachineGuid>
created=2026-09-30T15:14:00+09:00
```

첫 실행 때 현재 기기 지문과 대조하고 **다르면 값을 새로 만든다.**

| 지문 후보 | 이미지 복제 후 바뀌나 | 비고 |
|---|---|---|
| **머신 SID** (`whoami /user` 의 도메인 부분 · `S-1-5-21-…`) | ✅ sysprep 이 갱신 | sysprep 을 **안 돌리면** 안 바뀜 |
| `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid` | ✅ sysprep 이 갱신 | 읽기 쉬움 |
| 컴퓨터명 | △ 보통 바꾸지만 보장 없음 | 보조 축으로만 |
| 볼륨 시리얼 | ✅ 보통 다름 | 디스크 교체 시 값이 바뀌는 오탐 |

➡ **`MachineGuid` + 컴퓨터명 조합**을 권한다. 둘 중 하나라도 다르면 재생성.
➡ ⚠ **오탐 비용**: 지문이 정상적으로 바뀌는 경우(디스크 교체·컴퓨터명 변경)에도 값이 새로 생겨 **옛 유령 세션을 회수할 수 없게 된다**. 다만 유령 세션은 토큰 만료(24h)로 사라지므로 대가가 축출보다 훨씬 작다.

### 안 2 — 설치기가 **무인 설치 인자**로 값을 받게 한다

이미징 배포 현장이 각 PC 에 값을 주입하는 길을 열어 준다.

```
IronwallMonitoring_Setup.exe /VERYSILENT /clientid=gis-<현장이 지정한 고유값>
```

- 이미지 복제 후 **후처리 스크립트**가 PC 마다 다른 값을 넣을 수 있다
- 안 1 과 배타적이지 않다 — 함께 두는 것이 낫다

### 안 3 — 배포 지침으로만 막는다 (권하지 않음)

"이미지 복제 후 `%ProgramData%\Ironwall\Gis\client-id` 를 삭제하라"를 문서로 남기는 것. **사람이 잊으면 그대로 사고가 난다.** 안 1·2 의 보조로만 의미가 있다.

---

## 4. 판정

| | |
|---|---|
| 권고 | **안 1 + 안 2** 동시 적용 |
| 규모 | `Pages.iss`(생성·검증) + `IronwallMonitoring.iss`(무인 인자) — 설치기 국소 수정 |
| 선행조건 | 없음. 이번 런타임 수정과 독립 |
| 급한가 | **서버팀이 `session_self_replace_enabled` 를 켜기 전까지**는 터지지 않는다. 다만 켜는 시점을 우리가 정하지 않으므로 미리 닫는 것이 맞다 |

## 5. 이 분석이 틀렸다면 무엇을 보고 알 수 있나

- 관제석 2대 이상에서 `%ProgramData%\Ironwall\Gis\client-id` 내용이 **같으면** 이 위험이 실재한다
- 서버 세션 관리 화면에서 **같은 `client_id` 로 여러 관제석**이 보이면 이미 그 상태다
- 반대로 현장 배포가 이미징을 쓰지 않고 PC 마다 설치기를 돌린다면, 이 항목의 우선순위는 내려간다 → **현장 배포 방식 확인이 선행 정보다**

## 6. 관련

- 런타임 쪽 수정: [client-id-session-axis-report.md](../reports/client-id-session-axis-report.md)
- 웹 계열 같은 축 문제: [WEB_VMS_client_id_session_axis_NOTIFY_20260930.md](../coordinations/WEB_VMS_client_id_session_axis_NOTIFY_20260930.md)
- 서버 측 경고 원문: `api-test-server/app/routers/auth.py:613-616`
