using System;
using System.Collections.Generic;
using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using System.IO;
using System.Globalization;
using CsvHelper;
using Flurl.Http;


namespace ScrapySharp_scraper
{
    class Program
    {
        private static string _apiKey = "f8288a63-0f47-463d-ae4f-f4296751f935";
        static ScrapingBrowser _scrapingbrowser = new ScrapingBrowser();

        static void Main(string[] args)
        {
            var eventLinks = GetEventLinks("https://ra.co/", "https://ra.co/events/us/losangeles?page=2");
            var lstEventDetails = GetEventDetails(eventLinks);
            exportEventsToCsv(lstEventDetails);
        }

        static List<string> GetEventLinks(string baseUrl, string url)
        {
            var mainPageEventLinks = new List<string>();
            var htmlNode = GetHtml(url);
            var spansWithLinks = htmlNode.SelectNodes("//span[@href and @data-pw-test-id and @data-test-id]");

            if (spansWithLinks != null)
            {
                foreach (var span in spansWithLinks)
                {
                    var hrefAttribute = span.Attributes["href"];
                    if (hrefAttribute != null)
                    {
                        var absoluteUrl = new Uri(new Uri(baseUrl), hrefAttribute.Value).ToString();
                        mainPageEventLinks.Add(absoluteUrl);
                    }
                }
            }
            return mainPageEventLinks;
        }

        static List<EventDetails> GetEventDetails(List<string> urls)
        {
            var lstEventDetails = new List<EventDetails>();

            foreach (var url in urls)
            {
                var htmlNode = GetHtml(url);
                var eventDetails = new EventDetails();
                eventDetails.EventTitle = htmlNode.OwnerDocument.DocumentNode.SelectSingleNode("//html/head/title").InnerText;
                var description = htmlNode.OwnerDocument.DocumentNode.SelectSingleNode("//html/body/div/div[2]/section[1]/div/section[3]/section/div/div/div[2]/ul/li/div/span").InnerText;
                eventDetails.EventDescription = description.Replace("\n        \n            QR Code Link to This Post\n            \n        \n", "");
                eventDetails.EventUrl = url;
                if (eventDetails.EventTitle != null || eventDetails.EventDescription != null)
                {
                    lstEventDetails.Add(eventDetails);
                }
            }
            return lstEventDetails;
        }

        static HtmlNode GetHtml(string url)
        {
            try
            {
                var proxyUrl = GetScrapeOpsUrl(url);

                var headers = new
                {
                    UserAgent = GetRandomUserAgent()
                };

                var responseString = proxyUrl
                    .WithHeaders(headers)
                    .GetStringAsync()
                    .Result;

                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(responseString);
                return htmlDocument.DocumentNode;
            }
            catch (FlurlHttpException ex)
            {
                Console.WriteLine($"HTTP error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw;
            }
        }

        static string GetScrapeOpsUrl(string url)
        {
            var queryParams = new Dictionary<string, string>
            {
                {"api_key", _apiKey},
                {"url", url}
            };
            string queryString = string.Join("&", queryParams.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
            return $"https://proxy.scrapeops.io/v1/?{queryString}";
        }
        static void exportEventsToCsv(List<EventDetails> lstEventDetails)
        {
            string directoryPath = "/Users/guest/Desktop/ScrapySharp_scraper/CSVs/";
            Directory.CreateDirectory(directoryPath);
            using (var writer = new StreamWriter($"{directoryPath}_{DateTime.Now.ToFileTime()}.csv"))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(lstEventDetails);
            }
        }

        static string GetRandomUserAgent()
        {
            var userAgents = GetUserAgentList();
            var random = new Random();
            var randomIndex = random.Next(0, userAgents.Count);
            return userAgents[randomIndex];
        }

        static List<string> GetUserAgentList()
        {
            var responseString = $"http://headers.scrapeops.io/v1/user-agents?api_key={_apiKey}"
                .GetStringAsync()
                .Result;

            var json_response = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseString);
            var result = json_response.result;
            return result.ToObject<List<string>>();
        }

    }

    public class EventDetails
    {
        public string EventTitle { get; set; }
        public string EventDescription { get; set; }
        public string EventUrl { get; set; }
    }
}
