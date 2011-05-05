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

namespace freakcode.frequency
{
    /// <summary>
    /// High-performance memory efficient counter with automatic falloff over time. 
    /// Duration and precision (in seconds) are specified upon initialization and cannot be changed later on.
    /// Can be both read and written to concurrently. Accurately records occurrences independently of system
    /// clock changes such as DST or ntp-corrections.
    /// </summary>
    public sealed class FrequencyCounter
    {
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
        private long _value;

        /// <summary>
        /// Gets the total number of counted occurrences over the
        /// duration of the counter.
        /// </summary>
        /// <value>The total number of samples.</value>
        public long Value
        {
            get
            {
                if (Interlocked.Read(ref _value) == 0)
                    return 0;

                return VerifyHead(GetTimeValue());
            }
        }

        /// <summary>
        /// Gets the number of currently active data points within this counter
        /// </summary>
        /// <value>The number of internal nodes used to track and represent the value of the counter.</value>
        public int DataPointCount { get; private set; }

        private FrequencyNode head;
        private FrequencyNode tail;

        private readonly object listSyncObj = new object();

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

        /// <summary>
        /// Records one sample.
        /// </summary>
        /// <returns>The total sum of all samples in the counter after the sample has been added</returns>
        public long Record()
        {
            return Record(1);
        }

        /// <summary>
        /// Records the specified sample count.
        /// </summary>
        /// <param name="sampleCount">The number of samples to record. Must not be negative. If sampleCount is zero no action will be performed.</param>
        /// <returns>The total sum of all samples in the counter after the samples has been added</returns>
        public long Record(int sampleCount)
        {
            if (sampleCount == 0)
                return Value;

            if (sampleCount < 1)
                throw new ArgumentOutOfRangeException("count");

            int timeValue = GetTimeValue();
            var tailNode = this.tail;

            if (tailNode == null || tailNode.TimeValue != timeValue)
            {
                lock (listSyncObj)
                {
                    Debug.WriteLine("Add acquired lock; moving head");

                    tailNode = this.tail;

                    if (tailNode == null || tailNode.TimeValue != timeValue)
                    {
                        var newNode = new FrequencyNode(timeValue);

                        if (tailNode == null)
                        {
                            // This is the one and only item in the list
                            this.head = newNode;
                            this.tail = newNode;
                        }
                        else
                        {
                            this.tail = newNode;
                            tailNode.Next = newNode;
                        }

                        tailNode = newNode;
                        this.DataPointCount++;
                    }
                }
            }

            tailNode.AddSamples(sampleCount);
            Interlocked.Add(ref _value, sampleCount);

            return VerifyHead(timeValue);
        }

        private long VerifyHead(int timeValue)
        {
            FrequencyNode node = this.head;

            if (node == null)
                return 0;

            long sum = 0;
            int removedNodes = 0;

            int expiry = timeValue - Duration;

            while (node != null && node.TimeValue < expiry)
            {
                sum += node.Samples;
                removedNodes++;

                node = node.Next;
            }

            if (node != head)
            {
                // Something has changed

                lock (listSyncObj)
                {
                    if (node == null)
                    {
                        // None of the nodes where in range. We've purged all nodes;
                        Debug.WriteLine("VerifyHead acquired lock; no items left");

                        //Debug.Assert(this._sampleCount == removedSamples);

                        this.head = null;
                        this.tail = null;

                        Interlocked.Exchange(ref this._value, 0);

                        return 0;
                    }
                    else
                    {
                        // We've purged one or more nodes

                        Debug.WriteLine("VerifyHead acquired lock; moving head");

                        //Debug.Assert(_sampleCount >= removedSamples);

                        this.head = node;

                        this.DataPointCount -= removedNodes;

                        return Interlocked.Add(ref _value, -sum);
                    }
                }
            }

            // Nothing has changed; return the total value
            return Interlocked.Read(ref _value);
        }

        private int GetTimeValue()
        {
            long monoTimeMilliseconds = Monotonic.Now;
            long monoTimeSeconds = monoTimeMilliseconds / 1000;

            return (int)(monoTimeSeconds - (monoTimeSeconds % Precision));
        }
    }
}
