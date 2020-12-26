using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Tasks;
using Nop.Core.Domain.Topics;
using Nop.Plugin.Payments.SafeTyPay.Data;
using Nop.Plugin.Payments.SafeTyPay.Domain;
using Nop.Plugin.Payments.SafeTyPay.Infrastructure;
using Nop.Plugin.Payments.SafeTyPay.Models;
using Nop.Plugin.Payments.SafeTyPay.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Tasks;
using Nop.Services.Topics;

namespace Nop.Plugin.Payments.SafeTyPay
{
    public class SafeTyPayPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        public const int FAKE_RESULT_LENGTH = 101;
        public const string PAYMENT_METHOD = "Payments.SafeTyPay";

        private readonly ILocalizationService _localizationService;
        private readonly SafeTyPayPaymentSettings _safeTyPayPaymentSettings;
        private readonly ILogger _logger;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly IStoreContext _storeContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IOrderService _orderService;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly INotificationRequestService _notification;
        private readonly SafeTyPayHttpClient _safeTyPayHttpClient;
        private readonly IPaymentService _paymentService;
        private readonly ITopicService _topicService;

        #endregion Fields

        #region Ctor

        public SafeTyPayPaymentProcessor(
            ILocalizationService localizationService,
            SafeTyPayPaymentSettings safeTyPayPaymentSettings,
            ILogger logger,
            ISettingService settingService,
            IWebHelper webHelper,
            IStoreContext storeContext,
            IHttpContextAccessor httpContextAccessor,
            IOrderService orderService,
            IScheduleTaskService scheduleTaskService, 
            INotificationRequestService notification,
            SafeTyPayHttpClient safeTyPayHttpClient,
            IPaymentService paymentService,
            ITopicService topicService
            )
        {
            _localizationService = localizationService;
            _settingService = settingService;
            _webHelper = webHelper;
            _storeContext = storeContext;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _orderService = orderService;
            _scheduleTaskService = scheduleTaskService;
            _notification = notification;
            _safeTyPayHttpClient = safeTyPayHttpClient;
            _safeTyPayPaymentSettings = safeTyPayPaymentSettings;
            _paymentService = paymentService;
            _topicService = topicService;
        }

        #endregion Ctor

        #region Methods

        /// <summary>
        /// is used in the public store to validate customer input. 
        /// It returns a list of warnings (for example, a customer did not enter his credit card name). 
        /// If your payment method does not ask the customer to enter additional information, then the 
        /// ValidatePaymentForm should return an empty list:
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        /// <summary>
        /// is used in the public store to parse customer input, such as credit card information.
        /// This method returns a ProcessPaymentRequest object with parsed customer input.
        /// If your payment method does not ask the customer to enter additional information,
        /// then GetPaymentInfo will return an empty ProcessPaymentRequest object
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// is always invoked right before a customer places an order. 
        /// Use it when you need to process a payment before an order is stored into database. 
        /// For example, capture or authorize credit card. Usually this method is used when a 
        /// customer is not redirected to third-party site for completing a payment 
        /// and all payments are handled on your site (for example, PayPal Direct).
        /// 1) Create a Request Express Token
        /// 2) Force Request to get the transactionCode
        /// 3) Delete a notification id exist (OrderGuid)
        /// 4) Insert a new Notification
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns> </returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest ppr)
        {
            var result = new ProcessPaymentResult();
            try
            {
                #region Notification 
                var notification = new NotificationRequestSafeTyPay
                {
                    ApiKey = _safeTyPayPaymentSettings.ApiKey,
                    MerchantSalesID = ppr.OrderGuid.ToString()
                };
                _notification.InsertNotificationRequest(notification);

                #endregion

                #region Build ExpressToken

                var hasToken = false;
                var numberOfAttempts = 0;
                ExpressTokenResponse responseExpressToken = null;

                while (_safeTyPayPaymentSettings.NumberOfAttemps >= numberOfAttempts && !hasToken) {
                    numberOfAttempts++;
                    var requestExpressToken = _safeTyPayHttpClient.GetExpressTokenRequest(ppr.CustomerId, ppr.OrderGuid.ToString(), ppr.OrderTotal);
                    var strResult = WebUtility.UrlDecode(_safeTyPayHttpClient.GetExpressTokenResponse(requestExpressToken).Result);
                    responseExpressToken = SafeTyPayHelper.ToExpressTokenResponse(strResult);
                    if (responseExpressToken == null)
                    {
                        _logger.Error(string.Format(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.ResponseExpressToken"), strResult));
                    }
                    else
                    {
                        hasToken = true;
                    }
                } 

                #endregion ExpressToken


                if (hasToken)
                {
                    #region request simulator

                    var fakeResult = WebUtility.UrlDecode(_safeTyPayHttpClient.FakeHttpRequest(responseExpressToken.ClientRedirectURL).Result);

                    #endregion request simulator

                    #region Update Notification in Table 

                    notification = _notification.GetNotificationRequestByMerchanId(ppr.OrderGuid);
                    if (notification != null)
                    {
                        notification.ClientRedirectURL = responseExpressToken != null ? responseExpressToken.ClientRedirectURL : "ERROR";
                        notification.OperationCode = fakeResult.Length > FAKE_RESULT_LENGTH;
                    }
                    else
                    {
                        #region Error in database
                        var t = _localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.InProcessDataBase");
                        result.Errors.Add(t);
                        _logger.Error(t);
                        #endregion Error in safetypay brocker
                    }

                    _notification.UpdateNotificationRequest(notification);

                    result = new ProcessPaymentResult
                    {
                        AuthorizationTransactionId = responseExpressToken.ClientRedirectURL,
                        AuthorizationTransactionResult = "[OPERATION-CODE]"
                    };

                    #endregion Update Notification in Table
                }
                else
                {
                    #region Error in safetypay brocker
                    var t = _localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.InProcess");
                    result.Errors.Add(t);
                    _logger.Error(t);
                    #endregion Error in safetypay brocker
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.ProcessPayment"), ex.StackTrace.ToString()));
            }
            return result;
        }

        /// <summary>
        /// is invoked right after a customer places an order. 
        /// Usually this method is used when you need to redirect a customer to a 
        /// third-party site for completing a payment (for example, PayPal Standard)
        /// 1) Request the Operation Code with the especific OrderGuid
        /// </summary>
        /// <param name="postProcessPaymentRequest"></param>
        public void PostProcessPayment(PostProcessPaymentRequest pppr)
        {
            try
            {
                var order = pppr.Order;
                order.PaymentMethodSystemName = PAYMENT_METHOD;

                #region Check OperationCode

                var tmp = _notification.GetNotificationRequestByMerchanId(order.OrderGuid);
                var numberOfAttempts = 0;

                while (_safeTyPayPaymentSettings.NumberOfAttemps >= numberOfAttempts)
                {
                    numberOfAttempts++;
                    if (tmp == null)
                    {
                        tmp = _notification.GetNotificationRequestByMerchanId(order.OrderGuid);
                    }
                    else
                    {
                        break;
                    }
                }

                if (!tmp.OperationCode)
                {
                    var fakeResult = WebUtility.UrlDecode(_safeTyPayHttpClient.FakeHttpRequest(order.AuthorizationTransactionId).Result);
                    tmp.OperationCode = fakeResult.Length > FAKE_RESULT_LENGTH;
                }

                #endregion Check OperationCode

                #region Notification Token

                var requestNotificationToken = _safeTyPayHttpClient.GetNotificationRequest(order.OrderGuid.ToString());
                var strResult = WebUtility.UrlDecode(_safeTyPayHttpClient.GetNotificationResponse(requestNotificationToken).Result);
                var responseNotificationToken = SafeTyPayHelper.ToOperationActivityResponse(strResult, order.OrderGuid.ToString());
                if (responseNotificationToken == null)
                {
                    _logger.Error(string.Format(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.ResponseOperationToken"), strResult));
                }

                #endregion Notification Token

                order.AuthorizationTransactionResult = responseNotificationToken;
                order.AuthorizationTransactionCode = responseNotificationToken;
                tmp.PaymentReferenceNo = responseNotificationToken;

                _httpContextAccessor.HttpContext.Response.Redirect(tmp.ClientRedirectURL);
                _orderService.InsertOrderNote(new OrderNote
                {
                    OrderId = order.Id,
                    Note = string.Format(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Note.SendRequest"), responseNotificationToken),
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                _orderService.UpdateOrder(order);
                _notification.UpdateNotificationRequest(tmp);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.PostProcessPayment"), ex.StackTrace.ToString()));
                _httpContextAccessor.HttpContext.Response.Redirect(_storeContext.CurrentStore.Url);
            }

            return;
        }

        /// <summary>
        /// You can put any logic here. For example, hide this payment method
        /// if all products in the cart are downloadable. 
        /// Or hide this payment method if current customer is from certain country
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            if (_safeTyPayPaymentSettings.ApiKey.Length > 0 && _safeTyPayPaymentSettings.SignatureKey.Length > 0)
                return false;
            return true;
        }

        /// <summary>
        /// You can return any additional handling fees which will be added to an order total
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            //return _paymentService.CalculateAdditionalFee(cart,
            //    _safeTyPayPaymentSettings.AdditionalFee, _safeTyPayPaymentSettings.AdditionalFeePercentage);
            return 0M;
        }

        /// <summary>
        /// Some payment gateways allow you to authorize payments before they're captured. 
        /// It allows store owners to review order details before the payment is actually done.
        /// In this case you just authorize a payment in ProcessPayment or PostProcessPayment method (described above),
        /// and then just capture it. In this case a Capture button will be visible on the order details page in admin area.
        /// Note that an order should be already authorized and SupportCapture property should return true.
        /// </summary>
        /// <param name="capturePaymentRequest"></param>
        /// <returns></returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        /// <summary>
        /// This method allows you make a refund. 
        /// In this case a Refund button will be visible on the order details page in admin area. 
        /// Note that an order should be paid, and SupportRefund or SupportPartiallyRefund 
        /// property should return true.
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { _localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.Refund") } };
        }

        /// <summary>
        /// This method allows you void an authorized but not captured payment.
        /// In this case a Void button will be visible on the order details page in admin area. 
        /// Note that an order should be authorized and SupportVoid property should return true.
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { _localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.Void") } };
        }

        /// <summary>
        /// Use this method to process recurring payments.
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { _localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.RecurringPayment") } };
        }

        /// <summary>
        /// Use this method to cancel recurring payments.
        /// </summary>
        /// <param name="cancelPaymentRequest"></param>
        /// <returns></returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Usually this method is used when it redirects a customer to a third-party site for completing a payment.
        /// If the third party payment fails this option will allow customers to attempt the order again later without placing a new order.
        /// CanRePostProcessPayment should return true to enable this feature
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }



        /// <summary>
        /// This method should return a url of its Configure method
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentSafeTyPay/Configure";
        }

        /// <summary>
        /// This method should return the name of the view component which used to display public information for customers
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return "PaymentSafeTyPay";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new SafeTyPayPaymentSettings
            {
                UseSandbox = SafeTyPayDefaults.UseSandbox,
                ExpirationTime = SafeTyPayDefaults.ExpirationTime,

                AdditionalFee = SafeTyPayDefaults.AdditionalFee,
                AdditionalFeePercentage = SafeTyPayDefaults.AdditionalFeePercentage,

                TransactionOkURL = SafeTyPayDefaults.TransactionOkURL,
                TransactionErrorURL = SafeTyPayDefaults.TransactionErrorURL,

                NumberOfAttemps = SafeTyPayDefaults.NumberOfAttemps,

                UserNameMMS = "",
                PasswordMMS = "",

                PasswordTD = "",

                ApiKey = "",
                SignatureKey = "",
                ExpressTokenUrl = SafeTyPayDefaults.ExpressTokenUrl,
                NotificationUrl = SafeTyPayDefaults.NotificationUrl,
            });

            //ScheduleTask
            var task = new ScheduleTask
            {
                Name = SafeTyPayDefaults.SynchronizationTaskName,
                //60 minutes
                Seconds = SafeTyPayDefaults.SynchronizationPeriod,
                Type = SafeTyPayDefaults.SynchronizationTaskType,
                Enabled = SafeTyPayDefaults.SynchronizationEnabled,
                StopOnError = SafeTyPayDefaults.SynchronizationStopOnError
            };
            _scheduleTaskService.InsertTask(task);

            //topic
            var topic = new Topic
            {
                Title = "SafeTyPayError",
                SystemName = "SafeTyPayError",
                DisplayOrder = 1,
                Body = "<p>We had some problems when processing your payment. Please try again or contact <a href='mailto:support@safetypay.com'>support@safetypay.com</a>  to help you.</p>"
            };

            _topicService.InsertTopic(topic);

            //locales
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Configure", "Configure", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Configure", "Configuración", "es-ES");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.PaymentPending", "Payment Pending", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.PaymentPending", "Pago Pendiente", "es-ES");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.General", "General", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.General", "General", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.UseSandbox", "Use Sandbox","en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.UseSandbox", "Use Sandbox","es-ES");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.UseSandbox.Hint", "Check to enable development mode.", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.UseSandbox.Hint", "Marque para habilitar el modo de desarrollo.", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.ExpirationTime", "Duration", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.ExpirationTime", "Duración", "es-ES");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.ExpirationTime.Hint", "Time in minutes to expire the operation code by safetypay.Value given in minutes: 90, 60, 1440, 30, etc", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.ExpirationTime.Hint", "Tiempo en minutos para expirar el código de operación provisto por safetypay. Valor dado en minutos: 90, 60, 1440, 30, etc.", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFee", "Additional fee (Value)", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFee", "Cuota Adicional (Valor)", "es-ES");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFee.Hint", "Ingrese una tarifa adicional para cobrar a sus clientes.", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.NumberOfAttemps", "Number of Attemps", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.NumberOfAttemps", "Número de intentos", "es-ES");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.NumberOfAttemps.Hint", "Enter a number of attemps if safetypasy server is bussy.", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.NumberOfAttemps.Hint", "Ingrese un numero de intentos ante un problema de saturación del servidor de safetypay.", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFeePercentage", "Additional fee (%)", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFeePercentage", "Cuota Adicional (%)", "es-ES");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFeePercentage.Hint", "Enter percentage additional fee to charge your customers.", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFeePercentage.Hint", "Ingrese un procentaje adicional para cobrar a sus clientes.", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Merchant.Management.System", "Merchant Management System", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Merchant.Management.System", "Sistema de Gestión Comercial", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.UserNameMMS", "UserName", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.UserNameMMS", "Usuario", "es-ES");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.UserNameMMS.Hint", "Save UserName access", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.UserNameMMS.Hint", "Guardar nombre de usuario", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordMMS", "Password", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordMMS", "Contraseña", "es-ES");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordMMS.Hint", "Save secret Password", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordMMS.Hint", "Guardar contraseña secreta", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Technical.Documentation", "Technical Documentation", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Technical.Documentation", "Documentación Técnica", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordTD", "Password", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordTD", "Contraseña", "es-ES");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordTD.Hint", "Save secret Password", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordTD.Hint", "Guardar contraseña secreta", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Environment", "Environment", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Environment", "Entorno", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.ApiKey", "Api Key", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.ApiKey", "Api Key", "es-ES");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.ApiKey.Hint", "the ApiKey", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.ApiKey.Hint", "La llave de la Api", "es-ES");
            
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.SignatureKey", "Signature Key", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.SignatureKey", "Signature Key", "es-ES");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.SignatureKey.Hint", "The Signature Key", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.SignatureKey.Hint", "The Signature Key", "es-ES");


             
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Note.Case102", "Payment completed successfully with reference Code {0}", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Note.Case102", "Pago completado con éxito con el código de referencia {0}", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequest", "Send to SafetyPay for the code Operation {0}", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequest", "Se envio a SafetyPay por el codigo de operación {0}", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew1", "The code {0} was expired by SafetyPay", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew1", "El código {0}  fue caducado por SafetyPay", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew2", "The order with the operation code has expired {0}", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew2", "La orden con el código de operación {0} ha vencido", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew3", "System  request new code to SafeTyPay", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew3", "El sistema solicita un nuevo código a SafeTyPay", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew4", "SafetyPay send new operation code {0} to System", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew4", "SafetyPay send new operation code {0} to System", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.RedirectionTip", "You will be redirected to the SafetyPay site to obtain the operation number", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Fields.RedirectionTip", "Será redirigido al sitio de SafetyPay para obtener el numero de operación", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.PaymentMethodDescription", "Code generated by SafeTyPay", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.PaymentMethodDescription", "Código geneado por SafeTyPay", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.ResponseExpressToken", "Error in Response Express Token {0}", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.ResponseExpressToken", "Error la respuesta del Express Token {0}", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.ResponseOperationToken", "Error in Response Opeation Token {0}", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.ResponseOperationToken", "Error en la respuesta de notificacion Token {0}", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.ProcessPayment", "Error in the Process Payment Executed {0}", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.ProcessPayment", "Error en la ejecución Process Payment {0}", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.InProcess", "Error in the Process Payment with safetypay, try later", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.InProcess", "Error en la ejecución del Proceso de pago, intente en unos minutos", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.InProcessDataBase", "Error internal, try later", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.InProcessDataBase", "Sucedio un error interno, intente en unos minutos", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.PostProcessPayment", "Error in the Post Process Payment Executed {0}", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.PostProcessPayment", "Error en la ejecución Post Process Payment {0}", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.SchedulerTask.Execute", "Error in the executed task {0}", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.SchedulerTask.Execute", "Error en la ejecución de la tarea {0}", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.RecurringPayment", "Recurring payment not supported", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.RecurringPayment", "Pago recurrentes no soportados ", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.Refund", "Refund method not supported", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.Refund", "Método de reembolso no admitido", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.Void", "Void method not supported", "en-US");
            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Error.Void", "Método vacío no admitido", "es-ES");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Instructions", @"
            <p> If you are using this gateway, please make sure that SafeTyPay supports the currency of your main store. </p>
            <p> The <strong> general </strong> fields have the purpose of storing the credentials granted by SafeTyPay that give access to the <strong> Commercial Management System </strong> by means of a username and password which are sent by email. once the commercial management with the company has started. </p>
            <p> The password of the <strong> technical documentation </strong> provides access to the documentary portal that this company provides. </p>
            <p> In the <strong> Commercial Management System </strong> provided by SafeTyPay, go to the profile option located in the upper right, then in the left side menu select the credentials option there will generate the <strong> APIKEY </strong> and the <strong> SIGNATUREKEY </strong> that the system requires. </p>
            <p> Only in the case of development, In the Commercial Management System provided by SafeTyPay, go to the profile option located in the upper right, then in the left side menu select the option of notifications, enter an email so that you receive the notifications that SafeTyPay sends and in the <strong> PostUrl </strong> put the following value (" + _webHelper.GetStoreLocation() + @" Plugins/PaymentSafeTyPay/SafeTyPayAutomaticNotification). Also check the boxes for <strong> POST / WS and Email. </strong> </p>
            <p> The payments that SafeTyPay reports are <strong> asynchronous </strong>, this means that payment notifications are stored in the system and subsequently synchronized by means of a scheduled task that synchronizes payments from time to time, configurable in the next path to <a href='" + _webHelper.GetStoreLocation() + @"Admin/ScheduleTask/List'> scheduled tasks </a> </p>
            <p> To find out if you have notifications sent by SafeTyPay that have not yet been processed, <strong> see the section below </strong> </p>", "en-US");

            _localizationService.AddOrUpdateLocaleResource("Plugins.Payments.SafeTyPay.Instructions", @"
            <p>Si está utilizando esta puerta de enlace, asegúrese de que SafeTyPay admita la moneda de su tienda principal.</p>
            <p>Los campos <strong>generales</strong> tienen la finalidad de almacenar las credenciales otorgadas por SafeTyPay que dan acceso al <strong>Sistema de Gestión Comercial</strong> mediante un usuario y contraseña los cuales son enviado por correo una vez iniciada la gestión comercial con la empresa.</p>
            <p>La contraseña de la <strong>documentación técnica</strong> brinda el acceso al portal documentario que esta empresa brinda.</p>
            <p>En el <strong>Sistema de Gestión Comercial</strong> provisto por SafeTyPay, diríjase a la opción de perfil ubicada en la parte superior derecha, luego en el menú lateral de la izquierda seleccione la opción de credenciales ahí va a generar el <strong>APIKEY</strong> y el <strong>SIGNATUREKEY</strong> que el sistema requiere.</p>
            <p>Solo en el caso de desarrollo, En el Sistema de Gestión Comercial provisto por SafeTyPay, diríjase a la opción de perfil ubicada en la parte superior derecha, luego en el menú lateral de la izquierda seleccione la opción de notificaciones, ingrese un correo electrónico para que reciba las notificaciones que SafeTyPay envía y en el <strong>PostUrl</strong> coloque el siguiente valor ("+_webHelper.GetStoreLocation()+ @"Plugins/PaymentSafeTyPay/SafeTyPayAutomaticNotification). También active las casillas de <strong>POST/WS y Email.</strong></p>
            <p>Los pagos que SafeTyPay notifica son de forma <strong>asíncrona</strong>, esto quiere decir que las notificaciones de pagos son almacenadas en el sistema y posteriormente sincronizadas mediante una tarea programada que sincroniza los pagos cada cierto tiempo configurable en la siguiente ruta hacia las tareas <a href='"+_webHelper.GetStoreLocation()+ @"Admin/ScheduleTask/List'>programadas</a> </p>
            <p>Para saber si tiene notificaciones enviadas por SafeTyPay y que aún no han sido procesadas, <strong>vea el sección de abajo</strong></p>", "es-ES");
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<SafeTyPayPaymentSettings>();

            //ScheduleTask
            var task = _scheduleTaskService.GetTaskByType(SafeTyPayDefaults.SynchronizationTaskType);
            if(task!=null)
                _scheduleTaskService.DeleteTask(task);

            //Topic 
            var topic = _topicService.GetTopicBySystemName("SafeTyPayError", showHidden:true);
            if (topic  != null)
                _topicService.DeleteTopic(topic);

            //locales
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Configure");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.PaymentPending");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.General");

            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.UseSandbox");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.UseSandbox.Hint");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.ExpirationTime");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.ExpirationTime.Hint");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFee");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFee.Hint");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFeePercentage");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFeePercentage.Hint");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.NumberOfAttemps");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.NumberOfAttemps.Hint");

            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Merchant.Management.System");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.UserNameMMS");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.UserNameMMS.Hint");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordMMS");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordMMS.Hint");

            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Technical.Documentation");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordTD");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordTD.Hint");

            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Environment");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.ApiKey");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.ApiKey.Hint");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.SignatureKey");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.SignatureKey.Hint");

            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Note.Case102");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequest");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew1");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew2");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew3");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew4");

            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Fields.RedirectionTip");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.PaymentMethodDescription");

            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Error.ResponseExpressToken");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Error.ResponseOperationToken");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Error.ProcessPayment");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Error.InProcess");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Error.InProcessDataBase");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Error.PostProcessPayment");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Error.SchedulerTask.Execute");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Error.RecurringPayment");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Error.Refund");
            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Error.Void");

            _localizationService.DeleteLocaleResource("Plugins.Payments.SafeTyPay.Instructions");

        }

        #endregion Methods 

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture => false;

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund => false;

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund => false;

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid => false;

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        /// <summary>
        /// This property indicates payment method type. 
        /// Currently there are three types. 
        /// Standard used by payment methods when a customer is not redirected to a third-party site. 
        /// Redirection is used when a customer is redirected to a third-party site. And 
        /// Button is similar to Redirection payment methods. 
        /// The only difference is used that it's displayed as a button on shopping cart page (for example, Google Checkout).
        /// </summary>
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => false;

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription => _localizationService.GetResource("Plugins.Payments.SafeTyPay.PaymentMethodDescription");

        #endregion Properties

        public void Clear(Guid guid)
        {
            var  requestByMerchanId = _notification.GetNotificationRequestByMerchanId(guid);
            if (requestByMerchanId == null)
                return;
            _notification.DeleteNotificationRequest(requestByMerchanId);
        }
    }
}