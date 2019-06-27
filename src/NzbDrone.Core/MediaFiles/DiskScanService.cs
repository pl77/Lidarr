using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Music;
using NzbDrone.Core.MediaFiles.TrackImport;
using NzbDrone.Common;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Common.Serializer;

namespace NzbDrone.Core.MediaFiles
{
    public interface IDiskScanService
    {
        List<ImportDecision<LocalTrack>> Scan(List<string> folders, FilterFilesType filter = FilterFilesType.Known, ImportArtistDefaults importArtistDefaults = null);
        IFileInfo[] GetAudioFiles(string path, bool allDirectories = true);
        string[] GetNonAudioFiles(string path, bool allDirectories = true);
        List<IFileInfo> FilterFiles(string basePath, IEnumerable<IFileInfo> files);
        List<string> FilterFiles(string basePath, IEnumerable<string> files);
    }


    public class DiskScanService :
        IDiskScanService,
        IExecute<RescanFoldersCommand> 
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IMediaFileService _mediaFileService;
        private readonly IMakeImportDecision _importDecisionMaker;
        private readonly IImportApprovedTracks _importApprovedTracks;
        private readonly IConfigService _configService;
        private readonly IArtistService _artistService;
        private readonly IMediaFileTableCleanupService _mediaFileTableCleanupService;
        private readonly IRootFolderService _rootFolderService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public DiskScanService(IDiskProvider diskProvider,
                               IMediaFileService mediaFileService,
                               IMakeImportDecision importDecisionMaker,
                               IImportApprovedTracks importApprovedTracks,
                               IConfigService configService,
                               IArtistService artistService,
                               IRootFolderService rootFolderService,
                               IMediaFileTableCleanupService mediaFileTableCleanupService,
                               IEventAggregator eventAggregator,
                               Logger logger)
        {
            _diskProvider = diskProvider;
            _mediaFileService = mediaFileService;
            _importDecisionMaker = importDecisionMaker;
            _importApprovedTracks = importApprovedTracks;
            _configService = configService;
            _artistService = artistService;
            _mediaFileTableCleanupService = mediaFileTableCleanupService;
            _rootFolderService = rootFolderService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }
        private static readonly Regex ExcludedSubFoldersRegex = new Regex(@"(?:\\|\/|^)(?:extras|@eadir|extrafanart|plex versions|\.[^\\/]+)(?:\\|\/)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ExcludedFilesRegex = new Regex(@"^\._|^Thumbs\.db$|^\.DS_store$|\.partial~$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public List<ImportDecision<LocalTrack>> Scan(List<string> folders = null,
                                                     FilterFilesType filter = FilterFilesType.Known,
                                                     ImportArtistDefaults importArtistDefaults = null)
        {
            if (folders == null)
            {
                folders = _rootFolderService.All().Select(x => x.Path).ToList();
            }

            var mediaFileList = new List<IFileInfo>();
            var decisions = new List<ImportDecision<LocalTrack>>();

            var musicFilesStopwatch = Stopwatch.StartNew();
            foreach (var folder in folders)
            {
                if (!_diskProvider.FolderExists(folder))
                {
                    _logger.Warn("Specified scan folder ({0}) doesn't exist.", folder);
                    // TODO fix
                    // _eventAggregator.PublishEvent(new ScanSkippedEvent(artist, ArtistScanSkippedReason.RootFolderDoesNotExist));
                    return decisions;
                }

                _logger.ProgressInfo("Scanning {0}", folder);

                // if (!_diskProvider.FolderExists(artist.Path))
                // {
                //     if (_configService.CreateEmptyArtistFolders)
                //     {
                //         _logger.Debug("Creating missing artist folder: {0}", artist.Path);
                //         _diskProvider.CreateFolder(artist.Path);
                //         SetPermissions(artist.Path);
                //     }
                //     else
                //     {
                //         _logger.Debug("Artist folder doesn't exist: {0}", artist.Path);
                //     }

                //     CleanMediaFiles(artist, new List<string>());
                //     CompletedScanning(artist);

                //     return;
                // }


                mediaFileList.AddRange(FilterFiles(folder, GetAudioFiles(folder)));

                CleanMediaFiles(folder, mediaFileList.Select(x => x.FullName).ToList());

            }
            musicFilesStopwatch.Stop();
            _logger.Trace("Finished getting track files for:\n{0} [{1}]", folders.ConcatToString("\n"), musicFilesStopwatch.Elapsed);

            var decisionsStopwatch = Stopwatch.StartNew();
            decisions = _importDecisionMaker.GetImportDecisions(mediaFileList, null, filter, true, importArtistDefaults);
            decisionsStopwatch.Stop();
            _logger.Debug("Import decisions complete [{0}]", decisionsStopwatch.Elapsed);
            
            var importStopwatch = Stopwatch.StartNew();
            _importApprovedTracks.Import(decisions, false, artistDefaults: importArtistDefaults);

            // decisions may have been filtered to just new files.  Anything new and approved will have been inserted.
            // Now we need to make sure anything new but not approved gets inserted
            // Note that knownFiles will include anything imported just now
            var knownFiles = new List<TrackFile>();
            folders.ForEach(x => knownFiles.AddRange(_mediaFileService.GetFilesWithBasePath(x)));
            
            var newFiles = decisions
                .ExceptBy(x => x.Item.Path, knownFiles, x => x.Path, PathEqualityComparer.Instance)
                .Select(decision => new TrackFile {
                        Path = decision.Item.Path,
                        Size = decision.Item.Size,
                        Modified = decision.Item.Modified,
                        DateAdded = DateTime.UtcNow,
                        Quality = decision.Item.Quality,
                        MediaInfo = decision.Item.FileTrackInfo.MediaInfo,
                        Language = decision.Item.Language
                    })
                .ToList();
            _mediaFileService.AddMany(newFiles);
            
            _logger.Debug($"Inserted {newFiles.Count} new unmatched trackfiles");

            // finally update info on size/modified for existing files
            var updatedFiles = knownFiles
                .Join(decisions,
                      x => x.Path,
                      x => x.Item.Path,
                      (file, decision) => new {
                          File = file,
                          Item = decision.Item
                      },
                      PathEqualityComparer.Instance)
                .Where(x => x.File.Size != x.Item.Size ||
                       Math.Abs((x.File.Modified - x.Item.Modified).TotalSeconds) > 1 )
                .Select(x => {
                        x.File.Size = x.Item.Size;
                        x.File.Modified = x.Item.Modified;
                        x.File.MediaInfo = x.Item.FileTrackInfo.MediaInfo;
                        x.File.Quality = x.Item.Quality;
                        return x.File;
                    })
                .ToList();

            _mediaFileService.Update(updatedFiles);
            
            _logger.Debug($"Updated info for {updatedFiles.Count} known files");

            var artists = decisions
                .Where(x => x.Item.Artist != null && x.Item.Artist.Id > 0)
                .GroupBy(x => x.Item.Artist.Id)
                .Select(x => x.First().Item.Artist);
            
            foreach (var artist in artists)
            {
                CompletedScanning(artist);                
            }

            importStopwatch.Stop();
            _logger.Debug("Track import complete for:\n{0} [{1}]", folders.ConcatToString("\n"), importStopwatch.Elapsed);
            
            return decisions;
        }
    
        private void CleanMediaFiles(string folder, List<string> mediaFileList)
        {
            _logger.Debug($"Cleaning up media files in DB [{folder}]");
            _mediaFileTableCleanupService.Clean(folder, mediaFileList);
        }

        private void CompletedScanning(Artist artist)
        {
            _logger.Info("Completed scanning disk for {0}", artist.Name);
            _eventAggregator.PublishEvent(new ArtistScannedEvent(artist));
        }

        public IFileInfo[] GetAudioFiles(string path, bool allDirectories = true)
        {
            _logger.Debug("Scanning '{0}' for music files", path);

            var searchOption = allDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var filesOnDisk = _diskProvider.GetFileInfos(path, searchOption);

            var mediaFileList = filesOnDisk.Where(file => MediaFileExtensions.Extensions.Contains(file.Extension))
                                           .ToList();

            _logger.Trace("{0} files were found in {1}", filesOnDisk.Count, path);
            _logger.Debug("{0} audio files were found in {1}", mediaFileList.Count, path);

            return mediaFileList.ToArray();
        }

        public string[] GetNonAudioFiles(string path, bool allDirectories = true)
        {
            _logger.Debug("Scanning '{0}' for non-music files", path);

            var searchOption = allDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var filesOnDisk = _diskProvider.GetFiles(path, searchOption).ToList();

            var mediaFileList = filesOnDisk.Where(file => !MediaFileExtensions.Extensions.Contains(Path.GetExtension(file)))
                                           .ToList();

            _logger.Trace("{0} files were found in {1}", filesOnDisk.Count, path);
            _logger.Debug("{0} non-music files were found in {1}", mediaFileList.Count, path);

            return mediaFileList.ToArray();
        }

        public List<string> FilterFiles(string basePath, IEnumerable<string> files)
        {
            return files.Where(file => !ExcludedSubFoldersRegex.IsMatch(basePath.GetRelativePath(file)))
                        .Where(file => !ExcludedFilesRegex.IsMatch(Path.GetFileName(file)))
                        .ToList();
        }

        public List<IFileInfo> FilterFiles(string basePath, IEnumerable<IFileInfo> files)
        {
            return files.Where(file => !ExcludedSubFoldersRegex.IsMatch(basePath.GetRelativePath(file.FullName)))
                        .Where(file => !ExcludedFilesRegex.IsMatch(file.Name))
                        .ToList();
        }

        private void SetPermissions(string path)
        {
            if (!_configService.SetPermissionsLinux)
            {
                return;
            }

            try
            {
                var permissions = _configService.FolderChmod;
                _diskProvider.SetPermissions(path, permissions, _configService.ChownUser, _configService.ChownGroup);
            }

            catch (Exception ex)
            {

                _logger.Warn(ex, "Unable to apply permissions to: " + path);
                _logger.Debug(ex, ex.Message);
            }
        }

        private void RemoveEmptyArtistFolder(string path)
        {
            if (_configService.DeleteEmptyFolders)
            {
                if (_diskProvider.GetFiles(path, SearchOption.AllDirectories).Empty())
                {
                    _diskProvider.DeleteFolder(path, true);
                }
                else
                {
                    _diskProvider.RemoveEmptySubfolders(path);
                }
            }
        }

        public void Execute(RescanFoldersCommand message)
        {
            var filter = message.ImportArtistDefaults != null ? FilterFilesType.Matched : FilterFilesType.Known;
            _logger.Debug(message.ImportArtistDefaults.ToJson());

            Scan(message.Folders, filter, message.ImportArtistDefaults);
        }
    }
}
