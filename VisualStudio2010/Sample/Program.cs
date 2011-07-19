using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorrugatedIron;
using CorrugatedIron.Comms;
using CorrugatedIron.Extensions;
using CorrugatedIron.Models;
using CorrugatedIron.Models.MapReduce;
using CorrugatedIron.Util;
using Microsoft.Practices.Unity;
using Newtonsoft.Json.Linq;
using Sample.TinyIoc;
using Sample.Unity;

namespace Sample
{
    /// <summary>
    /// This is just a sample which shows how to load the CI client via Unity
    /// and demonstrates how you can interact with it to perform various functions.
    /// 
    /// This is <b>not</b> intended to show you the idea way of structuring your
    /// application. That is up to you. This is reference material only.
    /// </summary>
    class Program
    {
        private const string Bucket = "sample_data_bucket";

        static void Main(string[] args)
        {
            Console.WriteLine("Choose a bootstrapper:");
            Console.WriteLine(" 1 : Unity");
            Console.WriteLine(" 2 : TinyIoC");
            Console.WriteLine(" 3 : Autofac");
            Console.Write("Enter number [1 to 2] > ");

            // get an instance of a RiakClient that we can use
            IRiakClient client;

            switch (Console.ReadLine())
            {
                case "2":
                    {
                        var container = TinyIocBootstrapper.Bootstrap();
                        client = container.Resolve<IRiakClient>();
                        break;
                    }
                default:
                    {
                        // grab a unity container which has everything we need wired in
                        var container = UnityBootstrapper.Bootstrap();
                        client = container.Resolve<IRiakClient>();
                        break;
                    }
            }
            Run(client);
        }

        private static void Run(IRiakClient client)
        {
            // is the server alive?
            var pingResult = client.Ping();
            System.Diagnostics.Debug.Assert(pingResult.IsSuccess);

            // here's how you'd go about setting the properties on a bucket
            // (there are lots more than demoed here).
            var restProps = new RiakBucketProperties()
                .SetAllowMultiple(true)
                .SetWVal(3);
            client.SetBucketProperties(Bucket, restProps);
            // you'll notice that this is slow, because behind the scenes the client
            // has detected properties that can't be set via the PBC interface
            // so instead it has degraded to the REST api.

            // here's a sample which uses just the PBC properties and hence runs a
            // lot faster.
            var pbcProps = new RiakBucketProperties()
                .SetAllowMultiple(false);
            client.SetBucketProperties(Bucket, pbcProps);

            // we'll keep track of the keys we store as we create them
            var keys = new List<string>();

            // let's write some stuff to Riak, starting with a simple put
            var simplePutData = CreateData(0);
            var simplePutResponse = client.Put(simplePutData);
            System.Diagnostics.Debug.Assert(simplePutResponse.IsSuccess);
            keys.Add(simplePutData.Key);

            // next write and pull out the resulting object at the same time,
            // and specifying a different write quorum
            var putWithBody = CreateData(1);
            var putWithBodyResponse = client.Put(putWithBody, new RiakPutOptions { ReturnBody = true, W = 1 });
            System.Diagnostics.Debug.Assert(putWithBodyResponse.IsSuccess);
            System.Diagnostics.Debug.Assert(putWithBodyResponse.Value != null);
            System.Diagnostics.Debug.Assert(putWithBodyResponse.Value.VectorClock != null);
            keys.Add(putWithBody.Key);

            // let's bang out a few more objects to do a bulk load
            var objects = new List<RiakObject>();
            for (var i = 1; i < 11; ++i)
            {
                var obj = CreateData(i);
                objects.Add(obj);
                keys.Add(obj.Key);
            }
            var bulkInsertResults = client.Put(objects);
            // verify that they all went in
            foreach (var r in bulkInsertResults)
            {
                System.Diagnostics.Debug.Assert(r.IsSuccess);
            }

            // let's see if we can get out all the objects that we expect to retrieve
            // starting with a simple get:
            var simpleGetResult = client.Get(Bucket, keys[0]);
            System.Diagnostics.Debug.Assert(simpleGetResult.IsSuccess);
            System.Diagnostics.Debug.Assert(simpleGetResult.Value != null);

            // let's do a bulk get of all the objects we've written so far, again
            // mucking with the quorum value
            var objectIds = keys.Select(k => new RiakObjectId(Bucket, k));
            var bulkGetResults = client.Get(objectIds, 1);

            // verify that we got everything
            foreach (var r in bulkGetResults)
            {
                System.Diagnostics.Debug.Assert(r.IsSuccess);
                System.Diagnostics.Debug.Assert(r.Value != null);
            }

            // let's try a map/reduce function, with javascript, to count the
            // number of objects in the bucket
            var sumMapRed = new RiakMapReduceQuery()
                .Inputs(Bucket)
                .MapJs(m => m.Source(@"function(o) {return [ 1 ];}"))
                .ReduceJs(r => r.Name(@"Riak.reduceSum").Keep(true));

            // execute this query with blocking IO, waiting for all the results to come
            // back before we process them
            var blockingMRResult = client.MapReduce(sumMapRed);
            System.Diagnostics.Debug.Assert(blockingMRResult.IsSuccess);
            // next, pull out the phase we're interested in to get the result we want
            var reducePhaseResult = blockingMRResult.Value.PhaseResults.Last();
            // get the value and convert it to something meaningful
            var reducePhaseJson = JArray.Parse(reducePhaseResult.Value.FromRiakString());
            System.Diagnostics.Debug.Assert(reducePhaseJson[0].Value<int>() == 12);

            // now let's do the same thing, but with the blocking version that streams
            // the results back per phase, rather than waiting for all the reults to
            // be calculated first
            var streamingMRResult = client.StreamMapReduce(sumMapRed);
            System.Diagnostics.Debug.Assert(streamingMRResult.IsSuccess);
            foreach (var result in streamingMRResult.Value.PhaseResults)
            {
                if (result.Phase == 1)
                {
                    var json = JArray.Parse(result.Value.FromRiakString());
                    System.Diagnostics.Debug.Assert(json[0].Value<int>() == 12);
                }
            }

            // each of the above methods have an async equivalent that has an extra
            // parameter to pass in which is an Action that takes the result. This
            // is executed on the worker thread that the work is done on. For the
            // sake of this example, we'll only demonstrate how to do this with
            // streaming map/reduce as applying the principle to the other functions
            // is a simple thing to do. All the async methods are exposed via the
            // 'Async' property.

            // create an event to wait on while the results are being processed
            // (usually you wouldn't worry about this in a Ui app, you'd just take
            // the result of the other thread and dispatch it to the UI when processed)
            var autoResetEvent = new AutoResetEvent(false);
            client.Async.StreamMapReduce(sumMapRed, result => HandleStreamingMapReduce(result, autoResetEvent));
            autoResetEvent.WaitOne();

            // finally delete the bucket (this can also be done asynchronously)
            // this calls ListKeys behind the scenes, so it's a very slow process. Riak
            // doesn't currently have the ability to quickly delete a bucket.
            client.DeleteBucket(Bucket);
        }

        static void HandleStreamingMapReduce(RiakResult<RiakStreamedMapReduceResult> streamingMRResult, EventWaitHandle handle)
        {
            System.Diagnostics.Debug.Assert(streamingMRResult.IsSuccess);
            foreach (var result in streamingMRResult.Value.PhaseResults)
            {
                if (result.Phase == 1)
                {
                    var json = JArray.Parse(result.Value.FromRiakString());
                    System.Diagnostics.Debug.Assert(json[0].Value<int>() == 12);
                }
            }

            handle.Set();
        }

        static RiakObject CreateData(int index)
        {
            var key = Guid.NewGuid().ToString();
            var value = "{\"index\":" + index + ",\"data\":\"" + Guid.NewGuid() + "\"}";
            return new RiakObject(Bucket, key, value, RiakConstants.ContentTypes.ApplicationJson);
        }
    }
}
