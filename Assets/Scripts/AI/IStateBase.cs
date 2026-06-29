namespace GameAI
{
    public interface IStateBase<T>
    {
        string Name { get; }
        void Init(IFiniteStateMachine<T> fsm, T param);
        StateTransitionBase<T> Update();
        void Exit(bool globalTransition);
        void Exit();
    }
}
