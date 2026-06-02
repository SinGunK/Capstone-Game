using UnityEngine;
/// 슬로우를 받을 수 있는 오브젝트에 구현하는 인터페이스
/// 사용법: hit.GetComponent<ISlowable>()?.ApplySlow(0.4f, 1.5f);
public interface ISlowable
{
    /// <param name="amount">이동속도 비율 (0.4f = 40% 속도로 감소)</param>
    /// <param name="duration">슬로우 지속시간 (초)</param>
    void ApplySlow(float amount, float duration);
}