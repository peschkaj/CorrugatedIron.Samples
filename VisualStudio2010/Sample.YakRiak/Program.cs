using CorrugatedIron.Comms;
using Microsoft.Practices.Unity;
using CorrugatedIron;

namespace Sample.YakRiak
{
    class Program
    {
        static void Main(string[] args)
        {
            var container = UnityBootstrapper.Bootstrap();
            var client = container.Resolve<IRiakClient>();

            var yak = new YakRiak(client);
            yak.Run();

            container.Resolve<IRiakCluster>().Dispose();
        }
    }
}
