using System;
using System.Net;
using Nop.Core;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.SafeTyPay.Domain;
using Nop.Plugin.Payments.SafeTyPay.Infrastructure;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Tasks;

namespace Nop.Plugin.Payments.SafeTyPay.Services
{
    /// <summary>
    /// Represents the HTTP client to request safetypay services
    /// </summary>
    public partial class SynchronizationTask : IScheduleTask
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly INotificationRequestService _notification;
        private readonly IOrderService _orderService;
        private readonly ILogger _logger;
        private readonly IStoreContext _storeContext;
        private readonly ISettingService _settingService;
        private readonly IEmailAccountService _emailAccountService;
        private readonly ICurrencyService _currencyService;
        private readonly EmailAccountSettings _emailAccountSettings;
        private readonly IWorkContext _workContext;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ICustomerService _customerService;
        private readonly SafeTyPayHttpClient _safeTyPayHttpClient;
        private readonly SafeTyPayPaymentSettings _safeTyPayPaymentSettings;
        private readonly IOrderProcessingService _orderProcessingService;

        #endregion Fields

        #region Ctor

        public SynchronizationTask(
            IWorkContext workContext, IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            ILogger logger, IStoreContext storeContext, ISettingService settingService,
            INotificationRequestService notification, IEmailAccountService emailAccountService,
            IOrderService orderService, EmailAccountSettings emailAccountSettings, ICurrencyService currencyService,
            ICustomerService customerService,
            SafeTyPayHttpClient safeTyPayHttpClient,
            SafeTyPayPaymentSettings safeTyPayPaymentSettings,
            IOrderProcessingService orderProcessingService
            )
        {
            _notification = notification;
            _orderService = orderService;
            _storeContext = storeContext;
            _settingService = settingService;
            _workContext = workContext;
            _emailAccountService = emailAccountService;
            _emailAccountSettings = emailAccountSettings;
            _currencyService = currencyService;
            _genericAttributeService = genericAttributeService;
            _localizationService = localizationService;
            _logger = logger;
            _customerService = customerService;
            _safeTyPayHttpClient = safeTyPayHttpClient;
            _safeTyPayPaymentSettings = safeTyPayPaymentSettings;
            _orderProcessingService = orderProcessingService;
        }

        #endregion Ctor

        #region Methods

        /// <summary>
        /// Execute task
        /// </summary>
        public void Execute()
        {
            var safety = _notification.GetAllNotificationRequestTemp();
            foreach (var sf in safety)
            {
                var order = _orderService.GetOrderByGuid(new Guid(sf.MerchantSalesID));
                try
                {
                    //Transaction Expired - Expired
                    if (sf.StatusCode == "100")
                    {
                        if (_safeTyPayPaymentSettings.CanGenerateNewCode)
                            Case100(order, sf);
                        else
                            Case120(order);
                    }
                    //Purchase Complete - Paid
                    if (sf.StatusCode == "102")
                    {
                        Case102(order, sf);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(string.Format(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.SchedulerTask.Execute"), ex.StackTrace));
                }
            }
        }

        #endregion Methods

        #region Util

        /// <summary>
        /// Case 100 = Expired Operation Code
        /// </summary>
        /// <param name="order"></param>
        /// <param name="sf"></param>
        private void Case100(Order order, NotificationRequestSafeTyPay sf)
        {
            var newOrderGuid = Guid.NewGuid();

            #region ExpressToken

            var requestExpressToken = _safeTyPayHttpClient.GetExpressTokenRequest(order.CustomerId, newOrderGuid.ToString(), order.OrderTotal);
            var strResult = WebUtility.UrlDecode(_safeTyPayHttpClient.GetExpressTokenResponse(requestExpressToken).Result)??"";
            var responseExpressToken = SafeTyPayHelper.ToExpressTokenResponse(strResult);
            if (responseExpressToken == null)
            {
                _logger.Error(string.Format(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.ResponseExpressToken"), strResult));
            }
            else
            {
                #region request simulator

                var fakeResult = WebUtility.UrlDecode(_safeTyPayHttpClient.FakeHttpRequest(responseExpressToken.ClientRedirectURL).Result);

                #endregion request simulator

                #region Update Notification Table temporal

                sf.ClientRedirectURL = responseExpressToken.ClientRedirectURL;
                sf.OperationCode = fakeResult!=null && fakeResult.Length > SafeTyPayPaymentProcessor.FAKE_RESULT_LENGTH;

                #endregion Update Notification Table temporal

                #region AddNotes
                _orderService.InsertOrderNote(new OrderNote
                {
                    OrderId = order.Id,
                    Note = string.Format(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew1"), order.AuthorizationTransactionResult),
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
                _orderService.InsertOrderNote(new OrderNote
                {
                    OrderId = order.Id,
                    Note = string.Format(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew2"), order.AuthorizationTransactionResult),
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
                _orderService.InsertOrderNote(new OrderNote
                {
                    OrderId = order.Id,
                    Note = _localizationService.GetResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew3"),
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
                #endregion AddNotes

                #region Notification Token

                var requestNotificationToken = _safeTyPayHttpClient.GetNotificationRequest(newOrderGuid.ToString());
                strResult = WebUtility.UrlDecode(_safeTyPayHttpClient.GetNotificationResponse(requestNotificationToken).Result)??"";
                var responseNotificationToken = SafeTyPayHelper.ToOperationActivityResponse(strResult, newOrderGuid.ToString());
                if (responseNotificationToken == null)
                {
                    _logger.Error(string.Format(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.ResponseOperationToken"), strResult));
                }
                else
                {
                    _orderService.InsertOrderNote(new OrderNote
                    {
                        OrderId = order.Id,
                        Note = string.Format(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.SendRequestExpiredNew4"), responseNotificationToken),
                        DisplayToCustomer = false,
                        CreatedOnUtc = DateTime.UtcNow
                    });
                    order.OrderGuid = newOrderGuid;

                    #region newOrderGuid
                    order.AuthorizationTransactionResult = responseNotificationToken;
                    order.AuthorizationTransactionCode = responseNotificationToken;
                    order.AuthorizationTransactionId = responseExpressToken.ClientRedirectURL;
                    sf.PaymentReferenceNo = responseNotificationToken;
                    sf.MerchantSalesID = newOrderGuid.ToString();
                    #endregion newOrderGuid

                    _orderService.UpdateOrder(order);
                    _notification.UpdateNotificationRequest(sf);

                    _logger.Information(string.Format("OrderId={0}-successful | New SafeTyPayCode-{1}", order.Id, sf.PaymentReferenceNo));
                }
                #endregion Notification Token
            }
            #endregion ExpressToken
        }

        /// <summary>
        /// Case 102 = Operation Code Paid
        /// </summary>
        /// <param name="order"></param>
        /// <param name="sf"></param>
        private void Case102(Order order, NotificationRequestSafeTyPay sf)
        {
            order.CaptureTransactionResult = sf.StatusCode;
            order.CaptureTransactionId = sf.ReferenceNo;
 
            _orderService.InsertOrderNote(new OrderNote
            {
                OrderId = order.Id,
                Note = sf.Origin,
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });
            _orderProcessingService.MarkOrderAsPaid(order);

            _logger.Information(string.Format("OrderId={0}-successful | SafeTyPayCode-{1}", order.Id, sf.PaymentReferenceNo));
            _notification.DeleteNotificationRequest(sf);
        }

        private void Case120(Order order)
        {
            _orderProcessingService.DeleteOrder(order);
        }

        #endregion Util
    }
}