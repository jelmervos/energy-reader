using EnergyReader.Consumer;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace EnergyReader
{
    class ConsumerQueue
    {
        private BlockingCollection<byte[]> queue;
        private Task consumerTask;
        private readonly ITelegramConsumer consumer;
        private readonly ILogger logger;
        private CancellationTokenSource cts;

        public ConsumerQueue(ITelegramConsumer consumer, ILogger logger)
        {
            this.consumer = consumer;
            this.logger = logger;
        }

        public void Start()
        {
            logger.LogInformation("Start");
            cts = new CancellationTokenSource();
            queue = new BlockingCollection<byte[]>();
            var cancelToken = cts.Token;
            StartConsumer(cancelToken);
        }

        private void StartConsumer(CancellationToken cancelToken)
        {
            consumerTask = Task.Factory.StartNew(() => StartConsuming(cancelToken), TaskCreationOptions.LongRunning);
            consumerTask.ContinueWith(t => RestartConsumer(t, cancelToken), TaskContinuationOptions.OnlyOnFaulted);
        }

        private void StartConsuming(CancellationToken cancelToken)
        {
            try
            {
                consumer.StartConsuming(queue, cancelToken);
            }
            catch (OperationCanceledException) { }
        }

        private void RestartConsumer(Task task, CancellationToken cancelToken)
        {
            if (task?.Exception != null)
            {
                logger.LogError(task.Exception, $"Consuming error {consumer.GetType()}");
            }

            if (!cancelToken.IsCancellationRequested)
            {
                StartConsumer(cancelToken);
            }
        }

        public void Stop()
        {
            logger.LogInformation("Stop");
            cts.Cancel();
            queue.CompleteAdding();
            consumerTask.Wait(TimeSpan.FromSeconds(5));
            queue.Dispose();
        }

        public void Enqueue(byte[] data)
        {
            queue.Add(data);
        }
    }
}

