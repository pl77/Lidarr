import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import parseUrl from 'Utilities/String/parseUrl';
import { lookupAlbum, clearAddAlbum } from 'Store/Actions/addAlbumActions';
import { fetchRootFolders } from 'Store/Actions/rootFolderActions';
import AddNewAlbum from './AddNewAlbum';

function createMapStateToProps() {
  return createSelector(
    (state) => state.addAlbum,
    (state) => state.router.location,
    (addAlbum, location) => {
      const { params } = parseUrl(location.search);

      return {
        term: params.term,
        ...addAlbum
      };
    }
  );
}

const mapDispatchToProps = {
  lookupAlbum,
  clearAddAlbum,
  fetchRootFolders
};

class AddNewAlbumConnector extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this._artistLookupTimeout = null;
  }

  componentDidMount() {
    this.props.fetchRootFolders();
  }

  componentWillUnmount() {
    if (this._artistLookupTimeout) {
      clearTimeout(this._artistLookupTimeout);
    }

    this.props.clearAddAlbum();
  }

  //
  // Listeners

  onAlbumLookupChange = (term) => {
    if (this._artistLookupTimeout) {
      clearTimeout(this._artistLookupTimeout);
    }

    if (term.trim() === '') {
      this.props.clearAddAlbum();
    } else {
      this._artistLookupTimeout = setTimeout(() => {
        this.props.lookupAlbum({ term });
      }, 300);
    }
  }

  onClearAlbumLookup = () => {
    this.props.clearAddAlbum();
  }

  //
  // Render

  render() {
    const {
      term,
      ...otherProps
    } = this.props;

    return (
      <AddNewAlbum
        term={term}
        {...otherProps}
        onAlbumLookupChange={this.onAlbumLookupChange}
        onClearAlbumLookup={this.onClearAlbumLookup}
      />
    );
  }
}

AddNewAlbumConnector.propTypes = {
  term: PropTypes.string,
  lookupAlbum: PropTypes.func.isRequired,
  clearAddAlbum: PropTypes.func.isRequired,
  fetchRootFolders: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AddNewAlbumConnector);
