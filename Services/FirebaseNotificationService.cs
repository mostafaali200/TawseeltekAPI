using Google.Apis.Auth.OAuth2;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TawseeltekAPI.Services
{
    public class FirebaseV1Service
    {
        private readonly GoogleCredential _credential;
        private readonly string _projectId;

        public FirebaseV1Service(IConfiguration config)
        {
            // مسار ملف JSON اللي نزلته من Firebase
            var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "tawseeltek-a4835694c83b.json");
            _credential = GoogleCredential.FromFile(jsonPath)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");

            _projectId = config["Firebase:ProjectId"] ?? "tawseeltek";
        }

        public async Task<bool> SendNotificationAsync(string deviceToken, string title, string body)
        {
            var accessToken = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync();

            var url = $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send";

            var payload = new
            {
                message = new
                {
                    token = deviceToken,
                    notification = new
                    {
                        title,
                        body
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
    }
}
