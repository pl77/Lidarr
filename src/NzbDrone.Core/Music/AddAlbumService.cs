using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Music
{
    public interface IAddAlbumService
    {
        Album AddAlbum(Album newAlbum);
    }

    public class AddAlbumService : IAddAlbumService
    {
        private readonly IArtistService _artistService;
        private readonly IAddArtistService _addArtistService;
        private readonly IAlbumService _albumService;
        private readonly IRefreshAlbumService _refreshAlbumService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public AddAlbumService(IArtistService artistService,
                               IAddArtistService addArtistService,
                               IAlbumService albumService,
                               IRefreshAlbumService refreshAlbumService,
                               IManageCommandQueue commandQueueManager,
                               Logger logger)
        {
            _artistService = artistService;
            _addArtistService = addArtistService;
            _albumService = albumService;
            _refreshAlbumService = refreshAlbumService;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public Album AddAlbum(Album album)
        {
            _logger.Debug($"Adding album {album}");

            // Add the artist if necessary
            var dbArtist = _artistService.FindById(album.ArtistMetadata.Value.ForeignArtistId);
            if (dbArtist == null)
            {
                _addArtistService.AddArtist(album.Artist.Value);
            }

            // Add the album
            album = SetPropertiesAndValidate(album);

            _albumService.AddAlbum(album);
            _refreshAlbumService.RefreshAlbumInfo(album, new List<Album> { album }, false);
            
            // get the added album
            var dbAlbum = _albumService.FindById(album.ForeignAlbumId);
            
            // search if required
            if (dbAlbum.AddOptions.SearchForNewAlbum)
            {
                _commandQueueManager.Push(new AlbumSearchCommand(new List<int> { dbAlbum.Id }));
                dbAlbum.AddOptions.SearchForNewAlbum = false;
                _albumService.UpdateMany(new List<Album> { dbAlbum });
            }

            return dbAlbum;
        }

        private Album SetPropertiesAndValidate(Album newAlbum)
        {
            newAlbum.Images = newAlbum.Images ?? new List<MediaCover.MediaCover>();
            newAlbum.Genres = newAlbum.Genres ?? new List<string>();
            newAlbum.Links = newAlbum.Links ?? new List<Links>();
            newAlbum.Ratings = newAlbum.Ratings ?? new Ratings();
            newAlbum.Artist = newAlbum.Artist ?? new Artist();
            newAlbum.OldForeignAlbumIds = newAlbum.OldForeignAlbumIds ?? new List<string>();
            newAlbum.ArtistMetadata = newAlbum.ArtistMetadata ?? new ArtistMetadata();

            newAlbum.CleanTitle = Parser.Parser.CleanArtistName(newAlbum.Title);
            newAlbum.Added = DateTime.UtcNow;

            return newAlbum;
        }
    }
}
