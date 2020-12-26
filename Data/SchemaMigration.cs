using FluentMigrator;
using Nop.Data.Migrations;
using Nop.Plugin.Payments.SafeTyPay.Domain;

namespace Nop.Plugin.Payments.SafeTyPay.Data
{
    [SkipMigrationOnUpdate]
    [NopMigration("2020/12/25 08:40:55:1687541", "NotificationRequestSafeTyPay base schema")]
    public class SchemaMigration : AutoReversingMigration
    {
        protected IMigrationManager _migrationManager;

        public SchemaMigration(IMigrationManager migrationManager)
        {
            _migrationManager = migrationManager;
        }

        public override void Up()
        {
            _migrationManager.BuildTable<NotificationRequestSafeTyPay>(Create);
        }
    }
}
