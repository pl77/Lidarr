/* eslint max-params: 0 */
import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { push } from 'connected-react-router';
import { fetchRootFolders } from 'Store/Actions/rootFolderActions';
import { setAddArtistDefault } from 'Store/Actions/addArtistActions';
import { executeCommand } from 'Store/Actions/commandActions';
import createRouteMatchShape from 'Helpers/Props/Shapes/createRouteMatchShape';
import selectSettings from 'Store/Selectors/selectSettings';
import ImportArtist from './ImportArtist';

function createMapStateToProps() {
  return createSelector(
    (state, { match }) => match,
    (state) => state.rootFolders,
    (state) => state.addArtist,
    (state) => state.importArtist,
    (state) => state.settings.languageProfiles,
    (state) => state.settings.metadataProfiles,
    (
      match,
      rootFolders,
      addArtist,
      importArtistState,
      languageProfiles,
      metadataProfiles
    ) => {
      const {
        isFetching: rootFoldersFetching,
        isPopulated: rootFoldersPopulated,
        error: rootFoldersError,
        items
      } = rootFolders;

      const rootFolderId = parseInt(match.params.rootFolderId);

      const {
        isAdding,
        addError,
        defaults
      } = addArtist;

      const {
        settings,
        validationErrors,
        validationWarnings
      } = selectSettings(defaults, {}, addError);

      const result = {
        rootFolderId,
        rootFoldersFetching,
        rootFoldersPopulated,
        rootFoldersError,
        showLanguageProfile: languageProfiles.items.length > 1,
        showMetadataProfile: metadataProfiles.items.length > 1,
        isAdding,
        validationErrors,
        validationWarnings,
        ...settings
      };

      if (items.length) {
        const rootFolder = _.find(items, { id: rootFolderId });

        return {
          ...result,
          ...rootFolder
        };
      }

      return result;
    }
  );
}

const mapDispatchToProps = {
  push,
  dispatchFetchRootFolders: fetchRootFolders,
  dispatchSetAddArtistDefault: setAddArtistDefault,
  dispatchExecuteCommand: executeCommand
};

class ImportArtistConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    const {
      dispatchFetchRootFolders
    } = this.props;

    if (!this.props.rootFoldersPopulated) {
      dispatchFetchRootFolders();
    }
  }

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.dispatchSetAddArtistDefault({ [name]: value });
  }

  onImportPress = (defaults) => {
    this.props.dispatchExecuteCommand({
      name: 'RescanFolders',
      folders: [this.props.path],
      importArtistDefaults: defaults
    });

    this.props.push(`${window.Lidarr.urlBase}/`);
  }

  //
  // Render

  render() {
    return (
      <ImportArtist
        {...this.props}
        onInputChange={this.onInputChange}
        onImportPress={this.onImportPress}
      />
    );
  }
}

const routeMatchShape = createRouteMatchShape({
  rootFolderId: PropTypes.string.isRequired
});

ImportArtistConnector.propTypes = {
  match: routeMatchShape.isRequired,
  path: PropTypes.string,
  rootFoldersPopulated: PropTypes.bool.isRequired,
  push: PropTypes.func.isRequired,
  dispatchFetchRootFolders: PropTypes.func.isRequired,
  dispatchSetAddArtistDefault: PropTypes.func.isRequired,
  dispatchExecuteCommand: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(ImportArtistConnector);
