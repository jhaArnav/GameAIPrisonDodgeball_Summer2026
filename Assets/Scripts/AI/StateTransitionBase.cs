using System;
namespace GameAI
{
    public abstract class StateTransitionBase<T>
    {
        public IStateBase<T> TransitionState { get; private set; }
        public bool UpdateOnEnter { get; set; }

        public StateTransitionBase(IStateBase<T> state, bool updateOnEnter)
        {
            if (state == null)
                throw new ArgumentNullException("state cannot be null");

            TransitionState = state;
            UpdateOnEnter = updateOnEnter;
        }

        public StateTransitionBase(IStateBase<T> state):this(state, false)
        {}

        public abstract void Execute();
    }
}
