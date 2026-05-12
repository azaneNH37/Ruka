using System;
using System.Collections.Generic;
using R3;
using Ruka.Core.Symbols;
using VContainer;
using VContainer.Unity;

namespace Ruka.Core.FSM
{
    public abstract class StateMachineBase : IStateMachine, IInitializable, IDisposable
    {
        private readonly ReactiveProperty<StateBase> _currentState = new();
        private readonly ReadOnlyReactiveProperty<StateBase> _currentStateReadOnly;
        private readonly IObjectResolver _resolver;
        private readonly Type _entryStateType;
        private readonly Dictionary<(Type, Symbol<StateTrigger>), TransitionRule> _transitionMap;
        private readonly Dictionary<Type, StateBase> _stateCache = new();

        protected StateMachineBase(IObjectResolver resolver, FsmRules rules)
        {
            _resolver = resolver;
            _entryStateType = rules.EntryStateType;
            _transitionMap = new Dictionary<(Type, Symbol<StateTrigger>), TransitionRule>(rules.Transitions);
            _currentStateReadOnly = _currentState.ToReadOnlyReactiveProperty();
        }

        public ReadOnlyReactiveProperty<StateBase> CurrentState
            => _currentStateReadOnly;

        public bool Transit(Symbol<StateTrigger> trigger)
        {
            if (_currentState.Value == null) return false;

            var key = (_currentState.Value.GetType(), trigger);
            if (!_transitionMap.TryGetValue(key, out var rule)) return false;

            if (rule.Guard != null && !rule.Guard()) return false;

            var prevState = _currentState.Value;
            var nextState = ResolveOrCache(rule.ToState);

            prevState.OnStateExit();
            _currentState.Value = nextState;
            nextState.SetMachine(this);
            nextState.OnStateEnter();

            return true;
        }

        protected bool TryDispatch<T>(Action<T> action) where T : class
        {
            if (_currentState.Value is T state)
            {
                action(state);
                return true;
            }

            return false;
        }

        void IInitializable.Initialize()
        {
            var entryState = ResolveOrCache(_entryStateType);
            entryState.SetMachine(this);
            entryState.OnStateEnter();
            _currentState.Value = entryState;
        }

        private StateBase ResolveOrCache(Type stateType)
        {
            if (!_stateCache.TryGetValue(stateType, out var state))
            {
                state = (StateBase)_resolver.Resolve(stateType);
                _stateCache[stateType] = state;
            }

            return state;
        }

        public void Dispose()
        {
            _currentState.Dispose();
        }
    }
}
