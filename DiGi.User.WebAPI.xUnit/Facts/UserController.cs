using DiGi.User.PostgreSQL.Classes;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiGi.User.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that login against the stored credential issues a token carrying a jti session identifier and the
        /// session token lifetime, and that a wrong password, an unknown email and a blank password are each rejected.
        /// <para>Requires a database: returns without asserting when User_PostgreSQL_Main.conf is absent or unreachable.</para>
        /// </summary>
        [Fact]
        public async Task UserController_Login_SetsJti()
        {
            await using UserWebAPITestUser? userWebAPITestUser = await UserWebAPITestUser.CreateAsync();
            if (userWebAPITestUser is null)
            {
                return;
            }

            await using UserWebAPIHost userWebAPIHost = await UserWebAPIHost.CreateAsync();

            string? tokenString = await LoginAsync(userWebAPIHost.HttpClient);
            Assert.False(string.IsNullOrWhiteSpace(tokenString));

            JwtSecurityToken jwtSecurityToken = ReadToken(tokenString);
            Assert.False(string.IsNullOrWhiteSpace(jwtSecurityToken.Id));
            Assert.Equal(32, jwtSecurityToken.Id.Length);

            Assert.True(jwtSecurityToken.ValidTo > DateTime.UtcNow.AddMinutes(58), $"Token lifetime shorter than expected! ValidTo: {jwtSecurityToken.ValidTo}.");
            Assert.True(jwtSecurityToken.ValidTo <= DateTime.UtcNow.AddHours(1).AddMinutes(2), $"Token lifetime longer than expected! ValidTo: {jwtSecurityToken.ValidTo}.");

            // The denials matter more than the grant: a suite that only proves the right password works passes while
            // the endpoint is open to everyone.
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatusCodeAsync(userWebAPIHost.HttpClient, UserWebAPITestUser.Email, "not-the-password"));
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatusCodeAsync(userWebAPIHost.HttpClient, "other@example.com", UserWebAPITestUser.Password));
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatusCodeAsync(userWebAPIHost.HttpClient, UserWebAPITestUser.Email, string.Empty));
        }

        /// <summary>
        /// Tests that a user stored without a password credential cannot log in, even with the correct email.
        /// <para>Requires a database: returns without asserting when User_PostgreSQL_Main.conf is absent or unreachable.</para>
        /// </summary>
        [Fact]
        public async Task UserController_Login_NoCredential()
        {
            await using UserWebAPITestUser? userWebAPITestUser = await UserWebAPITestUser.CreateAsync();
            if (userWebAPITestUser is null)
            {
                return;
            }

            UserPostgreSQLConverter userPostgreSQLConverter = userWebAPITestUser.UserPostgreSQLConverter;

            // Drop the seeded row and re-insert the user alone, leaving the credential columns NULL.
            await userPostgreSQLConverter.DeleteUserByEmailAsync(UserWebAPITestUser.Email);
            List<string> insertedIds = await userPostgreSQLConverter.InsertAsync([new User.Classes.User(UserWebAPITestUser.Email)]);
            Assert.Single(insertedIds);
            Assert.Null(await userPostgreSQLConverter.GetUserCredentialAsync(UserWebAPITestUser.Email));

            await using UserWebAPIHost userWebAPIHost = await UserWebAPIHost.CreateAsync();
            Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatusCodeAsync(userWebAPIHost.HttpClient, UserWebAPITestUser.Email, UserWebAPITestUser.Password));
        }

        /// <summary>
        /// Tests that the permission level of a stored user survives a round trip through the database.
        /// <para>Requires a database: returns without asserting when User_PostgreSQL_Main.conf is absent or unreachable.</para>
        /// </summary>
        [Fact]
        public async Task UserController_Login_LevelRoundTrip()
        {
            await using UserWebAPITestUser? userWebAPITestUser = await UserWebAPITestUser.CreateAsync();
            if (userWebAPITestUser is null)
            {
                return;
            }

            UserPostgreSQLConverter userPostgreSQLConverter = userWebAPITestUser.UserPostgreSQLConverter;

            List<string> insertedIds = await userPostgreSQLConverter.InsertAsync([new User.Classes.User(UserWebAPITestUser.Email) { Level = (int)Enums.UserLevel.Admin }]);
            Assert.Single(insertedIds);

            User.Classes.User? user = await userPostgreSQLConverter.GetUserByEmailAsync(UserWebAPITestUser.Email);
            Assert.NotNull(user);
            Assert.Equal((int)Enums.UserLevel.Admin, user.Level);
            Assert.Equal(Enums.UserLevel.Admin, user.GetUserLevel());
        }

        /// <summary>
        /// Tests that the session endpoint returns the identity, session identifier and timestamps of the presented token.
        /// </summary>
        [Fact]
        public async Task UserController_Session_ReturnsClaims()
        {
            await using UserWebAPITestUser? userWebAPITestUser = await UserWebAPITestUser.CreateAsync();
            if (userWebAPITestUser is null)
            {
                return;
            }

            await using UserWebAPIHost userWebAPIHost = await UserWebAPIHost.CreateAsync();

            string? tokenString = await LoginAsync(userWebAPIHost.HttpClient);
            Assert.False(string.IsNullOrWhiteSpace(tokenString));

            SetBearer(userWebAPIHost.HttpClient, tokenString!);

            HttpResponseMessage httpResponseMessage = await userWebAPIHost.HttpClient.GetAsync("user/session");
            Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);

            string json = await httpResponseMessage.Content.ReadAsStringAsync();
            using JsonDocument jsonDocument = JsonDocument.Parse(json);

            Assert.True(jsonDocument.RootElement.TryGetProperty("email", out JsonElement jsonElement_Email));
            Assert.Equal(UserWebAPITestUser.Email, jsonElement_Email.GetString());

            JwtSecurityToken jwtSecurityToken = ReadToken(tokenString);
            Assert.True(jsonDocument.RootElement.TryGetProperty("jti", out JsonElement jsonElement_Jti));
            Assert.Equal(jwtSecurityToken.Id, jsonElement_Jti.GetString());

            Assert.True(jsonDocument.RootElement.TryGetProperty("issuedAt", out JsonElement jsonElement_IssuedAt));
            Assert.True(jsonDocument.RootElement.TryGetProperty("expiresAt", out JsonElement jsonElement_ExpiresAt));

            DateTimeOffset issuedAt = DateTimeOffset.Parse(jsonElement_IssuedAt.GetString() ?? string.Empty, CultureInfo.CurrentCulture);
            DateTimeOffset expiresAt = DateTimeOffset.Parse(jsonElement_ExpiresAt.GetString() ?? string.Empty, CultureInfo.CurrentCulture);
            Assert.True(expiresAt > issuedAt);
            Assert.True(expiresAt > DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// Tests that logout revokes the presented token so that every subsequent request carrying it, on any endpoint, is rejected.
        /// </summary>
        [Fact]
        public async Task UserController_Logout_RevokesToken()
        {
            await using UserWebAPITestUser? userWebAPITestUser = await UserWebAPITestUser.CreateAsync();
            if (userWebAPITestUser is null)
            {
                return;
            }

            await using UserWebAPIHost userWebAPIHost = await UserWebAPIHost.CreateAsync();

            string? tokenString = await LoginAsync(userWebAPIHost.HttpClient);
            Assert.False(string.IsNullOrWhiteSpace(tokenString));

            SetBearer(userWebAPIHost.HttpClient, tokenString!);

            HttpResponseMessage httpResponseMessage_SessionBefore = await userWebAPIHost.HttpClient.GetAsync("user/session");
            Assert.Equal(HttpStatusCode.OK, httpResponseMessage_SessionBefore.StatusCode);

            HttpResponseMessage httpResponseMessage_Logout = await userWebAPIHost.HttpClient.PostAsync("user/logout", EmptyJsonContent());
            Assert.Equal(HttpStatusCode.OK, httpResponseMessage_Logout.StatusCode);

            HttpResponseMessage httpResponseMessage_SessionAfter = await userWebAPIHost.HttpClient.GetAsync("user/session");
            Assert.Equal(HttpStatusCode.Unauthorized, httpResponseMessage_SessionAfter.StatusCode);

            HttpResponseMessage httpResponseMessage_SecureData = await userWebAPIHost.HttpClient.GetAsync("user/secure-data");
            Assert.Equal(HttpStatusCode.Unauthorized, httpResponseMessage_SecureData.StatusCode);

            HttpResponseMessage httpResponseMessage_LogoutAgain = await userWebAPIHost.HttpClient.PostAsync("user/logout", EmptyJsonContent());
            Assert.Equal(HttpStatusCode.Unauthorized, httpResponseMessage_LogoutAgain.StatusCode);
        }

        /// <summary>
        /// Tests that logout with a token lacking a jti claim answers bad request instead of revoking, keeping pre-feature tokens behaving as before.
        /// </summary>
        [Fact]
        public async Task UserController_Logout_MissingJti()
        {
            await using UserWebAPIHost userWebAPIHost = await UserWebAPIHost.CreateAsync();

            byte[] key = userWebAPIHost.SecurityKeyManager.GetActive()!.GetBytes();
            SecurityTokenDescriptor tokenDescriptor = new()
            {
                Subject = new ClaimsIdentity([new Claim(ClaimTypes.Email, UserWebAPITestUser.Email)]),
                Expires = DateTime.UtcNow.AddMinutes(10),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            JwtSecurityTokenHandler tokenHandler = new();
            string tokenString_Legacy = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

            SetBearer(userWebAPIHost.HttpClient, tokenString_Legacy);

            HttpResponseMessage httpResponseMessage = await userWebAPIHost.HttpClient.PostAsync("user/logout", EmptyJsonContent());
            Assert.Equal(HttpStatusCode.BadRequest, httpResponseMessage.StatusCode);
        }

        /// <summary>
        /// Tests that refresh issues a token with a fresh session identifier for the same identity, while the presented token stays valid until its own expiration.
        /// </summary>
        [Fact]
        public async Task UserController_Refresh_IssuesNewJti()
        {
            await using UserWebAPITestUser? userWebAPITestUser = await UserWebAPITestUser.CreateAsync();
            if (userWebAPITestUser is null)
            {
                return;
            }

            await using UserWebAPIHost userWebAPIHost = await UserWebAPIHost.CreateAsync();

            string? tokenString_Original = await LoginAsync(userWebAPIHost.HttpClient);
            Assert.False(string.IsNullOrWhiteSpace(tokenString_Original));

            SetBearer(userWebAPIHost.HttpClient, tokenString_Original!);

            HttpResponseMessage httpResponseMessage_Refresh = await userWebAPIHost.HttpClient.PostAsync("user/session/refresh", EmptyJsonContent());
            Assert.Equal(HttpStatusCode.OK, httpResponseMessage_Refresh.StatusCode);

            string? tokenString_Refreshed = await TokenFromResponseAsync(httpResponseMessage_Refresh);
            Assert.False(string.IsNullOrWhiteSpace(tokenString_Refreshed));

            JwtSecurityToken jwtSecurityToken_Original = ReadToken(tokenString_Original);
            JwtSecurityToken jwtSecurityToken_Refreshed = ReadToken(tokenString_Refreshed);

            Assert.NotEqual(jwtSecurityToken_Original.Id, jwtSecurityToken_Refreshed.Id);
            Assert.Equal(jwtSecurityToken_Original.Claims.First(claim => claim.Type == JwtRegisteredClaimNames.Email).Value, jwtSecurityToken_Refreshed.Claims.First(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);

            HttpResponseMessage httpResponseMessage_SessionOriginal = await userWebAPIHost.HttpClient.GetAsync("user/session");
            Assert.Equal(HttpStatusCode.OK, httpResponseMessage_SessionOriginal.StatusCode);

            SetBearer(userWebAPIHost.HttpClient, tokenString_Refreshed!);
            HttpResponseMessage httpResponseMessage_SessionRefreshed = await userWebAPIHost.HttpClient.GetAsync("user/session");
            Assert.Equal(HttpStatusCode.OK, httpResponseMessage_SessionRefreshed.StatusCode);
        }

        /// <summary>
        /// Logs in with the seeded test identity and returns the issued token string, or null when login fails.
        /// </summary>
        private static async Task<string?> LoginAsync(HttpClient httpClient)
        {
            HttpResponseMessage httpResponseMessage = await PostLoginAsync(httpClient, UserWebAPITestUser.Email, UserWebAPITestUser.Password);
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                return null;
            }

            return await TokenFromResponseAsync(httpResponseMessage);
        }

        /// <summary>
        /// Posts a login attempt and returns only its status code, for the attempts that are expected to be rejected.
        /// </summary>
        private static async Task<HttpStatusCode> LoginStatusCodeAsync(HttpClient httpClient, string email, string password)
        {
            HttpResponseMessage httpResponseMessage = await PostLoginAsync(httpClient, email, password);
            return httpResponseMessage.StatusCode;
        }

        /// <summary>
        /// Posts a login attempt for the given credentials.
        /// </summary>
        private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient httpClient, string email, string password)
        {
            string json = JsonSerializer.Serialize(new { Email = email, Password = password });
            StringContent stringContent = new(json, Encoding.UTF8, "application/json");
            return await httpClient.PostAsync("user/login", stringContent);
        }

        /// <summary>
        /// Reads the token property out of a login or refresh response body, or null when the body does not carry one.
        /// </summary>
        private static async Task<string?> TokenFromResponseAsync(HttpResponseMessage httpResponseMessage)
        {
            string json = await httpResponseMessage.Content.ReadAsStringAsync();
            using JsonDocument jsonDocument = JsonDocument.Parse(json);
            return jsonDocument.RootElement.TryGetProperty("token", out JsonElement jsonElement) ? jsonElement.GetString() : null;
        }

        /// <summary>
        /// Creates an empty JSON content for bodyless POST endpoints.
        /// </summary>
        private static StringContent EmptyJsonContent()
        {
            return new StringContent(string.Empty, Encoding.UTF8, "application/json");
        }

        /// <summary>
        /// Reads a token string into a <see cref="JwtSecurityToken"/> without validating it.
        /// </summary>
        private static JwtSecurityToken ReadToken(string? tokenString)
        {
            JwtSecurityTokenHandler tokenHandler = new();
            return tokenHandler.ReadJwtToken(tokenString);
        }

        /// <summary>
        /// Sets the bearer authorization header of the client to the given token.
        /// </summary>
        private static void SetBearer(HttpClient httpClient, string tokenString)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
        }
    }
}
