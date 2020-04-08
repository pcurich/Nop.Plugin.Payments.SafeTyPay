namespace Nop.Plugin.Payments.SafeTyPay.Models
{
    public class ExpressTokenResponse
    {
        //String (Required, ISO 8601: yyyy-MM-ddThh:mm:ss) Merchant Date and Time used to compose signature
        public string ResponseDateTime { get; set; }//2007-01-31T14:24:59

        //String  URL where shopper must be redirected to complete a payment
        public string ClientRedirectURL { get; set; } //https://sandbox-gateway.safetypay.com/Express4/Checkout/index?TokenID=86b7eba7-0965-4410-abf3-4a27e4929ffc

        //String Refer to https://developers.safetypay.com/docs/generating-signature
        public string Signature { get; set; } //Hash SHA256: ResponseDateTime +ClientRedirectURL +SignatureKey

        public string ErrorCode { get; set; }
    }
}