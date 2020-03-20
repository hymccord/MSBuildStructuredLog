﻿using System;
using System.Diagnostics;
using System.Threading;
using TPLTask = System.Threading.Tasks.Task;

namespace StructuredLogViewer
{
    public delegate object ExecuteSearchFunc(string query, int maxResults, CancellationToken cancellationToken);

    public class TypingConcurrentOperation : IDisposable
    {
        public ExecuteSearchFunc ExecuteSearch;
        public event Action<object, bool> DisplayResults;
        public event Action<string, object, TimeSpan> SearchComplete;

        public const int ThrottlingDelayMilliseconds = 300;

        private readonly Timer timer;
        private string searchText;
        private int maxResults;

        private CancellationTokenSource currentCancellationTokenSource;

        public TypingConcurrentOperation()
        {
            timer = new Timer(OnTimer);
        }

        public void Dispose()
        {
            timer.Dispose();
        }

        public void Reset()
        {
            Interlocked.Exchange(ref currentCancellationTokenSource, null)?.Cancel();
            timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void TextChanged(string searchText, int maxResults)
        {
            if (ExecuteSearch == null)
            {
                Reset();
                return;
            }

            Interlocked.Exchange(ref currentCancellationTokenSource, null)?.Cancel();

            this.searchText = searchText;
            this.maxResults = maxResults;

            timer.Change(ThrottlingDelayMilliseconds, Timeout.Infinite);
        }

        public void TriggerSearch(string searchText, int maxResults)
        {
            Reset();

            this.searchText = searchText;
            this.maxResults = maxResults;

            TPLTask.Run(StartOperation);
        }

        private void OnTimer(object state)
        {
            TPLTask.Run(StartOperation);
        }

        private void StartOperation()
        {
            var cts = new CancellationTokenSource();
            Interlocked.Exchange(ref currentCancellationTokenSource, cts)?.Cancel();

            var localSearchText = searchText;
            var localMaxResults = maxResults;

            var sw = Stopwatch.StartNew();
            var results = ExecuteSearch(localSearchText, localMaxResults, cts.Token);
            var elapsed = sw.Elapsed;

            var moreAvailable = results is System.Collections.ICollection collection && collection.Count >= localMaxResults;

            if (!cts.Token.IsCancellationRequested)
            {
                DisplayResults?.Invoke(results, moreAvailable);
                SearchComplete?.Invoke(localSearchText, results, elapsed);
            }
        }
    }
}
