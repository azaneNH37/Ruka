using System;
using System.Collections.Generic;
using Ruka.Core.Symbols;

namespace Ruka.Core.FSM
{
    public sealed class FsmRules
    {
        public Type EntryStateType { get; }
        public IReadOnlyDictionary<(Type FromState, Symbol<StateTrigger> Trigger), TransitionRule> Transitions { get; }
        public IReadOnlyList<Type> AllStateTypes { get; }

        internal FsmRules(
            Type entryStateType,
            Dictionary<(Type, Symbol<StateTrigger>), TransitionRule> transitions,
            List<Type> allStateTypes)
        {
            EntryStateType = entryStateType;
            Transitions = transitions;
            AllStateTypes = allStateTypes;
        }
    }

    public sealed class TransitionRule
    {
        public Type ToState { get; }
        public Func<bool> Guard { get; }

        internal TransitionRule(Type toState, Func<bool> guard = null)
        {
            ToState = toState;
            Guard = guard;
        }
    }
}
