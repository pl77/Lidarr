using System.Collections.Generic;
using NzbDrone.Core.MediaFiles.TrackImport;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.MediaFiles.Commands
{
    public class RescanFoldersCommand : Command
    {
        public List<string> Folders { get; set; }
        public ImportArtistDefaults ImportArtistDefaults { get; set; }

        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;

        public RescanFoldersCommand()
        {
        }

        public RescanFoldersCommand(List<string> folders)
        {
            Folders = folders;
        }
    }
}
