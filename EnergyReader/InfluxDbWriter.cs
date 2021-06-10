﻿using DSMRParser;
using DSMRParser.Models;
using InfluxDB.LineProtocol.Client;
using InfluxDB.LineProtocol.Payload;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EnergyReader
{
    class InfluxDbWriter : ITelegramConsumer
    {
        private BlockingCollection<byte[]> inputQueue;
        private CancellationTokenSource cts;
        private Task consumerTask;
        private int writeTasksRunning;
        private DateTimeOffset lastWrite;
        private readonly ILogger<InfluxDbWriter> logger;
        private readonly TimeSpan writeEvery = TimeSpan.FromMinutes(1);
        private const string Uri = "http://raspberrypi.:8086";
        private const string DatabaseName = "energy";

        public InfluxDbWriter(ILogger<InfluxDbWriter> logger)
        {
            writeTasksRunning = 0;
            lastWrite = DateTimeOffset.MinValue;
            this.logger = logger;
        }

        private void Consume(CancellationToken ct)
        {
            var telegrams = new List<Telegram>();
            var parser = new DSMRTelegramParser();

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (inputQueue.TryTake(out var data, 100, ct))
                    {
                        var telegram = parser.Parse(new Span<byte>(data));
                        if (telegram.TimeStamp != null)
                        {
                            telegrams.Add(telegram);
                        }
                    }
                    CheckForWritableTelegrams(telegrams);
                }
                catch (OperationCanceledException) { }
            }
        }

        private void CheckForWritableTelegrams(List<Telegram> telegrams)
        {
            var now = DateTimeOffset.Now;
            var lastWriteAge = now - lastWrite;
            if ((telegrams.Count > 0) && (lastWriteAge >= writeEvery))
            {
                var batch = telegrams.Where(t => t.TimeStamp >= lastWrite);
                if (batch.Any())
                {
                    Interlocked.Increment(ref writeTasksRunning);
                    if (!cts.IsCancellationRequested)
                    {
                        var list = batch.ToList();
                        Task
                            .Run(async () => await WriteTelegrams(list))
                            .ContinueWith((task) => Interlocked.Decrement(ref writeTasksRunning));
                    }
                    else
                    {
                        Interlocked.Decrement(ref writeTasksRunning);
                    }

                    telegrams.RemoveAll(t => batch.Contains(t));
                }
                lastWrite = now;
            }
        }

        private async Task WriteTelegrams(List<Telegram> telegrams)
        {
            logger.LogInformation($"Write {telegrams.Count} telegrams");

            LineProtocolPayload payload;

            payload = new LineProtocolPayload();
            payload.Add(GetElectricityPoint(telegrams));
            payload.Add(GetGasPoint(telegrams));

            var client = new LineProtocolClient(new Uri(Uri), DatabaseName);

            var writeResult = await client.WriteAsync(payload);
            logger.LogInformation($"WriteResult: {writeResult.Success} {writeResult.ErrorMessage}");
        }

        private static LineProtocolPoint GetGasPoint(List<Telegram> telegrams)
        {
            var timeStamp = telegrams.Max(t => t.GasDelivered.DateTime.Value);
            var delivered = telegrams.Max(t => t.GasDelivered.Value.Value);

            return new LineProtocolPoint(
                "gas", //Measurement
                new Dictionary<string, object> //Fields
                {
                    { "delivered", delivered },
                },
                new Dictionary<string, string> { }, //Tags
                timeStamp.UtcDateTime); //Timestamp
        }

        private static LineProtocolPoint GetElectricityPoint(List<Telegram> telegrams)
        {
            var timeStamp = telegrams.Max(t => t.TimeStamp.Value);
            var deliveredTariff1 = telegrams.Max(t => t.EnergyDeliveredTariff1.Value);
            var deliveredTariff2 = telegrams.Max(t => t.EnergyDeliveredTariff2.Value);
            var returnedTariff1 = telegrams.Max(t => t.EnergyReturnedTariff1.Value);
            var returnedTariff2 = telegrams.Max(t => t.EnergyReturnedTariff2.Value);
            var voltageL1 = telegrams.Average(t => t.VoltageL1.Value);

            return new LineProtocolPoint(
                "electricity", //Measurement
                new Dictionary<string, object> //Fields
                {
                    { "DeliveredTariff1", deliveredTariff1 },
                    { "DeliveredTariff2", deliveredTariff2 },
                    { "ReturnedTariff1", returnedTariff1 },
                    { "ReturnedTariff2", returnedTariff2 },
                    { "VoltageL1", voltageL1 },
                },
                new Dictionary<string, string> { }, //Tags
                timeStamp.UtcDateTime); //Timestamp
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
            SpinWait.SpinUntil(() => Interlocked.CompareExchange(ref writeTasksRunning, 0, 0) == 0, TimeSpan.FromSeconds(5));
            consumerTask.Wait(TimeSpan.FromSeconds(5));
            inputQueue.Dispose();
        }

        public void Enqueue(byte[] data)
        {
            inputQueue.Add(data);
        }
    }
}
