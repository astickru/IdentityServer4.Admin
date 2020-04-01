using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Skoruba.IdentityServer4.Admin.EntityFramework.Enums;

namespace Skoruba.IdentityServer4.Admin.EntityFramework.Shared.Entities.Identity
{
    /// <summary>
    /// Токены безопасности устройств пользователей
    /// </summary>
    /// <typeparam name="TKey">Тип Id пользователя</typeparam>
    public class UserIdentityUserDeviceId<TKey> where TKey : IEquatable<TKey>
    {
        public UserIdentityUserDeviceId()
        {
            CreateDate = DateTime.Now;
        }
        
        /// <summary>
        /// Id
        /// </summary>
        [Key]
        public Guid Id { get; set; }
        
        /// <summary>
        /// Id устройства
        /// </summary>
        public string DeviceId { get; set; }
        
        /// <summary>
        /// Значение токена безопасности устройства
        /// </summary>
        [ProtectedPersonalData]
        public string Value { get; set; }
        
        /// <summary>
        /// Id пользователя
        /// </summary>
        public TKey UserId { get; set; }
        
        /// <summary>
        /// Тип Id устройства
        /// </summary>
        public UserDeviceIdTypes DeviceIdType { get; set; }
        
        /// <summary>
        /// Наименование устройства
        /// </summary>
        public string DeviceName { get; set; }
        
        /// <summary>
        /// Время добавления устройства в доверительные
        /// </summary>
        public DateTime CreateDate { get; set; }
    }
    
    /// <summary>
    /// Токены безопасности устройств пользователей
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public class UserIdentityUserDeviceId : UserIdentityUserDeviceId<string>
    {
       
    }
}