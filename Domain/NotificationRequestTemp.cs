using Nop.Core;

namespace Nop.Plugin.Payments.SafeTyPay.Domain
{
    public class NotificationRequestTemp : BaseEntity
    { 
        #region from safetypay

        /// <summary>
        ///     The Api Key Used
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// the Request Date Time
        /// </summary>
        public string RequestDateTime { get; set; }

        /// <summary>
        /// the MerchantSalesID from safetypay or OrderGuid
        /// </summary>
        public string MerchantSalesID { get; set; }

        /// <summary>
        /// The ReferenceNo fro mSafetyPay
        /// </summary>
        public string ReferenceNo { get; set; }

        /// <summary>
        /// The Creation Date Time 
        /// </summary>
        public string CreationDateTime { get; set; }

        /// <summary>
        /// The Amount to inform
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// The CurrencyId Of Order
        /// </summary>
        public string CurrencyId { get; set; }

        /// <summary>
        /// The PaymentReferenceNo to pay in bank
        /// </summary>
        public string PaymentReferenceNo { get; set; }

        /// <summary>
        /// the Status Code send by safetypay
        /// </summary>
        public string StatusCode { get; set; }

        /// <summary>
        /// The signature send by safetypay
        /// </summary>
        public string Signature { get; set; }

        /// <summary>
        /// The origin Message from SafeTyPay
        /// </summary>
        public string Origin { get; set; }

        #endregion from safetypay

        /// <summary>
        /// Save the Url redirect to pay 
        /// </summary>
        public string ClientRedirectURL { get; set; }

        /// <summary>
        /// Check if the operationcode nro exist
        /// </summary>
        public bool OperationCode { get; set; }
    }
}