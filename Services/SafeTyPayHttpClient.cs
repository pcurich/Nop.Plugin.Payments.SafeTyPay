using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Net.Http.Headers;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Messages;
using Nop.Plugin.Payments.SafeTyPay.Infrastructure;
using Nop.Plugin.Payments.SafeTyPay.Models;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Services.Messages;

namespace Nop.Plugin.Payments.SafeTyPay.Services
{
    public partial class SafeTyPayHttpClient
    {
        #region Fields

        private readonly HttpClient _httpClient;
        private readonly IEmailAccountService _emailAccountService;
        private readonly ICustomerService _customerService;
        private readonly EmailAccountSettings _emailAccountSettings;
        private readonly SafeTyPayPaymentSettings _safeTyPayPaymentSettings;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly IWebHelper _webHelper;
        private readonly ILogger _log;

        #endregion Fields

        #region Ctor

        public SafeTyPayHttpClient(HttpClient client,
            IEmailAccountService emailAccountService,
            ICustomerService customerService,
            EmailAccountSettings emailAccountSettings,
            SafeTyPayPaymentSettings safeTyPayPaymentSettings,
            IGenericAttributeService genericAttributeService,
            IStoreContext storeContext, IWorkContext workContext,
            IWebHelper webHelper, ILogger log)
        {
            //configure client
            client.Timeout = TimeSpan.FromMilliseconds(5000);
            client.DefaultRequestHeaders.Add(HeaderNames.UserAgent, $"nopCommerce-{NopVersion.CURRENT_VERSION}");

            _httpClient = client;
            _emailAccountService = emailAccountService;
            _customerService = customerService;
            _emailAccountSettings = emailAccountSettings;
            _safeTyPayPaymentSettings = safeTyPayPaymentSettings;
            _genericAttributeService = genericAttributeService;
            _storeContext = storeContext;
            _workContext = workContext;
            _webHelper = webHelper;
            _log = log;
        }

        #endregion Ctor

        #region Methods ExpressToken

        /// <summary>
        /// Get ExpressToken Response
        /// </summary>
        /// <param name="currencyCode">_workContext.WorkingCurrency.CurrencyCode.Trim()</param>
        /// <param name="languageCode">workContext.WorkingLanguage.UniqueSeoCode.Trim().ToUpper()</param>
        /// <param name="customerId">processPaymentRequest.CustomerId</param>
        /// <param name="storeName"> _storeContext.CurrentStore.Name.Trim()</param>
        /// <param name="orderGuid">processPaymentRequest.OrderGuid.ToString()</param>
        /// <param name="orderTotal">processPaymentRequest.OrderTotal</param>
        /// <returns>The asynchronous task whose result contains the PDT details</returns>
        public async Task<string> GetExpressTokenResponse(List<KeyValuePair<string, string>> postData)
        {
            var url = string.Format(_safeTyPayPaymentSettings.ExpressTokenUrl,
            _safeTyPayPaymentSettings.UseSandbox ? SafeTyPayDefaults.PrefixSandbox : "");

            try
            {
                var requestContent = new FormUrlEncodedContent(postData);
                var response = await _httpClient.PostAsync(url, requestContent);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch(Exception e) {
                _log.Error(e.Message);
            }
            return null;
        }

        /// <summary>
        /// Gets a Request Token to be send to safetypay
        /// </summary>
        /// <param name="customerId">Id of customer</param>
        /// <param name="orderGuid">code of guid order</param>
        /// <param name="orderTotal">amount total of order</param>
        /// <returns></returns>
        public List<KeyValuePair<string, string>> GetExpressTokenRequest(int customerId, string orderGuid, decimal orderTotal)
        {
            var dateTime = DateTime.Now.ToString("yyyy-MM-ddTHH\\:mm\\:ss");
            var emailAccount = _emailAccountService.GetEmailAccountById(_emailAccountSettings.DefaultEmailAccountId);
            var customer = _customerService.GetCustomerById(customerId);

            var token = new ExpressTokenRequest
            {
                ApiKey = _safeTyPayPaymentSettings.ApiKey.Trim(),
                RequestDateTime = dateTime,
                CurrencyCode = _workContext.WorkingCurrency.CurrencyCode.Trim().ToUpper(),
                Amount = orderTotal,
                MerchantSalesID = orderGuid,
                Language = _workContext.WorkingLanguage.UniqueSeoCode.Trim().ToUpper(),
                TrackingCode = orderGuid,
                ExpirationTime = _safeTyPayPaymentSettings.ExpirationTime,
                TransactionOkURL = string.Format(_safeTyPayPaymentSettings.TransactionOkURL.Trim(), _webHelper.GetStoreLocation()),
                TransactionErrorURL = _safeTyPayPaymentSettings.TransactionErrorURL.Trim(),
                CustomMerchantName = _storeContext.CurrentStore.Name,
                ShopperEmail = emailAccount.Email.Trim(),
                ProductID = 8,
                ResponseFormat = "CSV"
            };

            token.ShopperInformation_first_name = (_genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.FirstNameAttribute).Trim());
            token.ShopperInformation_last_name = (_genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.LastNameAttribute).Trim());

            var phone = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.PhoneAttribute);
            var phoneCode = "+51";
            if (phone != null && phone.Length > 0)
            {
                phone = phone.Trim();
                phone = Regex.Replace(phone.Replace(" ", ""), "[^0-9.]", "");
                if (phone.Length > 9)
                {
                    phone = phone.Substring(phone.Length - 9, 9);
                }
                else
                {
                    phone = "";
                }
                token.ShopperInformation_country_code = phoneCode;
                token.ShopperInformation_mobile = phone;
            }

            token.ShopperInformation_email = customer.Email.Trim();
            token.Signature = SafeTyPayHelper.ComputeSha256Hash(token, _safeTyPayPaymentSettings.SignatureKey);
            return token.ToParameter();
        }

        #endregion Methods ExpressToken

        #region Methods Notification

        public async Task<string> GetNotificationResponse(List<KeyValuePair<string, string>> postData)
        {
            var url = string.Format(_safeTyPayPaymentSettings.NotificationUrl,
                _safeTyPayPaymentSettings.UseSandbox ? SafeTyPayDefaults.PrefixSandbox : "");

            try
            {
                var requestContent = new FormUrlEncodedContent(postData);
                var response = await _httpClient.PostAsync(url, requestContent);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                _log.Error(e.Message);
            }
            return null;
        }

        public List<KeyValuePair<string, string>> GetNotificationRequest(string orderGuid)
        {
            var operation = new OperationActivityRequest
            {
                ApiKey = _safeTyPayPaymentSettings.ApiKey.Trim(),
                MerchantSalesID = orderGuid,
                RequestDateTime = "",
                ResponseFormat = "CSV"
            };
            operation.Signature = SafeTyPayHelper.ComputeSha256Hash(operation, _safeTyPayPaymentSettings.SignatureKey);
            return operation.ToParameter();
        }

        #endregion Methods Notification

        #region fake requets

        /// <summary>
        /// Force safetypay to create the operation number
        /// </summary>
        /// <param name="clientRedirectURL"></param>
        /// <returns></returns>
        public async Task<string> FakeHttpRequest(string clientRedirectURL)
        {
            try{
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(clientRedirectURL)
                };

                var response = Task.Run(async () => await _httpClient.SendAsync(request).ConfigureAwait(false)).Result;
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                _log.Error(e.Message);
            }
            return null;
        }

        #endregion fake requets
    }
}