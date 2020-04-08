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
        private readonly NotificationRequestTempContext _objectContext;
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
            NotificationRequestTempContext objectContext, INotificationRequestService notification,
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
            _objectContext = objectContext;
            _notification = notification;
            _safeTyPayHttpClient = safeTyPayHttpClient;
            _safeTyPayPaymentSettings = safeTyPayPaymentSettings;
            _paymentService = paymentService;
            _topicService = topicService;
        }

        #endregion Ctor

        #region Methods

        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

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

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return _paymentService.CalculateAdditionalFee(cart,
                _safeTyPayPaymentSettings.AdditionalFee, _safeTyPayPaymentSettings.AdditionalFeePercentage);
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country

            if(_safeTyPayPaymentSettings.ApiKey.Length > 0 && _safeTyPayPaymentSettings.SignatureKey.Length > 0)
                return false;

            return true;
        }

        /// <summary>
        /// Process a payment
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
                var notification = new NotificationRequestTemp
                {
                    ApiKey = _safeTyPayPaymentSettings.ApiKey,
                    MerchantSalesID = ppr.OrderGuid.ToString()
                };
                _notification.InsertNotificationRequest(notification);

                #endregion

                #region ExpressToken

                var requestExpressToken = _safeTyPayHttpClient.GetExpressTokenRequest(ppr.CustomerId, ppr.OrderGuid.ToString(), ppr.OrderTotal);
                var strResult = WebUtility.UrlDecode(_safeTyPayHttpClient.GetExpressTokenResponse(requestExpressToken).Result);
                var responseExpressToken = SafeTyPayHelper.ToExpressTokenResponse(strResult);
                if (responseExpressToken == null)
                {
                    _logger.Error(string.Format(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.ResponseExpressToken"), strResult));
                }

                #endregion ExpressToken

                #region request simulator

                var fakeResult = WebUtility.UrlDecode(_safeTyPayHttpClient.FakeHttpRequest(responseExpressToken.ClientRedirectURL).Result);

                #endregion request simulator

                #region Update Notification Table temporal

                notification = _notification.GetNotificationRequestByMerchanId(ppr.OrderGuid);
                if (notification !=null)
                {
                    notification.ClientRedirectURL = responseExpressToken!=null ? responseExpressToken.ClientRedirectURL : "error";
                    notification.OperationCode = fakeResult.Length > FAKE_RESULT_LENGTH;
                };

                _notification.UpdateNotificationRequest(notification);

                result = new ProcessPaymentResult
                {
                    AuthorizationTransactionId = responseExpressToken.ClientRedirectURL,
                    AuthorizationTransactionResult = "[OPERATION-CODE]"
                };

                #endregion Update Notification Table temporal
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.ProcessPayment"), ex.StackTrace.ToString()));
            }
            return result;
        }

        /// <summary>
        /// PostProcessPayment
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
                if (tmp == null)
                {
                    tmp =_notification.GetNotificationRequestByMerchanId(order.OrderGuid); 
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
                order.AddNote(_localizationService.GetResource("Plugins.Payments.SafeTyPay.Note.SendRequest"), responseNotificationToken);

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
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { _localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.RecurringPayment") } };
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { _localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.Refund") } };
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { _localizationService.GetResource("Plugins.Payments.SafeTyPay.Error.Void") } }; 
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentSafeTyPay/Configure";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
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
            //database objects
            _objectContext.Install();

            //settings
            _settingService.SaveSetting(new SafeTyPayPaymentSettings
            {
                UseSandbox = SafeTyPayDefaults.UseSandbox,
                ExpirationTime = SafeTyPayDefaults.ExpirationTime,

                AdditionalFee = SafeTyPayDefaults.AdditionalFee,
                AdditionalFeePercentage = SafeTyPayDefaults.AdditionalFeePercentage,

                TransactionOkURL = SafeTyPayDefaults.TransactionOkURL,
                TransactionErrorURL = SafeTyPayDefaults.TransactionErrorURL,

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
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Configure", "Configure", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Configure", "Configuración", "es-ES");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.PaymentPending", "Payment Pending", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.PaymentPending", "Pago Pendiente", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.General", "General", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.General", "General", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.UseSandbox", "Use Sandbox","en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.UseSandbox", "Use Sandbox","es-ES");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.UseSandbox.Hint", "Check to enable development mode.", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.UseSandbox.Hint", "Marque para habilitar el modo de desarrollo.", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.CanGenerateNewCode", "Can Generate New Code", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.CanGenerateNewCode", "Puede generar un nuevo código", "es-ES");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.CanGenerateNewCode.Hint", "When safetypay announces that a code has expired, the system re-requests a new code from safetypay internally and announces the change to the customer by email and updates the purchase order with the new request.", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.CanGenerateNewCode.Hint", "cuando safetypay anuncia que un código ha caducado, el sistema vuelve a solicitar un nuevo código a safetypay de forma interna y anuncia al cliente de dicho cambio mediante email y actualiza la orden de compra con la nueva petición", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.ExpirationTime", "Duration", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.ExpirationTime", "Duración", "es-ES");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.ExpirationTime.Hint", "Time in minutes to expire the operation code by safetypay.Value given in minutes: 90, 60, 1440, 30, etc", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.ExpirationTime.Hint", "Tiempo en minutos para expirar el código de operación provisto por safetypay. Valor dado en minutos: 90, 60, 1440, 30, etc.", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFee", "Additional fee (Value)", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFee", "Cuota Adicional (Valor)", "es-ES");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFee.Hint", "Ingrese una tarifa adicional para cobrar a sus clientes.", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFeePercentage", "Additional fee (%)", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFeePercentage", "Cuota Adicional (%)", "es-ES");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFeePercentage.Hint", "Enter percentage additional fee to charge your customers.", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFeePercentage.Hint", "Ingrese un procentaje adicional para cobrar a sus clientes.", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Merchant.Management.System", "Merchant Management System", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Merchant.Management.System", "Sistema de Gestión Comercial", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.UserNameMMS", "UserName", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.UserNameMMS", "Usuario", "es-ES");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.UserNameMMS.Hint", "Save UserName access", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.UserNameMMS.Hint", "Guardar nombre de usuario", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordMMS", "Password", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordMMS", "Contraseña", "es-ES");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordMMS.Hint", "Save secret Password", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordMMS.Hint", "Guardar contraseña secreta", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Technical.Documentation", "Technical Documentation", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Technical.Documentation", "Documentación Técnica", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordTD", "Password", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordTD", "Contraseña", "es-ES");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordTD.Hint", "Save secret Password", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordTD.Hint", "Guardar contraseña secreta", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Environment", "Environment", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Environment", "Entorno", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.ApiKey", "Api Key", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.ApiKey", "Api Key", "es-ES");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.ApiKey.Hint", "the ApiKey", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.ApiKey.Hint", "La llave de la Api", "es-ES");
            
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.SignatureKey", "Signature Key", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.SignatureKey", "Signature Key", "es-ES");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.SignatureKey.Hint", "The Signature Key", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.SignatureKey.Hint", "The Signature Key", "es-ES");


             
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.Case102", "Payment completed successfully with reference Code {0}", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.Case102", "Pago completado con éxito con el código de referencia {0}", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequest", "Send to SafetyPay for the code Operation {0}", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequest", "Se envio a SafetyPay por el codigo de operación {0}", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew1", "The code {0} was expired by SafetyPay", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew1", "El código {0}  fue caducado por SafetyPay", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew2", "The order with the operation code has expired {0}", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew2", "La orden con el código de operación {0} ha vencido", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew3", "System  request new code to SafeTyPay", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew3", "El sistema solicita un nuevo código a SafeTyPay", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew4", "SafetyPay send new operation code {0} to System", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew4", "SafetyPay send new operation code {0} to System", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.RedirectionTip", "You will be redirected to the SafetyPay site to obtain the operation number", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.RedirectionTip", "Será redirigido al sitio de SafetyPay para obtener el numero de operación", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.PaymentMethodDescription", "Code generated by SafeTyPay", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.PaymentMethodDescription", "Código geneado por SafeTyPay", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.ResponseExpressToken", "Error in Response Express Token {0}", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.ResponseExpressToken", "Error la respuesta del Express Token {0}", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.ResponseOperationToken", "Error in Response Opeation Token {0}", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.ResponseOperationToken", "Error en la respuesta de notificacion Token {0}", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.ProcessPayment", "Error in the Process Payment Executed {0}", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.ProcessPayment", "Error en la ejecución Process Payment {0}", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.PostProcessPayment", "Error in the Post Process Payment Executed {0}", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.PostProcessPayment", "Error en la ejecución Post Process Payment {0}", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.SchedulerTask.Execute", "Error in the executed task {0}", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.SchedulerTask.Execute", "Error en la ejecución de la tarea {0}", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.RecurringPayment", "Recurring payment not supported", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.RecurringPayment", "Pago recurrentes no soportados ", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.Refund", "Refund method not supported", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.Refund", "Método de reembolso no admitido", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.Void", "Void method not supported", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.Void", "Método vacío no admitido", "es-ES");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Instructions", @"
            <p> If you are using this gateway, please make sure that SafeTyPay supports the currency of your main store. </p>
            <p> The <strong> general </strong> fields have the purpose of storing the credentials granted by SafeTyPay that give access to the <strong> Commercial Management System </strong> by means of a username and password which are sent by email. once the commercial management with the company has started. </p>
            <p> The password of the <strong> technical documentation </strong> provides access to the documentary portal that this company provides. </p>
            <p> In the <strong> Commercial Management System </strong> provided by SafeTyPay, go to the profile option located in the upper right, then in the left side menu select the credentials option there will generate the <strong> APIKEY </strong> and the <strong> SIGNATUREKEY </strong> that the system requires. </p>
            <p> Only in the case of development, In the Commercial Management System provided by SafeTyPay, go to the profile option located in the upper right, then in the left side menu select the option of notifications, enter an email so that you receive the notifications that SafeTyPay sends and in the <strong> PostUrl </strong> put the following value (" + _webHelper.GetStoreLocation() + @" Plugins/PaymentSafeTyPay/SafeTyPayAutomaticNotification). Also check the boxes for <strong> POST / WS and Email. </strong> </p>
            <p> The payments that SafeTyPay reports are <strong> asynchronous </strong>, this means that payment notifications are stored in the system and subsequently synchronized by means of a scheduled task that synchronizes payments from time to time, configurable in the next path to <a href='" + _webHelper.GetStoreLocation() + @"Admin/ScheduleTask/List'> scheduled tasks </a> </p>
            <p> To find out if you have notifications sent by SafeTyPay that have not yet been processed, <strong> see the section below </strong> </p>", "en-US");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.SafeTyPay.Instructions", @"
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
            //database objects
            _objectContext.Uninstall();

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
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Configure");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.PaymentPending");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.General");

            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.UseSandbox");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.UseSandbox.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.ExpirationTime");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.ExpirationTime.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFee");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFee.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFeePercentage");

            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.AdditionalFeePercentage.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Merchant.Management.System");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.UserNameMMS");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.UserNameMMS.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordMMS");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordMMS.Hint");

            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Technical.Documentation");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordTD");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.PasswordTD.Hint");

            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Environment");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.ApiKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.ApiKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.SignatureKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.SignatureKey.Hint");

            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.Case102");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequest");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew1");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew2");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew3");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Note.SendRequestExpiredNew4");

            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Fields.RedirectionTip");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.PaymentMethodDescription");

            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.ResponseExpressToken");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.ResponseOperationToken");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.ProcessPayment");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.PostProcessPayment");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.SchedulerTask.Execute");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.RecurringPayment");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.Refund");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Error.Void");

            _localizationService.DeletePluginLocaleResource("Plugins.Payments.SafeTyPay.Instructions");

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
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

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