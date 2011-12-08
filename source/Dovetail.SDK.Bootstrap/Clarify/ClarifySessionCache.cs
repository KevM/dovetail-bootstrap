using System;
using FChoice.Foundation.Clarify;
using FubuCore;
using FubuCore.Util;

namespace Dovetail.SDK.Bootstrap.Clarify
{
    public interface IClarifySessionCache
    {
        IClarifySession GetUserSession(ICurrentSDKUser user);
        IClarifySession GetApplicationSession();
    }

    public class ClarifySessionCache : IClarifySessionCache
    {
        private readonly IClarifyApplicationFactory _clarifyApplicationFactory;
        private readonly ILogger _logger;
        private readonly ILocaleCache _localeCache;

        //TODO configure StructureMap to do the Create() for us
        private ClarifyApplication _clarifyApplication;
        private readonly Cache<string, Guid> _agentSessionCacheByUsername = new Cache<string, Guid>();
        private readonly Cache<string, Guid> _contactSessionCacheByUsername = new Cache<string, Guid>();
        private Guid _applicationSessionId;

        public ClarifySessionCache(IClarifyApplicationFactory clarifyApplicationFactory, ILogger logger, ILocaleCache localeCache)
        {
            _clarifyApplicationFactory = clarifyApplicationFactory;
            _logger = logger;
            _localeCache = localeCache;
            _agentSessionCacheByUsername.OnMissing = onAgentMissing;
            _contactSessionCacheByUsername.OnMissing = onContactMissing;
        }

        public ClarifyApplication ClarifyApplication
        {
            get { return _clarifyApplication ?? (_clarifyApplication = _clarifyApplicationFactory.Create()); }
        }

        private Guid onAgentMissing(string username)
        {
            _logger.LogDebug("Creating missing session for agent {0}.".ToFormat(username));

            var clarifySession = ClarifyApplication.CreateSession(username, ClarifyLoginType.User);

            _logger.LogDebug("Created session {0} for agent {1}.".ToFormat(clarifySession.SessionID, username));

            return clarifySession.SessionID;
        }

        private Guid onContactMissing(string username)
        {
            _logger.LogDebug("Creating missing session for contact {0}.".ToFormat(username));

            var clarifySession = ClarifyApplication.CreateSession(username, ClarifyLoginType.Contact);

            _logger.LogDebug("Created session {0} for contact {1}.".ToFormat(clarifySession.SessionID, username));

            return clarifySession.SessionID;
        }

        public IClarifySession GetUserSession(ICurrentSDKUser user)
        {
            var username = user.Username;
            var sessionId = _agentSessionCacheByUsername[username];

            try
            {
                var session = ClarifyApplication.GetSession(sessionId);
                session.LocalTimeZone = user.Timezone;

                return wrapSession(session);
            }
            catch (Exception exception)
            {
                _logger.LogDebug("Could not retrieve agent session via id {0}. Likely it expired. Creating a new one. Error: {1}".ToFormat(sessionId, exception.Message));
                _agentSessionCacheByUsername.Remove(username);
                return GetUserSession(user);
            }
        }

        public IClarifySession GetApplicationSession()
        {
            try
            {
                if (_applicationSessionId == Guid.Empty)
                {
                    var session = ClarifyApplication.CreateSession();
                    _applicationSessionId = session.SessionID;
                    return wrapSession(session);
                }
                
                return wrapSession(ClarifyApplication.GetSession(_applicationSessionId));
            }
            catch (Exception exception)
            {
                _logger.LogDebug("Could not retrieve application session via id {0}. Likely it expired. Creating a new one. Error: {1}".ToFormat(_applicationSessionId, exception.Message));
                _applicationSessionId = Guid.Empty;
                return GetApplicationSession();
            }
        }

        public IClarifySession GetContactSession(string username)
        {
            var sessionId = _contactSessionCacheByUsername[username];

            try
            {
                return wrapSession(ClarifyApplication.GetSession(sessionId));
            }
            catch (Exception exception)
            {
                _logger.LogDebug("Could not retrieve contact session via id {0}. Likely it expired. Creating a new one. Error: {1}".ToFormat(sessionId, exception.Message));
                _contactSessionCacheByUsername.Remove(username);
                return GetContactSession(username);
            }
        }

        private IClarifySession wrapSession(ClarifySession session)
        {
            return new ClarifySessionWrapper(session);
        }
    }
}