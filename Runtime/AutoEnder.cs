using System;

namespace HelloWorld {
    public sealed class AutoEnder : IDisposable {
        private readonly IHasScope _scope;

        public AutoEnder(IHasScope scope) {
            _scope = scope;
        }

        public void Dispose() {
            _scope.End();
        }
    }
}