using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.SessionState;

namespace Happy.Web.Memcached
{
    [Serializable]
    internal sealed class SessionData
    {
        private SessionData() { }

        public SessionData(int timeout, byte[] serializedContent)
        {
            this.Timeout = timeout;
            this.SerializedContent = serializedContent;
        }

        public static SessionData CreateUninitialized()
        {
            return new SessionData() { Actions = SessionStateActions.InitializeItem };
        }

        public bool Locked { get; set; }

        public DateTime LockTime { get; set; }

        public int LockId { get; set; }

        public SessionStateActions Actions { get; set; }

        public int Timeout { get; set; }

        public byte[] SerializedContent { get; set; }
    }
}
