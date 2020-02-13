﻿using System;
using System.Collections.Generic;

namespace Iserv.IdentityServer4.BusinessLogic.ExceptionHandling
{
    public class PortalException : Exception
    {
        public string ErrorKey { get; set; }

        public PortalException(string message) : base(message)
        {
        }

        public PortalException(string message, string errorKey) : base(message)
        {
            ErrorKey = errorKey;
        }

        public PortalException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}