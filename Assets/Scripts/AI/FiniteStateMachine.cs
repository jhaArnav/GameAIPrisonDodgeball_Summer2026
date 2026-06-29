using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameAI
{

    public class FiniteStateMachine<T> : IFiniteStateMachine<T>
    {
        public IStateBase<T> CurrentState { get; private set; }

        public IState<T> InitialState { get; private set; }

        // array indexer. Example fsm["attack_state"]
        public IStateBase<T> this[string n]
        {
            get
            {
                if (States.TryGetValue(n, out IStateBase<T> state))
                    return state;
                else
                    throw new IndexOutOfRangeException();
            }
        }

        Dictionary<string, IStateBase<T>> States = new Dictionary<string, IStateBase<T>>();

        Dictionary<IStateBase<T>, StateTransitionBase<T>> DeferredStateTransitionPayloads = new Dictionary<IStateBase<T>, StateTransitionBase<T>>();

        IState<T> GlobalTransitionState;

        T StateInitializationParameter;

        // constructor
        public FiniteStateMachine(T param)
        {
            StateInitializationParameter = param;
        }



        public StateTransition<T> CreateStateTransition(string stateName)
        {
            return CreateStateTransition(stateName, false);
        }

        public StateTransition<T> CreateStateTransition(string stateName, bool updateOnEnter)
        {
            var stateBase = this[stateName];

            var state = stateBase as IState<T>;

            if (state == null)
                throw new ArgumentException($"State {stateName} doesn't match argument signature. statebase: {stateBase}");

            return CreateStateTransition(state, updateOnEnter);

        }

        public StateTransition<T> CreateStateTransition(IState<T> state)
        {
            return CreateStateTransition(state, false);
        }

        public StateTransition<T> CreateStateTransition(IState<T> state, bool updateOnEnter)
        {
            var stateBase = state as IStateBase<T>;

            StateTransition<T> dst = null;

            DeferredStateTransitionPayloads.TryGetValue(stateBase, out StateTransitionBase<T> basedst);

            if (basedst != null)
            {
                dst = basedst as StateTransition<T>;
            }

            if (dst == null)
            {
                dst = new StateTransition<T>(state, updateOnEnter);
                DeferredStateTransitionPayloads.Add(stateBase, dst);
            }


            dst.UpdateOnEnter = updateOnEnter;


            return dst;
        }


        ///////////////////////////////////////////////////////////////////////


        public StateTransition<T, S0> CreateStateTransition<S0>(string stateName, S0 arg0)
        {
            return CreateStateTransition<S0>(stateName, arg0, false);
        }

        public StateTransition<T, S0> CreateStateTransition<S0>(string stateName, S0 arg0, bool updateOnEnter)
        {
            var stateBase = this[stateName];

            var state = stateBase as IState<T, S0>;

            if (state == null)
                throw new ArgumentException("State doesn't match argument signature");

            return CreateStateTransition<S0>(state, arg0, updateOnEnter);

        }



        public StateTransition<T, S0> CreateStateTransition<S0>(IState<T, S0> state, S0 arg0)
        {
            return CreateStateTransition<S0>(state, arg0, false);
        }

        public StateTransition<T, S0> CreateStateTransition<S0>(IState<T, S0> state, S0 arg0, bool updateOnEnter)
        {
            var stateBase = state as IStateBase<T>;

            StateTransition<T, S0> dst = null;

            DeferredStateTransitionPayloads.TryGetValue(stateBase, out StateTransitionBase<T> basedst);

            if (basedst != null)
            {
                dst = basedst as StateTransition<T, S0>;
            }

            if (dst == null)
            {
                dst = new StateTransition<T, S0>(state, arg0, updateOnEnter);
                DeferredStateTransitionPayloads.Add(stateBase, dst);
            }


            dst.UpdateOnEnter = updateOnEnter;
            dst.Arg0 = arg0;

            return dst;
        }


        ///////////////////////////////////////////////////////////////////////


        public StateTransition<T, S0, S1> CreateStateTransition<S0, S1>(string stateName, S0 arg0, S1 arg1)
        {
            return CreateStateTransition(stateName, arg0, arg1, false);
        }

        public StateTransition<T, S0, S1> CreateStateTransition<S0, S1>(string stateName, S0 arg0, S1 arg1, bool updateOnEnter)
        {
            var stateBase = this[stateName];

            var state = stateBase as IState<T, S0, S1>;

            if (state == null)
                throw new ArgumentException("State doesn't match argument signature");

            return CreateStateTransition<S0, S1>(state, arg0, arg1, updateOnEnter);

        }

        public StateTransition<T, S0, S1> CreateStateTransition<S0, S1>(IState<T, S0, S1> state, S0 arg0, S1 arg1)
        {
            return CreateStateTransition(state, arg0, arg1, false);
        }

        public StateTransition<T, S0, S1> CreateStateTransition<S0, S1>(IState<T, S0, S1> state, S0 arg0, S1 arg1, bool updateOnEnter)
        {
            var stateBase = state as IStateBase<T>;

            StateTransition<T, S0, S1> dst = null;

            DeferredStateTransitionPayloads.TryGetValue(stateBase, out StateTransitionBase<T> basedst);

            if (basedst != null)
            {
                dst = basedst as StateTransition<T, S0, S1>;
            }

            if (dst == null)
            {
                dst = new StateTransition<T, S0, S1>(state, arg0, arg1, updateOnEnter);
                DeferredStateTransitionPayloads.Add(stateBase, dst);
            }


            dst.UpdateOnEnter = updateOnEnter;
            dst.Arg0 = arg0;
            dst.Arg1 = arg1;

            return dst;

        }


        ///////////////////////////////////////////////////////////////////////



        public StateTransition<T, S0, S1, S2> CreateStateTransition<S0, S1, S2>(string stateName, S0 arg0, S1 arg1, S2 arg2)
        {
            return CreateStateTransition(stateName, arg0, arg1, arg2, false);
        }

        public StateTransition<T, S0, S1, S2> CreateStateTransition<S0, S1, S2>(string stateName, S0 arg0, S1 arg1, S2 arg2, bool updateOnEnter)
        {
            var stateBase = this[stateName];

            var state = stateBase as IState<T, S0, S1, S2>;

            if (state == null)
                throw new ArgumentException("State doesn't match argument signature");

            return CreateStateTransition<S0, S1, S2>(state, arg0, arg1, arg2, updateOnEnter);

        }


        public StateTransition<T, S0, S1, S2> CreateStateTransition<S0, S1, S2>(IState<T, S0, S1, S2> state, S0 arg0, S1 arg1, S2 arg2)
        {
            return CreateStateTransition(state, arg0, arg1, arg2, false);
        }

        public StateTransition<T, S0, S1, S2> CreateStateTransition<S0, S1, S2>(IState<T, S0, S1, S2> state, S0 arg0, S1 arg1, S2 arg2, bool updateOnEnter)
        {
            var stateBase = state as IStateBase<T>;

            StateTransition<T, S0, S1, S2> dst = null;

            DeferredStateTransitionPayloads.TryGetValue(stateBase, out StateTransitionBase<T> basedst);

            if (basedst != null)
            {
                dst = basedst as StateTransition<T, S0, S1, S2>;
            }

            if (dst == null)
            {
                dst = new StateTransition<T, S0, S1, S2>(state, arg0, arg1, arg2, updateOnEnter);
                DeferredStateTransitionPayloads.Add(stateBase, dst);
            }


            dst.UpdateOnEnter = updateOnEnter;
            dst.Arg0 = arg0;
            dst.Arg1 = arg1;
            dst.Arg2 = arg2;

            return dst;
        }



        ///////////////////////////////////////////////////////////////////////



        public StateTransition<T, S0, S1, S2, S3> CreateStateTransition<S0, S1, S2, S3>(string stateName, S0 arg0, S1 arg1, S2 arg2, S3 arg3)
        {
            return CreateStateTransition(stateName, arg0, arg1, arg2, arg3, false);
        }

        public StateTransition<T, S0, S1, S2, S3> CreateStateTransition<S0, S1, S2, S3>(string stateName, S0 arg0, S1 arg1, S2 arg2, S3 arg3, bool updateOnEnter)
        {
            var stateBase = this[stateName];

            var state = stateBase as IState<T, S0, S1, S2, S3>;

            if (state == null)
                throw new ArgumentException("State doesn't match argument signature");

            return CreateStateTransition<S0, S1, S2, S3>(state, arg0, arg1, arg2, arg3, updateOnEnter);

        }


        public StateTransition<T, S0, S1, S2, S3> CreateStateTransition<S0, S1, S2, S3>(IState<T, S0, S1, S2, S3> state, S0 arg0, S1 arg1, S2 arg2, S3 arg3)
        {
            return CreateStateTransition(state, arg0, arg1, arg2, arg3, false);
        }

        public StateTransition<T, S0, S1, S2, S3> CreateStateTransition<S0, S1, S2, S3>(IState<T, S0, S1, S2, S3> state, S0 arg0, S1 arg1, S2 arg2, S3 arg3, bool updateOnEnter)
        {
            var stateBase = state as IStateBase<T>;

            StateTransition<T, S0, S1, S2, S3> dst = null;

            DeferredStateTransitionPayloads.TryGetValue(stateBase, out StateTransitionBase<T> basedst);

            if (basedst != null)
            {
                dst = basedst as StateTransition<T, S0, S1, S2, S3>;
            }

            if (dst == null)
            {
                dst = new StateTransition<T, S0, S1, S2, S3>(state, arg0, arg1, arg2, arg3, updateOnEnter);
                DeferredStateTransitionPayloads.Add(stateBase, dst);
            }


            dst.UpdateOnEnter = updateOnEnter;
            dst.Arg0 = arg0;
            dst.Arg1 = arg1;
            dst.Arg2 = arg2;
            dst.Arg3 = arg3;

            return dst;
        }


        ///////////////////////////////////////////////////////////////////////


        public void SetGlobalTransitionState(IState<T> state)
        {
            if (state == null)
                throw new System.ArgumentNullException("state must be non-null");

            GlobalTransitionState = state;

            //state.Init(this, StateInitializationParameter);
        }

        public void SetInitialState(IState<T> state)
        {
            if (state == null)
                throw new System.ArgumentNullException("state must be non-null");

            if (!States.ContainsKey(state.Name))
            {
                throw new System.ArgumentException("state not in finite state machine");
            }

            InitialState = state;
        }

        public void AddState(IStateBase<T> state, bool makeInitialState)
        {
            if (state == null)
                throw new System.ArgumentNullException("state must be non-null");

            States.Add(state.Name, state);

            if (makeInitialState || InitialState == null)
            {
                var stateNoArgs = state as IState<T>;

                if (makeInitialState && stateNoArgs == null)
                    throw new ArgumentException("Initial State must take no args on Enter()");

                if (stateNoArgs != null)
                {
                    InitialState = stateNoArgs;

                    //Debug.Log($"Initial state is: {InitialState.Name}");
                }
            }

        }

        public void AddState(IStateBase<T> state)
        {
            AddState(state, false);
        }


        protected void InitializeStates()
        {
            if (GlobalTransitionState != null)
                GlobalTransitionState.Init(this, StateInitializationParameter);

            foreach(var s in States)
            {
                if(s.Value != null)
                    s.Value.Init(this, StateInitializationParameter);
            }
        }

        bool firstTime = true;



        int numConsecutiveImmediateStateUpdates = 0;
        const int maxConsecutiveImmediateStateUpdates = 5;

        public int CurrNumConsecutiveImmediateStateUpdates
        {
            get => numConsecutiveImmediateStateUpdates;
        }

        public int MaxConsecutiveImmediateStateUpdates
        {
            get => maxConsecutiveImmediateStateUpdates;
        }

        public void Update()
        {
            if (firstTime)
            {
                firstTime = false;

                InitializeStates();

                if(GlobalTransitionState != null)
                    GlobalTransitionState.Enter();

                if (CurrentState == null)
                {
                    //Debug.Log($"Setting CurrentState to InitialState: {InitialState.Name}");
                    if (InitialState == null)
                        throw new ApplicationException("InitialState not set");

                    CurrentState = InitialState;
                    InitialState.Enter();
                }
            }

            bool updateOnEnter = false;

            numConsecutiveImmediateStateUpdates = 0;

            do
            {
                updateOnEnter = false;

                if (GlobalTransitionState != null)
                {

                    var stateTransition = GlobalTransitionState.Update();

                    if (stateTransition != null)
                    {
                        if (!States.TryGetValue(stateTransition.TransitionState.Name, out IStateBase<T> state))
                        {
                            throw new System.ArgumentException("state not in finite state machine");
                        }

                        //Debug.Log($"Global/Wildcard: Switching to state {stateName}");

                        if (CurrentState != null)

                        {
                            CurrentState.Exit(true);

                            CurrentState = stateTransition.TransitionState;//States[stateTransition];

                            //CurrentState.Enter(this);
                            stateTransition.Execute();
                        }

                    }
                }


                if (CurrentState == null)
                {
                    //Debug.Log($"Setting CurrentState to InitialState: {InitialState.Name}");
                    CurrentState = InitialState;
                    //CurrentState.Enter(this);
                    InitialState.Enter();
                }
                else
                {

                    var stateTransition = CurrentState.Update();

                    if (stateTransition != null)
                    {
                        if (!States.TryGetValue(stateTransition.TransitionState.Name, out IStateBase<T> state))
                        {
                            throw new System.ArgumentException("state not in finite state machine");
                        }

                        //Debug.Log($"Switching to state {stateTransition.DeferredState.Name}");

                        CurrentState.Exit();

                        //CurrentState = States[stateTransition];
                        CurrentState = stateTransition.TransitionState;

                        //CurrentState.Enter(this);
                        stateTransition.Execute();

                        updateOnEnter = stateTransition.UpdateOnEnter;
                    }
                }

                ++numConsecutiveImmediateStateUpdates;

                if (updateOnEnter && numConsecutiveImmediateStateUpdates >= maxConsecutiveImmediateStateUpdates)
                    Debug.LogWarning($"Number of updates on transition has reached limit ({numConsecutiveImmediateStateUpdates}). Last state transition will be delayed a frame!");

            } while (updateOnEnter && numConsecutiveImmediateStateUpdates < maxConsecutiveImmediateStateUpdates);
        }


    }

}
