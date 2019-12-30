using System;

namespace ReactViewControl {

    internal class SuspendedExecutionEngine : ExecutionEngine {

        public SuspendedExecutionEngine() : base(null, "suspended") { }

        public override T EvaluateMethod<T>(IViewModule module, string functionName, params object[] args) {
            throw new InvalidOperationException("Cannot evaluate javascript on a suspended execution engine");
        }

        public override void ExecuteMethod(IViewModule module, string functionName, params object[] args) {
            throw new InvalidOperationException("Cannot execute javascript on a suspended execution engine");
        }

        public override void Start(string generation) {
            throw new InvalidOperationException("Cannot start a suspended execution engine");
        }
    }
}
