using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.TorrentInfo;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;
using NzbDrone.Core.Validation;
using NzbDrone.Common.Cache;

namespace NzbDrone.Core.Download.Clients.QBittorrent
{
    public class QBittorrent : TorrentClientBase<QBittorrentSettings>
    {
        private readonly IQBittorrentProxySelector _proxySelector;
        private readonly ICached<SeedingTimeCacheEntry> _seedingTimeCache;

        private class SeedingTimeCacheEntry
        {
            public DateTime LastFetched { get; set; }
            public long SeedingTime { get; set; }
        }

        public QBittorrent(IQBittorrentProxySelector proxySelector,
                           ITorrentFileInfoReader torrentFileInfoReader,
                           IHttpClient httpClient,
                           IConfigService configService,
                           IDiskProvider diskProvider,
                           IRemotePathMappingService remotePathMappingService,
                           ICacheManager cacheManager,
                           Logger logger)
            : base(torrentFileInfoReader, httpClient, configService, diskProvider, remotePathMappingService, logger)
        {
            _proxySelector = proxySelector;

            _seedingTimeCache = cacheManager.GetCache<SeedingTimeCacheEntry>(GetType(), "seedingTime");
        }

        private IQBittorrentProxy Proxy => _proxySelector.GetProxy(Settings);

        public override void MarkItemAsImported(DownloadClientItem downloadClientItem)
        {
            // set post-import category
            if (Settings.MusicImportedCategory.IsNotNullOrWhiteSpace() &&
                Settings.MusicImportedCategory != Settings.MusicCategory)
            {
                try
                {
                    Proxy.SetTorrentLabel(downloadClientItem.DownloadId.ToLower(), Settings.MusicImportedCategory, Settings);
                }
                catch (DownloadClientException)
                {
                    _logger.Warn("Failed to set post-import torrent label \"{0}\" for {1} in qBittorrent. Does the label exist?",
                        Settings.MusicImportedCategory, downloadClientItem.Title);
                }
            }
        }

        protected override string AddFromMagnetLink(RemoteAlbum remoteAlbum, string hash, string magnetLink)
        {
            if (!Proxy.GetConfig(Settings).DhtEnabled && !magnetLink.Contains("&tr="))
            {
                throw new NotSupportedException("Magnet Links without trackers not supported if DHT is disabled");
            }

            Proxy.AddTorrentFromUrl(magnetLink, Settings);

            var isRecentAlbum = remoteAlbum.IsRecentAlbum();

            if (isRecentAlbum && Settings.RecentTvPriority == (int)QBittorrentPriority.First ||
                !isRecentAlbum && Settings.OlderTvPriority == (int)QBittorrentPriority.First)
            {
                Proxy.MoveTorrentToTopInQueue(hash.ToLower(), Settings);
            }

            SetInitialState(hash.ToLower());

            if (remoteAlbum.SeedConfiguration != null && (remoteAlbum.SeedConfiguration.Ratio.HasValue || remoteAlbum.SeedConfiguration.SeedTime.HasValue))
            {
                Proxy.SetTorrentSeedingConfiguration(hash.ToLower(), remoteAlbum.SeedConfiguration, Settings);
            }

            return hash;
        }

        protected override string AddFromTorrentFile(RemoteAlbum remoteAlbum, string hash, string filename, Byte[] fileContent)
        {
            Proxy.AddTorrentFromFile(filename, fileContent, Settings);

            try
            {
                var isRecentAlbum = remoteAlbum.IsRecentAlbum();

                if (isRecentAlbum && Settings.RecentTvPriority == (int)QBittorrentPriority.First ||
                 !isRecentAlbum && Settings.OlderTvPriority == (int)QBittorrentPriority.First)
                {
                    Proxy.MoveTorrentToTopInQueue(hash.ToLower(), Settings);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to set the torrent priority for {0}.", filename);
            }

            SetInitialState(hash.ToLower());

            if (remoteAlbum.SeedConfiguration != null && (remoteAlbum.SeedConfiguration.Ratio.HasValue || remoteAlbum.SeedConfiguration.SeedTime.HasValue))
            {
                Proxy.SetTorrentSeedingConfiguration(hash.ToLower(), remoteAlbum.SeedConfiguration, Settings);
            }

            return hash;
        }

        public override string Name => "qBittorrent";

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            var config = Proxy.GetConfig(Settings);
            var torrents = Proxy.GetTorrents(Settings);

            var queueItems = new List<DownloadClientItem>();

            foreach (var torrent in torrents)
            {
                var item = new DownloadClientItem
                {
                    DownloadId = torrent.Hash.ToUpper(),
                    Category = torrent.Category.IsNotNullOrWhiteSpace() ? torrent.Category : torrent.Label,
                    Title = torrent.Name,
                    TotalSize = torrent.Size,
                    DownloadClient = Definition.Name,
                    RemainingSize = (long)(torrent.Size * (1.0 - torrent.Progress)),
                    RemainingTime = GetRemainingTime(torrent),
                    SeedRatio = torrent.Ratio,
                    OutputPath = _remotePathMappingService.RemapRemoteToLocal(Settings.Host, new OsPath(torrent.SavePath)),
                };

                // Avoid removing torrents that haven't reached the global max ratio.
                // Removal also requires the torrent to be paused, in case a higher max ratio was set on the torrent itself (which is not exposed by the api).
                item.CanMoveFiles = item.CanBeRemoved = (torrent.State == "pausedUP" && HasReachedSeedLimit(torrent, config));

                if (!item.OutputPath.IsEmpty && item.OutputPath.FileName != torrent.Name)
                {
                    item.OutputPath += torrent.Name;
                }

                switch (torrent.State)
                {
                    case "error": // some error occurred, applies to paused torrents
                        item.Status = DownloadItemStatus.Failed;
                        item.Message = "qBittorrent is reporting an error";
                        break;

                    case "pausedDL": // torrent is paused and has NOT finished downloading
                        item.Status = DownloadItemStatus.Paused;
                        break;

                    case "queuedDL": // queuing is enabled and torrent is queued for download
                    case "checkingDL": // same as checkingUP, but torrent has NOT finished downloading
                        item.Status = DownloadItemStatus.Queued;
                        break;

                    case "pausedUP": // torrent is paused and has finished downloading
                    case "uploading": // torrent is being seeded and data is being transfered
                    case "stalledUP": // torrent is being seeded, but no connection were made
                    case "queuedUP": // queuing is enabled and torrent is queued for upload
                    case "checkingUP": // torrent has finished downloading and is being checked
                    case "forcedUP": // torrent has finished downloading and is being forcibly seeded
                        item.Status = DownloadItemStatus.Completed;
                        item.RemainingTime = TimeSpan.Zero; // qBittorrent sends eta=8640000 for completed torrents
                        break;

                    case "stalledDL": // torrent is being downloaded, but no connection were made
                        item.Status = DownloadItemStatus.Warning;
                        item.Message = "The download is stalled with no connections";
                        break;

                    case "metaDL": // torrent magnet is being downloaded
                        if (config.DhtEnabled)
                        {
                            item.Status = DownloadItemStatus.Queued;
                        }
                        else
                        {
                            item.Status = DownloadItemStatus.Warning;
                            item.Message = "qBittorrent cannot resolve magnet link with DHT disabled";
                        }
                        break;

                    case "downloading": // torrent is being downloaded and data is being transfered
                    default: // new status in API? default to downloading
                        item.Status = DownloadItemStatus.Downloading;
                        break;
                }

                queueItems.Add(item);
            }

            return queueItems;
        }

        public override void RemoveItem(string hash, bool deleteData)
        {
            Proxy.RemoveTorrent(hash.ToLower(), deleteData, Settings);
        }

        public override DownloadClientInfo GetStatus()
        {
            var config = Proxy.GetConfig(Settings);

            var destDir = new OsPath(config.SavePath);

            return new DownloadClientInfo
            {
                IsLocalhost = Settings.Host == "127.0.0.1" || Settings.Host == "localhost",
                OutputRootFolders = new List<OsPath> { _remotePathMappingService.RemapRemoteToLocal(Settings.Host, destDir) }
            };
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            failures.AddIfNotNull(TestConnection());
            if (failures.HasErrors()) return;
            failures.AddIfNotNull(TestCategory());
            failures.AddIfNotNull(TestPrioritySupport());
            failures.AddIfNotNull(TestGetTorrents());
        }

        private ValidationFailure TestConnection()
        {
            try
            {
                var version = _proxySelector.GetProxy(Settings, true).GetApiVersion(Settings);
                if (version < Version.Parse("1.5"))
                {
                    // API version 5 introduced the "save_path" property in /query/torrents
                    return new NzbDroneValidationFailure("Host", "Unsupported client version")
                    {
                        DetailedDescription = "Please upgrade to qBittorrent version 3.2.4 or higher."
                    };
                }
                else if (version < Version.Parse("1.6"))
                {
                    // API version 6 introduced support for labels
                    if (Settings.MusicCategory.IsNotNullOrWhiteSpace())
                    {
                        return new NzbDroneValidationFailure("Category", "Category is not supported")
                        {
                            DetailedDescription = "Labels are not supported until qBittorrent version 3.3.0. Please upgrade or try again with an empty Category."
                        };
                    }
                }
                else if (Settings.MusicCategory.IsNullOrWhiteSpace())
                {
                    // warn if labels are supported, but category is not provided
                    return new NzbDroneValidationFailure("MusicCategory", "Category is recommended")
                    {
                        IsWarning = true,
                        DetailedDescription = "Lidarr will not attempt to import completed downloads without a category."
                    };
                }

                // Complain if qBittorrent is configured to remove torrents on max ratio
                var config = Proxy.GetConfig(Settings);
                if ((config.MaxRatioEnabled || config.MaxSeedingTimeEnabled) && config.RemoveOnMaxRatio)
                {
                    return new NzbDroneValidationFailure(String.Empty, "qBittorrent is configured to remove torrents when they reach their Share Ratio Limit")
                    {
                        DetailedDescription = "Lidarr will be unable to perform Completed Download Handling as configured. You can fix this in qBittorrent ('Tools -> Options...' in the menu) by changing 'Options -> BitTorrent -> Share Ratio Limiting' from 'Remove them' to 'Pause them'."
                    };
                }
            }
            catch (DownloadClientAuthenticationException ex)
            {
                _logger.Error(ex, "Unable to authenticate");
                return new NzbDroneValidationFailure("Username", "Authentication failure")
                {
                    DetailedDescription = "Please verify your username and password."
                };
            }
            catch (WebException ex)
            {
                _logger.Error(ex, "Unable to connect to qBittorrent");
                if (ex.Status == WebExceptionStatus.ConnectFailure)
                {
                    return new NzbDroneValidationFailure("Host", "Unable to connect")
                    {
                        DetailedDescription = "Please verify the hostname and port."
                    };
                }
                return new NzbDroneValidationFailure(String.Empty, "Unknown exception: " + ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to test qBittorrent");
                return new NzbDroneValidationFailure(String.Empty, "Unknown exception: " + ex.Message);
            }

            return null;
        }

        private ValidationFailure TestCategory()
        {
            if (Settings.MusicCategory.IsNullOrWhiteSpace() && Settings.MusicImportedCategory.IsNullOrWhiteSpace())
            {
                return null;
            }

            // api v1 doesn't need to check/add categories as it's done on set
            var version = _proxySelector.GetProxy(Settings, true).GetApiVersion(Settings);
            if (version < Version.Parse("2.0"))
            {
                return null;
            }

            Dictionary<string, QBittorrentLabel> labels = Proxy.GetLabels(Settings);

            if (Settings.MusicCategory.IsNotNullOrWhiteSpace() && !labels.ContainsKey(Settings.MusicCategory))
            {
                Proxy.AddLabel(Settings.MusicCategory, Settings);
                labels = Proxy.GetLabels(Settings);

                if (!labels.ContainsKey(Settings.MusicCategory))
                {
                    return new NzbDroneValidationFailure("MusicCategory", "Configuration of label failed")
                    {
                        DetailedDescription = "Lidarr was unable to add the label to qBittorrent."
                    };
                }
            }

            if (Settings.MusicImportedCategory.IsNotNullOrWhiteSpace() && !labels.ContainsKey(Settings.MusicImportedCategory))
            {
                Proxy.AddLabel(Settings.MusicImportedCategory, Settings);
                labels = Proxy.GetLabels(Settings);

                if (!labels.ContainsKey(Settings.MusicImportedCategory))
                {
                    return new NzbDroneValidationFailure("MusicImportedCategory", "Configuration of label failed")
                    {
                        DetailedDescription = "Lidarr was unable to add the label to qBittorrent."
                    };
                }
            }

            return null;
        }

        private ValidationFailure TestPrioritySupport()
        {
            var recentPriorityDefault = Settings.RecentTvPriority == (int)QBittorrentPriority.Last;
            var olderPriorityDefault = Settings.OlderTvPriority == (int)QBittorrentPriority.Last;

            if (olderPriorityDefault && recentPriorityDefault)
            {
                return null;
            }

            try
            {
                var config = Proxy.GetConfig(Settings);

                if (!config.QueueingEnabled)
                {
                    if (!recentPriorityDefault)
                    {
                        return new NzbDroneValidationFailure(nameof(Settings.RecentTvPriority), "Queueing not enabled") { DetailedDescription = "Torrent Queueing is not enabled in your qBittorrent settings. Enable it in qBittorrent or select 'Last' as priority." };
                    }
                    else if (!olderPriorityDefault)
                    {
                        return new NzbDroneValidationFailure(nameof(Settings.OlderTvPriority), "Queueing not enabled") { DetailedDescription = "Torrent Queueing is not enabled in your qBittorrent settings. Enable it in qBittorrent or select 'Last' as priority." };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to test qBittorrent");
                return new NzbDroneValidationFailure(String.Empty, "Unknown exception: " + ex.Message);
            }

            return null;
        }

        private ValidationFailure TestGetTorrents()
        {
            try
            {
                Proxy.GetTorrents(Settings);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get torrents");
                return new NzbDroneValidationFailure(String.Empty, "Failed to get the list of torrents: " + ex.Message);
            }

            return null;
        }

        private void SetInitialState(string hash)
        {
            try
            {
                switch ((QBittorrentState)Settings.InitialState)
                {
                    case QBittorrentState.ForceStart:
                        Proxy.SetForceStart(hash, true, Settings);
                        break;
                    case QBittorrentState.Start:
                        Proxy.ResumeTorrent(hash, Settings);
                        break;
                    case QBittorrentState.Pause:
                        Proxy.PauseTorrent(hash, Settings);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to set inital state for {0}.", hash);
            }
        }

        protected TimeSpan? GetRemainingTime(QBittorrentTorrent torrent)
        {
            if (torrent.Eta < 0 || torrent.Eta > 365 * 24 * 3600)
            {
                return null;
            }

            // qBittorrent sends eta=8640000 if unknown such as queued
            if (torrent.Eta == 8640000)
            {
                return null;
            }

            return TimeSpan.FromSeconds((int)torrent.Eta);
        }

        protected bool HasReachedSeedLimit(QBittorrentTorrent torrent, QBittorrentPreferences config)
        {
            if (torrent.RatioLimit >= 0)
            {
                if (torrent.Ratio >= torrent.RatioLimit)
                {
                    return true;
                }
            }
            else if (torrent.RatioLimit == -2 && config.MaxRatioEnabled)
            {
                if (torrent.Ratio >= config.MaxRatio)
                {
                    return true;
                }
            }

            if (HasReachedSeedingTimeLimit(torrent, config)) return true;


            return false;
        }

        protected bool HasReachedSeedingTimeLimit(QBittorrentTorrent torrent, QBittorrentPreferences config)
        {
            long seedingTimeLimit;

            if (torrent.SeedingTimeLimit >= 0)
            {
                seedingTimeLimit = torrent.SeedingTimeLimit;
            }
            else if (torrent.SeedingTimeLimit == -2 && config.MaxSeedingTimeEnabled)
            {
                seedingTimeLimit = config.MaxSeedingTime;
            }
            else
            {
                return false;
            }

            if (torrent.SeedingTime.HasValue)
            {
                // SeedingTime can't be available here, but use it if the api starts to provide it.
                return torrent.SeedingTime.Value >= seedingTimeLimit;
            }

            var cacheKey = Settings.Host + Settings.Port + torrent.Hash;
            var cacheSeedingTime = _seedingTimeCache.Find(cacheKey);

            if (cacheSeedingTime != null)
            {
                var togo = seedingTimeLimit - cacheSeedingTime.SeedingTime;
                var elapsed = (DateTime.UtcNow - cacheSeedingTime.LastFetched).TotalSeconds;

                if (togo <= 0)
                {
                    // Already reached the limit, keep the cache alive
                    _seedingTimeCache.Set(cacheKey, cacheSeedingTime, TimeSpan.FromMinutes(5));
                    return true;
                }
                else if (togo > elapsed)
                {
                    // SeedingTime cannot have reached the required value since the last check, preserve the cache
                    _seedingTimeCache.Set(cacheKey, cacheSeedingTime, TimeSpan.FromMinutes(5));
                    return false;
                }
            }

            FetchTorrentDetails(torrent);

            cacheSeedingTime = new SeedingTimeCacheEntry
            {
                LastFetched = DateTime.UtcNow,
                SeedingTime = torrent.SeedingTime.Value
            };

            _seedingTimeCache.Set(cacheKey, cacheSeedingTime, TimeSpan.FromMinutes(5));

            if (cacheSeedingTime.SeedingTime >= seedingTimeLimit)
            {
                // Reached the limit, keep the cache alive
                return true;
            }

            return false;
        }

        protected void FetchTorrentDetails(QBittorrentTorrent torrent)
        {
            var torrentProperties = Proxy.GetTorrentProperties(torrent.Hash, Settings);

            torrent.SeedingTime = torrentProperties.SeedingTime;
        }
    }
}
