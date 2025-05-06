using OpenAI.Chat;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CarSureBotDotNet
{
    internal class CarSureBot
    {

        private bool _isRunning;
        internal List<UserSession> _sessions;
        private TelegramBotClient _botClient;
        private MindeeService _mindeeService;
        private Dictionary<string, string> _keyPrompts;
        private CancellationTokenSource _cts;
        private string _bot_token;



        public CarSureBot()
        {
            _isRunning = true;
            _bot_token = Environment.GetEnvironmentVariable("CARSURE_BOT_TOKEN");
            _cts = new CancellationTokenSource();
            _botClient = new TelegramBotClient(_bot_token, cancellationToken: _cts.Token);
            _mindeeService = new MindeeService();
            _sessions = new List<UserSession>();
        }

        public async Task StartAsync()
        {
            Console.CancelKeyPress += OnCanselKeyPress;
            _botClient.OnUpdate += OnUpdate;
            _botClient.OnError += OnError;
            _keyPrompts = SetKeyPrompts();

            Console.WriteLine("Receiving has been started! Press CTRL + C to stop receiving.");

            while (_isRunning) { await Task.Delay(1000); }

            await StopAsync();
        }

        public async Task StopAsync()
        {
            _cts.Cancel();
        }

        private async Task OnUpdate(Update update)
        {

            if (update.Type == UpdateType.Message)
            {

                UserSession currentSession; //User's session instance


                //validates if such user's session exists(if not creates new one)
                if (_sessions.Count == 0 || !_sessions.Any(x => x.ChatId == update.Message.Chat.Id))
                {
                    _sessions.Add(new UserSession(update.Message.Chat.Id));
                    currentSession = _sessions.Last();
                    await currentSession.openAiApi.SetBehaviorPattern();
                }
                else
                {
                    currentSession = _sessions.Find(x => x.ChatId == update.Message.Chat.Id);
                }


                if (update.Message.Type == MessageType.Text)
                {
                    await UpdateTextMessageHandler(update.Message, currentSession);
                }
                else if (update.Message.Type == MessageType.Photo)
                {
                    await UpdatePhotoMessageHandler(update.Message, currentSession);
                }
            }

        }

        private async Task OnError(Exception exception, HandleErrorSource errorSource)
        {
            Console.WriteLine(exception.ToString());
        }

        private async Task UpdateTextMessageHandler(Message message, UserSession currentSession)
        {
            Console.WriteLine($"A text message \"{message.Text}\" recieved from {message.Chat.Username}");

            string prompt = message.Text;
            string gptResponse = prompt;

            try
            {

                if (currentSession.keyStepOrder == 0 && message.Text.StartsWith("/start"))
                {
                    prompt = _keyPrompts["language"];

                    currentSession.keyStepOrder++;
                }
                else if (currentSession.keyStepOrder == 1)
                {

                    string languageStr = _keyPrompts["step1"].Split(' ')[4];

                    _keyPrompts["step1"] = _keyPrompts["step1"].Replace(languageStr, prompt);
                    gptResponse = await OpenAiResponseAsync(currentSession, _keyPrompts["step1"]);
                    await _botClient.SendMessage(currentSession.ChatId, gptResponse);
                    prompt = _keyPrompts["step2"];

                    currentSession.keyStepOrder++;
                }
                else if (currentSession.keyStepOrder == 5 || currentSession.keyStepOrder == 6)
                {

                    gptResponse = await OpenAiResponseAsync(currentSession, prompt);
                    string responseStatusStr = gptResponse.Split()[0];

                    Console.WriteLine("Chat GPT response: " + gptResponse);

                    gptResponse = gptResponse.Substring(gptResponse.IndexOf(' ') + 1);


                    if (responseStatusStr != "null")
                    {
                        bool responseStatus = bool.Parse(responseStatusStr);


                        if (responseStatus)
                        {
                            if (currentSession.keyStepOrder == 5)
                            {
                                currentSession.keyStepOrder++;

                                prompt = _keyPrompts["step4"];
                            }
                            else
                            {
                                prompt = _keyPrompts["step6"];
                                gptResponse = await OpenAiResponseAsync(currentSession, prompt);
                                await _botClient.SendMessage(currentSession.ChatId, gptResponse);

                                await GenerateAndSendPDF(currentSession);

                                prompt = _keyPrompts["step7"];
                                gptResponse = await OpenAiResponseAsync(currentSession, prompt);
                                await _botClient.SendMessage(currentSession.ChatId, gptResponse);

                                currentSession.keyStepOrder = 0;

                                return;
                            }

                        }
                        else
                        {
                            if (currentSession.keyStepOrder == 5)
                            {
                                currentSession.keyStepOrder -= 3;

                                prompt = "ask to send documents again";
                            }
                        }
                    }
                    else
                    {
                        await _botClient.SendMessage(currentSession.ChatId, gptResponse);

                        return;
                    }


                }


                gptResponse = await OpenAiResponseAsync(currentSession, prompt);
                await _botClient.SendMessage(currentSession.ChatId, gptResponse);

            }
            catch (Exception ex) { Console.WriteLine("Text message response: " + ex.ToString()); }
        }

        private async Task UpdatePhotoMessageHandler(Message message, UserSession currentSession)
        {

            Console.WriteLine($"A photo message recieved from {message.Chat.Username}");
            try
            {
                if (currentSession.keyStepOrder == 2 || currentSession.keyStepOrder == 3 || currentSession.keyStepOrder == 4)
                {

                    var photoBytes = await ExtractPhoto(message.Photo.Last());
                    string mindeeResponseText = await _mindeeService.GetTextData(photoBytes, currentSession.keyStepOrder);
                    string gptResponse = null;

                    //saving read document fields for future policy generation
                    var chatIdKey = currentSession.ChatId;
                    if (currentSession.userDocumentData.Any(x => x.Key == chatIdKey))
                    {
                        currentSession.userDocumentData[chatIdKey] += mindeeResponseText;
                    }
                    else
                    {
                        currentSession.userDocumentData.Add(currentSession.ChatId, mindeeResponseText);
                    }


                    mindeeResponseText += "\nTranslate these field's keys in language of YOUR previous text message. The user has to understand the content of it.(And don't say anything like \"Of course! Here are your translated fields\". Just do what I Asked)";
                    currentSession.openAiApi._messages.Add(new SystemChatMessage(mindeeResponseText));
                    gptResponse = await OpenAiResponseAsync(currentSession, mindeeResponseText);
                    await _botClient.SendMessage(currentSession.ChatId, gptResponse);

                    if (currentSession.keyStepOrder == 4)
                    {
                        var prompt = _keyPrompts["step3"];
                        gptResponse = await OpenAiResponseAsync(currentSession, prompt);
                        await _botClient.SendMessage(currentSession.ChatId, gptResponse);
                    }
                    currentSession.keyStepOrder++;
                }

            }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); }
        }

        private async Task<byte[]> ExtractPhoto(PhotoSize photoSize)
        {
            var file = await _botClient.GetFile(photoSize.FileId);

            using (var ms = new MemoryStream())
            {
                await _botClient.DownloadFile(file.FilePath, ms);
                ms.Seek(0, SeekOrigin.Begin);

                byte[] photoBytes = ms.ToArray();

                return photoBytes;
            }

        }

        private async Task<string> OpenAiResponseAsync(UserSession currentSession, string prompt)
        {

            currentSession.openAiApi._messages.Add(new UserChatMessage(prompt));
            string responseText = await currentSession.openAiApi.GetResponseAsync(currentSession.openAiApi._messages.ToArray());

            return responseText;
        }
        private Dictionary<string, string> SetKeyPrompts()
        {
            var dictionary = new Dictionary<string, string>();

            dictionary.Add("language", "Suggest to choose and write client's language. Write \"Choose and write your language.\" 3 times: in Ukrainian, English and German(Add flags of these countries in the end of each frase). In the end tell user that he can still choose any language besides these 3(tell it in English).");
            dictionary.Add("step1", $"Introduce yourself in language: NULL. You are the telegram bot that helps people purchasing car insurances. Your name is \"Car? Sure!\" so you can make 1-line marketing verse with it.");
            dictionary.Add("step2", "Ask user to submit photos of his documents in next order: 1. Personal ID Card(Front side). 2. Vehicle Registration Document(Front side). 3. Vehicle Registration Document(Back side). Remind that folow this order is necessary for correct data reading.");
            dictionary.Add("step3", "Ask user to confirm that all data was taken correctly. IF his response you consider to be positive, make YOUR next message starts with \"true\" without any uppercases and special symbols, and separate it only with white space. BUT IF you consider it to be negative, in the same way start your message with \"false\". IF user's response sounds not related to the question, start your response with \"null\".");
            dictionary.Add("step4", "Tell user a price of insuranse policy (100$) and ask user if they agree with the price. IF the user disagrees, the bot should apologize and explain that 100 USD is the only available price. If the user agrees, make YOUR next message starts with \"true\" and proceed to the final step. IF user's response sounds not related to the question, ignore it and politely insist on direct response.");
            dictionary.Add("step5", "Generate a structured text document in Makefile format that represents an auto insurance policy.(ONLY Makefile, do not add any additional text from you please) Use these fields: ");
            dictionary.Add("step6", "Say that you generating a file and finish sentence with three dots");
            dictionary.Add("step7", "Say that we've done, ask if he has some questions and tell him that if he wants to repeat the procedure, he has to send you \"/start\" command.");
            dictionary.Add("error", "Say that some problem occured while processing message. But without any links please.");

            return dictionary;
        }

        private async Task GenerateAndSendPDF(UserSession currentSession)
        {

            string prompt = _keyPrompts["step5"] + "\n" + currentSession.ToStringUserDocumentData();
            Console.WriteLine("Document prompt: " + prompt);
            string gptResponse = await OpenAiResponseAsync(currentSession, prompt);
            Console.WriteLine("Document content: " + gptResponse);

            
            using (var stream = new MemoryStream())
            {
                try
                {


                    var inputFile = new InputFileStream(stream, "InsurancePolicy.pdf");

                    PdfDocument document = new PdfDocument();


                    document.Info.Title = "Car Insurance Policy";

                    PdfPage page = document.AddPage();

                    XGraphics gfx = XGraphics.FromPdfPage(page);
                    var font = new XFont("Times New Roman", 12, XFontStyleEx.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));


                    double margin = 40;
                    double lineHeight = font.GetHeight() + 2;
                    double yPoint = margin;

                    string[] lines = gptResponse.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                    foreach (string line in lines)
                    {

                        if (yPoint + lineHeight > page.Height - margin)
                        {
                            page = document.AddPage();
                            gfx = XGraphics.FromPdfPage(page);
                            yPoint = margin;
                        }

                        gfx.DrawString(line, font, XBrushes.Black, new XRect(margin, yPoint, page.Width - 2 * margin, lineHeight), XStringFormats.TopLeft);
                        yPoint += lineHeight;
                    }

                    document.Save(stream);
                    stream.Position = 0;
                }
                catch(Exception ex) { await _botClient.SendMessage(currentSession.ChatId, ex.ToString()); }


                //try to send finihed policy document
                try { await _botClient.SendDocument(currentSession.ChatId, stream); }
                catch (Exception ex) { Console.WriteLine(ex.ToString()); }
            }

        }

        private void OnCanselKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            _isRunning = false;
        }

    }
}
