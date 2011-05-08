/*
 * Copyright (c) 2008 Markus Olsson 
 * var mail = string.Join(".", new string[] {"j", "markus", "olsson"}) + string.Concat('@', "gmail.com");
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this 
 * software and associated documentation files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use, copy, modify, merge, publish, 
 * distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING 
 * BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Diagnostics;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;

namespace freakcode.frequency
{
    /// <summary>
    /// High-performance memory efficient counter with automatic falloff over time. 
    /// Duration and precision (in seconds) are specified upon initialization and cannot be changed later on.
    /// Can be both read and written to concurrently. Accurately records occurrences independently of system
    /// clock changes such as DST or ntp-corrections.
    /// </summary>
    [DebuggerDisplay("Counter {Duration}/{Precision}: {Value}")]
    public sealed class FrequencyCounter
    {
        /// <summary>
        /// A singly-linked list node representing a specific time value (slice)
        /// </summary>
        [DebuggerDisplay("Sample: {TimeValue}: {Samples}")]
        private sealed class FrequencyNode
        {
            public int TimeValue;
            public long Samples;

            public FrequencyNode Next;
        }

        /// <summary>
        /// Gets or sets the duration of the counter in seconds.
        /// The duration is the amount of time for which this counter
        /// collects samples.
        /// </summary>
        /// <value>The duration in seconds.</value>
        public int Duration { get; private set; }

        /// <summary>
        /// Gets or sets the precision of the counter in seconds.
        /// </summary>
        /// <value>The precision in seconds.</value>
        public int Precision { get; private set; }

        /// <summary>
        /// Actual storage location of current counter value. 
        /// </summary>
        private long counterValue;

        /// <summary>
        /// Gets the total number of counted occurrences over the
        /// duration of the counter.
        /// </summary>
        /// <value>The total number of samples.</value>
        public long Value
        {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            get
            {
                // Fast-path out; if we're at zero there's no
                // nodes available for pruning
                if (Interlocked.Read(ref this.counterValue) == 0)
                    return 0;

                Prune(GetTimeValue());

                return Interlocked.Read(ref this.counterValue);
            }
        }

        /// <summary>
        /// The head node of the singly linked list.
        /// Contains the most current ("newest") node.
        /// </summary>
        private FrequencyNode head;

        /// <summary>
        /// The tail node of the singly linked list.
        /// Contains the oldest node.
        /// </summary>
        private FrequencyNode tail;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrequencyCounter"/> class.
        /// </summary>
        /// <param name="duration">The time period (in seconds) for which the counter will be collecting samples.</param>
        /// <param name="precision">The precision/resolution of the counter in seconds.</param>
        public FrequencyCounter(int duration, int precision)
        {
            if (duration < 1)
                throw new ArgumentOutOfRangeException("duration");

            if (precision < 1)
                throw new ArgumentOutOfRangeException("precision");

            this.Duration = duration;
            this.Precision = precision;
        }

#if DEBUG
        /* Supporting code only used for running unit tests */

        private IMonotonicTimeProvider timeProvider;

        internal FrequencyCounter(int duration, int precision, IMonotonicTimeProvider timeProvider)
            : this(duration, precision)
        {
            this.timeProvider = timeProvider;
        }
#endif

        /// <summary>
        /// Records one sample.
        /// </summary>
        /// <returns>The total sum of all samples in the counter after the sample has been added</returns>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public long Increment()
        {
            return this.IncrementBy(1);
        }

        /// <summary>
        /// Add the specified sample count.
        /// </summary>
        /// <param name="value">
        /// The number of samples to record. Must not be negative. If zero, no action will be performed. Negative values
        /// are not allowed.
        /// </param>
        /// <returns>The total sum of all samples in the counter after the samples has been added</returns>
        /// <exception cref="ArgumentOutOfRangeException">The value was negative</exception>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public long IncrementBy(int value)
        {
            if (value == 0)
                return Value;

            if (value < 1)
                throw new ArgumentOutOfRangeException("value");

            int timeValue = GetTimeValue();

            FrequencyNode currentHead = this.head;

            while (currentHead == null || currentHead.TimeValue < timeValue)
            {
                // The head node is out of date or we're empty so we need to
                // create a new node to take it's place. We'll populate the sample count
                // up-front since we're the only one who's got access to the node
                var newHeadNode = new FrequencyNode { TimeValue = timeValue };

                RuntimeHelpers.PrepareConstrainedRegions();
                try { }
                finally
                {
                    // Try to update the head reference to our newly created node.
                    // If we fail we'll just try again since most likely someone else
                    // managed to update the node before us and we can use that instead.
                    if (Interlocked.CompareExchange(ref this.head, newHeadNode, currentHead) == currentHead)
                    {
                        // Succeeded in setting the head reference.

                        // If we weren't empty before we need to update the previous 
                        // head node so that it references the new head.
                        if (currentHead != null)
                            currentHead.Next = newHeadNode;

                        // If the tail node reference is null we'll update it to point to
                        // our new head node.
                        Interlocked.CompareExchange(ref this.tail, newHeadNode, null);

                        currentHead = newHeadNode;
                    }
                    else
                    {
                        // Prepare for a retry
                        currentHead = this.head;
                    }
                }
            }

            RuntimeHelpers.PrepareConstrainedRegions();
            try { }
            finally
            {
                Interlocked.Add(ref currentHead.Samples, value);
                Interlocked.Add(ref this.counterValue, value);
            }

            Prune(currentHead.TimeValue);
            return Interlocked.Read(ref this.counterValue);
        }

        /// <summary>
        /// Removes all nodes which have expired from the linked list.
        /// </summary>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        private void Prune(int timeValue)
        {
            int expiry = timeValue - Duration;
            long removableSampleCount = 0;

            FrequencyNode currentTail, node;

            do
            {
                currentTail = this.tail;

                if (currentTail == null || currentTail.TimeValue >= expiry)
                    return;

                node = currentTail;

                // Seek to the first node which haven't expired yet and sum
                // all samples seen.
                while (node != null && node.TimeValue < expiry)
                {
                    removableSampleCount += node.Samples;
                    node = node.Next;
                }

                RuntimeHelpers.PrepareConstrainedRegions();
                try { }
                finally
                {
                    // Attempt to update the tail so that it points to the last
                    // valid node. Since the nodes only contain forward references
                    // there's no need to explicitly remove them, they will be garbage
                    // collected eventually
                    if (Interlocked.CompareExchange(ref this.tail, node, currentTail) == currentTail)
                    {
                        currentTail = node;

                        // We won the update race; subtract the sum of all detached nodes
                        Interlocked.Add(ref this.counterValue, -removableSampleCount);
                    }
                }
            } while (currentTail != null && currentTail.TimeValue > expiry);
        }

        /// <summary>
        /// Gets a time value in seconds evenly divisible with the precision of the counter.
        /// For a counter with a 5-second precision the first 15 seconds this will yield 
        /// 3 time distinct time values; 0, 5 and 10.
        /// </summary>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        private int GetTimeValue()
        {
#if DEBUG
            long monoTimeMilliseconds = (timeProvider == null ? Monotonic.Now : timeProvider.Now);
#else
            long monoTimeMilliseconds = Monotonic.Now;
#endif

            int monoTimeSeconds = (int)(monoTimeMilliseconds / 1000);

            return monoTimeSeconds - (monoTimeSeconds % Precision);
        }
    }
}