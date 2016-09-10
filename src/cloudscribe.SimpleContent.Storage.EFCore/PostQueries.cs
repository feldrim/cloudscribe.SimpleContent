﻿// Copyright (c) Source Tree Solutions, LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Author:					Joe Audette
// Created:					2016-08-31
// Last Modified:			2016-09-09
// 

using cloudscribe.SimpleContent.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cloudscribe.SimpleContent.Storage.EFCore.Models;

namespace cloudscribe.SimpleContent.Storage.EFCore
{
    public class PostQueries : IPostQueries
    {
        public PostQueries(SimpleContentDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        private SimpleContentDbContext dbContext;

        private const string PostContentType = "Post";

        /// <summary>
        /// this query is only used for the google site map so we don't need to get comments
        /// </summary>
        /// <param name="blogId"></param>
        /// <param name="includeUnpublished"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<List<IPost>> GetPosts(
            string blogId,
            bool includeUnpublished,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            var query = dbContext.Posts
                .Where(p =>
                    p.BlogId == blogId
                    &&  (includeUnpublished || (p.IsPublished && p.PubDate <= DateTime.UtcNow))
                      )
                      .OrderByDescending(p => p.PubDate);

            var list = await query
                .AsNoTracking()
                .ToListAsync<IPost>();
            
            return list;
        }

        public async Task<PagedPostResult> GetPosts(
            string blogId,
            string category,
            bool includeUnpublished,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            int offset = (pageSize * pageNumber) - pageSize;
            var currentTime = DateTime.UtcNow;

            var result = new PagedPostResult();

            var query = dbContext.Posts
                .Include(p => p.PostComments)
                .Where(x =>
                x.BlogId == blogId
                && (includeUnpublished || (x.IsPublished && x.PubDate <= currentTime))
                && (string.IsNullOrEmpty(category) || x.CategoriesCsv.Contains(category)) // will this work?
                )
                //.Select(x => new Post
                //{
                //    // note that this will have to be updated if there are any new properties added to Post
                //    // maybe better perf to not do this and instead just select post
                //    // could iterate through the list before returning it to update the categores? or is the shado property only available in the query?
                //    Id = x.Id,
                //    //Categories = EF.Property<string>(x, "CategoryCsv").Split(new char[] { ',' }, 
                //    //    StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim().ToLower()).ToList(), // get from shadow property
                //    Author = x.Author,
                //    BlogId = x.BlogId,
                //    Content = x.Content,
                //    IsPublished = x.IsPublished,
                //    LastModified = x.LastModified,
                //    MetaDescription = x.MetaDescription,
                //    PubDate = x.PubDate,
                //    Slug = x.Slug,
                //    Title = x.Title,
                //    Comments = x.Comments
                //    //, Comments = is is possible to sub query here without navigation property
                //}
                 
               // )
                .OrderByDescending(x => x.PubDate)
                ;

            
            var posts = await query
                .AsNoTracking()
                .Skip(offset)
                .Take(pageSize)
                .ToListAsync<IPost>(cancellationToken)
                .ConfigureAwait(false);

            int totalPosts = await GetCount(blogId, category, includeUnpublished, cancellationToken).ConfigureAwait(false);
            
            result.Data = posts;
            result.TotalItems = totalPosts;

            return result;
        }

        //private async Task<List<Comment>> GetCommentsForPageOfPosts(
        //    string blogId,
        //    string category,
        //    bool includeUnpublished,
        //    DateTime currentTime,
        //    int pageNumber,
        //    int pageSize,
        //    CancellationToken cancellationToken = default(CancellationToken)
        //    )
        //{
        //    ThrowIfDisposed();
        //    cancellationToken.ThrowIfCancellationRequested();

        //    int offset = (pageSize * pageNumber) - pageSize;


        //    var result = new List<Comment>();

        //    var query = from c in dbContext.Comments
        //            join x in dbContext.Posts
        //            on c.ContentId equals x.Id
        //            orderby x.PubDate
        //                where (
        //        x.BlogId == blogId
        //        && (includeUnpublished || (x.IsPublished && x.PubDate <= currentTime))
        //        && (string.IsNullOrEmpty(category) || (EF.Property<string>(x, "CategoryCsv").Contains(category))) // will this work?
        //        )

        //        select c
                
        //        ;


        //    //List<Comment> posts = await query
        //    //    .AsNoTracking()
        //    //    .Skip(offset)
        //    //    .Take(pageSize)
        //    //    .ToListAsync<Post>(cancellationToken)
        //    //    .ConfigureAwait(false);

           
           

        //    return result;
        //}

        public async Task<int> GetCount(
            string blogId,
            string category,
            bool includeUnpublished,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var currentTime = DateTime.UtcNow;

            var count = await dbContext.Posts
                .CountAsync(x =>
                x.BlogId == blogId
                && (includeUnpublished || (x.IsPublished && x.PubDate <= currentTime))
                && (string.IsNullOrEmpty(category) || x.CategoriesCsv.Contains(category)) // will this work?
                );
            
            return count;
        }

        public async Task<List<IPost>> GetRecentPosts(
            string blogId,
            int numberToGet,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            
            var query = dbContext.Posts
               // .Include(p => p.Comments) //think this is only used to populate a list in OLW so don't need the comments
                .Where(p =>
                p.IsPublished
                && p.PubDate <= DateTime.UtcNow)
                .OrderByDescending(p => p.PubDate)
                ;

            return await query
                .AsNoTracking()
                .Take(numberToGet)
                .ToListAsync<IPost>()
                .ConfigureAwait(false);

        }

        public async Task<PagedPostResult> GetPosts(
            string blogId,
            int year,
            int month = 0,
            int day = 0,
            int pageNumber = 1,
            int pageSize = 10,
            bool includeUnpublished = false,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            
            IQueryable<PostEntity> query;
            
            if (day > 0 && month > 0)
            {
                query = dbContext.Posts
                     .Include(p => p.PostComments)
                    .Where(
                x => x.PubDate.Year == year
                && x.PubDate.Month == month
                && x.PubDate.Day == day
                && (includeUnpublished || (x.IsPublished
                && x.PubDate <= DateTime.UtcNow))
                )
                ;
            }
            else if (month > 0)
            {
                query = dbContext.Posts
                     .Include(p => p.PostComments)
                    .Where(
                x => x.PubDate.Year == year
                && x.PubDate.Month == month
                && (includeUnpublished || (x.IsPublished
                && x.PubDate <= DateTime.UtcNow))
                )
                ;

            }
            else
            {
                query = dbContext.Posts
                     .Include(p => p.PostComments)
                     .Where(
                x => x.PubDate.Year == year
                )
                ;
            }
            
            int offset = (pageSize * pageNumber) - pageSize;
            var posts = await query
                .AsNoTracking()
                //.Select(x => new Post
                //{
                //    // note that this will have to be updated if there are any new properties added to Post
                //    // maybe better perf to not do this and instead just select post
                //    // could iterate through the list before returning it to update the categores? or is the shado property only available in the query?
                //    Id = x.Id,
                //    Categories = EF.Property<string>(x, "CategoryCsv").Split(new char[] { ',' },
                //        StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim().ToLower()).ToList(), // get from shadow property
                //    Author = x.Author,
                //    BlogId = x.BlogId,
                //    Content = x.Content,
                //    IsPublished = x.IsPublished,
                //    LastModified = x.LastModified,
                //    MetaDescription = x.MetaDescription,
                //    PubDate = x.PubDate,
                //    Slug = x.Slug,
                //    Title = x.Title,
                //    Comments = x.Comments
                //    //, Comments = is is possible to sub query here without navigation property
                //})
                .Skip(offset)
                .Take(pageSize)
                .ToListAsync<IPost>();

            var result = new PagedPostResult();
            result.Data = posts;
            result.TotalItems = await GetCount(blogId, year, month, day, includeUnpublished, cancellationToken).ConfigureAwait(false);

            return result;
            
        }

        public async Task<int> GetCount(
            string blogId,
            int year,
            int month = 0,
            int day = 0,
            bool includeUnpublished = false,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            IQueryable<PostEntity> query;

            if (day > 0 && month > 0)
            {
                query =  dbContext.Posts.Where(
                x => x.PubDate.Year == year
                && x.PubDate.Month == month
                && x.PubDate.Day == day
                && (includeUnpublished || (x.IsPublished
                && x.PubDate <= DateTime.UtcNow))
                )
                ;
            }
            else if (month > 0)
            {
                query = dbContext.Posts.Where(
                x => x.PubDate.Year == year
                && x.PubDate.Month == month
                && (includeUnpublished || (x.IsPublished
                && x.PubDate <= DateTime.UtcNow))
                )
                ;

            }
            else
            {
                query = dbContext.Posts.Where(
                x => x.PubDate.Year == year
                )
                ;
            }

            return await query.CountAsync<PostEntity>().ConfigureAwait(false);

        }


        public async Task<IPost> GetPost(
            string blogId,
            string postId,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var query = dbContext.Posts
                     .Include(p => p.PostComments)
                     .Where(p => p.Id == postId && p.BlogId == blogId)
                     //.Select(x => new Post
                     //{
                     //    // note that this will have to be updated if there are any new properties added to Post
                     //    // maybe better perf to not do this and instead just select post
                     //    // could iterate through the list before returning it to update the categores? or is the shado property only available in the query?
                     //    Id = x.Id,
                     //    //Categories = EF.Property<string>(x, "CategoryCsv").Split(new char[] { ',' },
                     //   //StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim().ToLower()).ToList(), // get from shadow property
                     //    Author = x.Author,
                     //    BlogId = x.BlogId,
                     //    Content = x.Content,
                     //    IsPublished = x.IsPublished,
                     //    LastModified = x.LastModified,
                     //    MetaDescription = x.MetaDescription,
                     //    PubDate = x.PubDate,
                     //    Slug = x.Slug,
                     //    Title = x.Title,
                     //    Comments = x.Comments
                     //    //, Comments = is is possible to sub query here without navigation property
                     //})
                     ;

            var post = await query.AsNoTracking().SingleOrDefaultAsync<PostEntity>().ConfigureAwait(false);

            // will this work? I'm doubtfull
            //post.Categories = EF.Property<string>(post, "CategoryCsv").Split(new char[] { ',' },
            //            StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim().ToLower()).ToList();

            return post;
        }

        public async Task<PostResult> GetPostBySlug(
            string blogId,
            string slug,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var result = new PostResult();

            var query = dbContext.Posts
                     .Include(p => p.PostComments)
                     .Where(p => p.Slug == slug && p.BlogId == blogId)
                     //.Select(x => new Post
                     //{
                     //    // note that this will have to be updated if there are any new properties added to Post
                     //    // maybe better perf to not do this and instead just select post
                     //    // could iterate through the list before returning it to update the categores? or is the shado property only available in the query?
                     //    Id = x.Id,
                     //    //Categories = EF.Property<string>(x, "CategoryCsv").Split(new char[] { ',' },
                     //   //StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim().ToLower()).ToList(), // get from shadow property
                     //    Author = x.Author,
                     //    BlogId = x.BlogId,
                     //    Content = x.Content,
                     //    IsPublished = x.IsPublished,
                     //    LastModified = x.LastModified,
                     //    MetaDescription = x.MetaDescription,
                     //    PubDate = x.PubDate,
                     //    Slug = x.Slug,
                     //    Title = x.Title,
                     //    Comments = x.Comments
                     //    //, Comments = is is possible to sub query here without navigation property
                     //})
                     ;

            var post = await query.AsNoTracking().SingleOrDefaultAsync<PostEntity>().ConfigureAwait(false);

            // will this work? I'm doubtfull
            //post.Categories = EF.Property<string>(post, "CategoryCsv").Split(new char[] { ',' },
            //            StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim().ToLower()).ToList();

            result.Post = await query.AsNoTracking().SingleOrDefaultAsync<PostEntity>().ConfigureAwait(false);

            if (result.Post != null)
            {
                var cutoff = result.Post.PubDate;

                result.PreviousPost = await dbContext.Posts
                    .AsNoTracking()
                    .Where(
                    p => p.PubDate < cutoff
                    && p.IsPublished == true
                    )
                    .OrderByDescending(p => p.PubDate)
                    .Take(1)
                    .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

                result.NextPost = await dbContext.Posts
                    .AsNoTracking()
                    .Where(
                    p => p.PubDate > cutoff
                    && p.IsPublished == true
                    )
                    .OrderBy(p => p.PubDate)
                    .Take(1)
                    .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            }

            return result;
        }

        public async Task<bool> SlugIsAvailable(
            string blogId,
            string slug,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            
            var isInUse = await dbContext.Posts.AnyAsync(
                p => p.Slug == slug && p.BlogId == blogId, 
                cancellationToken
                ).ConfigureAwait(false);

            return !isInUse;
        }


        public async Task<Dictionary<string, int>> GetCategories(
            string blogId,
            bool includeUnpublished,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var result = new Dictionary<string, int>();

            var query = dbContext.PostCategories
                .AsNoTracking()
                .Where(t => t.ProjectId == blogId)
                .Select(x => new
                {
                    cat = x.Value,
                    count = dbContext.PostCategories.Count<PostCategory>(u => u.ProjectId == x.ProjectId && u.Value == x.Value )
                })
                ;

            var list = await query
               .ToListAsync(cancellationToken)
               .ConfigureAwait(false);

            //var grouped = list
            //            .GroupBy(p => new { p.Value, p.ProjectId }) 
            //            .Select(p =>
            //            new { cat = p., count = p.Count() }
            //            )


            foreach (var category in list)
            {
                //var c = category.Cat.TagValue;
                if (!result.ContainsKey(category.cat))
                {
                    result.Add(category.cat, category.count);
                }

                //result[c] = result[c] + 1;
            }


            var sorted = new SortedDictionary<string, int>(result);

            return sorted.OrderBy(x => x.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value) as Dictionary<string, int>;
        }


        public async Task<Dictionary<string, int>> GetArchives(
            string blogId,
            bool includeUnpublished,
            CancellationToken cancellationToken = default(CancellationToken)
            )
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var result = new Dictionary<string, int>();
            
            var query = from p in dbContext.Posts
                          group p by new { month = p.PubDate.Month, year = p.PubDate.Year } into d
                          select new
                          {
                              key = d.Key.year.ToString() + "/" + d.Key.month.ToString("00")
                              ,
                              count = d.Count()
                          };

            var grouped = await query.ToListAsync().ConfigureAwait(false);

            foreach (var item in grouped)
            {
                result.Add(item.key, item.count);
            }

            var sorted = new SortedDictionary<string, int>(result);

            return sorted.OrderByDescending(x => x.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value) as Dictionary<string, int>;

            

            
        }


        #region IDisposable Support

        private void ThrowIfDisposed()
        {
            if (disposedValue)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        private bool disposedValue = false; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }


        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion

    }
}