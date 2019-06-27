import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { icons, kinds, inputTypes, tooltipPositions } from 'Helpers/Props';
import Icon from 'Components/Icon';
import SpinnerButton from 'Components/Link/SpinnerButton';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormLabel from 'Components/Form/FormLabel';
import FormInputGroup from 'Components/Form/FormInputGroup';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Popover from 'Components/Tooltip/Popover';
import ArtistMonitoringOptionsPopoverContent from 'AddArtist/ArtistMonitoringOptionsPopoverContent';
import PageContent from 'Components/Page/PageContent';
import PageContentBodyConnector from 'Components/Page/PageContentBodyConnector';
import styles from './ImportArtist.css';

class ImportArtist extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      contentBody: null,
      scrollTop: 0
    };
  }

  //
  // Control

  setContentBodyRef = (ref) => {
    this.setState({ contentBody: ref });
  }

  //
  // Listeners

  onQualityProfileIdChange = ({ value }) => {
    this.props.onInputChange({ name: 'qualityProfileId', value: parseInt(value) });
  }

  onLanguageProfileIdChange = ({ value }) => {
    this.props.onInputChange({ name: 'languageProfileId', value: parseInt(value) });
  }

  onMetadataProfileIdChange = ({ value }) => {
    this.props.onInputChange({ name: 'metadataProfileId', value: parseInt(value) });
  }

  onImportPress = () => {
    const {
      monitor,
      qualityProfileId,
      languageProfileId,
      metadataProfileId,
      albumFolder,
      tags
    } = this.props;

    this.props.onImportPress({
      metadataProfileId: metadataProfileId.value,
      qualityProfileId: qualityProfileId.value,
      languageProfileId: languageProfileId.value,
      albumFolder: albumFolder.value,
      monitored: monitor.value,
      tags: tags.value
    });
  }

  onScroll = ({ scrollTop }) => {
    this.setState({ scrollTop });
  }

  //
  // Render

  render() {
    const {
      rootFoldersFetching,
      rootFoldersPopulated,
      rootFoldersError,
      isAdding,
      monitor,
      qualityProfileId,
      languageProfileId,
      metadataProfileId,
      albumFolder,
      tags,
      showLanguageProfile,
      showMetadataProfile,
      onInputChange
    } = this.props;

    const {
      contentBody
    } = this.state;

    return (
      <PageContent title="Import Artist">
        <PageContentBodyConnector
          ref={this.setContentBodyRef}
          onScroll={this.onScroll}
        >
          {
            rootFoldersFetching && !rootFoldersPopulated &&
              <LoadingIndicator />
          }

          {
            !rootFoldersFetching && !!rootFoldersError &&
              <div>Unable to load root folders</div>
          }

          {
            !rootFoldersError && rootFoldersPopulated && contentBody &&
              <div>
                <Form>
                  <FormGroup>
                    <FormLabel>
                      Monitor

                      <Popover
                        anchor={
                          <Icon
                            className={styles.labelIcon}
                            name={icons.INFO}
                          />
                        }
                        title="Monitoring Options"
                        body={<ArtistMonitoringOptionsPopoverContent />}
                        position={tooltipPositions.RIGHT}
                      />
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.MONITOR_ALBUMS_SELECT}
                      name="monitor"
                      onChange={onInputChange}
                      {...monitor}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>Quality Profile</FormLabel>

                    <FormInputGroup
                      type={inputTypes.QUALITY_PROFILE_SELECT}
                      name="qualityProfileId"
                      onChange={this.onQualityProfileIdChange}
                      {...qualityProfileId}
                    />
                  </FormGroup>

                  <FormGroup className={showLanguageProfile ? undefined : styles.hideLanguageProfile}>
                    <FormLabel>Language Profile</FormLabel>

                    <FormInputGroup
                      type={inputTypes.LANGUAGE_PROFILE_SELECT}
                      name="languageProfileId"
                      onChange={this.onLanguageProfileIdChange}
                      {...languageProfileId}
                    />
                  </FormGroup>

                  <FormGroup className={showMetadataProfile ? undefined : styles.hideMetadataProfile}>
                    <FormLabel>Metadata Profile</FormLabel>

                    <FormInputGroup
                      type={inputTypes.METADATA_PROFILE_SELECT}
                      name="metadataProfileId"
                      onChange={this.onMetadataProfileIdChange}
                      {...metadataProfileId}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>Album Folder</FormLabel>

                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="albumFolder"
                      onChange={onInputChange}
                      {...albumFolder}
                    />
                  </FormGroup>

                  <FormGroup>
                    <FormLabel>Tags</FormLabel>

                    <FormInputGroup
                      type={inputTypes.TAG}
                      name="tags"
                      onChange={onInputChange}
                      {...tags}
                    />
                  </FormGroup>
                </Form>

                <SpinnerButton
                  className={styles.addButton}
                  kind={kinds.SUCCESS}
                  isSpinning={isAdding}
                  onPress={this.onImportPress}
                >
                  Import
                </SpinnerButton>
              </div>
          }
        </PageContentBodyConnector>
      </PageContent>
    );
  }
}

ImportArtist.propTypes = {
  rootFolderId: PropTypes.number.isRequired,
  rootFoldersFetching: PropTypes.bool.isRequired,
  rootFoldersPopulated: PropTypes.bool.isRequired,
  rootFoldersError: PropTypes.object,
  isAdding: PropTypes.bool.isRequired,
  monitor: PropTypes.object.isRequired,
  qualityProfileId: PropTypes.object,
  languageProfileId: PropTypes.object,
  metadataProfileId: PropTypes.object,
  albumFolder: PropTypes.object.isRequired,
  tags: PropTypes.object.isRequired,
  showLanguageProfile: PropTypes.bool.isRequired,
  showMetadataProfile: PropTypes.bool.isRequired,
  onInputChange: PropTypes.func.isRequired,
  onImportPress: PropTypes.func.isRequired
};

export default ImportArtist;
