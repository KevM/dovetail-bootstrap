using Dovetail.SDK.Bootstrap.AuthToken;
using NUnit.Framework;
using Rhino.Mocks;

namespace Dovetail.SDK.Bootstrap.Tests.Token
{
    [TestFixture]
    public class get_token_api : Context<TokenAuthenticationApi>
    {
        const string username = "username";
        const string password = "password";

        [Test]
        public void get_fails_when_authentication_fails()
        {
            MockFor<IUserAuthenticator>().Stub(s => s.Authenticate(username, password)).Return(false);

            var result = _cut.GetToken(username, password);

            result.Success.ShouldBeFalse();
        }

        [Test]
        public void get_succeeds_when_authentication_succeeds()
        {
            MockFor<IUserAuthenticator>().Stub(s => s.Authenticate(username, password)).Return(true);

            var result = _cut.GetToken(username, password);

            result.Success.ShouldBeTrue();
        }

        [Test]
        public void get_should_return_token_when_authentication_succeeds()
        {
            const string token = "existingtoken";
            MockFor<IUserAuthenticator>().Stub(s => s.Authenticate(username, password)).Return(true);
            MockFor<IAuthTokenRepository>().Stub(s => s.Get(username)).Return(token);

            var result = _cut.GetToken(username, password);

            result.Token.ShouldEqual(token);
        }
    }
}