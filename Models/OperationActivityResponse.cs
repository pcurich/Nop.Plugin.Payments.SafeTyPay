using System.Collections.Generic;

namespace Nop.Plugin.Payments.SafeTyPay.Models
{
    public class OperationActivityResponse
    { 
        //String (ISO 8601: yyyy-MM-ddThh:mm:ss) Merchant Date and Time used to compose signature
        public string ResponseDateTime { get; set; } = null!; //2007-01-31T14:24:59

        // List of OperationType OperationType: Entity that contains the information of an operation and their activities.
        public List<OperationType> ListOfOperations { get; set; } = new List<OperationType>();

        //String Refer to https://developers.safetypay.com/docs/generating-signature
        public string Signature { get; set; } = null!; //ResponseDateTime +

        //ListOfOperations[n].OperationID +
        //ListOfOperations[n].MerchantSalesID +
        //ListOfOperations[n].OperationActivities[0].Status.StatusCode +SignatureKey
        //Error associated to the call
        public string ErrorNumber { get; set; } = null!;
    }
}