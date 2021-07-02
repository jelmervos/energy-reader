using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace EnergyReader.Producer
{
    class SerialPortSource : ITelegramProducer
    {
        public event EventHandler<TelegramEventArgs> NewTelegram;
        private SerialPort serialPort;
        private readonly Encoding encoding = Encoding.ASCII;
        private const string LineSeperator = "\r\n";
        private List<string> inputBuffer;
        private readonly ILogger logger;
        private const string PortName = "/dev/ttyUSB0";
        private const int BaudRate = 115200;
        private CancellationTokenSource cts;
        private BlockingCollection<byte[]> queue;
        private Task consumerTask;
        private Task producerTask;

        public SerialPortSource(ILogger<SerialPortSource> logger)
        {
            this.logger = logger;
        }

        public void Start()
        {
            queue = new BlockingCollection<byte[]>();

            serialPort = new SerialPort(PortName, BaudRate)
            {
                Encoding = encoding,
                NewLine = LineSeperator
            };

            inputBuffer = new List<string>();

            serialPort.Open();

            cts = new CancellationTokenSource();
            var cancelToken = cts.Token;
            consumerTask = Task.Factory.StartNew(() => ProcessSerialPortData(cancelToken), cancelToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            producerTask = Task.Factory.StartNew(async () => await ReadFromSerialPort(cancelToken), cancelToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private async Task ReadFromSerialPort(CancellationToken cancelToken)
        {
            var buffer = new byte[1024];
            while (!cancelToken.IsCancellationRequested)
            {
                try
                {
                    var bytesRead = await serialPort.BaseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancelToken);
                    var data = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, data, 0, data.Length);
                    queue.Add(data, cancelToken);
                }
                catch (IOException ex)
                {
                    logger.LogError(ex, "Exception reading from serial port");
                }
                catch (OperationCanceledException) { }
            }
        }

        private void ProcessSerialPortData(CancellationToken cancelToken)
        {
            try
            {
                foreach (var data in queue.GetConsumingEnumerable(cancelToken))
                {
                    inputBuffer.Add(encoding.GetString(data));
                    CheckForTelegrams();
                }
            }
            catch (OperationCanceledException) { }
        }

        private void CheckForTelegrams()
        {
            while (FindTelegramInBuffer(out var telegram))
            {
                NewTelegram?.Invoke(this, new TelegramEventArgs(encoding.GetBytes(telegram)));
            }
        }

        private bool FindTelegramInBuffer(out string telegram)
        {
            var data = string.Join(string.Empty, inputBuffer);

            var startIndex = data.IndexOf('/');
            var crcMatch = Regex.Match(data, $"(![A-Z0-9]{{4}}{LineSeperator})");
            if (startIndex > -1 && crcMatch.Success)
            {
                var endIndex = crcMatch.Index + crcMatch.Length;
                var length = endIndex - startIndex;
                telegram = data.Substring(startIndex, length);

                inputBuffer.Clear();
                var left = data.Remove(0, endIndex);
                if (!string.IsNullOrEmpty(left))
                {
                    inputBuffer.Add(left);
                }

                logger.LogInformation($"Telegram found, size: {telegram.Length}, left in buffer: {inputBuffer.Sum(x => x.Length)}");

                return true;
            }

            telegram = null;
            return false;
        }

        public void Stop()
        {
            logger.LogInformation("Stop");
            queue.CompleteAdding();
            cts.Cancel();
            producerTask.Wait(TimeSpan.FromSeconds(5));
            consumerTask.Wait(TimeSpan.FromSeconds(5));
            serialPort.Close();
            serialPort.Dispose();
            cts.Dispose();
            queue.Dispose();
        }
    }
}
