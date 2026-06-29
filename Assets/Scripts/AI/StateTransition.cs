namespace GameAI
{

    public sealed class StateTransition<T> : StateTransitionBase<T>
    {
        public IState<T> TransitionStateWithArgs { get; private set; }

        public StateTransition(IState<T> state, bool updateOnEnter) : base(state, updateOnEnter)
        {
            TransitionStateWithArgs = state;
        }

        public StateTransition(IState<T> state) : this(state, false)
        { }

        public override void Execute()
        {
            TransitionStateWithArgs.Enter();
        }
    }


    public sealed class StateTransition<T, S0> : StateTransitionBase<T>
    {
        public IState<T, S0> TransitionStateWithArgs { get; private set; }
        public S0 Arg0 { get; set; }

        public StateTransition(IState<T, S0> state, S0 arg0, bool updateOnEnter) : base(state, updateOnEnter)
        {
            TransitionStateWithArgs = state;
            Arg0 = arg0;
        }

        public StateTransition(IState<T, S0> state, S0 arg0) : this(state, arg0, false)
        { }

        public override void Execute()
        {
            TransitionStateWithArgs.Enter(Arg0);
        }
    }

    public sealed class StateTransition<T, S0, S1> : StateTransitionBase<T>
    {
        public IState<T, S0, S1> TransitionStateWithArgs { get; private set; }
        public S0 Arg0 { get; set; }
        public S1 Arg1 { get; set; }

        public StateTransition(IState<T, S0, S1> state, S0 arg0, S1 arg1, bool updateOnEnter) : base(state, updateOnEnter)
        {
            TransitionStateWithArgs = state;
            Arg0 = arg0;
            Arg1 = arg1;
        }

        public StateTransition(IState<T, S0, S1> state, S0 arg0, S1 arg1) : this(state, arg0, arg1, false)
        { }

        public override void Execute()
        {
            TransitionStateWithArgs.Enter(Arg0, Arg1);
        }
    }

    public sealed class StateTransition<T, S0, S1, S2> : StateTransitionBase<T>
    {
        public IState<T, S0, S1, S2> TransitionStateWithArgs { get; private set; }
        public S0 Arg0 { get; set; }
        public S1 Arg1 { get; set; }
        public S2 Arg2 { get; set; }

        public StateTransition(IState<T, S0, S1, S2> state, S0 arg0, S1 arg1, S2 arg2, bool updateOnEnter) : base(state, updateOnEnter)
        {
            TransitionStateWithArgs = state;
            Arg0 = arg0;
            Arg1 = arg1;
            Arg2 = arg2;
        }

        public StateTransition(IState<T, S0, S1, S2> state, S0 arg0, S1 arg1, S2 arg2) : this(state, arg0, arg1, arg2, false)
        { }


        public override void Execute()
        {
            TransitionStateWithArgs.Enter(Arg0, Arg1, Arg2);
        }
    }

    public sealed class StateTransition<T, S0, S1, S2, S3> : StateTransitionBase<T>
    {
        public IState<T, S0, S1, S2, S3> TransitionStateWithArgs { get; private set; }
        public S0 Arg0 { get; set; }
        public S1 Arg1 { get; set; }
        public S2 Arg2 { get; set; }
        public S3 Arg3 { get; set; }

        public StateTransition(IState<T, S0, S1, S2, S3> state, S0 arg0, S1 arg1, S2 arg2, S3 arg3, bool updateOnEnter) : base(state, updateOnEnter)
        {
            TransitionStateWithArgs = state;
            Arg0 = arg0;
            Arg1 = arg1;
            Arg2 = arg2;
            Arg3 = arg3;
        }

        public StateTransition(IState<T, S0, S1, S2, S3> state, S0 arg0, S1 arg1, S2 arg2, S3 arg3) : this(state, arg0, arg1, arg2, arg3, false)
        { }


        public override void Execute()
        {
            TransitionStateWithArgs.Enter(Arg0, Arg1, Arg2, Arg3);
        }
    }
}