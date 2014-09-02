using System;
using System.Collections.Generic;

namespace Kethane.PartModules
{
    internal class TimedMovingAverage
    {
        private struct TimedValue
        {
            public readonly float Time;
            public readonly float Value;
            public TimedValue(float time, float value)
            {
                Time = time;
                Value = value;
            }
        }

        private readonly Queue<TimedValue> values = new Queue<TimedValue>();
        private readonly float interval;

        public TimedMovingAverage(float interval, float initialValue = 0)
        {
            this.interval = interval;
            values.Enqueue(new TimedValue(interval, initialValue));
        }

        public void Update(float time, float value)
        {
            values.Enqueue(new TimedValue(time, value));
        }

        public float Average
        {
            get
            {
                var time = 0f;
                var value = 0f;
                var removing = values.Count;

                foreach (var entry in values)
                {
                    removing--;
                    if (time + entry.Time > interval)
                    {
                        value += entry.Value * (interval - time);
                        break;
                    }
                    else
                    {
                        time += entry.Time;
                        value += entry.Value * entry.Time;
                    }
                }

                while (removing > 0)
                {
                    removing--;
                    values.Dequeue();
                }

                return value / interval;
            }
        }
    }
}
