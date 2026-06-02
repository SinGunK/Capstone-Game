using UnityEngine;

public static class GameContext
{
    /// 플레이어 위치를 전역으로 관리하는 정적 클래스
    /// PlayerController에서 자동으로 등록됨
    /// 사용법: GameContext.PlayerTransform.position
    public static Transform PlayerTransform;
}