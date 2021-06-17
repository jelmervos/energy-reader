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
        private readonly ILogger<FileWriter> logger;
        private const string DestinationFileName = "telegram.txt";

        public FileWriter(ILogger<FileWriter> logger)
        {
            this.logger = logger;
        }

        public async Task StartConsumingAsync(BlockingCollection<byte[]> queue, CancellationToken cancelToken)
        {
            foreach (var data in queue.GetConsumingEnumerable(cancelToken))
            {
                var fileName = Path.Combine(AppContext.BaseDirectory, DestinationFileName);
                using (var fileStream = File.Open(fileName, FileMode.Create))
                {
                    fileStream.Seek(0, SeekOrigin.End);
                    await fileStream.WriteAsync(data.AsMemory(0, data.Length), cancelToken);
                }
                logger.LogInformation($"Written {data.Length} bytes to {fileName}");
            }
        }
    }
}
