using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    internal static class Helper
    {
        private const int DataverseRetrieveMultiplePageSize = 5000;

        internal static List<Entity> RetrieveAll(IOrganizationService service, QueryExpression query)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (query.TopCount.HasValue)
            {
                throw new InvalidPluginExecutionException("RetrieveAll cannot be used with QueryExpression.TopCount.");
            }

            var results = new List<Entity>();
            query.PageInfo = new PagingInfo
            {
                Count = DataverseRetrieveMultiplePageSize,
                PageNumber = 1,
                PagingCookie = null
            };

            while (true)
            {
                var page = service.RetrieveMultiple(query);
                AddRange(results, page);

                if (!page.MoreRecords)
                {
                    return results;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }
        }

        internal static Entity RetrieveFirst(IOrganizationService service, QueryExpression query)
        {
            var page = RetrieveSinglePage(service, query);
            return page.Entities.Count == 0 ? null : page.Entities[0];
        }

        private static EntityCollection RetrieveSinglePage(IOrganizationService service, QueryExpression query)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return service.RetrieveMultiple(query);
        }

        private static void AddRange(List<Entity> results, EntityCollection collection)
        {
            if (collection == null)
            {
                return;
            }

            foreach (var entity in collection.Entities)
            {
                results.Add(entity);
            }
        }
    }
}
