using System.Collections.Generic;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.SafeTyPay.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.SafeTyPay.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }

        public bool UseSandbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.SafeTyPay.Fields.CanGenerateNewCode")]
        public bool CanGenerateNewCode { get; set; }

        public bool CanGenerateNewCode_OverrideForStore { get; set; }
        

        [NopResourceDisplayName("Plugins.Payments.SafeTyPay.Fields.ExpirationTime")]
        public int ExpirationTime { get; set; }

        public bool ExpirationTime_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.SafeTyPay.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }

        public bool AdditionalFee_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.SafeTyPay.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }

        public bool AdditionalFeePercentage_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.SafeTyPay.Fields.UserNameMMS")]
        public string UserNameMMS { get; set; }

        public bool UserNameMMS_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.SafeTyPay.Fields.PasswordMMS")]
        public string PasswordMMS { get; set; }

        public bool PasswordMMS_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.SafeTyPay.Fields.PasswordTD")]
        public string PasswordTD { get; set; }

        public bool PasswordTD_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.SafeTyPay.Fields.ApiKey")]
        public string ApiKey { get; set; }

        public bool ApiKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.SafeTyPay.Fields.SignatureKey")]
        public string SignatureKey { get; set; }

        public bool SignatureKey_OverrideForStore { get; set; }

        public IDictionary<string,string> PendingPaymen { get; set; }

    }
}