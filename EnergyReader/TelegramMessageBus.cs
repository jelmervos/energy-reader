using EnergyReader.Consumer;
using EnergyReader.Producer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyReader
{
    class TelegramMessageBus
    {
        private readonly List<ITelegramConsumer> consumers;
        private readonly ITelegramProducer producer;

        public TelegramMessageBus(ITelegramProducer producer, IEnumerable<ITelegramConsumer> consumers)
        {
            this.producer = producer;
            this.consumers = new List<ITelegramConsumer>(consumers);
        }

        public void Start()
        {
            producer.NewTelegram += NewTelegram;
            Parallel.ForEach(consumers, t => t.Start());
            producer.Start();
        }

        private void NewTelegram(object sender, TelegramEventArgs e)
        {
            Parallel.ForEach(consumers, t => t.Enqueue(e.Data));
        }

        public void Stop()
        {
            producer.Stop();
            Parallel.ForEach(consumers, t => t.Stop());
            producer.NewTelegram -= NewTelegram;
        }
    }
}
