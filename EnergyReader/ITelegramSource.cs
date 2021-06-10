using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyReader
{
    public class TelegramEventArgs
    {
        public byte[] Data { get; }

        public TelegramEventArgs(byte[] data)
        {
            Data = data;
        }
    }

    interface ITelegramSource
    {
        void Start();
        void Stop();
        event EventHandler<TelegramEventArgs> NewTelegram;
    }
}
