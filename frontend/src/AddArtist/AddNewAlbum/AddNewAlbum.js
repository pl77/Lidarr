import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { icons } from 'Helpers/Props';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import Icon from 'Components/Icon';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import TextInput from 'Components/Form/TextInput';
import PageContent from 'Components/Page/PageContent';
import PageContentBodyConnector from 'Components/Page/PageContentBodyConnector';
import AddNewAlbumSearchResultConnector from './AddNewAlbumSearchResultConnector';
import styles from './AddNewAlbum.css';

class AddNewAlbum extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      term: props.term || '',
      isFetching: false
    };
  }

  componentDidMount() {
    const term = this.state.term;

    if (term) {
      this.props.onAlbumLookupChange(term);
    }
  }

  componentDidUpdate(prevProps) {
    const {
      term,
      isFetching
    } = this.props;

    if (term && term !== prevProps.term) {
      this.setState({
        term,
        isFetching: true
      });
      this.props.onAlbumLookupChange(term);
    } else if (isFetching !== prevProps.isFetching) {
      this.setState({
        isFetching
      });
    }
  }

  //
  // Listeners

  onSearchInputChange = ({ value }) => {
    const hasValue = !!value.trim();

    this.setState({ term: value, isFetching: hasValue }, () => {
      if (hasValue) {
        this.props.onAlbumLookupChange(value);
      } else {
        this.props.onClearAlbumLookup();
      }
    });
  }

  onClearAlbumLookupPress = () => {
    this.setState({ term: '' });
    this.props.onClearAlbumLookup();
  }

  //
  // Render

  render() {
    const {
      error,
      items
    } = this.props;

    const term = this.state.term;
    const isFetching = this.state.isFetching;

    return (
      <PageContent title="Add New Album">
        <PageContentBodyConnector>
          <div className={styles.searchContainer}>
            <div className={styles.searchIconContainer}>
              <Icon
                name={icons.SEARCH}
                size={20}
              />
            </div>

            <TextInput
              className={styles.searchInput}
              name="albumLookup"
              value={term}
              placeholder="eg. We Are Not Alone, lidarr:9a03d313-0580-3f71-ae10-0e0996db3faa"
              autoFocus={true}
              onChange={this.onSearchInputChange}
            />

            <Button
              className={styles.clearLookupButton}
              onPress={this.onClearAlbumLookupPress}
            >
              <Icon
                name={icons.REMOVE}
                size={20}
              />
            </Button>
          </div>

          {
            isFetching &&
              <LoadingIndicator />
          }

          {
            !isFetching && !!error &&
              <div>Failed to load search results, please try again.</div>
          }

          {
            !isFetching && !error && !!items.length &&
              <div className={styles.searchResults}>
                {
                  items.map((item) => {
                    return (
                      <AddNewAlbumSearchResultConnector
                        isExistingAlbum={'id' in item && item.id !== 0}
                        isExistingArtist={'id' in item.artist && item.artist.id !== 0}
                        key={item.foreignAlbumId}
                        {...item}
                      />
                    );
                  })
                }
              </div>
          }

          {
            !isFetching && !error && !items.length && !!term &&
              <div className={styles.message}>
                <div className={styles.noResults}>Couldn't find any results for '{term}'</div>
                <div>You can also search using <Link to="https://musicbrainz.org/search?type=release_group">MusicBrainz ID</Link> of a release group e.g. lidarr:9a03d313-0580-3f71-ae10-0e0996db3faa</div>
              </div>
          }

          {
            !term &&
              <div className={styles.message}>
                <div className={styles.helpText}>It's easy to add a new album, just start typing the name of the album you want to add.</div>
                <div>You can also search using <Link to="https://musicbrainz.org/search?type=release_group">MusicBrainz ID</Link> of a release group e.g. lidarr:9a03d313-0580-3f71-ae10-0e0996db3faa</div>
              </div>
          }

          <div />
        </PageContentBodyConnector>
      </PageContent>
    );
  }
}

AddNewAlbum.propTypes = {
  term: PropTypes.string,
  isFetching: PropTypes.bool.isRequired,
  error: PropTypes.object,
  isAdding: PropTypes.bool.isRequired,
  addError: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  onAlbumLookupChange: PropTypes.func.isRequired,
  onClearAlbumLookup: PropTypes.func.isRequired
};

export default AddNewAlbum;
