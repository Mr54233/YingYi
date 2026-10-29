using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Api;
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
            var item = _libraryManager.GetItemById(request.Id);
            if (item == null)
            {
                throw new ArgumentException("Item not found: " + request.Id, nameof(request.Id));
            }

            return await _ratingAggregationService.BuildSummaryAsync(item, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
