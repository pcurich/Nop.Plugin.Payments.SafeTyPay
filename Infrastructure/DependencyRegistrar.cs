using Autofac;
using Autofac.Core;
using Nop.Core.Configuration;
using Nop.Core.Data;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Data;
using Nop.Plugin.Payments.SafeTyPay.Data;
using Nop.Plugin.Payments.SafeTyPay.Domain;
using Nop.Plugin.Payments.SafeTyPay.Services;
using Nop.Web.Framework.Infrastructure.Extensions;

namespace Nop.Plugin.Payments.SafeTyPay.Infrastructure
{
    /// <summary>
    /// Dependency registrar
    /// </summary>
    public class DependencyRegistrar : IDependencyRegistrar
    {
        /// <summary>
        /// Register services and interfaces
        /// </summary>
        /// <param name="builder">Container builder</param>
        /// <param name="typeFinder">Type finder</param>
        /// <param name="config">Config</param>
        public virtual void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig config)
        {
            builder.RegisterType<NotificationRequestService>().As<INotificationRequestService>().InstancePerLifetimeScope();

            //data context
            builder.RegisterPluginDataContext<NotificationRequestTempContext>("nop_object_context_payment_purchase_safetypay");

            //override required repository with our custom context
            builder.RegisterType<EfRepository<NotificationRequestTemp>>().As<IRepository<NotificationRequestTemp>>()
                .WithParameter(ResolvedParameter.ForNamed<IDbContext>("nop_object_context_payment_purchase_safetypay"))
                .InstancePerLifetimeScope();
        }

        /// <summary>
        /// Order of this dependency registrar implementation
        /// </summary>
        public int Order => 1;
    }
}