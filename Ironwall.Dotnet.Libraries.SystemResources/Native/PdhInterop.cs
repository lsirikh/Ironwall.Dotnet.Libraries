using System;
using System.Runtime.InteropServices;

namespace Ironwall.Dotnet.Libraries.SystemResources.Native;

/****************************************************************************
   Purpose      : PDH(pdh.dll) P/Invoke — 로케일 독립 English 카운터
   Created By    : GHLee
   Created On    : 2026-07-13
   Department    : SW Team
   Company       : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// Windows Performance Data Helper(pdh.dll) 얇은 P/Invoke 계층. <b>반드시</b>
/// <see cref="PdhAddEnglishCounterW"/>를 사용한다 — 관리형 <c>PerformanceCounter</c>는 카운터명을
/// 로케일별로 번역(ko-KR "프로세서")하여 영문 경로가 실패하지만, English 카운터 API는 Perflib 인덱스
/// 기반이라 표시 언어(MUI)와 무관하게 영문 경로를 해석한다. 커널 드라이버·관리자 권한 불요.
/// </summary>
internal static class PdhInterop
{
    // ── 포맷 플래그 ────────────────────────────────────────────────
    public const uint PDH_FMT_DOUBLE = 0x00000200;

    // ── 상태 코드(uint) — RISK-02: fail-safe 분기 판정용 ────────────
    public const uint ERROR_SUCCESS = 0x00000000;
    public const uint PDH_CSTATUS_VALID_DATA = 0x00000000;
    public const uint PDH_CSTATUS_NEW_DATA = 0x00000001;
    public const uint PDH_MORE_DATA = 0x800007D2;
    public const uint PDH_CSTATUS_NO_MACHINE = 0x800007D0;
    public const uint PDH_CSTATUS_NO_INSTANCE = 0x800007D1;
    public const uint PDH_NO_DATA = 0x800007D5;
    public const uint PDH_CSTATUS_NO_OBJECT = 0xC0000BB8;
    public const uint PDH_CSTATUS_NO_COUNTER = 0xC0000BB9;
    public const uint PDH_CSTATUS_INVALID_DATA = 0xC0000BBA;
    public const uint PDH_MEMORY_ALLOCATION_FAILURE = 0xC0000BBB;
    public const uint PDH_INVALID_HANDLE = 0xC0000BBC;
    public const uint PDH_INVALID_DATA = 0xC0000BC6;
    public const uint PDH_ACCESS_DENIED = 0xC0000BDB;

    // ── 단일 인스턴스 값(PDH_FMT_COUNTERVALUE, double 조회) ──────────
    // 네이티브: { DWORD CStatus; union{...double(8B)...}; } — Sequential 패딩으로 double은 offset 8.
    [StructLayout(LayoutKind.Sequential)]
    public struct PDH_FMT_COUNTERVALUE_DOUBLE
    {
        public uint CStatus;
        public double doubleValue;
    }

    // ── 배열 항목(PDH_FMT_COUNTERVALUE_ITEM_W) — GPU 와일드카드 ──────
    // 네이티브: { LPWSTR szName; PDH_FMT_COUNTERVALUE FmtValue; } — C ABI 정렬을 Sequential이 그대로 따름.
    // 크기는 하드코딩하지 않고 Marshal.SizeOf로 얻는다(x86/x64 자동).
    [StructLayout(LayoutKind.Sequential)]
    public struct PDH_FMT_COUNTERVALUE_ITEM_DOUBLE
    {
        public IntPtr szName;   // LPWSTR (버퍼 내부를 가리킴)
        public uint CStatus;
        public double doubleValue;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint PdhOpenQueryW(string? szDataSource, IntPtr dwUserData, out IntPtr phQuery);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint PdhAddEnglishCounterW(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

    [DllImport("pdh.dll", SetLastError = true)]
    public static extern uint PdhCollectQueryData(IntPtr hQuery);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint PdhGetFormattedCounterValue(IntPtr hCounter, uint dwFormat, out uint lpdwType, out PDH_FMT_COUNTERVALUE_DOUBLE pValue);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint PdhGetFormattedCounterArrayW(IntPtr hCounter, uint dwFormat, ref uint lpdwBufferSize, ref uint lpdwItemCount, IntPtr ItemBuffer);

    [DllImport("pdh.dll", SetLastError = true)]
    public static extern uint PdhCloseQuery(IntPtr hQuery);

    /// <summary>PDH 상태 코드가 "유효 데이터"인지(값을 사용할 수 있는지).</summary>
    public static bool IsValidData(uint cstatus)
        => cstatus == PDH_CSTATUS_VALID_DATA || cstatus == PDH_CSTATUS_NEW_DATA;
}
