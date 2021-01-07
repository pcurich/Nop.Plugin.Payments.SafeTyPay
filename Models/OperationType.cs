namespace Nop.Plugin.Payments.SafeTyPay.Models
{
    public class OperationType
    {
        //String (ISO 8601: yyyy-MM-ddThh:mm:ss) Creation date of the transaction
        public string CreationDateTime { get; set; } = null!;

        //String (lenght = 16) SafetyPay Operation Identifier
        public string OperationID { get; set; } = null!;

        //String (max-lenght = 20) Reference number of the sale.
        public string MerchantSalesID { get; set; } = null!;

        //String (max-length = 20) Purchase order identifier of the commerce. Transmitted by the merchant according to the confirmation.
        public string MerchantOrderID { get; set; } = null!;

        //Decimal Transaction amount in the merchant currency.
        public decimal Amount { get; set; }

        //String (ISO-4217) Transaction currency code
        public string CurrencyID { get; set; } = null!;

        //Decimal Transaction amount of the buyer currency.
        public string ShopperAmount { get; set; } = null!;

        //String (ISO-4217)Currency Code related to the ShopperAmount.
        public string ShopperCurrencyID { get; set; } = null!;

        //String (max-length = 20) Operation reference number of payment.
        public string PaymentReferenceNo { get; set; } = null!;

        //List of Refunds of the transaction.
        public string RefundsRelated { get; set; } = null!;
    }
}