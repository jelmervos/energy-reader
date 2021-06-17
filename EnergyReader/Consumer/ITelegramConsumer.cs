using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnergyReader.Consumer
{
    interface ITelegramConsumer
    {
        Task StartConsumingAsync(BlockingCollection<byte[]> queue, CancellationToken cancelToken);
    }
}
