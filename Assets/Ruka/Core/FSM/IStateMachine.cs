using R3;
using Ruka.Core.Symbols;

namespace Ruka.Core.FSM
{
    public interface IStateMachine
    {
        ReadOnlyReactiveProperty<StateBase> CurrentState { get; }
        bool Transit(Symbol<StateTrigger> trigger);
    }
}
