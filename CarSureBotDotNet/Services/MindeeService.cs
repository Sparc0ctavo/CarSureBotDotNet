using Mindee;
using Mindee.Input;
using Mindee.Http;
using Mindee.Product.Generated;

namespace CarSureBotDotNet
{
    internal class MindeeService
    {

        private string _apiKey;

        private MindeeClient _mindeeClient;

        public MindeeService()
        {
            _apiKey = Environment.GetEnvironmentVariable("MINDEE_TOKEN");
            _mindeeClient = new MindeeClient(new MindeeSettings { ApiKey = _apiKey });
        }

        public async Task<string> GetTextData(byte[] photoBytes, short queueStep)
        {

            try
            {
                string tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".jpg");
                File.WriteAllBytes(tempFilePath, photoBytes);


                var inputSource = new LocalInputSource(tempFilePath);

                string endpointName = null;

                if (queueStep == 0)
                {
                    endpointName = "id_card";
                }
                else if (queueStep == 1)
                {
                    endpointName = "car_title_front";
                }
                else if (queueStep == 4)
                {
                    endpointName = "car_title_back";
                }

                CustomEndpoint endpoint = new CustomEndpoint(
                    endpointName: endpointName,
                    accountName: "Sparc0ctavo",
                    version: "1"
                );


                var response = await _mindeeClient.EnqueueAndParseAsync<GeneratedV1>(inputSource, endpoint);

                var passport = response.Document;


                File.Delete(tempFilePath);

                string documentName = (char.ToUpper(endpointName[0]) + endpointName.Substring(1)).Replace('_', ' ');
                string passportTextData = $"Document: {documentName}\n";

                foreach (var field in passport.Inference.Prediction.Fields)
                {

                    string key = (char.ToUpper(field.Key.ToString()[0]) + field.Key.ToString().Substring(1)).Replace('_', ' ');
                    string value = field.Value.ToString().Replace(":value:", "").Trim();

                    passportTextData += $"\n{key}: {value}";
                }

                return passportTextData;
            }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); }


            return "null";

        }
    }
}
