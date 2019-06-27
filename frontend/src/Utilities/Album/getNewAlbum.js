import getNewArtist from 'Utilities/Artist/getNewArtist';

function getNewAlbum(album, payload) {
  const {
    searchForNewAlbum = false
  } = payload;

  getNewArtist(album.artist, payload);

  album.addOptions = {
    addType: 'manual',
    searchForNewAlbum
  };
  album.monitored = true;

  return album;
}

export default getNewAlbum;
