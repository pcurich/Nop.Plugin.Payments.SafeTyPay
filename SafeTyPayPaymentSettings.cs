﻿using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.SafeTyPay
{
    /// <summary>
    /// Represents settings of the  SafeTyPay payment plugin
    /// </summary>
    public class SafeTyPayPaymentSettings : ISettings
    {
        #region General

        /// <summary>
        /// Prefix Sandbox
        /// </summary>
        public string PrefixSandbox { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value indicating whether to use sandbox (testing environment)
        /// </summary>
        public bool UseSandbox { get; set; }

        /// <summary>
        /// Can Generate New Code 
        /// </summary>
        public bool CanGenerateNewCode { get; set; }
        /// <summary>
        /// Url to transaction Ok
        /// </summary>
        public string TransactionOkURL { get; set; } = null!;

        /// <summary>
        /// Url to transaction Error
        /// </summary>
        public string TransactionErrorURL { get; set; } = null!;

        /// <summary>
        /// Expiration time to life of operation code
        /// </summary>
        public int ExpirationTime { get; set; }

        /// <summary>
        /// Gets or sets an additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the number of attemps
        /// </summary>
        public int NumberOfAttemps { get; set; }

        #endregion

        #region Merchant Manager System

        /// <summary>
        /// Only for remember the UserName by SafetyPay
        /// </summary>
        public string UserNameMMS { get; set; } = null!;

        /// <summary>
        /// Only for remember the password by SafetyPay
        /// </summary>
        public string PasswordMMS { get; set; } = null!;

        #endregion

        #region Technical Documentation

        /// <summary>
        /// Only for remember the password by SafetyPay
        /// </summary>
        public string PasswordTD { get; set; } = null!;

        #endregion

        #region Enviroment

        /// <summary>
        /// Api Key From MMS of SafetyPay
        /// </summary>
        public string ApiKey { get; set; } = null!;

        /// <summary>
        /// Signature Key From MMS of SafetyPay
        /// </summary>
        public string SignatureKey { get; set; } = null!;

        /// <summary>
        /// Express Token Url
        /// </summary>
        public string ExpressTokenUrl { get; set; } = null!;

        /// <summary>
        /// Express Token Url
        /// </summary>
        public string NotificationUrl { get; set; } = null!;

        #endregion
    }
}