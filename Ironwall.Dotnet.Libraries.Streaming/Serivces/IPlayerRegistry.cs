using Ironwall.Dotnet.Libraries.Streaming.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
/// <summary>
/// RtspPlayer 인스턴스 등록 및 관리 인터페이스
/// </summary>
public interface IPlayerRegistry
{
    /// <summary>
    /// Player 등록
    /// </summary>
    bool RegisterPlayer(string contextId, ImprovedRtspPlayer player);

    /// <summary>
    /// Player 등록 해제
    /// </summary>
    bool UnregisterPlayer(string contextId);

    /// <summary>
    /// Player 조회
    /// </summary>
    ImprovedRtspPlayer GetPlayer(string contextId);

    /// <summary>
    /// Player 존재 확인
    /// </summary>
    bool HasPlayer(string contextId);

    /// <summary>
    /// 모든 Player 제거
    /// </summary>
    void ClearAllPlayers();
}
