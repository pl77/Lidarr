import _ from 'lodash';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import monitorOptions from 'Utilities/Artist/monitorOptions';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import getNewAlbum from 'Utilities/Album/getNewAlbum';
import { createThunk, handleThunks } from 'Store/thunks';
import createSetSettingValueReducer from './Creators/Reducers/createSetSettingValueReducer';
import createHandleActions from './Creators/createHandleActions';
import { set, update, updateItem } from './baseActions';

//
// Variables

export const section = 'addAlbum';
let abortCurrentRequest = null;

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  isAdding: false,
  isAdded: false,
  addError: null,
  items: [],

  defaults: {
    rootFolderPath: '',
    monitor: monitorOptions[0].key,
    qualityProfileId: 0,
    languageProfileId: 0,
    metadataProfileId: 0,
    albumFolder: true,
    tags: []
  }
};

export const persistState = [
  'addAlbum.defaults'
];

//
// Actions Types

export const LOOKUP_ALBUM = 'addAlbum/lookupAlbum';
export const ADD_ALBUM = 'addAlbum/addAlbum';
export const SET_ADD_ALBUM_VALUE = 'addAlbum/setAddAlbumValue';
export const CLEAR_ADD_ALBUM = 'addAlbum/clearAddAlbum';
export const SET_ADD_ALBUM_DEFAULT = 'addAlbum/setAddAlbumDefault';

//
// Action Creators

export const lookupAlbum = createThunk(LOOKUP_ALBUM);
export const addAlbum = createThunk(ADD_ALBUM);
export const clearAddAlbum = createAction(CLEAR_ADD_ALBUM);
export const setAddAlbumDefault = createAction(SET_ADD_ALBUM_DEFAULT);

export const setAddAlbumValue = createAction(SET_ADD_ALBUM_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

//
// Action Handlers

export const actionHandlers = handleThunks({

  [LOOKUP_ALBUM]: function(getState, payload, dispatch) {
    dispatch(set({ section, isFetching: true }));

    if (abortCurrentRequest) {
      abortCurrentRequest();
    }

    const { request, abortRequest } = createAjaxRequest({
      url: '/album/lookup',
      data: {
        term: payload.term
      }
    });

    abortCurrentRequest = abortRequest;

    request.done((data) => {
      dispatch(batchActions([
        update({ section, data }),

        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null
        })
      ]));
    });

    request.fail((xhr) => {
      dispatch(set({
        section,
        isFetching: false,
        isPopulated: false,
        error: xhr.aborted ? null : xhr
      }));
    });
  },

  [ADD_ALBUM]: function(getState, payload, dispatch) {
    dispatch(set({ section, isAdding: true }));

    const foreignAlbumId = payload.foreignAlbumId;
    const items = getState().addAlbum.items;

    const newAlbum = getNewAlbum(_.cloneDeep(_.find(items, { foreignAlbumId })), payload);

    const promise = createAjaxRequest({
      url: '/album',
      method: 'POST',
      contentType: 'application/json',
      data: JSON.stringify(newAlbum)
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        updateItem({
          section,
          idField: 'foreignAlbumId',
          ...data
        }),

        set({
          section,
          isAdding: false,
          isAdded: true,
          addError: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isAdding: false,
        isAdded: false,
        addError: xhr
      }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_ADD_ALBUM_VALUE]: createSetSettingValueReducer(section),

  [SET_ADD_ALBUM_DEFAULT]: function(state, { payload }) {
    const newState = getSectionState(state, section);

    newState.defaults = {
      ...newState.defaults,
      ...payload
    };

    return updateSectionState(state, section, newState);
  },

  [CLEAR_ADD_ALBUM]: function(state) {
    const {
      defaults,
      ...otherDefaultState
    } = defaultState;

    return Object.assign({}, state, otherDefaultState);
  }

}, defaultState, section);
