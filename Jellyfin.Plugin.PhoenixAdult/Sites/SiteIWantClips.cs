using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Newtonsoft.Json;
using PhoenixAdult.Helpers;
using PhoenixAdult.Helpers.Utils;

namespace PhoenixAdult.Sites
{
    public class SiteIWantClips : IProviderBase
    {
        public async Task<List<RemoteSearchResult>> Search(int[] siteNum, string searchTitle, DateTime? searchDate, CancellationToken cancellationToken)
        {
            var result = new List<RemoteSearchResult>(1);
            if (siteNum == null || string.IsNullOrEmpty(searchTitle) || !int.TryParse(searchTitle, out _))
            {
                return result;
            }

            var sceneID = new[] { Helper.Encode(searchTitle) };

            return await Helper.GetSearchResultsFromUpdate(this, siteNum, sceneID, searchDate, cancellationToken).ConfigureAwait(false);
        }

        public async Task<MetadataResult<BaseItem>> Update(int[] siteNum, string[] sceneID, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<BaseItem>()
            {
                Item = new Movie(),
                People = new List<PersonInfo>(),
            };

            var sceneURL = Helper.Decode(sceneID[0]);
            if (!sceneURL.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                sceneURL = Helper.GetSearchBaseURL(siteNum) + sceneURL;
            }

            var data = await HTML.ElementFromURL(sceneURL, cancellationToken).ConfigureAwait(false);

            result.Item.ExternalId = sceneID[0];
            result.Item.Name = data.SelectSingleText("//span[@class=\"headline hidden-xs\"]");
            var premiereDateString = data.SelectSingleText("//div[@class=\"col-xs-12 date fix\"]").Substring("Published ".Length);
            result.Item.PremiereDate = DateTime.Parse(premiereDateString, DateTimeFormatInfo.InvariantInfo);
            result.Item.Overview = data.SelectSingleText("//div[@class=\"col-xs-12 description fix\"]/span");

            result.Item.AddStudio("IWantClips");
            result.Item.AddStudio(data.SelectSingleText("//a[@class=\"modelLink\"]"));

            var hashtags = data.SelectSingleText("//div[@class=\"col-xs-12 hashtags fix\"]/span/em");
            var categories = data.SelectSingleText("//div[@class=\"col-xs-12 category fix\"]/span");
            var genres = hashtags.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Concat(categories.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct();
            foreach (var genre in genres)
            {
                result.Item.AddGenre(genre);
            }

            return result;
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(int[] siteNum, string[] sceneID, BaseItem item, CancellationToken cancellationToken)
        {
            var result = new List<RemoteImageInfo>();

            if (sceneID == null)
            {
                return result;
            }

            var sceneURL = Helper.Decode(sceneID[0]);
            if (!sceneURL.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                sceneURL = Helper.GetSearchBaseURL(siteNum) + sceneURL;
            }

            var data = await HTML.ElementFromURL(sceneURL, cancellationToken).ConfigureAwait(false);

            var videoNode = data.SelectSingleNode("//video[contains(@id,\"video-main\")]");
            if (videoNode == null)
            {
                return result;
            }

            var videoId = videoNode.Id.Substring("video-main-".Length);
            var posterUrl = videoNode.Attributes["poster"].Value;
            string imgUrl;
            if (posterUrl.EndsWith("png") || posterUrl.EndsWith("jpg"))
            {
                imgUrl = posterUrl;
            }
            else
            {
                var posterId = posterUrl.Split('/').Last().Split('.')[0];
                imgUrl = $"https://fans.iwantclips.com/uploads/model_store/store_previews/{videoId}_400_225__{posterId}.jpg";
            }

            result.Add(new RemoteImageInfo
            {
                Url = imgUrl,
                Type = ImageType.Primary,
            });

            return result;
        }
    }
}
