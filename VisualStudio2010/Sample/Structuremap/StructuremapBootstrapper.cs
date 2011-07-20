using CorrugatedIron;
using CorrugatedIron.Comms;
using CorrugatedIron.Config;
using StructureMap;

namespace Sample.Structuremap
{
    public static class StructuremapBootstrapper
    {
        public static IContainer Bootstrap()
        {
            // pull the configuration straight out of the app.config file using the appropriate section name
            var clusterConfig = RiakClusterConfiguration.LoadFromConfig("riakConfig");

            var container = new Container(expr =>
                {
                    // register the configuration instance with the IoC container
                    expr.For<IRiakClusterConfiguration>().Singleton().Add(clusterConfig);

                    // register the default connection factory (single instance)
                    expr.For<IRiakConnectionFactory>().Singleton().Use<RiakConnectionFactory>();

                    // register the default cluster (single instance)
                    expr.For<IRiakCluster>().Singleton().Use<RiakCluster>();

                    // register the client creator (multiple instance)
                    expr.For<IRiakClient>().Use(ctx => ctx.GetInstance<IRiakCluster>().CreateClient());
                });

            return container;
        }
    }
}
