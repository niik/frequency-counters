using System;

namespace freakcode.frequency
{
#if DEBUG
    /// <summary>
    /// An interface for a provider of consistently increasing values for time.
    /// Used for unit testing the counters.
    /// </summary>
    /// <remarks>All methods of implementing classes is guaranteed to be thread safe</remarks>
    interface IMonotonicTimeProvider
    {
        /// <summary>
        /// Gets a  consistently increasing number of elapsed milliseconds since an unspecified
        /// point in time.
        /// </summary>
        /// <remarks>This method should be implemented in a thread safe manner</remarks>
        long Now { get; }
    }
#endif
}
