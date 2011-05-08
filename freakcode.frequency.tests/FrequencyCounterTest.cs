using System;
using freakcode.frequency;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace freakcode.frequency.tests
{
    [TestClass]
    public class FrequencyCounterTest
    {
        [TestMethod]
        public void CounterShouldRemoveAllNodesWhenEmpty()
        {
            var timeProvider = new TestTimeProvider();

            var counter = new FrequencyCounter(5, 1, timeProvider);
            Assert.AreEqual(0, counter.Value);

            counter.Increment();
            Assert.AreEqual(1, counter.Value);

            timeProvider.Now = 10000;
            Assert.AreEqual(0, counter.Value);
        }

        [TestMethod]
        public void CounterFallofShouldBeExact()
        {
            var timeProvider = new TestTimeProvider();

            var counter = new FrequencyCounter(2, 1, timeProvider);
            Assert.AreEqual(0, counter.Value);

            timeProvider.Now = 0;
            counter.Increment();
            Assert.AreEqual(1, counter.Value);

            timeProvider.Now = 999;
            counter.Increment();
            Assert.AreEqual(2, counter.Value);

            timeProvider.Now = 1000;
            counter.Increment();
            Assert.AreEqual(3, counter.Value);

            timeProvider.Now = 2000;
            counter.Increment();
            Assert.AreEqual(4, counter.Value);

            timeProvider.Now = 2999;
            Assert.AreEqual(4, counter.Value);

            timeProvider.Now = 3000;
            Assert.AreEqual(2, counter.Value);

            timeProvider.Now = 4000;
            Assert.AreEqual(1, counter.Value);

            timeProvider.Now = 4999;
            Assert.AreEqual(1, counter.Value);

            timeProvider.Now = 5000;
            Assert.AreEqual(0, counter.Value);

            counter.Increment();
            Assert.AreEqual(1, counter.Value);

            timeProvider.Now = 8000;
            Assert.AreEqual(0, counter.Value);
        }
    }
}
