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
                {"rule1", "1. Response to user always in the language he writes the main text to you." },
                {"rule2", "2. IF user writes something NOT related to theme of car insurance, tell him politely that you can't support this theme and suggest to go back on topic." },
                {"rule3", "3. Try to understand a slang user can use in his messages." }

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
