using System;
using System.Collections.Generic;
using Ruka.Core.Symbols;
using VContainer;
using VContainer.Unity;

namespace Ruka.Core.FSM
{
    public interface IFsmConfigurator
    {
        IFsmConfigurator Entry<TState>() where TState : StateBase;
        IFsmTransitionBuilder On<TFrom>(Symbol<StateTrigger> trigger) where TFrom : StateBase;
    }

    public interface IFsmTransitionBuilder
    {
        IFsmConfigurator To<TTo>() where TTo : StateBase;
    }

    public static class FsmBuilderExtensions
    {
        public static void RegisterFsm<TFsm>(
            this IContainerBuilder builder,
            Action<IFsmConfigurator> configure)
            where TFsm : StateMachineBase
        {
            var configurator = new FsmConfigurator();
            configure(configurator);
            var rules = configurator.Build();

            foreach (var stateType in rules.AllStateTypes)
            {
                builder.Register(stateType, Lifetime.Singleton);
            }

            builder.RegisterEntryPoint<TFsm>()
                .AsSelf()
                .WithParameter(rules);
        }
    }

    internal sealed class FsmConfigurator : IFsmConfigurator
    {
        private Type _entryStateType;
        private readonly Dictionary<(Type, Symbol<StateTrigger>), TransitionRule> _transitions = new();
        private readonly List<Type> _allStateTypes = new();
        private Type _currentFromType;
        private Symbol<StateTrigger> _currentTrigger;
        private TransitionBuilder _transitionBuilder;

        IFsmConfigurator IFsmConfigurator.Entry<TState>()
        {
            _entryStateType = typeof(TState);
            AddStateType(typeof(TState));
            return this;
        }

        IFsmTransitionBuilder IFsmConfigurator.On<TFrom>(Symbol<StateTrigger> trigger)
        {
            _currentFromType = typeof(TFrom);
            _currentTrigger = trigger;
            AddStateType(typeof(TFrom));
            _transitionBuilder ??= new TransitionBuilder(this);
            return _transitionBuilder;
        }

        private void AddStateType(Type type)
        {
            if (!_allStateTypes.Contains(type))
            {
                _allStateTypes.Add(type);
            }
        }

        public FsmRules Build()
        {
            if (_entryStateType == null)
            {
                throw new InvalidOperationException("Entry state must be configured via Entry<TState>().");
            }

            return new FsmRules(_entryStateType, _transitions, _allStateTypes);
        }

        private sealed class TransitionBuilder : IFsmTransitionBuilder
        {
            private readonly FsmConfigurator _owner;

            public TransitionBuilder(FsmConfigurator owner) => _owner = owner;

            IFsmConfigurator IFsmTransitionBuilder.To<TTo>()
            {
                return DoTo<TTo>();
            }

            private IFsmConfigurator DoTo<TTo>() where TTo : StateBase
            {
                var toType = typeof(TTo);
                _owner._transitions[(_owner._currentFromType, _owner._currentTrigger)]
                    = new TransitionRule(toType);
                _owner.AddStateType(toType);
                return _owner;
            }
        }
    }
}
