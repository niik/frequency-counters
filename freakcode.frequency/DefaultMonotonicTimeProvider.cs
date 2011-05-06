using System;

namespace freakcode.frequency
{
    public class DefaultMonotonicTimeProvider : IMonotonicTimeProvider
    {
        public static readonly DefaultMonotonicTimeProvider Instance = new DefaultMonotonicTimeProvider();

        public long Now
        {
            get { return Monotonic.Time(); }
        }
    }
}
