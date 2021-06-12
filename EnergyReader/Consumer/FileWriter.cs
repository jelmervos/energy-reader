using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnergyReader.Consumer
{
    class FileWriter : ITelegramConsumer
    {
        private BlockingCollection<byte[]> inputQueue;
        private CancellationTokenSource cts;
        private Task consumerTask;
        private readonly ILogger<FileWriter> logger;
        private const string DestinationFileName = "telegram.txt";

        public FileWriter(ILogger<FileWriter> logger)
        {
            this.logger = logger;
        }

        private void Consume(CancellationToken ct)
        {
            var fileName = Path.Combine(AppContext.BaseDirectory, DestinationFileName);
            while (!ct.IsCancellationRequested)
            {
                if (inputQueue.TryTake(out var data, 100, ct))
                {
                    File.WriteAllBytes(fileName, data);
                    logger.LogInformation($"Written {data.Length} bytes to {fileName}");
                }
            }
        }

        public void Start()
        {
            inputQueue = new BlockingCollection<byte[]>();
            cts = new CancellationTokenSource();
            var cancelToken = cts.Token;
            consumerTask = Task.Factory.StartNew(() => Consume(cancelToken), TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            inputQueue.CompleteAdding();
            cts.Cancel();
            consumerTask.Wait(TimeSpan.FromSeconds(5));
            inputQueue.Dispose();
        }

        public void Enqueue(byte[] data)
        {
            inputQueue.Add(data);
        }
    }
}
