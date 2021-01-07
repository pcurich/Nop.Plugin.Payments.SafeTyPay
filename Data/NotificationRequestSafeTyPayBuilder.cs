using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Payments.SafeTyPay.Domain;

namespace Nop.Plugin.Payments.SafeTyPay.Data
{
    public class NotificationRequestSafeTyPayBuilder : NopEntityBuilder<NotificationRequestSafeTyPay>
    {
        /// <summary>
        /// Apply entity configuration
        /// </summary>
        /// <param name="table">Create table expression builder</param>
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            
            table
                //.WithColumn(nameof(NotificationRequestSafeTyPay.Id)).AsInt32().PrimaryKey()
                
                .WithColumn(nameof(NotificationRequestSafeTyPay.RequestDateTime)).AsString()
                .WithColumn(nameof(NotificationRequestSafeTyPay.MerchantSalesID)).AsString()
                .WithColumn(nameof(NotificationRequestSafeTyPay.ReferenceNo)).AsString()
                .WithColumn(nameof(NotificationRequestSafeTyPay.CreationDateTime)).AsString()
                .WithColumn(nameof(NotificationRequestSafeTyPay.Amount)).AsDecimal(18, 2)
                .WithColumn(nameof(NotificationRequestSafeTyPay.CurrencyId)).AsString()
                .WithColumn(nameof(NotificationRequestSafeTyPay.PaymentReferenceNo)).AsString()
                .WithColumn(nameof(NotificationRequestSafeTyPay.StatusCode)).AsString()
                .WithColumn(nameof(NotificationRequestSafeTyPay.Signature)).AsString()
                .WithColumn(nameof(NotificationRequestSafeTyPay.Origin)).AsString(4000)
                .WithColumn(nameof(NotificationRequestSafeTyPay.ClientRedirectURL)).AsString()
                .WithColumn(nameof(NotificationRequestSafeTyPay.OperationCode)).AsBoolean();
        }
    }
}
