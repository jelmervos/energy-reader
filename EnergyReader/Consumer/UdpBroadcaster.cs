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
        private const int UdpPort = 9433;
        private readonly ILogger logger;

        public UdpBroadcaster(ILogger<UdpBroadcaster> logger)
        {
            this.logger = logger;
        }

        public async Task StartConsumingAsync(BlockingCollection<byte[]> queue, CancellationToken cancelToken)
        {
            using var udpClient = new UdpClient();
            var broadcastIpAddress = IPAddress.Broadcast.ToString();

            foreach (var data in queue.GetConsumingEnumerable(cancelToken))
            {
                await udpClient.SendAsync(data, data.Length, broadcastIpAddress, UdpPort);
                logger.LogInformation($"Broadcasted data, size: {data.Length}, port: {UdpPort}");
            }
        }
    }
}
