using System;
using System.Collections.Generic;
using System.Diagnostics;
using ProgressItem = System.Collections.Generic.KeyValuePair<long, float>;

namespace Classifier.Core
{
    /// <summary>
    /// Credit goes to https://github.com/scottrippey/Progression/blob/master/Progression/Extras/ETACalculator.cs for this class.
    /// All I did was change the code style to suit this project.
    /// </summary>
    public interface IEtaCalculator
    {
        /// <summary>
        /// Clears all collected data.
        /// </summary>
        void Reset();

        /// <summary>
        /// Updates the current progress.
        /// </summary>
        /// <param name="progress">The current level of completion.
        /// Must be between 0.0 and 1.0 (inclusively).</param>
        void Update(float progress);

        /// <summary>
        /// Returns True when there is enough data to calculate the ETA.
        /// Returns False if the ETA is still calculating.
        /// </summary>
        bool ETAIsAvailable { get; }

        /// <summary>
        /// Calculates the Estimated Time of Arrival (Completion)
        /// </summary>
        DateTime ETA { get; }

        /// <summary>
        /// Calculates the Estimated Time Remaining.
        /// </summary>
        TimeSpan ETR { get; }
    }

    /// <summary>
    /// Calculates the "Estimated Time of Arrival"
    /// (or more accurately, "Estimated Time of Completion"),
    /// based on a "rolling average" of progress over time.
    /// Credit goes to https://github.com/scottrippey/Progression/blob/master/Progression/Extras/ETACalculator.cs for this class.
    /// All I did was change the code style to suit this project.
    /// </summary>
    public class EtaCalculator : IEtaCalculator
    {
        /// <summary>
        /// </summary>
        /// <param name="minimumData">
        /// The minimum number of data points required before ETA can be calculated.
        /// </param>
        /// <param name="maximumDuration">
        /// Determines how many seconds of data will be used to calculate the ETA.
        /// </param>
        public EtaCalculator(int minimumData, double maximumDuration)
        {
            _minimumData = minimumData;
            _maximumData = (long)(maximumDuration * Stopwatch.Frequency);
            _queue = new Queue<ProgressItem>(minimumData * 2);
            _timer = Stopwatch.StartNew();
        }

        private readonly int _minimumData;
        private readonly long _maximumData;
        private readonly Stopwatch _timer;
        private readonly Queue<ProgressItem> _queue;

        private ProgressItem _current;
        private ProgressItem _oldest;

        public void Reset()
        {
            _queue.Clear();

            _timer.Reset();
            _timer.Start();
        }

        private void ClearExpired()
        {
            var expired = _timer.ElapsedTicks - _maximumData;
            while (_queue.Count > _minimumData && _queue.Peek().Key < expired)
            {
                _oldest = _queue.Dequeue();
            }
        }

        /// <summary> Adds the current progress to the calculation of ETA.
        /// </summary>
        /// <param name="progress">The current level of completion.
        /// Must be between 0.0 and 1.0 (inclusively).</param>
        public void Update(float progress)
        {
            // If progress hasn't changed, ignore:
            if (_current.Value == progress)
            {
                return;
            }

            // Clear space for this item:
            ClearExpired();

            // Queue this item:
            var currentTicks = _timer.ElapsedTicks;
            _current = new ProgressItem(currentTicks, progress);
            _queue.Enqueue(_current);

            // See if its the first item:
            if (_queue.Count == 1)
            {
                _oldest = _current;
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
                var oldest = _oldest;
                var current = _current;
                // Make sure we have enough items:
                if (_queue.Count < _minimumData || oldest.Value == current.Value)
                {
                    return TimeSpan.MaxValue;
                }
                // Calculate the estimated finished time:
                var finishedInTicks = (1.0d - current.Value) * (current.Key - oldest.Key) / (current.Value - oldest.Value);
                return TimeSpan.FromSeconds(finishedInTicks / Stopwatch.Frequency);
            }
        }

        /// <summary>
        /// Calculates the Estimated Time of Arrival (Completion)
        /// </summary>
        public DateTime ETA => DateTime.Now.Add(ETR);

        /// <summary>
        /// Returns True when there is enough data to calculate the ETA.
        /// Returns False if the ETA is still calculating.
        /// </summary>
        public bool ETAIsAvailable => (_queue.Count >= _minimumData && _oldest.Value != _current.Value);
    }
}
