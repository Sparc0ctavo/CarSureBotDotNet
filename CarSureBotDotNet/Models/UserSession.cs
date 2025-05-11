
namespace CarSureBotDotNet
{
    internal class UserSession
    {

        internal long ChatId { get; set; }

        internal short keyStepOrder;
        internal short photoSubStepIterator;
        internal bool isDataConfirmed;   //indicator of user's response to question to confirm accuracy of read data: true - he gave any text response or it's default position; false - he did anything but text response(sent photo, voice, etc.)
        internal OpenAiService openAiApi;
        internal Dictionary<long, Dictionary<short, string>> userDocumentData;

        public UserSession(long chatId)
        {

            ChatId = chatId;
            keyStepOrder = 1;
            photoSubStepIterator = 0;
            isDataConfirmed = true;
            openAiApi = new OpenAiService();
            userDocumentData = new Dictionary<long, Dictionary<short, string>>();

        }

        public string ToStringUserDocumentData()
        {
            string content = " ";

            foreach (var key in userDocumentData[ChatId].Keys)
            {
                content += $"\n{userDocumentData[ChatId][key]}\n";

            }

            return content;
        }

    }
}
