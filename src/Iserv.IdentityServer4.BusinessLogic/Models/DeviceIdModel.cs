using System.ComponentModel.DataAnnotations;
using Skoruba.IdentityServer4.Admin.EntityFramework.Enums;

namespace Iserv.IdentityServer4.BusinessLogic.Models
{
    /// <summary>
    /// Модель токена безопасности устройства пользователя
    /// </summary>
    public class DeviceIdModel
    {
        /// <summary>
        /// Id устройства
        /// </summary>
        [Required]
        public string DeviceId { get; set; }

        /// <summary>
        /// Значение токена безопасности устройства
        /// </summary>
        [Required]
        public string DeviceToken { get; set; }

        /// <summary>
        /// Тип Id устройства
        /// </summary>
        [Required]
        public UserDeviceIdTypes DeviceIdType { get; set; }
        
        /// <summary>
        /// Наименование устройства
        /// </summary>
        [Required]
        public string DeviceName { get; set; }
    }
}
