using OpenAI.Chat;

namespace CarSureBotDotNet
{
    internal class OpenAiService
    {

        private ChatClient _chatClient;
        internal List<ChatMessage> _messages;
        private Dictionary<string, string> _behaviorRules;

        private string _apiKey = Environment.GetEnvironmentVariable("OPENAI_TOKEN");
        public OpenAiService()
        {
            _chatClient = new ChatClient("gpt-4o", _apiKey);
            _behaviorRules = new Dictionary<string, string> {

                {"introduction", "Now I'm going to give you some rules in your behavior during the dialog with clients, please folow them until the end of conversation." },
                {"rule1", "You are bot-assistant in car insurance purchasing. You can read photo samples of id documents for receiving data that needs you to generate insurance policy. Say so if user asks you. Say it in full capacity, not shortly" },
                {"rule2", "2. Response to user always in the language he writes the main text to you." },
                {"rule3", "3. IF user writes something NOT related to theme of car insurance, tell him politely that you can't support this theme and suggest to go back on topic." },
                {"rule4", "4. Try to understand a slang user can use in his messages." },
                {"rule5", "5. Pricing of our service: If client asks you something related to price of such procedures give him your common response, BUT mention that price of our service is always 100$ no matter what model and type of vehicle it is." },
                {"rule6", "6. Start all your responses with blue circle emoji(ONLY IF prompt does not mention some other circle emoji). One emoji per message." +
                " If user asks you what do they mean: Green - a key message which advances the procedure. Blue - a secondary message which doesn't advances the procedure. Red - an error message which tells about issue occured." +
                " Tell that user can consider that last green emoji message is his current stage of procedure."},
                {"rule7", "7. When user tells you something where you clearly understand that he made a mistake or changed his mind and wants to rollback(make steps back), tell him that successfuly rollbacked. In this case always finish ALL YOUR responses to ALL SUCH PROMPTS with \"rollback\"." },
                {"rule8", "8. In conflict between \"Emoji circle on the start of response\" and \"true/false/null on the start of response\" ALWAYS sets first true/false/null. true/false/null is priority above circle emojis. In this cases Circle emojis go SECOND, right after true/false/null." }

            };

            _messages = new List<ChatMessage>();

        }



        public async Task<string> GetResponseAsync(ChatMessage[] prompts)
        {
            string textResponse = "null";



            try
            {
                var completion = await _chatClient.CompleteChatAsync(prompts);

                textResponse = completion.Value.Content[0].Text;
                _messages.Add(new AssistantChatMessage(textResponse));

                Console.WriteLine("ChatGPT response: " + textResponse + "\n");
            }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); }

            return textResponse;
        }

        internal async Task SetBehaviorPattern()
        {

            foreach (var key in _behaviorRules.Keys)
            {


                _messages.Add(new SystemChatMessage(_behaviorRules[key]));
                await GetResponseAsync(_messages.ToArray());

            }

        }

    }
}
