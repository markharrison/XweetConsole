using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OAuth;
using Microsoft.Extensions.Configuration;
using HtmlAgilityPack;

namespace XweetConsole
{
    class Program
    {
        static string consumerKey = "";
        static string consumerSecret = "";
        static string accessToken = "";
        static string accessTokenSecret = "";

        static string tweetUrl = "https://api.twitter.com/2/tweets";
        static string uploadUrl = "https://upload.twitter.com/1.1/media/upload.json";

        static async Task<string> GetTwitterImageFromUrl(string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string htmlContent = await client.GetStringAsync(url);

                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(htmlContent);

                    var twitterImageMetaTag = htmlDoc.DocumentNode.SelectSingleNode("//meta[@name='twitter:image']");
                    if (twitterImageMetaTag != null)
                    {
                        return twitterImageMetaTag.GetAttributeValue("content", null);
                    }

                    var twitterImageSrcMetaTag = htmlDoc.DocumentNode.SelectSingleNode("//meta[@name='twitter:image:src']");
                    if (twitterImageSrcMetaTag != null)
                    {
                        return twitterImageSrcMetaTag.GetAttributeValue("content", null);
                    }

                    var ogImageMetaTag = htmlDoc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
                    if (ogImageMetaTag != null)
                    {
                        return ogImageMetaTag.GetAttributeValue("content", null);
                    }

                    var ogImageSecureUrlMetaTag = htmlDoc.DocumentNode.SelectSingleNode("//meta[@property='og:image:secure_url']");
                    if (ogImageSecureUrlMetaTag != null)
                    {
                        return ogImageSecureUrlMetaTag.GetAttributeValue("content", null);
                    }

                    var ogImageUrlMetaTag = htmlDoc.DocumentNode.SelectSingleNode("//meta[@property='og:image:url']");
                    if (ogImageUrlMetaTag != null)
                    {
                        return ogImageUrlMetaTag.GetAttributeValue("content", null);
                    }

                    var linkImageSrcTag = htmlDoc.DocumentNode.SelectSingleNode("//link[@rel='image_src']");
                    if (linkImageSrcTag != null)
                    {
                        return linkImageSrcTag.GetAttributeValue("href", null);
                    }

                    var thumbnailMetaTag = htmlDoc.DocumentNode.SelectSingleNode("//meta[@name='thumbnail']");
                    if (thumbnailMetaTag != null)
                    {
                        return thumbnailMetaTag.GetAttributeValue("content", null);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            return null;
        }

        static async Task<string> PostXweet(string tweetText, string? imagePath = null)
        {

            dynamic payload = new
            {
                text = tweetText
            };

            try
            {
                if (!string.IsNullOrEmpty(imagePath))
                {

                    byte[] imageData;

                    if (Uri.IsWellFormedUriString(imagePath, UriKind.Absolute))
                    {
                        using (HttpClient client = new HttpClient())
                        {
                            imageData = await client.GetByteArrayAsync(imagePath);
                        }
                    }
                    else
                    {
                        // If imagePath is a local file path, read the image data from the file
                        imageData = File.ReadAllBytes(imagePath);
                    }

                    var content = new MultipartFormDataContent();
                    var imageContent = new ByteArrayContent(imageData);
                    imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                    content.Add(imageContent, "media");

                    using (var httpClient = new HttpClient())
                    {

                        OAuthRequest oAuthRequest =
                                OAuthRequest.ForProtectedResource("POST", consumerKey, consumerSecret,
                                                     accessToken, accessTokenSecret);

                        oAuthRequest.RequestUrl = uploadUrl;

                        string oAuthHeaderValue = oAuthRequest.GetAuthorizationHeader();

                        httpClient.DefaultRequestHeaders.Add("Authorization", oAuthHeaderValue);

                        var uploadResponse = await httpClient.PostAsync(uploadUrl, content);

                        if (uploadResponse.IsSuccessStatusCode)
                        {
                            var uploadJsonResponse = await uploadResponse.Content.ReadAsStringAsync();
                            Console.WriteLine("Media posted: " + uploadJsonResponse);

                            using var uploadDoc = JsonDocument.Parse(uploadJsonResponse);
                            string mediaId = uploadDoc.RootElement.GetProperty("media_id_string").GetString() ?? string.Empty;

                            payload = new
                            {
                                text = tweetText,
                                media = new
                                {
                                    media_ids = new string[] { mediaId }
                                }
                            };

                        }
                        else
                        {
                            Console.WriteLine("Error posting media: " + uploadResponse.StatusCode);
                            return $"Error {uploadResponse.StatusCode}";
                        }

                    }

                }

                using (var httpClient = new HttpClient())
                {
                    var tweetContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");


                    OAuthRequest oAuthRequest =
                            OAuthRequest.ForProtectedResource("POST", consumerKey, consumerSecret,
                                                 accessToken, accessTokenSecret);

                    oAuthRequest.RequestUrl = tweetUrl;

                    string oAuthHeaderValue = oAuthRequest.GetAuthorizationHeader();

                    // add header to httpClient
                    httpClient.DefaultRequestHeaders.Add("Authorization", oAuthHeaderValue);
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Your code to use httpClient
                    var tweetResponse = await httpClient.PostAsync(tweetUrl, tweetContent);

                    if (tweetResponse.IsSuccessStatusCode)
                    {
                        var tweetJsonResponse = await tweetResponse.Content.ReadAsStringAsync();
                        Console.WriteLine("Tweet posted: " + tweetJsonResponse);
                    }
                    else
                    {
                        Console.WriteLine("Error posting tweet: " + tweetResponse.StatusCode);
                        return $"Error {tweetResponse.StatusCode}";
                    }

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return "OK";
        }

        static async Task Main(string[] args)
        {
            var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

            var builder = new ConfigurationBuilder()
                   .SetBasePath(Directory.GetCurrentDirectory())
                   .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                   .AddJsonFile($"appsettings.Development.json", optional: false)
                   .AddEnvironmentVariables();

            IConfigurationRoot configuration = builder.Build();

            consumerKey = configuration["ConsumerKey"] ?? "";
            consumerSecret = configuration["ConsumerSecret"] ?? "";
            accessToken = configuration["AccessToken"] ?? "";
            accessTokenSecret = configuration["AccessTokenSecret"] ?? "";

            //string imagePath = configuration["ImageDirectory"] +  "cat.png";
            string textTags = "#Microsoft #Azure #AppDev";
            string tweetText = "Hello Azure1";
            string pageUrl = "https://github.blog/ai-and-ml/generative-ai/what-are-ai-agents-and-why-do-they-matter";

            tweetText += " " + pageUrl + Environment.NewLine + textTags;

            string imgUrl = await GetTwitterImageFromUrl(pageUrl);
            await PostXweet(tweetText, imgUrl);

            await Task.Run(() => { });

        }
    }

}
