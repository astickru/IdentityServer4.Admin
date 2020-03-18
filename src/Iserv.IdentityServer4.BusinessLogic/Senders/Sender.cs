using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Iserv.IdentityServer4.BusinessLogic.Interfaces;

namespace Iserv.IdentityServer4.BusinessLogic.Senders
{
    public class Sender : ISender
    {
        private FirebaseMessaging _messaging;
        private const string KeyJson = "key.json";

        public Sender()
        {
            var app = FirebaseApp.Create(new AppOptions {Credential = GoogleCredential.FromFile(KeyJson).CreateScoped("https://www.googleapis.com/auth/firebase.messaging")});
            _messaging = FirebaseMessaging.GetMessaging(app);
        }

        public async Task SendNotificationAsync(string token, string subject, string message)
        {
            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            await _messaging.SendAsync(
                new Message
                {
                    Token = token,
                    Notification = new Notification
                    {
                        Body = message,
                        Title = subject
                    }
                });
        }

        public void Dispose()
        {
        }
    }
}