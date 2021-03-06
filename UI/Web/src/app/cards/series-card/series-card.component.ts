import { Component, EventEmitter, Input, OnChanges, OnInit, Output } from '@angular/core';
import { Router } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { take } from 'rxjs/operators';
import { Series } from 'src/app/_models/series';
import { AccountService } from 'src/app/_services/account.service';
import { ImageService } from 'src/app/_services/image.service';
import { ActionFactoryService, Action, ActionItem } from 'src/app/_services/action-factory.service';
import { SeriesService } from 'src/app/_services/series.service';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { ActionService } from 'src/app/_services/action.service';
import { EditSeriesModalComponent } from '../_modals/edit-series-modal/edit-series-modal.component';

@Component({
  selector: 'app-series-card',
  templateUrl: './series-card.component.html',
  styleUrls: ['./series-card.component.scss']
})
export class SeriesCardComponent implements OnInit, OnChanges {
  @Input() data!: Series;
  @Input() libraryId = 0;
  @Input() suppressLibraryLink = false;
  @Output() clicked = new EventEmitter<Series>();
  @Output() reload = new EventEmitter<boolean>();
  @Output() dataChanged = new EventEmitter<Series>();

  isAdmin = false;
  actions: ActionItem<Series>[] = [];
  imageUrl: string = '';

  constructor(private accountService: AccountService, private router: Router,
              private seriesService: SeriesService, private toastr: ToastrService,
              private modalService: NgbModal, private confirmService: ConfirmService, 
              public imageService: ImageService, private actionFactoryService: ActionFactoryService,
              private actionService: ActionService) {
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
      }
    });
  }


  ngOnInit(): void {
    if (this.data) {
      this.imageUrl = this.imageService.randomize(this.imageService.getSeriesCoverImage(this.data.id));
    }
  }

  ngOnChanges(changes: any) {
    if (this.data) {
      this.actions = this.actionFactoryService.getSeriesActions((action: Action, series: Series) => this.handleSeriesActionCallback(action, series)).filter(action => this.actionFactoryService.filterBookmarksForFormat(action, this.data));
      this.imageUrl = this.imageService.randomize(this.imageService.getSeriesCoverImage(this.data.id));
    }
  }

  handleSeriesActionCallback(action: Action, series: Series) {
    switch (action) {
      case(Action.MarkAsRead):
        this.markAsRead(series);
        break;
      case(Action.MarkAsUnread):
        this.markAsUnread(series);
        break;
      case(Action.ScanLibrary):
        this.scanLibrary(series);
        break;
      case(Action.RefreshMetadata):
        this.refreshMetdata(series);
        break;
      case(Action.Delete):
        this.deleteSeries(series);
        break;
      case(Action.Edit):
        this.openEditModal(series);
        break;
      case(Action.Bookmarks):
        this.actionService.openBookmarkModal(series, (series) => {/* No Operation */ });
        break;
      default:
        break;
    }
  }

  openEditModal(data: Series) {
    const modalRef = this.modalService.open(EditSeriesModalComponent, {  size: 'lg' });
    modalRef.componentInstance.series = data;
    modalRef.closed.subscribe((closeResult: {success: boolean, series: Series, coverImageUpdate: boolean}) => {
      window.scrollTo(0, 0);
      if (closeResult.success) {
        if (closeResult.coverImageUpdate) {
          this.imageUrl = this.imageService.randomize(this.imageService.getSeriesCoverImage(closeResult.series.id));
          console.log('image url: ', this.imageUrl);
        }
        this.seriesService.getSeries(data.id).subscribe(series => {
          this.data = series;
          this.reload.emit(true);
          this.dataChanged.emit(series);
        });
      }
    });
  }

  refreshMetdata(series: Series) {
    this.seriesService.refreshMetadata(series).subscribe((res: any) => {
      this.toastr.success('???????????? ' + series.name);
    });
  }

  scanLibrary(series: Series) {
    this.seriesService.scan(series.libraryId, series.id).subscribe((res: any) => {
      this.toastr.success('???????????? ' + series.name);
    });
  }

  async deleteSeries(series: Series) {
    if (!await this.confirmService.confirm('?????????????????????????????? ?????????????????????????????????.')) {
      return;
    }

    this.seriesService.delete(series.id).subscribe((res: boolean) => {
      if (res) {
        this.toastr.success('???????????????');
        this.reload.emit(true);
      }
    });
  }

  markAsUnread(series: Series) {
    this.seriesService.markUnread(series.id).subscribe(res => {
      this.toastr.success(series.name + ' ??????');
      series.pagesRead = 0;
      if (this.data) {
        this.data.pagesRead = 0;
      }
      
      this.dataChanged.emit(series);
    });
  }

  markAsRead(series: Series) {
    this.seriesService.markRead(series.id).subscribe(res => {
      this.toastr.success(series.name + ' ??????');
      series.pagesRead = series.pages;
      if (this.data) {
        this.data.pagesRead = series.pages;
      }
      this.dataChanged.emit(series);
    });
  }

  handleClick() {
    this.clicked.emit(this.data);
    this.router.navigate(['library', this.libraryId, 'series', this.data?.id]);
  }

}
