using System.Text;
using DotNetEnv;

namespace CarSureBotDotNet
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Env.Load();

            CarSureBot carSureBot = new CarSureBot();

            await carSureBot.StartAsync();
        }
    }
}
