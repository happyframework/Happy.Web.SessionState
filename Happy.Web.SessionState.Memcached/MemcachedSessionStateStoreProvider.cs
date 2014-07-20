using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.SessionState;
using System.Collections.Specialized;
using System.IO;

using Enyim.Caching;
using Enyim.Caching.Memcached;

namespace Happy.Web.SessionState.Memcached
{
    /// <summary>
    /// 参考网址：
    ///     1、实现会话状态存储提供程序：
    ///        http://msdn.microsoft.com/zh-cn/library/ms178587(v=vs.100).aspx
    ///     2、如何：演示会话状态存储提供程序
    ///        http://msdn.microsoft.com/zh-cn/library/ms178589(v=vs.100).aspx
    ///     3、https://github.com/rohita/MemcachedSessionProvider
    ///     4、http://memcachedproviders.codeplex.com/
    ///     5、https://github.com/enyim/EnyimMemcached/wiki
    /// </summary>
    public sealed class MemcachedSessionStateStoreProvider
                                                        : SessionStateStoreProviderBase
    {
        private MemcachedClient _memcachedClient;

        public override void Initialize(string name, NameValueCollection config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (string.IsNullOrEmpty(name))
            {
                name = typeof(MemcachedSessionStateStoreProvider).Name;
            }
            if (string.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Memcached Session State Store Provider");
            }

            base.Initialize(name, config);

            _memcachedClient = new MemcachedClient(GetEnyimSectionName(config));
        }

        /// <summary>
        /// Takes as input the HttpContext instance for the current request and performs
        /// any initialization required by your session-state store provider.
        /// </summary>
        public override void InitializeRequest(HttpContext context)
        {
        }

        /// <summary>
        /// Takes as input the HttpContext instance for the current request and performs
        /// any cleanup required by your session-state store provider.
        /// </summary>
        public override void EndRequest(HttpContext context)
        {
        }

        /// <summary>
        /// Frees any resources no longer in use by the session-state store provider.
        /// </summary>
        public override void Dispose()
        {
            _memcachedClient.Dispose();
        }

        /// <summary>
        /// <para>
        /// Takes as input the HttpContext instance for the current request and the 
        /// SessionID value for the current request. Retrieves session values and 
        /// information from the session data store and locks the session-item data at 
        /// the data store for the duration of the request. The GetItemExclusive method 
        /// sets several output-parameter values that inform the calling 
        /// SessionStateModule about the state of the current session-state item in the 
        /// data store.
        /// </para>
        ///     
        /// <para>
        /// If no session item data is found at the data store, the GetItemExclusive 
        /// method sets the locked output parameter to false and returns null. This 
        /// causes SessionStateModule to call the CreateNewStoreData method to create a 
        /// new SessionStateStoreData object for the request.
        /// </para>
        /// 
        /// <para>
        /// If session-item data is found at the data store but the data is locked, the 
        /// GetItemExclusive method sets the locked output parameter to true, sets the 
        /// lockAge output parameter to the current date and time minus the date and 
        /// time when the item was locked, sets the lockId output parameter to the lock 
        /// identifier retrieved from the data store, and returns null. This causes 
        /// SessionStateModule to call the GetItemExclusive method again after a 
        /// half-second interval, to attempt to retrieve the session-item information 
        /// and obtain a lock on the data. If the value that the lockAge output 
        /// parameter is set to exceeds the ExecutionTimeout value, SessionStateModule 
        /// calls the ReleaseItemExclusive method to clear the lock on the session-item 
        /// data and then call the GetItemExclusive method again.
        /// </para>
        /// 
        /// <para>
        /// The actionFlags parameter is used with sessions whose Cookieless property is
        /// true, when the regenerateExpiredSessionId attribute is set to true. An 
        /// actionFlags value set to InitializeItem (1) indicates that the entry in the 
        /// session data store is a new session that requires initialization. 
        /// Uninitialized entries in the session data store are created by a call to the
        /// CreateUninitializedItem method. If the item from the session data store is 
        /// already initialized, the actionFlags parameter is set to zero.    
        /// </para>
        /// 
        /// <para>
        /// If your provider supports cookieless sessions, set the actionFlags output 
        /// parameter to the value returned from the session data store for the current 
        /// item. If the actionFlags parameter value for the requested session-store 
        /// item equals the InitializeItem enumeration value (1), the GetItemExclusive 
        /// method should set the value in the data store to zero after setting the 
        /// actionFlags out parameter.
        /// </para>
        /// </summary>
        public override SessionStateStoreData GetItemExclusive(
                                                HttpContext context, string id,
                                                out bool locked, out TimeSpan lockAge,
                                                out object lockId,
                                                out SessionStateActions actions)
        {
            var success = false;

            do
            {
                locked = false;
                lockAge = TimeSpan.Zero;
                lockId = 0;
                actions = SessionStateActions.None;

                var getCasResult = _memcachedClient.GetWithCas<SessionData>(id);
                var sessionData = getCasResult.Result;

                if (sessionData == null)
                {
                    return null;
                }

                if (sessionData.Locked)
                {
                    locked = true;
                    lockAge = DateTime.Now - sessionData.LockTime;
                    lockId = sessionData.LockId;

                    return null;
                }

                actions = sessionData.Actions;

                sessionData.Locked = true;
                sessionData.LockTime = DateTime.Now;
                sessionData.LockId++;
                sessionData.Actions = SessionStateActions.None;

                var casResult = _memcachedClient.Cas(StoreMode.Set, id, sessionData,
                                                                        getCasResult.Cas);
                success = casResult.Result;

                if (success)
                {
                    lockId = sessionData.LockId;
                    return this.CreateSessionStateStoreData(context, actions, sessionData);
                }
            }
            while (!success);

            return null;
        }

        /// <summary>
        /// This method performs the same work as the GetItemExclusive method, except 
        /// that it does not attempt to lock the session item in the data store. The 
        /// GetItem method is called when the EnableSessionState attribute is set to 
        /// ReadOnly.
        /// </summary>
        public override SessionStateStoreData GetItem(
                                                HttpContext context, string id,
                                                out bool locked, out TimeSpan lockAge,
                                                out object lockId,
                                                out SessionStateActions actions)
        {
            locked = false;
            lockAge = TimeSpan.Zero;
            lockId = null;
            actions = SessionStateActions.None;

            var sessionData = _memcachedClient.Get<SessionData>(id);

            if (sessionData == null)
            {
                return null;
            }

            if (sessionData.Locked)
            {
                locked = true;
                lockAge = DateTime.Now - sessionData.LockTime;
                lockId = sessionData.LockId;

                return null;
            }

            actions = sessionData.Actions;

            return this.CreateSessionStateStoreData(context, actions, sessionData);
        }

        /// <summary>
        /// <para>
        /// Takes as input the HttpContext instance for the current request, the 
        /// SessionID value for the current request, a SessionStateStoreData object that
        /// contains the current session values to be stored, the lock identifier for 
        /// the current request, and a value that indicates whether the data to be 
        /// stored is for a new session or an existing session.
        /// </para>
        /// 
        /// <para>
        /// If the newItem parameter is true, the SetAndReleaseItemExclusive method 
        /// inserts a new item into the data store with the supplied values. Otherwise, 
        /// the existing item in the data store is updated with the supplied values, 
        /// and any lock on the data is released. Note that only session data for the 
        /// current application that matches the supplied SessionID value and lock 
        /// identifier values is updated.
        /// </para>
        /// 
        /// <para>
        /// After the SetAndReleaseItemExclusive method is called, the ResetItemTimeout 
        /// method is called by SessionStateModule to update the expiration date and 
        /// time of the session-item data.
        /// </para>
        /// </summary>
        public override void SetAndReleaseItemExclusive(
                                HttpContext context, string id,
                                SessionStateStoreData item,
                                object lockId, bool newItem)
        {
            var serializedContent = SessionStateItemCollectionToBytes(
                                                (SessionStateItemCollection)item.Items);
            if (newItem)
            {
                var sessionData = new SessionData(item.Timeout, serializedContent);
                _memcachedClient.Store(StoreMode.Set, id, sessionData,
                                                    TimeSpan.FromMinutes(item.Timeout));
            }
            else
            {
                var sessionData = _memcachedClient.Get<SessionData>(id);

                if (sessionData == null
                    || sessionData.LockId != (int)lockId)
                {
                    return;
                }

                sessionData.Locked = false;
                sessionData.SerializedContent = serializedContent;
                _memcachedClient.Store(StoreMode.Set, id, sessionData,
                                                TimeSpan.FromSeconds(sessionData.Timeout));
            }
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            var sessionData = _memcachedClient.Get<SessionData>(id);

            if (sessionData == null)
            {
                return;
            }

            _memcachedClient.Store(StoreMode.Set, id, sessionData,
                                            TimeSpan.FromMinutes(sessionData.Timeout));
        }

        /// <summary>
        /// Takes as input the HttpContext instance for the current request, the 
        /// SessionID value for the current request, and the lock identifier for the 
        /// current request, and releases the lock on an item in the session data store.
        /// This method is called when the GetItem or GetItemExclusive method is called
        /// and the data store specifies that the requested item is locked, but the lock
        /// age has exceeded the ExecutionTimeout value. The lock is cleared by this 
        /// method, freeing the item for use by other requests.
        /// </summary>
        public override void ReleaseItemExclusive(HttpContext context, string id,
                                                                        object lockId)
        {
            var sessionData = _memcachedClient.Get<SessionData>(id);

            if (sessionData == null
                || sessionData.LockId != (int)lockId)
            {
                return;
            }

            sessionData.Locked = false;
            _memcachedClient.Store(StoreMode.Set, id, sessionData,
                                            TimeSpan.FromSeconds(sessionData.Timeout));
        }

        /// <summary>
        /// Takes as input the HttpContext instance for the current request, the 
        /// SessionID value for the current request, and the lock identifier for the 
        /// current request, and deletes the session information from the data store 
        /// where the data store item matches the supplied SessionID value, the current 
        /// application, and the supplied lock identifier. This method is called when 
        /// the Abandon method is called.
        /// </summary>
        public override void RemoveItem(
                                HttpContext context, string id, object lockId,
                                SessionStateStoreData item)
        {
            var sessionData = _memcachedClient.Get<SessionData>(id);

            if (sessionData == null
                || sessionData.LockId != (int)lockId)
            {
                return;
            }

            _memcachedClient.Remove(id);
        }

        /// <summary>
        /// <para>
        /// Takes as input the HttpContext instance for the current request, the 
        /// SessionID value for the current request, and the Timeout value for the 
        /// current session, and adds an uninitialized item to the session data store 
        /// with an actionFlags value of InitializeItem.
        /// </para>
        /// 
        /// <para>
        /// The CreateUninitializedItem method is used with cookieless sessions when the
        /// regenerateExpiredSessionId attribute is set to true, which causes 
        /// SessionStateModule to generate a new SessionID value when an expired session
        /// ID is encountered.
        /// </para>
        /// 
        /// <para>
        /// The process of generating a new SessionID value requires the browser to be 
        /// redirected to a URL that contains the newly generated session ID. The 
        /// CreateUninitializedItem method is called during an initial request that 
        /// contains an expired session ID. After SessionStateModule acquires a new 
        /// SessionID value to replace the expired session ID, it calls the 
        /// CreateUninitializedItem method to add an uninitialized entry to the 
        /// session-state data store. The browser is then redirected to the URL 
        /// containing the newly generated SessionID value. The existence of the 
        /// uninitialized entry in the session data store ensures that the redirected 
        /// request with the newly generated SessionID value is not mistaken for a 
        /// request for an expired session, and instead is treated as a new session.
        /// </para>
        /// 
        /// <para>
        /// The uninitialized entry in the session data store is associated with the 
        /// newly generated SessionID value and contains only default values, including 
        /// an expiration date and time, and a value that corresponds to the actionFlags
        /// parameter of the GetItem and GetItemExclusive methods. The uninitialized 
        /// entry in the session state store should include an actionFlags value equal 
        /// to the InitializeItem enumeration value (1). This value is passed to 
        /// SessionStateModule by the GetItem and GetItemExclusive methods and specifies
        /// for SessionStateModule that the current session is a new session. 
        /// SessionStateModule will then initialize the new session and raise the 
        /// Session_OnStart event.
        /// </para>
        /// </summary>
        public override void CreateUninitializedItem(
                                HttpContext context, string id, int timeout)
        {
            _memcachedClient.Store(StoreMode.Set, id, SessionData.CreateUninitialized(),
                                                        TimeSpan.FromMinutes(timeout));
        }

        /// <summary>
        /// Takes as input the HttpContext instance for the current request and the 
        /// Timeout value for the current session, and returns a new 
        /// SessionStateStoreData object with an empty ISessionStateItemCollection 
        /// object, an HttpStaticObjectsCollection collection, and the specified Timeout
        /// value. The HttpStaticObjectsCollection instance for the ASP.NET application 
        /// can be retrieved using the GetSessionStaticObjects method.
        /// </summary>
        public override SessionStateStoreData CreateNewStoreData(
                                                    HttpContext context, int timeout)
        {
            return new SessionStateStoreData(
                            new SessionStateItemCollection(),
                            SessionStateUtility.GetSessionStaticObjects(context),
                            timeout);
        }

        /// <summary>
        /// Takes as input a delegate that references the Session_OnEnd event defined in
        /// the Global.asax file. If the session-state store provider supports the 
        /// Session_OnEnd event, a local reference to the SessionStateItemExpireCallback
        /// parameter is set and the method returns true; otherwise, the method returns
        /// false.
        /// </summary>
        public override bool SetItemExpireCallback(
                                        SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }

        #region 帮助方法

        private static string GetEnyimSectionName(NameValueCollection config)
        {
            if (string.IsNullOrEmpty(config["enyimSectionName"]))
            {
                return "enyim.com/memcached";
            }

            return config["enyimSectionName"];
        }

        private SessionStateStoreData CreateSessionStateStoreData(
                                       HttpContext context, SessionStateActions actions,
                                       SessionData sessionData)
        {
            if (actions == SessionStateActions.None)
            {
                var sessionItems = BytesToSessionStateItemCollection(
                                                        sessionData.SerializedContent);

                return new SessionStateStoreData(
                                sessionItems,
                                SessionStateUtility.GetSessionStaticObjects(context),
                                sessionData.Timeout);
            }
            else
            {
                return new SessionStateStoreData(
                                new SessionStateItemCollection(),
                                SessionStateUtility.GetSessionStaticObjects(context),
                                sessionData.Timeout);
            }
        }

        private static SessionStateItemCollection BytesToSessionStateItemCollection(
                                                                            byte[] bytes)
        {
            var ms = bytes == null
                        ? new MemoryStream()
                        : new MemoryStream(bytes);

            if (ms.Length <= 0)
            {
                return new SessionStateItemCollection();
            }

            using (var reader = new BinaryReader(ms))
            {
                return SessionStateItemCollection.Deserialize(reader);
            }
        }

        private static byte[] SessionStateItemCollectionToBytes(
                                                        SessionStateItemCollection items)
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms))
            {
                if (items != null)
                {
                    items.Serialize(writer);
                }

                return ms.ToArray();
            }
        }

        #endregion
    }
}
