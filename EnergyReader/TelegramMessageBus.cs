using EnergyReader.Consumer;
using EnergyReader.Producer;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyReader
{
    class TelegramMessageBus
    {
        private readonly List<ConsumerQueue> consumerQueues;
        private readonly ITelegramProducer producer;

        public TelegramMessageBus(ILoggerFactory logger, ITelegramProducer producer, IEnumerable<ITelegramConsumer> consumers)
        {
            this.producer = producer;
            consumerQueues = new List<ConsumerQueue>(consumers.Select(c => new ConsumerQueue(c, logger.CreateLogger(c.GetType().Name))));
        }

        public void Start()
        {
            Parallel.ForEach(consumerQueues, c => c.Start());
            producer.NewTelegram += NewTelegram;
            producer.Start();
        }

        private void NewTelegram(object sender, TelegramEventArgs e)
        {
            Parallel.ForEach(consumerQueues, c => c.Enqueue(e.Data));
        }

        public void Stop()
        {
            producer.Stop();
            producer.NewTelegram -= NewTelegram;
            Parallel.ForEach(consumerQueues, t => t.Stop());
        }
    }
}
