import PropTypes from 'prop-types';
import React from 'react';
import formatBytes from 'Utilities/Number/formatBytes';
import { icons } from 'Helpers/Props';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import TableRow from 'Components/Table/TableRow';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import styles from './RootFolderRow.css';

function RootFolderRow(props) {
  const {
    id,
    path,
    freeSpace,
    unmappedArtists,
    onDeletePress
  } = props;

  const unmappedArtistsCount = unmappedArtists.length || '-';

  return (
    <TableRow>
      <TableRowCell>
        <Link
          className={styles.link}
          to={`/add/import/${id}`}
        >
          {path}
        </Link>
      </TableRowCell>

      <TableRowCell className={styles.freeSpace}>
        {formatBytes(freeSpace) || '-'}
      </TableRowCell>

      <TableRowCell className={styles.unmappedArtists}>
        {unmappedArtistsCount}
      </TableRowCell>

      <TableRowCell className={styles.actions}>
        <IconButton
          title="Remove root folder"
          name={icons.REMOVE}
          onPress={onDeletePress}
        />
      </TableRowCell>
    </TableRow>
  );
}

RootFolderRow.propTypes = {
  id: PropTypes.number.isRequired,
  path: PropTypes.string.isRequired,
  freeSpace: PropTypes.number.isRequired,
  unmappedArtists: PropTypes.arrayOf(PropTypes.object).isRequired,
  onDeletePress: PropTypes.func.isRequired
};

RootFolderRow.defaultProps = {
  freeSpace: 0,
  unmappedArtists: []
};

export default RootFolderRow;
