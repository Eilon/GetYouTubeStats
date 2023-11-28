using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace GetYouTubeStats
{
    class Program
    {
        // How to get YouTube API key: https://developers.google.com/youtube/v3/getting-started

        const string UserSecretName = "YOUTUBE_TOKEN";
        private static string PlaylistId =>
            //"PLdo4fOcmZ0oVWop1HEOml2OdqbDs6IlcI"; // .NET Conf 2020 playlist
            "PLdo4fOcmZ0oULyHSPBx-tQzePOYlhvrAU"; // .NET Conf 2023 playlist

        static async Task<int> Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            var youTubeToken = config[UserSecretName];
            if (string.IsNullOrEmpty(youTubeToken))
            {
                Console.WriteLine($"Couldn't find required user secret named '{UserSecretName}'.");
                return 1;
            }

            Console.WriteLine("Getting videos in playlist...");

            var http = new HttpClient();
            var allVideosInPlayList = new List<Item>();
            string pageToken = null;
            do
            {
                var m = await GetPlaylistPage(http, youTubeToken, PlaylistId, pageToken);
                if (m == null)
                {
                    Console.WriteLine("Failed to retrieve playlist videos.");
                    return 1;
                }
                allVideosInPlayList.AddRange(m.items);
                pageToken = m.nextPageToken;
            }
            while (!string.IsNullOrEmpty(pageToken));

            Console.WriteLine($"Found {allVideosInPlayList.Count} videos in the playlist.");

            var videoDetails = new List<VideoDetail>();

            foreach (var vid in allVideosInPlayList)
            {
                var v = await GetVideoDetails(http, youTubeToken, vid.contentDetails.videoId);
                if (v == null)
                {
                    Console.WriteLine($"Failed to retrieve video detail for video id '{vid.id}'.");
                    return 1;
                }
                videoDetails.Add(v);
            }

            Console.WriteLine("ID,Title,Likes,Views,Comments,PublishedDate");
            foreach (var item in videoDetails)
            {
                Console.WriteLine(
                    string.Join(',',
                        new[] {
                            $"https://www.youtube.com/watch?v={item.ID}",
                            item.Title,
                            item.Likes.ToString(CultureInfo.InvariantCulture),
                            item.Views.ToString(CultureInfo.InvariantCulture),
                            item.Comments.ToString(CultureInfo.InvariantCulture),
                            item.PublishedDate.ToString(CultureInfo.InvariantCulture),
                        }
                        .Select(s => $"\"{s}\"")));
            }

            Console.ReadLine();

            return 0;
        }

        public class VideoDetail
        {
            public string Title { get; set; }
            public string ID { get; set; }
            public int Likes { get; set; }
            public int Views { get; set; }
            public int Comments { get; set; }
            public DateTime PublishedDate { get; set; }
        }

        private static async Task<VideoDetail> GetVideoDetails(HttpClient http, string youTubeToken, string id)
        {
            var videoStatsUrl = $"https://youtube.googleapis.com/youtube/v3/videos?part=snippet%2Cstatistics&id={id}&key={youTubeToken}";
            var res = await http.GetAsync(videoStatsUrl);
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine("Bad status code: " + res.StatusCode);
                return null;
            }
            var r = await res.Content.ReadAsStringAsync();
            var z = JsonSerializer.Deserialize<Rootobject>(r);
            return new VideoDetail
            {
                ID = z.items[0].id,
                Title = z.items[0].snippet.title,
                Likes = int.Parse(z.items[0].statistics.likeCount, CultureInfo.InvariantCulture),
                Views = int.Parse(z.items[0].statistics.viewCount, CultureInfo.InvariantCulture),
                Comments = int.Parse(z.items[0].statistics.commentCount, CultureInfo.InvariantCulture),
                PublishedDate = z.items[0].snippet.publishedAt,
            };
        }

        private static async Task<Rootobject> GetPlaylistPage(HttpClient http, string youTubeToken, string playlistId, string pageToken)
        {
            var playListItemsUrl = $"https://youtube.googleapis.com/youtube/v3/playlistItems?part=contentDetails&maxResults=50&playlistId={playlistId}&key={youTubeToken}";
            if (!string.IsNullOrEmpty(pageToken))
            {
                playListItemsUrl += $"&pageToken={pageToken}";
            }
            var res = await http.GetAsync(playListItemsUrl);
            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine("Bad status code: " + res.StatusCode);
                return null;
            }
            var r = await res.Content.ReadAsStringAsync();
            var z = JsonSerializer.Deserialize<Rootobject>(r);
            return z;
        }

        public class Rootobject
        {
            public string kind { get; set; }
            public string etag { get; set; }
            public string nextPageToken { get; set; }
            public Item[] items { get; set; }
            public Pageinfo pageInfo { get; set; }
        }

        public class Pageinfo
        {
            public int totalResults { get; set; }
            public int resultsPerPage { get; set; }
        }

        public class Item
        {
            public string kind { get; set; }
            public string etag { get; set; }
            public string id { get; set; }
            public Contentdetails contentDetails { get; set; }
            public Snippet snippet { get; set; }
            public Statistics statistics { get; set; }
        }

        public class Snippet
        {
            public DateTime publishedAt { get; set; }
            public string channelId { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public Thumbnails thumbnails { get; set; }
            public string channelTitle { get; set; }
            public string[] tags { get; set; }
            public string categoryId { get; set; }
            public string liveBroadcastContent { get; set; }
            public string defaultLanguage { get; set; }
            public Localized localized { get; set; }
            public string defaultAudioLanguage { get; set; }
        }

        public class Thumbnails
        {
            public Default _default { get; set; }
            public Medium medium { get; set; }
            public High high { get; set; }
            public Standard standard { get; set; }
            public Maxres maxres { get; set; }
        }

        public class Default
        {
            public string url { get; set; }
            public int width { get; set; }
            public int height { get; set; }
        }

        public class Medium
        {
            public string url { get; set; }
            public int width { get; set; }
            public int height { get; set; }
        }

        public class High
        {
            public string url { get; set; }
            public int width { get; set; }
            public int height { get; set; }
        }

        public class Standard
        {
            public string url { get; set; }
            public int width { get; set; }
            public int height { get; set; }
        }

        public class Maxres
        {
            public string url { get; set; }
            public int width { get; set; }
            public int height { get; set; }
        }

        public class Localized
        {
            public string title { get; set; }
            public string description { get; set; }
        }

        public class Statistics
        {
            public string viewCount { get; set; }
            public string likeCount { get; set; }
            public string favoriteCount { get; set; }
            public string commentCount { get; set; }
        }

        public class Contentdetails
        {
            public string videoId { get; set; }
            public DateTime videoPublishedAt { get; set; }
        }
    }
}
