using CorrugatedIron;
using CorrugatedIron.Comms;
using CorrugatedIron.Config;
using TinyIoC;

namespace Sample.TinyIoc
{
    public static class TinyIocBootstrapper
    {
        public static TinyIoCContainer Bootstrap()
        {
            // pull the configuration straight out of the app.config file using the appropriate section name
            var clusterConfig = RiakClusterConfiguration.LoadFromConfig("riakConfig");

            var container = TinyIoCContainer.Current;

            // register the configuration instance with the IoC container
            container.Register<IRiakClusterConfiguration>(clusterConfig);

            // register the default connection factory (single instance)
            container.Register<IRiakConnectionFactory, RiakConnectionFactory>().AsSingleton();

            // register the default cluster (single instance)
            container.Register<IRiakCluster, RiakCluster>().AsSingleton();

            // register the client creator (multiple instance)
            container.Register<IRiakClient>((c, np) => c.Resolve<IRiakCluster>().CreateClient());

            return container;
        }
    }
}
