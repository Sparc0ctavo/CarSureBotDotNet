
namespace CarSureBotDotNet
{
    internal class UserSession
    {

        internal long ChatId { get; set; }

        internal short keyStepOrder;
        internal short photoSentIterator;
        internal OpenAiService openAiApi;
        internal Dictionary<long, string> userDocumentData;

        public UserSession(long chatId)
        {

            ChatId = chatId;
            keyStepOrder = 1;
            photoSentIterator = 0;
            openAiApi = new OpenAiService();
            userDocumentData = new Dictionary<long, string>();

        }

        public string ToStringUserDocumentData()
        {
            string content = " ";

            foreach (var key in userDocumentData.Keys)
            {
                content += $"\n{userDocumentData[key]}\n";
            }

            return content;
        }

    }
}
