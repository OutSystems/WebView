using System;
using System.Threading;

namespace ReactViewControl {

    internal class CallStackBarrier {

        private Action onExit;

        private AutoResetEvent WaitHandle { get; } = new AutoResetEvent(false);

        /// <summary>
        /// Waits until the exit is signaled and executes the on exit action afterwards
        /// </summary>
        public void Exit() {
            WaitHandle.WaitOne();
            onExit?.Invoke();
        }

        /// <summary>
        /// Signals any pending call reaching the exit state, that it should proceed 
        /// </summary>
        /// <param name="onExit"></param>
        public void SignalExit(Action onExit = null) {
            this.onExit = onExit;
            WaitHandle.Set();
        }
    }
}
