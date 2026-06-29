namespace GameAI
{
    public interface IFiniteStateMachine<T>
    {

        IStateBase<T> CurrentState { get;  }
        IState<T> InitialState { get;  }
        StateTransition<T> CreateStateTransition(string stateName, bool updateOnEnter);
        StateTransition<T, S0> CreateStateTransition<S0>(string stateName, S0 arg0, bool updateOnEnter);
        StateTransition<T, S0, S1> CreateStateTransition<S0, S1>(string stateName, S0 arg0, S1 arg1, bool updateOnEnter);
        StateTransition<T, S0, S1, S2> CreateStateTransition<S0, S1, S2>(string stateName, S0 arg0, S1 arg1, S2 arg2, bool updateOnEnter);
        StateTransition<T, S0, S1, S2, S3> CreateStateTransition<S0, S1, S2, S3>(string stateName, S0 arg0, S1 arg1, S2 arg2, S3 arg3, bool updateOnEnter);
        StateTransition<T> CreateStateTransition(string stateName);
        StateTransition<T, S0> CreateStateTransition<S0>(string stateName, S0 arg0);
        StateTransition<T, S0, S1> CreateStateTransition<S0, S1>(string stateName, S0 arg0, S1 arg1);
        StateTransition<T, S0, S1, S2> CreateStateTransition<S0, S1, S2>(string stateName, S0 arg0, S1 arg1, S2 arg2);
        StateTransition<T, S0, S1, S2, S3> CreateStateTransition<S0, S1, S2, S3>(string stateName, S0 arg0, S1 arg1, S2 arg2, S3 arg3);

    }
}
