using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Moq;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : IEventApiService 목록 조회 Moq 표현식 팩토리 (F-22)
   Created By   : GHLee
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : `GetDetectionEventsAsync`/`GetMalfunctionEventsAsync` 의 Moq 표현식을
                  **이 한 파일에 모은다**.

                  왜 필요한가 — Moq 표현식 트리에는 **선택적 인수를 생략할 수 없다**(생략하면 CS0854).
                  그래서 시그니처에 필터 파라미터가 하나 늘 때마다 테스트 24곳이 CS1501/CS7036 으로
                  동시에 깨졌다. 표현식을 여기로 모아 두면 다음 시그니처 변경은
                  **이 파일 + 인라인 델리게이트 람다 4곳**만 손대면 끝난다.

                  ⚠ 새 파라미터가 생기면 아래 표현식에 `It.IsAny<>` 를 **빠짐없이 명시**한다.
****************************************************************************/

/// <summary>
/// <see cref="IEventApiService"/> 목록 조회용 Moq 표현식 팩토리.
/// <para><c>Setup</c>·<c>SetupSequence</c>·<c>Verify</c> 에 그대로 넘긴다.</para>
/// </summary>
internal static class EventApiQuery
{
    /// <summary>탐지 목록 — 모든 인수 무관(<c>It.IsAny</c> 전체).</summary>
    public static Expression<Func<IEventApiService, Task<ApiListResponse<DetectionEventDto>>>> AnyDetection()
        => x => x.GetDetectionEventsAsync(
            It.IsAny<string?>(),            // startDate
            It.IsAny<string?>(),            // endDate
            It.IsAny<int?>(),               // controller
            It.IsAny<int?>(),               // sensor
            It.IsAny<string?>(),            // status
            It.IsAny<string?>(),            // result      (F-22)
            It.IsAny<string?>(),            // typeEvent   (F-22)
            It.IsAny<int>(),                // page
            It.IsAny<int>(),                // limit
            It.IsAny<CancellationToken>());

    /// <summary>장애 목록 — 모든 인수 무관(<c>It.IsAny</c> 전체).</summary>
    public static Expression<Func<IEventApiService, Task<ApiListResponse<MalfunctionEventDto>>>> AnyMalfunction()
        => x => x.GetMalfunctionEventsAsync(
            It.IsAny<string?>(),            // startDate
            It.IsAny<string?>(),            // endDate
            It.IsAny<int?>(),               // controller
            It.IsAny<int?>(),               // sensor
            It.IsAny<string?>(),            // reason      (F-22)
            It.IsAny<int>(),                // page
            It.IsAny<int>(),                // limit
            It.IsAny<CancellationToken>());

    /// <summary>
    /// 탐지 목록 — <paramref name="sensor"/> 단일 센서 호출만 매칭한다
    /// (<c>controller</c>·<c>status</c> 는 프로덕션이 넘기지 않는 <c>null</c> 로 고정 — 그 계약이 깨지면 매칭도 깨져야 한다).
    /// </summary>
    public static Expression<Func<IEventApiService, Task<ApiListResponse<DetectionEventDto>>>> DetectionForSensor(int? sensor)
        => x => x.GetDetectionEventsAsync(
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            null,                           // controller — 죽은 인자, 프로덕션은 안 보낸다
            sensor,
            null,                           // status
            It.IsAny<string?>(),            // result      (F-22)
            It.IsAny<string?>(),            // typeEvent   (F-22)
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>());

    /// <summary>
    /// 탐지 목록 — <paramref name="sensors"/> 중 하나를 대상으로 하는 호출만 매칭한다(팬아웃 검증용).
    /// </summary>
    public static Expression<Func<IEventApiService, Task<ApiListResponse<DetectionEventDto>>>> DetectionForAnyOf(params int[] sensors)
        => x => x.GetDetectionEventsAsync(
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            null,                           // controller
            It.Is<int?>(s => s.HasValue && sensors.Contains(s.Value)),
            null,                           // status
            It.IsAny<string?>(),            // result      (F-22)
            It.IsAny<string?>(),            // typeEvent   (F-22)
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>());
}
