# Frequency Counters #

Automatically decreasing high performance thread safe counters. Perfect for rate throttling.

Frequency counters are defined by two parameters, the duration and the precision. The duration is the time period (in seconds) for which the counter will be collecting samples and the precision controls what level of accuracy you require. For example, a counter tracking occurrences over 24 hours or even a week is most likely not in need of greater precision than 10 or 15 minute intervals while a 60 second request throttling timer might need as much as one second precision for exact counting.

## Performance

Performance counters are

- Thread safe
- High performance
- Lock free

Counters are internally made up of a singly linked list with nodes containing the number of samples for each time slice in where the maximum number of nodes is the duration divided by the precision. So a counter which tracks occurences over a 60 second time window with 5 second precision will use **at most** 12 nodes. The use of the linked list ensures that even counters which are only incremented once or twice still maintain a low memory footprint.

Tracking occurrences over time is more tricky than one might think at first glance but when you take into account that you can't rely on the clock over long periods of time (either due manual clock modifications or automatic DST changes) it gets trickier. Frequency Counters attempt to solve this by relying on a monotonically increasing time value provided by the Monotonic class.

Performance counters avoid locks completely, synchronizing using atomic add and compare-exchange operations provided by the Interlocked class.

## Usage

Imagine some internal service or web application with a status page or monitoring interface. With frequency counters it's very easy and to maintain a set of rolling counters to give an at-a-glance perspective of usage over time.
    
	FrequencyCounter[] RequestCounters = new FrequencyCounter[] {
		new FrequencyCounter(60, 10),
		new FrequencyCounter(300, 30),
		new FrequencyCounter(600, 60),
		new FrequencyCounter(3600, 300),
		new FrequencyCounter(86400, 600),
	};

With each request/action or whatever it is you'd like to sample you just increment each of the counters. All counter operations are thread safe and **lock free** using only Interlocked atomic operations. Tallying up and showing developers or systems engineers the current usage of the app is a simple as iterating over the counters and asking for their value.


### Throttle sensitive web actions

    public class LoginController : Controller
    {
        private static FrequencyCounter loginThrottleShort = new FrequencyCounter(10, 1);
        private static FrequencyCounter loginThrottleLong = new FrequencyCounter(60, 5);

        [HttpPost]
        public ActionResult Login(string username, string password)
        {
            // If we've seen more than 20 login attempts the last 10 seconds or more than 80
            // during the last minute we'll opt for causion and deny requests until we drop
            // below the limits
            var shortCounter = loginThrottleShort.Increment();
            var longCounter = loginThrottleLong.Increment();
            
            if (shortCounter > 20 || longCounter > 80)
            {
                return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Request throttled");
            }

            // Continue with normal login procedure
        }
    }

## LICENSE
Frequency Counters is licensed under the [MIT License](https://github.com/markus-olsson/frequency-counters/blob/master/LICENSE.txt) ([OSI](http://www.opensource.org/licenses/mit-license.php)). Basically you're free to do whatever you want with it. Attribution not necessary but appreciated.
