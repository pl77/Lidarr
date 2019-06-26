using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Commands;
using NzbDrone.Test.Common;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class RefreshArtistServiceFixture : CoreTest<RefreshArtistService>
    {
        private Artist _artist;
        private Album _album1;
        private Album _album2;
        private List<Album> _albums;

        [SetUp]
        public void Setup()
        {
            _album1 = Builder<Album>.CreateNew()
                .With(s => s.ForeignAlbumId = "1")
                .Build();

            _album2 = Builder<Album>.CreateNew()
                .With(s => s.ForeignAlbumId = "2")
                .Build();

            _albums = new List<Album> {_album1, _album2};

            var metadata = Builder<ArtistMetadata>.CreateNew().Build();

            _artist = Builder<Artist>.CreateNew()
                .With(a => a.Metadata = metadata)
                .Build();

            Mocker.GetMock<IArtistService>()
                  .Setup(s => s.GetArtist(_artist.Id))
                  .Returns(_artist);

            Mocker.GetMock<IAlbumService>()
                .Setup(s => s.GetAlbumsForRefresh(It.IsAny<int>(), It.IsAny<IEnumerable<string>>()))
                .Returns(new List<Album>());

            Mocker.GetMock<IProvideArtistInfo>()
                  .Setup(s => s.GetArtistInfo(It.IsAny<string>(), It.IsAny<int>()))
                  .Callback(() => { throw new ArtistNotFoundException(_artist.ForeignArtistId); });

            Mocker.GetMock<IMediaFileService>()
                .Setup(x => x.GetFilesByArtist(It.IsAny<int>()))
                .Returns(new List<TrackFile>());
        }

        private void GivenNewArtistInfo(Artist artist)
        {
            Mocker.GetMock<IProvideArtistInfo>()
                  .Setup(s => s.GetArtistInfo(_artist.ForeignArtistId, _artist.MetadataProfileId))
                  .Returns(artist);
        }
        
        private void GivenArtistFiles()
        {
            Mocker.GetMock<IMediaFileService>()
                  .Setup(x => x.GetFilesByArtist(It.IsAny<int>()))
                  .Returns(Builder<TrackFile>.CreateListOfSize(1).BuildList());
        }

        [Test]
        public void should_log_error_and_delete_if_musicbrainz_id_not_found_and_artist_has_no_files()
        {
            Subject.Execute(new RefreshArtistCommand(_artist.Id));

            Mocker.GetMock<IArtistService>()
                .Verify(v => v.UpdateArtist(It.IsAny<Artist>()), Times.Never());
            
            Mocker.GetMock<IArtistService>()
                .Verify(v => v.DeleteArtist(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once());

            ExceptionVerification.ExpectedErrors(1);
            ExceptionVerification.ExpectedWarns(1);
        }
        
        [Test]
        public void should_log_error_but_not_delete_if_musicbrainz_id_not_found_and_artist_has_files()
        {
            GivenArtistFiles();
            
            Subject.Execute(new RefreshArtistCommand(_artist.Id));

            Mocker.GetMock<IArtistService>()
                .Verify(v => v.UpdateArtist(It.IsAny<Artist>()), Times.Never());
            
            Mocker.GetMock<IArtistService>()
                .Verify(v => v.DeleteArtist(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());

            ExceptionVerification.ExpectedErrors(2);
        }

        [Test]
        public void should_update_if_musicbrainz_id_changed_and_no_clash()
        {
            var newArtistInfo = _artist.JsonClone();
            newArtistInfo.Metadata = _artist.Metadata.Value.JsonClone();
            newArtistInfo.Albums = _albums;
            newArtistInfo.ForeignArtistId = _artist.ForeignArtistId + 1;
            newArtistInfo.Metadata.Value.Id = 100;

            GivenNewArtistInfo(newArtistInfo);

            Subject.Execute(new RefreshArtistCommand(_artist.Id));

            Mocker.GetMock<IArtistService>()
                .Verify(v => v.UpdateArtist(It.Is<Artist>(s => s.ArtistMetadataId == 100)));
        }
    }
}
