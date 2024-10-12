using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;

namespace TrafficCameraDownloader
{
    /// <summary>
    /// Class responsible for downloading and processing traffic camera images.
    /// </summary>
    public class TrafficCameraService
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrafficCameraService"/> class.
        /// </summary>
        /// <param name="httpClient">Injected HttpClient instance.</param>
        public TrafficCameraService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Fetches HTML content from the given URL.
        /// </summary>
        /// <param name="url">The URL to fetch the HTML content from.</param>
        /// <returns>Task that represents the asynchronous operation, containing the HTML content as a string.</returns>
        public async Task<string> GetHtmlContentAsync(string url)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Downloads an image from the given URL and saves it locally.
        /// </summary>
        /// <param name="imageUrl">The URL of the image to download.</param>
        /// <param name="savePath">The local path to save the image.</param>
        /// <returns>Task that represents the asynchronous operation.</returns>
        public async Task DownloadImageAsync(string imageUrl, string savePath)
        {
            if (string.IsNullOrEmpty(imageUrl))
                throw new ArgumentException("Image URL cannot be null or empty.", nameof(imageUrl));

            if (string.IsNullOrEmpty(savePath))
                throw new ArgumentException("Save path cannot be null or empty.", nameof(savePath));

            var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
            await File.WriteAllBytesAsync(savePath, imageBytes);
        }

        /// <summary>
        /// Extracts iframe sources from the given HTML content.
        /// </summary>
        /// <param name="htmlContent">The HTML content to process.</param>
        /// <returns>A list of iframe source URLs.</returns>
        public List<string> GetIframeSources(string htmlContent)
        {
            if (string.IsNullOrEmpty(htmlContent))
                throw new ArgumentException("HTML content cannot be null or empty.", nameof(htmlContent));

            var iframeSources = new List<string>();
            var iframeSplits = htmlContent.Split(new string[] { "<iframe" }, StringSplitOptions.None);

            for (int i = 1; i < iframeSplits.Length; i++)
            {
                var part = iframeSplits[i];
                var srcIndex = part.IndexOf("src=\"", StringComparison.InvariantCultureIgnoreCase);
                if (srcIndex != -1)
                {
                    var srcStart = srcIndex + 5;
                    var srcEnd = part.IndexOf("\"", srcStart, StringComparison.InvariantCultureIgnoreCase);

                    if (srcEnd != -1)
                    {
                        var srcValue = part.Substring(srcStart, srcEnd - srcStart);
                        iframeSources.Add(srcValue);
                    }
                }
            }

            return iframeSources;
        }

        /// <summary>
        /// Processes and downloads all traffic camera images referenced in the HTML content.
        /// </summary>
        /// <param name="htmlContent">The HTML content to process.</param>
        /// <returns>Task that represents the asynchronous operation.</returns>
        public async Task ProcessTrafficCamerasAsync(string htmlContent)
        {
            if (string.IsNullOrEmpty(htmlContent))
                throw new ArgumentException("HTML content cannot be null or empty.", nameof(htmlContent));

            var iframes = GetIframeSources(htmlContent);
            foreach (var iframeSource in iframes)
            {
                try
                {
                    var iframeUrl = iframeSource.Replace("..", "https://cic.tenerife.es/web3");
                    var iframeData = await _httpClient.GetStringAsync(iframeUrl);
                    var iframeSrcImage = ExtractImageSource(iframeData);

                    if (!string.IsNullOrEmpty(iframeSrcImage))
                    {
                        var filename = $"{iframeSrcImage.Split('/').Last()}";
                        await DownloadImageAsync(iframeSrcImage, filename);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing iframe source {iframeSource}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Extracts the image source URL from iframe data.
        /// </summary>
        /// <param name="iframeData">The iframe data.</param>
        /// <returns>The image source URL.</returns>
        private string ExtractImageSource(string iframeData)
        {
            if (string.IsNullOrEmpty(iframeData))
                throw new ArgumentException("Iframe data cannot be null or empty.", nameof(iframeData));

            var imageSplit = iframeData.Split(new string[] { "imgsrc = \"" }, StringSplitOptions.None);
            if (imageSplit.Length > 1)
            {
                return imageSplit[1].Split(new string[] { "\"" }, StringSplitOptions.None)[0];
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Main entry point for the application.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main method executed when the application starts.
        /// </summary>
        /// <returns>Task that represents the asynchronous operation.</returns>
        public static async Task Main()
        {
            var httpClient = new HttpClient();
            var service = new TrafficCameraService(httpClient);

            const string trafficCamerasUrl = "https://cic.tenerife.es/web3/mosaico_cctv/camaras_trafico_b.html";

            var htmlContent = await service.GetHtmlContentAsync(trafficCamerasUrl);
            await service.ProcessTrafficCamerasAsync(htmlContent);

            Console.WriteLine("Download completed.");
        }
    }
}