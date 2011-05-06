using System;

namespace freakcode.frequency.tests
{
    public class TestTimeProvider: IMonotonicTimeProvider
    {
        public long Now { get; set; }
    }
}
