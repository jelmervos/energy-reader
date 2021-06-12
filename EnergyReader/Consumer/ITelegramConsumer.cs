using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyReader.Consumer
{
    interface ITelegramConsumer
    {
        void Start();
        void Enqueue(byte[] data);
        void Stop();
    }
}
