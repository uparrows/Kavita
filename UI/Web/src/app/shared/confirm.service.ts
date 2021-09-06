import { Injectable } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { take } from 'rxjs/operators';
import { ConfirmDialogComponent } from './confirm-dialog/confirm-dialog.component';
import { ConfirmConfig } from './confirm-dialog/_models/confirm-config';

@Injectable({
  providedIn: 'root'
})
export class ConfirmService {

  defaultConfirm = new ConfirmConfig();
  defaultAlert = new ConfirmConfig();

  constructor(private modalService: NgbModal) {
    this.defaultConfirm.buttons.push({text: '取消', type: 'secondary'});
    this.defaultConfirm.buttons.push({text: '确认', type: 'primary'});

    this.defaultAlert._type = 'alert';
    this.defaultAlert.header = '警告';
    this.defaultAlert.buttons.push({text: '好的', type: 'primary'});

  }

  public async confirm(content?: string, config?: ConfirmConfig): Promise<boolean> {

    return new Promise((resolve, reject) => {
      if (content === undefined && config === undefined) {
        console.error('确认必须传递文本或配置对象');
        return reject(false);
      }

      if (content !== undefined && config === undefined) {
        config = this.defaultConfirm;
        config.content = content;
      }

      const modalRef = this.modalService.open(ConfirmDialogComponent);
      modalRef.componentInstance.config = config;
      modalRef.closed.pipe(take(1)).subscribe(result => {
        return resolve(result);
      });
      modalRef.dismissed.pipe(take(1)).subscribe(() => {
        return reject(false);
      });
    });

  }

  public async alert(content?: string, config?: ConfirmConfig): Promise<boolean> {
    return new Promise((resolve, reject) => {
      if (content === undefined && config === undefined) {
        console.error('警报必须传递文本或配置对象');
        return reject(false);
      }

      if (content !== undefined && config === undefined) {
        config = this.defaultConfirm;
        config.content = content;
      }

      const modalRef = this.modalService.open(ConfirmDialogComponent);
      modalRef.componentInstance.config = config;
      modalRef.closed.pipe(take(1)).subscribe(result => {
        return resolve(result);
      });
      modalRef.dismissed.pipe(take(1)).subscribe(() => {
        return reject(false);
      });
    })
  }
}
