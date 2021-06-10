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
        private readonly ITelegramSource source;

        public TelegramMessageBus(ITelegramSource source, IEnumerable<ITelegramConsumer> consumers)
        {
            this.source = source;
            this.consumers = new List<ITelegramConsumer>(consumers);
        }

        public void Start()
        {
            source.NewTelegram += NewTelegram;
            Parallel.ForEach(consumers, t => t.Start());
            source.Start();
        }

        private void NewTelegram(object sender, TelegramEventArgs e)
        {
            Parallel.ForEach(consumers, t => t.Enqueue(e.Data));
        }

        public void Stop()
        {
            source.Stop();
            Parallel.ForEach(consumers, t => t.Stop());
            source.NewTelegram -= NewTelegram;
        }
    }
}
