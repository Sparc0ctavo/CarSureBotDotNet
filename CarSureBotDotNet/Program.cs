using System.Text;
using DotNetEnv;
using PdfSharp.Fonts;

namespace CarSureBotDotNet
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            
            Env.Load("C:\\Users\\malya\\source\\repos\\CarSureBotDotNet\\CarSureBotDotNet\\.env");

            GlobalFontSettings.FontResolver = new CustomFontResolver();
            CarSureBot carSureBot = new CarSureBot();

            await carSureBot.StartAsync();

            return;
        }
    }
}
