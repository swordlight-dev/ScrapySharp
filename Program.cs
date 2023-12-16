using System;
using System.Collections.Generic;
using HtmlAgilityPack;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using System.IO;
using System.Globalization;
using CsvHelper;
using Flurl.Http;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

using MySql.EntityFrameworkCore;



namespace ScrapySharp_scraper
{
    class Program
    {
        private static string _apiKey = "f8288a63-0f47-463d-ae4f-f4296751f935";
        static ScrapingBrowser _scrapingbrowser = new ScrapingBrowser();

        static void Main(string[] args)
        {
            var lstEventDetails = GetEventLinks("https://ra.co/", "https://ra.co/events/mt/all?");
            // var lstEventDetails = GetEventDetails(eventLinks);
            // exportEventsToCsv(lstEventDetails);
            ExportEventsToDb(lstEventDetails);
        }

        static List<EventDetails> GetEventLinks(string baseUrl, string url)
        {
            var mainPageEventLinks = new List<string>();
            var lstEventDetails = new List<EventDetails>();
            var htmlNode = GetHtml(url);
            // var spansWithLinks = htmlNode.SelectNodes("//span[@href and @data-pw-test-id and @data-test-id]");
            var boxElements = htmlNode.SelectNodes("//div[@data-pw-test-id='event-listing-item-nonticketed' and contains(@class, 'Box-omzyfs-0')]");

            if (boxElements != null)
            {
                foreach (var box in boxElements)
                {
                    var eventDetails = new EventDetails();

                    var anchorTag = box.SelectSingleNode(".//a[@data-pw-test-id='event-image-link']");
                    var titleTag = box.SelectSingleNode(".//h3[@data-pw-test-id='event-title']");
                    var nameElement = box.SelectSingleNode(".//span[@color='primary' and @font-weight='normal' and @class='Text-sc-1t0gn2o-0 bYvpkM']");
                    var venueSpan = box.SelectSingleNode(".//span[@data-pw-test-id='event-venue-link' and @color='primary' and @font-weight='normal']");
                    var dateSpan = box.SelectSingleNode(".//span[@color='secondary' and @font-weight='normal' and contains(@class, 'Text-sc-1t0gn2o-0') and contains(@class, 'jmZufm')]");

                    eventDetails.EventUrl = anchorTag?.Attributes["href"]?.Value;
                    eventDetails.EventTitle = titleTag?.InnerText ?? "";
                    eventDetails.EventName = nameElement?.InnerText ?? "";
                    eventDetails.EventLocation = venueSpan?.InnerText ?? "";
                    eventDetails.EventDate = dateSpan?.InnerText ?? "";
                    if (eventDetails.EventUrl != null)
                    {
                        var absoluteUrl = new Uri(new Uri(baseUrl), eventDetails.EventUrl).ToString();
                        eventDetails.EventUrl = absoluteUrl;
                    }

                    lstEventDetails.Add(eventDetails);
                }
            }
            return lstEventDetails;
        }

        // static List<EventDetails> GetEventDetails(List<string> urls)
        // {
        //     var lstEventDetails = new List<EventDetails>();

        //     foreach (var url in urls)
        //     {
        //         var htmlNode = GetHtml(url);
        //         var eventDetails = new EventDetails();
        //         eventDetails.EventTitle = htmlNode.OwnerDocument.DocumentNode.SelectSingleNode("//html/head/title").InnerText;
        //         var description = htmlNode.OwnerDocument.DocumentNode.SelectSingleNode("//html/body/div/div[2]/section[1]/div/section[3]/section/div/div/div[2]/ul/li/div/span").InnerText;
        //         eventDetails.EventDescription = description.Replace("\n        \n            QR Code Link to This Post\n            \n        \n", "");
        //         eventDetails.EventUrl = url;
        //         if (eventDetails.EventTitle != null || eventDetails.EventDescription != null)
        //         {
        //             lstEventDetails.Add(eventDetails);
        //         }
        //     }
        //     return lstEventDetails;
        // }

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
            string directoryPath = "/ScrapySharp_scraper/CSVs/";
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

        static void ExportEventsToDb(List<EventDetails> lstEventDetails)
        {
            using (var dbContext = new EventDbContext())
            {
                dbContext.Database.EnsureCreated();

                foreach (var eventDetails in lstEventDetails)
                {
                    dbContext.Events.Add(eventDetails);
                }

                dbContext.SaveChanges();
            }
        }

    }

    public class EventDetails
    {
        [Key]
        public int EventId { get; set; }
        public string EventTitle { get; set; }
        public string EventName { get; set; }
        public string EventLocation { get; set; }
        public string EventDate { get; set; }
        public string EventUrl { get; set; }
    }
    public class EventDbContext : DbContext
    {
        public DbSet<EventDetails> Events { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
           optionsBuilder.UseMySQL("Server=127.0.0.1;Database=scrapy;User=root;Password=;");
        }
    }
}
