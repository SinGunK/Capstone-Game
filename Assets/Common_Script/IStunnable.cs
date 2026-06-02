public interface IStunnable
{
    /// 스턴을 받을 수 있는 오브젝트에 구현하는 인터페이스
    /// 사용법: hit.GetComponent<IStunnable>()?.ApplyStun(1.5f);
    void ApplyStun(float duration);
}