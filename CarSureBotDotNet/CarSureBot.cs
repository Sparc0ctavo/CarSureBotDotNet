using OpenAI.Chat;
using PdfSharp.Drawing;
using PdfSharp.Internal;
using PdfSharp.Pdf;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
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
        private Dictionary<string, string> _botMessageEmojiStatus;
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

            //for circle emojis
            _botMessageEmojiStatus = new Dictionary<string, string> {
                { "Green", "Start this message with green circle emoji(One emoji per message)."},
                { "Red", "Start this message with red circle emoji(One emoji per message)."},
                { "Blue", "Start this message with blue circle emoji(One emoji per message)."}
            };
            _keyPrompts = SetKeyPrompts();

            var commands = new[]{

                 new BotCommand { Command = "start", Description = "new procedure"}
            };

            await _botClient.SetMyCommands(commands);

            Console.WriteLine("Receiving has been started! Press CTRL + C to stop receiving.");

            while (_isRunning) {
                
                await PingChats();                              //checking for "dead" chats
                await Task.Delay(TimeSpan.FromSeconds(7));
            
            }

            await StopAsync();
        }

        public async Task StopAsync()
        {
            _cts.Cancel();
        }

        private async Task OnUpdate(Update update)
        {

            UserSession currentSession; //User's session instance

            
                
                if (update.Type == UpdateType.Message)
                {

                    
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
                    else if (update.Message.Type == MessageType.Animation ||
                             update.Message.Type == MessageType.Audio ||
                             update.Message.Type == MessageType.Document ||
                             update.Message.Type == MessageType.Location ||
                             update.Message.Type == MessageType.Video ||
                             update.Message.Type == MessageType.Contact)
                    {
                        string gptResponse = await OpenAiResponseAsync(currentSession, _keyPrompts["error2"]);
                        await _botClient.SendMessage(currentSession.ChatId, gptResponse);
                    }
                }
                            

        }

        private async Task PingChats()
        {

            if(_sessions.Count > 0)
            {
                UserSession currentSession = null;

                try
                {
                    foreach (var session in _sessions)
                    {
                        currentSession = session;
                        await _botClient.SendChatAction(session.ChatId, ChatAction.Typing);
                    }
                }
                catch(ApiRequestException ex) {
                    Console.WriteLine(ex.Message);
                    _sessions.Remove(currentSession);
                    Console.WriteLine("Session deleted.");
                }
                    
            }

            return;
        }

        private async Task OnError(Exception exception, HandleErrorSource errorSource)
        {
            Console.WriteLine(exception.ToString());
        }

        private async Task UpdateTextMessageHandler(Message message, UserSession currentSession)
        {

            Console.WriteLine($"A text message \"{message.Text}\" recieved from {message.Chat.Username}");

            string prompt = message.Text + " " + _botMessageEmojiStatus["Blue"];
            string gptResponse = await OpenAiResponseAsync(currentSession, prompt);

            //Extracting logical marker(true/false/null)
            Console.WriteLine("Chat GPT response: " + gptResponse); //Console output with marker included
            string responseStatusStr = gptResponse.Split()[0];
            bool responseStatus;
            string isGroup = gptResponse.Split().Last();


            try
            {
                //checking if user requested step back(such gpt responses are marked with word "rollback" in the end)
                if (gptResponse.Split().Last() == "rollback")
                {
                    InitStepBack(currentSession);
                }
                if (message.Text.StartsWith("/start"))
                {
                    currentSession.keyStepOrder = 1;
                    currentSession.photoSubStepIterator = 0;
                    currentSession.userDocumentData = new Dictionary<long, Dictionary<short, string>>();
                }



                //"logic markers as response polarity status indicators: true - positive, false - negative, null - neutral"
                if (responseStatusStr != "null")
                {
                    
                    if (currentSession.keyStepOrder == 1)
                    {

                        //get "NULL" to replace it with language user uses
                        var languageCode = message.From.LanguageCode;
                        _keyPrompts["step1"] = _keyPrompts["step1"].Replace("NULL", languageCode); //on last commit state, language adaptation does.n work stable.:(
                        await InitStep(currentSession, currentSession.keyStepOrder);
                        currentSession.keyStepOrder += 1;
                        await InitStep(currentSession, currentSession.keyStepOrder);
                        return;

                    }
                    else if (currentSession.keyStepOrder == 2 && currentSession.photoSubStepIterator != 0)
                    {
                        

                            if (responseStatusStr == "true" || responseStatusStr == "false")
                            {
                                responseStatus = bool.Parse(responseStatusStr);
                                currentSession.isDataConfirmed = true;

                                if (responseStatus)
                                {
                                    if (currentSession.photoSubStepIterator == 2)
                                    {
                                        currentSession.keyStepOrder += 2;
                                        currentSession.photoSubStepIterator = 0;
                                        await InitStep(currentSession, currentSession.keyStepOrder);
                                        return;
                                    }
                                    else
                                    {
                                        await InitStep(currentSession, 21);
                                        return;
                                    }

                                }
                                else
                                {
                                    if (isGroup == "group")
                                    {
                                        currentSession.photoSubStepIterator = 0;
                                        await InitStep(currentSession, 21);
                                        return;
                                    }
                                    else
                                    {
                                        currentSession.photoSubStepIterator--;
                                        await InitStep(currentSession, 21);
                                        return;
                                    }

                                
                                
                                }

                            }
                        

                        
                    }
                    else if (currentSession.keyStepOrder == 4)
                    {
                        if (responseStatusStr == "true" || responseStatusStr == "false")
                        {
                            responseStatus = bool.Parse(responseStatusStr);

                            if (responseStatus)
                            {
                                currentSession.keyStepOrder++;
                                await InitStep(currentSession, currentSession.keyStepOrder);

                                currentSession.keyStepOrder++;
                                await GenerateAndSendPDF(currentSession);
                                await InitStep(currentSession, 61);
                                currentSession.keyStepOrder++;

                                await InitStep(currentSession, currentSession.keyStepOrder);

                                return;
                            }
                            
                        }
                    }
                    
                }
                else
                {
                    string currentStep = _keyPrompts[$"step{currentSession.keyStepOrder}"];

                    //exceptional key prompts that are not included in dictionary _keyPrompts and do not handling by TextHandler
                    if (currentSession.keyStepOrder == 2) currentStep = _keyPrompts[$"step{currentSession.keyStepOrder}"];

                    if(isGroup != "group")
                    {
                        if ((currentSession.photoSubStepIterator - 1) == 0) currentStep = "Send your personal id";
                        if ((currentSession.photoSubStepIterator - 1) == 1) currentStep = "Send your Front side of vehicle id";
                    }
                    else { currentStep = "Send your personal ID"; }
                    
                    if (currentSession.keyStepOrder == 7) currentStep = _keyPrompts[$"step{currentSession.keyStepOrder}"];

                    prompt += $" And shortly remind user what he has to do now. Current step: {currentStep}. Say it more alive, like a human, not machine";
                    gptResponse = await OpenAiResponseAsync(currentSession, prompt);
                }


                //formating text by cutting "false"/"rollback" markers
                gptResponse = (gptResponse.Split().Last() == "rollback" || gptResponse.Split().Last() == "group") ? gptResponse = Regex.Replace(gptResponse, @"\s+\S+$", "") : gptResponse;
                gptResponse = (gptResponse.Split()[0] == "false" || gptResponse.Split()[0] == "null" || gptResponse.Split()[0] == "true") ? gptResponse = gptResponse.Substring(gptResponse.IndexOf(' ') + 1) : gptResponse;
                
                await _botClient.SendMessage(currentSession.ChatId, gptResponse);

            }
            catch (Exception ex) { Console.WriteLine("Text message response: " + ex.ToString()); }
        }

        private async Task UpdatePhotoMessageHandler(Message message, UserSession currentSession)
        {

            Console.WriteLine($"A photo message recieved from {message.Chat.Username}");
            try
            {
                if (currentSession.keyStepOrder == 2 && currentSession.isDataConfirmed) //if isDataConfirmed = false, we prevent reading from another photo before confirmation of previous one
                {

                    string gptResponse;

                    //related to pattern definition written below
                    if (message.MediaGroupId != null && currentSession.photoSubStepIterator == 0)
                    {
                        gptResponse = await OpenAiResponseAsync(currentSession, "Tell that now you processing all photos automatically because fotos been sent in group. And finish with 3 dots for view.");
                        await _botClient.SendMessage(currentSession.ChatId, gptResponse);
                    }

                    //extract photo from message
                    var photoBytes = await ExtractPhoto(message.Photo.Last());
                    string mindeeResponseText = await _mindeeService.GetTextData(photoBytes, currentSession.photoSubStepIterator);
                    

                    //saving read document fields for future policy generation
                    var chatIdKey = currentSession.ChatId;
                    if (currentSession.userDocumentData.Any(x => x.Key == chatIdKey))
                    {
                        currentSession.userDocumentData[chatIdKey][currentSession.photoSubStepIterator] = "\n\n" + mindeeResponseText;
                    }
                    else
                    {
                        currentSession.userDocumentData.Add(currentSession.ChatId, new Dictionary<short, string> { { currentSession.photoSubStepIterator, mindeeResponseText } });
                    }

                    //sending read text to bot
                    mindeeResponseText += "\nTranslate these field's keys in language of YOUR previous text message. The user has to understand the content of it." +
                        "(And don't say anything like \"Of course! Here are your translated fields\". Just do what I Asked). And " + _botMessageEmojiStatus["Green"];
                    gptResponse = await OpenAiResponseAsync(currentSession, mindeeResponseText);
                    await _botClient.SendMessage(currentSession.ChatId, gptResponse);

                    //defining which pattern of confirmation to use depends of photos bundles: single photo or mediaGrouId 
                    if (message.MediaGroupId == null)
                    {
                        currentSession.isDataConfirmed = false;
                        await InitStep(currentSession, 3);
                    }
                    else if (message.MediaGroupId != null && currentSession.photoSubStepIterator == 1)
                    {
                        currentSession.isDataConfirmed = false;
                        await InitStep(currentSession, 31);
                    }



                    
                    currentSession.photoSubStepIterator++;
                    

                }
                else {
                    var response = await OpenAiResponseAsync(currentSession, _keyPrompts["error1"]);
                    await _botClient.SendMessage(currentSession.ChatId, response); 
                }

            }
            catch (Exception ex) { Console.WriteLine("UpdatePhotoMessageHandler(): " + ex.ToString()); }
        }

        private async Task InitStep(UserSession currentSession, short customKeyStepOrder)
        {

            var prompt = _keyPrompts[$"step{customKeyStepOrder}"];
            var gptResponse = await OpenAiResponseAsync(currentSession, prompt);

            //extensions of initialization for steps
            if (customKeyStepOrder == 21)
            {
                if (currentSession.photoSubStepIterator == 0) prompt = prompt.Replace("DOCUMENT_NAME", "Personal id(id card)");
                if (currentSession.photoSubStepIterator == 1) prompt = prompt.Replace("DOCUMENT_NAME", "Front side of vehicle id");
                gptResponse = await OpenAiResponseAsync(currentSession, prompt);
                await _botClient.SendMessage(currentSession.ChatId, gptResponse);
                return;
            }
            else
            {
                await _botClient.SendMessage(currentSession.ChatId, gptResponse);
            }
        }

        private void InitStepBack(UserSession currentSession)
        {
            currentSession.keyStepOrder -= 1; return;
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

            dictionary.Add("step1", "Introduce yourself in language of Telegram standart language code: NULL. You are the telegram bot that helps people purchasing car insurances. Your name is \"Car? Sure!\" so you can make 1-line marketing verse with it." + _botMessageEmojiStatus["Green"]);
            dictionary.Add("step2", "Ask user to submit photos of his documents in next order: 1. Personal ID Card(Front side). 2. Vehicle Registration Document(Front side). Remind that folow this order is necessary for correct data reading. Instantly tell user to send his personal id(id card, etc.)" + _botMessageEmojiStatus["Green"]);
            dictionary.Add("step21", "now shortly ask user to send DOCUMENT_NAME" + _botMessageEmojiStatus["Green"]);
            dictionary.Add("step3", "Ask user to confirm that all data was taken correctly and shortly remind him to pick a spot with a good light source to make clear photos. IF his response you consider to be positive, make YOUR next message starts with \"true\" without any uppercases and special symbols, and separate it only with white space. BUT IF you consider it to be negative, in the same way start your message with \"false\"(without white space before word false but with space right after) and you have to ask him to send documents again. IF user's response sounds not related to the question, start your response with \"null\"." + _botMessageEmojiStatus["Green"]);
            dictionary.Add("step31", "Ask user to confirm that all data was taken correctly and shortly remind him to pick a spot with a good light source to make clear photos. IF his response you consider to be positive, make YOUR next message starts with \"true\" without any uppercases and special symbols, and separate it only with white space. BUT IF you consider it to be negative, in the same way start your message with \"false\"(without white space before word false but with space right after) and you have to ask him to send documents again. IF user's response sounds not related to the question, start your response with \"null\"."
                            + "And also in the very end after white space write \" group \"; " + _botMessageEmojiStatus["Green"]);
            dictionary.Add("step4", "Tell user a price of insuranse (100$) and ask user if they agree with the price. IF the user disagrees, the bot should apologize and explain that 100 USD is the only available price. If the user agrees, make YOUR next message starts with \"true\" and proceed to the final step." +
                           " If disagrees, YOUR next message starts with \"false\". IF user's response sounds not related to the question, politely insist on direct response. And start answer with \"null\". Keep going with these start words until user's positive answer." + _botMessageEmojiStatus["Green"]);
            dictionary.Add("step5", "Now our step is to generate an insurance policy file if user agreed with the price. Say that you generating a file and finish sentence with three dots. " + _botMessageEmojiStatus["Green"]);
            dictionary.Add("step6", "Generate a structured text document in Makefile format that represents an auto insurance policy.(ONLY Makefile, do not add any additional text from you please) Use these fields: ");
            dictionary.Add("step61", "Take a note that user sould open this file only as PDF" + _botMessageEmojiStatus["Green"]);
            dictionary.Add("step7", "Say that we've done, ask if he has some questions and tell him that if he wants to repeat the procedure, he has to send you \"/start\" command." + _botMessageEmojiStatus["Green"]);
            dictionary.Add("error1", "Say that some problem occured while processing message." + _botMessageEmojiStatus["Red"]);
            dictionary.Add("error2", "Say that you can handle only text and photo messages." + _botMessageEmojiStatus["Red"]);

            return dictionary;
        }

        private async Task GenerateAndSendPDF(UserSession currentSession)
        {

            string prompt = _keyPrompts["step6"] + "\n" + currentSession.ToStringUserDocumentData();
            string userDocumentData = new string(' ', 64) + "CAR INSURANCE POLICY\r\n" + currentSession.ToStringUserDocumentData() + $"\n{DateTime.Now.ToString("dd.MM.yyyy, HH:mm")}";
            Console.WriteLine("Document prompt: " + prompt);
            //string gptResponse = await OpenAiResponseAsync(currentSession, prompt);
            //Console.WriteLine("Document content: " + gptResponse);

            
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

                    string[] lines = userDocumentData.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

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
