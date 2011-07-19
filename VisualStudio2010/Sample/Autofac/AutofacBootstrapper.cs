using Autofac;
using CorrugatedIron;
using CorrugatedIron.Comms;
using CorrugatedIron.Config;

namespace Sample.Autofac
{
    public static class AutofacBootstrapper
    {
        public static IContainer Bootstrap()
        {
            // pull the configuration straight out of the app.config file using the appropriate section name
            var clusterConfig = RiakClusterConfiguration.LoadFromConfig("riakConfig");

            var builder = new ContainerBuilder();

            // register the configuration instance with the IoC container
            builder.RegisterInstance(clusterConfig).As<IRiakClusterConfiguration>();

            // register the default connection factory (single instance)
            builder.RegisterType<RiakConnectionFactory>().As<IRiakConnectionFactory>().SingleInstance();

            // register the default cluster (single instance)
            builder.RegisterType<RiakCluster>().As<IRiakCluster>().SingleInstance();

            // register the client creator (multiple instance)
            builder.Register(c => c.Resolve<IRiakCluster>().CreateClient()).As<IRiakClient>();

            return builder.Build();
        }
    }
}
