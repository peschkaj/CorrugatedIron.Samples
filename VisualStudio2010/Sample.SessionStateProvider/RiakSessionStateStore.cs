using System;
using System.Configuration;
using System.Collections.Specialized;
using System.Web;
using System.Web.Configuration;
using System.Web.SessionState;
using Newtonsoft.Json;
using CorrugatedIron;
using CorrugatedIron.Models;

namespace Sample.SessionStateProvider
{
    public class RiakSessionStateStore : SessionStateStoreProviderBase
    {

        private IRiakClient _client;
        private SessionStateSection _config;

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
            //TODO add Riak configuration
            if (config == null)
                throw new ArgumentNullException("config");

            if (String.IsNullOrEmpty(name))
                name = "RiakSessionStateStore";

            base.Initialize(name, config);

            ApplicationName = System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath;

            Configuration cfg = WebConfigurationManager.OpenWebConfiguration(ApplicationName);
            _config = (SessionStateSection)cfg.GetSection("system.web/sessionState");
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
            var riakSessionItem = new RiakSessionItem(riakObject);

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
            var sessionItem = new RiakSessionItem(result.Value);
            sessionItem.ResetTimeout();

            _client.Async.Put(sessionItem.ToRiakObject(), results => { return; });
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            // TODO change to MR job
            // TODO add expiration check
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
            riakSessionItem.SessionStoreItems = Serialize((SessionStateItemCollection)item.Items);

            _client.Async.Put(riakSessionItem.ToRiakObject(), results => { return; });
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }

        private SessionStateStoreData GetSessionStoreItem(bool lockRecord,
          HttpContext context,
          string id,
          out bool locked,
          out TimeSpan lockAge,
          out object lockId,
          out SessionStateActions actions)
        {
            var result = _client.Get(ApplicationName, id);
            var riakObject = result.Value;
            var riakSessionItem = new RiakSessionItem(riakObject);

            locked = riakSessionItem.Locked;
            lockAge = DateTime.Now.Subtract(riakSessionItem.LockDate);
            lockId = riakSessionItem.LockId;
            actions = (SessionStateActions)riakSessionItem.Flags;

            // TODO What to do about lockRecord?

            return Deserialize(context, riakSessionItem.SessionStoreItems, riakSessionItem.Timeout);
        }

        private static string Serialize(SessionStateItemCollection sessionStateItems)
        {
            return JsonConvert.SerializeObject(sessionStateItems);
        }

        private static SessionStateStoreData Deserialize(HttpContext context, string sessionStoreItems, int timeout)
        {
            var ssic = JsonConvert.DeserializeObject<SessionStateItemCollection>(sessionStoreItems);

            return new SessionStateStoreData(ssic, SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }
    }
}

