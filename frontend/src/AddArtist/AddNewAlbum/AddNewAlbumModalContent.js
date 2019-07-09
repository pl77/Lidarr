import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import TextTruncate from 'react-text-truncate';
import { kinds, inputTypes, icons } from 'Helpers/Props';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import SpinnerButton from 'Components/Link/SpinnerButton';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormLabel from 'Components/Form/FormLabel';
import FormInputGroup from 'Components/Form/FormInputGroup';
import CheckInput from 'Components/Form/CheckInput';
import ModalContent from 'Components/Modal/ModalContent';
import ModalHeader from 'Components/Modal/ModalHeader';
import ModalBody from 'Components/Modal/ModalBody';
import ModalFooter from 'Components/Modal/ModalFooter';
import ArtistPoster from 'Artist/ArtistPoster';
import styles from './AddNewAlbumModalContent.css';

class AddNewAlbumModalContent extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      searchForNewAlbum: false,
      expandReleases: false
    };
  }

  //
  // Listeners

  onSearchForNewAlbumChange = ({ value }) => {
    this.setState({ searchForNewAlbum: value });
  }

  onQualityProfileIdChange = ({ value }) => {
    this.props.onInputChange({ name: 'qualityProfileId', value: parseInt(value) });
  }

  onLanguageProfileIdChange = ({ value }) => {
    this.props.onInputChange({ name: 'languageProfileId', value: parseInt(value) });
  }

  onMetadataProfileIdChange = ({ value }) => {
    this.props.onInputChange({ name: 'metadataProfileId', value: parseInt(value) });
  }

  onExpandReleasesPress = () => {
    this.setState((prevState) => ({
      expandReleases: !prevState.expandReleases
    }));
  }

  onAddAlbumPress = () => {
    this.props.onAddAlbumPress(this.state.searchForNewAlbum);
  }

  //
  // Render

  render() {
    const {
      albumTitle,
      artistName,
      disambiguation,
      overview,
      images,
      releases,
      isAdding,
      isExistingArtist,
      rootFolderPath,
      monitor,
      qualityProfileId,
      languageProfileId,
      metadataProfileId,
      albumFolder,
      tags,
      showLanguageProfile,
      showMetadataProfile,
      isSmallScreen,
      onModalClose,
      onInputChange,
      ...otherProps
    } = this.props;

    const {
      expandReleases
    } = this.state;

    const columns = [
      {
        name: 'title',
        label: 'Title',
        isSortable: true,
        isVisible: true
      },
      {
        name: 'format',
        label: 'Format',
        isSortable: true,
        isVisible: true
      },
      {
        name: 'tracks',
        label: 'Tracks',
        isSortable: true,
        isVisible: true
      },
      {
        name: 'country',
        label: 'Country',
        isSortable: true,
        isVisible: true
      },
      {
        name: 'label',
        label: 'Label',
        isSortable: true,
        isVisible: true
      }
    ];

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          Add new Album
        </ModalHeader>

        <ModalBody>
          <div className={styles.container}>
            {
              isSmallScreen ?
                null:
                <div className={styles.poster}>
                  <ArtistPoster
                    className={styles.poster}
                    images={images}
                    size={250}
                  />
                </div>
            }

            <div className={styles.info}>
              <div className={styles.name}>
                {albumTitle}
              </div>

              {
                !!disambiguation &&
                  <span className={styles.disambiguation}>({disambiguation})</span>
              }

              <div>
                <span className={styles.artistName}> By: {artistName}</span>
              </div>

              {
                overview ?
                  <div className={styles.overview}>
                    <TextTruncate
                      truncateText="…"
                      line={8}
                      text={overview}
                    />
                  </div> :
                  null
              }

              <div
                className={styles.albumType}
              >
                <Link
                  className={styles.expandButton}
                  onPress={this.onExpandReleasesPress}
                >
                  <div className={styles.header}>
                    <div className={styles.left}>
                      {
                        <div>
                          <span className={styles.albumTypeLabel}>
                            Releases
                          </span>

                          <span className={styles.albumCount}>
                            ({releases.length} versions)
                          </span>
                        </div>
                      }

                    </div>

                    <Icon
                      className={styles.expandButtonIcon}
                      name={expandReleases ? icons.COLLAPSE : icons.EXPAND}
                      title={expandReleases ? 'Hide releases' : 'Show releases'}
                      size={24}
                    />

                    {
                      !isSmallScreen &&
                        <span>&nbsp;</span>
                    }

                  </div>
                </Link>

                {
                  expandReleases &&
                    <Table
                      columns={columns}
                    >
                      <TableBody>
                        {
                          releases.map((item, i) => {
                            return (
                              <TableRow key={i}>
                                <TableRowCell key='title'>
                                  {item.title}
                                </TableRowCell>
                                <TableRowCell key='format'>
                                  {item.format}
                                </TableRowCell>
                                <TableRowCell key='tracks'>
                                  {item.trackCount}
                                </TableRowCell>
                                <TableRowCell key='country'>
                                  {_.join(item.country, ', ')}
                                </TableRowCell>
                                <TableRowCell key='label'>
                                  {_.join(item.label, ', ')}
                                </TableRowCell>
                              </TableRow>
                            );
                          })
                        }
                      </TableBody>
                    </Table>
                }
              </div>

              {
                !isExistingArtist &&
                  <Form {...otherProps}>
                    <FieldSet
                      legend={`Artist Options for ${artistName}`}
                    >

                      <FormGroup>
                        <FormLabel>Root Folder</FormLabel>

                        <FormInputGroup
                          type={inputTypes.ROOT_FOLDER_SELECT}
                          name="rootFolderPath"
                          onChange={onInputChange}
                          {...rootFolderPath}
                        />
                      </FormGroup>

                      <FormGroup>
                        <FormLabel>
                          Monitor
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
                    </FieldSet>
                  </Form>
              }
            </div>
          </div>
        </ModalBody>

        <ModalFooter className={styles.modalFooter}>
          <label className={styles.searchForNewAlbumLabelContainer}>
            <span className={styles.searchForNewAlbumLabel}>
              Start search for new album
            </span>

            <CheckInput
              containerClassName={styles.searchForNewAlbumContainer}
              className={styles.searchForNewAlbumInput}
              name="searchForNewAlbum"
              value={this.state.searchForNewAlbum}
              onChange={this.onSearchForNewAlbumChange}
            />
          </label>

          <SpinnerButton
            className={styles.addButton}
            kind={kinds.SUCCESS}
            isSpinning={isAdding}
            onPress={this.onAddAlbumPress}
          >
            Add {albumTitle}
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    );
  }
}

AddNewAlbumModalContent.propTypes = {
  albumTitle: PropTypes.string.isRequired,
  artistName: PropTypes.string.isRequired,
  disambiguation: PropTypes.string.isRequired,
  overview: PropTypes.string,
  images: PropTypes.arrayOf(PropTypes.object).isRequired,
  releases: PropTypes.arrayOf(PropTypes.object).isRequired,
  isAdding: PropTypes.bool.isRequired,
  addError: PropTypes.object,
  isExistingArtist: PropTypes.bool.isRequired,
  rootFolderPath: PropTypes.object,
  monitor: PropTypes.object.isRequired,
  qualityProfileId: PropTypes.object,
  languageProfileId: PropTypes.object,
  metadataProfileId: PropTypes.object,
  albumFolder: PropTypes.object.isRequired,
  tags: PropTypes.object.isRequired,
  showLanguageProfile: PropTypes.bool.isRequired,
  showMetadataProfile: PropTypes.bool.isRequired,
  isSmallScreen: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onInputChange: PropTypes.func.isRequired,
  onAddAlbumPress: PropTypes.func.isRequired
};

export default AddNewAlbumModalContent;
