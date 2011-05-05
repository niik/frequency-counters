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
    [DebuggerDisplay("Sample: {TimeValue}: {Count}")]
    internal sealed class FrequencyNode
    {
        public FrequencyNode Next { get; set; }

        public int TimeValue { get; private set; }

        private long _samples;

        public long Samples { get { return Interlocked.Read(ref _samples); } }

        public FrequencyNode(int timeValue)
        {
            this.TimeValue = timeValue;
        }

        public long AddSamples(int count)
        {
            if (count == 0)
                return Samples;

            if (count < 0)
                throw new ArgumentOutOfRangeException("count");

            return Interlocked.Add(ref _samples, count);
        }
    }
}
