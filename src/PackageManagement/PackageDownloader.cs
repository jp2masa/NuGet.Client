﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Abstracts the logic to get a package stream for a given package identity from a given source repository
    /// </summary>
    public static class PackageDownloader
    {
        /// <summary>
        /// Returns the <see cref="DownloadResourceResult"/> for a given <paramref name="packageIdentity" />
        /// from the given <paramref name="sources" />.
        /// </summary>
        public static async Task<DownloadResourceResult> GetDownloadResourceResultAsync(IEnumerable<SourceRepository> sources,
            PackageIdentity packageIdentity,
            CancellationToken token)
        {
            using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                var tasks = new List<Task<DownloadResourceResult>>(sources
                    .Select(s => GetDownloadResourceResultAsync(s, packageIdentity, linkedTokenSource.Token)));

                while (tasks.Any())
                {
                    var completedTask = await Task.WhenAny(tasks);

                    if (completedTask.Status == TaskStatus.RanToCompletion)
                    {
                        // Cancel the other tasks, since, they may still be running
                        linkedTokenSource.Cancel();

                        return completedTask.Result;
                    }
                    else
                    {
                        token.ThrowIfCancellationRequested();

                        // In this case, completedTask did not run to completion.
                        // That is, it faulted or got canceled. Remove it, and try Task.WhenAny again
                        tasks.Remove(completedTask);
                    }
                }
            }

            // no matches were found
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                Strings.UnknownPackageSpecificVersion,
                packageIdentity.Id,
                packageIdentity.Version.ToNormalizedString()));
        }

        /// <summary>
        /// Returns the <see cref="DownloadResourceResult"/> for a given <paramref name="packageIdentity" /> from the given
        /// <paramref name="sourceRepository" />.
        /// </summary>
        public static async Task<DownloadResourceResult> GetDownloadResourceResultAsync(SourceRepository sourceRepository, PackageIdentity packageIdentity, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>(token);

            if (downloadResource == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.DownloadResourceNotFound, sourceRepository.PackageSource.Source));
            }

            var downloadResourceResult = await downloadResource.GetDownloadResourceResultAsync(packageIdentity, token);
            if (downloadResourceResult == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.DownloadStreamNotAvailable, packageIdentity, sourceRepository.PackageSource.Source));
            }

            return new DownloadResourceResult(
                await GetSeekableStream(downloadResourceResult.PackageStream, token),
                downloadResourceResult.PackageReader);
        }

        private static async Task<Stream> GetSeekableStream(Stream downloadStream, CancellationToken token)
        {
            if (!downloadStream.CanSeek)
            {
                var memoryStream = new MemoryStream();
                try
                {
                    token.ThrowIfCancellationRequested();
                    await downloadStream.CopyToAsync(memoryStream);
                }
                catch
                {
                    memoryStream.Dispose();
                    throw;
                }
                finally
                {
                    downloadStream.Dispose();
                }

                memoryStream.Position = 0;
                return memoryStream;
            }

            downloadStream.Position = 0;
            return downloadStream;
        }
    }
}
