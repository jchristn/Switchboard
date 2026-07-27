namespace Test.Shared
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Minimal assertion helpers for test descriptors. Failures are expressed by throwing, which
    /// the Touchstone executor records as a failed test. This type never writes to the console.
    /// </summary>
    public static class Check
    {
        /// <summary>
        /// Assert a condition is true.
        /// </summary>
        /// <param name="condition">Condition that must hold.</param>
        /// <param name="message">Failure message.</param>
        public static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        /// <summary>
        /// Assert a condition is false.
        /// </summary>
        /// <param name="condition">Condition that must not hold.</param>
        /// <param name="message">Failure message.</param>
        public static void False(bool condition, string message)
        {
            if (condition) throw new InvalidOperationException(message);
        }

        /// <summary>
        /// Assert two values are equal.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="expected">Expected value.</param>
        /// <param name="actual">Actual value.</param>
        /// <param name="label">Context label included in the failure message.</param>
        public static void Equal<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException(label + ": expected [" + expected + "] but got [" + actual + "]");
        }

        /// <summary>
        /// Assert a string contains a substring.
        /// </summary>
        /// <param name="haystack">String to search.</param>
        /// <param name="needle">Required substring.</param>
        /// <param name="label">Context label included in the failure message.</param>
        public static void Contains(string haystack, string needle, string label)
        {
            if (haystack == null || !haystack.Contains(needle))
                throw new InvalidOperationException(label + ": expected to contain [" + needle + "]");
        }

        /// <summary>
        /// Assert that an action throws an exception of a given type.
        /// </summary>
        /// <typeparam name="TException">Expected exception type.</typeparam>
        /// <param name="action">Action expected to throw.</param>
        /// <param name="label">Context label included in the failure message.</param>
        public static void Throws<TException>(Action action, string label)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(label + ": expected " + typeof(TException).Name + " but got " + ex.GetType().Name);
            }

            throw new InvalidOperationException(label + ": expected " + typeof(TException).Name + " but nothing was thrown");
        }

        /// <summary>
        /// Assert that an async action throws an exception of a given type.
        /// </summary>
        /// <typeparam name="TException">Expected exception type.</typeparam>
        /// <param name="action">Async action expected to throw.</param>
        /// <param name="label">Context label included in the failure message.</param>
        /// <returns>Task.</returns>
        public static async Task ThrowsAsync<TException>(Func<Task> action, string label)
            where TException : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException)
            {
                return;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(label + ": expected " + typeof(TException).Name + " but got " + ex.GetType().Name);
            }

            throw new InvalidOperationException(label + ": expected " + typeof(TException).Name + " but nothing was thrown");
        }
    }
}
