using System.IO;

namespace FileFlows.Shared.Helpers;

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FileFlows.Shared.Models;

/// <summary>
/// A helper for HTTP processing of requests
/// </summary>
public class HttpHelper
{
    /// <summary>
    /// Gets or sets the HTTP Client used
    /// </summary>
    public static HttpClient Client { get; set; }

    /// <summary>
    /// Gets or sets the logger used
    /// </summary>
    public static Plugin.ILogger Logger { get; set; }

    /// <summary>
    /// Performs a GET request
    /// </summary>
    /// <typeparam name="T">the type of object returned by the request</typeparam>
    /// <param name="url">the URL to call</param>
    /// <returns>the request result</returns>
    public static async Task<RequestResult<T>> Get<T>(string url)
    {
        return await MakeRequest<T>(HttpMethod.Get, url);
    }
    
    /// <summary>
    /// Performs a GET request
    /// </summary>
    /// <typeparam name="T">the type of object returned by the request</typeparam>
    /// <param name="url">the URL to call</param>
    /// <param name="timeoutSeconds">the number of seconds before a timeout occurs</param>
    /// <param name="noLog">if no logging should be done for this request</param>
    /// <returns>the request result</returns>
    public static async Task<RequestResult<T>> Get<T>(string url, int timeoutSeconds = 0, bool noLog = false)
    {
        return await MakeRequest<T>(HttpMethod.Get, url, timeoutSeconds: timeoutSeconds, noLog: noLog);
    }
#if (!DEMO)
    
    /// <summary>
    /// Performs a POST request
    /// </summary>
    /// <param name="url">the URL to call</param>
    /// <param name="data">any data to send with the request</param>
    /// <param name="noLog">if no logging should be done for this request</param>
    /// <returns>the request result</returns>
    public static async Task<RequestResult<string>> Post(string url, object data = null, bool noLog = false)
    {
        return await MakeRequest<string>(HttpMethod.Post, url, data, noLog: noLog);
    }
    
    /// <summary>
    /// Performs a POST request
    /// </summary>
    /// <typeparam name="T">the type of object returned by the request</typeparam>
    /// <param name="url">the URL to call</param>
    /// <param name="data">any data to send with the request</param>
    /// <param name="timeoutSeconds">the number of seconds before a timeout occurs</param>
    /// <returns>the request result</returns>
    public static async Task<RequestResult<T>> Post<T>(string url, object data = null, int timeoutSeconds = 0)
    {
        return await MakeRequest<T>(HttpMethod.Post, url, data, timeoutSeconds: timeoutSeconds);
    }
    
    /// <summary>
    /// Performs a PUT request
    /// </summary>
    /// <param name="url">the URL to call</param>
    /// <param name="data">any data to send with the request</param>
    /// <returns>the request result</returns>
    public static async Task<RequestResult<string>> Put(string url, object data = null)
    {
        return await MakeRequest<string>(HttpMethod.Put, url, data);
    }
    
    /// <summary>
    /// Performs a PUT request
    /// </summary>
    /// <typeparam name="T">the type of object returned by the request</typeparam>
    /// <param name="url">the URL to call</param>
    /// <param name="data">any data to send with the request</param>
    /// <returns>the request result</returns>
    public static async Task<RequestResult<T>> Put<T>(string url, object data = null)
    {
        return await MakeRequest<T>(HttpMethod.Put, url, data);
    }

    /// <summary>
    /// Perform a DELETE request
    /// </summary>
    /// <param name="url">the URL to call</param>
    /// <param name="data">any data to send with the request</param>
    /// <returns>the request result</returns>
    public static async Task<RequestResult<string>> Delete(string url, object data = null)
    {
        return await MakeRequest<string>(HttpMethod.Delete, url, data);
    }


    /// <summary>
    /// Downloads a file from a URL
    /// </summary>
    /// <param name="url">the URL of the download</param>
    /// <param name="destination">where the download should be saved</param>
    /// <exception cref="Exception">throws if the file fails to download</exception>
    public static async Task DownloadFile(string url, string destination)
    {
        try
        {
#if (DEBUG)
            if (url.Contains("i18n") == false && url.StartsWith("http") == false)
                url = "http://localhost:6868" + url;
#endif

            using HttpResponseMessage response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            await using var streamToReadFrom = await response.Content.ReadAsStreamAsync();
            await using var streamToWriteTo = File.Open(destination, FileMode.Create);
            await streamToReadFrom.CopyToAsync(streamToWriteTo);
        }
        catch (Exception ex)
        {
            throw new Exception("Error downloading file: " + ex.Message);
        }
    }
#endif

    /// <summary>
    /// Logs a message to the log
    /// </summary>
    /// <param name="message">the message to log</param>
    /// <param name="error">if the message is an error message or info</param>
    private static void Log(string message, bool error = false)
    {
        var logger = Logger ?? Shared.Logger.Instance;
        if (logger != null)
        {
            if(error)
                logger.ELog(message);
            else
                logger.ILog(message);
        }
        else
            Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " - " + (error ? "ERRR" : "INFO") + " -> " + message);
    }

    /// <summary>
    /// Makes a HTTP Request
    /// </summary>
    /// <param name="method">The request method</param>
    /// <param name="url">The URL of the request</param>
    /// <param name="data">Any data to be sent with the request</param>
    /// <param name="timeoutSeconds">the number of seconds to wait before a timeout</param>
    /// <param name="noLog">if the request show record nothing to the log</param>
    /// <typeparam name="T">The request object returned</typeparam>
    /// <returns>a processing result of the request</returns>
    private static async Task<RequestResult<T>> MakeRequest<T>(HttpMethod method, string url, object data = null, int timeoutSeconds = 0, bool noLog = false)
    {
        try
        {
#if (DEBUG)
            if (url.Contains("i18n") == false && url.StartsWith("http") == false)
                url = "http://localhost:6868" + url;
#endif
            var request = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri(url, UriKind.RelativeOrAbsolute),
                Content = data != null ? AsJson(data) : null
            };

            if (method == HttpMethod.Post && data == null)
            {
                // if this is null, asp.net will return a 415 content not support, as the content-type will not be set
                request.Content = new StringContent("", Encoding.UTF8, "application/json");
            }

            if(noLog == false)
                Log("Making request[" + method + "]: " + url);
            HttpResponseMessage response;
            if (timeoutSeconds > 0)
            {
                using var cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                response = await Client.SendAsync(request, cancelToken.Token);
            }
            else
                response = await Client.SendAsync(request);

            if (typeof(T) == typeof(byte[]))
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (response.IsSuccessStatusCode)
                    return new RequestResult<T> { Success = true, Data = (T)(object)bytes };
                return new RequestResult<T> { Success = false };
            }

            string body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode && (body.Contains("INFO") == false && body.Contains("An unhandled error has occurred.")) == false)
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new FileFlows.Shared.Json.ValidatorConverter() }
                };
#pragma warning disable CS8600
                T result = string.IsNullOrEmpty(body) ? default(T) : typeof(T) == typeof(string) ? (T)(object)body : JsonSerializer.Deserialize<T>(body, options);
#pragma warning restore CS8600
                return new RequestResult<T> { Success = true, Body = body, Data = result };
            }
            else
            {
                if (body.Contains("An unhandled error has occurred."))
                    body = "An unhandled error has occurred."; // asp.net error
                else if (body.Contains("502 Bad Gateway"))
                {
                    body = "Unable to connect, server possibly down";
                    noLog = true;
                }
                if(noLog == false)
                    Log("Error Body: " + body);
                return new RequestResult<T> { Success = false, Body = body, Data = default(T) };
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// Converts an object to a json string content result
    /// </summary>
    /// <param name="o">the object to convert</param>
    /// <returns>the object as a json string content</returns>
    private static StringContent AsJson(object o)
    {
        string json = o.ToJson();
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}