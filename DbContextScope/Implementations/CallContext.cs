namespace Zejji.Entity
{
    /// <summary>
    /// A .NET Core implementation of .NET Framework's CallContext class, which
    /// Provides a set of properties that are carried with the execution code path.
    ///
    /// Inspired by https://www.cazzulino.com/callcontext-netstandard-netcore.html
    /// but I think that implementation would leak memory, as the concurrent
    /// dictionary would grow over time.
    ///
    /// See also https://docs.microsoft.com/en-us/dotnet/api/system.threading.asynclocal-1?view=net-6.0
    /// </summary>
    internal static class CallContext
    {
        public static void LogicalSetData(string name, object? data)
        {
            if (state.Value == null)
            {
                state.Value = new Dictionary<string, object?>();
            }
            state.Value[name] = data;
        }

        public static object? LogicalGetData(string name)
        {
            if (state.Value == null)
            {
                return null;
            }

            if (!state.Value.TryGetValue(name, out object? result))
            {
                return null;
            }

            return result;
        }

        private static readonly AsyncLocal<Dictionary<string, object?>> state = new();
    }
}
