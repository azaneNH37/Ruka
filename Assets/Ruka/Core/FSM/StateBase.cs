namespace Ruka.Core.FSM
{
    public abstract class StateBase
    {
        protected IStateMachine Machine { get; private set; }

        internal void SetMachine(IStateMachine machine) => Machine = machine;

        public virtual void OnStateEnter() { }
        public virtual void OnStateUpdate() { }
        public virtual void OnStateExit() { }
    }
}
