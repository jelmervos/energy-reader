using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnergyReader.Consumer
{
    class UdpBroadcaster : ITelegramConsumer
    {
        private BlockingCollection<byte[]> inputQueue;
        private CancellationTokenSource cts;
        private Task consumerTask;
        private const int UdpPort = 9433;
        private readonly ILogger logger;

        public UdpBroadcaster(ILogger<UdpBroadcaster> logger)
        {
            this.logger = logger;
        }

        public void Enqueue(byte[] data)
        {
            inputQueue.Add(data);
        }

        private void Consume(CancellationToken ct)
        {
            using var udpClient = new UdpClient();
            var broadcastIpAddress = IPAddress.Broadcast.ToString();

            try
            {
                foreach (var data in inputQueue.GetConsumingEnumerable(ct))
                {
                    udpClient.Send(data, data.Length, broadcastIpAddress, UdpPort);
                    logger.LogInformation($"{DateTimeOffset.Now} Broadcasted data, size: {data.Length}, port: {UdpPort}");
                }
            }
            catch (OperationCanceledException) { }
        }

        public void Start()
        {
            inputQueue = new BlockingCollection<byte[]>();
            cts = new CancellationTokenSource();
            var cancelToken = cts.Token;
            StartConsumer(cancelToken);
        }

        private void StartConsumer(CancellationToken cancelToken)
        {
            consumerTask = Task.Factory.StartNew(() => Consume(cancelToken), TaskCreationOptions.LongRunning);
            consumerTask.ContinueWith(t => RestartConsumer(t, cancelToken), TaskContinuationOptions.OnlyOnFaulted);
        }

        private void RestartConsumer(Task task, CancellationToken cancelToken)
        {
            if (task?.Exception != null)
            {
                Console.Error.WriteAsync(task.Exception.ToString());
            }

            if (!cancelToken.IsCancellationRequested)
            {
                StartConsumer(cancelToken);
            }
        }

        public void Stop()
        {
            inputQueue.CompleteAdding();
            cts.Cancel();
            consumerTask.Wait(TimeSpan.FromSeconds(5));
            cts.Dispose();
        }
    }
}
