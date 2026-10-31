using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using StrmAssistant.Rating;
using StrmAssistant.Web.Api;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Web.Service
{
    public class RatingService : BaseApiService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly RatingAggregationService _ratingAggregationService;

        public RatingService(ILibraryManager libraryManager, IHttpClient httpClient,
            IJsonSerializer jsonSerializer)
        {
            _libraryManager = libraryManager;
            _ratingAggregationService = new RatingAggregationService(libraryManager, httpClient, jsonSerializer);
        }

        public async Task<RatingsSummary> Get(GetRatingsSummary request)
        {
            BaseItem item;
            try
            {
                item = _libraryManager.GetItemById(request.Id);
            }
            catch (Exception e)
            {
                // GetItemById throws for malformed ids (e.g. "Unrecognized Guid format",
                // 500-char garbage). NAS 真机实测：超长/非法 ID 直接 500。统一映射 400。
                throw new ArgumentException("Invalid item id: " + request.Id, nameof(request.Id), e);
            }

            if (item == null)
            {
                throw new ArgumentException("Item not found: " + request.Id, nameof(request.Id));
            }

            return await _ratingAggregationService.BuildSummaryAsync(item, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
