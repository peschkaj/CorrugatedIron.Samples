using System;
using CorrugatedIron.Models;
using CorrugatedIron.Util;
using CorrugatedIron.Extensions;

namespace Sample.SessionStateProvider
{
    public class RiakSessionItem
    {
        private int _timeout = 30;
        private bool _locked = false;

        public string SessionContainer { get; set; }
        public string SessionId { get; set; }
        public DateTime Created { get; set; }
        public DateTime Expires { get; set; }
        public DateTime LockDate { get; set; }
        public int LockId { get; set; }
        public int Flags { get; set; }
        public string SessionStoreItems { get; set; }
        public int Timeout
        {
            get
            {
                return _timeout;
            }

            set
            {
                _timeout = value;
                Expires = Created.AddMinutes(_timeout);
            }
        }
        public bool Locked
        {
            get
            {
                return _locked;
            }
            set
            {
                _locked = value;
                LockDate = DateTime.Now;
            }
        }

        public RiakSessionItem()
        {
            Flags = 1;
            Created = DateTime.Now;
            Expires = Created.AddMinutes(Timeout);
            LockDate = Created;
            LockId = 0;
            Locked = false;
        }

        public RiakSessionItem(RiakObject riakObject)
        {
            SessionStoreItems = riakObject.Value.FromRiakString();
            Created = DateTime.Parse(riakObject.UserMetaData["X-Riak-Meta-Created"]);
            Expires = DateTime.Parse(riakObject.UserMetaData["X-Riak-Meta-Expires"]);
            LockDate = DateTime.Parse(riakObject.UserMetaData["X-Riak-Meta-LockDate"]);
            LockId = int.Parse(riakObject.UserMetaData["X-Riak-Meta-LockId"]);
            Locked = bool.Parse(riakObject.UserMetaData["X-Riak-Meta-Locked"]);
            Flags = int.Parse(riakObject.UserMetaData["X-Riak-Meta-Flags"]);

            _timeout = (Expires - Created).Minutes;
        }

        public RiakObject ToRiakObject()
        {
            var o = new RiakObject(SessionContainer, SessionId, SessionStoreItems, RiakConstants.ContentTypes.ApplicationJson);

            o.UserMetaData["X-Riak-Meta-Created"] = Created.ToString("R");
            o.UserMetaData["X-Riak-Meta-Expires"] = Expires.ToString("R");
            o.UserMetaData["X-Riak-Meta-LockDate"] = LockDate.ToString("R");
            o.UserMetaData["X-Riak-Meta-LockId"] = LockId.ToString();
            o.UserMetaData["X-Riak-Meta-Locked"] = Locked.ToString();
            o.UserMetaData["X-Riak-Meta-Flags"] = Flags.ToString();

            return o;
        }

        public void ResetTimeout()
        {
            Expires = DateTime.Now.AddMinutes(Timeout);
        }

        public void Unlock()
        {
            LockId = 0;
            LockDate = Created;
        }
    }
}

