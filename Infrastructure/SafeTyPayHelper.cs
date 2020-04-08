using System;
using System.Security.Cryptography;
using System.Text;
using Nop.Plugin.Payments.SafeTyPay.Models;

namespace Nop.Plugin.Payments.SafeTyPay.Infrastructure
{
    public static class SafeTyPayHelper
    {
        #region ComputeSha256Hash

        public static string ComputeSha256Hash(ExpressTokenRequest token, string signatureKey)
        {
            var rawData = token.RequestDateTime
                            + token.CurrencyCode
                            + token.Amount.ToString().Replace(',', '.')
                            + token.MerchantSalesID
                            + token.Language
                            + token.TrackingCode
                            + token.ExpirationTime
                            + token.TransactionOkURL
                            + token.TransactionErrorURL
                            + signatureKey;
            // Create a SHA256
            using (var sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string
                var builder = new StringBuilder();
                for (var i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static string ComputeSha256Hash(NotifcationResponse token, string signatureKey)
        {
            var rawData = token.ResponseDateTime
                            + token.MerchantSalesID
                            + token.ReferenceNo
                            + token.CreationDateTime
                            + token.Amount.ToString().Replace(',', '.')
                            + token.CurrencyId
                            + token.PaymentReferenceNo
                            + token.Status
                            + token.OrderNo
                            + signatureKey;
            // Create a SHA256
            using (var sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string
                var builder = new StringBuilder();
                for (var i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static string ComputeSha256Hash(NotificationRequest token, string signatureKey)
        {
            var rawData = token.RequestDateTime
                            + token.MerchantSalesID
                            + token.ReferenceNo
                            + token.CreationDateTime
                            + token.Amount.ToString().Replace(',', '.')
                            + token.CurrencyId
                            + token.PaymentReferenceNo
                            + token.StatusCode
                            + signatureKey;
            // Create a SHA256
            using (var sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string
                var builder = new StringBuilder();
                for (var i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static string ComputeSha256Hash(OperationActivityRequest token, string signatureKey)
        {
            var rawData = token.RequestDateTime
                         + token.MerchantSalesID
                         + signatureKey;

            // Create a SHA256
            using (var sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string
                var builder = new StringBuilder();
                for (var i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        #endregion ComputeSha256Hash

        #region Mapping

        public static ExpressTokenResponse ToExpressTokenResponse(string tokenResponse)
        {
            try
            {
                var response = tokenResponse.Split(',');
                int.TryParse(response[0], out var errorCode);

                var expressTokenResponse = new ExpressTokenResponse
                {
                    ResponseDateTime = response[1],
                    ClientRedirectURL = response[2],
                    Signature = response[3],
                    ErrorCode = response[0]
                };

                return expressTokenResponse;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string ToOperationActivityResponse(string tokenResponse, string merchantSalesId)
        {
            try
            {
                var operationCode = "";
                var response = tokenResponse.Split('\n');
                var posMerchantSalesID = 5;
                var posOperationCode = 12;
                foreach (var line in response)
                {
                    var item = line.Split(',');
                    if (item.Length > posMerchantSalesID)
                    {
                        if (merchantSalesId == item[posMerchantSalesID])
                        {
                            if (item.Length > posOperationCode)
                            {
                                operationCode = item[posOperationCode];
                                return operationCode;
                            }
                        }
                    }
                }
                return operationCode;
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion Mapping
    }
}