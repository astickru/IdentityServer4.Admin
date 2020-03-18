using System;
using System.Threading.Tasks;

namespace Iserv.IdentityServer4.BusinessLogic.Interfaces
{
    /// <summary>
    /// Отправитель уведомлений
    /// </summary>
    public interface ISender : IDisposable
    {
        /// <summary>
        /// Отправка уведомления
        /// </summary>
        /// <param name="token">Токен устройства</param>
        /// <param name="subject">Тема</param>
        /// <param name="message">Сообщение</param>
        /// <returns></returns>
        Task SendNotificationAsync(string token, string subject, string message);
    }
}