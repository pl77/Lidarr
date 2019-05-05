using System;
using NLog;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class RepackSpecification : IDecisionEngineSpecification
    {
        private readonly Logger _logger;
        private readonly IMediaFileService _mediaFileService;

        public RepackSpecification(IMediaFileService mediaFileService, Logger logger)
        {
            _logger = logger;
            _mediaFileService = mediaFileService;
        }

        public SpecificationPriority Priority => SpecificationPriority.Database;
        public RejectionType Type => RejectionType.Permanent;

        public Decision IsSatisfiedBy(RemoteAlbum subject, SearchCriteriaBase searchCriteria)
        {
            if (!subject.ParsedAlbumInfo.Quality.Revision.IsRepack)
            {
                return Decision.Accept();
            }

            foreach (var album in subject.Albums)
            {
                var releaseGroup = subject.ParsedAlbumInfo.ReleaseGroup;
                var trackFiles = _mediaFileService.GetFilesByAlbum(album.Id);

                foreach (var file in trackFiles)
                {
                    var fileReleaseGroup = file.ReleaseGroup;

                    if (!fileReleaseGroup.Equals(releaseGroup, StringComparison.InvariantCultureIgnoreCase))
                    {
                        _logger.Debug("Release is a repack for a different release group. Release Group: {0}. File release group: {0}", releaseGroup, fileReleaseGroup);
                        return Decision.Reject("Release is a repack for a different release group. Release Group: {0}. File release group: {0}", releaseGroup, fileReleaseGroup);
                    }

                }
            }

            return Decision.Accept();
        }
    }
}
