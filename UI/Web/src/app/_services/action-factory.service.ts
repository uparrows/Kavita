import { Injectable } from '@angular/core';
import { Chapter } from '../_models/chapter';
import { CollectionTag } from '../_models/collection-tag';
import { Library } from '../_models/library';
import { MangaFormat } from '../_models/manga-format';
import { Series } from '../_models/series';
import { Volume } from '../_models/volume';
import { AccountService } from './account.service';

export enum Action {
  MarkAsRead = 0,
  MarkAsUnread = 1,
  ScanLibrary = 2,
  Delete = 3,
  Edit = 4,
  Info = 5,
  RefreshMetadata = 6,
  Download = 7,
  Bookmarks = 8
}

export interface ActionItem<T> {
  title: string;
  action: Action;
  callback: (action: Action, data: T) => void;
  requiresAdmin: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ActionFactoryService {

  libraryActions: Array<ActionItem<Library>> = [];

  seriesActions: Array<ActionItem<Series>> = [];

  volumeActions: Array<ActionItem<Volume>> = [];

  chapterActions: Array<ActionItem<Chapter>> = [];

  collectionTagActions: Array<ActionItem<CollectionTag>> = [];

  isAdmin = false;
  hasDownloadRole = false;

  constructor(private accountService: AccountService) {
    this.accountService.currentUser$.subscribe(user => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
        this.hasDownloadRole = this.accountService.hasDownloadRole(user);
      } else {
        this._resetActions();
        return; // If user is logged out, we don't need to do anything
      }

      this._resetActions();

      if (this.isAdmin) {
        this.collectionTagActions.push({
          action: Action.Edit,
          title: '编辑',
          callback: this.dummyCallback,
          requiresAdmin: true
        });

        this.seriesActions.push({
          action: Action.ScanLibrary,
          title: '扫描系列',
          callback: this.dummyCallback,
          requiresAdmin: true
        });

        this.seriesActions.push({
          action: Action.RefreshMetadata,
          title: '刷新元数据',
          callback: this.dummyCallback,
          requiresAdmin: true
        });

        this.seriesActions.push({
          action: Action.Delete,
          title: '删除',
          callback: this.dummyCallback,
          requiresAdmin: true
        });

        this.seriesActions.push({
          action: Action.Edit,
          title: '编辑',
          callback: this.dummyCallback,
          requiresAdmin: true
        });

        this.libraryActions.push({
          action: Action.ScanLibrary,
          title: '扫描库',
          callback: this.dummyCallback,
          requiresAdmin: true
        });

        this.libraryActions.push({
          action: Action.RefreshMetadata,
          title: '刷新元数据',
          callback: this.dummyCallback,
          requiresAdmin: true
        });
    
        this.chapterActions.push({
          action: Action.Edit,
          title: '编辑',
          callback: this.dummyCallback,
          requiresAdmin: false
        });
      }

      if (this.hasDownloadRole || this.isAdmin) {
        this.volumeActions.push({
          action: Action.Download,
          title: '下载',
          callback: this.dummyCallback,
          requiresAdmin: false
        });

        this.chapterActions.push({
          action: Action.Download,
          title: '下载',
          callback: this.dummyCallback,
          requiresAdmin: false
        });
      }
    });
  }

  getLibraryActions(callback: (action: Action, library: Library) => void) {
    this.libraryActions.forEach(action => action.callback = callback);
    return this.libraryActions;
  }

  getSeriesActions(callback: (action: Action, series: Series) => void) {
    this.seriesActions.forEach(action => action.callback = callback);
    return this.seriesActions;
  }

  getVolumeActions(callback: (action: Action, volume: Volume) => void) {
    this.volumeActions.forEach(action => action.callback = callback);
    return this.volumeActions;
  }

  getChapterActions(callback: (action: Action, chapter: Chapter) => void) {
    this.chapterActions.forEach(action => action.callback = callback);
    return this.chapterActions;
  }

  getCollectionTagActions(callback: (action: Action, collectionTag: CollectionTag) => void) {
    this.collectionTagActions.forEach(action => action.callback = callback);
    return this.collectionTagActions;
  }

  filterBookmarksForFormat(action: ActionItem<Series>, series: Series) {
    if (action.action === Action.Bookmarks && series?.format === MangaFormat.EPUB) return false;
    return true;
  }

  dummyCallback(action: Action, data: any) {}

  _resetActions() {
    this.libraryActions = [];

    this.collectionTagActions = [];
    
    this.seriesActions = [
      {
        action: Action.MarkAsRead,
        title: '标记已读',
        callback: this.dummyCallback,
          requiresAdmin: false
      },
      {
        action: Action.MarkAsUnread,
        title: '标记未读',
        callback: this.dummyCallback,
          requiresAdmin: false
      }, 
      {
        action: Action.Bookmarks,
        title: '书签',
        callback: this.dummyCallback,
          requiresAdmin: false
      }
    ];

    this.volumeActions = [
      {
        action: Action.MarkAsRead,
        title: '标记已读',
        callback: this.dummyCallback,
          requiresAdmin: false
      },
      {
        action: Action.MarkAsUnread,
        title: '标记未读',
        callback: this.dummyCallback,
        requiresAdmin: false
      },
      {
        action: Action.Edit,
        title: '信息',
        callback: this.dummyCallback,
        requiresAdmin: false
      }
    ];

    this.chapterActions = [
      {
        action: Action.MarkAsRead,
        title: '标记已读',
        callback: this.dummyCallback,
        requiresAdmin: false
      },
      {
        action: Action.MarkAsUnread,
        title: '标记未读',
        callback: this.dummyCallback,
        requiresAdmin: false
      }
    ];
  }
}
