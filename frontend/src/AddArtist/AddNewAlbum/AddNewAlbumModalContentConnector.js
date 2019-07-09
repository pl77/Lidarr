import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { setAddAlbumDefault, addAlbum } from 'Store/Actions/addAlbumActions';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import selectSettings from 'Store/Selectors/selectSettings';
import AddNewAlbumModalContent from './AddNewAlbumModalContent';

function createMapStateToProps() {
  return createSelector(
    (state, { isExistingArtist }) => isExistingArtist,
    (state) => state.addAlbum,
    (state) => state.settings.languageProfiles,
    (state) => state.settings.metadataProfiles,
    createDimensionsSelector(),
    (isExistingArtist, addAlbumState, languageProfiles, metadataProfiles, dimensions) => {
      const {
        isAdding,
        addError,
        defaults
      } = addAlbumState;

      const {
        settings,
        validationErrors,
        validationWarnings
      } = selectSettings(defaults, {}, addError);

      return {
        isAdding,
        addError,
        showLanguageProfile: !isExistingArtist && languageProfiles.items.length > 1,
        showMetadataProfile: !isExistingArtist && metadataProfiles.items.length > 1,
        isSmallScreen: dimensions.isSmallScreen,
        validationErrors,
        validationWarnings,
        ...settings
      };
    }
  );
}

const mapDispatchToProps = {
  setAddAlbumDefault,
  addAlbum
};

class AddNewAlbumModalContentConnector extends Component {

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.setAddAlbumDefault({ [name]: value });
  }

  onAddAlbumPress = (searchForNewAlbum) => {
    const {
      foreignAlbumId,
      rootFolderPath,
      monitor,
      qualityProfileId,
      languageProfileId,
      metadataProfileId,
      albumFolder,
      tags
    } = this.props;

    this.props.addAlbum({
      foreignAlbumId,
      rootFolderPath: rootFolderPath.value,
      monitor: monitor.value,
      qualityProfileId: qualityProfileId.value,
      languageProfileId: languageProfileId.value,
      metadataProfileId: metadataProfileId.value,
      albumFolder: albumFolder.value,
      tags: tags.value,
      searchForNewAlbum
    });
  }

  //
  // Render

  render() {
    return (
      <AddNewAlbumModalContent
        {...this.props}
        onInputChange={this.onInputChange}
        onAddAlbumPress={this.onAddAlbumPress}
      />
    );
  }
}

AddNewAlbumModalContentConnector.propTypes = {
  isExistingArtist: PropTypes.bool.isRequired,
  foreignAlbumId: PropTypes.string.isRequired,
  rootFolderPath: PropTypes.object,
  monitor: PropTypes.object.isRequired,
  qualityProfileId: PropTypes.object,
  languageProfileId: PropTypes.object,
  metadataProfileId: PropTypes.object,
  albumFolder: PropTypes.object.isRequired,
  tags: PropTypes.object.isRequired,
  onModalClose: PropTypes.func.isRequired,
  setAddAlbumDefault: PropTypes.func.isRequired,
  addAlbum: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AddNewAlbumModalContentConnector);
