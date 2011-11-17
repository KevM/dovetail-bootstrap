using System;
using FChoice.Foundation.Clarify;
using FubuCore;
using FubuCore.Util;

namespace Dovetail.SDK.Bootstrap.Clarify
{
    public interface IClarifySessionCache
    {
        IClarifySession GetUserSession(string username);
        IClarifySession GetApplicationSession();
        IClarifySession GetContactSession(string username);
    }

    public class ClarifySessionCache : IClarifySessionCache
    {
        private readonly IClarifyApplicationFactory _clarifyApplicationFactory;
        private readonly ILogger _logger;

        //TODO configure StructureMap to do the Create() for us
        private ClarifyApplication _clarifyApplication;
        private readonly Cache<string, Guid> _agentSessionCacheByUsername = new Cache<string, Guid>();
        private readonly Cache<string, Guid> _contactSessionCacheByUsername = new Cache<string, Guid>();
        private Guid _applicationSessionId;

        public ClarifySessionCache(IClarifyApplicationFactory clarifyApplicationFactory, ILogger logger)
        {
            _clarifyApplicationFactory = clarifyApplicationFactory;
            _logger = logger;
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

        public IClarifySession GetUserSession(string username)
        {
            var sessionId = _agentSessionCacheByUsername[username];

            try
            {
                return wrapSession(ClarifyApplication.GetSession(sessionId));
            }
            catch (Exception exception)
            {
                _logger.LogDebug("Could not retrieve agent session via id {0}. Likely it expired. Creating a new one. Error: {1}".ToFormat(sessionId, exception.Message));
                _agentSessionCacheByUsername.Remove(username);
                return GetUserSession(username);
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