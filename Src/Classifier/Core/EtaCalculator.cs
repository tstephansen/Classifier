﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ProgressItem = System.Collections.Generic.KeyValuePair<long, float>;

namespace Classifier.Core
{
    public interface IETACalculator
    {
        /// <summary> Clears all collected data.
        /// </summary>
        void Reset();

        /// <summary> Updates the current progress.
        /// </summary>
        /// <param name="progress">The current level of completion.
        /// Must be between 0.0 and 1.0 (inclusively).</param>
        void Update(float progress);

        /// <summary> Returns True when there is enough data to calculate the ETA.
        /// Returns False if the ETA is still calculating.
        /// </summary>
        bool ETAIsAvailable { get; }

        /// <summary> Calculates the Estimated Time of Arrival (Completion)
        /// </summary>
        DateTime ETA { get; }

        /// <summary> Calculates the Estimated Time Remaining.
        /// </summary>
        TimeSpan ETR { get; }
    }

    /// <summary> Calculates the "Estimated Time of Arrival"
    /// (or more accurately, "Estimated Time of Completion"),
    /// based on a "rolling average" of progress over time.
    /// Credit goes to https://github.com/scottrippey/Progression/blob/master/Progression/Extras/ETACalculator.cs for this class.
    /// </summary>
    public class ETACalculator : IETACalculator
    {
        /// <summary>
        /// </summary>
        /// <param name="minimumData">
        /// The minimum number of data points required before ETA can be calculated.
        /// </param>
        /// <param name="maximumDuration">
        /// Determines how many seconds of data will be used to calculate the ETA.
        /// </param>
        public ETACalculator(int minimumData, double maximumDuration)
        {
            this.minimumData = minimumData;
            this.maximumTicks = (long)(maximumDuration * Stopwatch.Frequency);
            this.queue = new Queue<ProgressItem>(minimumData * 2);
            this.timer = Stopwatch.StartNew();
        }

        private int minimumData;
        private long maximumTicks;
        private readonly Stopwatch timer;
        private readonly Queue<ProgressItem> queue;

        private ProgressItem current;
        private ProgressItem oldest;

        public void Reset()
        {
            queue.Clear();

            timer.Reset();
            timer.Start();
        }

        private void ClearExpired()
        {
            var expired = timer.ElapsedTicks - this.maximumTicks;
            while (queue.Count > this.minimumData && queue.Peek().Key < expired)
            {
                this.oldest = queue.Dequeue();
            }
        }

        /// <summary> Adds the current progress to the calculation of ETA.
        /// </summary>
        /// <param name="progress">The current level of completion.
        /// Must be between 0.0 and 1.0 (inclusively).</param>
        public void Update(float progress)
        {
            // If progress hasn't changed, ignore:
            if (this.current.Value == progress)
            {
                return;
            }

            // Clear space for this item:
            ClearExpired();

            // Queue this item:
            long currentTicks = timer.ElapsedTicks;
            this.current = new ProgressItem(currentTicks, progress);
            this.queue.Enqueue(this.current);

            // See if its the first item:
            if (this.queue.Count == 1)
            {
                this.oldest = this.current;
            }
        }

        /// <summary> Calculates the Estimated Time Remaining
        /// </summary>
        public TimeSpan ETR
        {
            get
            {
                // Create local copies of the oldest & current,
                // so that another thread can update them without locking:
                var oldest = this.oldest;
                var current = this.current;

                // Make sure we have enough items:
                if (queue.Count < this.minimumData || oldest.Value == current.Value)
                {
                    return TimeSpan.MaxValue;
                }

                // Calculate the estimated finished time:
                double finishedInTicks = (1.0d - current.Value) * (current.Key - oldest.Key) / (current.Value - oldest.Value);

                return TimeSpan.FromSeconds(finishedInTicks / Stopwatch.Frequency);
            }
        }

        /// <summary> Calculates the Estimated Time of Arrival (Completion)
        /// </summary>
        public DateTime ETA
        {
            get
            {
                return DateTime.Now.Add(ETR);
            }
        }

        /// <summary> Returns True when there is enough data to calculate the ETA.
        /// Returns False if the ETA is still calculating.
        /// </summary>
        public bool ETAIsAvailable
        {
            get
            {
                // Make sure we have enough items:
                return (queue.Count >= this.minimumData && oldest.Value != current.Value);
            }
        }

    }
}
