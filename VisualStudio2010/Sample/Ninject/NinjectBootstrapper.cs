using CorrugatedIron;
using CorrugatedIron.Comms;
using CorrugatedIron.Config;
using Ninject;

namespace Sample.Ninject
{
    public static class NinjectBootstrapper
    {
        public static IKernel Bootstrap()
        {
            // pull the configuration straight out of the app.config file using the appropriate section name
            var clusterConfig = RiakClusterConfiguration.LoadFromConfig("riakConfig");
            var container = new StandardKernel();

            // register the configuration instance with the IoC container
            container.Bind<IRiakClusterConfiguration>().ToConstant(clusterConfig);

            // register the default connection factory (single instance)
            container.Bind<IRiakConnectionFactory>().To<RiakConnectionFactory>().InSingletonScope();

            // register the default cluster (single instance)
            container.Bind<IRiakCluster>().To<RiakCluster>().InSingletonScope();

            // register the client creator (multiple instance)
            container.Bind<IRiakClient>().ToMethod(ctx => container.Get<IRiakCluster>().CreateClient());

            return container;
        }
    }
}
