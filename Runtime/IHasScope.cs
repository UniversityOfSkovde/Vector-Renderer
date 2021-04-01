namespace HelloWorld {
    public interface IHasScope {
        
        /**
         * Starts a new scope. For every begin there should be a matching call
         * to End().
         */
        AutoEnder Begin();

        /**
         * Ends an open scope. For every begin there should be a matching call
         * to Begin().
         */
        void End();
    }
}