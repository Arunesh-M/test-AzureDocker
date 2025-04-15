using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.DevTools;
using DevToolsSessionDomains = OpenQA.Selenium.DevTools.V134.DevToolsSessionDomains;
using Network = OpenQA.Selenium.DevTools.V134.Network;
using Newtonsoft.Json;
using System.Net;
using OpenQA.Selenium.Support.UI;

namespace AzureCheck
{
    public class AzureTest
    {
        private readonly ILogger<AzureTest> _logger;

        public AzureTest(ILogger<AzureTest> logger)
        {
            _logger = logger;
        }

        [Function("PerformLogin")]
        public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            try
            {
                _logger.LogInformation("Azure Function Started:::::::\n");

                // Initialize Chrome Options for headless browsing
                ChromeOptions options = new ChromeOptions();
                
                options.AddArguments("--no-sandbox");
                options.AddArguments("--disable-gpu");
                options.AddArguments("--headless");
                options.AddArguments("--disable-dev-shm-usage");
              

                IWebDriver driver = new ChromeDriver(options);

                // Get credentials from environment variables 
                var userName = Environment.GetEnvironmentVariable("marcos_username");
                var password = Environment.GetEnvironmentVariable("marcos_password");

                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                TokenResponse tokenResponse = new TokenResponse();
                string tokenResponseString="";

                if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
                {
                    _logger.LogError("Error: Username or Password environment variables are null or not set");
                    return new BadRequestObjectResult("Username or password not set");
                }

                // Setup DevTools session
                var devTools = driver as IDevTools;
                if (devTools != null)
                {
                    IDevToolsSession session = devTools.GetDevToolsSession();
                    var domains = session.GetVersionSpecificDomains<DevToolsSessionDomains>();
                    await domains.Network.Enable(new Network.EnableCommandSettings());

                    domains.Network.ResponseReceived += async (sender, e) =>
                    {

                        _logger.LogInformation($"URL::::::{e.Response.Url}\n");

                        if (e.Response.Url.Contains("https://identity.marcosoms.com/connect/token") && e.Response.MimeType.Contains("application/json"))
                        {
                            try
                            {
                                var responseBody = await domains.Network.GetResponseBody(new Network.GetResponseBodyCommandSettings
                                {
                                    RequestId = e.RequestId
                                });


                                string jsonResponse = responseBody.Body;
                                tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(jsonResponse);

                                // Log the token response
                                _logger.LogInformation($"ID Token:::: {tokenResponse.IdToken}\n");
                                _logger.LogInformation($"Access Token::: {tokenResponse.AccessToken}\n");
                                _logger.LogInformation($"Expires In:::: {tokenResponse.ExpiresIn}\n");
                                _logger.LogInformation($"Token Type::::: {tokenResponse.TokenType}\n");
                                _logger.LogInformation($"Refresh Token::::: {tokenResponse.RefreshToken}\n");
                                _logger.LogInformation($"Scope::::: {tokenResponse.Scope}\n");

                                tokenResponseString = $"IdToken:\'{tokenResponse.IdToken}'\n\nAccessToken:'{tokenResponse.AccessToken}'\n\nExpiresIn:{tokenResponse.ExpiresIn}\n\nTokenType:'{tokenResponse.TokenType}'\n\nRefreshToken:'{tokenResponse.RefreshToken}'\n\nScope:'{tokenResponse.RefreshToken}'";
                                tcs.SetResult(true);

                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"An error occurred while processing the response: {ex.Message}");
                                tcs.SetResult(false);
                            }
                        }

                    };
                }

                // Navigate to the login page
                driver.Navigate().GoToUrl("https://identity.marcosoms.com/Account/Login?ReturnUrl=%2Fconnect%2Fauthorize%2Fcallback%3Fclient_id%3De73778ec-e4b2-4c62-b4e8-e1ee7b106bec%26redirect_uri%3Dhttps%253A%252F%252Fcloud.marcosoms.com%252Fauthentication%252Flogin-callback%26response_type%3Dcode%26scope%3Dopenid%2520profile%2520offline_access%2520identity_api_scope%2520console_api_scope%2520openid%2520profile%26state%3Dfe00047ff59b4008a1b4ddd172425f14%26code_challenge%3D42l4LFNh17O5iQfxcogeJopGiGcP4Do92PdZ8lP0ANs%26code_challenge_method%3DS256%26response_mode%3Dquery"); // Replace with your URL
                var usernameElement = driver.FindElement(By.Id("Input_Username"));
                var passwordElement = driver.FindElement(By.Id("Input_Password"));

                // Enter credentials
                usernameElement.SendKeys(userName);
                passwordElement.SendKeys(password);
                driver.FindElement(By.XPath("/html/body/div[2]/div/div[2]/div/div/div[2]/form/button[1]")).Click();

                // Wait for the response and refresh (if needed)
                driver.Navigate().Refresh();
                driver.Navigate().Refresh();

                try
                {
                    WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(70));
                    wait.Until(driver =>
                    {
                        var errorElements = driver.FindElements(By.XPath("//*[contains(@class, 'alert alert-danger')]"));
                        if (errorElements.Count > 0 && errorElements[0].Displayed && errorElements[0].GetAttribute("class").Contains("alert-danger"))
                        {
                            tokenResponseString = "Failed to fetch the response token.";
                            _logger.LogInformation(tokenResponseString);
                            tcs.SetResult(true);
                            return true;
                        }

                        var loginElements = driver.FindElements(By.XPath("//p[contains(text(),'Completing login...')]"));
                        if (loginElements.Count > 0 && loginElements[0].Displayed)
                        {
                            _logger.LogInformation("Completing login...' message is visible.\n");
                            return true;
                        }

                        return false; // Keep waiting if neither condition met
                    });
                }

                catch (WebDriverTimeoutException)
                {
                    tokenResponseString = "No response after the expected time.";
                    _logger.LogInformation(tokenResponseString);
                    tcs.SetResult(false);
                }

                await tcs.Task;
                driver.Quit();

                return new OkObjectResult($"Process Completed\n\n{tokenResponseString}");

            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
                return new StatusCodeResult(500);
            }
        }


        public class TokenResponse
        {
            [JsonProperty("id_token")]
            public string IdToken { get; set; }

            [JsonProperty("access_token")]
            public string AccessToken { get; set; }

            [JsonProperty("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonProperty("token_type")]
            public string TokenType { get; set; }

            [JsonProperty("refresh_token")]
            public string RefreshToken { get; set; }

            [JsonProperty("scope")]
            public string Scope { get; set; }
        }
    }
}
