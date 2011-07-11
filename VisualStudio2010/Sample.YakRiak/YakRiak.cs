using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using CorrugatedIron;
using CorrugatedIron.Extensions;
using CorrugatedIron.Models;
using CorrugatedIron.Models.MapReduce;
using CorrugatedIron.Models.MapReduce.Inputs;
using CorrugatedIron.Util;
using Newtonsoft.Json;

namespace Sample.YakRiak
{
    public class YakRiak
    {
        private readonly IRiakClient _riakClient;
        private string _userName;
        private string _gravatar;
        private readonly DateTime _timeStart;
        private readonly Random _rnd;
        private ulong _since;

        public YakRiak(IRiakClient riakClient)
        {
            _riakClient = riakClient;
            // used for waiting for random periods of time
            _rnd = new Random();

            // this is the start date that we need to compare times against to
            // keep the timestamps in sync with YakRiak server
            _timeStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        public void Run()
        {
            ShowHeader();

            // quick check to make sure we can connect
            if (!RiakRunning())
            {
                Console.Error.WriteLine("Unuable to connect to the Riak cluster");
                return;
            }

            // get some info from the user
            SetUserDetails();
            // show the usage message
            Usage();

            // start polling the back end
            ThreadPool.QueueUserWorkItem(_ => StartPolling());

            // deal with user input
            HandleInput();
        }

        private static void Usage()
        {
            Console.WriteLine("To exit type: /quit");
            Console.WriteLine("To send a message, type it out and hit enter");
        }

        private void HandleInput()
        {
            var running = true;
            while (running)
            {
                var input = Console.ReadLine();
                switch (input)
                {
                    case "/quit":
                        running = false;
                        break;

                    default:
                        SendMessage(input);
                        break;
                }
            }
        }

        private void SendMessage(string message)
        {
            var yakMsg = new YakMessage
            {
                gravatar = _gravatar,
                message = message,
                timestamp = Since(DateTime.UtcNow),
                name = _userName,
                key = Md5(_userName + "-" + DateTime.UtcNow)
            };
            var json = JsonConvert.SerializeObject(yakMsg);
            var riakObj = new RiakObject("messages", yakMsg.key, json, RiakConstants.ContentTypes.ApplicationJson);
            var result = _riakClient.Put(riakObj);
            if (!result.IsSuccess)
            {
                Console.Error.WriteLine("Failed to send message!");
            }
        }

        private void StartPolling()
        {
            // when we start polling we look to see any items from the last hour..
            _since = Since(DateTime.UtcNow.AddHours(-1));

            // and do a simple map reduce which gets the last 25 elements that appear in that
            // time frame.
            var pollQuery = new RiakMapReduceQuery()
                .Inputs(new RiakPhaseInputs("messages"))
                .MapJs(m => m.BucketKey("yakmr", "mapMessageSince").Argument(_since))
                .ReduceJs(r => r.BucketKey("yakmr", "reduceSortTimestamp"))
                .ReduceJs(r => r.BucketKey("yakmr", "reduceLimitLastN").Argument(25).Keep(true));

            // make sure we don't have our MILLIONS of clients smashing the cluster at exactly
            // the same time
            WaitRandom();

            // then asyncrhonously stream the result set
            _riakClient.Async.StreamMapReduce(pollQuery, Poll);
        }

        private void Poll(RiakResult<RiakStreamedMapReduceResult> result)
        {
            if (result.IsSuccess)
            {
                foreach (var phase in result.Value.PhaseResults)
                {
                    // make sure we get hold of the phase result which has data
                    if (phase.Value != null)
                    {
                        // deserialize into an array of messages
                        var messages = JsonConvert.DeserializeObject<YakMessage[]>(phase.Value.FromRiakString());

                        // throw them on screen
                        foreach (var m in messages)
                        {
                            DisplayMessage(m);
                            _since = m.timestamp + 1;
                        }
                    }
                }
            }

            // create the next map reduce job
            var pollQuery = new RiakMapReduceQuery()
                .Inputs(new RiakPhaseInputs("messages"))
                .MapJs(m => m.BucketKey("yakmr", "mapMessageSince").Argument(_since))
                .ReduceJs(r => r.BucketKey("yakmr", "reduceSortTimestamp").Keep(true));

            // do the usual wait
            WaitRandom();

            // and off we go again.
            _riakClient.Async.StreamMapReduce(pollQuery, Poll);
        }

        private void WaitRandom()
        {
            // wait betwee 1 and 4 seconds
            var wait = _rnd.Next(1000, 4000);
            Thread.Sleep(wait);
        }

        private void DisplayMessage(YakMessage msg)
        {
            var time = _timeStart.AddMilliseconds(msg.timestamp);
            Console.WriteLine("[{0:HH:mm:ss}] <{1}> {2}", time.ToLocalTime(), msg.name, msg.message);
        }

        private ulong Since(DateTime dt)
        {
            // calculate the offset in milliseconds so that the timestamp
            // fields are correct.
            return (ulong)(dt - _timeStart).TotalMilliseconds;
        }

        private bool RiakRunning()
        {
            // simple ping to see if the cluster is iup
            return _riakClient.Ping().IsSuccess;
        }

        private static string Md5(string data)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(data));
                var sb = new StringBuilder();
                foreach (var b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private void ShowHeader()
        {
            const string header =
@"__   __    _    ____  _       _      _   _ _____ _____ 
\ \ / /_ _| | _|  _ \(_) __ _| | __ | \ | | ____|_   _|
 \ V / _` | |/ / |_) | |/ _` | |/ / |  \| |  _|   | |  
  | | (_| |   <|  _ <| | (_| |   < _| |\  | |___  | |  
  |_|\__,_|_|\_\_| \_\_|\__,_|_|\_(_)_| \_|_____| |_|  ";

            Console.WriteLine(header);
            Console.WriteLine();
        }

        private void SetUserDetails()
        {
            Console.Write("Welcome. Please enter your username: ");
            _userName = Console.ReadLine();
            Console.Write("Please enter your email (for gravatars): ");
            _gravatar = Md5(Console.ReadLine());
        }
    }
}
