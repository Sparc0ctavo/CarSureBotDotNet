using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarSureBot
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            CarSureBot carSureBot = new CarSureBot();



            await carSureBot.StartAsync();
        }
    }
}
