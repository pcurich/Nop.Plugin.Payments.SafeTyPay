using Autofac;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Plugin.Payments.SafeTyPay.Services;

namespace Nop.Plugin.Payments.SafeTyPay.Infrastructure
{
    /// <summary>
    /// Dependency registrar
    /// </summary>
    public class DependencyRegistrar : IDependencyRegistrar
    {
/// <summary>
        ///  Register services and interfaces
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="typeFinder"></param>
        /// <param name="appSettings"></param>
        public void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig appSettings)
        {
            builder.RegisterType<NotificationRequestService>().As<INotificationRequestService>().InstancePerLifetimeScope();
        }

        /// <summary>
        /// Order of this dependency registrar implementation
        /// </summary>
        public int Order => 1;
    }
}