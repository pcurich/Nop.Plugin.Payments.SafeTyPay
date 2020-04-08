using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.SafeTyPay.Models;
using Nop.Plugin.Payments.SafeTyPay.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.SafeTyPay.Controllers
{
    public class PaymentSafeTyPayController : BasePaymentController
    {
        #region Fields

        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IPermissionService _permissionService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly INotificationService _notificationService;
        private readonly INotificationRequestService _notificationRequestService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly ShoppingCartSettings _shoppingCartSettings;

        #endregion Fields

        #region Ctor

        public PaymentSafeTyPayController(
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentPluginManager paymentPluginManager,
            IPermissionService permissionService,
            ILocalizationService localizationService,
            ILogger logger,
            INotificationService notificationService,
            ISettingService settingService,
            IStoreContext storeContext,
            IWebHelper webHelper,
            IWorkContext workContext,
            INotificationRequestService notificationRequestService,
            ShoppingCartSettings shoppingCartSettings)
        {
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _paymentPluginManager = paymentPluginManager;
            _permissionService = permissionService;
            _localizationService = localizationService;
            _logger = logger;
            _notificationService = notificationService;
            _settingService = settingService;
            _storeContext = storeContext;
            _webHelper = webHelper;
            _workContext = workContext;
            _shoppingCartSettings = shoppingCartSettings;
            _notificationRequestService = notificationRequestService;
        }

        #endregion Ctor

        #region Utilities

        protected virtual void ProcessRecurringPayment(string invoiceId, PaymentStatus newPaymentStatus, string transactionId, string ipnInfo)
        {
            Guid orderNumberGuid;

            try
            {
                orderNumberGuid = new Guid(invoiceId);
            }
            catch
            {
                orderNumberGuid = Guid.Empty;
            }

            var order = _orderService.GetOrderByGuid(orderNumberGuid);
            if (order == null)
            {
                _logger.Error("PayPal IPN. Order is not found", new NopException(ipnInfo));
                return;
            }

            var recurringPayments = _orderService.SearchRecurringPayments(initialOrderId: order.Id);

            foreach (var rp in recurringPayments)
            {
                switch (newPaymentStatus)
                {
                    case PaymentStatus.Authorized:
                    case PaymentStatus.Paid:
                        {
                            var recurringPaymentHistory = rp.RecurringPaymentHistory;
                            if (!recurringPaymentHistory.Any())
                            {
                                //first payment
                                var rph = new RecurringPaymentHistory
                                {
                                    RecurringPaymentId = rp.Id,
                                    OrderId = order.Id,
                                    CreatedOnUtc = DateTime.UtcNow
                                };
                                rp.RecurringPaymentHistory.Add(rph);
                                _orderService.UpdateRecurringPayment(rp);
                            }
                            else
                            {
                                //next payments
                                var processPaymentResult = new ProcessPaymentResult
                                {
                                    NewPaymentStatus = newPaymentStatus
                                };
                                if (newPaymentStatus == PaymentStatus.Authorized)
                                    processPaymentResult.AuthorizationTransactionId = transactionId;
                                else
                                    processPaymentResult.CaptureTransactionId = transactionId;

                                _orderProcessingService.ProcessNextRecurringPayment(rp,
                                    processPaymentResult);
                            }
                        }

                        break;

                    case PaymentStatus.Voided:
                        //failed payment
                        var failedPaymentResult = new ProcessPaymentResult
                        {
                            Errors = new[] { $"PayPal IPN. Recurring payment is {nameof(PaymentStatus.Voided).ToLower()} ." },
                            RecurringPaymentFailed = true
                        };
                        _orderProcessingService.ProcessNextRecurringPayment(rp, failedPaymentResult);
                        break;
                }
            }

            //OrderService.InsertOrderNote(newOrder.OrderId, sb.ToString(), DateTime.UtcNow);
            _logger.Information("PayPal IPN. Recurring info", new NopException(ipnInfo));
        }

        protected virtual void ProcessPayment(string orderNumber, string ipnInfo, PaymentStatus newPaymentStatus, decimal mcGross, string transactionId)
        {
            Guid orderNumberGuid;

            try
            {
                orderNumberGuid = new Guid(orderNumber);
            }
            catch
            {
                orderNumberGuid = Guid.Empty;
            }

            var order = _orderService.GetOrderByGuid(orderNumberGuid);

            if (order == null)
            {
                _logger.Error("PayPal IPN. Order is not found", new NopException(ipnInfo));
                return;
            }

            //order note
            order.OrderNotes.Add(new OrderNote
            {
                Note = ipnInfo,
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });

            _orderService.UpdateOrder(order);

            //validate order total
            if ((newPaymentStatus == PaymentStatus.Authorized || newPaymentStatus == PaymentStatus.Paid) && !Math.Round(mcGross, 2).Equals(Math.Round(order.OrderTotal, 2)))
            {
                var errorStr = $"PayPal IPN. Returned order total {mcGross} doesn't equal order total {order.OrderTotal}. Order# {order.Id}.";
                //log
                _logger.Error(errorStr);
                //order note
                order.OrderNotes.Add(new OrderNote
                {
                    Note = errorStr,
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });
                _orderService.UpdateOrder(order);

                return;
            }

            switch (newPaymentStatus)
            {
                case PaymentStatus.Authorized:
                    if (_orderProcessingService.CanMarkOrderAsAuthorized(order))
                        _orderProcessingService.MarkAsAuthorized(order);
                    break;

                case PaymentStatus.Paid:
                    if (_orderProcessingService.CanMarkOrderAsPaid(order))
                    {
                        order.AuthorizationTransactionId = transactionId;
                        _orderService.UpdateOrder(order);

                        _orderProcessingService.MarkOrderAsPaid(order);
                    }

                    break;

                case PaymentStatus.Refunded:
                    var totalToRefund = Math.Abs(mcGross);
                    if (totalToRefund > 0 && Math.Round(totalToRefund, 2).Equals(Math.Round(order.OrderTotal, 2)))
                    {
                        //refund
                        if (_orderProcessingService.CanRefundOffline(order))
                            _orderProcessingService.RefundOffline(order);
                    }
                    else
                    {
                        //partial refund
                        if (_orderProcessingService.CanPartiallyRefundOffline(order, totalToRefund))
                            _orderProcessingService.PartiallyRefundOffline(order, totalToRefund);
                    }

                    break;

                case PaymentStatus.Voided:
                    if (_orderProcessingService.CanVoidOffline(order))
                        _orderProcessingService.VoidOffline(order);

                    break;
            }
        }

        protected virtual NotificationRequest ProcessNotifcationRequest(string strRequest)
        {
            NotificationRequest notification = null;
            try
            {
                var token = strRequest.Split('&');
                notification = new NotificationRequest();

                foreach (var t in token)
                {
                    var item = t.Split("=");
                    switch (item[0])
                    {
                        case "ApiKey":
                            notification.ApiKey = item[1];
                            break;

                        case "RequestDateTime":
                            notification.RequestDateTime = item[1];
                            break;

                        case "MerchantSalesID":
                            notification.MerchantSalesID = item[1];
                            break;

                        case "ReferenceNo":
                            notification.ReferenceNo = item[1];
                            break;

                        case "CreationDateTime":
                            notification.CreationDateTime = item[1];
                            break;

                        case "Amount":
                            notification.Amount = decimal.Parse(item[1], CultureInfo.InvariantCulture);
                            break;

                        case "CurrencyID":
                            notification.CurrencyId = item[1];
                            break;

                        case "PaymentReferenceNo":
                            notification.PaymentReferenceNo = item[1];
                            break;

                        case "Status":
                            notification.StatusCode = item[1];
                            break;

                        case "Signature":
                            notification.Signature = item[1];
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.InnerException.ToString());
            }
            return notification;
        }

        #endregion Utilities

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var safeTyPayPaymentSettings = _settingService.LoadSetting<SafeTyPayPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                UseSandbox = safeTyPayPaymentSettings.UseSandbox,
                ExpirationTime = safeTyPayPaymentSettings.ExpirationTime,
                AdditionalFee = safeTyPayPaymentSettings.AdditionalFee,
                AdditionalFeePercentage = safeTyPayPaymentSettings.AdditionalFeePercentage,

                UserNameMMS = safeTyPayPaymentSettings.UserNameMMS,
                PasswordMMS = safeTyPayPaymentSettings.PasswordMMS,
                PasswordTD = safeTyPayPaymentSettings.PasswordTD,

                ApiKey = safeTyPayPaymentSettings.ApiKey,
                SignatureKey = safeTyPayPaymentSettings.SignatureKey,
                CanGenerateNewCode = safeTyPayPaymentSettings.CanGenerateNewCode,

                ActiveStoreScopeConfiguration = storeScope
            };

            model.PendingPaymen = new Dictionary<string, string>();

            var data = _notificationRequestService.GetAllNotificationRequestTemp();
            foreach (var d in data)
            {
                if (d.Origin!=null && d.Origin.Length > 0 && d.CreationDateTime!=null)
                    model.PendingPaymen.Add(d.CreationDateTime!=null? d.CreationDateTime:"--/--/---- --:--:--", d.Origin!=null? d.Origin:"--------------------");
            }

            if (storeScope <= 0)
                return View("~/Plugins/Payments.SafeTyPay/Views/Configure.cshtml", model);

            model.UseSandbox_OverrideForStore = _settingService.SettingExists(safeTyPayPaymentSettings, x => x.UseSandbox, storeScope);
            model.CanGenerateNewCode_OverrideForStore = _settingService.SettingExists(safeTyPayPaymentSettings, x => x.CanGenerateNewCode, storeScope);
            model.UserNameMMS_OverrideForStore = _settingService.SettingExists(safeTyPayPaymentSettings, x => x.UserNameMMS, storeScope);
            model.PasswordMMS_OverrideForStore = _settingService.SettingExists(safeTyPayPaymentSettings, x => x.PasswordMMS, storeScope);
            model.PasswordTD_OverrideForStore = _settingService.SettingExists(safeTyPayPaymentSettings, x => x.PasswordTD, storeScope);
            model.ApiKey_OverrideForStore = _settingService.SettingExists(safeTyPayPaymentSettings, x => x.ApiKey, storeScope);
            model.SignatureKey_OverrideForStore = _settingService.SettingExists(safeTyPayPaymentSettings, x => x.SignatureKey, storeScope);
            model.ExpirationTime_OverrideForStore = _settingService.SettingExists(safeTyPayPaymentSettings, x => x.ExpirationTime, storeScope);
            model.AdditionalFee_OverrideForStore = _settingService.SettingExists(safeTyPayPaymentSettings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(safeTyPayPaymentSettings, x => x.AdditionalFeePercentage, storeScope);

            

            return View("~/Plugins/Payments.SafeTyPay/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AdminAntiForgery]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var safeTyPayPaymentSettings = _settingService.LoadSetting<SafeTyPayPaymentSettings>(storeScope);

            //save settings
            safeTyPayPaymentSettings.UseSandbox = model.UseSandbox;
            safeTyPayPaymentSettings.CanGenerateNewCode = model.CanGenerateNewCode;
            safeTyPayPaymentSettings.UserNameMMS = model.UserNameMMS;
            safeTyPayPaymentSettings.PasswordMMS = model.PasswordMMS;
            safeTyPayPaymentSettings.PasswordTD = model.PasswordTD;
            safeTyPayPaymentSettings.ApiKey = model.ApiKey;
            safeTyPayPaymentSettings.SignatureKey = model.SignatureKey;
            safeTyPayPaymentSettings.ExpirationTime = model.ExpirationTime;
            safeTyPayPaymentSettings.AdditionalFee = model.AdditionalFee;
            safeTyPayPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(safeTyPayPaymentSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(safeTyPayPaymentSettings, x => x.CanGenerateNewCode, model.CanGenerateNewCode_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(safeTyPayPaymentSettings, x => x.UserNameMMS, model.UserNameMMS_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(safeTyPayPaymentSettings, x => x.PasswordMMS, model.PasswordMMS_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(safeTyPayPaymentSettings, x => x.PasswordTD, model.PasswordTD_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(safeTyPayPaymentSettings, x => x.ApiKey, model.ApiKey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(safeTyPayPaymentSettings, x => x.SignatureKey, model.SignatureKey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(safeTyPayPaymentSettings, x => x.ExpirationTime, model.ExpirationTime_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(safeTyPayPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(safeTyPayPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

            _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [HttpPost]
        public IActionResult SafeTyPayAutomaticNotification()
        {
            byte[] parameters;
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var safeTyPayPaymentSettings = _settingService.LoadSetting<SafeTyPayPaymentSettings>(storeScope);

            using (var stream = new MemoryStream())
            {
                Request.Body.CopyTo(stream);
                parameters = stream.ToArray();
            }

            var request = ProcessNotifcationRequest(Encoding.ASCII.GetString(parameters));

            var notification = _notificationRequestService.GetNotificationRequestByMerchanId(new Guid(request.MerchantSalesID));
            var domain = request.ToDomain();
            if (notification != null)
            {
                notification.RequestDateTime = domain.RequestDateTime;
                notification.ReferenceNo = domain.ReferenceNo;
                notification.CreationDateTime = domain.CreationDateTime;
                notification.Amount = domain.Amount;
                notification.CurrencyId = domain.CurrencyId;
                notification.PaymentReferenceNo = domain.PaymentReferenceNo;
                notification.StatusCode = domain.StatusCode;
                notification.Signature = domain.Signature;
                notification.Origin = Encoding.ASCII.GetString(parameters);
                _notificationRequestService.UpdateNotificationRequest(notification);
            }
            else
            {
                _notificationRequestService.InsertNotificationRequest(request.ToDomain());
            }
            //check signature from equest
            var result = request.IsValid(safeTyPayPaymentSettings.SignatureKey);
            var response = request.ToResponse(safeTyPayPaymentSettings.SignatureKey);

            var csvResponse = response.ToParameter();
            return Content(csvResponse);
        }

        #endregion Methods
    }
}