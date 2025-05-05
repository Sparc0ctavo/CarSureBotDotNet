using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarSureBot
{
    internal class UserSession
    {

        internal long ChatId { get; set; }

        internal short keyStepOrder;
        internal OpenAiService openAiApi;
        internal Dictionary<long, string> userDocumentData;

        public UserSession(long chatId)
        {

            ChatId = chatId;
            keyStepOrder = 0;
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
