using Nop.Core;

namespace Nop.Plugin.Payments.SafeTyPay.Domain
{
    public partial class NotificationRequestSafeTyPay : BaseEntity
    {
        #region from safetypay

        /// <summary>
        ///     The Api Key Used
        /// </summary>
        public string ApiKey { get; set; } = null!;

        /// <summary>
        /// the Request Date Time
        /// </summary>
        public string RequestDateTime { get; set; } = null!;

        /// <summary>
        /// the MerchantSalesID from safetypay or OrderGuid
        /// </summary>
        public string MerchantSalesID { get; set; } = null!;

        /// <summary>
        /// The ReferenceNo fro mSafetyPay
        /// </summary>
        public string ReferenceNo { get; set; } = null!;

        /// <summary>
        /// The Creation Date Time 
        /// </summary>
        public string CreationDateTime { get; set; } = null!;

        /// <summary>
        /// The Amount to inform
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// The CurrencyId Of Order
        /// </summary>
        public string CurrencyId { get; set; } = null!;

        /// <summary>
        /// The PaymentReferenceNo to pay in bank
        /// </summary>
        public string PaymentReferenceNo { get; set; } = null!;

        /// <summary>
        /// the Status Code send by safetypay
        /// </summary>
        public string StatusCode { get; set; } = null!;

        /// <summary>
        /// The signature send by safetypay
        /// </summary>
        public string Signature { get; set; } = null!;

        /// <summary>
        /// The origin Message from SafeTyPay
        /// </summary>
        public string Origin { get; set; } = null!;

        #endregion from safetypay

        /// <summary>
        /// Save the Url redirect to pay 
        /// </summary>
        public string ClientRedirectURL { get; set; } = null!;

        /// <summary>
        /// Check if the operationcode nro exist
        /// </summary>
        public bool OperationCode { get; set; }
    }
}