namespace Nop.Plugin.Payments.SafeTyPay
{
    public class SafeTyPayDefaults
    {
        #region ScheduleTask

        /// <summary>
        /// Gets a name of the synchronization schedule task
        /// </summary>
        public static string SynchronizationTaskName => "Synchronization (SafetyPay plugin)";

        /// <summary>
        /// Gets a type of the synchronization schedule task
        /// </summary>
        public static string SynchronizationTaskType => "Nop.Plugin.Payments.SafeTyPay.Services.SynchronizationTask";

        /// <summary>
        /// Gets a default synchronization period in 1 hours
        /// </summary>
        public static int SynchronizationPeriod => 60 * 60;

        /// <summary>
        /// Gets a default synchronization Enabled
        /// </summary>
        public static bool SynchronizationEnabled => true;

        /// <summary>
        /// Gets a default synchronization Enabled Stop On Error
        /// </summary>
        public static bool SynchronizationStopOnError => false;

        #endregion ScheduleTask

        #region SafeTyPayPaymentSettings

        public static string PrefixSandbox => "sandbox-mws2.";
        public static bool UseSandbox => true;
        public static string TransactionOkURL => "{0}/order/history";
        public static string TransactionErrorURL => "{0}/SafeTyPayError";
        public static int ExpirationTime => 1440;
        public static decimal AdditionalFee => 0;
        public static bool AdditionalFeePercentage => false;

        public static string ExpressTokenUrl => "https://{0}safetypay.com/express/ws/v.3.0/Post/CreateExpressToken";
        public static string NotificationUrl => "https://{0}safetypay.com/express/ws/v.3.0/Post/GetOperation";

        #endregion SafeTyPayPaymentSettings
    }
}