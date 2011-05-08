using System;
using System.Diagnostics.CodeAnalysis;

namespace freakcode.frequency
{
    public class DefaultMonotonicTimeProvider : IMonotonicTimeProvider
    {
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "DefaultMonotonicTimeProvider is immutable")]
        public static readonly DefaultMonotonicTimeProvider Instance = new DefaultMonotonicTimeProvider();

        public long Now
        {
            get { return Monotonic.Time(); }
        }
    }
}
