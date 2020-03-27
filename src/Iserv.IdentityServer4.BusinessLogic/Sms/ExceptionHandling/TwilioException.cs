using System;

namespace Iserv.IdentityServer4.BusinessLogic.Sms.ExceptionHandling
{
    public class TwilioException : Exception
    {
        public string ErrorKey { get; set; }

        public TwilioException(string message) : base(message)
        {
        }

        public TwilioException(string message, string errorKey) : base(message)
        {
            ErrorKey = errorKey;
        }

        public TwilioException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}