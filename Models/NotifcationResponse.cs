namespace Nop.Plugin.Payments.SafeTyPay.Models
{
    public class NotifcationResponse
    {
        //Integer  Error associated to the call: ErrorNumber = 0, which means that call was successful.
        public int ErrorNumber { get; set; } //0 : No error 1 : API Key not recognized 2 : Signature not valid 3 : Other errors

        //String (ISO 8601: yyyy-MM-ddThh:mm:ss) Merchant Date and Time used to compose signature
        public string ResponseDateTime { get; set; } //2007-01-31T14:24:59

        //String (max-lenght = 20) Reference number of the sale.(the same used in the purchase process or call to CreateTokenExpress)
        public string MerchantSalesID { get; set; } //ORD-10001,

        //String (lenght = 32) SafetyPay Operation Identifier
        public string ReferenceNo { get; set; }  //0119182951762562

        //String (ISO 8601: yyyy-MM-ddThh:mm:ss)  Creation date of the transactio
        public string CreationDateTime { get; set; }//2007-01-31T14:24:59

        //Decimal The amount of the transaction.Use 2 decimals.
        public decimal Amount { get; set; } //100.00

        //String(ISO-4217) Currency of the transaction
        public string CurrencyId { get; set; } //USD

        //String(max-lenght = 20) Reference number of the payment operation
        public string PaymentReferenceNo { get; set; } //ORD-10001,

        //Status of SafetyPay operation
        public string Status { get; set; }//“102”, “201”, etc

        //Merchant’s order number or MerchantSalesID.
        public string OrderNo { get; set; } //ORD-10001,

        //Refer to https://developers.safetypay.com/docs/notification-signature-calculator#section-request-signature-calculator
        public string Signature { get; set; } //Hash SHA256 :

                                              //RequestDateTime
                                              //+MerchantSalesID
                                              //+ReferenceNo
                                              //+CreationDateTime
                                              //+Amount
                                              //+CurrencyID
                                              //+PaymentReferenceNo
                                              //+Status
                                              //+SignatureKey

        public string ToParameter()
        {
            //ErrorNumber,ResponseDateTime,MerchantSalesID,ReferenceNo,CreationDateTime,Amount,CurrencyID,PaymentReferenceNo,Status,OrderNo,Signature
            var str = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                ErrorNumber,
                ResponseDateTime,
                MerchantSalesID,
                ReferenceNo,
                CreationDateTime,
                Amount.ToString().Replace(",","."),
                CurrencyId,
                PaymentReferenceNo,
                Status,
                OrderNo,
                Signature);
            return str;
        }
    }
}