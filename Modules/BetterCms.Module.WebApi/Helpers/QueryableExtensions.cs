﻿using System.Linq;

using BetterCms.Core.Api.DataContracts;
using BetterCms.Core.DataAccess.DataContext;
using BetterCms.Module.Api.Operations;

using NHibernate.Linq;

namespace BetterCms.Module.Api.Helpers
{
    public static class QueryableExtensions
    {
        public static DataListResponse<TModel> ToDataListResponse<TModel>(this IQueryable<TModel> query, DataOptions options)
        {
            var creator = new DataOptionsQueryCreator(options);

            query = query.ApplyFilter(options, creator);

            if (options.HasPaging())
            {
                var totalCount = query.ToRowCountFutureValue();

                query = query.ApplyOrder(options, creator);
                query = query.ApplyPaging(options);
                var items = query.ToFuture();

                return new DataListResponse<TModel>(items.ToList(), totalCount.Value);
            }
            else
            {
                query = query.ApplyOrder(options, creator);
                var items = query.ToList();

                return new DataListResponse<TModel>(items, items.Count);
            }
        }

        public static IQueryable<TModel> ApplyFilter<TModel>(this IQueryable<TModel> query, DataOptions options, DataOptionsQueryCreator creator = null)
        {
            if (options != null
                && options.Filter != null
                && ((options.Filter.Where != null && options.Filter.Where.Count > 0)
                    || (options.Filter.Inner != null && options.Filter.Inner.Count > 0)))
            {
                if (creator == null)
                {
                    creator = new DataOptionsQueryCreator(options);
                }

                query = query.Where(creator.GetFilterQuery());
            }

            return query;
        }

        public static IQueryable<TModel> ApplyOrder<TModel>(this IQueryable<TModel> query, DataOptions options, DataOptionsQueryCreator creator = null)
        {
            if (options != null && options.Order != null && options.Order.By != null && options.Order.By.Count > 0)
            {
                if (creator == null)
                {
                    creator = new DataOptionsQueryCreator(options);
                }

                query = query.OrderBy(creator.GetOrderQuery());
            }

            return query;
        }

        public static IQueryable<TModel> ApplyPaging<TModel>(this IQueryable<TModel> query, DataOptions options)
        {
            if (options.HasPaging())
            {
                if (options.Skip > 0)
                {
                    query = query.Skip(options.Skip.Value).Take(options.Take.Value).Cast<TModel>();
                }
                else
                {
                    query = query.Take(options.Take.Value).Cast<TModel>();
                }
            }

            return query;
        }
    }
}