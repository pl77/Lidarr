using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Common;
using NzbDrone.Core.Housekeeping;
using NzbDrone.Core.Housekeeping.Housekeepers;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.MediaFiles
{
    public interface IMediaFileTableCleanupService
    {
        void Clean(string folder, List<string> filesOnDisk);
    }

    public class MediaFileTableCleanupService : IMediaFileTableCleanupService
    {
        private readonly IMediaFileService _mediaFileService;
        private readonly ITrackService _trackService;
        private readonly Logger _logger;
        private readonly IHousekeepingTask _housekeeper;

        public MediaFileTableCleanupService(IMediaFileService mediaFileService,
                                            ITrackService trackService,
                                            CleanupOrphanedTrackFiles housekeeper,
                                            Logger logger)
        {
            _mediaFileService = mediaFileService;
            _trackService = trackService;
            _housekeeper = housekeeper;
            _logger = logger;
        }

        public void Clean(string folder, List<string> filesOnDisk)
        {
            var files = _mediaFileService.GetFilesWithBasePath(folder);
            var filesOnDiskKeys = new HashSet<string>(filesOnDisk, PathEqualityComparer.Instance);
            
            foreach (var file in files)
            {
                try
                {
                    if (!filesOnDiskKeys.Contains(file.Path))
                    {
                        _logger.Debug("File [{0}] no longer exists on disk, removing from db", file.Path);
                        _mediaFileService.Delete(file, DeleteMediaFileReason.MissingFromDisk);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to cleanup TrackFile in DB: {0}", file.Id);
                }
            }

            _housekeeper.Clean();
        }
    }
}
