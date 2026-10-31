using MediaBrowser.Controller.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using StrmAssistant.Web.Api;
using System;
using System.Collections.Generic;

namespace StrmAssistant.Web.Service
{
    public class ItemService : BaseApiService
    {
        private readonly ILibraryManager _libraryManager;

        public ItemService(ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }

        public void Post(LockItem request)
        {
            BaseItem itemById;
            try
            {
                itemById = _libraryManager.GetItemById(request.ItemId);
            }
            catch (Exception e)
            {
                // GetItemById throws (e.g. "Unrecognized Guid format") for a malformed id —
                // same guard as LibraryStructureService. NAS 真机实测：非法 ID 直接 500。
                throw new ArgumentException("Invalid item id: " + request.ItemId, nameof(request.ItemId), e);
            }

            if (itemById == null)
            {
                throw new ArgumentException("Item not found: " + request.ItemId, nameof(request.ItemId));
            }

            var items = new List<BaseItem> { itemById };
            var sameKey = _libraryManager.GetItemList(new InternalItemsQuery
            {
                PresentationUniqueKey = itemById.PresentationUniqueKey
            });
            foreach (var k in sameKey)
            {
                if (k != null && !items.Contains(k))
                {
                    items.Add(k);
                }
            }

            foreach (var item in items)
            {
                if (item.IsLocked != request.LockData)
                {
                    item.IsLocked = request.LockData;
                    item.UpdateToRepository(ItemUpdateType.MetadataEdit);
                }

                if (item is Folder folder)
                {
                    foreach (var child in folder.GetItemList(new InternalItemsQuery { Recursive = true }))
                    {
                        if (child.IsLocked != request.LockData)
                        {
                            child.IsLocked = request.LockData;
                            child.UpdateToRepository(ItemUpdateType.MetadataEdit);
                        }
                    }
                }
            }
        }
    }
}
