using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnergyReader.Producer
{
    class UdpSource : ITelegramProducer
    {
        public event EventHandler<TelegramEventArgs> NewTelegram;
        private UdpClient udpClient;
        private const int UdpPort = 9433;
        private readonly ILogger<UdpSource> logger;
        private CancellationTokenSource cts;
        private Task receiveTask;

        public UdpSource(ILogger<UdpSource> logger)
        {
            this.logger = logger;
        }

        public void Start()
        {
            cts = new CancellationTokenSource();

            udpClient = new UdpClient();
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, UdpPort));

            receiveTask = Task.Run(() => Receive(cts.Token));
        }

        private void Receive(CancellationToken cancelToken)
        {
            var from = new IPEndPoint(0, 0);
            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    var buffer = udpClient.Receive(ref from);
                    logger.LogInformation($"Received {buffer.Length} bytes from {from}");
                    NewTelegram?.Invoke(this, new TelegramEventArgs(buffer));
                }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            catch (OperationCanceledException) { }
        }

        public void Stop()
        {
            cts.Cancel();
            udpClient.Dispose();
            receiveTask.Wait(TimeSpan.FromSeconds(5));
        }
    }
}
