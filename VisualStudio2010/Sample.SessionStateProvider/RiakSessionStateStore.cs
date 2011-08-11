using System;
using System.Configuration;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.SessionState;
using CorrugatedIron.Models.MapReduce;
using CorrugatedIron.Models.MapReduce.Inputs;
using Newtonsoft.Json;
using CorrugatedIron;
using CorrugatedIron.Models;
using CorrugatedIron.Extensions;
using Microsoft.Practices.Unity;

namespace Sample.SessionStateProvider
{
    public class RiakSessionStateStore : SessionStateStoreProviderBase
    {

        private IRiakClient _client;
        private SessionStateSection _config;
        private SessionStateItemExpireCallback _expireCallBack = null;
        private System.Timers.Timer _expiredSessionDeletionTimer;

        public new string Description
        {
            get { return "Riak ASP.NET Session State"; }
        }

        public string ApplicationName
        {
            get;
            internal set;
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            if (String.IsNullOrEmpty(name))
                name = "RiakSessionStateStore";

            base.Initialize(name, config);

            ApplicationName = System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath;

            Configuration cfg = WebConfigurationManager.OpenWebConfiguration(ApplicationName);
            _config = (SessionStateSection)cfg.GetSection("system.web/sessionState");

            var container = UnityBootstrapper.Bootstrap();
            _client = container.Resolve<IRiakClient>();

            var riakSessionConfiguration = container.Resolve<RiakSessionStateConfiguration>();
            int expiredSessionDeletionInterval = riakSessionConfiguration.TimeoutInMilliseconds;

            _expiredSessionDeletionTimer = new System.Timers.Timer(expiredSessionDeletionInterval);
            _expiredSessionDeletionTimer.Elapsed += ExpiredSessionDeletionTimerElapsed;
            _expiredSessionDeletionTimer.Enabled = true;
            _expiredSessionDeletionTimer.AutoReset = true;
        }

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(),
                                             SessionStateUtility.GetSessionStaticObjects(context),
                                             timeout);
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            var riakSessionItem = new RiakSessionItem
            {
                SessionContainer = ApplicationName,
                SessionId = id,
                Created = DateTime.Now,
                Timeout = timeout
            };

            _client.Async.Put(riakSessionItem.ToRiakObject(), result => { return; });
            return;
        }

        public override void Dispose()
        {
        }

        public override void EndRequest(HttpContext context)
        {
        }

        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return GetSessionStoreItem(false, context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return GetSessionStoreItem(true, context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override void InitializeRequest(HttpContext context)
        {
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            var riakObject = _client.Get(ApplicationName, id).Value;
            var riakSessionItem = new RiakSessionItem(riakObject) {Timeout = _config.Timeout.Minutes};

            if (riakSessionItem.LockId == (int)lockId)
            {
                riakSessionItem.ResetTimeout();
                _client.Async.Put(riakSessionItem.ToRiakObject(), result => { return; });
            }
        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            _client.Delete(ApplicationName, id);
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            var result = _client.Get(ApplicationName, id);
            var riakSessionItem = new RiakSessionItem(result.Value) {Timeout = _config.Timeout.Minutes};
            riakSessionItem.ResetTimeout();

            _client.Async.Put(riakSessionItem.ToRiakObject(), results => { return; });
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            if (newItem)
            {
                var riakSessionItem = new RiakSessionItem
                                          {
                                              SessionStoreItems = Serialize(item)
                                          };
                _client.Put(riakSessionItem.ToRiakObject());
            }
            else
            {
                var result = _client.Get(ApplicationName, id);
                var riakSessionItem = new RiakSessionItem();

                if (result.ResultCode != ResultCode.NotFound)
                {
                    riakSessionItem = new RiakSessionItem(result.Value) {Flags = 0};
                    riakSessionItem.Unlock();
                }
                else
                {
                    riakSessionItem.SessionId = id;
                    riakSessionItem.SessionContainer = ApplicationName;
                }

                riakSessionItem.Created = DateTime.Now;
                riakSessionItem.Timeout = item.Timeout;
                riakSessionItem.SessionStoreItems = Serialize(item);

                _client.Async.Put(riakSessionItem.ToRiakObject(), results => { return; });
            }
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            _expireCallBack = expireCallback;
            return true;
        }

        private SessionStateStoreData GetSessionStoreItem(bool lockRecord,
          HttpContext context,
          string id,
          out bool locked,
          out TimeSpan lockAge,
          out object lockId,
          out SessionStateActions actions)
        {
            SessionStateStoreData result = null;

            locked = default(bool);
            lockAge = default(TimeSpan);
            lockId = null;
            actions = default(SessionStateActions);

            var riakResult = _client.Get(ApplicationName, id);

            if (riakResult.IsSuccess)
            {
                var riakObject = riakResult.Value;
                var riakSessionItem = new RiakSessionItem(riakObject);

                locked = riakSessionItem.Locked;
                lockAge = DateTime.Now.Subtract(riakSessionItem.LockDate);
                lockId = riakSessionItem.LockId;
                actions = (SessionStateActions) riakSessionItem.Flags;

                if (riakSessionItem.Expires < DateTime.Now || riakSessionItem.Locked)
                    return null;
                
                if (actions == SessionStateActions.InitializeItem)
                {
                    result = CreateNewStoreData(context, riakSessionItem.Timeout);
                }
                else
                {
                    result = Deserialize(riakSessionItem.SessionStoreItems);
                }

                if (lockRecord)
                {
                    riakSessionItem.Locked = true;
                    _client.Async.Put(riakSessionItem.ToRiakObject(), results => { return; });
                }
            }

            return result;
        }

        private static string Serialize(SessionStateStoreData sessionStateItems)
        {
            return JsonConvert.SerializeObject(sessionStateItems);
        }

        private static SessionStateStoreData Deserialize(string sessionStoreItems)
        {
            return JsonConvert.DeserializeObject<SessionStateStoreData>(sessionStoreItems);
        }

        private void ExpiredSessionDeletionTimerElapsed(object source, System.Timers.ElapsedEventArgs e)
        {
            /*
             * Determine mode of session garbage collection. If the session expire callback is disabled
             * one may simple delete all expired session from the session table. If however the session expire callback
             * is enabled, we need to load the session data for every expired session and invoke the expire callback
             * for each of these sessions prior to deletion.
             * Also check if an expire call back was actually defined. If m_expireCallback is null we also don't have to take
             * the more expensive path where every session is enumerated while there's no real need to do so.
             */

            if (_expireCallBack != null)
                InvokeExpireCallbackAndDeleteSession();
            else
                DeleteExpiredSessions();
        }

        private void InvokeExpireCallbackAndDeleteSession()
        {
            // MR to get sessions to delete
            var query = new RiakMapReduceQuery();
            query.Inputs(ApplicationName)
                .MapJs(
                    m =>
                    m.Source(@"
function (value, keyData, arg) {
    var now = new Date(Date.parse(arg));
    var metadata = value.values[0].metadata;
    var expires = metadata['X-Riak-Meta']['X-Riak-Meta-Expires'];

    if (arg > expires) 
    {
        return [value.key, expires];
    }
    else
    {
        return [];
    }
}")
                        .Argument(DateTime.Now.ToString("R")));

            var results = _client.MapReduce(query);
            
            if (results.IsSuccess)
            {
                var keys = results.Value.PhaseResults.ElementAt(results.Value.PhaseResults.Count() - 1).Value.FromRiakString();
                var keyList = JsonConvert.DeserializeObject<string[]>(keys);

                var riakObjectIdList = keyList.Select(key => new RiakObjectId(ApplicationName, key)).ToList();

                // for some stupid reason, we have to retrieve all of the deleted keys, process them, and THEN delete them
                var riakSessionObjects = _client.Get(riakObjectIdList);
                foreach (var riakSessionObject in riakSessionObjects)
                {
                    var value = riakSessionObject.Value;
                    var session = new RiakSessionItem(value);
                    _expireCallBack.Invoke(value.Key, Deserialize(session.SessionStoreItems));
                }

                _client.Async.Delete(riakObjectIdList, deleteResults => { return; });
            }
        }

        private void DeleteExpiredSessions()
        {
            // MR to get sessions to delete
            var query = new RiakMapReduceQuery();
            query.Inputs(ApplicationName)
                .MapJs(
                    m =>
                    m.Source(@"
function (value, keyData, arg) {
    var now = new Date(Date.parse(arg));
    var metadata = value.values[0].metadata;
    var expires = metadata['X-Riak-Meta']['X-Riak-Meta-Expires'];

    if (arg > expires) 
    {
        return [value.key, expires];
    }
    else
    {
        return [];
    }
}")
                        .Argument(DateTime.Now.ToString("R")));

            var results = _client.MapReduce(query);

            if (results.IsSuccess)
            {
                var keys = results.Value.PhaseResults.ElementAt(results.Value.PhaseResults.Count() - 1).Value.FromRiakString();
                var keyList = JsonConvert.DeserializeObject<string[]>(keys);

                var riakObjectIdList = keyList.Select(key => new RiakObjectId(ApplicationName, key)).ToList();

                _client.Async.Delete(riakObjectIdList, deleteResults => { return; });
            }
        }
    }
}

