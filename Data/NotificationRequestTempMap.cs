using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nop.Data.Mapping;
using Nop.Plugin.Payments.SafeTyPay.Domain;

namespace Nop.Plugin.Payments.SafeTyPay.Data
{
    /// <summary>
    /// Represents a Notification Request Temp by SafeTyPay
    /// </summary>
    public class NotificationRequestTempMap : NopEntityTypeConfiguration<NotificationRequestTemp>
    {
        #region Methods

        /// <summary>
        /// Configures the entity
        /// </summary>
        /// <param name="builder">The builder to be used to configure the entity</param>
        public override void Configure(EntityTypeBuilder<NotificationRequestTemp> builder)
        {
            builder.ToTable(nameof(NotificationRequestTemp));
            builder.HasKey(temp => temp.Id);

            builder.Property(temp => temp.Amount).HasColumnType("decimal(18, 2)");

            base.Configure(builder);
        }

        #endregion Methods
    }
}