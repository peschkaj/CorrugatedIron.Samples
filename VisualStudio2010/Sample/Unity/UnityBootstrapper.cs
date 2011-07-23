using CorrugatedIron;
using CorrugatedIron.Comms;
using CorrugatedIron.Config;
using Microsoft.Practices.Unity;

namespace Sample.Unity
{
    public static class UnityBootstrapper
    {
        public static IUnityContainer Bootstrap()
        {
            // pull the configuration straight out of the app.config file using the appropriate section name
            var clusterConfig = RiakClusterConfiguration.LoadFromConfig("riakConfig");

            var container = new UnityContainer();
            // register the configuration instance with the IoC container
            container.RegisterInstance<IRiakClusterConfiguration>(clusterConfig);

            // register the default connection factory (single instance)
            container.RegisterType<IRiakConnectionFactory, RiakConnectionFactory>(new ContainerControlledLifetimeManager());
            // register the default cluster (single instance)
            container.RegisterType<IRiakCluster, RiakCluster>(new ContainerControlledLifetimeManager());

            // register the client creator (multiple instance)
            container.RegisterType<IRiakClient>(new InjectionFactory(c => c.Resolve<IRiakCluster>().CreateClient()));

            return container;
        }
    }
}
